using System.Text;
using System.Text.Json;
using LoraGateway.Models;
using LoraGateway.Services.Contracts;

namespace LoraGateway.Services;

public class MeasurementsService : IDisposable
{
    private readonly ILogger<MeasurementsService> _logger;
    private readonly List<MeasurementDto> _measurementDtos = new();
    private string _location = "";

    private FileStream? _measurementFile;

    public MeasurementsService(ILogger<MeasurementsService> logger)
    {
        _logger = logger;
    }

    public void Dispose()
    {
        _measurementFile?.Close();
    }

    public string GetMeasurementFile()
    {
        var rootPath = JsonDataStoreExtensions.BasePath;
        return Path.GetFullPath($"{rootPath}/measurements{_location}.json", Directory.GetCurrentDirectory());
    }

    private void EnsureSourceExists()
    {
        var path = GetMeasurementFile();
        var dirName = Path.GetDirectoryName(path);
        if (dirName == null) return;

        if (!Directory.Exists(dirName)) Directory.CreateDirectory(dirName);
    }

    public void InitMeasurements()
    {
        _measurementDtos.Clear();
        EnsureSourceExists();
    }

    public void SetLocation(int x, int y)
    {
        _location = $"x{x}_y{y}";
        UpdateFileLock();
    }

    private void UpdateFileLock()
    {
        CloseFileIfOpened();
        if (string.IsNullOrEmpty(_location)) return;
        OpenFile(GetMeasurementFile());
    }

    private void CloseFileIfOpened()
    {
        _measurementFile?.Close();
        _measurementFile = null;
    }

    private void OpenFile(string path)
    {
        CloseFileIfOpened();
        _measurementFile = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
    }

    public void SetLocationText(string locationText)
    {
        _location = locationText.Trim().Replace(" ", "_");

        UpdateFileLock();
    }

    public async Task<bool> AddMeasurement(uint seq, int snr, int rssi)
    {
        if (string.IsNullOrEmpty(_location))
            // _logger.LogInformation("Skipped measurement as location was unset. SeqNr:{Seq}", seq);
            return false;

        _measurementDtos.Add(new MeasurementDto
        {
            TimeStamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            Rssi = rssi,
            Snr = snr,
            SequenceNumber = seq
        });

        var jsonBlob = JsonSerializer.Serialize(_measurementDtos, new JsonSerializerOptions { WriteIndented = true });
        var blob = Encoding.UTF8.GetBytes(jsonBlob);
        if (_measurementFile == null) OpenFile(GetMeasurementFile());

        await _measurementFile.WriteAsync(blob);

        return true;
    }
}