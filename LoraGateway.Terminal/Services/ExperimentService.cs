using LoraGateway.Models;
using LoraGateway.Services.Contracts;

namespace LoraGateway.Services;

public class ExperimentService: JsonDataStore<ExperimentConfig>
{
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

    public void ShowExperiment()
    {
        
    }
}