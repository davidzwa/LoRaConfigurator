namespace LoraGateway.Models;

public class ExperimentDataUpdateEntry
{
    public float PerConfig { get; set; }
    public bool IsRunning { get; set; }
    public bool Success { get; set; }
    public uint Rank { get; set; }
    public int Redundancy { get; set; }
    public uint GenerationIndex { get; set; }
    public uint MissedPackets { get; set; }
    public uint ReceivedPackets { get; set; }
    
    public uint FirstRowCrc8 { get; set; }
    public uint LastRowCrc8 { get; set; }
    public uint MatrixCrc8 { get; set; }
    public uint PrngStateBefore { get; set; }
    public uint PrngStateAfter { get; set; }
}