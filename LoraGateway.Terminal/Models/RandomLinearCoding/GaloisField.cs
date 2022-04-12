namespace LoraGateway.Services.Firmware.RandomLinearCoding;

/// <summary>
///     Finite field arithmetic using Galois Field
///     https://github.com/fauzanhilmi/GaloisField/tree/master/FiniteField
/// </summary>
public class GFSymbol
{
    public const int Order = 256; // 2 ^ 8;

    // https://www.partow.net/programming/polynomials/index.html#deg16
    // Irreducible polynomial used : x^8 + x^4 + x^3 + x^2 + 1 (0x11D)
    public const int Polynomial = 0x11D;

    // Generator to be used in Exp & Log table generation
    public const byte Generator = 0x2;
    
    // Some reused symbols
    public static GFSymbol Nil = new(0);
    public static GFSymbol Unity = new(1);
    
    // Stored tables
    public static readonly byte[] Exp;
    public static readonly byte[] Log;

    private byte _value;

    // Generates Exp & Log table for fast multiplication operator
    static GFSymbol()
    {
        Exp = new byte[Order];
        Log = new byte[Order];

        byte val = 0x01;
        for (var i = 0; i < Order; i++)
        {
            Exp[i] = val;
            if (i < Order - 1) Log[val] = (byte) i;

            val = Multiply(Generator, val);
        }
    }

    public GFSymbol()
    {
        _value = 0;
    }

    public GFSymbol(byte value)
    {
        _value = value;
    }

    public int[] GetIrreduciblePolynomial()
    {
        return new[]
        {
            //  8  7  6  5  4  3  2  1  0
            1, 0, 0, 0, 1, 1, 1, 0, 1
        };
    }

    // getters and setters
    public byte GetValue()
    {
        return _value;
    }

    public void SetValue(byte value)
    {
        _value = value;
    }

    //operators
    public static explicit operator GFSymbol(byte b)
    {
        var f = new GFSymbol(b);
        return f;
    }

    public static explicit operator byte(GFSymbol f)
    {
        return f._value;
    }

    public static GFSymbol operator +(GFSymbol fieldA, GFSymbol fieldB)
    {
        var bResidue = (byte) (fieldA._value ^ fieldB._value);
        return new GFSymbol(bResidue);
    }

    public static GFSymbol operator -(GFSymbol fieldA, GFSymbol fieldB)
    {
        var bResidue = (byte) (fieldA._value ^ fieldB._value);
        return new GFSymbol(bResidue);
    }

    public static GFSymbol operator *(GFSymbol fieldA, GFSymbol fieldB)
    {
        var fieldTemp = new GFSymbol(0);
        if (fieldA._value != 0 && fieldB._value != 0)
        {
            var fieldLog = (byte) ((Log[fieldA._value] + Log[fieldB._value]) % (Order - 1));
            fieldLog = Exp[fieldLog];
            fieldTemp._value = fieldLog;
        }

        return fieldTemp;
    }

    public static GFSymbol operator /(GFSymbol fieldA, GFSymbol fieldB)
    {
        if (fieldB._value == 0) throw new ArgumentException("Divisor cannot be 0", "fieldB");

        var fieldTemp = new GFSymbol(0);
        if (fieldA._value != 0)
        {
            var fieldTempValue = (byte) ((Order - 1 + Log[fieldA._value] - Log[fieldB._value]) % (Order - 1));
            fieldTempValue = Exp[fieldTempValue];
            fieldTemp._value = fieldTempValue;
        }

        return fieldTemp;
    }

    public static GFSymbol Pow(GFSymbol f, byte exp)
    {
        var fieldTemp = new GFSymbol(1);
        for (byte i = 0; i < exp; i++) fieldTemp *= f;

        return fieldTemp;
    }

    public static bool operator ==(GFSymbol fieldA, GFSymbol fieldB)
    {
        return fieldA._value == fieldB._value;
    }

    public static bool operator !=(GFSymbol fieldA, GFSymbol fieldB)
    {
        return fieldA._value != fieldB._value;
    }

    public override bool Equals(object? obj)
    {
        if (obj == null) return false;

        var field = obj as GFSymbol;
        if ((object) field == null) return false;

        return _value == field._value;
    }

    public override int GetHashCode()
    {
        return _value;
    }

    public override string ToString()
    {
        return _value.ToString();
    }

    // Multiplication method which is only used in Exp & Log table generation
    // implemented with Russian Peasant Multiplication algorithm
    private static byte Multiply(byte a, byte b)
    {
        byte result = 0;
        var aa = a;
        var bb = b;
        while (bb != 0)
        {
            if ((bb & 1) != 0) result ^= aa;

            var highestBit = (byte) (aa & 0x80);
            aa <<= 1;
            if (highestBit != 0) aa ^= Polynomial & 0xFF;

            bb >>= 1;
        }

        return result;
    }
}