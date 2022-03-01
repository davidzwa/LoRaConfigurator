using JKang.EventBus;
using LoraGateway.BackgroundServices;

namespace LoraGateway.Handlers;

public class InitFuotaSession
{
    public string Message { get; set; }
}

public class StopFuotaSession
{
    public string Message { get; set; }
}

public class FuotaEventHandler : IEventHandler<InitFuotaSession>, IEventHandler<StopFuotaSession>
{
    private readonly FuotaSessionHostedService _fuotaSessionHostedService;

    public FuotaEventHandler(
        FuotaSessionHostedService fuotaSessionHostedService    
    )
    {
        _fuotaSessionHostedService = fuotaSessionHostedService;
    }
    
    public async Task HandleEventAsync(InitFuotaSession @event)
    {
        await _fuotaSessionHostedService.StartAsync(CancellationToken.None);
    }
    
    public async Task HandleEventAsync(StopFuotaSession @event)
    {
        await _fuotaSessionHostedService.StopAsync(CancellationToken.None);
    }
}