using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using LoraGateway.Services.Firmware;
using LoraGateway.Services.Firmware.RandomLinearCoding;
using LoraGateway.Services.Firmware.Utils;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.FirmwareUpdates;

public class RlncEncodingServiceTests
{
    [Fact]
    public async Task EncodeOneGenerationTest()
    {
        var firmwareSize = 100;
        var frameSize = 20;
        var fakeFirmware = await new BlobFragmentationService().GenerateFakeFirmwareAsync(firmwareSize, frameSize);
        var serviceUnderTest = new RlncEncodingService();
        serviceUnderTest.PreprocessGenerations(fakeFirmware, (uint)fakeFirmware.Count);

        var generation = serviceUnderTest.PrecodeCurrentGeneration(0);

        generation.EncodedPackets.Count.ShouldBe(fakeFirmware.Count);
        generation.GenerationIndex.ShouldBe(0);
        generation.OriginalPackets.First().ShouldBe(fakeFirmware.First());

        var symbolMatrix = generation.EncodedPackets.ToEncodingMatrix();

        var flattened = Enumerable.Range(0, symbolMatrix.GetLength(0))
            .SelectMany(x => Enumerable.Range(0, symbolMatrix.GetLength(1))
                .Select(y => symbolMatrix[x, y]));
        
        // Check all values in the encoding matrix are unique (as promised by LFSR properties)
        var distinct = flattened.Distinct().ToList();
        distinct.Count.ShouldBe(flattened.Count());
    }

    [Fact]
    public async Task EncodeOneGenerationWithExtraTest()
    {
        // 103 / 12 -> 9 packets which is less than generation size 12 (on purpose)
        var unencodedPackets = await new BlobFragmentationService().GenerateFakeFirmwareAsync(103, 12);

        var service = new RlncEncodingService();
        service.PreprocessGenerations(unencodedPackets, 12);
        var result = service.PrecodeCurrentGeneration(1);
        result.EncodedPackets.Count.ShouldBe(10);
    }

    [Fact]
    public async Task EncodeOneGenerationLfsrOverrunTests()
    {
        // Generate 9 packets of size 12
        var unencodedPackets = await new BlobFragmentationService().GenerateFakeFirmwareAsync(103, 12);
        unencodedPackets.Count.ShouldBe(9);
        // Check that the 103/12 division resulted in whole packets
        unencodedPackets[unencodedPackets.Count - 1].Payload.Count.ShouldBe(12);

        var service = new RlncEncodingService();
        service.PreprocessGenerations(unencodedPackets, 12);
        // Check that the generator is in deterministic state
        service.GetGeneratorState().ShouldBe((byte)0x08);
        // Generates 9 original packets (103/12 => 9) with extra prematurely
        // 255 / 9 = 29 max => 20 extra at most
        Should.Throw<Exception>(() => service.PrecodeCurrentGeneration(29 - 9));

        service.PreprocessGenerations(unencodedPackets, 12);
        // Check that the generator has been reset
        service.GetGeneratorState().ShouldBe((byte)0x08);
        service.PrecodeCurrentGeneration(28 - 9);
    }

    [Fact]
    public async Task EncodeOneGenerationLfsrNoOverrunTests()
    {
        // Generate 9 packets of size 12
        var unencodedPackets = await new BlobFragmentationService().GenerateFakeFirmwareAsync(103, 12);
        var service = new RlncEncodingService();
        service.PreprocessGenerations(unencodedPackets, 12);
        // Check that the generator is in deterministic state
        service.GetGeneratorState().ShouldBe((byte)0x08);
        // Generates 9 original packets (103/12 => 9) with extra prematurely
        // 255 / 9 = 29 max => 20 extra at most
        service.PrecodeCurrentGeneration(28 - 9);
    }

    [Fact]
    public async Task RoundedGenerationSizeTest()
    {
        var firmwareSize = 1; // This firmware size should not be problematic for our fake firmware generator
        var frameSize = 20;
        var fakeFirmware = await new BlobFragmentationService().GenerateFakeFirmwareAsync(firmwareSize, frameSize);
        var serviceUnderTest = new RlncEncodingService();

        serviceUnderTest.PreprocessGenerations(fakeFirmware, (uint)fakeFirmware.Count);
        var nextGeneration = serviceUnderTest.PrecodeCurrentGeneration(0);
        nextGeneration.EncodedPackets.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GoodGenerationSizeTest()
    {
        var firmwareSize = 15;
        var frameSize = 1;
        var fakeFirmware = await new BlobFragmentationService().GenerateFakeFirmwareAsync(firmwareSize, frameSize);
        fakeFirmware.Count.ShouldBe(firmwareSize);

        var generationSize = (uint)fakeFirmware.Count;
        var serviceUnderTest = new RlncEncodingService();

        serviceUnderTest.PreprocessGenerations(fakeFirmware, generationSize);
        serviceUnderTest.PrecodeCurrentGeneration(0);
    }

    [Fact]
    public async Task BadGenerationSizeTest()
    {
        var firmwareSize = 20;
        var frameSize = 1;
        var fakeFirmware = await new BlobFragmentationService().GenerateFakeFirmwareAsync(firmwareSize, frameSize);
        fakeFirmware.Count.ShouldBe(firmwareSize);

        var generationSize = (uint)fakeFirmware.Count;
        var service = new RlncEncodingService();

        // Generation is not low enough - should be kept within bounds to prevent high memory usage (embedded...)
        Should.Throw<ValidationException>(() => service.PreprocessGenerations(fakeFirmware, generationSize));
    }

    [Fact]
    public void InconsistentPacketLengthsTest()
    {
        var unencodedPackets = new List<UnencodedPacket>().Append(new UnencodedPacket
        {
            Payload = new List<GFSymbol> { new(0x00) }
        }).Append(new UnencodedPacket
        {
            Payload = new List<GFSymbol> { new(0x00), new(0x01) }
        }).ToList();

        var generationSize = (uint)2;
        var service = new RlncEncodingService();

        // Packets are not padded correctly - encoding should not be allowed
        Should.Throw<ValidationException>(() => service.PreprocessGenerations(unencodedPackets, generationSize));
    }
}