using Google.Protobuf;
using LoRa;
using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Services.Extensions;

public static class SerialProcessingExtensions
{
    public static void SendUnicastTransmitCommand(
        this SerialProcessorService processorService,
        byte[] payload
    )
    {
        var command = new UartCommand
        {
            TransmitCommand =
                new LoRaMessage
                {
                    IsMulticast = false,
                    Payload = ByteString.CopyFrom(payload)
                }
        };

        processorService.WriteMessage(command);
    }

    public static void SendPeriodicTransmitCommand(
        this SerialProcessorService processorService,
        uint period,
        bool infinite,
        uint repetitions,
        byte[] payload
    )
    {
        var command = new UartCommand
        {
            TransmitCommand =
                new LoRaMessage
                {
                    IsMulticast = false,
                    SequenceConfig = new ForwardSequenceConfig
                    {
                        Period = period,
                        Indefinite = infinite,
                        SequenceCountLimit = repetitions
                    },
                    Payload = ByteString.CopyFrom(payload)
                }
        };

        processorService.WriteMessage(command);
    }

    public static void SendDeviceConfiguration(
        this SerialProcessorService processorService,
        bool enableAlwaysSend,
        uint alwaysSendPeriod
    )
    {
        var command = new UartCommand
        {
            DeviceConfiguration =
                new DeviceConfiguration
                {
                    AlwaysSendPeriod = alwaysSendPeriod,
                    EnableAlwaysSend = enableAlwaysSend
                }
        };

        processorService.WriteMessage(command);
    }

    public static void SendBootCommand(
        this SerialProcessorService processorService)
    {
        var command = new UartCommand
        {
            RequestBootInfo = new RequestBootInfo {Request = true}
        };
        processorService.WriteMessage(command);
    }

    public static void SendClearMeasurementsCommands(
        this SerialProcessorService processorService)
    {
        var command = new UartCommand
        {
            ClearMeasurementsCommand = new ClearMeasurementsCommand {SendBootAfter = true}
        };
        processorService.WriteMessage(command);
    }
}