using System.Text.Json;

namespace LoraGateway.Services;

public class MeasurementDto
{
    public long TimeStamp { get; set; }
    public uint SequenceNumber { get; set; }
    public uint Snr { get; set; }
    public int Rssi { get; set; }
}

public class MeasurementsService
{
    private List<MeasurementDto> _measurementDtos = new();
    private String _location = "";
    
    public string GetMeasurementFile()
    {
        return Path.GetFullPath($"../../../Data/measurements{_location}.json", Directory.GetCurrentDirectory());
    }
    

    private async Task EnsureSourceExists()
    {
        var path = GetMeasurementFile();
        var dirName = Path.GetDirectoryName(path);
        if (dirName == null) return;

        if (!Directory.Exists(dirName)) Directory.CreateDirectory(dirName);
        // if (!File.Exists(path))
        // {
        //     await File.WriteAllLinesAsync(path, null);
        // }
    }
    
    public async Task InitMeasurements()
    {
        this._measurementDtos.Clear();
        await EnsureSourceExists();
    }

    public void SetLocation(int x, int y)
    {
        _location = $"x{x}_y{y}";
    }
    
    public void SetLocationText(string locationText)
    {
        _location = locationText.Trim().Replace(" ", "_");
    }

    public async Task AddMeasurement(uint seq, uint snr, int rssi)
    {
        _measurementDtos.Add(new()
        {
            TimeStamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            Rssi = rssi,
            Snr = snr,
            SequenceNumber = seq
        });

        var jsonBlob = JsonSerializer.Serialize(_measurementDtos, new JsonSerializerOptions() {WriteIndented = true});
        
        await File.WriteAllTextAsync(GetMeasurementFile(), jsonBlob);
    }
}