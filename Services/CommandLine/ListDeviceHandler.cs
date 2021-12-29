﻿using System.CommandLine;
using System.CommandLine.Invocation;

namespace LoraGateway.Services.CommandLine;

public class ListDeviceCommandHandler
{
    private readonly DeviceDataStore _deviceStore;
    private readonly SelectedDeviceService _selectedDeviceService;
    private readonly ILogger _logger;
    private readonly SerialProcessorService _serialProcessorService;

    public ListDeviceCommandHandler(
        ILogger<ListDeviceCommandHandler> logger,
        DeviceDataStore deviceStore,
        SelectedDeviceService selectedDeviceService,
        SerialProcessorService serialProcessorService
    )
    {
        _logger = logger;
        _deviceStore = deviceStore;
        _selectedDeviceService = selectedDeviceService;
        _serialProcessorService = serialProcessorService;
    }

    public Command GetHandler()
    {
        var commandHandler = new Command("list");
        commandHandler.AddAlias("l");

        commandHandler.Handler = CommandHandler.Create(() =>
        {
            var ports = _serialProcessorService.SerialPorts;
            foreach (var port in ports)
            {
                var device = _deviceStore.GetDeviceByPort(port.PortName);
                if (device == null)
                    _logger.LogInformation("Untracked device on port {port}", port.PortName);
                else
                {
                    var isSelected = port.PortName == _selectedDeviceService.SelectedPortName;
                    _logger.LogInformation("{IsSelectedMarker} Device {device} on port {port}", isSelected ? "SELECTED >" : "", device.NickName, port.PortName);
                }
                    
            }
        });

        return commandHandler;
    }
}