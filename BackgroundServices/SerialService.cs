using LoraGateway.Services;
using LoraGateway.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LoraGateway.BackgroundServices;

internal sealed class SerialHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger _logger;
    private readonly DeviceDataStore _store;
    private readonly SerialProcessorService _serialService;

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
        
        Console.WriteLine(Directory.GetCurrentDirectory());
        _logger.LogDebug($"Starting with arguments: {string.Join(" ", Environment.GetCommandLineArgs())}");

        _appLifetime.ApplicationStarted.Register(() =>
        {
            Task.Run(async () =>
            {
                try
                {
                    var ports = SerialUtil.GetStmDevicePorts("STMicroelectronics");
                    
                    var stringComparer = StringComparer.OrdinalIgnoreCase;
                    var readThread = _serialService.Initialize(ports.First().Port);
                    _serialService.Continue = true;
                    readThread.Start();

                    Console.WriteLine("Type QUIT to exit");

                    while (_serialService.Continue)
                    {
                        var message = Console.ReadLine();

                        if (stringComparer.Equals("quit", message))
                            _serialService.Continue = false;
                        else
                            _serialService.SerialPort?.Write($"{0xFF}{message}\0");
                    }

                    readThread.Join();
                    await _store.AddDevice(new()
                    {
                        Id = _serialService.ConvertDeviceId(_serialService.LastBootMessage?.DeviceIdentifier),
                        Meta = { },
                        IsGateway = false
                    });
                    _serialService.SerialPort?.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception!");
                }
            }, cancellationToken);
        });
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}