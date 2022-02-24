using System.Linq;
using LoraGateway.Services.Firmware;
using LoraGateway.Services.Firmware.RandomLinearCoding;
using LoraGateway.Services.Firmware.Utils;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.FirmwareUpdates;

public class RlncDecodingServiceTests
{
    [Fact]
    public void DependentFailureDecodePacketsTest()
    {
        var firmwareSize = 100;
        var frameSize = 10; // 10 symbols per packet
        var generationSize = (uint)5; // 5 packets (= 5 enc vectors)
        var generationExtra = (uint)0; // no packets overhead
        var totalPacketsOutput = generationSize;

        var unencodedPackets = new BlobFragmentationService().GenerateFakeFirmware(firmwareSize, frameSize);
        var service = new RlncEncodingService();
        service.PreprocessGenerations(unencodedPackets, generationSize);
        var encodedPackets = service.PrecodeNextGeneration(generationExtra).EncodedPackets;

        // Simulate a dropped packet
        encodedPackets.RemoveAt(0);

        var decodedPackets = RlncDecodingService.DecodePackets(encodedPackets);
        decodedPackets.Count.ShouldBe(4);
        decodedPackets.ShouldAllBe(p => !p.IsRedundant && !p.DecodingSuccess);
        decodedPackets.FindAll(p => p.DecodingSuccess).Count.ShouldBe(0);
    }

    [Fact]
    public void RestoredFailureDecodePacketsTest()
    {
        var firmwareSize = 100;
        var frameSize = 10; // 10 symbols per packet
        var generationSize = (uint)5; // 5 packets (= 5 enc vectors)
        var generationExtra = (uint)0; // no packets overhead
        var totalPacketsOutput = generationSize;

        var unencodedPackets = new BlobFragmentationService().GenerateFakeFirmware(firmwareSize, frameSize);
        var encodingService = new RlncEncodingService();
        encodingService.PreprocessGenerations(unencodedPackets, generationSize);
        var encodedPackets = encodingService.PrecodeNextGeneration(generationExtra).EncodedPackets;

        // Simulate a dropped packet
        encodedPackets.RemoveAt(0);

        // Failure in decoding
        var decodedPackets = RlncDecodingService.DecodePackets(encodedPackets);
        decodedPackets.Count.ShouldBe(4);
        decodedPackets.ShouldAllBe(p => !p.IsRedundant && !p.DecodingSuccess);
        decodedPackets.FindAll(p => p.DecodingSuccess).Count.ShouldBe(0);

        // Add a new one
        encodedPackets.AddRange(encodingService.PrecodeNumberOfPackets(1));

        // We can fix it
        var decodedPacketsRetry = RlncDecodingService.DecodePackets(encodedPackets);
        decodedPacketsRetry.Count.ShouldBe(5);
        decodedPacketsRetry.ShouldAllBe(p => !p.IsRedundant && p.DecodingSuccess);
        decodedPacketsRetry.FindAll(p => p.DecodingSuccess).Count.ShouldBe(5);
        var byteArray = decodedPacketsRetry.SerializePacketsToBinary();

        // Filtering on innovative packets should not do weird stuff 
        decodedPacketsRetry = RlncDecodingService.DecodeGeneration(encodedPackets);
        decodedPacketsRetry.Count.ShouldBe(5);
        var byteArray2 = decodedPacketsRetry.SerializePacketsToBinary();
        
        // Now validate that the result was still correct
        byteArray.ShouldBeEquivalentTo(byteArray2);
        byteArray[3].ShouldBe(unencodedPackets[0].Payload[3].GetValue());
    }

    [Fact]
    public void SuccessfulExactDecodePacketsTest()
    {
        var firmwareSize = 100;
        var frameSize = 10; // 10 symbols per packet
        var generationSize = (uint)5; // 5 packets (= 5 enc vectors)
        var generationExtra = (uint)0; // no packets overhead
        var totalPacketsOutput = generationSize;

        var unencodedPackets = new BlobFragmentationService().GenerateFakeFirmware(firmwareSize, frameSize);
        var service = new RlncEncodingService();
        service.PreprocessGenerations(unencodedPackets, generationSize);
        var encodedPackets = service.PrecodeNextGeneration(generationExtra).EncodedPackets;

        var decodedPackets = RlncDecodingService.DecodePackets(encodedPackets);
        decodedPackets.Count.ShouldBe(5);
        decodedPackets.Last().DecodingSuccess.ShouldBeTrue();
        decodedPackets.ShouldAllBe(p => p.IsRedundant == false);
        decodedPackets.FindAll(p => p.DecodingSuccess).Count.ShouldBe((int)generationSize);
        var byteArray = decodedPackets.SerializePacketsToBinary();
        byteArray.Length.ShouldBe(decodedPackets.Count * decodedPackets.First().Payload.Count);
        byteArray.Length.ShouldBeGreaterThan(1);
        byteArray[3].ShouldBe(unencodedPackets[0].Payload[3].GetValue());
    }

