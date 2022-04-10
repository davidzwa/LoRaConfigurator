using LoraGateway.Services.Firmware.Packets;
using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Services.Firmware.Utils;

public static class MatrixUtils
{
    public static GFSymbol[] BytesToGfSymbols(this byte[] ba)
    {
        return ba.Select(b => new GFSymbol(b)).ToArray();
    }
    
    public static GFSymbol[,] ToAugmentedMatrix(this IList<IEncodedPacket> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var max = source.Select(l => l.EncodingVector).Max(l => l.Count);
        var maxAugmentation = source.Select(l => l.Payload).Max(l => l.Count);
        var result = new GFSymbol[source.Count, max + maxAugmentation];
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

    public static GFSymbol[,] ToEncodingMatrix<T>(this IList<T> source) where T : IEncodedPacket
    {
        if (source == null) throw new ArgumentNullException("source");

        var max = source.Select(l => l.EncodingVector).Max(l => l.Count());
        var result = new GFSymbol[source.Count, max];
        for (var i = 0; i < source.Count; i++)
        for (var j = 0; j < source[i].EncodingVector.Count; j++)
        {
            result[i, j] = source[i].EncodingVector[j];
        }

        return result;
    }

    public static GFSymbol[,] ToPayloadMatrix(this IList<IPacket> source)
    {
        if (source == null) throw new ArgumentNullException("source");

        var max = source.Select(l => l.Payload).Max(l => l.Count());
        var result = new GFSymbol[source.Count, max];
        for (var i = 0; i < source.Count; i++)
        for (var j = 0; j < source[i].Payload.Count(); j++)
            result[i, j] = source[i].Payload[j];

        return result;
    }

    public static GFSymbol[,] BytesToMatrix(this byte[,] bytes)
    {
        var rowCount = bytes.GetLength(0);
        var colCount = bytes.GetLength(1);
        var matrix = new GFSymbol[rowCount, colCount];

        for (var i = 0; i < rowCount; i++)
        {
            for (var j = 0; j < colCount; j++)
            {
                matrix[i, j] = new GFSymbol(bytes[i, j]);
            }
        }

        return matrix;
    }
}