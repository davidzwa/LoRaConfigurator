namespace LoraGateway.Services.Firmware.RandomLinearCoding;

public class EncodingSettings
{
    public uint FieldOrder { get; set; }
    public byte Seed { get; set; }
    public uint[] Polynomial { get; set; } = new uint[] { };
    public uint GenerationSize { get; set; }
}