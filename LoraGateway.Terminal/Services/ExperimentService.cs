using System.Timers;
using LoraGateway.Models;
using LoraGateway.Services.Contracts;

namespace LoraGateway.Services;

public class ExperimentService : JsonDataStore<ExperimentConfig>
{
    private readonly ILogger<ExperimentService> _logger;
    private readonly FuotaManagerService _fuotaManagerService;
    private readonly SerialProcessorService _serialProcessorService;

    private bool IsStarted = false;
    private bool DeviceRlncRoundTerminated = false;
    private CancellationTokenSource _cancellationTokenSource;

    public ExperimentService(
        ILogger<ExperimentService> logger,
        FuotaManagerService fuotaManagerService,
        SerialProcessorService serialProcessorService
    )
    {
        _logger = logger;
        _fuotaManagerService = fuotaManagerService;
        _serialProcessorService = serialProcessorService;
    }

    public override string GetJsonFileName()
    {
        return "experiment_config.json";
    }

    public override ExperimentConfig GetDefaultJson()
    {
        var jsonStore = new ExperimentConfig();
        return jsonStore;
    }

    public void MarkTerminationReceived()
    {
        DeviceRlncRoundTerminated = true;
        _logger.LogInformation("Stopped due to externally received termination");
    }

    private async Task AwaitTermination(CancellationToken ct)
    {
        DeviceRlncRoundTerminated = false;
        while (!DeviceRlncRoundTerminated && !ct.IsCancellationRequested)
        {
            await Task.Delay(500, ct).ConfigureAwait(true);
        }

        _logger.LogInformation("Cancelled termination");
    }

    public async Task RunExperiments()
    {
        var experimentConfig = await LoadStore();

        var min = experimentConfig.MinPer;
        var max = experimentConfig.MaxPer;
        var step = experimentConfig.PerStep;

        _cancellationTokenSource = new CancellationTokenSource();

        var per = min;
        while (per < max)
        {
            var result = _cancellationTokenSource.TryReset();
            
            _logger.LogInformation("Next gen started. CT: {CT}", result);
            _fuotaManagerService.SetPacketErrorRate(per);

            if (experimentConfig.RandomPerSeed)
            {
                var buffer = new byte[sizeof(Int32)];
                new Random().NextBytes(buffer);
                _fuotaManagerService.SetPacketErrorSeed(BitConverter.ToUInt16(buffer.Reverse().ToArray()));
            }
                
            var fuotaConfig = _fuotaManagerService.GetStore();
            
            var loraMessageStart = _fuotaManagerService.RemoteSessionStartCommand();
            _serialProcessorService.SetDeviceFilter(fuotaConfig.TargetedNickname);
            DeviceRlncRoundTerminated = false;

            _logger.LogInformation("PER {Per} - Awaiting {Ms} Ms", per, experimentConfig.ExperimentTimeout);
            _serialProcessorService.SendUnicastTransmitCommand(loraMessageStart, fuotaConfig.UartFakeLoRaRxMode);

            // Await completion            
            await Task.WhenAny(Task.Delay(experimentConfig.ExperimentTimeout, _cancellationTokenSource.Token),
                AwaitTermination(_cancellationTokenSource.Token));

            _logger.LogInformation("Await completed");

            var loraMessageStop = _fuotaManagerService.RemoteSessionStopCommand();
            _serialProcessorService.SendUnicastTransmitCommand(loraMessageStop, fuotaConfig.UartFakeLoRaRxMode);

            per += step;
            await Task.Delay(1000);
        }

        _logger.LogInformation("Experiment done");
    }
}