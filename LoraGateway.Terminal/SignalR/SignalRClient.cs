using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR.Client;

namespace LoraGateway.SignalR;

public class SignalRClient
{
    private readonly ILogger<SignalRClient> _logger;
    private HubConnection? _connection;

    public SignalRClient(
        ILogger<SignalRClient> logger
    )
    {
        _logger = logger;
    }

    public async Task Startup(string url = "http://localhost:53353/fuotaHub")
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(url)
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromSeconds(10) })
            .Build();
        
        _connection.On("ReceiveMessage", () =>
        {
            _logger.LogInformation("RX");
        });
        _connection.On("StartFirmwareUpdate", FirmwareUpdateStartCommand);

        _logger.LogInformation("Starting SignalR client");
        await _connection.StartAsync();
        _logger.LogInformation("SignalR client started");
    }

    public async Task Disconnect()
    {
        await _connection!.StopAsync();
    }
    
    private async Task FirmwareUpdateStartCommand()
    {
        _logger.LogInformation("FirmwareUpdate command received");
    }

    public void PushDeviceUpdateState(string deviceName, int progressPerc)
    {
        _connection.InvokeAsync("DeviceUpdateState", progressPerc, deviceName);
    }
    
}