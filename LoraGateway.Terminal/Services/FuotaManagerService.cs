using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using LoraGateway.Models;
using LoraGateway.Services.Contracts;
using LoraGateway.Services.Firmware;
using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Services;

public class FuotaManagerService : JsonDataStore<FuotaConfig>
{
    private readonly BlobFragmentationService _blobFragmentationService;
    private FuotaSession? _currentFuotaSession = null;
    private List<UnencodedPacket> _firmwarePackets = new();
    private readonly RlncEncodingService _rlncEncodingService;

    public FuotaManagerService(
        BlobFragmentationService blobFragmentationService,
        RlncEncodingService rlncEncodingService)
    {
        _blobFragmentationService = blobFragmentationService;
        _rlncEncodingService = rlncEncodingService;
    }

    public override string GetJsonFileName()
    {
        return "fuota_config.json";
    }

    public async Task<FuotaSession> PrepareFuotaSession()
    {
        if (Store == null)
        {
            throw new ValidationException(
                "Fuota config store was not loaded, yet PrepareFuotaSession was called - call LoadStore first");
        }

        if (_firmwarePackets.Count != 0)
        {
            throw new ValidationException(
                "A new fuota session was started, while a previous one was already requested");
        }

        if (Store.FakeFirmware)
        {
            var frameSize = (int) Store.FakeFragmentSize;
            var firmwareSize = Store.FakeFragmentCount * frameSize;
            _firmwarePackets = await _blobFragmentationService.GenerateFakeFirmwareAsync(firmwareSize, frameSize);
        }

        // Prepare 
        _rlncEncodingService.ConfigureEncoding(new EncodingConfiguration()
        {
            Seed = (byte)Store.LfsrSeed,
            CurrentGeneration = 0,
            FieldDegree = Store.FieldDegree,
            GenerationSize = Store.GenerationSize
        });
        var generationCount =
            (uint) _rlncEncodingService.PreprocessGenerations(_firmwarePackets, Store.GenerationSize);

        _currentFuotaSession = new FuotaSession(generationCount);

        return _currentFuotaSession;
    }

    public override FuotaConfig GetDefaultJson()
    {
        var jsonStore = new FuotaConfig();
        
        // Adjust anything deviating from default here
        
        return jsonStore;
    } 
}