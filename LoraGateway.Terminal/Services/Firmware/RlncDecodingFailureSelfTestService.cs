using LoraGateway.Services.Firmware.Packets;
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
    private readonly Random rng = new();

    public RlncDecodingFailureSelfTestService(
        ILogger<RlncDecodingFailureSelfTestService> logger,
        FuotaManagerService fuotaManagerService
    )
    {
        _logger = logger;
        _fuotaManagerService = fuotaManagerService;
    }

    public async Task RunSelfTest()
    {
        // List<bool> resultsXoshiro = new List<bool>();
        // List<bool> resultsLfsr = new List<bool>();
        List<bool> resultsXoshiro8 = new List<bool>();
        for (int i = 0; i < 1000; i++)
        {
            // var resultLfsr = await RunSelfTestRound(RlncEncodingService.RandomGeneratorType.Lfsr);
            // resultsLfsr.Add(resultLfsr);
            // var resultXoshiro = await RunSelfTestRound(RlncEncodingService.RandomGeneratorType.System);
            // resultsXoshiro.Add(resultXoshiro);
            _fuotaManagerService.SetPrngSeed((uint)rng.Next());
            var resultXoshiro8 = await RunSelfTestRound(RlncEncodingService.RandomGeneratorType.XoShiRoStarStar8);
            resultsXoshiro8.Add(resultXoshiro8);
        }

        // var successSystem = resultsXoshiro.Count(b => b);
        // var failedSystem = resultsXoshiro.Count(b => !b);
        // var totalSystem = resultsXoshiro.Count;
        // _logger.LogInformation("Result Xoshiro Success {Succeeded} vs Failed {Failed} out of {Total} Total",
        //     successSystem,
        //     failedSystem,
        //     totalSystem);
        //
        // var successLfsr = resultsLfsr.Count(b => b);
        // var failedLfsr = resultsLfsr.Count(b => !b);
        // var totalLfsr = resultsLfsr.Count;
        // _logger.LogInformation("Results LFSR Success {Succeeded} vs Failed {Failed} out of {Total} Total",
        //     successLfsr,
        //     failedLfsr,
        //     totalLfsr);
        
        var successXoShiro8 = resultsXoshiro8.Count(b => b);
        var failedXoShiro8 = resultsXoshiro8.Count(b => !b);
        var totalXoShiro8 = resultsXoshiro8.Count;
        _logger.LogInformation("Results XoShiro8 Success {Succeeded} vs Failed {Failed} out of {Total} Total",
            successXoShiro8,
            failedXoShiro8,
            totalXoShiro8);
    }

    public async Task<bool> RunSelfTestRound(RlncEncodingService.RandomGeneratorType prngType)
    {
        // if (prngType == RlncEncodingService.RandomGeneratorType.Lfsr)
        // {
        //     _fuotaManagerService.SetPrngSeed((byte)rng.Next(1, 256));
        // }

        var config = await _fuotaManagerService.LoadStore();
        _fuotaManagerService.SetGeneratorType(prngType);

        await _fuotaManagerService.StartFuotaSession(false);
        var generationFragments = new List<FragmentWithSeed>();
        while (!_fuotaManagerService.IsCurrentGenerationComplete())
        {
            var wireFragment = _fuotaManagerService.FetchNextRlncPayloadWithGenerator();
            generationFragments.Add(wireFragment);
        }

        // INPUT
        var encodedPackets = generationFragments.Select(f => f.OriginalPacket).ToList();
        // _logger.LogDebug("{EncodedPackets} Packets encoded", encodedPackets.Count);

        // Simulate droppage
        // encodedPackets.RemoveAt(7);

        // Add packet which is totally fine
        // var dummyPacket = new EncodedPacket()
        // {
        //     Payload = (new byte[10] { 0, 0, 0, 0x08, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }).BytesToGfSymbols().ToList(),
        //     EncodingVector = (new byte[9] { 0, 0, 0, 0, 0, 0, 0, 0, 1 }).BytesToGfSymbols().ToList(),
        //     PacketIndex = 8
        // };
        // encodedPackets.Add(dummyPacket);

        var decodedPackets = RlncDecodingService.DecodeGeneration(encodedPackets)
            .Cast<IEncodedPacket>()
            .ToList();
        // _logger.LogInformation("{EncodedPackets} Packets decoded", encodedPackets.Count);

        // var eSymbolMatrix = encodedPackets.ToEncodingMatrix();
        // PrintMatrixSize(eSymbolMatrix);
        // var eMatrixRow = SerialUtil.MatrixToString(eSymbolMatrix);
        // _logger.LogInformation("Encoded Enc. Matrix \n{MatrixRow}", eMatrixRow);

        // var lastPacket = decodedPackets.Last();
        // var encVector = lastPacket.EncodingVector;
        // var lastSymb = encVector.Last();
        // var success = lastSymb.Equals(GFSymbol.Unity);
        var symbolMatrix = decodedPackets.ToEncodingMatrix();
        // PrintMatrixSize(symbolMatrix);

        var rowsDecode1 = symbolMatrix.GetLength(0);
        var colsDecode1 = symbolMatrix.GetLength(1);
        var matrixRowDecode1 = SerialUtil.MatrixToString(symbolMatrix);
        var successDecode1 = rowsDecode1 == config.GenerationSize;
        _logger.LogDebug("Decoded Matrix (Success: {Success}, Rank: {Rank} vs {GenSize})",
            successDecode1,
            colsDecode1,
            config.GenerationSize);

        await _fuotaManagerService.StopFuotaSession(false);

        return successDecode1;
    }

    private void PrintMatrixSize(GFSymbol[,] gfSymbols)
    {
        var rows = gfSymbols.GetLength(0);
        var cols = gfSymbols.GetLength(1);
        _logger.LogInformation("Decoded Cols {Cols} Rows {Rows}", cols, rows);
    }
}