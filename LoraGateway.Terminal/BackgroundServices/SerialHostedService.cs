using LoraGateway.Services;
using LoraGateway.Utils;
using Microsoft.Extensions.Hosting;

namespace LoraGateway.BackgroundServices;

public sealed class SerialHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger _logger;
    private readonly MeasurementsService _measurementsService;
    private readonly SerialWatcher _serialPortWatcher;
    private readonly DeviceDataStore _store;

    public SerialHostedService(
        ILogger<SerialHostedService> logger,
        DeviceDataStore store,
        SerialWatcher serialPortWatcher,
        MeasurementsService measurementsService,
        IHostApplicationLifetime appLifetime)
    {
        _logger = logger;
        _store = store;
        _serialPortWatcher = serialPortWatcher;
        _measurementsService = measurementsService;
        _appLifetime = appLifetime;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Load data store
        await _store.LoadStore();
        _measurementsService.InitMeasurements();

        // Initiate the event watchers
        _serialPortWatcher.Initiate();

        _appLifetime.ApplicationStarted.Register(() =>
        {
            Task.Run(() =>
            {
                try
                {
                    var ports = SerialUtil.GetStmDevicePorts();
                    foreach (var port in ports) _serialPortWatcher.CreateMessageProcessor(port.PortName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled setup exception!");
                }
            }, cancellationToken);
        });
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _serialPortWatcher.Dispose();
        return Task.CompletedTask;
    }
}