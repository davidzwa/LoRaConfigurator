using LoraGateway.Services.Firmware.RandomLinearCoding;
using LoraGateway.Services.Firmware.Utils;

namespace LoraGateway.Services.Firmware;

public static class RlncDecodingService
{
    // https://github.com/elsheimy/Elsheimy.Components.Linears/tree/main/Matrix
    public static List<DecodedPacket> DecodePackets(List<EncodedPacket> encodedPackets)
    {
        var generationSize = encodedPackets.First().EncodingVector.Count;
        var frameSize = encodedPackets.First().Payload.Count;
        var encodingMatrix = encodedPackets.ToAugmentedMatrix();

        var result = MatrixFunctions.Reduce(encodingMatrix);

        return result.ToDecodedPackets(generationSize, frameSize);
    }

    private static List<DecodedPacket> FilterInnovativePackets(List<DecodedPacket> decodedPackets)
    {
        List<DecodedPacket> innovativePackets = new();

        foreach (var index in Enumerable.Range(0, decodedPackets.Count))
        {
            var packet = decodedPackets[index];
            if (packet.DecodingSuccess && packet.IsRedundant == false)
            {
                innovativePackets.Add(packet);
            }
        }

        return innovativePackets;
    }

    public static List<DecodedPacket> DecodeGeneration(List<EncodedPacket> generationPackets)
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