namespace LoraGateway.Services.Firmware.RandomLinearCoding;

public class Generation
{
    public int GenerationIndex { get; set; }
    public List<UnencodedPacket> OriginalPackets { get; set; } = new();
    public List<EncodedPacket> EncodedPackets { get; set; } = new();
}