using Google.Protobuf;
using LoRa;
using LoraGateway.Models;
using LoraGateway.Services.Firmware.RandomLinearCoding;
using LoraGateway.Utils;

namespace LoraGateway.Services;

public partial class SerialProcessorService
{
    private Device? _deviceFilter = null;

    public bool IsDeviceFilterMulticast()
    {
        return _deviceFilter == null;
    }
    
    public void SetDeviceFilter(string nickName)
    {
        // Clear the filter if multicast   
        if (String.IsNullOrEmpty(nickName))
        {
            ClearDeviceFilter();
            _logger.LogDebug("Set device unicast filter to BROADCAST mode");
            return;
        }
        
        var device = _deviceStore!.GetDeviceByNick(nickName);
        if (device == null)
        {
            _logger.LogWarning("Device by nickname '{NickName}' not found in store, could not set unicast device filter", nickName);
        }

        _deviceFilter = device;
        _logger.LogDebug("Set device unicast filter to {DeviceId}", _deviceFilter!.Id);
    }

    public void ClearDeviceFilter()
    {
        _deviceFilter = null;
    }

    public void WriteMessage(UartCommand message, string? portName = null)
    {
        // Make sure the device filter is not reused
        ClearDeviceFilter();
        
        var selectedPortName = _selectedDeviceService.SelectedPortName;
        if (portName != null)
        {
            selectedPortName = portName;
        }

        if (selectedPortName == null)
        {
            throw new InvalidOperationException("Selected port was not set - check USB connection");
        }

        // Get inner payload, prepend length and crc
        var payload = message.ToByteArray();
        var crc8Checksum = Crc8.ComputeChecksum(payload);
        var protoMessageBuffer = new[] {crc8Checksum, (byte) payload.Length}.Concat(payload);

        // Encode packet
        var messageBuffer = Cobs.Encode(protoMessageBuffer).ToArray();

        var transmitBuffer = new[] {StartByte}
            .Concat(new[] {(byte) messageBuffer.Length})
            .Concat(messageBuffer)
            .Concat(new[] {EndByte})
            .ToArray();

        _logger.LogDebug("[{Port}] \n\tTRANSMIT {Message} \n\tPROTO    {Payload}", selectedPortName,
            SerialUtil.ByteArrayToString(transmitBuffer), SerialUtil.ByteArrayToString(payload));
        var port = GetPort(selectedPortName);
        if (port == null)
        {
            _logger.LogWarning("[{Port}] Port was null. Cant send", selectedPortName);
            return;
        }

        port.Write(transmitBuffer, 0, transmitBuffer.Length);
    }
    
    LoRaMessage TransformMessageCastType(LoRaMessage loraMessage)
    {
        // Unicast message
        if (_deviceFilter != null)
        {
            loraMessage.DeviceId = _deviceFilter.Id;
            loraMessage.IsMulticast = false;
        }
        else
        {
            loraMessage.IsMulticast = true;
        }

        return loraMessage;
    }
    
    public void SendBootCommand(bool doNotProxy, string? portName = null)
    {
        var command = new UartCommand
        {
            DoNotProxyCommand = doNotProxy,
            RequestBootInfo = new RequestBootInfo {Request = true}
        };
        WriteMessage(command, portName);
    }
    
    public void SendUnicastTransmitCommand(
        LoRaMessage loRaMessage,
        bool doNotProxy
    )
    {
        if (doNotProxy)
        {
            _logger.LogWarning("DO NOT PROXY TURNED ON FOR UNICAST!");
        }
        var command = new UartCommand
        {
            DoNotProxyCommand = doNotProxy,
            TransmitCommand = TransformMessageCastType(loRaMessage)
        };

        WriteMessage(command);
    }
    
    public void SendRlncInitConfigCommand(FuotaSession fuotaSession)
    {
        var config = fuotaSession.Config;
        
        var command = new UartCommand
        {
            DoNotProxyCommand = config.UartFakeLoRaRxMode,
            TransmitCommand = TransformMessageCastType(new ()
            {
                CorrelationCode = 0,
                RlncInitConfigCommand = new RlncInitConfigCommand
                {
                    FieldPoly = GFSymbol.Polynomial,
                    FieldDegree = config.FieldDegree,
                    FrameCount = config.FakeFragmentCount,
                    FrameSize = config.FakeFragmentSize,
                    // Calculated value from config store
                    GenerationCount = fuotaSession.GenerationCount,
                    GenerationSize = config.GenerationSize,
                    // Wont send poly as its highly static
                    // LfsrPoly = ,
                    LfsrSeed = config.LfsrSeed
                }
            })
        };

        WriteMessage(command);
    }

    public void SendNextRlncFragment(FuotaSession fuotaSession, List<byte> payload)
    {
        var config = fuotaSession.Config;
        var byteString = ByteString.CopyFrom(payload.ToArray());

        var command = new UartCommand
        {
            DoNotProxyCommand = config.UartFakeLoRaRxMode,
            TransmitCommand = TransformMessageCastType(new LoRaMessage
            {
                CorrelationCode = 0,
                DeviceId = 0,
                IsMulticast = true,
                Payload = byteString,
                RlncEncodedFragment = new RlncEncodedFragment()
            })
        };

        WriteMessage(command);
    }

    public void SendRlncUpdate(FuotaSession fuotaSession)
    {
        var config = fuotaSession.Config;
        var command = new UartCommand
        {
            DoNotProxyCommand = config.UartFakeLoRaRxMode,
            TransmitCommand = TransformMessageCastType(new LoRaMessage
            {
                CorrelationCode = 0,
                RlncStateUpdate = new RlncStateUpdate()
                {
                    GenerationIndex = fuotaSession.CurrentGenerationIndex
                }
            })
        };

        WriteMessage(command);
    }
    
    public void SendRlncTermination(FuotaSession fuotaSession)
    {
        var config = fuotaSession.Config;
        var command = new UartCommand
        {
            DoNotProxyCommand = config.UartFakeLoRaRxMode,
            TransmitCommand = TransformMessageCastType(new LoRaMessage
            {
                CorrelationCode = 0,
                DeviceId = 0,
                IsMulticast = true,
                RlncTerminationCommand = new RlncTerminationCommand()
            })
        };

        WriteMessage(command);
    }
}