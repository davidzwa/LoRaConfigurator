﻿using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using LoRa;
using LoraGateway.Models;
using LoraGateway.Services.Contracts;

namespace LoraGateway.Services;

public class ExperimentService : JsonDataStore<ExperimentConfig>
{
    private readonly ILogger<ExperimentService> _logger;
    private readonly FuotaManagerService _fuotaManagerService;
    private readonly SerialProcessorService _serialProcessorService;

    private bool _isStarted = false;
    private bool _hasCrashed = false;
    private bool _deviceRlncRoundTerminated;
    private uint? _lastResultGenerationIndex;
    private List<DecodingUpdate> _decodingUpdatesReceived = new();
    private float _currentPer = 0.0f;
    private CancellationTokenSource _cancellationTokenSource = new();

    private List<ExperimentDataEntry> _dataPoints = new();

    private CsvConfiguration csvConfig = new(CultureInfo.InvariantCulture)
    {
        NewLine = Environment.NewLine,
    };

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

    public string GetCsvFileName()
    {
        return "experiment.csv";
    }

    public string GetPlotFileName()
    {
        return "experiment.png";
    }

    public string GetCsvFilePath()
    {
        var dataFolderAbsolute = GetDataFolderFullPath();
        var fileName = GetCsvFileName();
        return Path.Join(dataFolderAbsolute, fileName);
    }

    public string GetPlotFilePath()
    {
        var dataFolderAbsolute = GetDataFolderFullPath();
        var fileName = GetPlotFileName();
        return Path.Join(dataFolderAbsolute, fileName);
    }

    public override ExperimentConfig GetDefaultJson()
    {
        var jsonStore = new ExperimentConfig();
        return jsonStore;
    }

    public void MarkTerminationReceived()
    {
        _deviceRlncRoundTerminated = true;
        _logger.LogInformation("Stopped due to externally received termination");
    }

    public async Task ProcessResult(DecodingResult result)
    {
        try
        {
            await ProcessResultInner(result);
        }
        catch (Exception e)
        {
            _logger.LogError(e.ToString());
            _cancellationTokenSource.Cancel();
            _hasCrashed = true;
        }
    }

    public async Task ProcessUpdate(DecodingUpdate update)
    {
        try
        {
            _decodingUpdatesReceived.Add(update);
            _logger.LogInformation("Update RX {Dropped} {Received} Gen{Gen}", update.MissedGenFragments,
                update.ReceivedFragments, update.CurrentGenerationIndex);
        }
        catch (Exception e)
        {
            _logger.LogError(e.ToString());
            _cancellationTokenSource.Cancel();
            _hasCrashed = true;
        }
    }

