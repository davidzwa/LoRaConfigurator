using LoraGateway.Services.Firmware.RandomLinearCoding;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.FirmwareUpdates;

public class EmbeddedGaloisFieldTests
{
    [Fact]
    public void CompareEmbeddedGFieldOutputTest()
    {
        var fieldA = new GField(3);
        var fieldB = new GField(14);
        
        (fieldA * fieldB).GetValue().ShouldBe((byte)18);
    }
}