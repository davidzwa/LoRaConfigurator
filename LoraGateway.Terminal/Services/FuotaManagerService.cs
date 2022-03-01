using System.ComponentModel.DataAnnotations;
using LoraGateway.Models;
using LoraGateway.Services.Contracts;
using LoraGateway.Services.Firmware;
using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Services;

public class FuotaManagerService : JsonDataStore<FuotaConfig>
{
    private readonly ILogger<FuotaManagerService> _logger;
    private readonly BlobFragmentationService _blobFragmentationService;
    private readonly RlncEncodingService _rlncEncodingService;
    private FuotaSession? _currentFuotaSession;
    private List<UnencodedPacket> _firmwarePackets = new();

    public FuotaManagerService(
        ILogger<FuotaManagerService> logger,
        BlobFragmentationService blobFragmentationService,
        RlncEncodingService rlncEncodingService
    )
    {
        _logger = logger;
        _blobFragmentationService = blobFragmentationService;
        _rlncEncodingService = rlncEncodingService;
    }

    public override string GetJsonFileName()
    {
        return "fuota_config.json";
    }

    public bool IsFuotaSessionEnabled()
    {
        return _currentFuotaSession != null;
    }

    public FuotaSession GetCurrentSession()
    {
        if (_currentFuotaSession == null) throw new ValidationException("Cant provide fuota session as its unset");

        return _currentFuotaSession;
    }

    public async Task<FuotaSession> PrepareFuotaSession()
    {
        if (Store == null)
            throw new ValidationException(
                "Fuota config store was not loaded, yet PrepareFuotaSession was called - call LoadStore first");

        if (_firmwarePackets.Count != 0)
            throw new ValidationException(
                "A new fuota session was started, while a previous one was already requested");

        if (Store.UartFakeLoRaRxMode)
        {
            _logger.LogWarning("UartFakeLoRaRxMode enabled");
        }

        if (Store.FakeFirmware)
        {
            var frameSize = (int) Store.FakeFragmentSize;
            var firmwareSize = Store.FakeFragmentCount * frameSize;
            _firmwarePackets = await _blobFragmentationService.GenerateFakeFirmwareAsync(firmwareSize, frameSize);
        }

        // Prepare 
        _rlncEncodingService.ConfigureEncoding(new EncodingConfiguration
        {
            Seed = (byte) Store.LfsrSeed,
            CurrentGeneration = 0,
            FieldDegree = Store.FieldDegree,
            GenerationSize = Store.GenerationSize
        });
        var generationCount =
            (uint) _rlncEncodingService.PreprocessGenerations(_firmwarePackets, Store.GenerationSize);

        _currentFuotaSession = new FuotaSession(Store, generationCount);
        _currentFuotaSession.TotalFragmentCount = (uint) _firmwarePackets.Count;
        return _currentFuotaSession;
    }

    public List<byte>? FetchNextRlncPayload()
    {
        if (_currentFuotaSession == null)
        {
            throw new ValidationException("Cant fetch RLNC payload when session is null");
        }
        
        var config = _currentFuotaSession.Config;
        var maxGenerationFragments = config.GenerationSize + config.GenerationSizeRedundancy;
        var fragmentCount = _currentFuotaSession.CurrentFragmentIndex + 1;
        if (fragmentCount >= maxGenerationFragments)
        {
            if (!_rlncEncodingService.HasNextGeneration())
            {
                return null;
            }
            
            // Increment generation, reset fragment index
            _currentFuotaSession.IncrementGenerationIndex();
            
            _rlncEncodingService.MoveNextGeneration();
        }
        
        var encodedPacket = _rlncEncodingService.PrecodeNumberOfPackets(1, false).First();
        var fragmentBytes = encodedPacket.Payload.Select(p => p.GetValue());
        
        _currentFuotaSession.IncrementFragmentIndex();

        return fragmentBytes.ToList();
    }

    public void LogSessionProgress()
    {
        if (!IsFuotaSessionEnabled())
        {
            _logger.LogInformation("Fuota session stopped - no progress");
            return;
        }

        var session = GetCurrentSession();
        var currentGen = session.CurrentGenerationIndex + 1;
        var maxGen = session.GenerationCount;
        var fragment = (session.Config.GenerationSize * (currentGen-1)) + (session.CurrentFragmentIndex + 1);
        var fragmentMax = session.TotalFragmentCount + maxGen * session.Config.GenerationSizeRedundancy;
        var progress = Math.Round(100.0 * fragment / fragmentMax, 1);
        
        _logger.LogInformation("Progress {Progress}% Gen {Gen}/{MaxGen} Fragment {Frag}/{MaxFrag}",
            progress, currentGen, maxGen, fragment, fragmentMax);
    }

    public void ClearFuotaSession()
    {
        if (!IsFuotaSessionEnabled())
        {
            throw new ValidationException("Cant stop FUOTA session when none is enabled");
        }

        _firmwarePackets = new List<UnencodedPacket>();
        _currentFuotaSession = null;
        _rlncEncodingService.ResetEncoding();
    }

    public override FuotaConfig GetDefaultJson()
    {
        var jsonStore = new FuotaConfig();

        // Adjust anything deviating from default here

        return jsonStore;
    }
}