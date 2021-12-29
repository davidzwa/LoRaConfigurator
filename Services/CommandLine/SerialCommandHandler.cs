using System.CommandLine;
using System.CommandLine.Invocation;
using LoraGateway.Services.Extensions;

namespace LoraGateway.Services.CommandLine;

public class SerialCommandHandler
{
    private readonly ILogger _logger;
    private readonly SelectedDeviceService _selectedDeviceService;
    private readonly MeasurementsService _measurementsService;
    private readonly SerialProcessorService _serialProcessorService;

    public SerialCommandHandler(
        ILogger<SerialCommandHandler> logger,
        SelectedDeviceService selectedDeviceService,
        MeasurementsService measurementsService,
        SerialProcessorService serialProcessorService
    )
    {
        _logger = logger;
        _selectedDeviceService = selectedDeviceService;
        _measurementsService = measurementsService;
        _serialProcessorService = serialProcessorService;
    }

    public RootCommand ApplyCommands(RootCommand rootCommand)
    {
        rootCommand.Add(GetPeriodicSendCommand());
        rootCommand.Add(GetBootCommand());
        rootCommand.Add(GetUnicastSendCommand());
        rootCommand.Add(DeviceConfigurationCommand());

        return rootCommand;
    }

    public Command DeviceConfigurationCommand()
    {
        var command = new Command("device");
        command.AddAlias("d");
        command.AddArgument(new Argument<bool>("enableAlwaysSend"));
        command.AddArgument(new Argument<uint>("alwaysSendPeriod"));
        command.Handler = CommandHandler.Create(
            (bool enableAlwaysSend, uint alwaysSendPeriod) =>
            {
                var selectedPortName = _selectedDeviceService.SelectedPortName;
                _logger.LogInformation("Unicast command {port}", selectedPortName);
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
                _logger.LogInformation("Unicast command {port}", selectedPortName);
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
                _logger.LogInformation("Periodic command {port}", selectedPortName);
                _serialProcessorService.SendPeriodicTransmitCommand(period, count, new byte[]
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
            _logger.LogInformation("Boot sent {port}", selectedPortName);
            _serialProcessorService.SendBootCommand();
        });

        return command;
    }
}