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
    private bool _deviceLaggingBehind = false;

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
                await _fuotaManagerService.LoadStore();
                
                if (!_fuotaManagerService.IsFuotaSessionEnabled())
                {
                    _logger.LogDebug("FUOTA Session Host service going to IDLE mode");
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
                    _logger.LogInformation("Sending RLNC init command {Session} gens", session.GenerationCount);
                    _serialProcessorService.SendRlncInitConfigCommand(session);

                    // Give the devices some time to catch up
                    await Task.Delay(100, cancellationToken);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, e.Message);

                    await _fuotaManagerService.StopFuotaSession(true);

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

                    var cappedPeriod = Math.Max((int)fuotaConfig.LocalUpdateIntervalMs, 100);
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
                await _fuotaManagerService.StopFuotaSession(true);

                return;
            }

            if (_fuotaManagerService.IsAwaitAckEnabled() && _fuotaManagerService.ShouldWait())
            {
                if (!_deviceLaggingBehind) {
                    _logger.LogInformation("ACKs lagging behind. Waiting");
                    _deviceLaggingBehind = true;
                }
                return;
            }
            
            // Restore the status to operational
            if (_deviceLaggingBehind)
            {
                _deviceLaggingBehind = false;
            }
            
            if (_fuotaManagerService.IsCurrentGenerationComplete())
            {
                _fuotaManagerService.MoveNextRlncGeneration();
                var fuotaSession = _fuotaManagerService.GetCurrentSession();
                _serialProcessorService.SendRlncUpdate(fuotaSession);
                return;
            }

            // _fuotaManagerService.LogSessionProgress();
            var fragmentWithGenerator = _fuotaManagerService.FetchNextRlncPayloadWithGenerator();
            _serialProcessorService.SendNextRlncFragment(_fuotaManagerService.GetCurrentSession(), fragmentWithGenerator);
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
            await _fuotaManagerService.StopFuotaSession(true);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("FUOTA background service stopping - CanceledByToken: {Canceled}",
            cancellationToken.IsCancellationRequested);

        if (_fuotaManagerService.IsFuotaSessionEnabled())
        {
            _logger.LogInformation("FUOTA termination sent");
            try
            {
                _serialProcessorService.SendRlncTermination(_fuotaManagerService.GetCurrentSession());
            }
            catch
            {
                _logger.LogInformation("FUOTA force stop (Serial processor failure)");
                await _fuotaManagerService.StopFuotaSession(false);
            }
        }
        
        _stopFired = true;
    }
}