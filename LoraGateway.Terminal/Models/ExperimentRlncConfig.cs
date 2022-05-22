namespace LoraGateway.Models;

public class ExperimentConfig : ICloneable
{
    public uint MinProbP { get; set; } = 1;
    public uint MaxProbP { get; set; } = 15;
    public uint ProbPStep { get; set; } = 3;
    public bool UseBurstModelOverride { get; set; } = true;
    public uint MinPer { get; set; } = 500;
    public uint MaxPer { get; set; } = 5000;
    public uint PerStep { get; set; } = 500;
    public int ExperimentUpdateTimeout { get; set; } = 2000;
    public int ExperimentInitAckTimeout { get; set; } = 500;

    public bool RandomPerSeed { get; set; } = true;

    public object Clone()
    {
        return MemberwiseClone();
    }
}