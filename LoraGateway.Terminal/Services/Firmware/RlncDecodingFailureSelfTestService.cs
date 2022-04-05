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

    public async Task RunSelfTest()
    {
        var config = await _fuotaManagerService.LoadStore();
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

        var decodedPackets = RlncDecodingService.DecodeGeneration(encodedPackets)
            .Cast<IEncodedPacket>()
            .ToList();
        _logger.LogInformation("{EncodedPackets} Packets decoded", encodedPackets.Count);

        var eSymbolMatrix = encodedPackets.ToEncodingMatrix();
        var eRows = eSymbolMatrix.GetLength(0);
        var eCols = eSymbolMatrix.GetLength(1);
        _logger.LogInformation("Encoded Cols {Cols} Rows {Rows}", eCols, eRows);
        var eMatrixRow = SerialUtil.MatrixToString(eSymbolMatrix);
        _logger.LogInformation("Encoded Enc. Matrix \n{MatrixRow}", eMatrixRow);

        var symbolMatrix = decodedPackets.ToEncodingMatrix();
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
}