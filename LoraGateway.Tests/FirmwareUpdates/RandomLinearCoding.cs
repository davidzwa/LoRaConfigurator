using System.Linq;
using LoraGateway.Services.Firmware.RandomLinearCoding;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.FirmwareUpdates;

public class RandomLinearCoding
{
    [Fact]
    public void RandomNumberGeneration()
    {
        // Test the LFSR implementation for 16-bits with cycle 65535
        var generator = new LinearFeedbackShiftRegister(0x1234);

        // 091a
        // 848d
        // c246
        // e123
        // 7091
        generator.Generate().ToString("X").ShouldBe("91A");
        var rngList = generator.GenerateMany(4).ToList();
        rngList[0].ToString("X").ShouldBe("848D");
        rngList[1].ToString("X").ShouldBe("C246");
        rngList[2].ToString("X").ShouldBe("E123");
        rngList[3].ToString("X").ShouldBe("7091");
    }
}