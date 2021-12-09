namespace LoraGateway.Services;

public class SelectedDeviceService
{
    public string? SelectedPortName { get; set; }
    
    public void SetPortIfUnset(string portName)
    {
        if (SelectedPortName == null) SelectedPortName = portName;
    }
}