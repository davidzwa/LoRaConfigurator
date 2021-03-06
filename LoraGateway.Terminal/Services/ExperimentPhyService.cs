using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using LoRa;
using LoraGateway.Handlers;
using LoraGateway.Models;
using LoraGateway.Services.Contracts;
using LoraGateway.Services.Extensions;

namespace LoraGateway.Services;

public class ExperimentPhyService : JsonDataStore<ExperimentPhyConfig>
{
    private readonly ILogger<ExperimentPhyService> _logger;
    private readonly SerialProcessorService _serialProcessorService;
    private readonly SelectedDeviceService _selectedDeviceService;

    public CsvConfiguration CsvConfig = new(CultureInfo.InvariantCulture)
    {
        NewLine = Environment.NewLine,
    };

    public ExperimentPhyConfig.PhyConfig CurrentConfig { get; set; } = new();

    public string GetCsvFilePath(string fileName)
    {
        var dataFolderAbsolute = GetDataFolderFullPath();
        return Path.Join(dataFolderAbsolute, fileName);
    }

    private readonly List<ExperimentPhyDataEntry> _dataPoints = new();

    private string _currentTimeStamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

    private string GetCurrentTimeStamp()
    {
        return DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
    }

    public ExperimentPhyService(
        ILogger<ExperimentPhyService> logger,
        SerialProcessorService processorService,
        SelectedDeviceService selectedDeviceService
    )
    {
        _logger = logger;
        _serialProcessorService = processorService;
        _selectedDeviceService = selectedDeviceService;
    }

    public override string GetJsonFileName()
    {
        return "experiment_phy_config.json";
    }

    public string GetCsvFileName()
    {
        return $"./0_phy/experiment_phy_{_currentTimeStamp}.csv";
    }

    public override ExperimentPhyConfig GetDefaultJson()
    {
        var jsonStore = new ExperimentPhyConfig();
        if (jsonStore.WriteDataCounterDivisor == 0)
        {
            jsonStore.WriteDataCounterDivisor = 20;
        }

        return jsonStore;
    }

    public void ReceiveAck(string portName, LoRaMessage message)
    {
        // Not yet built - wait for later specification
        // AcksReceived.Add();
    }

    public async Task ReceivePeriodTxMessage(PeriodTxEvent transmitEvent)
    {
        // Silenced for now - only required for full clarity on experiment PER/PRR
        // _logger.LogInformation("PeriodTx event triggered");
    }

    public async Task ReceiveMessage(RxEvent receiveEvent)
    {
        if (Store == null)
        {
            _logger.LogWarning("Loaded store on RX - are you sure we have started properly?");
            await LoadStore();
        }

        var measurement = receiveEvent.Message.LoraMeasurement;
        _logger.LogInformation("[{Nr}] - RX event - RSSI {RSSI} SNR {SNR}",
            measurement.SequenceNumber,
            measurement.Rssi,
            measurement.Snr
        );

        _dataPoints.Add(new()
        {
            Timestamp = DateTime.Now.ToFileTimeUtc(),
            Rssi = measurement.Rssi,
            Snr = measurement.Snr,
            SequenceNumber = measurement.SequenceNumber,
            Bandwidth = CurrentConfig.TxBandwidth,
            TxPower = CurrentConfig.TxPower,
            SpreadingFactor = CurrentConfig.TxDataRate
        });

        var store = GetStore();
        if (_dataPoints.Count() % 100 == 0)
        {
            _logger.LogInformation("Updating CSV with {points} data points", _dataPoints.Count);
            await WriteData();
        }
    }

    public async Task RunPhyExperiments(bool runAsTransmitter)
    {
        var config = await LoadStore();
        if (runAsTransmitter)
        {
            _logger.LogInformation("Starting in TRANSMITTER MODE");
        }
        else
        {
            _logger.LogInformation("Starting in RECEIVER MODE");
        }

        _currentTimeStamp = GetCurrentTimeStamp();
        _dataPoints.Clear();
        CurrentConfig = ExperimentPhyConfig.PhyConfig.Default;

        var powers = config.TxPSeries;
        var sfs = config.TxSfSeries;
        var sfsSlow = config.TxSfSeriesSlow;

        _logger.LogInformation("AWAITING KEYPRESS to INIT sfs:{sfs} powers:{powers}", sfs, powers);
        var input = Console.ReadLine();
        if (ShouldStop(input))
        {
            _logger.LogInformation("Stopping experiment");
            return;
        }

        await Task.Delay(5000);
        _logger.LogInformation("Starting");

        foreach (var sf in sfs)
        {
            foreach (var txPower in powers)
            {
                await RunSpecificExperiment(
                    txPower,
                    sf,
                    config.SeqCount,
                    config.SeqPeriodMs,
                    runAsTransmitter,
                    config.DeviceIsRemote,
                    config.DeviceTargetNickName
                );
            }
        }

        foreach (var sfSlow in sfsSlow)
        {
            foreach (var txPower in powers)
            {
                await RunSpecificExperiment(
                    txPower,
                    sfSlow,
                    config.SeqCountSlow,
                    config.SeqPeriodMsSlow,
                    runAsTransmitter,
                    config.DeviceIsRemote,
                    config.DeviceTargetNickName
                );
            }
        }

        _logger.LogInformation("PHY experiment done");
    }

