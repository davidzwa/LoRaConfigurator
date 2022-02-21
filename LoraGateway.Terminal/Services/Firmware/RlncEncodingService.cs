﻿using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text;
using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Services.Firmware;

/// <summary>
/// Accepts unencoded packets so the generation size can be applied and encoding is performed to get RLNC encoded packets 
/// </summary>
public class RlncEncodingService
{
    private EncodingConfiguration? _settings;
    private List<UnencodedPacket>? _unencodedPackets;

    // Encoding vectors using implicit mode (regeneration on receiving side)
    private List<Generation>? _generations;
    public int CurrentGenerationIndex { get; private set; }
    readonly LinearFeedbackShiftRegister _generator = new LinearFeedbackShiftRegister(0x08);

    public int PacketSymbols { get; private set; }

    /// <summary>
    /// TODO apply custom size symbol 2,4,8 bits
    /// </summary>
    public const int SymbolSize = 1;

    public RlncEncodingService(uint generationSize)
    {
        ConfigureEncoding(new EncodingConfiguration()
        {
            Seed = 0x08,
            FieldOrder = 8,
            GenerationSize = generationSize,
            CurrentGeneration = 0
        }, new List<UnencodedPacket>());

        ValidateEncodingConfig();
    }

    private void ConfigureEncoding(EncodingConfiguration settings, List<UnencodedPacket> unencodedPackets)
    {
        _unencodedPackets = unencodedPackets;
        _settings = settings;
        _generations = null;
        PacketSymbols = 0;
        CurrentGenerationIndex = 0;
    }

    private void ValidateEncodingConfig()
    {
        if (_settings == null)
        {
            throw new ValidationException("Encoding settings were not provided");
        }

        if (_settings.GenerationSize > 16)
        {
            throw new ValidationException("Generation size greater than 16 is prohibited");
        }

        if (_settings.GenerationSize == 0)
        {
            throw new ValidationException("Generation size 0 is prohibited");
        }
    }

    private void ValidateUnencodedPackets()
    {
        if (_unencodedPackets == null || !_unencodedPackets.Any())
        {
            throw new ValidationException(
                "No unencoded packets were stored, please store these with StoreUnencodedPackets(.)");
        }

        var biggestPacket = _unencodedPackets.Max(p => p.Payload.Length);
        var smallestPacket = _unencodedPackets.Min(p => p.Payload.Length);

        if (biggestPacket != smallestPacket)
        {
            throw new ValidationException(
                $"Inconsistent packet length detected across packets. Please pad the packets first. Biggest: {biggestPacket} smallest: {smallestPacket}");
        }
    }

    private void ValidateGenerationsState()
    {
        if (_generations == null || !_generations.Any())
        {
            throw new ValidationException(
                "No generations were prepared, please preprocess these with PreprocessGenerations(.)");
        }

        // No need to do exhaustion check
    }

    public void PreprocessGenerations(List<UnencodedPacket> unencodedPackets)
    {
        ValidateEncodingConfig();
        _generations = null;

        var generationChunks = unencodedPackets.Chunk((int) _settings!.GenerationSize);
        _generations = generationChunks.Select((val, index) => new Generation()
        {
            OriginalPackets = val.ToList(),
            GenerationIndex = index
        }).ToList();

        _unencodedPackets = unencodedPackets;
        ValidateUnencodedPackets();

        // Packet length (bytes) / symbol length (=1-byte)
        PacketSymbols =
            _generations.First().OriginalPackets.First().Payload.Length; // divide by encoding symbol size
        CurrentGenerationIndex = 0;
        if (PacketSymbols == 0)
        {
            throw new ValidationException("PacketSymbols was 0, unencoded packet list was empty");
        }
    }

    public Generation EncodeNextGeneration()
    {
        ValidateEncodingConfig();
        ValidateUnencodedPackets();
        ValidateGenerationsState();

        var currentGeneration = new Generation();
        currentGeneration.GenerationIndex = CurrentGenerationIndex; // Increment state after

        foreach (var unused in _unencodedPackets!)
        {
            // Array of coeffs used to loop over all symbols and packets
            var encodingCoeffs = _generator
                .GenerateMany(_unencodedPackets!.Count)
                .Select(b => new GField(b))
                .ToList();

            // Generate packet using coefficients
            var encodedPacket = StoreNextGeneratedPacket(encodingCoeffs, currentGeneration.EncodedPackets.Count);

            // Increments the current encoded packet count automatically - its zero based
            currentGeneration.EncodedPackets.Add(encodedPacket);
        }

        CurrentGenerationIndex++;
        currentGeneration.OriginalPackets = _unencodedPackets;
        _generations!.Add(currentGeneration);
        return currentGeneration;
    }

    private EncodedPacket StoreNextGeneratedPacket(List<GField> encodingCoefficients, int currentEncodedPacketIndex)
    {
        // Initiate the output packet vector with capacity equal to known amount of symbols
        var outputElements = Enumerable.Range(0, PacketSymbols).Select((_) => new GField()).ToList();
        var currentPacketIndex = 0;

        // This performs the core encoding procedure
        foreach (var unencodedPacket in _unencodedPackets!.AsEnumerable())
        {
            // Loop over symbols of U-packet, multiply each with E-symbol, add to outputElements
            foreach (var symbolIndex in Enumerable.Range(0, unencodedPacket.Payload.Length))
            {
                var packetSymbolGalois256 = new GField(unencodedPacket.Payload[symbolIndex]);
                packetSymbolGalois256 *= encodingCoefficients[currentPacketIndex];
                outputElements[symbolIndex] += packetSymbolGalois256;
            }

            currentPacketIndex++;
        }

        // Result should make sense? Decode now?
        return new EncodedPacket()
        {
            EncodingVector = encodingCoefficients,
            PacketIndex = currentEncodedPacketIndex,
            Payload = outputElements.Select(g => g.GetValue()).ToArray()
        };
    }
}