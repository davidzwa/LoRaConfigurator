using LoRa;

namespace LoraGateway.Models;

public class FuotaSession
{
    public FuotaSession(FuotaConfig config, uint generationCount)
    {
        Config = config.Clone() as FuotaConfig;
        GenerationCount = generationCount;
        TimeStarted = DateTime.Now;
    }

    public void IncrementGenerationIndex()
    {
        CurrentGenerationIndex++;
        CurrentFragmentIndex = 0;
    }

    public void IncrementFragmentIndex()
    {
        CurrentFragmentIndex++;
    }

    public List<DecodingUpdate> Acks { get; } = new();
    public uint GenerationCount { get; }
    public uint TotalFragmentCount { get; set; }
    public FuotaConfig Config { get; }
    public DateTime TimeStarted { get; }

    public uint CurrentGenerationIndex { get; private set; } = 0;
    public uint CurrentFragmentIndex { get; private set; } = 0;
}