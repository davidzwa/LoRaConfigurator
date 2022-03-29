using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using LoraGateway.Services.Firmware.LoRaPhy;
using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Services.Firmware;

/// <summary>
///     Preprocessor - fragment any firmware blob including zeropadding
/// </summary>
public class BlobFragmentationService
{
    private int ValidateGenerationSize(long firmwareSize, int frameSize)
    {
        if (frameSize < 1) throw new ValidationException("Illegal frameSize of 0 specified");

        if (frameSize > LoRaWanTimeOnAir.PayloadMax)
            throw new ValidationException(
                "Required fragmentation payload size exceeds the LoRaWAN max packet size of 22");

        if (firmwareSize == 0) throw new ValidationException("Firmware size specified was 0");

        var fragmentCount = (int) Math.Ceiling((double) firmwareSize / frameSize);
        if (fragmentCount == 0) throw new ValidationException("Fragment count would be 0 which is illegal");

        if (fragmentCount > 5000)
            throw new ValidationException($"Fragment count of {fragmentCount} exceeded maximum tolerable");

        return fragmentCount;
    }

    /// <summary>
    ///     Fakes firmware by repeating 1 to N in the payloads
    ///     Might consider adding 32-bit CRC at the end
    /// </summary>
    /// <param name="firmwareSize"></param>
    /// <param name="frameSize"></param>
    public async Task<List<UnencodedPacket>> GenerateFakeFirmwareAsync(long firmwareSize, int frameSize)
    {
        var fragmentCount = ValidateGenerationSize(firmwareSize, frameSize);

        return Enumerable
            .Range(0, fragmentCount)
            .Select(index =>
            {
                var splitInt = new IntByte {IntVal = index};
                var payloadBytes = Enumerable.Repeat(new GFSymbol(0xFF), frameSize).ToList();
                if (payloadBytes.Count > 0) payloadBytes[0] = new GFSymbol(splitInt.Byte3);
                if (payloadBytes.Count > 1) payloadBytes[1] = new GFSymbol(splitInt.Byte2);
                if (payloadBytes.Count > 2) payloadBytes[2] = new GFSymbol(splitInt.Byte1);
                if (payloadBytes.Count > 3) payloadBytes[3] = new GFSymbol(splitInt.Byte0);

                return new UnencodedPacket {Payload = payloadBytes};
            })
            .ToList();
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct IntByte
    {
        [FieldOffset(0)] public int IntVal;
        [FieldOffset(0)] public readonly byte Byte0;
        [FieldOffset(1)] public readonly byte Byte1;
        [FieldOffset(2)] public readonly byte Byte2;
        [FieldOffset(3)] public readonly byte Byte3;
    }
}