using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using LoRa;
using LoraGateway.Services;
using LoraGateway.Utils;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.UartProtocol;

public class UartEncodingTests
{
    readonly IServiceProvider _services = 
        LoraGateway.CreateHostBuilder(new string[] { }).Build().Services;

    readonly byte[] _cobsPayload = new byte[]
    {
        // COBS payload
        0x8,
        0x7, // Protobuf start
        0xA,
        0x3,
        0x31,
        0x32,
        0x33,
        0x2A,
        0x1, // Protobuf end
        0x1, // COBS end
    };
    
    readonly byte[] _cobsPayloadFaulty = new byte[]
    {
        // COBS payloads
        0x7, // Protobuf start
        0xA,
        0x3,
        0x31,
        0x32,
        0x33,
        0x2A,
        0x1, // Protobuf end
        0x1, // COBS end
    };
    
    [Fact]
    public async Task SerialProcessorReceiveDebugTest()
    {
        var serialProcessor = _services.GetRequiredService<SerialProcessorService>();
        var result = await serialProcessor.ProcessMessage("test", _cobsPayload);
        result.ShouldBe(0);
        
        // Wrong protobuf COBS buffer (wrong byte removed)
        var result2 = await serialProcessor.ProcessMessage("test", _cobsPayloadFaulty);
        result2.ShouldBe(2);
    }
    
    [Fact]
    public void UartProtobufDebugMessageDecodingTest()
    {
        var encodedData = new byte[]
            {
                // Start
                0xFF,
                0xA, // Length
            }
            .Concat(_cobsPayload)
            .Concat(
                new byte[]
                {
                    0x0
                }).ToList();

        encodedData.First().ShouldBe(SerialProcessorService.StartByte);
        encodedData.Last().ShouldBe(SerialProcessorService.EndByte);
        encodedData.FindLastIndex(val => val == SerialProcessorService.StartByte).ShouldBe(0);
        encodedData.FindIndex(val => val == SerialProcessorService.StartByte).ShouldBe(0);

        var decodedData = Cobs.Decode(_cobsPayload);
        decodedData.Count.ShouldBe(_cobsPayload.Length - 1);
        decodedData.ShouldBe(new List<byte>()
        {
            0x7,
            0xa,
            0x3,
            0x31,
            0x32,
            0x33,
            0x2a,
            0x0,
            0x0
        });

        decodedData.RemoveAt(0);
        decodedData.RemoveAt(decodedData.Count - 1);
        decodedData.Count.ShouldBe(7);

        var result = UartResponse.Parser.ParseFrom(decodedData.ToArray());
        result.DebugMessage.Code.ShouldBe<uint>(0);
        result.Payload.ShouldBe<ByteString>(ByteString.CopyFromUtf8("123"));
    }
}