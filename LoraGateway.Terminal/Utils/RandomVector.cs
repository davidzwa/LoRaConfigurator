namespace LoraGateway.Utils;

public static class RandomVector
{
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