    private async Task ProcessResultInner(DecodingResult result)
    {
        var currentGenIndex = result.CurrentGenerationIndex;
        var fuotaConfig = _fuotaManagerService.GetStore();
        var generationPacketsCount = fuotaConfig.GenerationSize + fuotaConfig.GenerationSizeRedundancy;

        if (_lastResultGenerationIndex == result.CurrentGenerationIndex)
        {
            throw new InvalidOperationException("Generations already received - Experiment failure");
        }

        // We detect skipped generations which need to be accounted
        var missedGens = _lastResultGenerationIndex == null
            ? (int)currentGenIndex
            : (int)(currentGenIndex - (_lastResultGenerationIndex! + 1));
        if (_lastResultGenerationIndex == null && currentGenIndex > 0 ||
            _lastResultGenerationIndex != null && missedGens > 0)
        {
            var missedGenerationIndices = new List<uint>();
            var missedDecodingUpdates = new List<DecodingUpdate>();
            if (_lastResultGenerationIndex != null && missedGens > 0)
            {
                missedGenerationIndices = Enumerable.Range((int)_lastResultGenerationIndex!+1, missedGens).Select(x => (uint)x).ToList();
                if (missedGenerationIndices.Count != missedGenerationIndices.Distinct().Count())
                {
                    throw new InvalidOperationException(
                        $"Missed dupes in range distinct {missedGenerationIndices.Distinct().Count()} vs range {missedGenerationIndices.Count}");
                }
            }
            else
            {
                missedGenerationIndices = Enumerable.Range(0, missedGens).Select(x => (uint)x).ToList();
                if (missedGenerationIndices.Count != missedGenerationIndices.Distinct().Count())
                {
                    throw new InvalidOperationException(
                        $"Missed dupes in range distinct {missedGenerationIndices.Distinct().Count()} vs range {missedGenerationIndices.Count}");
                }
            }
            
            var groupedDecodingUpdatesByGenIndex = _decodingUpdatesReceived.GroupBy(d => d.CurrentGenerationIndex);
            foreach (var index in missedGenerationIndices)
            {
                var foundGenUpdates = groupedDecodingUpdatesByGenIndex.FirstOrDefault(d => d.Key == index);
                if (foundGenUpdates == null)
                {
                    throw new Exception($"Could not find missed gen update for index {index}");
                }

                var maxTotalPackets = foundGenUpdates.Max(g => g.ReceivedFragments + g.MissedGenFragments);
                var lastGenUpdate = foundGenUpdates.Last();
                var totalPacketsFound = lastGenUpdate.ReceivedFragments + lastGenUpdate.MissedGenFragments;
                if (lastGenUpdate.ReceivedFragments + lastGenUpdate.MissedGenFragments != maxTotalPackets)
                {
                    throw new Exception($"Last gen update for index {index} with {totalPacketsFound} total was not newest ({maxTotalPackets})");
                }
                    
                missedDecodingUpdates.Add(lastGenUpdate);
            }
                
            _logger.LogWarning(
                "Missed generations during run - Experiment failure RecvGen:{LastGenUpdate} MissedGens:{MissedGens} PER:{CurrentPer}",
                currentGenIndex, missedDecodingUpdates.Count(), _currentPer);

            // F.e. we received index 2, so 1 and 0 were missed => 2x missed
            foreach (var missedDecodingUpdate in missedDecodingUpdates)
            {
                var total = missedDecodingUpdate.ReceivedFragments + missedDecodingUpdate.MissedGenFragments;
                _logger.LogWarning("Appending loss {MissedGenFragments} out of {TotalFragments}", missedDecodingUpdate.MissedGenFragments, total);
                var per = (float)missedDecodingUpdate.MissedGenFragments / total;
                _dataPoints.Add(new()
                {
                    Rank = 0,
                    Success = false,
                    GenerationIndex = missedDecodingUpdate.CurrentGenerationIndex,
                    GenerationTotalPackets = total,
                    GenerationRedundancyUsed = fuotaConfig.GenerationSizeRedundancy,
                    MissedPackets = missedDecodingUpdate.MissedGenFragments,
                    ReceivedPackets = missedDecodingUpdate.ReceivedFragments,
                    RngResolution = total,
                    PacketErrorRate = per,
                    ConfiguredPacketErrorRate = _currentPer
                });
            }
        }

        // Now set it straight
        if (_lastResultGenerationIndex == null)
        {
            _lastResultGenerationIndex = currentGenIndex - 1;
        }

        var success = result.Success;
        var missedFrags = result.MissedGenFragments;
        var receivedFrags = result.ReceivedFragments;
        var totalFrags = result.MissedGenFragments + receivedFrags;
        var packetErrorRate = (float)missedFrags / totalFrags;

        _dataPoints.Add(new()
        {
            Rank = result.MatrixRank,
            Success = success,
            GenerationIndex = result.CurrentGenerationIndex,
            GenerationTotalPackets = fuotaConfig.GenerationSize,
            GenerationRedundancyUsed = fuotaConfig.GenerationSizeRedundancy,
            MissedPackets = result.MissedGenFragments,
            ReceivedPackets = result.ReceivedFragments,
            RngResolution = totalFrags,
            PacketErrorRate = packetErrorRate,
            ConfiguredPacketErrorRate = _currentPer
        });

        await WriteData();

        _lastResultGenerationIndex = result.CurrentGenerationIndex;
    }

