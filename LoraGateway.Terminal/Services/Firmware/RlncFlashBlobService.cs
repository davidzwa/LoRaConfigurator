using System.ComponentModel.DataAnnotations;
using System.Text;
using Google.Protobuf;
using LoRa;
using LoraGateway.Models;
using LoraGateway.Services.Firmware.RandomLinearCoding;
using LoraGateway.Utils;
using Serilog;
using Shouldly;

namespace LoraGateway.Services.Firmware;

public class RlncGeneration
{
    public List<RlncFlashEncodedFragment> Fragments { get; set; } = new();
    public LoRaMessage UpdateMessage { get; set; }
}

public class RlncFlashBlobService
{
    private readonly FuotaManagerService _fuotaManagerService;
    private int _packetsGenerated = 0;
    private int _bytesWritten = 0;
    public const string FileName = "../../../../rlnc.bin";

    public RlncFlashBlobService(FuotaManagerService fuotaManagerService)
    {
        _fuotaManagerService = fuotaManagerService;
    }

    public async Task GenerateFlashBlob()
    {
        if (_fuotaManagerService.IsFuotaSessionEnabled())
        {
            throw new InvalidOperationException(
                "It is not safe to start blob generation when FuotaManager is currently running");
        }

        // Load config and start unmanaged session
        _packetsGenerated = 0;
        _bytesWritten = 0;
        await _fuotaManagerService.LoadStore();
        await _fuotaManagerService.StartFuotaSession(false);

        var initCommand = GenerateInitCommand(_fuotaManagerService.GetCurrentSession());
        AnalyseBlob(initCommand);
        var terminationCommand = GenerateTerminationCommand();
        AnalyseBlob(terminationCommand);

        List<RlncGeneration> rlncGenerations = new List<RlncGeneration>();
        while (!_fuotaManagerService.IsFuotaSessionDone())
        {
            Log.Information("Next gen started");
            var newRlncGeneration = new RlncGeneration();
            while (!_fuotaManagerService.IsCurrentGenerationComplete())
            {
                var fragmentWithMeta = _fuotaManagerService.FetchNextRlncPayloadWithGenerator(false);
                fragmentWithMeta.SequenceNumber = (byte) _packetsGenerated;
                _packetsGenerated++;
                var optimizedFragmentCommand = GenerateOptimizedFragmentCommand(fragmentWithMeta);
                AnalyseOptimalBlob(optimizedFragmentCommand);

                newRlncGeneration.Fragments.Add(optimizedFragmentCommand);
            }

            if (_fuotaManagerService.IsFuotaSessionDone())
            {
                rlncGenerations.Add(newRlncGeneration);
                break;
            }

            _fuotaManagerService.MoveNextRlncGeneration();
            var generationUpdateCommand = GenerateGenerationUpdateCommand();
            AnalyseBlob(generationUpdateCommand);
            newRlncGeneration.UpdateMessage = generationUpdateCommand;

            rlncGenerations.Add(newRlncGeneration);
        }

        Log.Information("Writing blob with {PacketsGenerated} fragments to file {File} using {Bytes} bytes",
            _packetsGenerated, FileName, _bytesWritten);
        WriteRlncBlob(initCommand, terminationCommand, rlncGenerations);

        Log.Information("Cleanup blob generator");
        await _fuotaManagerService.StopFuotaSession(false);
    }

