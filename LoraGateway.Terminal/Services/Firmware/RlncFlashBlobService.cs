using System.Text;
using Google.Protobuf;
using LoRa;
using LoraGateway.Models;
using LoraGateway.Services.Firmware.RandomLinearCoding;
using Serilog;
using Shouldly;

namespace LoraGateway.Services.Firmware;

public class RlncGeneration
{
    public List<RlncFlashEncodedFragment> Fragments { get; set; }
    public LoRaMessage UpdateMessage { get; set; }
}

public class RlncFlashBlobService
{
    private readonly FuotaManagerService _fuotaManagerService;
    private int _packetsGenerated = 0;

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
        await _fuotaManagerService.LoadStore();
        await _fuotaManagerService.StartFuotaSession(false);

        var initCommand = GenerateInitCommand(_fuotaManagerService.GetCurrentSession());
        AnalyseBlob(initCommand);
        var terminationCommand = GenerateTerminationCommand();
        AnalyseBlob(terminationCommand);

        List<RlncGeneration> rlncGenerations = new List<RlncGeneration>();
        while (!_fuotaManagerService.IsFuotaSessionDone())
        {
            var newRlncGeneration = new RlncGeneration();
            while (!_fuotaManagerService.IsCurrentGenerationComplete())
            {
                var fragmentWithMeta = _fuotaManagerService.FetchNextRlncPayloadWithGenerator();
                fragmentWithMeta.SequenceNumber = (byte)_packetsGenerated;
                _packetsGenerated++;
                // var fragmentCommand = GenerateFragmentCommand(fragmentWithMeta);
                // AnalyseBlob(fragmentCommand);
                var optimizedFragmentCommand = GenerateOptimizedFragmentCommand(fragmentWithMeta);
                AnalyseOptimalBlob(optimizedFragmentCommand);
                
                newRlncGeneration.Fragments.Add(optimizedFragmentCommand);
            }

            _fuotaManagerService.MoveNextRlncGeneration();
            var generationUpdateCommand = GenerateGenerationUpdateCommand();
            AnalyseBlob(generationUpdateCommand);
            newRlncGeneration.UpdateMessage = generationUpdateCommand;
        }

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
        using (var stream = File.Open(fileName, FileMode.Create))
        {
            using (var writer = new BinaryWriter(stream, Encoding.BigEndianUnicode, false))
            {
                writer.Write(0xFFFF0000); // (32 bits) Page header
                writer.Write(initCommand.Length); // (32 bits) Init Size header 
                writer.Write(termCommand.Length); // (32 bits) Termination Size header
                writer.Write(initCommand);
                writer.Write(termCommand);

                foreach (var gen in generations)
                {
                    var totalSize = (UInt32)gen.Fragments.Sum(f => f.Payload.Length + f.Meta.Length);
                    byte[] syncHeader = new byte[]{
                        0xFF, 0xFF
                    }.Concat(BitConverter.GetBytes(totalSize)).ToArray();
                    syncHeader.Length.ShouldBe(4);
                    // TODO check endianness
                    writer.Write(syncHeader);
                    foreach(var encodedFragment in gen.Fragments)
                    {
                        writer.Write(
                            encodedFragment.Meta.Concat(encodedFragment.Payload).ToArray()
                        );
                    }

                    // TODO check endianness
                    var updateMessage = gen.UpdateMessage.ToByteArray();
                    var updateHeaderSize = updateMessage.Length;
                    byte[] syncUpdateHeader = new byte[]{
                        0xFF, 0xFF
                    }.Concat(BitConverter.GetBytes(updateHeaderSize)).ToArray();
                    writer.Write(syncUpdateHeader);
                }
            }
        }
    }

    private void AnalyseBlob(LoRaMessage message)
    {
        var payload = message.Payload;
        var blob = message.ToByteArray();
        Log.Information("Encoded payload Size {Size} Proto size {SizeBlob}",
            payload.Length,
            blob.Length
            // SerialUtil.ByteArrayToString(blob)
        );
    }

    const string fileName = "../../../../rlnc.bin";
    

    
    private void AnalyseOptimalBlob(RlncFlashEncodedFragment blob)
    {
        var payload = blob.Payload;
        var proto = blob.ToByteString();

        Log.Information("Optimized fragment Size {Size} Total {TotalSize} Proto size {SizeBlob}",
            payload.Length,
            payload.Length + blob.Meta.Length,
            proto.Length
            // SerialUtil.ByteArrayToString(blob)
        );
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