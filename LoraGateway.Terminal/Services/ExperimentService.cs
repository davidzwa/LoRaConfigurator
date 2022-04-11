using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Google.Protobuf.Reflection;
using LoRa;
using LoraGateway.Models;
using LoraGateway.Services.Contracts;
using LoraGateway.Utils;
using ScottPlot;

namespace LoraGateway.Services;

public class ExperimentService : JsonDataStore<ExperimentConfig>
{
    private readonly ILogger<ExperimentService> _logger;
    private readonly FuotaManagerService _fuotaManagerService;
    private readonly ExperimentPlotService _experimentPlotService;
    private readonly SerialProcessorService _serialProcessorService;

    private bool _isStarted = false;
    private bool _hasCrashed = false;
    private bool _deviceRlncRoundTerminated;
    private uint? _lastResultGenerationIndex;
    private List<DecodingUpdate> _decodingUpdatesReceived = new();
    private float _currentPer = 0.0f;
    private CancellationTokenSource _cancellationTokenSource = new();

    private List<ExperimentDataEntry> _dataPoints = new();

    public ExperimentService(
        ILogger<ExperimentService> logger,
        FuotaManagerService fuotaManagerService,
        ExperimentPlotService experimentPlotService,
        SerialProcessorService serialProcessorService
    )
    {
        _logger = logger;
        _fuotaManagerService = fuotaManagerService;
        _experimentPlotService = experimentPlotService;
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

    public string GetCsvFilePath()
    {
        var dataFolderAbsolute = GetDataFolderFullPath();
        var fileName = GetCsvFileName();
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

    public async Task RunExperiments()
    {
        var experimentConfig = await LoadStore();

        _dataPoints = new();
        var min = experimentConfig.MinPer;
        var max = experimentConfig.MaxPer;
        var step = experimentConfig.PerStep;

        _cancellationTokenSource = new CancellationTokenSource();
        _hasCrashed = false;

        var per = min;
        var cappedMax = Math.Min(0.99999f, max);
        while (per <= cappedMax && !_hasCrashed)
        {
            var result = _cancellationTokenSource.TryReset();
            _decodingUpdatesReceived.Clear();
            _lastResultGenerationIndex = null;

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

            // Await any update or failure            
            await Task.WhenAny(Task.Delay(experimentConfig.ExperimentTimeout, _cancellationTokenSource.Token),
                AwaitTermination(_cancellationTokenSource.Token));

            _logger.LogInformation("Await completed");

            var loraMessageStop = _fuotaManagerService.RemoteSessionStopCommand();
            _serialProcessorService.SendUnicastTransmitCommand(loraMessageStop, fuotaConfig.UartFakeLoRaRxMode);

            per += step;
            await Task.Delay(500);

            // Check if some generations were not registered
            var genCount = fuotaConfig.GetGenerationCount();
            if (_lastResultGenerationIndex != genCount - 1)
            {
                // Of course this Generation Index does not really exist - its used as diff
                var lostIndices = CalculateLostGenerationIndices(genCount);
                AppendLostGenerationsToDataPoints(lostIndices, genCount);
            }

            await Task.Delay(2000);
        }

        ExportPlot();

        _logger.LogInformation("Experiment done");
        _dataPoints = null;
    }

    private async Task ProcessResultInner(DecodingResult result)
    {
        var currentGenIndex = result.CurrentGenerationIndex;
        var fuotaConfig = _fuotaManagerService.GetStore();


        if (_lastResultGenerationIndex == result.CurrentGenerationIndex)
        {
            throw new InvalidOperationException("Generations already received - Experiment failure");
        }

        // We detect skipped generations which need to be accounted
        var lostIndices = CalculateLostGenerationIndices(currentGenIndex);
        if (lostIndices.Count > 0)
        {
            AppendLostGenerationsToDataPoints(lostIndices, currentGenIndex);
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
            TriggeredByDecodingResult = true,
            TriggeredByCompleteLoss = false,
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
            using (var csv = new CsvWriter(writer, _experimentPlotService.CsvConfig))
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

    private void ExportPlot()
    {
        if (_dataPoints.Count == 0)
        {
            _logger.LogWarning("No data points cannot export plot");
            return;
        }

        _experimentPlotService.SavePlotsFromLiveData(_dataPoints);
    }

    private List<uint> CalculateLostGenerationIndices(uint currentGenIndex)
    {
        var missedGenerationIndices = new List<uint>();
        var missedGens = _lastResultGenerationIndex == null
            ? (int)currentGenIndex
            : (int)(currentGenIndex - (_lastResultGenerationIndex! + 1));
        if (_lastResultGenerationIndex == null && currentGenIndex > 0 ||
            _lastResultGenerationIndex != null && missedGens > 0)
        {
            if (_lastResultGenerationIndex != null && missedGens > 0)
            {
                missedGenerationIndices = Enumerable.Range((int)_lastResultGenerationIndex! + 1, missedGens)
                    .Select(x => (uint)x).ToList();
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
        }

        return missedGenerationIndices;
    }

    private List<DecodingUpdate> AppendLostGenerationsToDataPoints(List<uint> missedGenerationIndices,
        uint? currentGenIndex)
    {
        var missedDecodingUpdates = new List<DecodingUpdate>();
        var fuotaConfig = _fuotaManagerService.GetStore();
        var generationPacketsCount = fuotaConfig.GenerationSize + fuotaConfig.GenerationSizeRedundancy;

        var groupedDecodingUpdatesByGenIndex = _decodingUpdatesReceived.GroupBy(d => d.CurrentGenerationIndex);
        foreach (var index in missedGenerationIndices)
        {
            var foundGenUpdates = groupedDecodingUpdatesByGenIndex.FirstOrDefault(d => d.Key == index);
            var wasLostGeneration = false;
            if (foundGenUpdates == null)
            {
                foundGenUpdates = new Grouping<uint, DecodingUpdate>(index)
                {
                    new()
                    {
                        RankProgress = 0,
                        CurrentGenerationIndex = index,
                        ReceivedFragments = 0,
                        MissedGenFragments = generationPacketsCount
                    }
                };
                wasLostGeneration = true;
                _logger.LogWarning(
                    "Could not find missed gen update for index {Index} - inserted fake one with 100% loss", index);
            }

            var maxTotalPackets = foundGenUpdates.Max(g => g.ReceivedFragments + g.MissedGenFragments);
            var lastGenUpdate = foundGenUpdates.Last();
            var totalPacketsFound = lastGenUpdate.ReceivedFragments + lastGenUpdate.MissedGenFragments;
            if (lastGenUpdate.ReceivedFragments + lastGenUpdate.MissedGenFragments != maxTotalPackets)
            {
                throw new Exception(
                    $"Last gen update for index {index} with {totalPacketsFound} total was not newest ({maxTotalPackets})");
            }

            var total = lastGenUpdate.ReceivedFragments + lastGenUpdate.MissedGenFragments;
            _logger.LogWarning("Appending loss {MissedGenFragments} out of {TotalFragments}",
                lastGenUpdate.MissedGenFragments, total);

            var per = (float)lastGenUpdate.MissedGenFragments / total;
            _dataPoints.Add(new()
            {
                Rank = 0,
                Success = false,
                GenerationIndex = lastGenUpdate.CurrentGenerationIndex,
                GenerationTotalPackets = total,
                GenerationRedundancyUsed = fuotaConfig.GenerationSizeRedundancy,
                MissedPackets = lastGenUpdate.MissedGenFragments,
                ReceivedPackets = lastGenUpdate.ReceivedFragments,
                RngResolution = total,
                TriggeredByDecodingResult = false,
                TriggeredByCompleteLoss = wasLostGeneration,
                PacketErrorRate = per,
                ConfiguredPacketErrorRate = _currentPer
            });
            missedDecodingUpdates.Add(lastGenUpdate);
        }

        var genIndexString = currentGenIndex != null ? currentGenIndex.ToString() : "FINAL";
        _logger.LogWarning(
            "Missed generations during run - Experiment failure RecvGen:{LastGenUpdate} MissedGens:{MissedGens} PER:{CurrentPer}",
            genIndexString, missedDecodingUpdates.Count(), _currentPer);

        return missedDecodingUpdates;
    }
}