﻿namespace LoraGateway.Services.Firmware.RandomLinearCoding;

public class FragmentWithGenerator
{
    public byte UsedGenerator { get; set; }
    public byte SequenceNumber { get; set; }
    public byte GenerationIndex { get; set; }
    public byte[] Fragment { get; set; }

}