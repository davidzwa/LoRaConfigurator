using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LoraGateway.Services.Firmware;
using LoraGateway.Services.Firmware.Packets;
using LoraGateway.Services.Firmware.RandomLinearCoding;
using LoraGateway.Services.Firmware.Utils;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.FirmwareUpdates;

public class RlncDecodingServiceTests
{
    [Fact]
    public async Task DependentFailureDecodePacketsTest()
    {
        var firmwareSize = 100;
        var frameSize = 10; // 10 symbols per packet
        var generationSize = (uint)5; // 5 packets (= 5 enc vectors)
        var generationExtra = (uint)0; // no packets overhead
        var totalPacketsOutput = generationSize;

        var unencodedPackets = await new BlobFragmentationService().GenerateFakeFirmwareAsync(firmwareSize, frameSize);
        var service = new RlncEncodingService();
        service.PreprocessGenerations(unencodedPackets, generationSize);
        var encodedPackets = service.PrecodeCurrentGeneration(generationExtra).EncodedPackets;

        // Simulate a dropped packet
        encodedPackets.RemoveAt(0);

        var decodedPackets = RlncDecodingService.DecodePackets(encodedPackets);
        decodedPackets.Count.ShouldBe(4);
        decodedPackets.ShouldAllBe(p => !p.IsRedundant && !p.DecodingSuccess);
        decodedPackets.FindAll(p => p.DecodingSuccess).Count.ShouldBe(0);
    }

    [Fact]
    public async Task RestoredFailureDecodePacketsTest()
    {
        var firmwareSize = 100;
        var frameSize = 10; // 10 symbols per packet
        var generationSize = (uint)5; // 5 packets (= 5 enc vectors)
        var generationExtra = (uint)0; // no packets overhead
        var totalPacketsOutput = generationSize;

        var unencodedPackets = await new BlobFragmentationService().GenerateFakeFirmwareAsync(firmwareSize, frameSize);
        var encodingService = new RlncEncodingService();
        encodingService.PreprocessGenerations(unencodedPackets, generationSize);
        var encodedPackets = encodingService.PrecodeCurrentGeneration(generationExtra).EncodedPackets;

        // Simulate a dropped packet
        var lossyChannelPackets = new List<IEncodedPacket>(encodedPackets);
        lossyChannelPackets.RemoveAt(0);
        lossyChannelPackets.Count.ShouldBe((int)totalPacketsOutput - 1);

        // Failure in decoding
        var decodedPackets = RlncDecodingService.DecodePackets(lossyChannelPackets);
        decodedPackets.Count.ShouldBe(4);
        decodedPackets.ShouldAllBe(p => !p.IsRedundant && !p.DecodingSuccess);
        decodedPackets.FindAll(p => p.DecodingSuccess).Count.ShouldBe(0);

        // Add a new one
        var fixingPacketCount = 1;
        var fixingPackets = encodingService.PrecodeNumberOfPackets((uint)fixingPacketCount);
        fixingPackets.Count.ShouldBe(fixingPacketCount);
        lossyChannelPackets.AddRange(fixingPackets);
        lossyChannelPackets.Count.ShouldBe((int)totalPacketsOutput);

        // We can fix it
        var decodedPacketsRetry = RlncDecodingService.DecodePackets(lossyChannelPackets);
        decodedPacketsRetry.Count.ShouldBe(5);
        decodedPacketsRetry.ShouldAllBe(p => !p.IsRedundant && p.DecodingSuccess);
        decodedPacketsRetry.FindAll(p => p.DecodingSuccess).Count.ShouldBe(5);
        var byteArray = decodedPacketsRetry.SerializePacketsToBinary();

        // Filtering on innovative packets should not do weird stuff 
        decodedPacketsRetry = RlncDecodingService.DecodeGeneration(lossyChannelPackets);
        decodedPacketsRetry.Count.ShouldBe(5);
        var byteArray2 = decodedPacketsRetry.SerializePacketsToBinary();

        // Now validate that the result was still correct
        byteArray.ShouldBeEquivalentTo(byteArray2);
        byteArray[3].ShouldBe(unencodedPackets[0].Payload[3].GetValue());
    }

