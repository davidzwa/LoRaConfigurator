using System.ComponentModel.DataAnnotations;
using System.Text;
using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Services.Firmware.Utils;

public static class PacketUtils
{
    public static string SerializePacketDebug(this IPacket packet, string prefix = "")
    {
        if (packet.Payload.Count == 0) return "EMPTY";

        var hex = new StringBuilder(packet.Payload.Count * 2);
        var chars = new StringBuilder(packet.Payload.Count);
        foreach (byte b in packet.Payload)
        {
            hex.AppendFormat("{0:x2}", b);
            if (b == 0)
                chars.Append('0');
            else if (b == 255)
                chars.Append('.');
            else
                chars.Append(Convert.ToChar(b));
        }

        return $"{prefix} [{packet.Payload.Count}b] {hex} {""}\n";
    }

    public static void PrintPackets<T>(this List<T> packets) where T : IPacket
    {
        var count = 0;
        var packetsSerializedDebug = new StringBuilder();
        foreach (var packet in packets)
        {
            packetsSerializedDebug.Append(packet.SerializePacketDebug($"Packet {count}"));
            count++;
        }

        // Logging to console is inconsistent with newlines
        Console.Write(packetsSerializedDebug);
        Console.WriteLine("-- End of packets --");
    }

    public static DecodedPacket ToDecodedPacket(this GFSymbol[,] matrix, int packetRow, int encodingVectorSize,
        int payloadSize)
    {
        if (matrix.GetLength(1) < encodingVectorSize + payloadSize)
            throw new ValidationException("Cant unwrap matrix which does not have this many cols");

        var decodedPacket = new DecodedPacket();
        var i = packetRow;
        for (var j = 0; j < encodingVectorSize + payloadSize; j++)
            if (j < encodingVectorSize)
                decodedPacket.EncodingVector.Add(matrix[i, j]);
            else
                decodedPacket.Payload.Add(matrix[i, j]);

        if (decodedPacket.EncodingVector.Count > packetRow &&
            decodedPacket.EncodingVector[packetRow] == new GFSymbol(0x01) &&
            decodedPacket.EncodingVector.All(v => v == new GFSymbol(0x00) || v == new GFSymbol(0x01)))
            decodedPacket.DecodingSuccess = true;

        if (decodedPacket.EncodingVector.All(v => v == new GFSymbol(0x00))) decodedPacket.IsRedundant = true;

        return decodedPacket;
    }

    public static List<DecodedPacket> ToDecodedPackets(this GFSymbol[,] matrix, int encodingVectorSize, int payloadSize)
    {
        var decodedPackets = new List<DecodedPacket>();
        foreach (var index in Enumerable.Range(0, matrix.GetLength(0)))
            decodedPackets.Add(ToDecodedPacket(matrix, index, encodingVectorSize, payloadSize));

        return decodedPackets;
    }
}