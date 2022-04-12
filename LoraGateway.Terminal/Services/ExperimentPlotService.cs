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

    public string GetGenSuccessRateErrPlotFileName()
    {
        return "experiment_success_rate_err.png";
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
            var perIqr = Statistics.Quartiles(b.Select(b => b.PacketErrorRate).ToArray());
            var genSuccessRate = b.Average(g => g.Success ? 1.0f : 0.0f);
            var genSuccessRateError = Statistics.Quartiles(b.Select(g => g.Success ? 1.0f : 0.0f).ToArray());
            return new
            {
                PerAvg = b.Average(d => d.PacketErrorRate),
                PerIQR = perIqr.IQR,
                ConfiguredPer = b.Key,
                GenSuccessRate = genSuccessRate,
                GenSuccessError = genSuccessRateError.IQR
            };
        });

        var configuredPerArray = perBuckets.Select(p => (double)p.ConfiguredPer).ToArray();
        var averagePerArray = perBuckets.Select(p => (double)p.PerAvg).ToArray();
        var iqrPerArray = perBuckets.Select(p => (double)p.PerIQR).ToArray();
        var genSuccessRateArray = perBuckets.Select(p => (double)p.GenSuccessRate).ToArray();
        var genSuccessErrorArray = perBuckets.Select(p => (double)p.GenSuccessError).ToArray();

        SavePlots(new()
        {
            InputPerArray = configuredPerArray,
            ErrorArray = iqrPerArray,
            AveragePerArray = averagePerArray,
            GenSuccessRateArray = genSuccessRateArray,
            GenSuccessErrorArray = genSuccessErrorArray
        });
    }

    class GenSuccess
    {
        public bool Success { get; set; }
        public uint GenerationIndex { get; set; }
        public float OriginalPer { get; set; }
    }
    
    /**
     * This will plot each PER as a scatter (X: Redundancy, Y: Success Rate)
     */
    public void SaveMultiGenPlot(List<ExperimentDataUpdateEntry> filteredUpdateEntries, uint maxRedundancy)
    {
        // We first need to collect all Success vs Redundancy pairs for each PER
        var plots = filteredUpdateEntries.GroupBy(f => f.PerConfig).Select(per =>
        {
            // Build up a histogram of redundancy vs successes
            var successRedundancyHist = new Dictionary<uint, List<GenSuccess>>();
            foreach (var sample in per)
            {
                // sample.Success
                // if red == max => all lower have failed (successes 0)
                // if red < max => lower failed, higher/equal success (partial success)
                // if red == 0 => all higher have succeeded (successes max)
                for (uint i = 0; i < maxRedundancy + 1; i++)
                {
                    if (!successRedundancyHist.ContainsKey(i))
                    {
                        successRedundancyHist.Add(i, new List<GenSuccess>());
                    }
                    successRedundancyHist[i].Add(new ()
                    {
                        Success = sample.RedundancyUsed >= i && sample.Success,
                        GenerationIndex = sample.GenerationIndex,
                        OriginalPer = per.Key
                    });
                }

            }

            // Convert histogram into rates
            var successRates = new List<double>();
            var redundanciesCounted = new List<double>();
            return new
            {
                Key = g.Key,
                DataPoints = dataPoints.ToList()
            };
        });
    }

    public void SavePlots(ExperimentPlotDto data)
    {
        _logger.LogInformation("Saving experiment plot");
        var configuredPerArray = data.InputPerArray;
        var averagePerArray = data.AveragePerArray;
        var perErrorArray = data.ErrorArray;

        SaveNormalPlot(configuredPerArray, averagePerArray, configuredPerArray);
        SaveNormalWithErrorBarPlot(configuredPerArray, averagePerArray, perErrorArray, configuredPerArray);
        SaveGenSuccessPlot(configuredPerArray, data.GenSuccessRateArray);
        SaveGenSuccessWithErrorPlot(configuredPerArray, data.GenSuccessRateArray, data.GenSuccessErrorArray);
    }

    private void SaveNormalPlot(double[] per, double[] yAxisRealPer, double[] yAxisInputPer)
    {
        var maxPer = Math.Min(1.0f, Math.Round(per.Max() + 0.1f, 1));
        var minPer = Math.Max(0.0f, Math.Round(per.Min() - 0.1f, 1));

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

    private void SaveNormalWithErrorBarPlot(double[] per, double[] yAxisRealPer, double[] yAxisRealPerError,
        double[] yAxisInputPer)
    {
        var maxPer = Math.Min(1.0f, Math.Round(per.Max() + 0.1f, 1));
        var minPer = Math.Max(0.0f, Math.Round(per.Min() - 0.1f, 1));

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

    private void SaveGenSuccessPlot(double[] per, double[] genSuccessRate)
    {
        var maxPer = Math.Min(1.0f, Math.Round(per.Max() + 0.1f, 1));
        var minPer = Math.Max(0.0f, Math.Round(per.Min() - 0.1f, 1));

        var plt = new Plot(400, 300);
        plt.AddScatter(per, genSuccessRate, label: "Gen. success rate");
        plt.Legend();
        plt.SetAxisLimits(minPer, maxPer, 0.0f, 1.0f);
        plt.Title("Generation Success Rate (GSR) vs PER");
        plt.YLabel("Generation Success Rate (GSR)");
        plt.XLabel("PER");
        plt.SaveFig(GetPlotFilePath(GetGenSuccessRatePlotFileName()));
    }

    private void SaveGenSuccessWithErrorPlot(double[] per, double[] genSuccessRate, double[] yAxisSuccessRateError)
    {
        var maxPer = Math.Min(1.0f, Math.Round(per.Max() + 0.1f, 1));
        var minPer = Math.Max(0.0f, Math.Round(per.Min() - 0.1f, 1));

        var plt = new Plot(400, 300);
        plt.AddErrorBars(per, genSuccessRate, null, yAxisSuccessRateError);
        var scatter = plt.AddScatter(per, genSuccessRate, label: "Gen. success rate");
        scatter.YError = yAxisSuccessRateError;
        scatter.ErrorCapSize = 3;
        scatter.ErrorLineWidth = 1;
        scatter.LineStyle = LineStyle.Dot;
        plt.Legend();
        plt.SetAxisLimits(minPer, maxPer, 0.0f, 1.0f);
        plt.Title("Generation Success Rate (GSR) vs PER");
        plt.YLabel("Generation Success Rate (GSR)");
        plt.XLabel("PER");
        plt.SaveFig(GetPlotFilePath(GetGenSuccessRateErrPlotFileName()));
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