    [Fact]
    public async Task SuccessfulExactDecodePacketsTest()
    {
        var firmwareSize = 100;
        var frameSize = 10; // 10 symbols per packet
        var generationSize = (uint)5; // 5 packets (= 5 enc vectors)
        var generationExtra = (uint)0; // no packets overhead
        var totalPacketsOutput = generationSize;

        var unencodedPackets = await new BlobFragmentationService().GenerateFakeFirmwareAsync(firmwareSize, frameSize);
        var service = new RlncEncodingService();
        service.PreprocessGenerations(unencodedPackets, generationSize);
        var encodedPackets = service.PrecodeCurrentGeneration(generationExtra).EncodedPackets;

        var decodedPackets = RlncDecodingService.DecodePackets(encodedPackets);
        decodedPackets.Count.ShouldBe((int)totalPacketsOutput);
        decodedPackets.Last().DecodingSuccess.ShouldBeTrue();
        decodedPackets.ShouldAllBe(p => p.IsRedundant == false);
        decodedPackets.FindAll(p => p.DecodingSuccess).Count.ShouldBe((int)generationSize);
        var byteArray = decodedPackets.SerializePacketsToBinary();
        byteArray.Length.ShouldBe(decodedPackets.Count * decodedPackets.First().Payload.Count);
        byteArray.Length.ShouldBeGreaterThan(1);
        byteArray[3].ShouldBe(unencodedPackets[0].Payload[3].GetValue());
    }

    [Fact]
    public async Task SuccessfulOverheadDecodePacketsTest()
    {
        var firmwareSize = 100;
        var frameSize = 10; // 10 symbols per packet
        var generationSize = (uint)5; // 5 packets (= 5 enc vectors)
        var generationExtra = (uint)1; // 1 packet overhead
        var totalPacketsOutput = generationExtra + generationSize;

        var unencodedPackets = await new BlobFragmentationService().GenerateFakeFirmwareAsync(firmwareSize, frameSize);
        var service = new RlncEncodingService();
        service.PreprocessGenerations(unencodedPackets, generationSize);
        var encodedPackets = service.PrecodeCurrentGeneration(generationExtra).EncodedPackets;

        var decodedPackets = RlncDecodingService.DecodePackets(encodedPackets);
        decodedPackets.Count.ShouldBe((int)totalPacketsOutput);
        decodedPackets.Last().DecodingSuccess.ShouldBeFalse();
        decodedPackets.ShouldAllBe(p => p.DecodingSuccess == true || p.IsRedundant == true);
        decodedPackets.FindAll(p => p.DecodingSuccess).Count.ShouldBe((int)generationSize);
    }

    [Fact]
    public async Task SkipGenerationOverheadDecodePacketsTest()
    {
        // A test ab absurdum where a whole generation is dropped to check for proper state management

        var firmwareSize = 100;
        var frameSize = 10; // 10 symbols per packet
        var generationSize = (uint)5; // 5 packets (= 5 enc vectors)
        var generationExtra = (uint)5; // 1 packet overhead
        var totalPacketsOutput = generationExtra + generationSize;

        var unencodedPackets = await new BlobFragmentationService().GenerateFakeFirmwareAsync(firmwareSize, frameSize);
        var service = new RlncEncodingService();
        service.PreprocessGenerations(unencodedPackets, generationSize);
        var encodedPackets = service.PrecodeCurrentGeneration(generationExtra).EncodedPackets;
        var chunkedPacketSets = encodedPackets.Chunk(5).ToList();

        var decodedPackets = RlncDecodingService.DecodePackets(chunkedPacketSets.Last().ToList());
        decodedPackets.Count.ShouldBe((int)generationExtra);
        decodedPackets.Last().DecodingSuccess.ShouldBeTrue();
        decodedPackets.ShouldAllBe(p => p.DecodingSuccess && p.IsRedundant == false);
        decodedPackets.FindAll(p => p.DecodingSuccess).Count.ShouldBe((int)generationSize);
    }

