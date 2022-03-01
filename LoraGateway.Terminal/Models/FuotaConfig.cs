using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Models;

public class FuotaConfig : ICloneable
{
    // Enabling this makes sure the UART packet are treated are LoRa RX packets instead of forwarding them
    public bool UartFakeLoRaRxMode { get; set; } = true;

    public uint UpdateIntervalSeconds { get; set; } = 5;
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