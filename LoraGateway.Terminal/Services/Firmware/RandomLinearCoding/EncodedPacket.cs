namespace LoraGateway.Services.Firmware.RandomLinearCoding;

public class EncodedPacket : UnencodedPacket
{
    public byte[] EncodingVector { get; set; } = new byte[] { };
}