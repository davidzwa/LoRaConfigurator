using System.ComponentModel.DataAnnotations;
using LoraGateway.Services.Firmware.RandomLinearCoding;
using Serilog;

namespace LoraGateway.Services.Firmware;

/// <summary>
///     Accepts unencoded packets so the generation size can be applied and encoding is performed to get RLNC encoded
///     packets
/// </summary>
public class RlncEncodingService
{
    /// <summary>
    ///     TODO apply custom size symbol 2,4,8 bits
    /// </summary>
    private const int SymbolSize = 8;

    private LinearFeedbackShiftRegister _generator = new(0x08);

    // Encoding vectors using implicit mode (regeneration on receiving side)
    private List<Generation>? _generations;
    private EncodingConfiguration? _settings;

    public RlncEncodingService()
    {
        ResetEncoding();
    }

    public int CurrentGenerationIndex { get; private set; }

    public int PacketSymbols { get; private set; }

    public byte GetGeneratorState()
    {
        return _generator.State;
    }

    public void ConfigureEncoding(EncodingConfiguration settings)
    {
        _settings = settings;
        _generations = null;
        _generator = new (settings.Seed);
        PacketSymbols = 0;
        CurrentGenerationIndex = 0;
    }

    private void ValidateEncodingConfig()
    {
        if (_settings == null) throw new ValidationException("Encoding settings were not provided");

        if (_settings.GenerationSize > 16)
            throw new ValidationException("Generation size greater than 16 is prohibited");

        if (_settings.GenerationSize == 0) throw new ValidationException("Generation size 0 is prohibited");
    }

    private void ValidateUnencodedPackets(List<UnencodedPacket> unencodedPackets)
    {
        if (unencodedPackets == null || !unencodedPackets.Any())
            throw new ValidationException(
                "No unencoded packets were stored, please store these with StoreUnencodedPackets(.)");

        var biggestPacket = unencodedPackets.Max(p => p.Payload.Count);
        var smallestPacket = unencodedPackets.Min(p => p.Payload.Count);

        if (biggestPacket != smallestPacket)
            throw new ValidationException(
                $"Inconsistent packet length detected across packets. Please pad the packets first. Biggest: {biggestPacket} smallest: {smallestPacket}");
    }

    private void ValidateGenerationsState()
    {
        if (_generations == null || !_generations.Any())
            throw new ValidationException(
                "No generations were prepared, please preprocess these with PreprocessGenerations(.)");

        if (_generations.Count <= CurrentGenerationIndex)
            throw new ValidationException(
                "An error occurred: CurrentGenerationIndex exceeded generations available.");

        // No need to do exhaustion check
    }

    /// <summary>
    ///     Provide all packets to be chunked up in generations, effectively resetting the encoding process
    /// </summary>
    /// <param name="unencodedPackets"></param>
    /// <param name="generationSize"></param>
    /// <exception cref="ValidationException"></exception>
    public int PreprocessGenerations(List<UnencodedPacket> unencodedPackets, uint generationSize)
    {
        ValidateUnencodedPackets(unencodedPackets);

        _settings!.GenerationSize = generationSize;
        ValidateEncodingConfig();

        // Collect the payloads in generation chunks
        _generations = null;
        var generationChunks = unencodedPackets.Chunk((int) _settings!.GenerationSize);

        // This deals with underrun generation size well (f.e. when less packets were chunked than gen size)
        _generations = generationChunks.Select((packetChunk, index) => new Generation
        {
            OriginalPackets = packetChunk.ToList(),
            GenerationIndex = index
        }).ToList();

        // Packet length (bytes) / symbol length (=1-byte)
        PacketSymbols =
            _generations.First().OriginalPackets.First().Payload.Count; // divide by encoding symbol size

        // Reset state
        _generator.Reset();
        CurrentGenerationIndex = 0;
        if (PacketSymbols == 0) throw new ValidationException("PacketSymbols was 0, unencoded packet list was empty");

        return _generations.Count;
    }

