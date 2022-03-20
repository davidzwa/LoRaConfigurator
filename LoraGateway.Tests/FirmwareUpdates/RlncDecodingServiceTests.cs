using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using LoraGateway.Services.Firmware;
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
        var lossyChannelPackets = new List<EncodedPacket>(encodedPackets);
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
        result[0, 0].ShouldBe(new GFSymbol(0x01));
        result[1, 1].ShouldBe(new GFSymbol(0x01));
        result[2, 2].ShouldBe(new GFSymbol(0x01));
        result[3, 3].ShouldBe(new GFSymbol(0x01));
        result[4, 4].ShouldBe(new GFSymbol(0x01));
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
        result[0, 0].ShouldBe(new GFSymbol(0x01));
        result[1, 1].ShouldBe(new GFSymbol(0x01));
        result[2, 2].ShouldBe(new GFSymbol(0x01));
        result[3, 3].ShouldBe(new GFSymbol(0x01));
        result[4, 4].ShouldBe(new GFSymbol(0x01));
        result[5, 4].ShouldBe(new GFSymbol(0x00)); // Redundant packet
    }

    [Fact]
    public void DebugDecodingMatrixReduction()
    {
        byte[,] inputBytes = {
            { 0x04, 0x82, 0x41, 0xa0, 0xd0, 0x00, 0x00, 0x00, 0x9a, 0x78 },
            { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }
        };
        var inputMatrix = inputBytes.BytesToMatrix();
        var decodedResultMatrix = RlncDecodingService.DecodeMatrix(inputMatrix, 5);
        
        byte[,] decodingBytes = {
            { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            { 0x01, 0x01, 0x8e, 0xc7, 0xb3, 0x00, 0x00, 0x00, 0xa2, 0x15 },
            { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }
        };
        var comparedOutputMatrix = decodingBytes.BytesToMatrix();

        // This shows the C# implementation is not faulty
        for(int i = 0; i < decodedResultMatrix.GetLength(0); i++)
        {
            for(int j = 0; j < decodedResultMatrix.GetLength(1); j++)
            {
                decodedResultMatrix[i,j].ShouldBe(comparedOutputMatrix[i,j], $"Row {i} col {j}"); 
            }
        }   
    }
}