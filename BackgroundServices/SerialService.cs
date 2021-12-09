using LoraGateway.Services;
using LoraGateway.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LoraGateway.BackgroundServices;

internal sealed class SerialHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger _logger;
    private readonly SerialProcessorService _serialService;
    private readonly DeviceDataStore _store;

    public bool Continue = false;

    public SerialHostedService(
        ILogger<SerialHostedService> logger,
        DeviceDataStore store,
        SerialProcessorService serialService,
        IHostApplicationLifetime appLifetime)
    {
        _logger = logger;
        _store = store;
        _serialService = serialService;
        _appLifetime = appLifetime;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _store.LoadStore();

        _appLifetime.ApplicationStarted.Register(() =>
        {
            Task.Run(() =>
            {
                CancellationTokenSource innerCancellation = new CancellationTokenSource();
                
                try
                {
                    var ports = SerialUtil.GetStmDevicePorts("STMicroelectronics");

                    var stringComparer = StringComparer.OrdinalIgnoreCase;
                    _serialService.Initialize(ports.First().Port);
                    Task.Run(() => _serialService.WaitMessage(innerCancellation.Token), innerCancellation.Token);
                    Continue = true;
                    _logger.LogInformation("Type QUIT to exit");

                    while (Continue)
                    {
                        var message = Console.ReadLine();
                        if (stringComparer.Equals("quit", message))
                        {
                            Continue = false;
                            innerCancellation.Cancel();
                        }
                        else
                        {
                            _serialService.SerialPort?.Write($"{0xFF}{message}\0");
                        }
                    }

                    _logger.LogInformation("Closing serial");
                    _serialService.SerialPort?.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception!");
                }
                
                _logger.LogInformation("Service stopped");
            }, cancellationToken);
        });
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}