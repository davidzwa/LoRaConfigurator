using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Services.Firmware.Packets;

public class Generation
{
    public int GenerationIndex { get; set; }
    public List<UnencodedPacket> OriginalPackets { get; set; } = new();
    public List<IEncodedPacket> EncodedPackets { get; set; } = new();
}