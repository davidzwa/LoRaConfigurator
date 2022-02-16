using System.Security.Cryptography;

namespace LoraGateway.Services.Firmware.RandomLinearCoding;

public class Register
{
    private readonly bool[] _feedbackPoints = new bool[256];
    private readonly bool[] _register;
    private readonly int _registerLength;

    public Register(int length, int[] feedbackPoints) : this(length, feedbackPoints, new byte [0])
    {
    }

    public Register(int length, int[] feedbackPoints, byte[] seed)
    {
        if (length > 256)
            throw new ArgumentOutOfRangeException("length", "Alloewed vaues need to be between 1 and 256");

        _registerLength = length;
        _register = new bool[length];

        foreach (var feedbackPoint in feedbackPoints)
            if (feedbackPoint > 256)
                throw new ArgumentOutOfRangeException("feedbackPoints",
                    "Alloewed vaues of item of array need to be between 1 and 256");
            else
                _feedbackPoints[feedbackPoint] = true;


        var randomizedSeed = SeedRandomization(seed);

        var temporaryRegisterRepresantation = string.Empty;
        foreach (var seedItem in randomizedSeed)
            temporaryRegisterRepresantation += Convert.ToString(seedItem, 2);

        var index = 0;
        foreach (var bit in temporaryRegisterRepresantation)
            if (index < length)
                _register[index++] = bit == '1';
    }

    public bool Clock()
    {
        lock (this)
        {
            var output = _register[0];

            for (var index = 0; index < _registerLength - 1; index++)
                _register[index] = _feedbackPoints[index] ? _register[index + 1] ^ output : _register[index + 1];

            _register[_registerLength - 1] = output;

            return output;
        }
    }

    private byte[] SeedRandomization(byte[] inputSeed)
    {
        var sha256 = new SHA256Managed();
        var seedLength = inputSeed.Length;

        var seed = new byte [seedLength];
        Array.Copy(inputSeed, seed, seedLength);


        Array.Resize(ref seed, seedLength + 4);
        var dateTime = BitConverter.GetBytes(DateTime.Now.Ticks);
        seed[seedLength] = dateTime[0];
        seed[seedLength + 1] = dateTime[1];
        seed[seedLength + 2] = dateTime[2];
        seed[seedLength + 3] = dateTime[3];

        return sha256.ComputeHash(seed, 0, seed.Length);
    }
}