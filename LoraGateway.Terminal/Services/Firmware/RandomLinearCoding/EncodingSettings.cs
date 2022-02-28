namespace LoraGateway.Services.Firmware.RandomLinearCoding;

public class EncodingConfiguration
{
    public uint FieldDegree { get; set; }
    public byte Seed { get; set; }
    public uint GenerationSize { get; set; }

    /// <summary>
    ///     0-indexed generation index
    /// </summary>
    public uint CurrentGeneration { get; set; }
}