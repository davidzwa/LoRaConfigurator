using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using LoraGateway.Models;
using LoraGateway.Services.Firmware;
using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Services;

public class FuotaManagerService
{
    private readonly BlobFragmentationService _blobFragmentationService;
    private FuotaSession? _currentFuotaSession = null;
    private List<UnencodedPacket> _firmwarePackets = new();
    private readonly RlncEncodingService _rlncEncodingService;

    private FuotaConfig? _store;

    public FuotaManagerService(
        BlobFragmentationService blobFragmentationService,
        RlncEncodingService rlncEncodingService)
    {
        _blobFragmentationService = blobFragmentationService;
        _rlncEncodingService = rlncEncodingService;
    }

    public string GetJsonFilePath()
    {
        return Path.GetFullPath("../../../Data/fuota_config.json", Directory.GetCurrentDirectory());
    }

    public async Task<FuotaSession> PrepareFuotaSession()
    {
        if (_store == null)
        {
            throw new ValidationException(
                "Fuota config store was not loaded, yet PrepareFuotaSession was called - call LoadStore first");
        }

        if (_firmwarePackets.Count != 0)
        {
            throw new ValidationException(
                "A new fuota session was started, while a previous one was already requested");
        }

        if (_store.FakeFirmware)
        {
            var frameSize = (int) _store.FakeFragmentSize;
            var firmwareSize = _store.FakeFragmentCount * frameSize;
            _firmwarePackets = await _blobFragmentationService.GenerateFakeFirmwareAsync(firmwareSize, frameSize);
        }

        // Prepare 
        _rlncEncodingService.ConfigureEncoding(new EncodingConfiguration()
        {
            Seed = (byte)_store.LfsrSeed,
            CurrentGeneration = 0,
            FieldDegree = _store.FieldDegree,
            GenerationSize = _store.GenerationSize
        });
        var generationCount =
            (uint) _rlncEncodingService.PreprocessGenerations(_firmwarePackets, _store.GenerationSize);

        _currentFuotaSession = new FuotaSession(generationCount);

        return _currentFuotaSession;
    }

    private FuotaConfig GetDefaultJson()
    {
        var jsonStore = new FuotaConfig();
        // Adjust anything deviating from default here
        return jsonStore;
    }

    private async Task EnsureSourceExists()
    {
        var path = GetJsonFilePath();
        var dirName = Path.GetDirectoryName(path);
        if (dirName == null) return;

        if (!Directory.Exists(dirName)) Directory.CreateDirectory(dirName);

        if (!File.Exists(path)) await WriteStore();
    }

    public async Task<FuotaConfig?> LoadStore()
    {
        await EnsureSourceExists();

        var path = GetJsonFilePath();
        var blob = await File.ReadAllTextAsync(path);
        _store = JsonSerializer.Deserialize<FuotaConfig>(blob, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return _store;
    }

    private async Task WriteStore()
    {
        var path = GetJsonFilePath();
        var jsonStore = _store ?? GetDefaultJson();
        var serializedBlob = JsonSerializer.SerializeToUtf8Bytes(jsonStore, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllBytesAsync(path, serializedBlob);
    }
}