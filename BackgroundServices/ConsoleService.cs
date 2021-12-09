using LoraGateway.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace LoraGateway.BackgroundServices;

internal sealed class ConsoleHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger _logger;
    private readonly ConsoleProcessorService _consoleProcessorService;

    public ConsoleHostedService(
        ILogger<ConsoleHostedService> logger,
        ConsoleProcessorService consoleProcessorService,
        IHostApplicationLifetime appLifetime)
    {
        _logger = logger;
        _consoleProcessorService = consoleProcessorService;
        _appLifetime = appLifetime;
    }

                    
    // Continue = true;
    // _logger.LogInformation("Type QUIT to exit");
    //
    // while (Continue)
    // {
    //     // Block on read
    //     var message = Console.ReadLine();
    //     
    //     // Decide quit or keep on going
    //     if (StringComparer.OrdinalIgnoreCase.Equals("quit", message))
    //     {
    //         Continue = false;
    //         innerCancellation.Cancel();
    //     }
    //     else
    //     {
    //         _serialService.SerialPort?.Write($"{0xFF}{message}\0");
    //     }
    // }
    //
    // _logger.LogInformation("Closing serial");
    // _serialService.SerialPort?.Close();
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug($"Starting with arguments: {string.Join(" ", Environment.GetCommandLineArgs())}");

        _appLifetime.ApplicationStarted.Register(() =>
        {
            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await _consoleProcessorService.ProcessCommandLine();
                }

            });
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}