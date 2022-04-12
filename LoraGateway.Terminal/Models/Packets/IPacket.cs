namespace LoraGateway.Services.Firmware.RandomLinearCoding;

public interface IPacket
{
    public List<GFSymbol> Payload { get; set; }
}