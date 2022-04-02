using System.Globalization;
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
    private bool _deviceRlncRoundTerminated;
    private uint? _lastGenerationIndexReceived;
    private float _currentPer = 0.0f;
    private CancellationTokenSource _cancellationTokenSource = new();

    private List<ExperimentDataEntry> _dataPoints = new();
    
    private CsvConfiguration csvConfig = new (CultureInfo.InvariantCulture)
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
        var currentGenIndex = result.CurrentGenerationIndex;
        var fuotaConfig = _fuotaManagerService.GetStore();
        var generationPacketsCount = fuotaConfig.GenerationSize + fuotaConfig.GenerationSizeRedundancy;
        
        if (_lastGenerationIndexReceived == result.CurrentGenerationIndex)
        {
            _logger.LogWarning("Skipping already processed generation");
            return;
        }
        
        // First generation(s) failed to succeed, administer those 
        if (_lastGenerationIndexReceived == null && currentGenIndex > 0)
        {
            if (currentGenIndex > 4)
            {
                throw new InvalidOperationException(
                    "Missed generations overflowed or was out of bounds - Experiment failure");
            }
            _logger.LogWarning("Missed generations {MissedGens} at start - PER {Per}", currentGenIndex, _currentPer);
            // F.e. we received index 2, so 1 and 0 were missed => 2x missed
            var missedGenerationsStart = currentGenIndex;
            for (uint i = 0; i < missedGenerationsStart; i++)
            {
                _dataPoints.Add(new ()
                {
                    Rank = 0,
                    Success = false,
                    GenerationIndex = i,
                    GenerationTotalPackets = fuotaConfig.GenerationSize,
                    GenerationRedundancyUsed = fuotaConfig.GenerationSizeRedundancy,
                    MissedPackets = generationPacketsCount,
                    ReceivedPackets = 0,
                    RngResolution = 0,
                    PacketErrorRate = 1.0f,
                    ConfiguredPacketErrorRate = _currentPer
                });
            }
        }
        
        // Now set it straight
        if (_lastGenerationIndexReceived == null)
        {
            _lastGenerationIndexReceived = currentGenIndex - 1;
        }

        // We had skipped generations - in the middle
        var missedGens = (int)(currentGenIndex - _lastGenerationIndexReceived!);
        if (_lastGenerationIndexReceived != null && missedGens > 1)
        {
            var missedGenerations = missedGens - 1;
            _logger.LogWarning("Missed {MissedGens} generations after start - PER {Per}", missedGenerations, _currentPer);

            if (missedGenerations > 4)
            {
                throw new InvalidOperationException(
                    "Missed generations overflowed or was out of bounds - Experiment failure");
            }
            // F.e. we received index 2 and last 0, so 1 and 0 were missed => 2x missed
            for (uint i = 0; i < missedGenerations; i++)
            {
                _dataPoints.Add(new ()
                {
                    Rank = 0,
                    Success = false,
                    GenerationIndex = i,
                    GenerationTotalPackets = fuotaConfig.GenerationSize,
                    GenerationRedundancyUsed = fuotaConfig.GenerationSizeRedundancy,
                    MissedPackets = generationPacketsCount,
                    ReceivedPackets = 0,
                    RngResolution = 0,
                    PacketErrorRate = 1.0f,
                    ConfiguredPacketErrorRate = _currentPer
                });
            }
        }

        var success = result.Success;
        var missedFrags = result.MissedGenFragments;
        var receivedFrags = result.ReceivedFragments;
        var totalFrags = result.MissedGenFragments + receivedFrags;
        var packetErrorRate = (float)missedFrags / totalFrags; 
        
        _dataPoints.Add(new ()
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

        _lastGenerationIndexReceived = result.CurrentGenerationIndex;
    }

    private async Task WriteData()
    {
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
        _lastGenerationIndexReceived = null;

        var per = min;
        while (per < max)
        {
            var result = _cancellationTokenSource.TryReset();

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
        var dataBuckets = _dataPoints.GroupBy(d => d.ConfiguredPacketErrorRate);
        var perBuckets  = dataBuckets.Select(b =>
        {
            return new
            {
                PerAvg = b.Average(d => d.PacketErrorRate),
                ConfiguredPer = b.Key
            };
        });
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