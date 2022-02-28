namespace LoraGateway.Models;

public class MeasurementDto

{
    public long TimeStamp { get; set; }
    public uint SequenceNumber { get; set; }
    public int Snr { get; set; }
    public int Rssi { get; set; }
}