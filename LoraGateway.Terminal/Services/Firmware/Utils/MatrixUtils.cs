using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Services.Firmware.Utils;

public static class MatrixUtils
{
    public static GField[,] ToAugmentedMatrix(this IList<EncodedPacket> source)
    {
        if (source == null) throw new ArgumentNullException("source");

        var max = source.Select(l => l.EncodingVector).Max(l => l.Count);
        var maxAugmentation = source.Select(l => l.Payload).Max(l => l.Count);
        var result = new GField[source.Count, max + maxAugmentation];
        for (var i = 0; i < source.Count; i++)
        {
            var systemicLength = source[i].EncodingVector.Count;
            var augmentationLength = source[i].Payload.Count;
            for (var j = 0; j < systemicLength + augmentationLength; j++)
                if (j < systemicLength)
                    result[i, j] = source[i].EncodingVector[j];
                else
                    result[i, j] = source[i].Payload[j - systemicLength];
        }

        return result;
    }

    public static GField[,] ToEncodingMatrix(this IList<EncodedPacket> source)
    {
        if (source == null) throw new ArgumentNullException("source");

        var max = source.Select(l => l.EncodingVector).Max(l => l.Count());
        var result = new GField[source.Count, max];
        for (var i = 0; i < source.Count; i++)
        for (var j = 0; j < source[i].EncodingVector.Count(); j++)
            result[i, j] = source[i].EncodingVector[j];

        return result;
    }

    public static GField[,] ToPayloadMatrix(this IList<EncodedPacket> source)
    {
        if (source == null) throw new ArgumentNullException("source");

        var max = source.Select(l => l.Payload).Max(l => l.Count());
        var result = new GField[source.Count, max];
        for (var i = 0; i < source.Count; i++)
        for (var j = 0; j < source[i].Payload.Count(); j++)
            result[i, j] = source[i].Payload[j];

        return result;
    }
}