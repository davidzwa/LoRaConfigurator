namespace LoraGateway.Services.Firmware.RandomLinearCoding;

public interface IPacket
{
    public List<GField> Payload { get; set; }
}