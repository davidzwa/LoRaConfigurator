using System.ComponentModel.DataAnnotations;
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
        var serviceUnderTest = new RlncEncodingService((uint)fakeFirmware.Count);
        serviceUnderTest.PreprocessGenerations(fakeFirmware);

        var generation = serviceUnderTest.EncodeNextGeneration();

        generation.EncodedPackets.Count.ShouldBe(fakeFirmware.Count);
        generation.GenerationIndex.ShouldBe(0);
        generation.OriginalPackets.First().ShouldBe(fakeFirmware.First());
    }

    [Fact]
    public void RoundedGenerationSizeTest()
    {
        var firmwareSize = 1; // This firmware size should not be problematic for our fake firmware generator
        var frameSize = 20;
        var fakeFirmware = new BlobFragmentationService().GenerateFakeFirmware(firmwareSize, frameSize);
        var serviceUnderTest = new RlncEncodingService((uint)fakeFirmware.Count);

        serviceUnderTest.PreprocessGenerations(fakeFirmware);
        var nextGeneration = serviceUnderTest.EncodeNextGeneration();
        nextGeneration.EncodedPackets.Count.ShouldBe(1);
    }
    
    [Fact]
    public void GoodGenerationSizeTest()
    {
        var firmwareSize = 15;
        var frameSize = 1;
        var fakeFirmware = new BlobFragmentationService().GenerateFakeFirmware(firmwareSize, frameSize);
        fakeFirmware.Count.ShouldBe(firmwareSize);
        
        var generationSize = (uint)fakeFirmware.Count;
        var serviceUnderTest = new RlncEncodingService(generationSize);
        
        serviceUnderTest.PreprocessGenerations(fakeFirmware);
        serviceUnderTest.EncodeNextGeneration();
    }
    
    [Fact]
    public void BadGenerationSizeTest()
    {
        var firmwareSize = 20;
        var frameSize = 1;
        var fakeFirmware = new BlobFragmentationService().GenerateFakeFirmware(firmwareSize, frameSize);
        fakeFirmware.Count.ShouldBe(firmwareSize);
        var generationSize = (uint)fakeFirmware.Count;
        Should.Throw<ValidationException>(() => new RlncEncodingService(generationSize));
    }
}