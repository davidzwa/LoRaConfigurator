using System.CommandLine;
using System.CommandLine.Invocation;

namespace LoraGateway.Services.CommandLine;

public class ListDeviceCommandHandler
{
    private readonly DeviceDataStore _deviceStore;
    private readonly ILogger _logger;
    private readonly SelectedDeviceService _selectedDeviceService;
    private readonly SerialProcessorService _serialProcessorService;

    public ListDeviceCommandHandler(
        ILogger<ListDeviceCommandHandler> logger,
        DeviceDataStore deviceStore,
        SelectedDeviceService selectedDeviceService,
        SerialProcessorService serialProcessorService
    )
    {
        _logger = logger;
        _deviceStore = deviceStore;
        _selectedDeviceService = selectedDeviceService;
        _serialProcessorService = serialProcessorService;
    }

    public Command GetHandler()
    {
        var commandHandler = new Command("list");
        commandHandler.AddAlias("l");

        commandHandler.Handler = CommandHandler.Create(() =>
        {
            var ports = _serialProcessorService.SerialPorts;
            foreach (var port in ports)
            {
                var device = _deviceStore.GetDeviceByPort(port.PortName).ToList();
                if (device.Count == 0)
                {
                    _logger.LogInformation("Untracked device on port {port}", port.PortName);
                }
                else
                {
                    // var isSelected = port.PortName == _selectedDeviceService.SelectedPortName;
                    _logger.LogInformation("Device on port {port}", port.PortName);
                }
            }
        });

        return commandHandler;
    }
}