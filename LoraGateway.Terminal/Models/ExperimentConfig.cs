namespace LoraGateway.Models;

public class ExperimentConfig : ICloneable
{
    public float MinPer { get; set; } = 0.05f;
    public float MaxPer { get; set; } = 0.50f;
    public float PerStep { get; set; } = 0.05f;
    private int RssiAvgWindow { get; set; } = 10;
    private int SnrAvgWindow { get; set; } = 10;

    public bool AwaitUartAck { get; set; } = false;
    public int ExperimentTimeout = 30000;

    public object Clone()
    {
        return MemberwiseClone();
    }
}