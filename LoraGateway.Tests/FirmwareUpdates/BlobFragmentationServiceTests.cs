using System.Linq;
using LoraGateway.Services.Firmware;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.FirmwareUpdates;

public class BlobFragmentationServiceTests
{
    [Fact]
    public void FakeFragmentationTests()
    {
        var unitUnderTest = new BlobFragmentationService();

        var firmwareSize = 100000;
        var loraPacketSize = 20;
        var fragmentationCollection = unitUnderTest.GenerateFakeFirmware(firmwareSize, loraPacketSize);
        fragmentationCollection.Count.ShouldBe(5000);
        fragmentationCollection.First().PacketIndex.ShouldBe(0);
        fragmentationCollection.First().Payload[0].ShouldBe((byte)0x00);
        fragmentationCollection.First().Payload[1].ShouldBe((byte)0x00);
        fragmentationCollection.First().Payload[2].ShouldBe((byte)0x00);
        fragmentationCollection.First().Payload[3].ShouldBe((byte)0x00);
        fragmentationCollection.Last().PacketIndex.ShouldBe(4999);
        fragmentationCollection.Last().Payload[0].ShouldBe((byte)0x00);
        fragmentationCollection.Last().Payload[1].ShouldBe((byte)0x00);
        fragmentationCollection.Last().Payload[2].ShouldBe((byte)0x13);
        fragmentationCollection.Last().Payload[3].ShouldBe((byte)0x87);
    }
    
    [Fact]
    public void AlternativeShortFakeFirmwareTest()
    {
        // Tests the small input criteria for random firmware
        var firmware = new BlobFragmentationService().GenerateFakeFirmware(100, 20);
        firmware.Count.ShouldBe(100 / 20);
    }
}