using LoRa;

namespace LoraGateway.Handlers;

public class PeriodTxEvent
{
    // Placeholder for now
    public string PortName { get; set; }
    // public LoRaMessage Message { get; set; }
}

public class RxConfigAckEvent
{
    public string PortName { get; set; }
    public LoRaMessage Message { get; set; }
    // Placeholder for now
}

public class RxEvent
{
    public string PortName { get; set; }
    public UartResponse Message { get; set; }
}