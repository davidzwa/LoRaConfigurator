﻿using Google.Protobuf;
using LoRa;

namespace LoraGateway.Services.Extensions;

public static class SerialProcessingExtensions
{
    public static void SendUnicastTransmitCommand(
        this SerialProcessorService processorService,
        byte[] payload,
        bool doNotProxy
    )
    {
        var command = new UartCommand
        {
            DoNotProxyCommand = doNotProxy,
            TransmitCommand =
                new LoRaMessage
                {
                    DeviceId = 5832774, // TODO replace with something
                    IsMulticast = false,
                    Payload = ByteString.CopyFrom(payload)
                }
        };

        processorService.WriteMessage(command);
    }

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

    public static void SendBootCommand(
        this SerialProcessorService processorService, bool doNotProxy)
    {
        var command = new UartCommand
        {
            DoNotProxyCommand = doNotProxy,
            RequestBootInfo = new RequestBootInfo {Request = true}
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