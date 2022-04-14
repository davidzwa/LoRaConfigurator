using System.ComponentModel.DataAnnotations;
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

    const double MaxRedundancyScale = 3.0;

    public string GetPlotFileName()
    {
        return "experiment.png";
    }

    public string GetPlotMultiPerSuccessRateFileName()
    {
        return "experiment_multi_per_success_rate.png";
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

    public string GetCsvFileNameFilteredGenUpdates()
    {
        return "experiment_filtered_gen_updates.csv";
    }

    public string GetDefaultCsvFilePath(string fileName)
    {
        var dataFolderAbsolute = DataStoreExtensions.BasePath;
        return Path.Join(dataFolderAbsolute, fileName);
    }

    public string GetPlotFilePath(string fileName)
    {
        var folder = DataStoreExtensions.BasePath;
        var dataFolderAbsolute = Path.GetFullPath(folder, Directory.GetCurrentDirectory());

        return Path.Join(dataFolderAbsolute, fileName);
    }

    public void SaveSuccessRatePlotsFromCsv(string csvFileName = "")
    {
        var path = String.IsNullOrEmpty(csvFileName)
            ? GetDefaultCsvFilePath(GetCsvFileNameFilteredGenUpdates())
            : csvFileName;
        var perGenUpdateEntries = LoadSuccessRatePerData(path).ToList();
        var maxRedundancy = perGenUpdateEntries.First().RedundancyMax;

        _logger.LogInformation("Found {Entries} entries in CSV", perGenUpdateEntries.Count);

        SaveMultiGenPlot(perGenUpdateEntries, maxRedundancy);
        _logger.LogInformation("Saved plot");
    }

    public void SavePlotsFromCsv(string csvFileName = "")
    {
        var path = String.IsNullOrEmpty(csvFileName) ? GetDefaultCsvFilePath(GetCsvFileName()) : csvFileName;
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
        public double Success { get; set; }
        // public uint GenerationIndex { get; set; }
        // public float OriginalPer { get; set; }
    }

    class PerSuccessRates
    {
        public float Per { get; set; }
        public uint[] SuccessThresholdHistogram { get; set; }
        public double[] SuccessCumulative { get; set; }
    }

    /**
     * This will plot each PER as a scatter (X: Redundancy, Y: Success Rate)
     */
    public void SaveMultiGenPlot(List<ExperimentDataUpdateEntry> filteredUpdateEntries, uint maxRedundancy)
    {
        // We first need to collect all Success vs Redundancy pairs for each PER
        var perGroupings = filteredUpdateEntries
            .GroupBy(f => f.PerConfig);

        var perPlotsCumul = perGroupings.Select(per =>
        {
            // Build up a cumulative distro and histogram of redundancy vs successes
            var successRedundancyCumul = new Dictionary<uint, List<GenSuccess>>();
            var successThresholdHistogram = new uint [maxRedundancy + 1];
            foreach (var genSample in per)
            {
                // sample.Success
                var redundancyThreshold = genSample.Success ? genSample.RedundancyUsed : (int)genSample.RedundancyMax;
                if (redundancyThreshold < 0)
                {
                    throw new InvalidOperationException("Cant finish generation with negative redundancy");
                }

                successThresholdHistogram[redundancyThreshold]++;

                // if red == max => all lower have failed (successes 0)
                // if red < max => lower failed, higher/equal success (partial success)
                // if red == 0 => all higher have succeeded (successes max)
                for (uint i = 0; i <= maxRedundancy; i++)
                {
                    if (!successRedundancyCumul.ContainsKey(i))
                    {
                        successRedundancyCumul.Add(i, new List<GenSuccess>());
                    }

                    // Add the success sample for this redundancy (if succeeded)
                    successRedundancyCumul[i].Add(new()
                    {
                        Success = genSample.RedundancyUsed <= i && genSample.Success ? 1.0 : 0.0
                        // GenerationIndex = genSample.GenerationIndex,
                        // OriginalPer = per.Key
                    });
                }
            }

            // Tuple of (Index: redundancy used, Value: success rate)
            var redundancySuccessRatesTuples =
                successRedundancyCumul.Select((k, v) => (k.Key, k.Value.Average(v => v.Success)));

            // Should validate the tuple is not incorrectly ordered
            var redundancySuccessRates = redundancySuccessRatesTuples.Select(t => t.Item2);

            // Convert histogram into rates
            return new PerSuccessRates()
            {
                Per = per.Key,
                SuccessThresholdHistogram = successThresholdHistogram,
                SuccessCumulative = redundancySuccessRates.ToArray()
            };
        });

        var xAxis = Enumerable
            .Range(0, (int)maxRedundancy + 1)
            .Select(v => MaxRedundancyScale * v / ((int)maxRedundancy + 1.0))
            .ToArray();

        SaveSuccessRatePerPlots(xAxis, perPlotsCumul.ToList());
    }

    private void SaveSuccessRatePerPlots(double[] xAxisPercentage, List<PerSuccessRates> ySuccessRate)
    {
        var plt = new Plot(600, 400);
        foreach (var perPlot in ySuccessRate)
        {
            var per100 = perPlot.Per * 100.0f;
            var c = ScottPlot.Drawing.Colormap.Viridis.GetColor(perPlot.Per);
            plt.AddScatter(xAxisPercentage, perPlot.SuccessCumulative, label: $"PER {per100:F1}%", color: c);
        }

        // plt.Legend();
        var cmap = ScottPlot.Drawing.Colormap.Viridis;
        plt.AddColorbar(cmap);
        plt.SetAxisLimits(0, xAxisPercentage.Max(), 0.0f, 1.0f);
        plt.Title("Success Rate vs Packet Redundancy for uniform PER");
        plt.YLabel("Generation Success Rate");
        plt.XLabel("Packet Redundancy (%)");
        plt.SaveFig(GetPlotFilePath(GetPlotMultiPerSuccessRateFileName()));
    }

    private void SaveSuccessThresholdPerHistograms(double[] xAxis, List<PerSuccessRates> ySuccessRate)
    {
        // TODO wip
        var plt = new Plot(600, 400);
        foreach (var perPlot in ySuccessRate)
        {
            var per100 = perPlot.Per * 100.0f;
            var c = ScottPlot.Drawing.Colormap.Viridis.GetColor(perPlot.Per);
            plt.AddScatter(xAxis, perPlot.SuccessCumulative, label: $"PER {per100:F1}%", color: c);
        }

        // plt.Legend();
        var cmap = ScottPlot.Drawing.Colormap.Viridis;
        plt.AddColorbar(cmap);
        plt.SetAxisLimits(0, xAxis.Max(), 0.0f, 1.0f);
        plt.Title("Success Threshold Histogram vs Packet Redundancy for uniform PER");
        plt.YLabel("Generation Success Rate");
        plt.XLabel("Packet Redundancy");
        plt.SaveFig(GetPlotFilePath(GetPlotMultiPerSuccessRateFileName()));
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

    private IEnumerable<ExperimentDataUpdateEntry> LoadSuccessRatePerData(string fileName)
    {
        var data = new List<ExperimentDataUpdateEntry>();
        using (var reader = new StreamReader(fileName))
        {
            using (var csv = new CsvReader(reader, CsvConfig))
            {
                data = csv.GetRecords<ExperimentDataUpdateEntry>().ToList();
            }
        }

        return data;
    }
}