using System.CommandLine;
using System.CommandLine.Invocation;

namespace LoraGateway.Services.CommandLine;

public class SelectDeviceCommandHandler
{
    private readonly ILogger _logger;
    private readonly SelectedDeviceService _selectedDeviceService;
    private readonly SerialProcessorService _serialProcessorService;
    private readonly DeviceDataStore _store;

    public SelectDeviceCommandHandler(
        ILogger<SelectDeviceCommandHandler> logger,
        SelectedDeviceService selectedDeviceService,
        DeviceDataStore store,
        SerialProcessorService serialProcessorService
    )
    {
        _logger = logger;
        _selectedDeviceService = selectedDeviceService;
        _store = store;
        _serialProcessorService = serialProcessorService;
    }

    public Command GetCurrentSelectedCommand()
    {
        var commandHandler = new Command("current");
        commandHandler.AddAlias("c");
        commandHandler.Handler = CommandHandler.Create(() =>
        {
            var port = _selectedDeviceService.SelectedPortName;
            var device = _store.GetDeviceByPort(port!)?.FirstOrDefault();
            _logger.LogInformation("Selected device {DeviceName} at port {Port}", device?.NickName, port);
        });
        return commandHandler;
    }

    public Command GetSelectCommand()
    {
        var commandHandler = new Command("select");
        commandHandler.AddAlias("s");
        commandHandler.AddArgument(new Argument<int>("portNumber"));

        commandHandler.Handler = CommandHandler.Create((int portNumber) =>
        {
            var portName = "COM" + portNumber;
            _logger.LogInformation("Port selected {port}", portName);

            if (!_serialProcessorService.HasPort(portName))
            {
                _logger.LogError("New port {Port} was invalid as it was not available or correct", portName);
                return;
            }

            _selectedDeviceService.SelectedPortName = portName;
        });

        return commandHandler;
    }
}