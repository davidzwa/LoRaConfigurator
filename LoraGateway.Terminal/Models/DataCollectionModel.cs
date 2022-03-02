namespace LoraGateway.Models;

public class DeviceCollection : ICloneable
{
    public GatewayModel Gateway { get; set; } = new()
    {
        Receive = new RadioModel(),
        Transmit = new RadioModel()
    };

    public List<Device?> Devices { get; set; } = new();
    
    public object Clone()
    {
        return MemberwiseClone();
    }
}