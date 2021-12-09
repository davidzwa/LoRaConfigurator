using System.Collections.ObjectModel;
using System.Management;
using LoraGateway.Models;
using LoraGateway.Utils;

namespace LoraGateway.Services;

class CancellableMessageProcessor
{
    public Task MessageProcessor { get; set; }
    public CancellationTokenSource CancellationTokenSource { get; set; }
    public string PortName { get; set; }
}

public class SerialWatcher : IDisposable
{
    private readonly SerialProcessorService _serialProcessorService;
    private readonly ManagementEventWatcher _arrivalwatcher;
    private readonly ManagementEventWatcher _removalwatcher;

    private List<CancellableMessageProcessor> _serialProcessors = new();

    public SerialWatcher(
        SerialProcessorService serialProcessorService
    )
    {
        _serialProcessorService = serialProcessorService;
        var deviceArrivalQuery = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2");
        var deviceRemovalQuery = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 3");

        _arrivalwatcher = new ManagementEventWatcher(deviceArrivalQuery);
        _removalwatcher = new ManagementEventWatcher(deviceRemovalQuery);

        _arrivalwatcher.EventArrived += (sender, eventArgs) => CheckForNewPortsAsync(sender, eventArgs, false);
        _removalwatcher.EventArrived += (sender, eventArgs) => CheckForNewPortsAsync(sender, eventArgs, true);
    }

    public void Dispose()
    {
        _arrivalwatcher?.Stop();
        _removalwatcher?.Stop();
    }

    public void Initiate()
    {
        _arrivalwatcher?.Start();
        _removalwatcher?.Start();
    }

    public void CheckForNewPortsAsync(object sender, EventArrivedEventArgs eventArgs, bool removal)
    {
        var ports = SerialUtil.GetStmDevicePorts();
        
        List<CancellableMessageProcessor> removedProcessors = new();
        foreach (var processor in _serialProcessors)
        {
            if (!ports.Any(p => p.PortName.Equals(processor.PortName)))
            {
                DisposeMessageProcessor(processor.PortName);
                removedProcessors.Add(processor);
            }
        }

        foreach (var removedProcessor in removedProcessors)
        {
            _serialProcessors.Remove(removedProcessor);
        }

        foreach (var port in ports.ToList())
        {
            if (!_serialProcessors.Any(s => s.PortName.Equals(port.PortName)))
            {
                CreateMessageProcessor(port.PortName);
            }
        }
    }

    public void CreateMessageProcessor(string portName)
    {
        var processor = _serialProcessors.Find(s => s.PortName.Equals(portName));
        if (processor != null) return;
        
        CancellationTokenSource innerCancellation = new CancellationTokenSource();
        _serialProcessorService.ConnectPort(portName);
        var task = Task.Run(() =>
                _serialProcessorService.MessageProcessor(portName, innerCancellation.Token)
            , innerCancellation.Token);
        
        _serialProcessors.Add(new ()
        {
            MessageProcessor = task,
            PortName = portName,
            CancellationTokenSource = innerCancellation
        });
    }

    public void DisposeMessageProcessor(string portName)
    {
        var processor = _serialProcessors.Find(processor => processor.PortName.Equals(portName));
        if (processor == null) return;
        
        _serialProcessorService.DisconnectPort(portName);
        processor.CancellationTokenSource.Cancel();
    }
}