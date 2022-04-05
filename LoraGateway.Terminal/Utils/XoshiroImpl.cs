using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LoraGateway.Utils;

public class XoshiroImpl2
{
    // NextUInt64 is based on the algorithm from http://prng.di.unimi.it/xoshiro256starstar.c:
    //
    //     Written in 2018 by David Blackman and Sebastiano Vigna (vigna@acm.org)
    //
    //     To the extent possible under law, the author has dedicated all copyright
    //     and related and neighboring rights to this software to the public domain
    //     worldwide. This software is distributed without any warranty.
    //
    //     See <http://creativecommons.org/publicdomain/zero/1.0/>.

    private ulong _s0, _s1, _s2, _s3;

    public XoshiroImpl2(ulong[] seed)
    {
        _s0 = seed[0];
        _s1 = seed[1];
        _s2 = seed[2];
        _s3 = seed[3];
        if ((_s0 | _s1 | _s2 | _s3) == 0)
        {
            // at least one value must be non-zero
            throw new InvalidOperationException(
                "Seeds do not OR to non-zero value");
        }
    }

    /// <summary>Produces a value in the range [0, uint.MaxValue].</summary>
    public uint NextUInt32() => (uint)(NextUInt64() >> 32);

    /// <summary>Produces a value in the range [0, ulong.MaxValue].</summary>
    public ulong NextUInt64()
    {
        ulong s0 = _s0, s1 = _s1, s2 = _s2, s3 = _s3;

        ulong result = BitOperations.RotateLeft(s1 * 5, 7) * 9;
        ulong t = s1 << 17;

        s2 ^= s0;
        s3 ^= s1;
        s1 ^= s2;
        s0 ^= s3;

        s2 ^= t;
        s3 = BitOperations.RotateLeft(s3, 45);

        _s0 = s0;
        _s1 = s1;
        _s2 = s2;
        _s3 = s3;

        return result;
    }

    public int Next()
    {
        while (true)
        {
            // Get top 31 bits to get a value in the range [0, int.MaxValue], but try again
            // if the value is actually int.MaxValue, as the method is defined to return a value
            // in the range [0, int.MaxValue).
            ulong result = NextUInt64() >> 33;
            if (result != int.MaxValue)
            {
                return (int)result;
            }
        }
    }

    public void NextBytes(byte[] buffer) => NextBytes((Span<byte>)buffer);

    public void NextBytes(Span<byte> buffer)
    {
        ulong s0 = _s0, s1 = _s1, s2 = _s2, s3 = _s3;

        while (buffer.Length >= sizeof(ulong))
        {
            Unsafe.WriteUnaligned(
                ref MemoryMarshal.GetReference(buffer),
                BitOperations.RotateLeft(s1 * 5, 7) * 9);

            // Update PRNG state.
            ulong t = s1 << 17;
            s2 ^= s0;
            s3 ^= s1;
            s1 ^= s2;
            s0 ^= s3;
            s2 ^= t;
            s3 = BitOperations.RotateLeft(s3, 45);

            buffer = buffer.Slice(sizeof(ulong));
        }

        if (!buffer.IsEmpty)
        {
            ulong next = BitOperations.RotateLeft(s1 * 5, 7) * 9;
            
            byte[] remainingBytes = BitConverter.GetBytes(next);
            Debug.Assert(buffer.Length < sizeof(ulong));
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = remainingBytes[i];
            }

            // Update PRNG state.
            ulong t = s1 << 17;
            s2 ^= s0;
            s3 ^= s1;
            s1 ^= s2;
            s0 ^= s3;
            s2 ^= t;
            s3 = BitOperations.RotateLeft(s3, 45);
        }

        _s0 = s0;
        _s1 = s1;
        _s2 = s2;
        _s3 = s3;
    }
}