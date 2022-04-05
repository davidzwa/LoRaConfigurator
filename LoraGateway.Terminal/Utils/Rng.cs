namespace LoraGateway.Utils;

public static class Rng
{
    public const long RandLocalMax = 2147483647L;

    static UInt32 next = 1;

    /**
     * Linear Congruential Generator (BSD LCG design)
     */
    public static UInt32 Random32() {
        return (next = (uint)((next * 1103515245L + 12345L) % RandLocalMax));
    }

    public static byte Random8()
    {
        return (byte)(Random32() >> (32 - 8));
    }
    
    public static byte[] GeneratePseudoRandomBytes(int length)
    {
        List<byte> rngBytes = new ();
        var rng = new Random();
        for (int i = 0; i < length; i++)
        {
            rngBytes.Add((byte)rng.Next(0, 256));
        }

        return rngBytes.ToArray();
    }
}