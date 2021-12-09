namespace LoraGateway.Models;

public class DeviceCollection
{
    public GatewayModel Gateway { get; set; } = new()
    {
        Receive = new (),
        Transmit = new()
    };

    public List<DeviceModel> Devices { get; set; } = new();
}