    private async Task WriteData()
    {
        if (_dataPoints.Count == 0) return;

        var filePath = GetCsvFilePath();
        using (var writer = new StreamWriter(filePath))
        {
            using (var csv = new CsvWriter(writer, csvConfig))
            {
                await csv.WriteRecordsAsync(_dataPoints);
            }
        }
    }

    private async Task AwaitTermination(CancellationToken ct)
    {
        _deviceRlncRoundTerminated = false;
        while (!_deviceRlncRoundTerminated && !ct.IsCancellationRequested)
        {
            await Task.Delay(500, ct).ConfigureAwait(true);
        }

        _logger.LogInformation("Cancelled termination");
    }

    public async Task RunExperiments()
    {
        var experimentConfig = await LoadStore();

        _dataPoints = new();
        var min = experimentConfig.MinPer;
        var max = experimentConfig.MaxPer;
        var step = experimentConfig.PerStep;

        _cancellationTokenSource = new CancellationTokenSource();
        _lastResultGenerationIndex = null;
        _hasCrashed = false;

        var per = min;
        while (per < max && !_hasCrashed)
        {
            var result = _cancellationTokenSource.TryReset();
            _decodingUpdatesReceived.Clear();

            _logger.LogInformation("Next gen started. CT: {CT}", result);
            _fuotaManagerService.SetPacketErrorRate(per);
            _currentPer = per;

            if (experimentConfig.RandomPerSeed)
            {
                var buffer = new byte[sizeof(Int32)];
                new Random().NextBytes(buffer);
                _fuotaManagerService.SetPacketErrorSeed(BitConverter.ToUInt16(buffer.Reverse().ToArray()));
            }

            var fuotaConfig = _fuotaManagerService.GetStore();

            var loraMessageStart = _fuotaManagerService.RemoteSessionStartCommand();
            _serialProcessorService.SetDeviceFilter(fuotaConfig.TargetedNickname);
            _deviceRlncRoundTerminated = false;

            _logger.LogInformation("PER {Per} - Awaiting {Ms} Ms", per, experimentConfig.ExperimentTimeout);
            _serialProcessorService.SendUnicastTransmitCommand(loraMessageStart, fuotaConfig.UartFakeLoRaRxMode);

            // Await completion            
            await Task.WhenAny(Task.Delay(experimentConfig.ExperimentTimeout, _cancellationTokenSource.Token),
                AwaitTermination(_cancellationTokenSource.Token));

            _logger.LogInformation("Await completed");

            var loraMessageStop = _fuotaManagerService.RemoteSessionStopCommand();
            _serialProcessorService.SendUnicastTransmitCommand(loraMessageStop, fuotaConfig.UartFakeLoRaRxMode);

            per += step;
            await Task.Delay(2000);
        }

        ExportPlot();

        _logger.LogInformation("Experiment done");
        _dataPoints = null;
    }

    private void ExportPlot()
    {
        if (_dataPoints.Count == 0)
        {
            _logger.LogWarning("No data points cannot export plot");
            return;
        }

        var dataBuckets = _dataPoints.GroupBy(d => d.ConfiguredPacketErrorRate);
        var perBuckets = dataBuckets.Select(b =>
        {
            return new
            {
                PerAvg = b.Average(d => d.PacketErrorRate),
                ConfiguredPer = b.Key
            };
        });
        
        _logger.LogInformation("Saving experiment plot");
        var configuredPerArray = perBuckets.Select(p => (double)p.ConfiguredPer).ToArray();
        var avgPerArray = perBuckets.Select(p => (double)p.PerAvg).ToArray();

        var plt = new ScottPlot.Plot(400, 300);
        plt.AddScatter(configuredPerArray, avgPerArray);
        plt.Title("Packet-Error-Rate (PER) vs avg. realised PER");
        plt.YLabel("Averaged realised PER");
        plt.XLabel("Configured PER");
        plt.SaveFig(GetPlotFilePath());
    }
}