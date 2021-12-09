using System.IO.Ports;
using LoraGateway.Models;
using LoraGateway.Utils;
using LoraGateway.Utils.COBS;
using Microsoft.Extensions.Logging;

namespace LoraGateway.Services;

public class SerialProcessorService : IDisposable
{
    private readonly DeviceDataStore _store;
    private readonly ILogger<SerialProcessorService> _logger;

    private readonly int maxIdle = 500;
    public BootMessage? LastBootMessage;

    public SerialProcessorService(
        DeviceDataStore store,
        ILogger<SerialProcessorService> logger
    )
    {
        _store = store;
        _logger = logger;
    }

    public SerialPort? SerialPort { get; private set; }

    public void Initialize(string portName)
    {
        // Create a new SerialPort object with default settings.
        SerialPort = new SerialPort();

        // Allow the user to set the appropriate properties.
        SerialPort.PortName = portName;
        SerialPort.BaudRate = 921600;
        SerialPort.Parity = Parity.None;
        SerialPort.DataBits = 8;
        SerialPort.StopBits = StopBits.One;
        SerialPort.Handshake = Handshake.None;

        // Set the read/write timeouts
        SerialPort.ReadTimeout = 10000;
        SerialPort.WriteTimeout = 500;

        SerialPort.Open();
    }


    public async Task WaitMessage(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var port = SerialPort;
            if (port == null) throw new Exception("Serial Port was not created. Call Initialize first.");

            var buffer = await WaitBuffer(cancellationToken);
            // We had an error so we loop to trigger read timeout to start again
            if (buffer == null)
            {
                continue;
            }

            _logger.LogDebug("Serial decoding COBS buffer ({ByteLength})", buffer.Length);
            var outputBuffer = COBS.Decode(buffer);
            if (outputBuffer.Count == 0)
            {
                _logger.LogError("COBS output empty {HexString}", SerialUtil.ByteArrayToString(buffer));
                continue;
            }

            outputBuffer.RemoveAt(0);
            outputBuffer.RemoveAt(outputBuffer.Count - 1);

            var decodedBuffer = outputBuffer.ToArray();
            var response = UartResponse.Parser.ParseFrom(decodedBuffer);
            if (response.BodyCase.Equals(UartResponse.BodyOneofCase.BootMessage))
            {
                LastBootMessage = response.BootMessage;

                var device = await _store.GetOrAddDevice(new Device
                {
                    Id = ConvertDeviceId(LastBootMessage?.DeviceIdentifier),
                    IsGateway = false
                });

                var deviceId = ConvertDeviceId(response.BootMessage.DeviceIdentifier);
                _logger.LogInformation("Device {Name} heart beat {DeviceId}", device.NickName, deviceId);
            }
        }

        Dispose();
    }

    public string ConvertDeviceId(DeviceId? spec)
    {
        if (spec == null) return "";

        return $"{spec.Id0}-{spec.Id1}-{spec.Id2}";
    }

    private async Task<byte[]?> WaitBuffer(CancellationToken cancellationToken)
    {
        if (SerialPort == null) return null;

        try
        {
            var data = SerialPort.ReadByte();
            if (data == 0xFF)
            {
                var dataLength = SerialPort.ReadByte();
                _logger.LogDebug("Serial packet started, expecting {Count} bytes ", dataLength);

                var buffer = new byte[dataLength];
                var currentIdle = 0;
                while (SerialPort.BytesToRead != dataLength || currentIdle > maxIdle)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return null;
                    }

                    currentIdle++;
                    Thread.Sleep(1);
                }

                 await SerialPort.BaseStream.ReadAsync(buffer, 0, dataLength);
                 
                 return buffer;
            }
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Read timeout");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Serial closed");
        }

        return null;
    }

    public void Dispose()
    {
        SerialPort?.Close();
        SerialPort?.Dispose();
    }
}