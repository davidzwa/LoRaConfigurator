using System.ComponentModel.DataAnnotations;
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
    private List<Generation>? _generations;
    
    public int PacketSymbols { get; private set; }

    public RlncEncodingService()
    {
        ConfigureEncoding(new EncodingConfiguration()
        {
            Seed = 0x08,
            FieldOrder = 8,
            GenerationSize = 16,
            CurrentGeneration = 0
        }, new List<UnencodedPacket>());
    }

    private void ConfigureEncoding(EncodingConfiguration settings, List<UnencodedPacket> unencodedPackets)
    {
        _unencodedPackets = unencodedPackets;
        _settings = settings;
        _generations = null;
        PacketSymbols = 0;
    }

    private void ValidateEncodingConfig()
    {
        if (_settings == null)
        {
            throw new ValidationException("Encoding settings were not provided");
        }
    }

    private void ValidateUnencodedPackets()
    {
        if (_unencodedPackets == null || !_unencodedPackets.Any())
        {
            throw new ValidationException(
                "No unencoded packets were stored, please store these with StoreUnencodedPackets(.)");
        }
    }
    
    private void ValidateGenerationsState()
    {
        if (_generations == null || !_generations.Any())
        {
            throw new ValidationException(
                "No generations were prepared, please preprocess these with PreprocessGenerations(.)");
        }
    }

    public void PreprocessGenerations(List<UnencodedPacket> unencodedPackets)
    {
        ValidateEncodingConfig();
        _generations = null;

        var generationChunks = unencodedPackets.Chunk((int)_settings!.GenerationSize);
        _generations = generationChunks.Select((val, index) => new Generation()
        {
            OriginalPackets = val.ToList(),
            GenerationIndex = index
        }).ToList();
        
        // Packet encoding loop limit
        PacketSymbols = _generations.First().OriginalPackets.First().Payload.Length);
    }

    public void EncodeNextGeneration()
    {
        ValidateEncodingConfig();
        ValidateUnencodedPackets();
        ValidateGenerationsState();

        // GF l-value of degree 8, ensuring static log-table allocation is done beforehand
        // (Note: 16-bit or lower degree only advised! The LUTs can grow in size very quickly with 2^degree)
        var lField = new GField();

        // Encoding vectors using implicit mode (regeneration on receiving side)
        var generator = new LinearFeedbackShiftRegister(0x08);
        var encodingVectors = generator.GenerateMany(_unencodedPackets.Count);

        Enumerable.Range(0, packetSize).Select()
        encodingVectors.Select((encVector, index) =>
        {
            var encodedPacket = new List<byte>();
            _unencodedPackets.Select((fragment, index) =>
            {
                lField.SetValue(fragment.Payload[index]);
                lField.
                return new byte[] { };
            });


            return new EncodedPacket()
            {
                EncodingVector = encVector,
                PacketIndex = index,
            };
        });
    }
}