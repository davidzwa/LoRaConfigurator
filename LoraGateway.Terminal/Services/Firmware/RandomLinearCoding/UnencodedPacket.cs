namespace LoraGateway.Services.Firmware.RandomLinearCoding;

public class UnencodedPacket
{
    /// <summary>
    /// The encoded payload in bytes (regardless of what original symbol size, although 8-bit is taken for now)
    /// </summary>
    public byte[] Payload { get; set; } = new byte[] { };
    
    /// <summary>
    /// To recognize the original index in the encoding
    /// </summary>
    public int PacketIndex { get; set; }
}