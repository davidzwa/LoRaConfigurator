namespace LoraGateway.Models;

public class FuotaSession
{
    public FuotaSession(uint generationCount, bool uartFakeLoRaRxMode)
    {
        TimeStarted = DateTime.Now;
        UartFakeLoRaRxMode = uartFakeLoRaRxMode;
        GenerationCount = generationCount;
    }

    public bool UartFakeLoRaRxMode { get; }
    public DateTime TimeStarted { get; }
    public uint GenerationCount { get; }
}