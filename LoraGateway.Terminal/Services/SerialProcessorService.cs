using System.IO.Ports;
using System.Text;
using Google.Protobuf;
using LoRa;
using LoraGateway.Models;
using LoraGateway.Utils;

namespace LoraGateway.Services;

public partial class SerialProcessorService
{
    private readonly ILogger<SerialProcessorService> _logger;
    private readonly MeasurementsService _measurementsService;
    private readonly SelectedDeviceService _selectedDeviceService;
    private readonly DeviceDataStore _store;
    private readonly byte endByte = 0x00;
    private readonly byte startByte = 0xFF;

    public SerialProcessorService(
        DeviceDataStore store,
        SelectedDeviceService selectedDeviceService,
        MeasurementsService measurementsService,
        ILogger<SerialProcessorService> logger
    )
    {
        _store = store;
        _selectedDeviceService = selectedDeviceService;
        _measurementsService = measurementsService;
        _logger = logger;
    }

    public List<SerialPort> SerialPorts { get; } = new();

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
        port.Handshake = Handshake.RequestToSend;
        port.DtrEnable = true;
        port.RtsEnable = true;
        port.NewLine = "\0";
        port.Encoding = Encoding.UTF8;

        // Set the read/write timeouts
        port.ReadTimeout = 10000;
        port.WriteTimeout = 500;

