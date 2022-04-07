using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using LoraGateway.Models;
using LoraGateway.Services.Contracts;
using LoraGateway.Utils;
using ScottPlot;

namespace LoraGateway.Services;

public class ExperimentPlotService
{
    private readonly ILogger<ExperimentPlotService> _logger;

    public ExperimentPlotService(
        ILogger<ExperimentPlotService> logger
    )
    {
        _logger = logger;
    }
    public CsvConfiguration CsvConfig = new(CultureInfo.InvariantCulture)
    {
        NewLine = Environment.NewLine,
    };
    
    public string GetPlotFileName()
    {
        return "experiment.png";
    }
    
    public string GetErrorPlotFileName()
    {
        return "experiment_err.png";
    }
    
    public string GetGenSuccessRatePlotFileName()
    {
        return "experiment_success_rate.png";
    }
    public string GetCsvFileName()
    {
        return "experiment.csv";
    }

    public string GetDefaultCsvFilePath()
    {
        var dataFolderAbsolute = DataStoreExtensions.BasePath;
        var fileName = GetCsvFileName();
        return Path.Join(dataFolderAbsolute, fileName);
    }
    public string GetPlotFilePath(string fileName)
    {
        var folder = DataStoreExtensions.BasePath;
        var dataFolderAbsolute = Path.GetFullPath(folder, Directory.GetCurrentDirectory());
        
        return Path.Join(dataFolderAbsolute, fileName);
    }

    public void SavePlotsFromCsv(string csvFileName = "")
    {
        var path = String.IsNullOrEmpty(csvFileName) ? GetDefaultCsvFilePath() : csvFileName;
        var dataEntries = LoadData(path).ToList();
        
        SavePlotsFromLiveData(dataEntries);
    }
    
    public void SavePlotsFromLiveData(List<ExperimentDataEntry> dataEntries)
    {
        var dataBuckets = dataEntries.GroupBy(d => d.ConfiguredPacketErrorRate);
        var perBuckets = dataBuckets.Select(b =>
        {
            var iqr = Statistics.Quartiles(b.Select(b => b.PacketErrorRate).ToArray());
            var genSuccessRate = b.Average(g => g.Success ? 1.0f : 0.0f);
            return new
            {
                PerAvg = b.Average(d => d.PacketErrorRate),
                PerIQR = iqr.IQR,
                ConfiguredPer = b.Key,
                GenSuccessRate = genSuccessRate
            };
        });

        _logger.LogInformation("Saving experiment plot");
        var configuredPerArray = perBuckets.Select(p => (double)p.ConfiguredPer).ToArray();
        var averagePerArray = perBuckets.Select(p => (double)p.PerAvg).ToArray();
        var iqrPerArray = perBuckets.Select(p => (double)p.PerIQR).ToArray();
        var genSuccessRateArray = perBuckets.Select(p => (double)p.GenSuccessRate).ToArray();
        
        SavePlots(new()
        {
            InputPerArray = configuredPerArray,
            ErrorArray = iqrPerArray,
            AveragePerArray = averagePerArray,
            GenSuccessRateArray = genSuccessRateArray
        });
    }
    
    public void SavePlots(ExperimentPlotDto data)
    {
        _logger.LogInformation("Saving experiment plot");
        var configuredPerArray = data.InputPerArray;
        var averagePerArray = data.AveragePerArray;
        var perErrorArray = data.ErrorArray;

        SaveNormalPlot(configuredPerArray, averagePerArray, configuredPerArray);
        SaveErrorBarPlot(configuredPerArray, averagePerArray, perErrorArray, configuredPerArray);
        SaveGenSuccessPlot(configuredPerArray, data.GenSuccessRateArray);
    }

    private void SaveNormalPlot(double[] per, double[] yAxisRealPer, double[] yAxisInputPer)
    {
        var maxPer = Math.Min(1.0f, Math.Round(per.Max()+0.1f, 1));
        var minPer = Math.Max(0.0f, Math.Round(per.Min()-0.1f, 1));
        
        var plt = new Plot(400, 300);
        plt.AddScatter(per, yAxisRealPer, label: "Real PER");
        plt.AddScatter(per, yAxisInputPer, label: "Input PER");
        plt.Legend();
        plt.SetAxisLimits(minPer, maxPer, minPer, maxPer);
        plt.Title("Packet-Error-Rate (PER) vs median realised PER");
        plt.YLabel("Averaged realised PER");
        plt.XLabel("Configured PER");
        plt.SaveFig(GetPlotFilePath(GetPlotFileName()));
    }
    
    private void SaveGenSuccessPlot(double[] per, double[] genSuccessRate /*, double[] genSuccessCount*/)
    {
        var maxPer = Math.Min(1.0f, Math.Round(per.Max()+0.1f, 1));
        var minPer = Math.Max(0.0f, Math.Round(per.Min()-0.1f, 1));
        
        var plt = new Plot(400, 300);
        plt.AddScatter(per, genSuccessRate, label: "Gen. success rate");
        plt.Legend();
        plt.SetAxisLimits(minPer, maxPer, 0.0f, 1.0f);
        plt.Title("Generation Success Rate (GSR) vs PER");
        plt.YLabel("Generation Success Rate (GSR)");
        plt.XLabel("PER");
        plt.SaveFig(GetPlotFilePath(GetGenSuccessRatePlotFileName()));
    }
    
    private void SaveErrorBarPlot(double[] per, double[] yAxisRealPer, double[] yAxisRealPerError, double[] yAxisInputPer)
    {
        var maxPer = Math.Min(1.0f, Math.Round(per.Max()+0.1f, 1));
        var minPer = Math.Max(0.0f, Math.Round(per.Min()-0.1f, 1));
        
        var plt2 = new Plot(400, 300);
        var scatter2 = plt2.AddScatter(per, yAxisRealPer, label: "Real PER");
        scatter2.YError = yAxisRealPerError;
        scatter2.ErrorCapSize = 3;
        scatter2.ErrorLineWidth = 1;
        scatter2.LineStyle = LineStyle.Dot;
        plt2.AddErrorBars(per, yAxisRealPer, null, yAxisRealPerError);
        plt2.AddScatter(per, yAxisInputPer, label: "Input PER");
        plt2.Legend();
        
        plt2.SetAxisLimits(minPer, maxPer, minPer, maxPer);
        plt2.Title("Packet-Error-Rate (PER) vs avg. realised PER");
        plt2.YLabel("Averaged realised PER");
        plt2.XLabel("Configured PER");
        plt2.SaveFig(GetPlotFilePath(GetErrorPlotFileName()));
    }
    
    private IEnumerable<ExperimentDataEntry> LoadData(string fileName)
    {
        IEnumerable<ExperimentDataEntry> data = new List<ExperimentDataEntry>();
        using (var reader = new StreamReader(fileName))
        {
            using (var csv = new CsvReader(reader, CsvConfig))
            {
                data = csv.GetRecords<ExperimentDataEntry>().ToList();
            }
        }

        return data;
    }
}