using JKang.EventBus;
using LoRa;
using LoraGateway.BackgroundServices;
using LoraGateway.Services;

namespace LoraGateway.Handlers;

public class InitFuotaSession
{
    public CancellationToken Token { get; set; }
    public string Message { get; set; }
}

public class DecodingUpdateEvent
{
    public DecodingUpdate? DecodingUpdate { get; set; }
}

public class StopFuotaSession
{
    public CancellationToken Token { get; set; }
    public string Message { get; set; }
}

public class FuotaEventHandler : IEventHandler<InitFuotaSession>, IEventHandler<StopFuotaSession>, IEventHandler<DecodingUpdateEvent>
{
    private readonly FuotaSessionHostedService _fuotaSessionHostedService;
    private readonly FuotaManagerService _fuotaManagerService;

    public FuotaEventHandler(
        FuotaSessionHostedService fuotaSessionHostedService  ,
        FuotaManagerService fuotaManagerService
    )
    {
        _fuotaSessionHostedService = fuotaSessionHostedService;
        _fuotaManagerService = fuotaManagerService;
    }
    
    public async Task HandleEventAsync(InitFuotaSession @event)
    {
        await _fuotaSessionHostedService.StartAsync(@event.Token);
    }
    
    public async Task HandleEventAsync(DecodingUpdateEvent @event)
    {
        if (@event.DecodingUpdate != null) {
            _fuotaManagerService.SaveFuotaDebuggingProgress(@event.DecodingUpdate);
        }
    }
    
    public async Task HandleEventAsync(StopFuotaSession @event)
    {
        await _fuotaSessionHostedService.StopAsync(@event.Token);
    }
}