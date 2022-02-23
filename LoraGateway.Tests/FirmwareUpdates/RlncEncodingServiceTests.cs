using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using LoraGateway.Services.Firmware;
using LoraGateway.Services.Firmware.RandomLinearCoding;
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
        var serviceUnderTest = new RlncEncodingService();
        serviceUnderTest.PreprocessGenerations(fakeFirmware, (uint) fakeFirmware.Count);

        var generation = serviceUnderTest.PrecodeNextGeneration(0);

        generation.EncodedPackets.Count.ShouldBe(fakeFirmware.Count);
        generation.GenerationIndex.ShouldBe(0);
        generation.OriginalPackets.First().ShouldBe(fakeFirmware.First());
    }

    [Fact]
    public void EncodeOneGenerationWithExtraTest()
    {
        // 103 / 12 -> 9 packets which is less than generation size 12 (on purpose)
        var unencodedPackets = new BlobFragmentationService().GenerateFakeFirmware(103, 12);
        
        var service = new RlncEncodingService();
        service.PreprocessGenerations(unencodedPackets, 12);
        var result = service.PrecodeNextGeneration(1);
        result.EncodedPackets.Count.ShouldBe(10);
    }

    [Fact]
    public void EncodeOneGenerationLfsrOverrunTests()
    {
        // Generate 9 packets of size 12
        var unencodedPackets = new BlobFragmentationService().GenerateFakeFirmware(103, 12);
        unencodedPackets.Count.ShouldBe(9);
        // Check that the 103/12 division resulted in whole packets
        unencodedPackets[unencodedPackets.Count-1].Payload.Length.ShouldBe(12);

        var service = new RlncEncodingService();
        service.PreprocessGenerations(unencodedPackets, 12);
        // Check that the generator is in deterministic state
        service.GetGeneratorState().ShouldBe((byte)0x08);
        // Generates 9 original packets (103/12 => 9) with extra prematurely
        // 255 / 9 = 29 max => 20 extra at most
        Should.Throw<Exception>(() => service.PrecodeNextGeneration(29-9));
        
        service.PreprocessGenerations(unencodedPackets, 12);
        // Check that the generator has been reset
        service.GetGeneratorState().ShouldBe((byte)0x08);
        service.PrecodeNextGeneration(28 - 9);
    }

    [Fact]
    public void EncodeOneGenerationLfsrNoOverrunTests()
    {
        // Generate 9 packets of size 12
        var unencodedPackets = new BlobFragmentationService().GenerateFakeFirmware(103, 12);
        var service = new RlncEncodingService();
        service.PreprocessGenerations(unencodedPackets, 12);
        // Check that the generator is in deterministic state
        service.GetGeneratorState().ShouldBe((byte) 0x08);
        // Generates 9 original packets (103/12 => 9) with extra prematurely
        // 255 / 9 = 29 max => 20 extra at most
        service.PrecodeNextGeneration(28-9);
    }

    [Fact]
    public void RoundedGenerationSizeTest()
    {
        var firmwareSize = 1; // This firmware size should not be problematic for our fake firmware generator
        var frameSize = 20;
        var fakeFirmware = new BlobFragmentationService().GenerateFakeFirmware(firmwareSize, frameSize);
        var serviceUnderTest = new RlncEncodingService();

        serviceUnderTest.PreprocessGenerations(fakeFirmware, (uint) fakeFirmware.Count);
        var nextGeneration = serviceUnderTest.PrecodeNextGeneration(0);
        nextGeneration.EncodedPackets.Count.ShouldBe(1);
    }

    [Fact]
    public void GoodGenerationSizeTest()
    {
        var firmwareSize = 15;
        var frameSize = 1;
        var fakeFirmware = new BlobFragmentationService().GenerateFakeFirmware(firmwareSize, frameSize);
        fakeFirmware.Count.ShouldBe(firmwareSize);

        var generationSize = (uint) fakeFirmware.Count;
        var serviceUnderTest = new RlncEncodingService();

        serviceUnderTest.PreprocessGenerations(fakeFirmware, generationSize);
        serviceUnderTest.PrecodeNextGeneration(0);
    }

    [Fact]
    public void BadGenerationSizeTest()
    {
        var firmwareSize = 20;
        var frameSize = 1;
        var fakeFirmware = new BlobFragmentationService().GenerateFakeFirmware(firmwareSize, frameSize);
        fakeFirmware.Count.ShouldBe(firmwareSize);

        var generationSize = (uint) fakeFirmware.Count;
        var service = new RlncEncodingService();
        Should.Throw<ValidationException>(() => service.PreprocessGenerations(fakeFirmware, generationSize));
    }

    [Fact]
    public void InconsistentPacketLengthsTest()
    {
        var unencodedPackets = new List<IPacket>().Append(new UnencodedPacket()
        {
            Payload = new[] {(byte) 0x00}
        }).Append(new UnencodedPacket()
        {
            Payload = new[] {(byte) 0x00, (byte) 0x01},
        }).ToList();

        var generationSize = (uint) 2;
        var service = new RlncEncodingService();
        Should.Throw<ValidationException>(() => service.PreprocessGenerations(unencodedPackets, generationSize));
    }
}