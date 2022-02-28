using LoraGateway.Services.Firmware.RandomLinearCoding;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.FirmwareUpdates.RandomLinearNetworkCoding;

public class GaloisFieldTests
{
    [Fact]
    public void GaloisFieldOperationsTest()
    {
        // Verified with:
        // https://asecuritysite.com/encryption/gf?a0=1%2C0%2C0%2C1&a1=1%2C0%2C1&b0=1%2C0%2C0%2C1%2C1
        var fieldA = new GField(3);
        var fieldB = new GField(4);

        (fieldA + fieldA).GetValue().ShouldBe((byte) 0x00);
        (fieldA + fieldB).GetValue().ShouldBe((byte) 0x07);
        (fieldA - fieldB).GetValue().ShouldBe((byte) 0x07);
        (fieldA * fieldB).GetValue().ShouldBe((byte) 0b1100);
    }

    [Fact]
    public void GaloisFieldInversionTest()
    {
        var a = new GField(133);
        var b = new GField(217);
        var unit = new GField(1);

        var aInverse = unit / a;
        (a * aInverse).ShouldBe(unit);

        var c = a * b;
        (c / b).ShouldBe(a);
        GField.Log.Length.ShouldBe(256);
    }
}