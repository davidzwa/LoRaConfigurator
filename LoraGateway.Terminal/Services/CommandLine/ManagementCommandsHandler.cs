using System.CommandLine;
using System.CommandLine.Invocation;

namespace LoraGateway.Services.CommandLine;

public class ListDeviceCommandHandler
{
    private readonly DeviceDataStore _deviceStore;
    private readonly ExperimentPlotService _experimentPlotService;
    private readonly ILogger _logger;
    private readonly SelectedDeviceService _selectedDeviceService;
    private readonly SerialProcessorService _serialProcessorService;

    public ListDeviceCommandHandler(
        ILogger<ListDeviceCommandHandler> logger,
        DeviceDataStore deviceStore,
        ExperimentPlotService experimentPlotService,
        SelectedDeviceService selectedDeviceService,
        SerialProcessorService serialProcessorService
    )
    {
        _logger = logger;
        _deviceStore = deviceStore;
        _experimentPlotService = experimentPlotService;
        _selectedDeviceService = selectedDeviceService;
        _serialProcessorService = serialProcessorService;
    }

    public RootCommand ApplyCommands(RootCommand rootCommand)
    {
        rootCommand.Add(GetListDeviceCommand());
        rootCommand.Add(CsvToPngProcessCommand());
        rootCommand.Add(CsvToMultiPerPngProcessCommand());
        return rootCommand;
    }

    public Command CsvToPngProcessCommand()
    {
        var cmd = new Command("csv-png");
        cmd.AddAlias("png");
        cmd.AddOption(new Option<string>("--path"));

        cmd.Handler = CommandHandler.Create((string filePath) =>
        {
            _experimentPlotService.SavePlotsFromCsv(filePath);
        });

        return cmd;
    }
    
    public Command CsvToMultiPerPngProcessCommand()
    {
        var cmd = new Command("csv-per-png");
        cmd.AddAlias("per-png");
        cmd.AddOption(new Option<string>("--path"));

        cmd.Handler = CommandHandler.Create((string filePath) =>
        {
            _experimentPlotService.SaveSuccessRatePlotsFromCsv(filePath);
        });

        return cmd;
    }
    
    public Command GetListDeviceCommand()
    {
        var commandHandler = new Command("list");
        commandHandler.AddAlias("l");

        commandHandler.Handler = CommandHandler.Create(() =>
        {
            var ports = _serialProcessorService.SerialPorts;
            foreach (var port in ports)
            {
                var device = _deviceStore.GetDeviceByPort(port.PortName).ToList();
                if (device.Count == 0)
                {
                    _logger.LogInformation("Untracked device on port {port}", port.PortName);
                }
                else
                {
                    // var isSelected = port.PortName == _selectedDeviceService.SelectedPortName;
                    _logger.LogInformation("Device on port {port}", port.PortName);
                }
            }
        });

        return commandHandler;
    }
}