using System.ComponentModel.DataAnnotations;
using LoraGateway.Services;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace LoraGateway.BackgroundServices;

public class FuotaSessionHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<FuotaSessionHostedService> _logger;
    private readonly SerialProcessorService _serialProcessorService;
    private readonly FuotaManagerService _fuotaManagerService;

    private bool StopFired = false;
    private Task taskSingleton;

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
        StopFired = false;
        _logger.LogInformation("CancellationTokenId {Id}", cancellationToken.GetHashCode());
        
        _appLifetime.ApplicationStarted.Register(() =>
        {
            taskSingleton = Task.Run(async () =>
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

                var session = _fuotaManagerService.GetCurrentSession();
                _serialProcessorService.SendRlncInitConfigCommand(session);
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (StopFired || cancellationToken.IsCancellationRequested)
                    {
                        StopFired = false;
                        return;
                    }
                    
                    await Process();

                    var cappedPeriod = Math.Max((int) fuotaConfig.UpdateIntervalMilliSeconds, 100);
                    await Task.Delay(cappedPeriod, cancellationToken);
                }
                
                Log.Information("STOPPED - Cancellation {Cancel} Stoppage {Stop}", cancellationToken.IsCancellationRequested, StopFired);
            });
        });

        return Task.CompletedTask;
    }
    
    
    private async Task Process()
    {
        try
        {
            if (_fuotaManagerService.IsFuotaSessionDone())
            {
                _logger.LogDebug("FuotaManager indicated stoppage - Stop Fired: {Stopped}", StopFired);
                await _fuotaManagerService.StopFuotaSession();
                
                return;
            }

            // perform UART FUOTA session operations
            _fuotaManagerService.LogSessionProgress();
            
            var payload = _fuotaManagerService.FetchNextRlncPayload();
            var fuotaSession = _fuotaManagerService.GetCurrentSession();

            _serialProcessorService.SendNextRlncFragment(fuotaSession, payload);
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
            await _fuotaManagerService.StopFuotaSession();
        }
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("FUOTA background service stopping - CanceledByToken: {Canceled}", cancellationToken.IsCancellationRequested);
        
        StopFired = true;
        
        return Task.CompletedTask;
    }
}