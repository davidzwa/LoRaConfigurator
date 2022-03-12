namespace LoraGateway.Models;

public class Device
{
    public string HardwareId { get; set; }
    public UInt32 Id { get; set; }
    public string NickName { get; set; }
    public string FirmwareVersion { get; set; }
    public string RegisteredAt { get; set; }
    public string LastPortName { get; set; }
    public bool IsGateway { get; set; }
    public object Meta { get; set; }
}