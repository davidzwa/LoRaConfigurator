using LoRa;

namespace LoraGateway.Models;

public enum DecodingStatus
{
    Success=0,
    FailCobs=1,
    FailProto=2,
}

public class UartDecodingResultDto
{
    public DecodingStatus DecodingResult { get; set; }
    public UartResponse? Response { get; set; }
}