using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Logging;

namespace LoraGateway.Services;

public class ConsoleProcessorService
{
    private readonly ILogger _logger;

    public ConsoleProcessorService(
        ILogger<ConsoleProcessorService> logger)
    {
        _logger = logger;
    }
    
    public async Task ProcessCommandLine()
    {
        try
        {
            var message = Console.ReadLine();
            if (message == null) return;
            
            RootCommand rootCommand = new RootCommand("Converts an image file from one format to another.");
            await rootCommand.InvokeAsync(message);
                    
            _logger.LogInformation("Received {Message}", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception!");
        }
    }
}