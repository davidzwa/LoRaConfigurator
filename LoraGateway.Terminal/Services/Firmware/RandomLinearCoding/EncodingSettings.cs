namespace LoraGateway.Services.Firmware.RandomLinearCoding;

public class EncodingConfiguration
{
    public uint FieldOrder { get; set; } = 8;
    public byte Seed { get; set; } = 0x08;
    public uint GenerationSize { get; set; } = 16;

    /// <summary>
    /// 0-indexed generation index
    /// </summary>
    public uint CurrentGeneration { get; set; } = 0;
}