    [Fact]
    public void SuccessfulOverheadDecodePacketsTest()
    {
        var firmwareSize = 100;
        var frameSize = 10; // 10 symbols per packet
        var generationSize = (uint)5; // 5 packets (= 5 enc vectors)
        var generationExtra = (uint)1; // 1 packet overhead
        var totalPacketsOutput = generationExtra + generationSize;

        var unencodedPackets = new BlobFragmentationService().GenerateFakeFirmware(firmwareSize, frameSize);
        var service = new RlncEncodingService();
        service.PreprocessGenerations(unencodedPackets, generationSize);
        var encodedPackets = service.PrecodeNextGeneration(generationExtra).EncodedPackets;

        var decodedPackets = RlncDecodingService.DecodePackets(encodedPackets);
        decodedPackets.Count.ShouldBe(6);
        decodedPackets.Last().DecodingSuccess.ShouldBeFalse();
        decodedPackets.ShouldAllBe(p => p.DecodingSuccess == true || p.IsRedundant == true);
        decodedPackets.FindAll(p => p.DecodingSuccess).Count.ShouldBe((int)generationSize);
    }

    [Fact]
    public void DecodeMatrixTestInternally()
    {
        var firmwareSize = 100;
        var frameSize = 10; // 10 symbols per packet
        var generationSize = (uint)5; // 5 packets (= 5 enc vectors)
        var generationExtra = (uint)1; // 1 packet overhead
        var totalPacketsOutput = generationExtra + generationSize;

        var unencodedPackets = new BlobFragmentationService().GenerateFakeFirmware(firmwareSize, frameSize);
        var service = new RlncEncodingService();
        service.PreprocessGenerations(unencodedPackets, generationSize);
        var generation = service.PrecodeNextGeneration(generationExtra);

        var fullAugmentedMatrix = generation.EncodedPackets.ToAugmentedMatrix();
        // 6 * (5+10) = 30
        fullAugmentedMatrix.Length.ShouldBe((int)(totalPacketsOutput * (generationSize + frameSize)));
        fullAugmentedMatrix[0, 10].ShouldNotBeNull();

        var result = MatrixFunctions.Reduce(fullAugmentedMatrix);
        result[0, 0].ShouldBe(new GField(0x01));
        result[1, 1].ShouldBe(new GField(0x01));
        result[2, 2].ShouldBe(new GField(0x01));
        result[3, 3].ShouldBe(new GField(0x01));
        result[4, 4].ShouldBe(new GField(0x01));
        result[5, 4].ShouldBe(new GField(0x00)); // Redundant packet

        var decodedPackets = result.ToDecodedPackets((int)generationSize, frameSize);
        decodedPackets.Count.ShouldBe(6);
        decodedPackets.Last().DecodingSuccess.ShouldBeFalse();
        decodedPackets.ShouldAllBe(p => p.DecodingSuccess == true || p.IsRedundant == true);
        decodedPackets.FindAll(p => p.DecodingSuccess).Count.ShouldBe((int)generationSize);
    }

    [Fact]
    public void MatrixRankTest()
    {
        GField[,] encMatrix =
        {
            { new(0x01), new(0x01) },
            { new(0x00), new(0x01) }
        };

        // Lin Alg rank of matrix must 
        encMatrix.Rank.ShouldBe(2);
    }

    [Fact]
    public void AnalyseEncodingMatrix()
    {
        var firmwareSize = 100;
        var frameSize = 10;
        var generationSize = (uint)5;
        var generationExtra = (uint)1;
        var totalPacketsOutput = generationExtra + generationSize;

        var unencodedPackets = new BlobFragmentationService().GenerateFakeFirmware(firmwareSize, frameSize);
        var service = new RlncEncodingService();
        service.PreprocessGenerations(unencodedPackets, generationSize);
        var generation = service.PrecodeNextGeneration(generationExtra);

        var encodingMatrix = generation.EncodedPackets.ToEncodingMatrix();
        // 6 * 5 = 30
        encodingMatrix.Length.ShouldBe((int)(totalPacketsOutput * generationSize));

        var result = MatrixFunctions.Reduce(encodingMatrix);
        result[0, 0].ShouldBe(new GField(0x01));
        result[1, 1].ShouldBe(new GField(0x01));
        result[2, 2].ShouldBe(new GField(0x01));
        result[3, 3].ShouldBe(new GField(0x01));
        result[4, 4].ShouldBe(new GField(0x01));
        result[5, 4].ShouldBe(new GField(0x00)); // Redundant packet
    }
}