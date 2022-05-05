using LoraGateway.SignalR;
using Microsoft.Extensions.Hosting;

namespace LoraGateway.BackgroundServices;

public class FuotaHubClientService : IHostedService
{
    private readonly SignalRClient _client;
    private readonly IHostApplicationLifetime _appLifetime;

    public FuotaHubClientService(
        SignalRClient client,
        IHostApplicationLifetime appLifetime
        )
    {
        _client = client;
        _appLifetime = appLifetime;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // _appLifetime.ApplicationStarted.Register(() =>
        // {
        //     Task.Run(async () => { await _client.Startup("http://localhost:5000/fuotaHub"); });
        // });
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // await _client.Disconnect();
    }
}