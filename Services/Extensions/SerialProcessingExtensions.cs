﻿using Google.Protobuf;

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
                new TransmitCommand
                {
                    IsMulticast = false,
                    Period = 0,
                    Payload = ByteString.CopyFrom(payload)
                }
        };

        processorService.WriteMessage(command);
    }
    
    public static void SendPeriodicTransmitCommand(
        this SerialProcessorService processorService,
        uint period,
        uint repetitions,
        byte[] payload
    )
    {
        var command = new UartCommand
        {
            TransmitCommand =
                new TransmitCommand
                {
                    IsMulticast = false,
                    Period = period,
                    MaxPacketCount = repetitions,
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
                new ()
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
            RequestBootInfo = new RequestBootInfo { Request = true }
        };
        processorService.WriteMessage(command);
    }

    public static void SendClearMeasurementsCommands(
        this SerialProcessorService processorService)
    {
        
        var command = new UartCommand
        {
            ClearMeasurementsCommand = new ClearMeasurementsCommand(){ SendBootAfter = true }
        };
        processorService.WriteMessage(command);
    } 
}