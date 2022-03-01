using JKang.EventBus;
using LoraGateway.BackgroundServices;

namespace LoraGateway.Handlers;

public class InitFuotaSession
{
    public string Message { get; set; }
}

public class MyEventHandler : IEventHandler<InitFuotaSession>
{
    private readonly FuotaSessionHostedService _fuotaSessionHostedService;

    public MyEventHandler(
        FuotaSessionHostedService fuotaSessionHostedService    
    )
    {
        _fuotaSessionHostedService = fuotaSessionHostedService;
    }
    
    public async Task HandleEventAsync(InitFuotaSession @event)
    {
        await _fuotaSessionHostedService.StartAsync(CancellationToken.None);
    }
}