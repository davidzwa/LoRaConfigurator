using System.Text;
using Google.Protobuf;
using LoRa;
using LoraGateway.Models;
using LoraGateway.Services.Firmware.RandomLinearCoding;
using LoraGateway.Utils;
using Serilog;

namespace LoraGateway.Services.Firmware;

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
        var fragmentWithMeta = _fuotaManagerService.FetchNextRlncPayloadWithGenerator();
        fragmentWithMeta.SequenceNumber = (byte)_packetsGenerated;
        _packetsGenerated++;
        
        var fragmentCommand = GenerateFragmentCommand(fragmentWithMeta);
        AnalyseBlob(fragmentCommand);
        var optimizedFragmentCommand = GenerateOptimizedFragmentCommand(fragmentWithMeta);
        AnalyseOptimalBlob(optimizedFragmentCommand);
        
        var generationUpdateCommand = GenerateGenerationUpdateCommand();
        AnalyseBlob(generationUpdateCommand);
        var terminationCommand = GenerateTerminationCommand();
        AnalyseBlob(terminationCommand);

        WriteDefaultValues();

        Log.Information("Cleanup blob generator");
        await _fuotaManagerService.StopFuotaSession(false);
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
    
    public void WriteDefaultValues()
    {
        using (var stream = File.Open(fileName, FileMode.Create))
        {
            using (var writer = new BinaryWriter(stream, Encoding.BigEndianUnicode, false))
            {
                writer.Write(0xFFFF0000);
                writer.Write(0xFFFFFFFF);
                writer.Write(0xFFFFFFFF);
                writer.Write(0xFFFFFFFF);
                writer.Write((byte)0x01);
                writer.Write((byte)0x02);
                writer.Write((byte)0x03);
                writer.Write((byte)0x04);
                writer.Write((byte)0x05);
                writer.Write((byte)0x06);
                writer.Write((byte)0x07);
                writer.Write((byte)0x08);
                // writer.Write(@"c:\Temp");
                // writer.Write(10);
                // writer.Write(true);
            }
        }
    }
    
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