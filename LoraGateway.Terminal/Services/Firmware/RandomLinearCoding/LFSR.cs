namespace LoraGateway.Services.Firmware.RandomLinearCoding;

public class LinearFeedbackShiftRegister
{
    // https://stackoverflow.com/questions/61024861/random-number-using-lfsr
    public ushort State { get; private set; }

    /* Must be 16-bit to allow bit<<15 later in the code */
    /* taps: 16 14 13 11; feedback polynomial: x^16 + x^14 + x^13 + x^11 + 1 */
    public ushort Bit { get; private set; }

    public int GenerationCount { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="seed"></param>
    public LinearFeedbackShiftRegister(ushort seed)
    {
        State = seed;
        GenerationCount = 0;
    }

    public IEnumerable<uint> GenerateMany(int count)
    {
        return Enumerable.Range(1, count).Select((_) => Generate());
    }

    public uint Generate()
    {
        if (GenerationCount > (Math.Pow(2, 16) - 1))
        {
            throw new Exception("LFSR cycle limit reached (65535), duplicates generated");
        }

        Bit = (ushort)((State >> 0) ^ (State >> 2) ^ (State >> 3) ^ (State >> 5)) /* & 1u */;
        State = (ushort)((State >> 1) | (Bit << 15));

        GenerationCount++;
        return State;
    }
}