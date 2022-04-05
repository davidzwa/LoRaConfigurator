using LoraGateway.Services.Firmware.RandomLinearCoding;
using LoraGateway.Services.Firmware.Utils;
using LoraGateway.Utils;

namespace LoraGateway.Services.Firmware;

/**
 * A service to run the current RLNC configuration through a codec self test
 * Main purpose: validate encoding vector uniqueness and decoding failure probability
 */
public class RlncDecodingFailureSelfTestService
{
    private readonly ILogger<RlncDecodingFailureSelfTestService> _logger;
    private readonly FuotaManagerService _fuotaManagerService;

    public RlncDecodingFailureSelfTestService(
        ILogger<RlncDecodingFailureSelfTestService> logger,
        FuotaManagerService fuotaManagerService
    )
    {
        _logger = logger;
        _fuotaManagerService = fuotaManagerService;
    }

    private byte[] GeneratePseudoRandomBytes(uint length)
    {
        List<byte> rngBytes = new ();
        var rng = new Random();
        for (int i = 0; i < length; i++)
        {
            rngBytes.Add((byte)rng.Next(0, 256));
        }

        return rngBytes.ToArray();
    }
    
    public async Task RunSelfTest()
    {
        var byteValue = new Random().Next(0, 256);
        _fuotaManagerService.SetLfsrSeed((byte)byteValue);
        
        var config = await _fuotaManagerService.LoadStore();
        var rngBytes = GeneratePseudoRandomBytes(config.GenerationSize);
        
        
        await _fuotaManagerService.StartFuotaSession(false);

        var generationFragments = new List<FragmentWithGenerator>();
        while (!_fuotaManagerService.IsCurrentGenerationComplete())
        {
            var wireFragment = _fuotaManagerService.FetchNextRlncPayloadWithGenerator();
            generationFragments.Add(wireFragment);
        }

        var encodedPackets = generationFragments.Select(f => f.OriginalPacket).ToList();
        _logger.LogInformation("{EncodedPackets} Packets encoded", encodedPackets.Count);

        // Simulate droppage
        // encodedPackets.RemoveAt(7);

        // Add packet which is totally fine
        var dummyPacket = new EncodedPacket()
        {
            Payload = (new byte[10] { 0, 0, 0, 0x08, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }).BytesToGfSymbols().ToList(),
            EncodingVector = (new byte[9] { 0, 0, 0, 0, 0, 0, 0, 0, 1 }).BytesToGfSymbols().ToList(),
            PacketIndex = 8
        };
        encodedPackets.Add(dummyPacket);
        
        var decodedPackets = RlncDecodingService.DecodeGeneration(encodedPackets)
            .Cast<IEncodedPacket>()
            .ToList();
        _logger.LogInformation("{EncodedPackets} Packets decoded", encodedPackets.Count);

        var eSymbolMatrix = encodedPackets.ToEncodingMatrix();
        PrintMatrixSize(eSymbolMatrix);
        var eMatrixRow = SerialUtil.MatrixToString(eSymbolMatrix);
        _logger.LogInformation("Encoded Enc. Matrix \n{MatrixRow}", eMatrixRow);

        var symbolMatrix = decodedPackets.ToEncodingMatrix();
        PrintMatrixSize(symbolMatrix);

        var rowsDecode1 = symbolMatrix.GetLength(0);
        var colsDecode1 = symbolMatrix.GetLength(1);
        var matrixRowDecode1 = SerialUtil.MatrixToString(symbolMatrix);
        var successDecode1 = rowsDecode1 == config.GenerationSize;
        _logger.LogInformation("Decoded Matrix (Success: {Success}, Rank: {Rank} vs {GenSize}) \n{MatrixRow}", 
            successDecode1, 
            colsDecode1,
            config.GenerationSize,
            matrixRowDecode1);

        var roundTwoDecode = RlncDecodingService.DecodeGeneration(decodedPackets);
        PrintMatrixSize(roundTwoDecode.ToEncodingMatrix());
        var rows = symbolMatrix.GetLength(0);
        var cols = symbolMatrix.GetLength(1);
        _logger.LogInformation("Decoded Cols {Cols} Rows {Rows}", cols, rows);
        var matrixRow = SerialUtil.MatrixToString(symbolMatrix);
        var success = rows == config.GenerationSize;
        _logger.LogInformation("Decoded Matrix (Success: {Success}, Rank: {Rank} vs {GenSize}) \n{MatrixRow}", 
            success, 
            rows,
            config.GenerationSize,
            matrixRow);
        
        await _fuotaManagerService.StopFuotaSession(false);
    }

    private void PrintMatrixSize(GFSymbol[,] gfSymbols)
    {
        var rows = gfSymbols.GetLength(0);
        var cols = gfSymbols.GetLength(1);
        _logger.LogInformation("Decoded Cols {Cols} Rows {Rows}", cols, rows);
    }
}