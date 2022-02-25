namespace LoraGateway.Models;

public class FuotaSession
{
    public FuotaSession(uint generationCount)
    {
        TimeStarted = DateTime.Now;
        GenerationCount = generationCount;
    }

    public DateTime TimeStarted { get; }
    public uint GenerationCount { get; }
}