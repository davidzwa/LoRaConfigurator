namespace LoraGateway.Models;

public class ExperimentConfig : ICloneable
{
    public uint MinPer { get; set; } = 5;
    public uint MaxPer { get; set; } = 50;
    public uint PerStep { get; set; } = 5;
    public int ExperimentUpdateTimeout { get; set; } = 2000;
    public int ExperimentInitAckTimeout { get; set; } = 500;

    public bool RandomPerSeed { get; set; } = true;

    public object Clone()
    {
        return MemberwiseClone();
    }
}