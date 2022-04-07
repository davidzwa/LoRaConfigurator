using LoraGateway.Services.Firmware.Packets;
using LoraGateway.Services.Firmware.RandomLinearCoding;
using LoraGateway.Services.Firmware.Utils;

namespace LoraGateway.Services.Firmware;

public static class RlncDecodingService
{
    // https://github.com/elsheimy/Elsheimy.Components.Linears/tree/main/Matrix
    public static List<DecodedPacket> DecodePackets(List<IEncodedPacket> encodedPackets)
    {
        var generationSize = encodedPackets.First().EncodingVector.Count;
        var frameSize = encodedPackets.First().Payload.Count;
        var encodingMatrix = encodedPackets.ToAugmentedMatrix();

        var result = DecodeMatrix(encodingMatrix, frameSize);

        return result.ToDecodedPackets(generationSize, frameSize);
    }

    public static GFSymbol[,] DecodeMatrix(GFSymbol[,] matrix, int augmentedCols)
    {
        return MatrixFunctions.Eliminate(matrix, augmentedCols);
    }

    private static List<DecodedPacket> FilterInnovativePackets(List<DecodedPacket> decodedPackets)
    {
        List<DecodedPacket> innovativePackets = new();

        foreach (var index in Enumerable.Range(0, decodedPackets.Count))
        {
            var packet = decodedPackets[index];
            if (packet.DecodingSuccess && packet.IsRedundant == false) innovativePackets.Add(packet);
        }

        return innovativePackets;
    }

    public static List<DecodedPacket> DecodeGeneration(List<IEncodedPacket> generationPackets)
    {
        var result = DecodePackets(generationPackets);

        return FilterInnovativePackets(result);
    }

    public static byte[] SerializePacketsToBinary(this List<DecodedPacket> innovativePackets)
    {
        return innovativePackets
            .SelectMany(p => p.Payload.Select(p => p.GetValue()))
            .ToArray();
    }
}