namespace LoraGateway.Models;

public class ExperimentPhyDataEntry
{
    public long Timestamp { get; set; }
    public int Rssi { get; set; }
    public int Snr { get; set; }
    public uint SequenceNumber { get; set; }
    public uint SpreadingFactor { get; set; }
    public int TxPower { get; set; }
    public uint Bandwidth { get; set; }
}