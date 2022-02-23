using System.Text;
using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Services.Firmware.Utils;

public static class PacketUtils
{
    public static GField[,] ToEncodingMatrix(this IList<EncodedPacket> source)
    {
        if (source == null)
        {
            throw new ArgumentNullException("source");
        }

        int max = source.Select(l => l.EncodingVector).Max(l => l.Count());
        var result = new GField[source.Count, max];
        for (int i = 0; i < source.Count; i++)
        {
            for (int j = 0; j < source[i].EncodingVector.Count(); j++)
            {
                result[i, j] = source[i].EncodingVector[j];
            }
        }

        return result;
    }

    public static GField[,] ToPayloadMatrix(this IList<EncodedPacket> source)
    {
        if (source == null)
        {
            throw new ArgumentNullException("source");
        }

        int max = source.Select(l => l.Payload).Max(l => l.Count());
        var result = new GField[source.Count, max];
        for (int i = 0; i < source.Count; i++)
        {
            for (int j = 0; j < source[i].Payload.Count(); j++)
            {
                result[i, j] = new GField(source[i].Payload[j]);
            }
        }

        return result;
    }

    public static string SerializePacket(this IPacket packet, string prefix = "")
    {
        if (packet.Payload.Length == 0) return "EMPTY";

        StringBuilder hex = new StringBuilder(packet.Payload.Length * 2);
        StringBuilder chars = new StringBuilder(packet.Payload.Length);
        foreach (byte b in packet.Payload)
        {
            hex.AppendFormat("{0:x2}", b);
            if (b == 0)
            {
                chars.Append('0');
            }
            else if (b == 255)
            {
                chars.Append('.');
            }
            else
            {
                chars.Append(Convert.ToChar(b));
            }
        }

        return $"{prefix} [{packet.Payload.Length}b] {hex} {""}\n";
    }

    public static void PrintPackets<T>(this List<T> packets) where T : IPacket
    {
        int count = 0;
        StringBuilder packetsSerializedDebug = new StringBuilder();
        foreach (var packet in packets)
        {
            packetsSerializedDebug.Append(packet.SerializePacket($"Packet {count}"));
            count++;
        }

        // Logging to console is inconsistent with newlines
        Console.Write(packetsSerializedDebug);
        Console.WriteLine("-- End of packets --");
    }
}