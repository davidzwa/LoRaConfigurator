namespace LoraGateway.Services.Firmware.RandomLinearCoding;

public interface IPacket
{
    public byte[] Payload { get; set; }
}