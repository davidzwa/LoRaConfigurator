using System.CommandLine;
using System.CommandLine.Invocation;

namespace LoraGateway.Services.CommandLine;

public class ListDeviceCommandHandler
{
    private readonly ILogger _logger;
    private readonly DeviceDataStore _deviceStore;
    private readonly SerialProcessorService _serialProcessorService;

    public ListDeviceCommandHandler(
        ILogger<ListDeviceCommandHandler> logger,
        DeviceDataStore deviceStore,
        SerialProcessorService serialProcessorService
    )
    {
        _logger = logger;
        _deviceStore = deviceStore;
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
                var device = _deviceStore.GetDeviceByPort(port.PortName);
                if (device == null)
                {
                    _logger.LogInformation("Untracked device on port {port}", port.PortName);    
                }
                else
                {
                    _logger.LogInformation("Device {device} on port {port}", device.NickName, port.PortName);
                }
            }
        });

        return commandHandler;
    }
}