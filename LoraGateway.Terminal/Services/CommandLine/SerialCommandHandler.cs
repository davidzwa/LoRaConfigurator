using System.CommandLine;
using System.CommandLine.Invocation;
using Google.Protobuf;
using JKang.EventBus;
using LoRa;
using LoraGateway.Handlers;
using LoraGateway.Services.Extensions;

namespace LoraGateway.Services.CommandLine;

public class SerialCommandHandler
{
    private readonly ILogger _logger;
    private readonly MeasurementsService _measurementsService;
    private readonly FuotaManagerService _fuotaManagerService;
    private readonly DeviceDataStore _deviceDataStore;
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
        rootCommand.Add(GetBootCommand());
        rootCommand.Add(GetUnicastSendCommand());
        rootCommand.Add(GetDeviceConfigurationCommand());
        rootCommand.Add(GetClearMeasurementsCommand());
        rootCommand.Add(GetRlncCommand());
        rootCommand.Add(GetRlncStoreReloadCommand());

        // Fluent structure
        return rootCommand;
    }

    bool GetDoNotProxyConfig()
    {
        var store = _fuotaManagerService.GetStore();
        if (store == null) return false;

        return store.UartFakeLoRaRxMode;
    }

    public Command GetRlncCommand()
    {
        var command = new Command("rlnc-init");
        command.AddAlias("rlnc");
        command.Handler = CommandHandler.Create(
            async () => { await _fuotaManagerService.HandleRlncConsoleCommand(); });
        return command;
    }

    public Command GetRlncStoreReloadCommand()
    {
        var command = new Command("rlnc-load");
        command.AddAlias("rl");
        command.Handler = CommandHandler.Create(
            async () => { await _fuotaManagerService.ReloadStore(); });
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
                if (loc.Length != 0)
                {
                    _measurementsService.SetLocationText(loc);
                }

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
        command.AddOption(new Option<string>("--d"));
        command.AddOption(new Option("--clc"));
        command.Handler = CommandHandler.Create(
            (string d, bool clc) =>
            {
                var forwardExperimentCommand =
                    clc
                        ? ForwardExperimentCommand.Types.SlaveCommand.ClearFlash
                        : ForwardExperimentCommand.Types.SlaveCommand.QueryFlash;
                var isMulticast = String.IsNullOrEmpty(d);
                var loraMessage = new LoRaMessage
                {
                    IsMulticast = isMulticast,
                    ForwardExperimentCommand = new ForwardExperimentCommand()
                    {
                        SlaveCommand = forwardExperimentCommand
                    }
                };
                
                if (!isMulticast)
                {
                    var device = _deviceDataStore.GetDeviceByNick(d);
                    loraMessage.DeviceId = device.Id;
                }
                
                var selectedPortName = _selectedDeviceService.SelectedPortName;
                _logger.LogInformation("Unicast command {Port} MC:{MC} ClearFlash:{Clc}", selectedPortName, isMulticast, clc);
                _serialProcessorService.SendUnicastTransmitCommand(loraMessage, GetDoNotProxyConfig());
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