namespace LoraGateway.Models;

public class ExperimentPhyConfig : ICloneable
{
    public class PhyConfig
    {
        public static PhyConfig Default = new() { TxBandwidth = 2, TxPower = 14, TxDataRate = 7 };
        public int TxPower { get; set; } = 14;
        public uint TxBandwidth { get; set; } = 2; // 0(125k),1(250k),2(500k)
        public uint TxDataRate { get; set; } = 7; // 7-12
    }

    public int ExperimentAckTimeout { get; set; } = 100;
    public uint TransmitStartDelay { get; set; } = 3000;
    public string TargetedTransmitterNickname { get; set; } = "";
    public string[] AcksRequired { get; set; } = Array.Empty<string>();

    // Phy experiments
    public uint SeqPeriodMs { get; set; } = 2000;
    public uint SeqCount { get; set; } = 10;
    public uint[] TxBwSeries { get; set; } = { 0, 1, 2 };
    public uint[] TxSfSeries { get; set; } = { 7, 8, 9 };
    public int[] TxPSeries { get; set; } = { 14, 10, 6, 2, -2 };
    public PhyConfig DefaultPhy { get; set; } = PhyConfig.Default;

    public uint WriteDataCounterDivisor { get; set; }

    public object Clone()
    {
        return MemberwiseClone();
    }
}