namespace LoraGateway.Models;

public class ExperimentConfig : ICloneable
{
    public float MinPer { get; set; } = 0.05f;
    public float MaxPer { get; set; } = 0.50f;
    public float PerStep { get; set; } = 0.05f;
    
    // Not implemented yet
    // private int RssiAvgWindow { get; set; } = 10;
    // Not implemented yet
    // private int SnrAvgWindow { get; set; } = 10;
    public int ExperimentUpdateTimeout { get; set; }= 2000;

    public bool RandomPerSeed { get; set; } = true;

    public object Clone()
    {
        return MemberwiseClone();
    }
}