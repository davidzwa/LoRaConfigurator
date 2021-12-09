namespace LoraGateway.Models;

public class DeviceCollection
{
    public GatewayModel Gateway { get; set; } = new()
    {
        Receive = new RadioModel(),
        Transmit = new RadioModel()
    };

    public List<Device> Devices { get; set; } = new();
}