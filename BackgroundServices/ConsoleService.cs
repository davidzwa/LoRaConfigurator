using LoraGateway.Services;
using Microsoft.Extensions.Hosting;

namespace LoraGateway.BackgroundServices;

internal sealed class ConsoleHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ConsoleProcessorService _consoleProcessorService;

    public ConsoleHostedService(
        ILogger<ConsoleHostedService> logger,
        ConsoleProcessorService consoleProcessorService,
        IHostApplicationLifetime appLifetime)
    {
        _consoleProcessorService = consoleProcessorService;
        _appLifetime = appLifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _appLifetime.ApplicationStarted.Register(() =>
        {
            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested) await _consoleProcessorService.ProcessCommandLine();
            });
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}