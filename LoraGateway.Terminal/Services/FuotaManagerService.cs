using System.ComponentModel.DataAnnotations;
using JKang.EventBus;
using LoraGateway.Handlers;
using LoraGateway.Models;
using LoraGateway.Services.Contracts;
using LoraGateway.Services.Firmware;
using LoraGateway.Services.Firmware.RandomLinearCoding;
using LoraGateway.Utils;

namespace LoraGateway.Services;

public class FuotaManagerService : JsonDataStore<FuotaConfig>
{
    private readonly BlobFragmentationService _blobFragmentationService;

    private readonly CancellationTokenSource _cancellation = new();
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<FuotaManagerService> _logger;
    private readonly RlncEncodingService _rlncEncodingService;
    private FuotaSession? _currentFuotaSession;
    private List<UnencodedPacket> _firmwarePackets = new();

    public FuotaManagerService(
        ILogger<FuotaManagerService> logger,
        IEventPublisher eventPublisher,
        BlobFragmentationService blobFragmentationService,
        RlncEncodingService rlncEncodingService
    )
    {
        _logger = logger;
        _eventPublisher = eventPublisher;
        _blobFragmentationService = blobFragmentationService;
        _rlncEncodingService = rlncEncodingService;
    }

    public async Task HandleRlncConsoleCommand()
    {
        if (Store == null) await LoadStore();

        if (!IsFuotaSessionEnabled())
            await StartFuotaSession();
        else
            await StopFuotaSession();
    }

    public override string GetJsonFileName()
    {
        return "fuota_config.json";
    }

    public bool IsFuotaSessionEnabled()
    {
        return _currentFuotaSession != null;
    }

    public bool IsLastGeneration()
    {
        if (_currentFuotaSession == null) return true;

        var currentGen = _currentFuotaSession.CurrentGenerationIndex + 1;
        var maxGen = _currentFuotaSession.GenerationCount;

        return currentGen == maxGen;
    }

    public bool IsCurrentGenerationComplete()
    {
        if (_currentFuotaSession == null) return true;

        var config = _currentFuotaSession.Config;

        var maxGenerationFragments = config.GenerationSize + config.GenerationSizeRedundancy;
        var fragmentIndex = _currentFuotaSession.CurrentFragmentIndex;

        return fragmentIndex >= maxGenerationFragments;
    }

    public bool IsFuotaSessionDone()
    {
        return IsCurrentGenerationComplete() && IsLastGeneration();
    }

    public FuotaSession GetCurrentSession()
    {
        if (_currentFuotaSession == null) throw new ValidationException("Cant provide fuota session as its unset");

        return _currentFuotaSession;
    }

    public async Task StartFuotaSession()
    {
        _logger.LogInformation("Starting FUOTA session");

        await PrepareFuotaSession();

        var result = _cancellation.TryReset();
        if (!result) _logger.LogWarning("Resetting of FUOTA cancellation source failed. Continuing anyway");

        await _eventPublisher.PublishEventAsync(new InitFuotaSession {Message = "Initiating"});
    }

    public async Task StopFuotaSession()
    {
        _logger.LogInformation("Stopping FUOTA session");
        _cancellation.Cancel();

        // Termination imminent - clear the session and terminate the hosted service
        await _eventPublisher.PublishEventAsync(new StopFuotaSession {Message = "Stopping"});

        ClearFuotaSession();
    }

    private async Task PrepareFuotaSession()
    {
        if (Store == null)
            throw new ValidationException(
                "Fuota config store was not loaded, yet PrepareFuotaSession was called - call LoadStore first");

        if (_firmwarePackets.Count != 0)
            throw new ValidationException(
                "A new fuota session was started, while a previous one was already requested");

        if (Store.UartFakeLoRaRxMode) _logger.LogWarning("UartFakeLoRaRxMode enabled");

        if (Store.FakeFirmware)
        {
            var frameSize = (int) Store.FakeFragmentSize;
            var firmwareSize = Store.FakeFragmentCount * frameSize;
            _firmwarePackets = await _blobFragmentationService.GenerateFakeFirmwareAsync(firmwareSize, frameSize);
            foreach (var packetIndex in Enumerable.Range(0, _firmwarePackets.Count))
            {
                var packet = _firmwarePackets[packetIndex];
                var packetSerialized = SerialUtil.ByteArrayToString(packet.Payload
                    .Select(b => b.GetValue())
                    .ToArray());
                _logger.LogInformation("OriginalPacket {Packet}", packetSerialized);
            }
        }

        // Prepare 
        _rlncEncodingService.ConfigureEncoding(new EncodingConfiguration
        {
            Seed = (byte) Store.LfsrSeed,
            CurrentGeneration = 0,
            FieldDegree = Store.FieldDegree,
            GenerationSize = Store.GenerationSize
        });
        var genCountResult =
            (uint) _rlncEncodingService.PreprocessGenerations(_firmwarePackets, Store.GenerationSize);

        _currentFuotaSession = new FuotaSession(Store, genCountResult)
        {
            TotalFragmentCount = (uint) _firmwarePackets.Count
        };
    }

    public void LogSessionProgress()
    {
        if (!IsFuotaSessionEnabled())
        {
            _logger.LogInformation("Fuota session stopped - no progress to log");
            return;
        }

        if (IsCurrentGenerationComplete())
        {
            _logger.LogInformation("Generation {GenIndex} complete - switching to next generation",
                _currentFuotaSession!.CurrentGenerationIndex + 1);
            return;
        }

        var session = GetCurrentSession();
        var currentGen = session.CurrentGenerationIndex + 1;
        var maxGen = session.GenerationCount;
        var genTotal = session.Config.GenerationSize + session.Config.GenerationSizeRedundancy;
        var fragment = genTotal * (currentGen - 1) + session.CurrentFragmentIndex + 1;
        var fragmentMax = session.TotalFragmentCount + maxGen * session.Config.GenerationSizeRedundancy;
        var progress = Math.Round(100.0 * fragment / fragmentMax, 1);

        _logger.LogInformation("Progress {Progress}% Gen {Gen}/{MaxGen} Fragment {Frag}/{MaxFrag}",
            progress, currentGen, maxGen, fragment, fragmentMax);
    }

    public void MoveNextRlncGeneration()
    {
        if (_currentFuotaSession == null) throw new ValidationException("Cant fetch RLNC payload when session is null");

        // Increment generation, reset fragment index
        _currentFuotaSession.IncrementGenerationIndex();

        // Increase the encoder generation as well
        _rlncEncodingService.MoveNextGeneration();
    }

    public List<byte> FetchNextRlncPayload()
    {
        if (_currentFuotaSession == null) throw new ValidationException("Cant fetch RLNC payload when session is null");

        if (IsCurrentGenerationComplete())
        {
            if (!_rlncEncodingService.HasNextGeneration())
                throw new ValidationException("Cant fetch RLNC payload when no generation is left");

            throw new ValidationException("Generation packets have run out");
        }

        var encodedPacket = _rlncEncodingService.PrecodeNumberOfPackets(1).First();
        var fragmentBytes = encodedPacket.Payload.Select(p => p.GetValue());

        // Next fragment index to be sent is 1 higher
        _currentFuotaSession.IncrementFragmentIndex();

        return fragmentBytes.ToList();
    }

    private void ClearFuotaSession()
    {
        if (!IsFuotaSessionEnabled()) throw new ValidationException("Cant stop FUOTA session when none is enabled");

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