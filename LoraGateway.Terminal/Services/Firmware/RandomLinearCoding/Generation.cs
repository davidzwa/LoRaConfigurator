﻿namespace LoraGateway.Services.Firmware.RandomLinearCoding;

public class Generation
{
    public int GenerationIndex { get; set; }
    public List<IPacket> OriginalPackets { get; set; } = new ();
    public List<IPacket> EncodedPackets { get; set; } = new ();
}