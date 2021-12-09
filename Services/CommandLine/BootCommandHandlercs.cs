using System.CommandLine;
using System.CommandLine.Invocation;

namespace LoraGateway.Services.CommandLine;

public class BootCommandHandler
{
    private readonly ILogger _logger;
    private readonly SelectedDeviceService _selectedDeviceService;
    private readonly SerialProcessorService _serialProcessorService;

    public BootCommandHandler(
        ILogger<BootCommandHandler> logger,
        SelectedDeviceService selectedDeviceService,
        SerialProcessorService serialProcessorService
    )
    {
        _logger = logger;
        _selectedDeviceService = selectedDeviceService;
        _serialProcessorService = serialProcessorService;
    }

    public Command GetBootCommand()
    {
        var bootCommand = new Command("boot");
        bootCommand.AddAlias("b");
        bootCommand.Handler = CommandHandler.Create(() =>
        {
            var selectedPortName = _selectedDeviceService.SelectedPortName;
            if (selectedPortName == null)
            {
                _logger.LogWarning("Cant send as multiple devices are connected and 1 is not selected");
                return;
            }
            
            _logger.LogInformation("Port {port}", selectedPortName);

            var command = new UartCommand();
            command.RequestBootInfo = new RequestBootInfo { Request = true };
            _serialProcessorService.WriteMessage(selectedPortName, command);
        });

        return bootCommand;
    }
}