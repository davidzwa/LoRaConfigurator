using System.CommandLine;
using System.CommandLine.Invocation;

namespace LoraGateway.Services.CommandLine;

public class SelectDeviceCommandHandler
{
    private readonly ILogger _logger;
    private readonly SelectedDeviceService _selectedDeviceService;
    private readonly SerialProcessorService _serialProcessorService;

    public SelectDeviceCommandHandler(
        ILogger<SelectDeviceCommandHandler> logger,
        SelectedDeviceService selectedDeviceService,
        SerialProcessorService serialProcessorService
    )
    {
        _logger = logger;
        _selectedDeviceService = selectedDeviceService;
        _serialProcessorService = serialProcessorService;
    }

    public Command GetSelectCommand()
    {
        var commandHandler = new Command("select");
        commandHandler.AddArgument(new Argument<int>("portNumber"));

        commandHandler.Handler = CommandHandler.Create((int portNumber) =>
        {
            var portName = "COM" + portNumber;
            _logger.LogInformation("New Port {port}", portName);

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