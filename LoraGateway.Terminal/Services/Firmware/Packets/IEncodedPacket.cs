using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Services.Firmware.Packets;

public interface IEncodedPacket : IPacket
{
    public List<GFSymbol> EncodingVector { get; set; }

}