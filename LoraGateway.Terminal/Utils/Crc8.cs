public static class Crc8
{
    // x8 + x7 + x6 + x4 + x2 + 1
    private const byte poly = 0xd5;

    /// http://sanity-free.org/146/crc8_implementation_in_csharp.html
    private static readonly byte[] table = new byte[256];

    static Crc8()
    {
        for (var i = 0; i < 256; ++i)
        {
            var temp = i;
            for (var j = 0; j < 8; ++j)
                if ((temp & 0x80) != 0)
                    temp = (temp << 1) ^ poly;
                else
                    temp <<= 1;
            table[i] = (byte) temp; // & 0xFF capoff
        }
    }

    public static byte ComputeChecksum(params byte[] bytes)
    {
        byte crc = 0xFF;
        if (bytes != null && bytes.Length > 0)
            foreach (var b in bytes)
                crc = table[crc ^ b];
        return crc;
    }
}