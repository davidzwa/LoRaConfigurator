namespace LoraGateway.Services.Firmware.RandomLinearCoding;

public class UnencodedPacket : IPacket
{
    /// <summary>
    ///     The encoded payload in bytes (regardless of what original symbol size, although 8-bit is taken for now)
    /// </summary>
    public List<GFSymbol> Payload { get; set; } = new();
}