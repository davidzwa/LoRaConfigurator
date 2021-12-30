﻿using System.Text;
using System.Text.Json;

namespace LoraGateway.Services;

public class MeasurementDto
{
    public long TimeStamp { get; set; }
    public uint SequenceNumber { get; set; }
    public uint Snr { get; set; }
    public int Rssi { get; set; }
}

public class MeasurementsService : IDisposable
{
    private readonly ILogger<MeasurementsService> _logger;
    private string _location = "";
    private readonly List<MeasurementDto> _measurementDtos = new();

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
        return Path.GetFullPath($"../../../Data/measurements{_location}.json", Directory.GetCurrentDirectory());
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

    public async Task AddMeasurement(uint seq, uint snr, int rssi)
    {
        if (string.IsNullOrEmpty(_location))
        {
            _logger.LogInformation("Skipped measurement as location was unset. SeqNr:{Seq}", seq);
            return;
        }

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
    }
}