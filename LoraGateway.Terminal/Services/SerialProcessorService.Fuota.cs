using Google.Protobuf;
using LoRa;
using LoraGateway.Models;
using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Services;

public partial class SerialProcessorService
{
    public void SendRlncInitConfigCommand(FuotaSession fuotaSession)
    {
        var config = fuotaSession.Config;

        var command = new UartCommand
        {
            DoNotProxyCommand = config.UartFakeLoRaRxMode,
            TransmitCommand = new LoRaMessage
            {
                CorrelationCode = 0,
                DeviceId = 0,
                IsMulticast = true,
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
            }
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
            TransmitCommand = new LoRaMessage
            {
                CorrelationCode = 0,
                DeviceId = 0,
                IsMulticast = true,
                Payload = byteString,
                RlncEncodedFragment = new RlncEncodedFragment()
                {
                }
            }
        };

        WriteMessage(command);
    }

    public void SendRlncUpdate(FuotaSession fuotaSession)
    {
        var config = fuotaSession.Config;
        var command = new UartCommand
        {
            DoNotProxyCommand = config.UartFakeLoRaRxMode,
            TransmitCommand = new LoRaMessage
            {
                CorrelationCode = 0,
                DeviceId = 0,
                IsMulticast = true,
                RlncStateUpdate = new RlncStateUpdate()
                {
                    GenerationIndex = fuotaSession.CurrentGenerationIndex
                }
            }
        };

        WriteMessage(command);
    }
    
    public void SendRlncTermination(FuotaSession fuotaSession)
    {
        var config = fuotaSession.Config;
        var command = new UartCommand
        {
            DoNotProxyCommand = config.UartFakeLoRaRxMode,
            TransmitCommand = new LoRaMessage
            {
                CorrelationCode = 0,
                DeviceId = 0,
                IsMulticast = true,
                RlncTerminationCommand = new RlncTerminationCommand()
            }
        };

        WriteMessage(command);
    }
}