using LoraGateway.Services.Firmware;
using LoraGateway.Services.Firmware.RandomLinearCoding;
using LoraGateway.Services.Firmware.Utils;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.FirmwareUpdates;

public class RlncDecodingServiceTests
{
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
    public void AnalyseRankMatrix()
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
        
        var encMatrix = generation.EncodedPackets.ToEncodingMatrix();
        encMatrix.Length.ShouldBe(2);

        // encMatrix[0].
        // Console.WriteLine(encMatrix.Rank);
    }
}