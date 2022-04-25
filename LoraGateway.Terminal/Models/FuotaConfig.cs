using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Models;

public class FuotaConfig : ICloneable
{
    public uint GetGenerationCount()
    {
        return (uint)Math.Ceiling((float)FakeFragmentCount / GenerationSize);
    }

    // Phy settings for Fuota experiments
    public int TxPower { get; set; } = 14;
    public uint TxBandwidth { get; set; } = 2; // 0(125k),1(250k),2(500k)
    public uint TxDataRate { get; set; } = 11; // 7-12

    // Enabling this makes sure the UART packet are treated are LoRa RX packets instead of forwarding them
    public bool UartFakeLoRaRxMode { get; set; } = true;
    public bool UartFakeAwaitAck { get; set; } = false;
    public string TargetedNickname { get; set; } = "";

    public uint RemoteDeviceId0 { get; set; } = 0;
    public bool RemoteIsMulticast { get; set; } = true;
    public uint RemoteUpdateIntervalMs { get; set; } = 500;
    public float ApproxPacketErrorRate { get; set; } = 0.2f;
    public bool OverridePacketErrorSeed { get; set; } = true;
    public bool DropUpdateCommands { get; set; } = false;
    public UInt32 PacketErrorSeed { get; set; } = 1963;
    public uint LocalUpdateIntervalMs { get; set; } = 500;
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
    public UInt32 PRngSeedState { get; set; }

    public object Clone()
    {
        return MemberwiseClone();
    }
}