    [Fact]
    public async Task DecodeMatrixTestInternally()
    {
        var firmwareSize = 100;
        var frameSize = 10; // 10 symbols per packet
        var generationSize = (uint)5; // 5 packets (= 5 enc vectors)
        var generationExtra = (uint)1; // 1 packet overhead
        var totalPacketsOutput = generationExtra + generationSize;

        var unencodedPackets = await new BlobFragmentationService().GenerateFakeFirmwareAsync(firmwareSize, frameSize);
        var service = new RlncEncodingService();
        service.PreprocessGenerations(unencodedPackets, generationSize);
        var generation = service.PrecodeCurrentGeneration(generationExtra);

        var fullAugmentedMatrix = generation.EncodedPackets.ToAugmentedMatrix();
        // 6 * (5+10) = 30
        fullAugmentedMatrix.Length.ShouldBe((int)(totalPacketsOutput * (generationSize + frameSize)));
        fullAugmentedMatrix[0, 10].ShouldNotBeNull();

        var result = MatrixFunctions.Eliminate(fullAugmentedMatrix, frameSize);
        for (int i = 0; i < 5; i++)
        {
            result[i, i].ShouldBe(new GFSymbol(0x01), $"Pivot number {i}");
        }

        result[5, 4].ShouldBe(new GFSymbol(0x00)); // Redundant packet

        var decodedPackets = result.ToDecodedPackets((int)generationSize, frameSize);
        decodedPackets.Count.ShouldBe(6);
        decodedPackets.Last().DecodingSuccess.ShouldBeFalse();
        decodedPackets.ShouldAllBe(p => p.DecodingSuccess == true || p.IsRedundant == true);
        decodedPackets.FindAll(p => p.DecodingSuccess).Count.ShouldBe((int)generationSize);
    }

    [Fact]
    public void MatrixRankTest()
    {
        GFSymbol[,] encMatrix =
        {
            { new(0x01), new(0x01) },
            { new(0x00), new(0x01) }
        };

        // Lin Alg rank of matrix must 
        encMatrix.Rank.ShouldBe(2);
    }

    [Fact]
    public async Task AnalyseEncodingMatrix()
    {
        var firmwareSize = 100;
        var frameSize = 10;
        var generationSize = (uint)5;
        var generationExtra = (uint)1;
        var totalPacketsOutput = generationExtra + generationSize;

        var unencodedPackets = await new BlobFragmentationService().GenerateFakeFirmwareAsync(firmwareSize, frameSize);
        var service = new RlncEncodingService();
        service.PreprocessGenerations(unencodedPackets, generationSize);
        var generation = service.PrecodeCurrentGeneration(generationExtra);

        var encodingMatrix = generation.EncodedPackets.ToEncodingMatrix();
        // 6 * 5 = 30
        encodingMatrix.Length.ShouldBe((int)(totalPacketsOutput * generationSize));

        // We have not augmented the matrix - 0 augmentation
        var result = MatrixFunctions.Eliminate(encodingMatrix, 0);
        for (int i = 0; i < 5; i++)
        {
            result[i, i].ShouldBe(new GFSymbol(0x01), $"Pivot number {i}");
        }

        result[5, 4].ShouldBe(new GFSymbol(0x00)); // Redundant packet
    }

    [Fact]
    public void DebugDecodingMatrixReduction()
    {
        byte[,] inputBytes =
        {
            { 0x04, 0x82, 0x41, 0xa0, 0xd0, 0x00, 0x00, 0x00, 0x9a, 0x78 },
            { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }
        };
        var inputMatrix = inputBytes.BytesToMatrix();
        var decodedResultMatrix = RlncDecodingService.DecodeMatrix(inputMatrix, 5);

        byte[,] decodingBytes =
        {
            { 0x01, 0xae, 0x57, 0x28, 0x34, 0x00, 0x00, 0x00, 0xa8, 0x1e },
            { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }
        };
        var comparedOutputMatrix = decodingBytes.BytesToMatrix();

        // This shows the C# implementation is not faulty and equivalent to the C++ implementation
        for (int i = 0; i < decodedResultMatrix.GetLength(0); i++)
        {
            for (int j = 0; j < decodedResultMatrix.GetLength(1); j++)
            {
                decodedResultMatrix[i, j].ShouldBe(comparedOutputMatrix[i, j], $"Row {i} col {j}");
            }
        }
    }

