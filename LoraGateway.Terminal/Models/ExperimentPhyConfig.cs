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

    public uint TransmitStartDelay { get; set; } = 3000;
    public string DeviceTargetNickName { get; set; } = "";
    public bool DeviceIsRemote { get; set; } = false;

    public int[] TxPSeries { get; set; } = { 14, 12, 10, 8 };

    // Phy experiments
    public uint SeqPeriodMs { get; set; } = 75;
    public uint SeqCount { get; set; } = 100;
    public uint[] TxSfSeries { get; set; } = { 10, 9, 8 };

    public uint SeqPeriodMsSlow { get; set; } = 500;
    public uint SeqCountSlow { get; set; } = 50;
    public uint[] TxSfSeriesSlow { get; set; } = { 12, 11 };

    public PhyConfig DefaultPhy { get; set; } = PhyConfig.Default;

    public uint WriteDataCounterDivisor { get; set; } = 20;

    public object Clone()
    {
        return MemberwiseClone();
    }
}