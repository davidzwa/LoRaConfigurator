using LoRa;
using LoraGateway.Handlers;
using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Services;

public partial class SerialProcessorService
{
    public async Task SendRlncInitConfigCommand()
    {
        await _fuotaManagerService.LoadStore();

        var fuotaSession = await _fuotaManagerService.PrepareFuotaSession();
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
                    FieldPoly = GField.Polynomial,
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

        await _eventPublisher.PublishEventAsync(new InitFuotaSession() {Message = "yes"});

        WriteMessage(command);
    }

    public async Task SendNextRlncFragment()
    {
        var fuotaSession = _fuotaManagerService.GetCurrentSession();
    }
}