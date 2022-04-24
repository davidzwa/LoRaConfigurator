namespace LoraGateway.Models;

public class ExperimentPhyConfig : ICloneable
{
    public class PhyConfig
    {
        public static PhyConfig Default = new() { TxBandwidth = 2, TxPower = 14, TxDataRate = 11 }; 
        public int TxPower { get; set; } = 14;
        public uint TxBandwidth { get; set; } = 2; // 0(125k),1(250k),2(500k)
        public uint TxDataRate { get; set; } = 11; // 7-12
    }
    public int ExperimentAckTimeout { get; set; } = 100;
    public string TargetedReceiverNickname { get; set; } = "";

    // Phy experiments
    public uint SeqPeriodMs { get; set; } = 2000;
    public uint SeqCount { get; set; } = 10;
    public uint[] TxBwSeries { get; set; } = { 0, 1, 2 };
    public uint[] TxSfSeries { get; set; } = { 7, 8, 9 };
    public int[] TxPSeries { get; set; } = { 14, 10, 6, 2, -2 };
    public PhyConfig DefaultPhy { get; set; } = PhyConfig.Default;

    public object Clone()
    {
        return MemberwiseClone();
    }
}