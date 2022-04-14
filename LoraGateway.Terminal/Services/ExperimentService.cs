using CsvHelper;
using LoRa;
using LoraGateway.Models;
using LoraGateway.Services.Contracts;
using LoraGateway.Utils;

namespace LoraGateway.Services;

public class ExperimentService : JsonDataStore<ExperimentConfig>
{
    private readonly ILogger<ExperimentService> _logger;
    private readonly FuotaManagerService _fuotaManagerService;
    private readonly ExperimentPlotService _experimentPlotService;
    private readonly SerialProcessorService _serialProcessorService;

    private bool _hasCrashed;
    private bool _initReceived;
    private bool _deviceRlncRoundTerminated;
    private uint? _lastResultGenerationIndex;
    private DateTime _lastUpdateReceived = DateTime.Now;
    private List<ExperimentDataUpdateEntry> _allDecodingUpdatesReceived = new();
    private List<ExperimentDataUpdateEntry> _filteredGenUpdates = new();
    private ExperimentDataUpdateEntry[] _currentPerGenUpdates = { };
    private float _currentPer;
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

    public string GetCsvFileNameUpdates()
    {
        return "experiment_updates.csv";
    }

    public string GetCsvFileNameFilteredGenUpdates()
    {
        return "experiment_filtered_gen_updates.csv";
    }

