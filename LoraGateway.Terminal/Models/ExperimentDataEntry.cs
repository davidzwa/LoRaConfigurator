namespace LoraGateway.Models;

public class ExperimentDataEntry
{
    public int GenerationIndex { get; set; }
    public int MissedPackets { get; set; }
    public int ReceivedPackets { get; set; }
    public float PacketErrorRate { get; set; }
    public bool Success { get; set; }
    public int Rank { get; set; }
}