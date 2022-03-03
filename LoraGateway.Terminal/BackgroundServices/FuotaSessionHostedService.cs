using System.ComponentModel.DataAnnotations;
using LoraGateway.Services;
using LoraGateway.Utils;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace LoraGateway.BackgroundServices;

public class FuotaSessionHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<FuotaSessionHostedService> _logger;
    private readonly SerialProcessorService _serialProcessorService;
    private readonly FuotaManagerService _fuotaManagerService;

    private bool _stopFired = false;

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
        _stopFired = false;

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
                }

                var session = _fuotaManagerService.GetCurrentSession();
                try
                {
                    _logger.LogInformation("Sending RLNC init command");
                    _serialProcessorService.SendRlncInitConfigCommand(session);

                    // Give the devices some time to catch up
                    await Task.Delay(1000, cancellationToken);

                    // Nice debugging step to verify init step
                    // _logger.LogInformation("Quitting RLNC init");
                    // await _fuotaManagerService.StopFuotaSession();
                    // return;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, e.Message);
                    
                    await _fuotaManagerService.StopFuotaSession();

                    return;
                }

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (_stopFired || cancellationToken.IsCancellationRequested)
                    {
                        _stopFired = false;
                        return;
                    }

                    await Process();

                    var cappedPeriod = Math.Max((int)fuotaConfig.UpdateIntervalMilliSeconds, 100);
                    await Task.Delay(cappedPeriod, cancellationToken);
                }

                Log.Information("STOPPED - Cancellation {Cancel} Stoppage {Stop}",
                    cancellationToken.IsCancellationRequested, _stopFired);
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
                _logger.LogDebug("FuotaManager indicated stoppage - Stop Fired: {Stopped}", _stopFired);
                await _fuotaManagerService.StopFuotaSession();

                return;
            }

            // perform UART FUOTA session operations
            _fuotaManagerService.LogSessionProgress();

            if (_fuotaManagerService.IsCurrentGenerationComplete())
            {
                _fuotaManagerService.MoveNextRlncGeneration();
                
                var fuotaSession = _fuotaManagerService.GetCurrentSession();
                _serialProcessorService.SendRlncUpdate(fuotaSession);
                
                return;
            }
            
            var payload = _fuotaManagerService.FetchNextRlncPayload();
            _logger.LogInformation("Encoded {Message}", SerialUtil.ByteArrayToString(payload.ToArray()));
            _serialProcessorService.SendNextRlncFragment(_fuotaManagerService.GetCurrentSession(), payload);
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
            await _fuotaManagerService.StopFuotaSession();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("FUOTA background service stopping - CanceledByToken: {Canceled}",
            cancellationToken.IsCancellationRequested);

        _stopFired = true;

        return Task.CompletedTask;
    }
}