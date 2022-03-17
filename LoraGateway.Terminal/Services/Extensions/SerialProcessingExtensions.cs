using LoRa;

namespace LoraGateway.Services.Extensions;

public static class SerialProcessingExtensions
{
    public static void SendDeviceConfiguration(
        this SerialProcessorService processorService,
        bool enableAlwaysSend,
        uint alwaysSendPeriod,
        uint limitedPacketCount,
        bool doNotProxy
    )
    {
        var command = new UartCommand
        {
            DoNotProxyCommand = doNotProxy,
            DeviceConfiguration =
                new DeviceConfiguration
                {
                    AlwaysSendPeriod = alwaysSendPeriod,
                    EnableAlwaysSend = enableAlwaysSend,
                    LimitedSendCount = limitedPacketCount
                }
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
            ClearMeasurementsCommand = new ClearMeasurementsCommand {SendBootAfter = true}
        };
        processorService.WriteMessage(command);
    }
}