    [Fact(DisplayName = "Test for specific C++ RREF failure for 10g/10r")]
    public void DebugDecodingMatrixReductionBigGeneration()
    {
        // We found a breaking bug at gen size 10
        byte[,] inputBytes =
        {
            {
                0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x00, 0x01, 0xff, 0xff, 0xff,
                0xff, 0xff, 0xff
            },
            {
                0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x09, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00
            },
            {
                0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x0a, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00
            },
            {
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x0a, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00
            },
            {
                0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0xff, 0xff, 0xff,
                0xff, 0xff, 0xff
            },
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0xff, 0xff, 0xff,
                0xff, 0xff, 0xff
            },
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0xff, 0xff, 0xff,
                0xff, 0xff, 0xff
            },
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00, 0x06, 0xff, 0xff, 0xff,
                0xff, 0xff, 0xff
            },
            {
                0x1d, 0x0e, 0x07, 0x83, 0xc1, 0x60, 0x30, 0x18, 0x0c, 0x86, 0x00, 0x00, 0x00, 0x0c, 0xf2, 0xf2, 0xf2,
                0xf2, 0xf2, 0xf2
            },
            { // Empty row
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00
            }
        };
        var inputMatrix = inputBytes.BytesToMatrix();
        var decodedResultMatrix = RlncDecodingService.DecodeMatrix(inputMatrix, 5);
        
        // We only get 0-8, which is expected with only 9 packets
        for (int i = 0; i < 8; i++)
        {
            decodedResultMatrix[i, i].ShouldBe(new GFSymbol(0x01), $"Pivot number {i}");
        }
    }

    [Fact(DisplayName = "Codec test for specific C++ failure for 200f/15b/8gen/10r")]
    public async Task DecodeMatrixTestInternallyBigGeneration()
    {
        var firmwareSize = 200;
        var frameSize = 15; // 15 symbols per packet
        var generationSize = (uint)8; // x frames in window (=> x enc vectors of x symbols each)
        var generationExtra = (uint)10; // x frames redundancy
        var totalPacketsOutput = generationExtra + generationSize;

        var unencodedPackets = await new BlobFragmentationService().GenerateFakeFirmwareAsync(firmwareSize, frameSize);
        var service = new RlncEncodingService();
        service.PreprocessGenerations(unencodedPackets, generationSize);
        var generation = service.PrecodeCurrentGeneration(generationExtra);

        var fullAugmentedMatrix = generation.EncodedPackets.ToAugmentedMatrix();
        fullAugmentedMatrix.Length.ShouldBe((int)(totalPacketsOutput * (generationSize + frameSize)));
        fullAugmentedMatrix[0, 10].ShouldNotBeNull();

        var result = MatrixFunctions.Eliminate(fullAugmentedMatrix, frameSize);
        for (int i = 0; i < 5; i++)
        {
            result[i, i].ShouldBe(new GFSymbol(0x01), $"Pivot number {i}");
        }

        result[5, 4].ShouldBe(new GFSymbol(0x00)); // Redundant packet

        var decodedPackets = result.ToDecodedPackets((int)generationSize, frameSize);
        decodedPackets.Count.ShouldBe((int)totalPacketsOutput);
        decodedPackets.Last().DecodingSuccess.ShouldBeFalse();
        decodedPackets.ShouldAllBe(p => p.DecodingSuccess == true || p.IsRedundant == true);
        decodedPackets.FindAll(p => p.DecodingSuccess).Count.ShouldBe((int)generationSize);
    }

    [Fact(DisplayName = "Codec test for specific C++ failure for 200f/15b/9gen/20r")]
    public async Task DecodeMatrixTestInternallyBiggerGeneration()
    {
        var firmwareSize = 200;
        var frameSize = 15; // 10 symbols per packet
        var generationSize = (uint)9; // 5 packets (= 5 enc vectors)
        var generationExtra = (uint)10; // 1 packet overhead
        var totalPacketsOutput = generationExtra + generationSize;

        var unencodedPackets = await new BlobFragmentationService().GenerateFakeFirmwareAsync(firmwareSize, frameSize);
        var service = new RlncEncodingService();
        service.PreprocessGenerations(unencodedPackets, generationSize);
        var generation = service.PrecodeCurrentGeneration(generationExtra);

        var fullAugmentedMatrix = generation.EncodedPackets.ToAugmentedMatrix();
        // 6 * (5+10) = 30
        fullAugmentedMatrix.Length.ShouldBe((int)(totalPacketsOutput * (generationSize + frameSize)));
        fullAugmentedMatrix[0, 10].ShouldNotBeNull();

        var result = MatrixFunctions.Eliminate(fullAugmentedMatrix, frameSize);
        for (int i = 0; i < 5; i++)
        {
            result[i, i].ShouldBe(new GFSymbol(0x01), $"Pivot number {i}");
        }

        result[5, 4].ShouldBe(new GFSymbol(0x00)); // Redundant packet

        var decodedPackets = result.ToDecodedPackets((int)generationSize, frameSize);
        decodedPackets.Count.ShouldBe((int)totalPacketsOutput);
        decodedPackets.Last().DecodingSuccess.ShouldBeFalse();
        decodedPackets.ShouldAllBe(p => p.DecodingSuccess == true || p.IsRedundant == true);
        decodedPackets.FindAll(p => p.DecodingSuccess).Count.ShouldBe((int)generationSize);
    }
}