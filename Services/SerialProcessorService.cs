using System.IO.Ports;
using Google.Protobuf;
using LoraGateway.Models;
using LoraGateway.Utils;

namespace LoraGateway.Services;

public class SerialProcessorService : IDisposable
{
    private readonly ILogger<SerialProcessorService> _logger;
    private readonly SelectedDeviceService _selectedDeviceService;
    private readonly DeviceDataStore _store;
    private readonly byte endByte = 0x00;

    private readonly int maxIdle = 500;
    private readonly byte startByte = 0xFF;
    private BootMessage? _lastBootMessage;

    public SerialProcessorService(
        DeviceDataStore store,
        SelectedDeviceService selectedDeviceService,
        ILogger<SerialProcessorService> logger
    )
    {
        _store = store;
        _selectedDeviceService = selectedDeviceService;
        _logger = logger;
    }

    public List<SerialPort> SerialPorts { get; } = new();

    public void Dispose()
    {
        foreach (var port in SerialPorts) DisposePort(port.PortName);
    }

    public bool HasPort(string portName)
    {
        return SerialPorts.Any(p => p.PortName.Equals(portName));
    }

    public void ConnectPort(string portName)
    {
        var serialPort = SerialPorts.Find(p => p.PortName.Equals(portName));
        if (serialPort != null) return;

        // Create a new SerialPort object with default settings.
        var port = new SerialPort();

        // Allow the user to set the appropriate properties.
        port.PortName = portName;
        port.BaudRate = 921600;
        port.Parity = Parity.None;
        port.DataBits = 8;
        port.StopBits = StopBits.One;
        port.Handshake = Handshake.None;

        // Set the read/write timeouts
        port.ReadTimeout = 10000;
        port.WriteTimeout = 500;

        port.ErrorReceived += OnPortError;
        try
        {
            port.Open();
            _selectedDeviceService.SetPortIfUnset(portName);
        }
        catch (IOException)
        {
            _logger.LogWarning("Device IO error occurred");
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        _logger.LogInformation("Connected to device {PortName}", portName);
        SerialPorts.Add(port);
    }

    public SerialPort? GetPort(string portName)
    {
        return SerialPorts.Find(p => p.PortName.Equals(portName));
    }

    public void DisconnectPort(string portName)
    {
        var serialPort = GetPort(portName);

        // Fallback to other selected port in case this one was used
        var fallbackPort = SerialPorts.Find(p => !p.PortName.Equals(portName));
        _selectedDeviceService.SwitchIfSet(portName, fallbackPort?.PortName);

        serialPort?.Close();
        SerialPorts.Remove(serialPort);
        _logger.LogInformation("Disconnected serial port {PortName}", portName);
    }

    public void OnPortError(object subject, SerialErrorReceivedEventArgs e)
    {
        _logger.LogWarning("Serial error {ErrorType}", e.EventType);
    }

    public void WriteMessage(UartCommand message)
    {
        var selectedPortName = _selectedDeviceService.SelectedPortName;
        if (selectedPortName == null)
        {
            _logger.LogWarning("Cant send as multiple devices are connected and 1 is not selected");
            return;
        }

        var payload = message.ToByteArray();
        var protoMessageBuffer = (new[] {(byte) payload.Length}).Concat(payload);
        var messageBuffer = Cobs.Encode(protoMessageBuffer).ToArray();
        var len = new[] {(byte) messageBuffer.Length};
        byte[] transmitBuffer = new[] {startByte}
            .Concat(len)
            .Concat(messageBuffer)
            .Concat(new[] {endByte})
            .ToArray();

        _logger.LogInformation("TX {Message}", SerialUtil.ByteArrayToString(transmitBuffer));
        var port = GetPort(selectedPortName);
        if (port == null)
        {
            _logger.LogWarning("Port was null. Cant send to {Port}", selectedPortName);
            return;
        }

        port.Write(transmitBuffer, 0, transmitBuffer.Length);
    }

    public async Task MessageProcessor(string portName, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var port = GetPort(portName);
                if (port == null) throw new Exception("Serial Port was not created, call ConnectPort first");

                var buffer = await WaitBuffer(portName, cancellationToken);
                // We had an error so we loop to trigger read timeout to start again
                if (buffer == null) continue;

                _logger.LogDebug("Serial decoding COBS buffer ({ByteLength})", buffer.Length);
                var outputBuffer = Cobs.Decode(buffer);
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
                    _lastBootMessage = response.BootMessage;

                    var deviceId = response.BootMessage.DeviceIdentifier.DeviceIdAsString();
                    var firmwareVersion = response.BootMessage.GetFirmwareAsString();
                    var device = await _store.GetOrAddDevice(new Device
                    {
                        Id = deviceId,
                        FirmwareVersion = firmwareVersion,
                        IsGateway = false,
                        LastPortName = portName
                    });
                    
                    _logger.LogInformation("[{Name}] heart beat {DeviceId}", device?.NickName, deviceId);
                }
            }
        }
        catch (InvalidOperationException)
        {
            _logger.LogError("Invalid operation on Serial port. Closing");
        }

        DisconnectPort(portName);
        DisposePort(portName);
    }

    private async Task<byte[]?> WaitBuffer(string portName, CancellationToken cancellationToken)
    {
        var serialPort = GetPort(portName);
        if (serialPort == null) return null;

        try
        {
            var data = serialPort.ReadByte();
            if (data == 0xFF)
            {
                var dataLength = serialPort.ReadByte();
                _logger.LogDebug("Serial packet started, expecting {Count} bytes ", dataLength);

                var buffer = new byte[dataLength];
                var currentIdle = 0;
                while (serialPort.BytesToRead != dataLength || currentIdle > maxIdle)
                {
                    if (cancellationToken.IsCancellationRequested) return null;

                    currentIdle++;
                    Thread.Sleep(1);
                }

                await serialPort.BaseStream.ReadAsync(buffer, 0, dataLength, cancellationToken);
                return buffer;
            }
        }
        catch (TimeoutException)
        {
            _logger.LogDebug("Read timeout");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Serial closed");
        }

        return null;
    }

    public void DisposePort(string portName)
    {
        var port = GetPort(portName);
        port?.Dispose();
    }
}