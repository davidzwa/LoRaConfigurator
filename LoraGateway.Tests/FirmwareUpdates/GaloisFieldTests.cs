﻿using LoraGateway.Services.Firmware.RandomLinearCoding;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.FirmwareUpdates;

public class GaloisFieldTests
{
    [Fact]
    public void TestGaloisFieldOperations()
    {
        // Verified with:
        // https://asecuritysite.com/encryption/gf?a0=1%2C0%2C0%2C1&a1=1%2C0%2C1&b0=1%2C0%2C0%2C1%2C1
        var fieldA = new GField(3);
        var fieldB = new GField(4);
        
        (fieldA + fieldA).GetValue().ShouldBe((byte)0x00);
        (fieldA + fieldB).GetValue().ShouldBe((byte)0x07);
        (fieldA - fieldB).GetValue().ShouldBe((byte)0x07);
        (fieldA * fieldB).GetValue().ShouldBe((byte)0b1100);
    }
}