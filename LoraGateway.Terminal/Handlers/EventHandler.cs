using Google.Protobuf;
using JKang.EventBus;
using LoRa;
using LoraGateway.BackgroundServices;
using LoraGateway.Services;
using LoraGateway.Utils;

namespace LoraGateway.Handlers;

public class InitFuotaSession
{
    public CancellationToken Token { get; set; }
    public string Message { get; set; }
}

public class DecodingUpdateEvent
{
    public string Source { get; set; }
    public DecodingUpdate? DecodingUpdate { get; set; }
    public ByteString Payload { get; set; }
}

public class DecodingResultEvent
{
    public DecodingResult DecodingResult { get; set; }
}

public class StopFuotaSession
{
    public CancellationToken Token { get; set; }
    public bool SuccessfulTermination { get; set; } = false;
    public string Message { get; set; }
}

public class FuotaEventHandler : IEventHandler<InitFuotaSession>, IEventHandler<StopFuotaSession>, IEventHandler<DecodingUpdateEvent>, IEventHandler<DecodingResultEvent>
{
    private readonly FuotaSessionHostedService _fuotaSessionHostedService;
    private readonly FuotaManagerService _fuotaManagerService;
    private readonly ExperimentService _experimentService;

    public FuotaEventHandler(
        FuotaSessionHostedService fuotaSessionHostedService,
        FuotaManagerService fuotaManagerService,
        ExperimentService experimentService
    )
    {
        _fuotaSessionHostedService = fuotaSessionHostedService;
        _fuotaManagerService = fuotaManagerService;
        _experimentService = experimentService;
    }
    
    public async Task HandleEventAsync(InitFuotaSession @event)
    {
        await _fuotaSessionHostedService.StartAsync(@event.Token);
    }
    
    public async Task HandleEventAsync(DecodingUpdateEvent @event)
    {
        if (@event.DecodingUpdate != null) {
            _fuotaManagerService.SaveFuotaDebuggingProgress(@event.Source, @event.DecodingUpdate, @event.Payload);
        }
    }

    public async Task HandleEventAsync(DecodingResultEvent @event)
    {
        await _experimentService.ProcessResult(@event.DecodingResult);
    }
    
    public async Task HandleEventAsync(StopFuotaSession @event)
    {
        await _fuotaSessionHostedService.StopAsync(@event.Token);
        
        // An external factor caused FUOTA cancellation
        if (_fuotaManagerService.IsFuotaSessionEnabled())
        {
            _fuotaManagerService.ClearFuotaSession();
        }
        else if (_fuotaManagerService.IsRemoteSessionStarted)
        {
            _fuotaManagerService.IsRemoteSessionStarted = false;
        }

        if (@event.SuccessfulTermination)
        {
            _experimentService.MarkTerminationReceived();
        }
    }
}