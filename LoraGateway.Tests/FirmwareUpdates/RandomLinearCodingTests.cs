using System.Linq;
using LoraGateway.Services.Firmware.RandomLinearCoding;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.FirmwareUpdates;

public class RandomLinearCodingTests
{
    [Fact]
    public void RandomNumberGeneration()
    {
        // Test the LFSR implementation for 8-bits with cycle 255
        var generator = new LinearFeedbackShiftRegister(0x12);

        generator.Generate().ShouldBe((byte)137);
        generator.Generate().ShouldBe((byte)68);
        var rngList = generator.GenerateMany(4).ToList();
        rngList[0].ShouldBe((byte)162);
        rngList[1].ShouldBe((byte)81);
        rngList[2].ShouldBe((byte)168);
        rngList[3].ShouldBe((byte)212);
    }
}