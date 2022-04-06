namespace LoraGateway.Utils;

public class XoshiroStarStar
{
    public static XoshiroStarStar XoShiRo8 = new(new byte[] { 0x32, 0x33, 0x34, 0x35 });
    
// NextUInt64 is based on the algorithm from http://prng.di.unimi.it/xoshiro256starstar.c:
    //
    //     Written in 2018 by David Blackman and Sebastiano Vigna (vigna@acm.org)
    //
    //     To the extent possible under law, the author has dedicated all copyright
    //     and related and neighboring rights to this software to the public domain
    //     worldwide. This software is distributed without any warranty.
    //
    //     See <http://creativecommons.org/publicdomain/zero/1.0/>.
    
    private const byte _orot = 7;
    private const byte _mult1 = 5;
    private const byte _mult2 = 9;
    private const byte _a = 3;
    private const byte _b = 7;
    
    private byte _s0, _s1, _s2, _s3;

    public XoshiroStarStar(byte[] seed)
    {
        _s0 = seed[0];
        _s1 = seed[1];
        _s2 = seed[2];
        _s3 = seed[3];
        if ((_s0 | _s1 | _s2 | _s3) == 0)
            // at least one value must be non-zero
            throw new InvalidOperationException(
                "Seeds do not OR to non-zero value");
    }
    
    private static byte Rotl(byte x, int k) {
        return (byte)((x << k) | (x >> (8 - k)));
    }

    public byte[] NextBytes(int length)
    {
        return Enumerable.Range(0, length).Select(v => NextByte()).ToArray();
    }
    
    public byte NextByte()
    {
        byte s0 = _s0, s1 = _s1, s2 = _s2, s3 = _s3;
        var state = (byte)(Rotl((byte)(s1 * _mult1), _orot) * _mult2);

        // Advance PRNG state
        var t = (ulong)s1 << _a;
        s2 ^= s0;
        s3 ^= s1;
        s1 ^= s2;
        s0 ^= s3;
        s2 ^= (byte)t;
        s3 = Rotl(s3, _b);
        
        _s0 = s0;
        _s1 = s1;
        _s2 = s2;
        _s3 = s3;

        return state;
    }
}