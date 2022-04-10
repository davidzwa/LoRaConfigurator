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
        // TODO apply CRC in payload
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
        result.Payload.ShouldBe(ByteString.CopyFromUtf8("123"));
    }

    [Fact]
    public void ProtoFailureTest()
    {
        var payload = new byte[]
        {
            0x0a, 0x0d, 0x4c, 0x6f, 0x52, 0x61, 0x4d, 0x75, 0x6c, 0x74, 0x69, 0x63, 0x61, 0x73, 0x74, 0x12, 0x20, 0x0a,
            0x11, 0x08, 0xc0, 0x80, 0x8c, 0x03, 0x10, 0x95, 0xa2, 0xe1, 0xa1, 0x03, 0x18, 0xb9, 0xf0, 0xc8, 0x89, 0x03,
            0x12, 0x06, 0x08, 0x02, 0x18, 0x01, 0x20, 0x0c, 0x18, 0xaf, 0x03, 0x20, 0x01 
            // We noticed the last byte was vital
        };
        
        var result = UartResponse.Parser.ParseFrom(payload);
        result.BodyCase.ShouldBe(UartResponse.BodyOneofCase.BootMessage);
        result.BootMessage.MeasurementsDisabled.ShouldBeTrue();
    }

    [Fact]
    public void ProtoFailure2Test()
    {
        var payload = new byte[]
        {
            0x0a, 0x0d, 0x4c, 0x6f, 0x52, 0x61, 0x4d, 0x75, 0x6c, 0x74, 0x69, 0x63, 0x61, 0x73, 0x74, 0x12, 0x1b, 0x0a, 
            0x11, 0x08, 0xb5, 0x80, 0xb4, 0x02, 0x10, 0x95, 0xa2, 0xe1, 0xa1, 0x03, 0x18, 0xb9, 0xf0, 0xc8, 0x89, 0x03, 
            0x12, 0x06, 0x08, 0x02, 0x18, 0x01, 0x20, 0x0d
        };
        var result = UartResponse.Parser.ParseFrom(payload);
        result.BodyCase.ShouldBe(UartResponse.BodyOneofCase.BootMessage);
        result.BootMessage.MeasurementsDisabled.ShouldBeFalse();
    }

    [Fact]
    public void BootRequestProtoDecode()
    {
        var payload = new byte[]
        {
            0x22, 0x02, 0x08, 0x01
        };
        var result = UartCommand.Parser.ParseFrom(payload);
        result.BodyCase.ShouldBe(UartCommand.BodyOneofCase.RequestBootInfo);
    }

    [Fact]
    public void CobsOutputEmptyTest()
    {
        var cobsEncodedInput = new byte[]
            { 0xc8, 0x89, 0x03, 0x12, 0x06, 0x08, 0x02, 0x18, 0x01, 0x20, 0x0e, 0x18, 0x04, 0x20, 0x01 };

        var output = Cobs.Decode(cobsEncodedInput);
        
        output.Count.ShouldBeGreaterThan(0);
    }
}