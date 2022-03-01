using System.ComponentModel.DataAnnotations;
using LoraGateway.Services;
using Microsoft.Extensions.Hosting;

namespace LoraGateway.BackgroundServices;

public class FuotaSessionHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<FuotaSessionHostedService> _logger;
    private readonly SerialProcessorService _serialProcessorService;
    private readonly FuotaManagerService _fuotaManagerService;

    public FuotaSessionHostedService(
        ILogger<FuotaSessionHostedService> logger,
        SerialProcessorService serialProcessorService,
        FuotaManagerService fuotaManagerService,
        IHostApplicationLifetime appLifetime)
    {
        _logger = logger;
        _serialProcessorService = serialProcessorService;
        _fuotaManagerService = fuotaManagerService;
        _appLifetime = appLifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _appLifetime.ApplicationStarted.Register(() =>
        {
            Task.Run(async () =>
            {
                if (!_fuotaManagerService.IsFuotaSessionEnabled())
                {
                    _logger.LogInformation("FUOTA Session Host service going to IDLE mode");
                    return;
                }

                var fuotaConfig = _fuotaManagerService.GetStore();
                if (fuotaConfig == null)
                {
                    throw new ValidationException("FUOTA session was started when no config was loaded or stored");
                };
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Sending next FUOTA packet");
                    
                    await Process();

                    await Task.Delay((int)fuotaConfig.UpdateIntervalSeconds * 1000, cancellationToken);
                }
            });
        });

        return Task.CompletedTask;
    }
    
    
    private async Task Process()
    {
        try
        {
            // perform UART FUOTA session operations
            await _serialProcessorService.SendNextRlncFragment();
            _logger.LogInformation("Do nothing. No end condition");
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
        }
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping FUOTA background service");
        return Task.CompletedTask;
    }
}