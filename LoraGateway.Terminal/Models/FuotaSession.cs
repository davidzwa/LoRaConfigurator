namespace LoraGateway.Models;

public class FuotaSession
{
    public FuotaSession(uint generationCount, bool uartFakeLoRaRXMode)
    {
        TimeStarted = DateTime.Now;
        UartFakeLoRaRXMode = uartFakeLoRaRXMode;
        GenerationCount = generationCount;
    }

    public bool UartFakeLoRaRXMode { get; }
    public DateTime TimeStarted { get; }
    public uint GenerationCount { get; }
}