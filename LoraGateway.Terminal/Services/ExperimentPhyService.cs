using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using LoRa;
using LoraGateway.Handlers;
using LoraGateway.Models;
using LoraGateway.Services.Contracts;

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
        return "experiment_phy.csv";
    }

    public override ExperimentPhyConfig GetDefaultJson()
    {
        var jsonStore = new ExperimentPhyConfig();
        return jsonStore;
    }

    public void ReceiveAck(string portName, LoRaMessage message)
    {
        // Not yet built - wait for later specification
    }

    public async Task ReceivePeriodTxMessage(PeriodTxEvent transmitEvent)
    {
        // Silenced for now - only required for full clarity on experiment PER/PRR
        // _logger.LogInformation("PeriodTx event triggered");
    }

    public async Task ReceiveMessage(RxEvent receiveEvent)
    {
        var measurement = receiveEvent.Message.LoraMeasurement;
        _logger.LogInformation("Rx event triggered RSSI {RSSI} SNR {SNR} SeqNr {Nr}",
            measurement.Rssi,
            measurement.Snr,
            measurement.SequenceNumber);

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
        if (_dataPoints.Count() % store.WriteDataCounterDivisor == 0)
        {
            _logger.LogInformation("Updating CSV with {points} data points", _dataPoints.Count);
            await WriteData();
        }
    }

    public async Task RunPhyExperiments()
    {
        var config = await LoadStore();
        _logger.LogInformation("Starting PHY experiment iterations");

        _dataPoints.Clear();
        CurrentConfig = ExperimentPhyConfig.PhyConfig.Default;

        // var bws = config.TxBwSeries;
        var powers = config.TxPSeries;
        var sfs = config.TxSfSeries;
        foreach (var txPower in powers)
        {
            foreach (var sf in sfs)
            {
                // CurrentConfig.TxBandwidth = bandwidth;
                CurrentConfig.TxPower = txPower;
                CurrentConfig.TxDataRate = sf;
                var totalDuration = config.SeqCount * config.SeqPeriodMs;
                _logger.LogInformation("Iteration P{BW} SF{SF} Duration{Time}", 
                    txPower,
                    sf,
                    totalDuration);
                await RunIteration();

                await Task.Delay((int)totalDuration + 200);
                await WriteData();
            }
        }

        _logger.LogInformation("PHY experiment done");
    }

    public async Task RunIteration()
    {
        var store = GetStore();

        // Store multicast context
        _serialProcessorService.SetDeviceFilter(store.TargetedReceiverNickname);

        var isMulticast = _serialProcessorService.IsDeviceFilterMulticast();
        var selectedPortName = _selectedDeviceService.SelectedPortName;

        var loraMessage = new LoRaMessage();
        loraMessage.DeviceConfiguration = new DeviceConfiguration();
        loraMessage.DeviceConfiguration.TransmitConfiguration = new TransmitConfiguration();

        var devConf = loraMessage.DeviceConfiguration;
        devConf.AlwaysSendPeriod = store.SeqPeriodMs;
        devConf.EnableAlwaysSend = false;
        devConf.LimitedSendCount = store.SeqCount;

        var txConf = devConf.TransmitConfiguration;
        txConf.TxBandwidth = CurrentConfig.TxBandwidth;
        txConf.TxPower = CurrentConfig.TxPower;
        txConf.TxDataRate = CurrentConfig.TxDataRate;

        _logger.LogInformation("Experiment command {Port} MC?{MC} BW{BW} P{P} SF{SF}",
            selectedPortName, isMulticast,
            txConf.TxBandwidth, txConf.TxBandwidth, txConf.TxDataRate);

        _serialProcessorService.SendUnicastTransmitCommand(loraMessage, false);
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