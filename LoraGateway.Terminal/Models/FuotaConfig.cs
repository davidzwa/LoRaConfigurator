using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Models;

public class FuotaConfig : ICloneable
{
    // Phy settings
    public int TxPower { get; set; } = 14;
    public uint TxBandwidth { get; set; } = 2; // 0(125k),1(250k),2(500k)
    public uint TxDataRate { get; set; } = 11; // 7-12
    public uint SeqPeriodMs { get; set; } = 2000;
    public uint SeqCount { get; set; } = 10;
    
    // Enabling this makes sure the UART packet are treated are LoRa RX packets instead of forwarding them
    public bool UartFakeLoRaRxMode { get; set; } = true;
    public bool UartFakeAwaitAck { get; set; } = true;
    public string TargetedNickname { get; set; } = "";

    public uint UpdateIntervalMilliSeconds { get; set; } = 500;
    public bool DebugMatrixUart { get; set; } = false;
    public bool DebugFragmentUart { get; set; } = false;
    // Not checked
    public bool FakeFirmware { get; set; } = true;
    // Not implemented
    // public string? FirmwareBinPath { get; set; } = null;
    // Used for fake generation
    public uint FakeFragmentCount { get; set; } = 10;
    // Used for fake generation
    public uint FakeFragmentSize { get; set; } = 10;
    // With all other params will result in GenerationCount
    public uint GenerationSize { get; set; } = 5;
    public uint GenerationSizeRedundancy { get; set; } = 2;
    
    // Encoding related
    // Changing poly of Field is not implemented
    // public int FieldPoly { get; set; }
    // Static degree, might become dynamic later
    public uint FieldDegree { get; set; } = 8;
    
    // Changing poly of LFSR is not seemingly interesting
    // public int LfsrPoly { get; set; }
    // Static seed might change to dynamic later
    public uint LfsrSeed { get; set; } = LinearFeedbackShiftRegister.DefaultSeed;

    public object Clone()
    {
        return MemberwiseClone();
    }
}