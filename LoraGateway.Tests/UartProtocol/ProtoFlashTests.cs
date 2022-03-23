using LoRa;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.UartProtocol;

public class ProtoFlashTests
{
    [Fact]
    public void InitCommandDeserializeTest()
    {
        byte[] data = {
            0x52, 0x0f, 0x08, 0x04, 0x10, 0x0d, 0x18, 0x0a, 0x20, 0x32, 0x28, 0x08, 0x30, 0x9d, 0x02, 0x38, 0x08
        };

        LoRaMessage message = LoRaMessage.Parser.ParseFrom(data);
        message.BodyCase.ShouldBe(LoRaMessage.BodyOneofCase.RlncInitConfigCommand);
    }
}