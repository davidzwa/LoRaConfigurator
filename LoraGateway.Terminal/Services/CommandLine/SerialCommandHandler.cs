using System.CommandLine;
using System.CommandLine.Invocation;
using JKang.EventBus;
using LoraGateway.Handlers;
using LoraGateway.Services.Extensions;

namespace LoraGateway.Services.CommandLine;

public class SerialCommandHandler
{
    private readonly ILogger _logger;
    private readonly MeasurementsService _measurementsService;
    private readonly IEventPublisher _eventPublisher;
    private readonly FuotaManagerService _fuotaManagerService;
    private readonly SelectedDeviceService _selectedDeviceService;
    private readonly SerialProcessorService _serialProcessorService;

    public SerialCommandHandler(
        ILogger<SerialCommandHandler> logger,
        SelectedDeviceService selectedDeviceService,
        MeasurementsService measurementsService,
        IEventPublisher eventPublisher,
        FuotaManagerService fuotaManagerService,
        SerialProcessorService serialProcessorService
    )
    {
        _logger = logger;
        _selectedDeviceService = selectedDeviceService;
        _measurementsService = measurementsService;
        _eventPublisher = eventPublisher;
        _fuotaManagerService = fuotaManagerService;
        _serialProcessorService = serialProcessorService;
    }

    public RootCommand ApplyCommands(RootCommand rootCommand)
    {
        rootCommand.Add(GetPeriodicSendCommand());
        rootCommand.Add(GetBootCommand());
        rootCommand.Add(GetUnicastSendCommand());
        rootCommand.Add(GetDeviceConfigurationCommand());
        rootCommand.Add(GetClearMeasurementsCommand());
        rootCommand.Add(GetRlncCommand());

        // Fluent structure
        return rootCommand;
    }

    public Command GetRlncCommand()
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

    public Command GetClearMeasurementsCommand()
    {
        var command = new Command("clear-measurements");
        command.AddAlias("clc");
        command.Handler = CommandHandler.Create(
            () =>
            {
                var selectedPortName = _selectedDeviceService.SelectedPortName;
                _logger.LogInformation("Clearing device measurements {Port}", selectedPortName);
                _serialProcessorService.SendClearMeasurementsCommands();
            });
        return command;
    }

    public Command GetDeviceConfigurationCommand()
    {
        var command = new Command("device");
        command.AddAlias("d");
        command.AddArgument(new Argument<bool>("enableAlwaysSend"));
        command.AddArgument(new Argument<uint>("alwaysSendPeriod"));
        command.Handler = CommandHandler.Create(
            (bool enableAlwaysSend, uint alwaysSendPeriod) =>
            {
                var selectedPortName = _selectedDeviceService.SelectedPortName;
                _logger.LogInformation("Device config {Port}", selectedPortName);
                _serialProcessorService.SendDeviceConfiguration(enableAlwaysSend, alwaysSendPeriod);
            });
        return command;
    }

    public Command GetUnicastSendCommand()
    {
        var command = new Command("unicast");
        command.AddAlias("u");
        command.Handler = CommandHandler.Create(
            () =>
            {
                var selectedPortName = _selectedDeviceService.SelectedPortName;
                _logger.LogInformation("Unicast command {Port}", selectedPortName);
                _serialProcessorService.SendUnicastTransmitCommand(new byte[]
                {
                    0xFF, 0xFE, 0xFD
                });
            });

        return command;
    }

    public Command GetPeriodicSendCommand()
    {
        var command = new Command("period");
        command.AddAlias("p");
        command.AddArgument(new Argument<uint>("period"));
        command.AddArgument(new Argument<uint>("count"));
        command.AddArgument(new Argument<int>("x"));
        command.AddArgument(new Argument<int>("y"));
        command.Handler = CommandHandler.Create(
            (uint period, uint count, int x, int y) =>
            {
                _measurementsService.SetLocation(x, y);
                var selectedPortName = _selectedDeviceService.SelectedPortName;
                _logger.LogInformation("Periodic command {Port}", selectedPortName);
                _serialProcessorService.SendPeriodicTransmitCommand(period, false, count, new byte[]
                {
                    0xFF, 0xFE, 0xFD
                });
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
            _serialProcessorService.SendBootCommand();
        });

        return command;
    }
}