        port.ErrorReceived += (sender, args) => OnPortError((SerialPort) sender, args);
        port.DataReceived += async (sender, args) => await OnPortData((SerialPort) sender, args);
        try
        {
            port.Open();
            port.BaseStream.Flush();
            _selectedDeviceService.SetPortIfUnset(portName);
        }
        catch (IOException)
        {
            _logger.LogWarning("Device IO error occurred. Retrying once after 2 sec");
            Thread.Sleep(2000);
            port.Open();
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

    public void WriteMessage(UartCommand message)
    {
        var selectedPortName = _selectedDeviceService.SelectedPortName;
        if (selectedPortName == null)
        {
            _logger.LogWarning("Selected port [selected gateway] was not set - check USB connection");
            return;
        }

        var payload = message.ToByteArray();
        var protoMessageBuffer = new[] {(byte) payload.Length}.Concat(payload);
        var messageBuffer = Cobs.Encode(protoMessageBuffer).ToArray();
        var len = new[] {(byte) messageBuffer.Length};
        var transmitBuffer = new[] {startByte}
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

    public void OnPortError(SerialPort subject, SerialErrorReceivedEventArgs e)
    {
        _logger.LogWarning("Serial error {ErrorType}", e.EventType);
    }

    public async Task OnPortData(SerialPort port, SerialDataReceivedEventArgs d)
    {
        if (port.BytesToRead == 0 || d.EventType == SerialData.Eof)
        {
            _logger.LogDebug("EOF");
            return;
        }

        while (port.BytesToRead > 0) await ProcessMessagePreamble(port);
    }

    private async Task ProcessMessagePreamble(SerialPort port)
    {
        var buffer = new List<byte>();
        var packetWaitingBytes = true;
        try
        {
            while (packetWaitingBytes)
            {
                var newByte = (byte) port.ReadByte();
                buffer.Add(newByte);
                packetWaitingBytes = newByte != 0x00;
            }
        }
        catch (TimeoutException)
        {
            _logger.LogInformation("Read timeout");
            return;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Serial closed");
            return;
        }
        catch (InvalidOperationException)
        {
            _logger.LogError("Invalid operation on Serial port. Closing");
            return;
        }
        catch (Exception e)
        {
            _logger.LogError("Serial port error. Closing {Error}", e.Message);
            DisconnectPort(port.PortName);
            DisposePort(port.PortName);
            return;
        }

        if (buffer.Count <= 3)
        {
            _logger.LogWarning("Illegal packet length <3. Skipping");
            return;
        }

        var listBuffer = buffer.ToList();
        var startByteIndex = listBuffer.FindIndex(val => val == 0xFF);
        var dataLength = listBuffer[startByteIndex + 1];
        var hasEnd = listBuffer.FindIndex(val => val == 0x00);

        // Packet bytes: Start, Length, ...Data..., End
        var overhead = 3;
        if (dataLength + overhead < listBuffer.Count)
        {
            _logger.LogWarning("Packet length {DataCount} smaller than buffer length {PacketLength}. Skipping",
                listBuffer.Count, dataLength + overhead);
            return;
        }

        if (startByteIndex > 0) // Clear unknown bytes
            listBuffer.RemoveRange(0, startByteIndex);
        listBuffer.RemoveAt(0);
        listBuffer.RemoveAt(1);
        listBuffer.RemoveAt(listBuffer.Count - 1);

        _logger.LogDebug(
            "Data [{Port}] Bytes [{Bytes}] Expected [{dataLength}] Start [{Start}] End [{End}] LeftOver [{LeftOver}]",
            port.PortName, listBuffer.Count, dataLength, startByteIndex, hasEnd, port.BytesToRead);
        _logger.LogDebug(SerialUtil.ByteArrayToString(listBuffer.ToArray()));

        await ProcessMessage(port.PortName, listBuffer.ToArray());
    }

    private async Task ProcessMessage(string portName, byte[] buffer)
    {
        _logger.LogDebug("Serial decoding COBS buffer ({ByteLength})", buffer.Length);
        var outputBuffer = Cobs.Decode(buffer);
        if (outputBuffer.Count == 0)
        {
            _logger.LogError("COBS output empty {HexString}", SerialUtil.ByteArrayToString(buffer));
            return;
        }

        // Remove COBS overhead of 2
        outputBuffer.RemoveAt(0);
        outputBuffer.RemoveAt(outputBuffer.Count - 1);

        var decodedBuffer = outputBuffer.ToArray();
        UartResponse response;
        try
        {
            response = UartResponse.Parser.ParseFrom(decodedBuffer);
        }
        catch (InvalidProtocolBufferException e)
        {
            _logger.LogInformation(SerialUtil.ByteArrayToString(decodedBuffer));
            _logger.LogError("Protobuf decoding error {Error} - skipping packet", e.Message);
            return;
        }

        var bodyCase = response.BodyCase;
        if (bodyCase.Equals(UartResponse.BodyOneofCase.BootMessage))
        {
            var deviceId = response.BootMessage.DeviceIdentifier.DeviceIdAsString();
            var firmwareVersion = response.BootMessage.GetFirmwareAsString();
            var measurementCount = response.BootMessage.MeasurementCount;
            var measurementDisabled = response.BootMessage.MeasurementsDisabled;
            var device = await _store.GetOrAddDevice(new Device
            {
                Id = deviceId,
                FirmwareVersion = firmwareVersion,
                IsGateway = false,
                LastPortName = portName
            });

            _logger.LogInformation("[{Name}, MC:{Count}, MD:{Disabled}] heart beat {DeviceId}", device?.NickName,
                measurementCount, measurementDisabled, deviceId);
        }
        else if (bodyCase.Equals(UartResponse.BodyOneofCase.AckMessage))
        {
            var ackNumber = response.AckMessage.Code;
            _logger.LogInformation("[{Name}] ACK {Int}", portName, ackNumber);
        }
        else if (bodyCase.Equals(UartResponse.BodyOneofCase.DebugMessage))
        {
            var payload = response.DebugMessage.Payload;
            var code = response.DebugMessage.Code;

            _logger.LogInformation("[{Name}, Debug] {Payload} Code:{Code}", portName, payload.ToStringUtf8(), code);
        }
        else if (bodyCase.Equals(UartResponse.BodyOneofCase.LoraMeasurement))
        {
            if (!response.LoraMeasurement.Success)
            {
                _logger.LogInformation("[{Name}] LoRa RX error!", portName);
                return;
            }

            var snr = response.LoraMeasurement.Snr;
            var rssi = response.LoraMeasurement.Rssi;
            var sequenceNumber = response.LoraMeasurement.SequenceNumber;
            var isMeasurement = response.LoraMeasurement.IsMeasurementFragment;

            var result = await _measurementsService.AddMeasurement(sequenceNumber, snr, rssi);
            if (sequenceNumber > 60000) _measurementsService.SetLocationText("");

            _logger.LogInformation(
                "[{Name}] LoRa RX snr: {SNR} rssi: {RSSI} sequence-id:{Index} is-measurement:{IsMeasurement}, skipped:{Skipped}",
                portName,
                snr, rssi, sequenceNumber, isMeasurement, result);
        }
        else
        {
            _logger.LogInformation("Got an unknown message");
        }
    }

    public void DisposePort(string portName)
    {
        var port = GetPort(portName);
        port?.Dispose();
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
}