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
        var fieldA = new GFSymbol(3);
        var fieldB = new GFSymbol(4);

        (fieldA + fieldA).GetValue().ShouldBe((byte) 0x00);
        (fieldA + fieldB).GetValue().ShouldBe((byte) 0x07);
        (fieldA - fieldB).GetValue().ShouldBe((byte) 0x07);
        (fieldA * fieldB).GetValue().ShouldBe((byte) 0b1100);
    }

    [Fact]
    public void GaloisFieldInversionTest()
    {
        var a = new GFSymbol(133);
        var b = new GFSymbol(217);
        var unit = new GFSymbol(1);

        var aInverse = unit / a;
        (a * aInverse).ShouldBe(unit);

        var c = a * b;
        (c / b).ShouldBe(a);
        GFSymbol.Log.Length.ShouldBe(256);
    }
    
    [Fact]
    public void GaloisFieldInversionTest_PracticalCalculations()
    {
        var a = new GFSymbol(0xb4);
        var b = new GFSymbol(0x70);

        var result = a / b;
        // Our validation should show 255 but this was wrong (embedded C++ implementation)
        ((int)result.GetValue()).ShouldBe(202);
    }
    
    [Fact]
    public void GaloisFieldInversionTest_PracticalCalculation2()
    {
        var a = new GFSymbol(0xdb);
        var b = new GFSymbol(0x04);

        var result = a / b;
        // Our validation should show 255 but this was wrong (embedded C++ implementation)
        ((int)result.GetValue()).ShouldBe(255);
    }
    
    [Fact]
    public void GaloisFieldInversionTest_PracticalCalculation3()
    {
        var a = new GFSymbol(0x04);
        var unity = new GFSymbol(0x01);

        // Tests the ReduceRow (pivoting) function
        var result = unity / a;
        ((int)result.GetValue()).ShouldBe(0x47);
        (a * result).ShouldBe(unity);
        (new GFSymbol(0xdb) * result).ShouldBe(new GFSymbol(0xFF));
    }
}