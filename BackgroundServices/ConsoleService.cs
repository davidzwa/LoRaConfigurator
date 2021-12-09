using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LoraGateway.BackgroundServices;

internal sealed class ConsoleHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger _logger;

    public ConsoleHostedService(
        ILogger<ConsoleHostedService> logger,
        IHostApplicationLifetime appLifetime)
    {
        _logger = logger;
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
                try
                {
                    var message = Console.ReadLine();
                    _logger.LogInformation("Received {Message}", message);
                    await DoWork();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception!");
                }
            });
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task DoWork()
    {
        while (true) await Task.Delay(1000);
    }
}