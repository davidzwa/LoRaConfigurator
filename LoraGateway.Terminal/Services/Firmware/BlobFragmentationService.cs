using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using LoraGateway.Services.Firmware.LoRaPhy;
using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Services.Firmware;

/// <summary>
/// Preprocessor - fragment any firmware blob including zeropadding
/// </summary>
public class BlobFragmentationService
{
    [StructLayout(LayoutKind.Explicit)]
    struct IntByte
    {
        [FieldOffset(0)] public int IntVal;
        [FieldOffset(0)] public byte Byte0;
        [FieldOffset(1)] public byte Byte1;
        [FieldOffset(2)] public byte Byte2;
        [FieldOffset(3)] public byte Byte3;
    }

    public BlobFragmentationService()
    {
    }

    /// <summary>
    /// Fakes firmware by repeating 1 to N in the payloads
    /// Might consider adding 32-bit CRC at the end
    /// </summary>
    /// <param name="firmwareSize"></param>
    /// <param name="packetSize"></param>
    public List<UnencodedPacket> GenerateFakeFirmware(long firmwareSize, int packetSize)
    {
        if (packetSize > LoRaWanTimeOnAir.PayloadMax)
        {
            throw new ValidationException(
                "Required fragmentation payload size exceeds the LoRaWAN max packet size of 22");
        }

        var packetCount = (int)Math.Ceiling((double) firmwareSize / packetSize);
        return Enumerable
            .Range(0, packetCount)
            .Select((index) =>
            {
                var splitInt = new IntByte {IntVal = index};
                byte[] payloadBytes = Enumerable.Repeat((byte) 0xFF, packetSize).ToArray();
                payloadBytes[0] = splitInt.Byte3;
                payloadBytes[1] = splitInt.Byte2;
                payloadBytes[2] = splitInt.Byte1;
                payloadBytes[3] = splitInt.Byte0;
                return new UnencodedPacket() {PacketIndex = index, Payload = payloadBytes};
            })
            .ToList();
    }

    // public bool CompareFragmentSequences(List<UnencodedPacket> a, List<UnencodedPacket> b) { }
}