﻿using LoraGateway.Services;
using Microsoft.Extensions.Hosting;

namespace LoraGateway.BackgroundServices;

internal sealed class ConsoleHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ConsoleProcessorService _consoleProcessorService;
    private readonly ILogger _logger;

    public ConsoleHostedService(
        ILogger<ConsoleHostedService> logger,
        ConsoleProcessorService consoleProcessorService,
        IHostApplicationLifetime appLifetime)
    {
        _logger = logger;
        _consoleProcessorService = consoleProcessorService;
        _appLifetime = appLifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug($"Starting with arguments: {string.Join(" ", Environment.GetCommandLineArgs())}");

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