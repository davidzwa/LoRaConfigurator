using System.Text;
using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Services.Firmware.Utils;

public static class PacketUtils
{
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
            else {
                chars.Append(Convert.ToChar(b));
            }
        }

        return $"{prefix} [{packet.Payload.Length}b] {hex} {""}\n";
    }

    public static void PrintPackets(this List<IPacket> packets)
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