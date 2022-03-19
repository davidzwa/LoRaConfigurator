using Shouldly;
using Xunit;

namespace LoraGateway.Tests.Utils;

public class Crc8Tests
{
    [Fact]
    public void Crc8PrototypeTest()
    {
        // Tests a static example to compare with the embedded side implementation
        // to test the polynomial

        var crc = Crc8.ComputeChecksum(new byte[] {0xFF, 0x12, 0x34, 0x00});
        crc.ShouldBe((byte)227);
        
        crc = Crc8.ComputeChecksum(new byte[] {0x00, 0x12, 0x34, 0x00});
        crc.ShouldBe((byte)1);
    }
}

