using LoraGateway.Models;
using LoraGateway.Services.Contracts;

namespace LoraGateway.Services;

public class ExperimentPhyService : JsonDataStore<ExperimentPhyConfig>
{
    private readonly ILogger<ExperimentPhyService> _logger;

    public ExperimentPhyService(
        ILogger<ExperimentPhyService> logger
        )
    {
        _logger = logger;
    }
        
    public override string GetJsonFileName()
    {
        return "experiment_rlnc_config.json";
    }
    
    public override ExperimentPhyConfig GetDefaultJson()
    {
        var jsonStore = new ExperimentPhyConfig();
        return jsonStore;
    }

    public async Task RunPhyExperiments()
    {
        var config = await LoadStore();
        var bws = config.TxBwSeries;
        foreach (var bandwidth in bws)
        {
            _logger.LogInformation("Bandwidth {BW}", bandwidth);
        }
    }
}