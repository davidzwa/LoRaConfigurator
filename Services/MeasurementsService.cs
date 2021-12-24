namespace LoraGateway.Services;

public class MeasurementDto
{
    public long TimeStamp { get; set; }
    public int SequenceNumber { get; set; }
    public uint Snr { get; set; }
    public int Rssi { get; set; }
}

public class MeasurementsService
{
    private List<MeasurementDto> _measurementDtos = new();
    
    public string GetMeasurementFile()
    {
        return Path.GetFullPath("../../../Data/measurement.txt", Directory.GetCurrentDirectory());
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

    public void AddMeasurement(int seq, uint snr, int rssi)
    {
        _measurementDtos.Add(new()
        {
            TimeStamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            Rssi = rssi,
            Snr = snr,
            SequenceNumber = seq
        });
    }
}