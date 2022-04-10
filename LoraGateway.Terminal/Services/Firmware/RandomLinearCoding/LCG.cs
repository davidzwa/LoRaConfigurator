namespace LoraGateway.Services.Firmware.RandomLinearCoding;

/**
 * Linear Congruential Generator (BSD LCG design)
 */
public class LCG
{
    public const long RandLocalMax = 2147483647L;
    UInt32 Seed = 1;
    UInt32 State;

    public LCG()
    {
        Reset();
    }

    public UInt32 Next()
    {
        // Inconsistent with 32-bit architecture C++ implementation?
        return (UInt32)((State = (UInt32)(State * 1103515245L + 12345L)) % RandLocalMax);
    }

    public byte NextByte()
    {
        return (byte)(Next() >> (32 - 8));
    }

    void ResetSeed(UInt32 seed)
    {
        Seed = seed;
        Reset();
    }

    void Reset()
    {
        State = Seed;
    }

    UInt32 GetSeed()
    {
        return Seed;
    }
}