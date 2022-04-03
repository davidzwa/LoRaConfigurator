namespace LoraGateway.Services.Firmware.LoRaPhy;

public class LoRaTimeOnAirSpecs
{
    public int SF { get; set; }
    public int CR { get; set; }
    public int BW { get; set; }
    public int PreSymb { get; set; }
    public int PayloadMax { get; set; }
    public int PayloadSize { get; set; }
    public bool ImplicitHeader { get; set; }
    public bool LowDataRateOptimization { get; set; }
    public double TimeSymbol { get; set; }
    public double TimePreamble { get; set; }
    public long PayloadSymbDiff { get; set; }
    public long PayloadSymbNumber { get; set; }
    public double TimePayload { get; set; }
    public double TimePacket { get; set; }
    public double TimeOverhead { get; set; }
    public double OverheadRatio { get; set; }
    public double TimePhyOverhead { get; set; }
    public double PhyOverheadRatio { get; set; }
}

/// <summary>
///     Nice Time-on-air calculation
///     https://www.rfwireless-world.com/calculators/LoRaWAN-Airtime-calculator.html
/// </summary>
public static class LoRaWanTimeOnAir
{
    public static readonly int SF = 7;
    public static readonly int CR = 1;
    public static readonly int BW = 500 * (int) Math.Pow(10, 3);
    public static readonly int PreSymb = 8;

    public static readonly int PayloadMax = 22; // bytes

    public static LoRaTimeOnAirSpecs GetTimeOnAir(int pSize, bool implHdr = false, bool lowDr = false)
    {
        var tSymb = Math.Pow(2, SF) / BW;
        var tPreamble = (PreSymb + 4.25) * tSymb;

        var numerator = 8 * pSize - 4 * SF + 28 + 16 - 20 * Convert.ToUInt16(implHdr);
        var denominator = 4 * (SF - 2 * Convert.ToUInt16(lowDr));
        var payloadSymb = (long) Math.Ceiling((double) numerator / denominator) * (CR + 4);
        var payloadSymbNb = 8 + Math.Max(payloadSymb, 0);

        var tPayload = payloadSymbNb * tSymb;
        var tPacket = tPreamble + tPayload;

        return new LoRaTimeOnAirSpecs
        {
            BW = BW,
            CR = CR,
            ImplicitHeader = implHdr,
            LowDataRateOptimization = lowDr,
            PreSymb = PreSymb,
            PayloadMax = PayloadMax,
            SF = SF,

            PayloadSize = pSize,
            TimeSymbol = tSymb,
            PayloadSymbDiff = payloadSymb,
            PayloadSymbNumber = payloadSymbNb,

            TimePreamble = tPreamble,
            TimePacket = tPacket,
            TimePayload = tPayload,
            TimeOverhead = tPacket - tPayload,
            OverheadRatio = (tPacket - tPayload) / tPacket,
            TimePhyOverhead = tPreamble + tPacket - tPayload,
            PhyOverheadRatio = (tPreamble + tPacket - tPayload) / tPacket
        };
    }
}