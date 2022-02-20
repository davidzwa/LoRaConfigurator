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

    private int ValidateGenerationSize(long firmwareSize, int frameSize)
    {
        if (frameSize < 1)
        {
            throw new ValidationException("Illegal frameSize of 0 specified");
        }

        if (frameSize > LoRaWanTimeOnAir.PayloadMax)
        {
            throw new ValidationException(
                "Required fragmentation payload size exceeds the LoRaWAN max packet size of 22");
        }

        if (firmwareSize == 0)
        {
            throw new ValidationException("Firmware size specified was 0");
        }

        var fragmentCount = (int)Math.Ceiling((double)firmwareSize / frameSize);
        if (fragmentCount == 0)
        {
            throw new ValidationException("Fragment count would be 0 which is illegal");
        }

        if (fragmentCount > 5000)
        {
            throw new ValidationException($"Fragment count of {fragmentCount} exceeded maximum tolerable");
        }

        return fragmentCount;
    }

    /// <summary>
    /// Fakes firmware by repeating 1 to N in the payloads
    /// Might consider adding 32-bit CRC at the end
    /// </summary>
    /// <param name="firmwareSize"></param>
    /// <param name="frameSize"></param>
    public List<UnencodedPacket> GenerateFakeFirmware(long firmwareSize, int frameSize)
    {
        var fragmentCount = ValidateGenerationSize(firmwareSize, frameSize);

        return Enumerable
            .Range(0, fragmentCount)
            .Select((index) =>
            {
                var splitInt = new IntByte { IntVal = index };
                byte[] payloadBytes = Enumerable.Repeat((byte)0xFF, frameSize).ToArray();
                if (payloadBytes.Length > 0)payloadBytes[0] = splitInt.Byte3;
                if (payloadBytes.Length > 1) payloadBytes[1] = splitInt.Byte2;
                if (payloadBytes.Length > 2) payloadBytes[2] = splitInt.Byte1;
                if (payloadBytes.Length > 3) payloadBytes[3] = splitInt.Byte0;
                return new UnencodedPacket() { PacketIndex = index, Payload = payloadBytes };
            })
            .ToList();
    }

    // public bool CompareFragmentSequences(List<UnencodedPacket> a, List<UnencodedPacket> b) { }
}