using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using LoraGateway.Services.Firmware;
using LoraGateway.Services.Firmware.LoRaPhy;
using LoraGateway.Services.Firmware.RandomLinearCoding;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.FirmwareUpdates;

public class BlobFragmentationServiceTests
{
    [Fact]
    public async Task FakeFragmentationTests()
    {
        var unitUnderTest = new BlobFragmentationService();

        var firmwareSize = 100000;
        var loraPacketSize = 20;
        var fragmentationCollection = await unitUnderTest.GenerateFakeFirmwareAsync(firmwareSize, loraPacketSize);
        fragmentationCollection.Count.ShouldBe(5000);
        fragmentationCollection.First().Payload[0].ShouldBe(new GFSymbol(0x00));
        fragmentationCollection.First().Payload[1].ShouldBe(new GFSymbol(0x00));
        fragmentationCollection.First().Payload[2].ShouldBe(new GFSymbol(0x00));
        fragmentationCollection.First().Payload[3].ShouldBe(new GFSymbol(0x00));
        fragmentationCollection.Last().Payload[0].ShouldBe(new GFSymbol(0x00));
        fragmentationCollection.Last().Payload[1].ShouldBe(new GFSymbol(0x00));
        fragmentationCollection.Last().Payload[2].ShouldBe(new GFSymbol(0x13));
        fragmentationCollection.Last().Payload[3].ShouldBe(new GFSymbol(0x87));
    }

    [Fact]
    public async Task AlternativeShortFakeFirmwareTest()
    {
        // Tests the small input criteria for random firmware
        var firmware = await new BlobFragmentationService().GenerateFakeFirmwareAsync(100, 20);
        firmware.Count.ShouldBe(100 / 20);
    }

    [Fact]
    public void IllegalFrameSizeTest()
    {
        var firmwareSize = 0;
        var frameSize = LoRaWanTimeOnAir.PayloadMax + 1;
        Should.Throw<ValidationException>(() =>
            new BlobFragmentationService().GenerateFakeFirmwareAsync(firmwareSize, frameSize)
        );

        frameSize = 0;
        Should.Throw<ValidationException>(() =>
            new BlobFragmentationService().GenerateFakeFirmwareAsync(firmwareSize, frameSize)
        );
    }

    [Fact]
    public void TooManyFragmentsTest()
    {
        var firmwareSize = 20000;
        var frameSize = 1;
        Should.Throw<ValidationException>(() =>
            new BlobFragmentationService().GenerateFakeFirmwareAsync(firmwareSize, frameSize)
        );
    }

    [Fact]
    public async Task RoundedFakeFragmentationTest()
    {
        // This firmware size should not be problematic for our fake firmware generator - ceil used
        var firmwareSize = 1;
        var frameSize = 20;
        var fakeFirmware = await new BlobFragmentationService().GenerateFakeFirmwareAsync(firmwareSize, frameSize);
        fakeFirmware.Count.ShouldBe(1);

        // This firmware size should not be problematic for our fake firmware generator - ceil used
        firmwareSize = 20;
        frameSize = 20;
        var fakeFirmware2 = await new BlobFragmentationService().GenerateFakeFirmwareAsync(firmwareSize, frameSize);
        fakeFirmware2.Count.ShouldBe(1);

        // This firmware size should not be problematic for our fake firmware generator - ceil used
        firmwareSize = 21;
        frameSize = 20;
        var fakeFirmware3 = await new BlobFragmentationService().GenerateFakeFirmwareAsync(firmwareSize, frameSize);
        fakeFirmware3.Count.ShouldBe(2);
    }
}