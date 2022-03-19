using LoraGateway.Services.Firmware.RandomLinearCoding;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.FirmwareUpdates.FiniteField;

public class EmbeddedGaloisFieldTests
{
    [Fact]
    public void CompareEmbeddedGFieldOutputTest()
    {
        var fieldA = new GFSymbol(3);
        var fieldB = new GFSymbol(14);

        (fieldA * fieldB).GetValue().ShouldBe((byte) 18);
    }
}