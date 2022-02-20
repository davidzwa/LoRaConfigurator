using System.Linq;
using LoraGateway.Services.Firmware;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.FirmwareUpdates;

public class RlncEncodingServiceTests
{
    [Fact]
    public void EncodeOneGenerationTest()
    {
        var firmwareSize = 100;
        var frameSize = 20;
        var fakeFirmware = new BlobFragmentationService().GenerateFakeFirmware(firmwareSize, frameSize);
        var serviceUnderTest = new RlncEncodingService(fakeFirmware.Count);
        serviceUnderTest.PreprocessGenerations(fakeFirmware);
        
        var generation = serviceUnderTest.EncodeNextGeneration();
        
        generation.EncodedPackets.Count.ShouldBe(fakeFirmware.Count);
        generation.GenerationIndex.ShouldBe(0);
        generation.OriginalPackets.First().ShouldBe(fakeFirmware.First());
    }
}