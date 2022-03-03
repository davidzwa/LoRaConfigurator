﻿using JKang.EventBus;
using LoraGateway.BackgroundServices;

namespace LoraGateway.Handlers;

public class InitFuotaSession
{
    public CancellationToken Token { get; set; }
    public string Message { get; set; }
}

public class StopFuotaSession
{
    public CancellationToken Token { get; set; }
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
        await _fuotaSessionHostedService.StartAsync(@event.Token);
    }
    
    public async Task HandleEventAsync(StopFuotaSession @event)
    {
        await _fuotaSessionHostedService.StopAsync(@event.Token);
    }
}