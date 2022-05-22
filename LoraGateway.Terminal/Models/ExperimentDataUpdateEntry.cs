namespace LoraGateway.Models;

public class ExperimentDataUpdateEntry
{
    public long Timestamp { get; set; }
    public float SweepParam { get; set; }
    public uint SweepParam10000 {get; set; }
    public uint GenerationIndex { get; set; }
    public uint CurrentFragmentIndex { get; set; }
    public uint CurrentSequenceNumber { get; set; }
    public int RedundancyUsed { get; set; }
    public uint RedundancyMax { get; set; }
    public bool Success { get; set; }
    
    // Debugging
    public uint Rank { get; set; }
    public uint MissedPackets { get; set; }
    public uint ReceivedPackets { get; set; }
    public bool IsRunning { get; set; }
    public uint FirstRowCrc8 { get; set; }
    public uint LastRowCrc8 { get; set; }
    public uint PrngStateBefore { get; set; }
    public uint PrngStateAfter { get; set; }
}