    public void WriteRlncBlob(
        LoRaMessage initMessage,
        LoRaMessage terminationMessage,
        List<RlncGeneration> generations
    )
    {
        var initCommand = initMessage.ToByteArray();
        var termCommand = terminationMessage.ToByteArray();
        using (var stream = File.Open(FileName, FileMode.Create))
        {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
            {
                var prefix = 0xFFFF0000;
                writer.Write(prefix); // (32 bits) Page header
                Log.Information("Page header {Data}",
                    SerialUtil.ByteArrayToString(BitConverter.GetBytes(prefix)));
                writer.Write(initCommand.Length); // (32 bits) Init Size header 
                Log.Information("Init command header {Data}",
                    SerialUtil.ByteArrayToString(BitConverter.GetBytes(initCommand.Length)));
                writer.Write(termCommand.Length); // (32 bits) Termination Size header
                Log.Information("Term command header {Data}",
                    SerialUtil.ByteArrayToString(BitConverter.GetBytes(termCommand.Length)));
                writer.Write(initCommand);
                Log.Information("Init command {Data}", SerialUtil.ByteArrayToString(initCommand));
                writer.Write(termCommand);
                Log.Information("Term command {Data}", SerialUtil.ByteArrayToString(termCommand));

                int currentGenIndex = 0;
                foreach (var gen in generations)
                {
                    var totalSize = (UInt16) gen.Fragments.Sum(f => f.Payload.Length + f.Meta.Length);
                    byte[] syncHeader = new byte[]
                    {
                        0xFF, 0xFF
                    }.Concat(BitConverter.GetBytes(totalSize)).ToArray();
                    Log.Information("Sync header {Data}", SerialUtil.ByteArrayToString(syncHeader));
                    syncHeader.Length.ShouldBe(4);
                    // TODO check endianness
                    writer.Write(syncHeader);
                    foreach (var encodedFragment in gen.Fragments)
                    {
                        var flashPayload = encodedFragment.Meta.Concat(encodedFragment.Payload).ToArray();
                        writer.Write(flashPayload);
                        Log.Information("Fragment({Size}) {Data}", flashPayload.Length, SerialUtil.ByteArrayToString(
                            flashPayload));
                    }

                    // TODO check endianness
                    if (gen.UpdateMessage == null && currentGenIndex + 1 != generations.Count)
                    {
                        throw new ValidationException("Missing update message in between generations");
                    }

                    if (gen.UpdateMessage != null)
                    {
                        var updateMessage = gen.UpdateMessage.ToByteArray();
                        var updateHeaderSize = updateMessage.Length;
                        byte[] syncUpdateHeader = new byte[]
                        {
                            0xFF, 0xFF
                        }.Concat(BitConverter.GetBytes((UInt16) updateHeaderSize)).ToArray();
                        writer.Write(syncUpdateHeader);
                        Log.Information("Update header {Data}", SerialUtil.ByteArrayToString(syncUpdateHeader));
                    }

                    currentGenIndex++;
                }
            }
        }
    }

    private void AnalyseBlob(LoRaMessage message)
    {
        // var payload = message.Payload;
        var blob = message.ToByteArray();
        _bytesWritten += blob.Length;
        // Log.Information("Encoded payload Size {Size} Proto size {SizeBlob}",
        //     payload.Length,
        //     blob.Length
        //     // SerialUtil.ByteArrayToString(blob)
        // );
    }

    private void AnalyseOptimalBlob(RlncFlashEncodedFragment blob)
    {
        _bytesWritten += blob.Payload.Length + blob.Meta.Length;

        // var proto = blob.ToByteString();
        // Log.Information("Optimized fragment Size {Size} Total {TotalSize}",
        //     payload.Length,
        //     payload.Length + blob.Meta.Length
        //     // proto.Length
        //     // SerialUtil.ByteArrayToString(blob)
        // );
    }

    private LoRaMessage GenerateInitCommand(FuotaSession fuotaSession)
    {
        var config = fuotaSession.Config;
        return new LoRaMessage()
        {
            CorrelationCode = 0,
            RlncInitConfigCommand = new RlncInitConfigCommand
            {
                FieldPoly = GFSymbol.Polynomial,
                FieldDegree = config.FieldDegree,
                TotalFrameCount = config.FakeFragmentCount,
                FrameSize = config.FakeFragmentSize,
                DebugFragmentUart = config.DebugFragmentUart,
                DebugMatrixUart = config.DebugMatrixUart,
                // Calculated value from config store
                GenerationCount = fuotaSession.GenerationCount,
                GenerationSize = config.GenerationSize,
                // Wont send poly as its highly static
                // LfsrPoly = ,
                LfsrSeed = config.LfsrSeed
            }
        };
    }

    private RlncFlashEncodedFragment GenerateOptimizedFragmentCommand(FragmentWithGenerator fragment)
    {
        return new RlncFlashEncodedFragment
        {
            Payload = ByteString.CopyFrom(fragment.Fragment),
            Meta = ByteString.CopyFrom(new[]
            {
                fragment.UsedGenerator, fragment.SequenceNumber, fragment.GenerationIndex
            })
        };
    }

    private LoRaMessage GenerateFragmentCommand(FragmentWithGenerator fragment)
    {
        var message = new LoRaMessage
        {
            CorrelationCode = 0,
            DeviceId = 0,
            IsMulticast = true,
            Payload = ByteString.CopyFrom(fragment.Fragment),
            RlncEncodedFragment = new RlncEncodedFragment()
            {
                LfsrState = fragment.UsedGenerator
            }
        };

        return message;
    }

    private LoRaMessage GenerateGenerationUpdateCommand()
    {
        return new LoRaMessage
        {
            CorrelationCode = 0x01,
            DeviceId = 0x01,
            IsMulticast = true,
            RlncStateUpdate = new RlncStateUpdate
            {
                GenerationIndex = 0x23, // TODO
                LfsrState = 0x01 // TODO
            }
        };
    }

    private LoRaMessage GenerateTerminationCommand()
    {
        return new LoRaMessage
        {
            CorrelationCode = 0x01,
            DeviceId = 0x01,
            IsMulticast = true,
            RlncTerminationCommand = new RlncTerminationCommand()
        };
    }
}