    public void ResetEncoding()
    {
        var oldSeed = _generator.Seed;
        ConfigureEncoding(new EncodingConfiguration
        {
            Seed = oldSeed,
            FieldDegree = 8,
            GenerationSize = 0,
            CurrentGeneration = 0
        });

        _generations?.Clear();
        _generator.Reset();
    }
    
    public bool HasNextGeneration()
    {
        return _generations != null && _generations.Count - 1 > CurrentGenerationIndex;
    }

    public void MoveNextGeneration()
    {
        if (HasNextGeneration()) CurrentGenerationIndex++;

        ValidateGenerationsState();
        
        _generator.Reset();
    }

    /// <summary>
    ///     Encoding of exactly the amount of unencoded packets
    /// </summary>
    /// <returns></returns>
    public Generation PrecodeCurrentGeneration(uint precodeExtra)
    {
        ValidateEncodingConfig();
        ValidateGenerationsState();

        var currentGeneration = _generations![CurrentGenerationIndex];
        if (currentGeneration.EncodedPackets.Count > 0)
            throw new ValidationException(
                "Generation already contained encoded packets. In order to prevent inconsistent behaviour please use PrecodeNumberOfPackets with care to not overrun the random number generator");

        var sourcePackets = currentGeneration.OriginalPackets;
        var encodedPackets = sourcePackets.Count + precodeExtra;
        PrecodeNumberOfPackets((uint) encodedPackets, true);

        return currentGeneration;
    }

    public IEncodedPacket GetLastEncodedPacket()
    {
        return _generations!.Last().EncodedPackets.Last();
    }

    public List<IEncodedPacket> PrecodeNumberOfPackets(uint packetCount, bool resetGenerationPackets = false)
    {
        ValidateGenerationsState();

        var currentGeneration = _generations![CurrentGenerationIndex];

        if (resetGenerationPackets) currentGeneration.EncodedPackets = new List<IEncodedPacket>();

        var packetsGenerated = new List<IEncodedPacket>();
        long generatorSamplesTaken = 0;
        foreach (var unused in Enumerable.Range(1, (int) packetCount))
        {
            generatorSamplesTaken += currentGeneration.OriginalPackets.Count;
            if (generatorSamplesTaken >= 256) throw new Exception("LFSR overrun");

            // Array of coeffs used to loop over all symbols and packets
            var encodingCoeffs = _generator
                .GenerateMany(currentGeneration.OriginalPackets.Count)
                .Select(b => new GFSymbol(b))
                .ToList();
            
            Log.Debug("Precode resulted in LFSR state {State}", _generator.State);

            // Generate packet using coefficients
            var nextEncodedPacketIndex = currentGeneration.EncodedPackets.Count;
            var encodedPacket = EncodeNextPacket(encodingCoeffs, nextEncodedPacketIndex);

            // Increments the current encoded packet count automatically - its zero based
            packetsGenerated.Add(encodedPacket);
            currentGeneration.EncodedPackets.Add(encodedPacket);
        }

        return packetsGenerated;
    }

    private EncodedPacket EncodeNextPacket(List<GFSymbol> encodingCoefficients, int currentEncodedPacketIndex)
    {
        // Initiate the output packet vector with capacity equal to known amount of symbols
        var outputElements = Enumerable.Range(0, PacketSymbols).Select(_ => new GFSymbol()).ToList();
        var currentPacketIndex = 0;

        // This performs the core encoding procedure
        foreach (var unencodedPacket in _generations![CurrentGenerationIndex].OriginalPackets!.AsEnumerable())
        {
            // Loop over symbols of U-packet, multiply each with E-symbol, add to outputElements
            foreach (var symbolIndex in Enumerable.Range(0, unencodedPacket.Payload.Count))
            {
                var packetSymbolGalois256 = unencodedPacket.Payload[symbolIndex];
                packetSymbolGalois256 *= encodingCoefficients[currentPacketIndex];
                outputElements[symbolIndex] += packetSymbolGalois256;
            }

            currentPacketIndex++;
        }

        // Result should make sense - decode now?
        return new EncodedPacket
        {
            EncodingVector = encodingCoefficients,
            PacketIndex = currentEncodedPacketIndex,
            Payload = outputElements.Select(g => g).ToList()
        };
    }
}