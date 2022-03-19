using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using LoRa;
using LoraGateway.Services.Extensions;

namespace LoraGateway.Services.CommandLine;

public class SerialCommandHandler
{
    private readonly DeviceDataStore _deviceDataStore;
    private readonly FuotaManagerService _fuotaManagerService;
    private readonly ILogger _logger;
    private readonly MeasurementsService _measurementsService;
    private readonly SelectedDeviceService _selectedDeviceService;
    private readonly SerialProcessorService _serialProcessorService;

    public SerialCommandHandler(
        ILogger<SerialCommandHandler> logger,
        SelectedDeviceService selectedDeviceService,
        MeasurementsService measurementsService,
        FuotaManagerService fuotaManagerService,
        DeviceDataStore deviceDataStore,
        SerialProcessorService serialProcessorService
    )
    {
        _logger = logger;
        _selectedDeviceService = selectedDeviceService;
        _measurementsService = measurementsService;
        _fuotaManagerService = fuotaManagerService;
        _deviceDataStore = deviceDataStore;
        _serialProcessorService = serialProcessorService;
    }

    public RootCommand ApplyCommands(RootCommand rootCommand)
    {
        rootCommand.AddGlobalOption(new Option<string>("--d"));
        rootCommand.Add(GetBootCommand());
        rootCommand.Add(GetUnicastSendCommand());
        rootCommand.Add(GetDeviceConfigurationCommand());
        rootCommand.Add(GetClearMeasurementsCommand());
        rootCommand.Add(GetRlncInitCommand());
        rootCommand.Add(GetRlncStoreReloadCommand());
        rootCommand.Add(SetTxPowerCommand());

        // Fluent structure
        return rootCommand;
    }

    private bool GetDoNotProxyConfig()
    {
        var store = _fuotaManagerService.GetStore();
        if (store == null) return false;

        return store.UartFakeLoRaRxMode;
    }

    public Command GetRlncInitCommand()
    {
        var command = new Command("rlnc-init");
        command.AddAlias("rlnc");
        command.Handler = CommandHandler.Create(
            async () =>
            {
                await _fuotaManagerService.HandleRlncConsoleCommand();
            });
        return command;
    }

    public Command GetRlncStoreReloadCommand()
    {
        var command = new Command("rlnc-load");
        command.AddAlias("rl");
        command.Handler = CommandHandler.Create(
            async () =>
            {
                await _fuotaManagerService.ReloadStore();
            });
        return command;
    }

    public Command GetClearMeasurementsCommand()
    {
        var command = new Command("clear-measurements");
        command.AddAlias("clc");
        command.Handler = CommandHandler.Create(
            () =>
            {
                var selectedPortName = _selectedDeviceService.SelectedPortName;
                _logger.LogInformation("Clearing device measurements {Port}", selectedPortName);
                _serialProcessorService.SendClearMeasurementsCommands(GetDoNotProxyConfig());
            });
        return command;
    }

    public Command GetDeviceConfigurationCommand()
    {
        var command = new Command("device");
        command.AddAlias("d");
        command.AddArgument(new Argument<bool>("enableAlwaysSend"));
        command.AddOption(new Option<uint>("t", () => 1000));
        command.AddOption(new Option<uint>("n", () => 0)); // if 0 disable it 
        command.AddOption(new Option<string>("--loc", () => ""));
        command.Handler = CommandHandler.Create(
            (bool enableAlwaysSend, uint t, uint n, string loc) =>
            {
                if (loc.Length != 0) _measurementsService.SetLocationText(loc);

                var selectedPortName = _selectedDeviceService.SelectedPortName;
                _logger.LogInformation("Device config {Port}", selectedPortName);
                _serialProcessorService.SendDeviceConfiguration(enableAlwaysSend, t, n, GetDoNotProxyConfig());
            });
        return command;
    }

    public Command GetUnicastSendCommand()
    {
        var command = new Command("unicast");
        command.AddAlias("u");
        command.AddOption(new Option("--clc"));
        command.AddOption(new Option("--q"));
        command.AddOption(new Option("--conf"));
        command.Handler = CommandHandler.Create(
            async (string d, bool clc, bool q, bool conf) =>
            {
                var loraMessage = new LoRaMessage();

                // We will be transmitted a device conf
                if (conf || q)
                {
                    await _fuotaManagerService.ReloadStore();

                    var store = _fuotaManagerService.GetStore();
                    loraMessage.DeviceConfiguration = new DeviceConfiguration();

                    var devConf = loraMessage.DeviceConfiguration;
                    devConf.TxBandwidth = store!.TxBandwidth;
                    devConf.TxPower = store.TxPower;
                    devConf.TxDataRate = store.TxDataRate;
                    if (q)
                    {
                        _logger.LogInformation("Stopping all transmitters");
                        loraMessage.IsMulticast = true;
                        devConf.AlwaysSendPeriod = 0;
                        devConf.EnableAlwaysSend = false;
                    }
                    else
                    {
                        devConf.EnableAlwaysSend = false;
                        devConf.AlwaysSendPeriod = store.SeqPeriodMs;
                        devConf.LimitedSendCount = store.SeqCount;
                    }
                }
                else
                {
                    var forwardExperimentCommand =
                        clc
                            ? ForwardExperimentCommand.Types.SlaveCommand.ClearFlash
                            : ForwardExperimentCommand.Types.SlaveCommand.QueryFlash;

                    loraMessage.ForwardExperimentCommand = new ForwardExperimentCommand
                    {
                        SlaveCommand = forwardExperimentCommand
                    };
                }

                // Store multicast context
                _serialProcessorService.SetDeviceFilter(d);
                
                var isMulticast = _serialProcessorService.IsDeviceFilterMulticast();
                var selectedPortName = _selectedDeviceService.SelectedPortName;
                _logger.LogInformation("Unicast command {Port} MC:{MC} ClearFlash:{Clc}", selectedPortName, isMulticast,
                    clc);
                
                _serialProcessorService.SendUnicastTransmitCommand(loraMessage, GetDoNotProxyConfig());
            });

        return command;
    }

    public Command SetTxPowerCommand()
    {
        var command = new Command("tx-power");
        command.AddAlias("tx");
        command.AddArgument(new Argument<int>("power"));
        // command.AddOption(new Option<uint>("sf"));
        command.Handler = CommandHandler.Create((int power) =>
        {
            var selectedPortName = _selectedDeviceService.SelectedPortName;
            _logger.LogInformation("Set TX power {Power} sent {Port}", selectedPortName, power);
            _serialProcessorService.SendTxPowerCommand(power, GetDoNotProxyConfig());
        });

        return command;
    }

    public Command GetBootCommand()
    {
        var command = new Command("boot");
        command.AddAlias("b");
        command.Handler = CommandHandler.Create(() =>
        {
            var selectedPortName = _selectedDeviceService.SelectedPortName;
            _logger.LogInformation("Boot sent {Port}", selectedPortName);
            _serialProcessorService.SendBootCommand(GetDoNotProxyConfig());
        });

        return command;
    }
}