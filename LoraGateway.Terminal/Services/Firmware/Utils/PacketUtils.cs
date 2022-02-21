using System.Text;
using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Services.Firmware.Utils;

public static class PacketUtils
{
    public static void PrintPacket(this UnencodedPacket packet)
    {
        if (packet.Payload.Length == 0) return;

        StringBuilder hex = new StringBuilder(packet.Payload.Length * 2);
        StringBuilder chars = new StringBuilder(packet.Payload.Length * 2);
        foreach (byte b in packet.Payload)
        {
            hex.AppendFormat("{0:x2}", b);
            if (b == 0x255)
            {
                chars.Append('.');
            } 
            else {
            chars.Append(Convert.ToChar(b));
            }
        }

        Console.WriteLine("Packet [{0}] >{1}< >{2}<", packet.Payload.Length, hex, chars);
    }
}