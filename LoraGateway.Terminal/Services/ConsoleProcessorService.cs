using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using LoraGateway.Services.CommandLine;

namespace LoraGateway.Services;

public class ConsoleProcessorService
{
    private readonly ListDeviceCommandHandler _managementCommandHandler;
    private readonly ILogger _logger;
    private readonly SelectDeviceCommandHandler _selectDeviceCommandHandler;
    private readonly SerialCommandHandler _serialCommandHandler;


    public ConsoleProcessorService(
        ILogger<ConsoleProcessorService> logger,
        SerialCommandHandler serialCommandHandler,
        SelectDeviceCommandHandler selectDeviceCommandHandler,
        ListDeviceCommandHandler managementCommandHandler
    )
    {
        _logger = logger;
        _serialCommandHandler = serialCommandHandler;
        _selectDeviceCommandHandler = selectDeviceCommandHandler;
        _managementCommandHandler = managementCommandHandler;
    }

    public async Task ProcessCommandLine()
    {
        try
        {
            var message = Console.ReadLine();
            if (message == null) return;

            var rootCommand = new RootCommand("Processes UART terminal commands for the LoRa proxy gateway device.");
            rootCommand.TreatUnmatchedTokensAsErrors = true;
            rootCommand.Add(_selectDeviceCommandHandler.GetSelectCommand());
            rootCommand.Add(_selectDeviceCommandHandler.GetCurrentSelectedCommand());
            _serialCommandHandler.ApplyCommands(rootCommand);
            _managementCommandHandler.ApplyCommands(rootCommand);

            await rootCommand.InvokeAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception!");
        }
    }
}