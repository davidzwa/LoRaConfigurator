namespace LoraGateway.Services;

public class SelectedDeviceService
{
    public string? SelectedPortName { get; set; }

    public void SetPortIfUnset(string portName)
    {
        if (SelectedPortName == null) SelectedPortName = portName;
    }

    public void SwitchIfSet(string disconnectedPort, string? altPortName)
    {
        if (SelectedPortName != null && !SelectedPortName.Equals(disconnectedPort)) return;

        if (SelectedPortName != null) SelectedPortName = altPortName;
    }
}