﻿namespace LoraGateway.Services.Firmware.RandomLinearCoding;

public class FragmentWithGenerator
{
    public byte UsedGenerator { get; set; }
    public UInt16 SequenceNumber { get; set; }
    public byte GenerationIndex { get; set; }
    public byte[] Fragment { get; set; }
    
    /*
     * Carries the original packet for self-testing locally
     */
    public IEncodedPacket? OriginalPacket { get; set; }

}