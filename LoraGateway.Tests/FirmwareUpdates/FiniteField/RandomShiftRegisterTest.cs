using System.Linq;
using LoraGateway.Services.Firmware.RandomLinearCoding;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.FirmwareUpdates.FiniteField;

public class RandomShiftRegisterTests
{
    [Fact]
    public void RandomNumberGeneration()
    {
        // Test the LFSR implementation for 8-bits with cycle 255
        var generator = new LinearFeedbackShiftRegister(0x12);
        generator.Generate().ShouldBe((byte) 137);
        generator.Generate().ShouldBe((byte) 68);

        var rngList = generator.GenerateMany(4).ToList();
        rngList[0].ShouldBe((byte) 162);
        rngList[1].ShouldBe((byte) 81);
        rngList[2].ShouldBe((byte) 168);
        rngList[3].ShouldBe((byte) 212);
        
        var generator2 = new LinearFeedbackShiftRegister(0x08);
        var rngList2 = generator2.GenerateMany(4).ToList();
        rngList2[0].ShouldBe((byte) 4);
        rngList2[1].ShouldBe((byte) 130);
        rngList2[2].ShouldBe((byte) 65);
        rngList2[3].ShouldBe((byte) 160);
    }

    // Nice way to test cycle length
    // byte seed = 0x12;
    // var generator = new LinearFeedbackShiftRegister(seed);
    //
    // var values = new List<byte>();
    // values.Add(generator.Generate());
    // Console.WriteLine("First reference {0} Count {1}", values.First(), values.Count);
    //     
    // values.Add(generator.Generate());
    // Console.WriteLine("Val {0} Count {1}", values.Last(), values.Count);
    // byte? comparisonValue = values.Last();
    //     while (comparisonValue != values.First())
    // {
    //     values.Add(generator.Generate());
    //     comparisonValue = values.Last();
    //     Console.WriteLine("Val {0} Count {1}", comparisonValue, values.Count);
    // }
}