using System.CommandLine;
using LoraGateway.Services.CommandLine;

namespace LoraGateway.Services;

public class ConsoleProcessorService
{
    private readonly ILogger _logger;
    private readonly BootCommandHandler _bootCommandHandler;
    private readonly SelectDeviceCommandHandler _selectDeviceCommandHandler;
    private readonly ListDeviceCommandHandler _listDeviceCommandHandler;


    public ConsoleProcessorService(
        ILogger<ConsoleProcessorService> logger,
        BootCommandHandler bootCommandHandler,
        SelectDeviceCommandHandler selectDeviceCommandHandler,
        ListDeviceCommandHandler listDeviceCommandHandler
    )
    {
        _logger = logger;
        _bootCommandHandler = bootCommandHandler;
        _selectDeviceCommandHandler = selectDeviceCommandHandler;
        _listDeviceCommandHandler = listDeviceCommandHandler;
    }

    public async Task ProcessCommandLine()
    {
        try
        {
            var message = Console.ReadLine();
            if (message == null) return;

            var rootCommand = new RootCommand("Converts an image file from one format to another.");
            rootCommand.Add(_selectDeviceCommandHandler.GetHandler());
            rootCommand.Add(_bootCommandHandler.GetBootCommand());
            rootCommand.Add(_listDeviceCommandHandler.GetHandler());
            await rootCommand.InvokeAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception!");
        }
    }
}