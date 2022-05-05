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
        if (_dataPoints.Count() % store.WriteDataCounterDivisor == 0)
        {
            _logger.LogInformation("Updating CSV with {points} data points", _dataPoints.Count);
            await WriteData();
        }
    }

    public async Task RunPhyExperiments()
    {
        var config = await LoadStore();
        if (config.ReceiverMode)
        {
            _logger.LogInformation("Starting in RECEIVER MODE");
        }
        else
        {
            _logger.LogInformation("Starting in TRANSMITTER MODE");
        }

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
                var periodMs = config.SeqCount * config.SeqPeriodMs;
                _logger.LogInformation("NEW RadioConfig T{Time}ms P{BW}dBm SF{SF}",
                    periodMs,
                    txPower,
                    sf);

                // Wait for keyboard confirmation to start
                _logger.LogInformation("AWAITING KEYPRESS to START");
                Console.ReadLine();

                var devConf = GetDevConf(true, true);
                devConf.EnableSequenceTransmit = false;

                // Send the start command 
                if (!config.ReceiverMode)
                {
                    // Transmitter must configure start
                    devConf.EnableSequenceTransmit = true;
                    SendConfig(devConf, config.DeviceIsRemote, config.DeviceTargetNickName);
                 
                    var sleepTimeMs = (int)periodMs + 500;
                    _logger.LogInformation("Sleeping for {time}ms (round duration)", sleepTimeMs);
                    await Task.Delay(sleepTimeMs);
                    
                    _logger.LogInformation("AWAITING KEYPRESS to END ROUND");
                    Console.ReadLine();
                }
                else
                {
                    // Receiver must just wait
                    var sleepTimeMs = (int)periodMs + 500;
                    _logger.LogInformation("Sleeping for {time}ms (wait)", sleepTimeMs);
                    await Task.Delay(sleepTimeMs);
                    
                    _logger.LogInformation("AWAITING KEYPRESS to END ROUND + SAVE DATA");
                    Console.ReadLine();
                }
                
                await WriteData();
            }
        }

        _logger.LogInformation("PHY experiment done");
    }

    public void SendConfig(DeviceConfiguration deviceConfiguration, bool usingLoRa, string targetName)
    {
        _logger.LogInformation("RadioConf START {Start}", deviceConfiguration.EnableSequenceTransmit);
        if (usingLoRa)
        {
            SendRadioConfigUnicastLora(targetName, deviceConfiguration);
        }
        else {
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