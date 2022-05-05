using LoRa;

namespace LoraGateway.Services.Extensions;

public static class SerialProcessingExtensions
{
    public static void SendDeviceConfiguration(
        this SerialProcessorService processorService,
        DeviceConfiguration deviceConfiguration,
        bool doNotProxy
    )
    {
        var command = new UartCommand
        {
            DoNotProxyCommand = doNotProxy,
            DeviceConfiguration = deviceConfiguration
        };

        processorService.WriteMessage(command);
    }

    public static void SendTxPowerCommand(
        this SerialProcessorService processorService, int power, bool doNotProxy)
    {
        var command = new UartCommand
        {
            DoNotProxyCommand = doNotProxy,
            TxConfig = new RadioTxConfig()
            {
                Power = power
            }
        };
        processorService.WriteMessage(command);
    }

    public static void SendClearMeasurementsCommands(
        this SerialProcessorService processorService, bool doNotProxy)
    {
        var command = new UartCommand
        {
            DoNotProxyCommand = doNotProxy,
            ClearMeasurementsCommand = new ClearMeasurementsCommand { SendBootAfter = true }
        };
        processorService.WriteMessage(command);
    }
}