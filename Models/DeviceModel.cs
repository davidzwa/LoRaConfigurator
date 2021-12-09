namespace LoraGateway.Models;

public class DeviceModel
{
    public string Id { get; set; }
    public string NickName { get; set; }
    public string RegisteredAt { get; set; }
    public bool IsGateway { get; set; }
    public object Meta { get; set; }
}