    public string GetCsvFilePath(string fileName)
    {
        var dataFolderAbsolute = GetDataFolderFullPath();
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

    public async Task ProcessInitAck(string deviceSource, RlncRemoteFlashResponse response)
    {
        _logger.LogInformation("Source {Source} init command Frags{FragCount} Gens{GenCount}",
            deviceSource,
            response.TotalFrameCount,
            response.GenerationCount);

        _initReceived = true;

        var store = _fuotaManagerService.GetStore();
        store.GenerationSize = response.GenerationSize;
        store.GenerationSizeRedundancy = response.GenerationRedundancySize;
        store.FakeFragmentSize = response.FrameSize;
        store.FakeFragmentCount = response.TotalFrameCount;
        _fuotaManagerService.UpdateConfig(store);
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
            uint currentGenIndex = update.CurrentGenerationIndex;
            var config = _fuotaManagerService.GetStore();

            _lastUpdateReceived = DateTime.Now;
            ExperimentDataUpdateEntry lastUpdate = new()
            {
                PerConfig = _currentPer,
                CurrentSequenceNumber = update.CurrentSequenceNumber,
                GenerationIndex = update.CurrentGenerationIndex,
                CurrentFragmentIndex = update.CurrentFragmentIndex,
                IsRunning = update.IsRunning,
                Success = update.Success,
                Rank = update.RankProgress,
                MissedPackets = update.MissedGenFragments,
                ReceivedPackets = update.ReceivedFragments,
                PrngStateAfter = update.CurrentPrngState,
                PrngStateBefore = update.UsedPrngSeedState,
                FirstRowCrc8 = update.FirstRowCrc8,
                LastRowCrc8 = update.LastRowCrc8,
                RedundancyUsed = (int)(update.CurrentFragmentIndex + 1 - config.GenerationSize),
                RedundancyMax = config.GenerationSizeRedundancy
            };
            _allDecodingUpdatesReceived.Add(lastUpdate);
            if (_currentPerGenUpdates.Length < currentGenIndex + 1)
            {
                _logger.LogWarning("Error when receiving generation update {GenCount} is more than {AllocSize}",
                    currentGenIndex + 1, _currentPerGenUpdates.Length);
                return;
            }

            _currentPerGenUpdates[currentGenIndex] = lastUpdate;

            _logger.LogInformation("Update RX {Dropped} {Received} Gen{Gen} Success {Success}",
                update.MissedGenFragments,
                update.ReceivedFragments,
                update.CurrentGenerationIndex,
                update.Success);
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
        var min100 = experimentConfig.MinPer;
        var max100 = experimentConfig.MaxPer;
        var step = experimentConfig.PerStep;

        _cancellationTokenSource = new CancellationTokenSource();
        _hasCrashed = false;

        var per100 = min100;
        var cappedMax = Math.Min(100, max100);
        _lastUpdateReceived = DateTime.Now;
        while (per100 <= cappedMax && !_hasCrashed)
        {
            _logger.LogInformation("Next gen started");
            _fuotaManagerService.SetPacketErrorRate(per100 / 100.0f);
            _currentPer = per100 / 100.0f;

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

            // Allocate and clear results
            _currentPerGenUpdates = new ExperimentDataUpdateEntry[fuotaConfig.GetGenerationCount()];
            _allDecodingUpdatesReceived.Clear();
            _lastResultGenerationIndex = null;

            // Await initial response with used config (so we can dynamically update FuotaManager)
            _initReceived = false;
            _serialProcessorService.SendUnicastTransmitCommand(loraMessageStart, fuotaConfig.UartFakeLoRaRxMode);
            _logger.LogInformation("PER {Per}% - Awaiting {Ms} Ms", per100, experimentConfig.ExperimentInitAckTimeout);
            await Task.Delay(experimentConfig.ExperimentInitAckTimeout, _cancellationTokenSource.Token);

            if (!_initReceived)
            {
                _cancellationTokenSource.Cancel();
                _logger.LogInformation("Init not received, exiting experiment");
                _hasCrashed = true;
                break;
            }

            // Await any update or failure
            var timeDiff = DateTime.Now - _lastUpdateReceived;
            while (timeDiff.Milliseconds < experimentConfig.ExperimentUpdateTimeout && !_deviceRlncRoundTerminated)
            {
                _cancellationTokenSource.TryReset();
                await Task.WhenAny(Task.Delay(experimentConfig.ExperimentUpdateTimeout, _cancellationTokenSource.Token),
                    AwaitTermination(_cancellationTokenSource.Token));
                timeDiff = DateTime.Now - _lastUpdateReceived;
            }

            _logger.LogInformation("Await completed");

            var loraMessageStop = _fuotaManagerService.RemoteSessionStopCommand();
            _serialProcessorService.SendUnicastTransmitCommand(loraMessageStop, fuotaConfig.UartFakeLoRaRxMode);

            per100 += step;

            // Check if some generations were not registered
            var genCount = fuotaConfig.GetGenerationCount();
            if (_lastResultGenerationIndex != genCount - 1)
            {
                // Of course this Generation Index does not really exist - its used as diff
                var lostIndices = CalculateLostGenerationIndices(genCount);
                ConvertGenerationsToDataPoints(lostIndices, genCount);
            }

            await WriteDataUpdates();
            await WriteDataFilteredGenUpdates();

            if (!_hasCrashed)
            {
                ExportPlot();
            }

            await Task.Delay(2000);
        }

        _logger.LogInformation("Experiment done");
        _dataPoints = null;
    }
    
    private async Task AwaitTermination(CancellationToken ct)
    {
        _deviceRlncRoundTerminated = false;
        while (!_deviceRlncRoundTerminated && !ct.IsCancellationRequested)
        {
            await Task.Delay(500, ct).ConfigureAwait(true);
        }
    }

    private void ExportPlot()
    {
        if (_dataPoints.Count == 0)
        {
            _logger.LogWarning("No data points cannot export plot");
            return;
        }

        _experimentPlotService.SavePlotsFromLiveData(_dataPoints);
        var maxRedundancy = _filteredGenUpdates.First().RedundancyMax;
        _experimentPlotService.SaveMultiGenPlot(_filteredGenUpdates, maxRedundancy);
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

    private void ConvertGenerationsToDataPoints(List<uint> missedGenerationIndices,
        uint? currentGenIndex)
    {
        var missedDecodingUpdates = new List<ExperimentDataUpdateEntry>();
        var fuotaConfig = _fuotaManagerService.GetStore();
        var generationPacketsCount = fuotaConfig.GenerationSize + fuotaConfig.GenerationSizeRedundancy;

        var groupedDecodingUpdatesByGenIndex = _allDecodingUpdatesReceived.GroupBy(d => d.GenerationIndex);
        foreach (var index in missedGenerationIndices)
        {
            // Cover for generations without any update - fill in gap with 100% PER (a DNF iteration)
            var foundGenUpdates = groupedDecodingUpdatesByGenIndex.FirstOrDefault(d => d.Key == index);
            var wasLostGeneration = false;
            if (foundGenUpdates == null)
            {
                ExperimentDataUpdateEntry emptyEntry = new()
                {
                    PerConfig = _currentPer,
                    Rank = 0,
                    GenerationIndex = index,
                    ReceivedPackets = 0,
                    MissedPackets = generationPacketsCount,
                    RedundancyUsed = (int)fuotaConfig.GenerationSizeRedundancy,
                    RedundancyMax = fuotaConfig.GenerationSizeRedundancy,
                    Success = false,
                    IsRunning = false
                };
                foundGenUpdates = new Grouping<uint, ExperimentDataUpdateEntry>(index)
                {
                    emptyEntry
                };
                _currentPerGenUpdates[index] = emptyEntry;
                wasLostGeneration = true;
                _logger.LogWarning(
                    "Could not find missed gen update for index {Index} - inserted fake one with 100% loss", index);
            }

            // Find the first success or take the last update (DNF)
            var orderedUpdates = foundGenUpdates.OrderBy(g => g.CurrentFragmentIndex);
            var lastGenUpdate = foundGenUpdates.FirstOrDefault(u => u.Success);
            if (lastGenUpdate == null)
            {
                lastGenUpdate = orderedUpdates.Last();
            }

            _filteredGenUpdates.Add(lastGenUpdate);

            // Process first successful (or the last) update
            var total = lastGenUpdate.ReceivedPackets + lastGenUpdate.MissedPackets;
            _logger.LogWarning(
                "Appending gen result (Success:{Success}) PER {MissedGenFragments} out of {TotalFragments}",
                lastGenUpdate.Success,
                lastGenUpdate.MissedPackets,
                total
            );
            var per = (float)lastGenUpdate.MissedPackets / total;
            _dataPoints.Add(new()
            {
                Rank = 0,
                Success = lastGenUpdate.Success,
                GenerationIndex = lastGenUpdate.GenerationIndex,
                GenerationTotalPackets = total,
                GenerationRedundancyUsed = fuotaConfig.GenerationSizeRedundancy,
                MissedPackets = lastGenUpdate.MissedPackets,
                ReceivedPackets = lastGenUpdate.ReceivedPackets,
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
            "Saving generation results during run RecvGen:{LastGenUpdate} MissedGens:{MissedGens} PER:{CurrentPer}",
            genIndexString, missedDecodingUpdates.Count(), _currentPer);
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
            ConvertGenerationsToDataPoints(lostIndices, currentGenIndex);
        }

        var success = result.Success;
        var missedFrags = result.MissedGenFragments;
        var receivedFrags = result.ReceivedFragments;
        // Missing frags after the last update doesnt really matter
        var totalFrags = result.MissedGenFragments + receivedFrags;
        var maxFrags = fuotaConfig.GenerationSize + fuotaConfig.GenerationSizeRedundancy;

        if (!success && totalFrags < maxFrags)
        {
            totalFrags = maxFrags;
        }
        // else
        // {
        //     // Hardware losses/bit-flips or mathematical failure (low prob.)
        // }

        var packetErrorRate = (float)missedFrags / totalFrags;
        _dataPoints.Add(new()
        {
            Rank = result.MatrixRank,
            Success = success,
            GenerationIndex = currentGenIndex,
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

    private async Task WriteDataUpdates()
    {
        if (_dataPoints.Count == 0) return;

        var filePath = GetCsvFilePath(GetCsvFileNameUpdates());
        using (var writer = new StreamWriter(filePath))
        {
            using (var csv = new CsvWriter(writer, _experimentPlotService.CsvConfig))
            {
                await csv.WriteRecordsAsync(_allDecodingUpdatesReceived);
            }
        }
    }

    private async Task WriteDataFilteredGenUpdates()
    {
        if (_dataPoints.Count == 0) return;

        var filePath = GetCsvFilePath(GetCsvFileNameFilteredGenUpdates());
        using (var writer = new StreamWriter(filePath))
        {
            using (var csv = new CsvWriter(writer, _experimentPlotService.CsvConfig))
            {
                await csv.WriteRecordsAsync(_filteredGenUpdates);
            }
        }
    }
    private async Task WriteData()
    {
        if (_dataPoints.Count == 0) return;

        var filePath = GetCsvFilePath(GetCsvFileName());
        using (var writer = new StreamWriter(filePath))
        {
            using (var csv = new CsvWriter(writer, _experimentPlotService.CsvConfig))
            {
                await csv.WriteRecordsAsync(_dataPoints);
            }
        }
    }
}