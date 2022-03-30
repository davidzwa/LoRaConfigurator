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
    public bool ShouldUpdateAfter { get; set; }
}

public class RlncFlashBlobService
{
    private readonly FuotaManagerService _fuotaManagerService;
    private int _packetsGenerated;
    private int _bytesWritten;
    public const string Formatter = "*!*";
    public const string FileName = "../../../../*!*.bin";

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

        var session = _fuotaManagerService.GetCurrentSession();
        var config = session.Config;

        var initCommand = GenerateInitCommand(session);
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
                if (_packetsGenerated > Math.Pow(2, 16))
                {
                    throw new OverflowException("Byte cannot fit more than 2^16 sequence numbers without overrun");
                }

                fragmentWithMeta.SequenceNumber = (UInt16)_packetsGenerated;
                _packetsGenerated++;
                var optimizedFragmentCommand = GenerateOptimizedFragmentCommand(fragmentWithMeta);
                AnalyseOptimalBlob(optimizedFragmentCommand);

                newRlncGeneration.Fragments.Add(optimizedFragmentCommand);
            }

            if (_fuotaManagerService.IsFuotaSessionDone())
            {
                newRlncGeneration.ShouldUpdateAfter = false;
                rlncGenerations.Add(newRlncGeneration);
                break;
            }

            _fuotaManagerService.MoveNextRlncGeneration();
            newRlncGeneration.ShouldUpdateAfter = true;

            rlncGenerations.Add(newRlncGeneration);
        }

        var fragCount = config.FakeFragmentCount;
        var fragSize = config.FakeFragmentSize;
        var genCount = rlncGenerations.Count;
        var redCount = config.GenerationSizeRedundancy;
        var templateFileName = $"rlnc_{fragCount}f_{fragSize}b_fake_{genCount}g_{redCount}r";
        var fullFileName = FileName.Replace(Formatter, templateFileName);
        Log.Information("Writing blob with {PacketsGenerated} fragments to file {File} using {Bytes} bytes",
            _packetsGenerated, fullFileName, _bytesWritten);
        WriteRlncBlob(fullFileName, initCommand, terminationCommand, rlncGenerations);

        Log.Information("Cleanup blob generator");
        await _fuotaManagerService.StopFuotaSession(false);
    }

    public void WriteRlncBlob(
        string fullFileName,
        LoRaMessage initMessage,
        LoRaMessage terminationMessage,
        List<RlncGeneration> generations
    )
    {
        var initCommand = initMessage.ToByteArray();
        var termCommand = terminationMessage.ToByteArray();
        using (var stream = File.Open(fullFileName, FileMode.Create))
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
                    var totalSize = (UInt16)gen.Fragments.Sum(f => f.Payload.Length + f.Meta.Length);
                    byte[] syncHeader = new byte[]
                    {
                        0xFF, 0xFF
                    }.Concat(BitConverter.GetBytes(totalSize).Reverse()).ToArray();
                    Log.Information("Sync header {Data}", SerialUtil.ByteArrayToString(syncHeader));
                    syncHeader.Length.ShouldBe(4);
                    writer.Write(syncHeader);

                    foreach (var encodedFragment in gen.Fragments)
                    {
                        var flashPayload = encodedFragment.Meta.Concat(encodedFragment.Payload).ToArray();
                        writer.Write(flashPayload);
                        Log.Information("Fragment({Size}) {Data}", flashPayload.Length, SerialUtil.ByteArrayToString(
                            flashPayload));
                    }

                    if (gen.ShouldUpdateAfter == false && currentGenIndex + 1 != generations.Count)
                    {
                        throw new ValidationException("Missing update message in between generations");
                    }

                    currentGenIndex++;

                    if (gen.ShouldUpdateAfter)
                    {
                        byte[] syncUpdateHeader = new byte[]
                        {
                            0xFF, 0xFF
                        }.Concat(BitConverter.GetBytes((UInt16)currentGenIndex).Reverse()).ToArray();
                        writer.Write(syncUpdateHeader);
                        Log.Information("Update header {Data}", SerialUtil.ByteArrayToString(syncUpdateHeader));
                    }
                }
            }
        }
    }

    private void AnalyseBlob(LoRaMessage message)
    {
        var blob = message.ToByteArray();
        _bytesWritten += blob.Length;
    }

    private void AnalyseOptimalBlob(RlncFlashEncodedFragment blob)
    {
        _bytesWritten += blob.Payload.Length + blob.Meta.Length;
    }

    private LoRaMessage GenerateInitCommand(FuotaSession fuotaSession)
    {
        var config = fuotaSession.Config;
        return new()
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
                GenerationRedundancySize = config.GenerationSizeRedundancy,
                // Wont send poly as its highly static
                // LfsrPoly = ,
                LfsrSeed = config.LfsrSeed,
                ReceptionRateConfig = new()
                {
                    PacketErrorRate = config.ApproxPacketErrorRate,
                    DropUpdateCommands = config.DropUpdateCommands,
                    OverrideSeed = config.OverridePacketErrorSeed,
                    Seed = config.PacketErrorSeed
                }
            }
        };
    }

    private RlncFlashEncodedFragment GenerateOptimizedFragmentCommand(FragmentWithGenerator fragment)
    {
        var fragmentBytesWithMeta = new[]
            {
                fragment.UsedGenerator, fragment.GenerationIndex
            }
            .Concat(BitConverter.GetBytes(fragment.SequenceNumber).Reverse()).ToArray();

        return new RlncFlashEncodedFragment
        {
            Payload = ByteString.CopyFrom(fragment.Fragment),
            Meta = ByteString.CopyFrom(fragmentBytesWithMeta)
        };
    }

    private LoRaMessage GenerateTerminationCommand()
    {
        return new LoRaMessage
        {
            IsMulticast = true,
            RlncTerminationCommand = new RlncTerminationCommand()
        };
    }
}