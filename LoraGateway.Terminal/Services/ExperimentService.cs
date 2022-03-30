using LoraGateway.Models;
using LoraGateway.Services.Contracts;

namespace LoraGateway.Services;

public class ExperimentService : JsonDataStore<ExperimentConfig>
{
    private readonly ILogger<ExperimentService> _logger;
    private readonly FuotaManagerService _fuotaManagerService;
    private readonly SerialProcessorService _serialProcessorService;

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

    private bool IsStarted = false;

    public ExperimentService()
    {
    }

    public async Task RunExperiments()
    {
        var store = await LoadStore();

        var min = store.MinPer;
        var max = store.MaxPer;
        var step = store.PerStep;

        var per = min;
        while (per < max)
        {
            _logger.LogInformation("PER {PER}", per);
            
            _fuotaManagerService.SetPacketErrorRate(per);
            var fuotaConfig = _fuotaManagerService.GetStore();
            var loraMessageStart = _fuotaManagerService.RemoteSessionStartCommand();
            _serialProcessorService.SetDeviceFilter(fuotaConfig.TargetedNickname);
            _serialProcessorService.SendUnicastTransmitCommand(loraMessageStart, fuotaConfig.UartFakeLoRaRxMode);
            Thread.Sleep(1000);
            var loraMessageStop = _fuotaManagerService.RemoteSessionStopCommand();
            _serialProcessorService.SendUnicastTransmitCommand(loraMessageStop, fuotaConfig.UartFakeLoRaRxMode);
            
            per += step;
        }
    }
}