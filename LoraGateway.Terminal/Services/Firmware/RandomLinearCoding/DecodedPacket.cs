namespace LoraGateway.Services.Firmware.RandomLinearCoding;

public class DecodedPacket : UnencodedPacket
{
    public bool DecodingSuccess { get; set; }
    public bool IsRedundant { get; set; }

    /// <summary>
    ///     The binary encoding vector represented by the GF(256) degree 8 or byte per packet
    ///     Length is coupled to generation size in bytes / symbols size in bytes
    /// </summary>
    public List<GField> EncodingVector { get; set; } = new();
}