namespace LoraGateway.Services.Firmware.RandomLinearCoding;

public class LinearFeedbackShiftRegister
{
    /// <summary>
    ///     8-bit fixated LFSR with 8-bit seed as point of entry
    /// </summary>
    /// <param name="seed"></param>
    public LinearFeedbackShiftRegister(byte seed)
    {
        Seed = seed;
        State = seed;
        // Taps = taps;
        Taps = 8; // Hard-coded
        GenerationCount = 0;
    }

    // https://stackoverflow.com/questions/61024861/random-number-using-lfsr
    public byte Seed { get; }
    public byte State { get; private set; }
    public uint Taps { get; }

    public byte Bit { get; private set; }

    public int GenerationCount { get; set; }

    public void Reset()
    {
        GenerationCount = 0;
        State = Seed;
    }

    public IEnumerable<byte> GenerateMany(int count)
    {
        return Enumerable.Range(1, count).Select(_ => Generate());
    }

    public byte Generate()
    {
        if (GenerationCount > Math.Pow(2, Taps) - 1)
            throw new Exception("LFSR cycle limit reached (255), duplicates generated");

        /* Must be 16-bit to allow bit<<15 later in the code */
        /* taps: 16 14 13 11; feedback polynomial: x^16 + x^14 + x^13 + x^11 + 1 */
        // var A = new [] {0, 2, 3, 5}; // 17-bit (degree 16) with 4 entries
        // Bit = (ushort)((State >> A[0]) ^ (State >> A[1]) ^ (State >> A[2]) ^ (State >> A[3])) /* & 1u */;

        /* Must be 8-bit to allow bit<<7 later in the code */
        /* taps: 8 4 3 1; feedback polynomial: x^8 + x^4 + x^3 + x^1 + 1 */
        // var p = new [] {0, 2, 3, 7}; // 4 entries for GF 2^8 
        // 8 6 5 1
        var p = new[] {0, 1, 2, 7};
        Bit = (byte) ((State >> p[0]) ^ (State >> p[1]) ^ (State >> p[2]) ^ (State >> p[3])); /* & 1u */
        State = (byte) ((State >> 1) | (Bit << (int) (Taps - 1))); // Shift the output and cap off

        GenerationCount++;
        return State;
    }
}