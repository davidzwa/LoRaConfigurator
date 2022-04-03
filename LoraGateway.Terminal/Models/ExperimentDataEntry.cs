namespace LoraGateway.Models;

public class ExperimentDataEntry
{
    public uint GenerationIndex { get; set; }
    public uint GenerationTotalPackets { get; set; }
    public uint GenerationRedundancyUsed { get; set; }
    public uint MissedPackets { get; set; }
    public uint ReceivedPackets { get; set; }
    public uint RngResolution { get; set; }
    public float PacketErrorRate { get; set; }
    public float ConfiguredPacketErrorRate { get; set; }
    public bool Success { get; set; }
    public bool TriggeredByDecodingResult { get; set; }
    public bool TriggeredByCompleteLoss { get; set; }
    public uint Rank { get; set; }
}