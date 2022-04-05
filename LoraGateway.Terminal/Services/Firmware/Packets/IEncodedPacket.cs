namespace LoraGateway.Services.Firmware.RandomLinearCoding;

public interface IEncodedPacket : IPacket
{
    public List<GFSymbol> EncodingVector { get; set; }

}