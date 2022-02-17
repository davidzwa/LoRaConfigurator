namespace LoraGateway.Services.Firmware.RandomLinearCoding;

public class EncodedPacket : UnencodedPacket
{
    /// <summary>
    /// The binary encoding vector represented by the GF(256) degree 8 or byte
    /// </summary>
    public byte EncodingVector { get; set; }
}