    public async Task RunSpecificExperiment(
        int txPower,
        uint sf,
        uint seqCount,
        uint seqPeriodMs,
        bool runAsTransmitter,
        bool deviceIsRemote,
        string targetName
        )
    {
        // CurrentConfig.TxBandwidth = bandwidth;
        CurrentConfig.TxPower = txPower;
        CurrentConfig.TxDataRate = sf;
        var periodMs = seqCount * seqPeriodMs;
        _logger.LogInformation("NEW RadioConfig T{Time}ms (t{TimeEach}ms) P{BW}dBm SF{SF}",
            periodMs,
            seqPeriodMs,
            txPower,
            sf);

        var devConf = GetDevConf(true, true);
        devConf.EnableSequenceTransmit = false;

        // Patch the command with the given overrides
        devConf.SequenceConfiguration.AlwaysSendPeriod = seqPeriodMs;
        devConf.SequenceConfiguration.LimitedSendCount = seqCount;

        // Send the start command 
        if (runAsTransmitter)
        {
            // Transmitter must configure start
            devConf.EnableSequenceTransmit = true;
            SendConfig(devConf, deviceIsRemote, targetName);
        }
        else
        {
            // Receiver must just wait
            devConf.EnableSequenceTransmit = false;
            SendConfig(devConf, deviceIsRemote, targetName);
        }

        // Add wait time
        var waitTimeMs = (int)periodMs + 3000;
        _logger.LogInformation("AWAITING TIME to END ROUND ({Time})", waitTimeMs);
        await Task.Delay(waitTimeMs);

        await WriteData();
    }

    public bool ShouldStop(string? input)
    {
        return (input != null && input.ToLowerInvariant().Contains("stop"));
    }

    public void SendConfig(DeviceConfiguration deviceConfiguration, bool usingLoRa, string targetName)
    {
        _logger.LogInformation("RadioConf START {Start}", deviceConfiguration.EnableSequenceTransmit);
        if (usingLoRa)
        {
            SendRadioConfigUnicastLora(targetName, deviceConfiguration);
        }
        else
        {
            SendRadioConfigUart(deviceConfiguration);
        }
    }

    private void SendRadioConfigUart(DeviceConfiguration deviceConfiguration)
    {
        _serialProcessorService.SendDeviceConfiguration(deviceConfiguration, false);
    }

    private void SendRadioConfigUnicastLora(string device, DeviceConfiguration deviceConfiguration)
    {
        var loraMessage = new LoRaMessage
        {
            DeviceConfiguration = deviceConfiguration
        };
        _serialProcessorService.SetDeviceFilter(device);
        _serialProcessorService.SendUnicastTransmitCommand(loraMessage, false);
    }

    private DeviceConfiguration GetDevConf(bool rx, bool tx)
    {
        var devConf = new DeviceConfiguration
        {
            TransmitConfiguration = new TransmitReceiveConfiguration
            {
                TxPower = CurrentConfig.TxPower,
                TxRxBandwidth = CurrentConfig.TxBandwidth,
                TxRxDataRate = CurrentConfig.TxDataRate,
                SetTx = tx,
                SetRx = rx
            },
            SequenceConfiguration = new SequenceConfiguration
            {
                AlwaysSendPeriod = Store.SeqPeriodMs,
                LimitedSendCount = Store.SeqCount,
                Delay = Store.TransmitStartDelay,
                EnableAlwaysSend = false
            },
            ApplyTransmitConfig = true,
            EnableSequenceTransmit = false,
            AckRequired = true,
        };

        return devConf;
    }

    private async Task WriteData()
    {
        if (_dataPoints.Count == 0) return;

        var filePath = GetCsvFilePath(GetCsvFileName());
        using (var writer = new StreamWriter(filePath))
        {
            using (var csv = new CsvWriter(writer, CsvConfig))
            {
                await csv.WriteRecordsAsync(_dataPoints);
            }
        }
    }
}