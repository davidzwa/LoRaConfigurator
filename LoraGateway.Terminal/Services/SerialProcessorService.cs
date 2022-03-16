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
    private readonly DeviceDataStore _deviceStore;
    public static readonly byte EndByte = 0x00;
    public static readonly byte StartByte = 0xFF;

    public SerialProcessorService(
        DeviceDataStore deviceStore,
        SelectedDeviceService selectedDeviceService,
        MeasurementsService measurementsService,
        ILogger<SerialProcessorService> logger
    )
    {
        _deviceStore = deviceStore;
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

        port.ErrorReceived += (sender, args) => OnPortError((SerialPort)sender, args);
        port.DataReceived += async (sender, args) => await OnPortData((SerialPort)sender, args);
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

        var devices = _deviceStore.GetDeviceByPort(portName);
        var names = devices.Select(d => d?.NickName);
        _logger.LogInformation("[{PortName}] Connected to device - {Names}", portName, names);
        SerialPorts.Add(port);
    }

    public SerialPort? GetPort(string portName)
    {
        return SerialPorts.Find(p => p.PortName.Equals(portName));
    }

    public void SendBootCommand(bool doNotProxy, string? portName = null)
    {
        var command = new UartCommand
        {
            DoNotProxyCommand = doNotProxy,
            RequestBootInfo = new RequestBootInfo { Request = true }
        };
        WriteMessage(command, portName);
    }

    public void WriteMessage(UartCommand message, string? portName = null)
    {
        var selectedPortName = _selectedDeviceService.SelectedPortName;
        if (portName != null)
        {
            selectedPortName = portName;
        }

        if (selectedPortName == null)
        {
            throw new InvalidOperationException("Selected port was not set - check USB connection");
        }

        message.Crc8 = Crc8.ComputeChecksum(payload);
        
        var payload = message.ToByteArray();
        var protoMessageBuffer = new[] { (byte)payload.Length }.Concat(payload);
        var messageBuffer = Cobs.Encode(protoMessageBuffer).ToArray();
        var len = new[] { (byte)messageBuffer.Length };
        var transmitBuffer = new[] { StartByte }
            .Concat(len)
            .Concat(messageBuffer)
            .Concat(new[] { EndByte })
            .ToArray();

        _logger.LogDebug("[{Port}] TRANSMIT {Message}", selectedPortName, SerialUtil.ByteArrayToString(transmitBuffer));
        var port = GetPort(selectedPortName);
        if (port == null)
        {
            _logger.LogWarning("[{Port}] Port was null. Cant send", selectedPortName);
            return;
        }

        port.Write(transmitBuffer, 0, transmitBuffer.Length);
    }

    public void OnPortError(SerialPort subject, SerialErrorReceivedEventArgs e)
    {
        _logger.LogWarning("[{Port}] Serial error {ErrorType}", e.EventType, subject.PortName);
    }

    public async Task OnPortData(SerialPort port, SerialDataReceivedEventArgs d)
    {
        if (port.BytesToRead == 0 || d.EventType == SerialData.Eof)
        {
            _logger.LogDebug("[{Port}] EOF", port.PortName);
            return;
        }

        _logger.LogDebug("[{Port}] PORT DATA {Len}", port.PortName, port.BytesToRead);
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
                var newByte = (byte)port.ReadByte();
                buffer.Add(newByte);
                // If End byte is spotted we break
                packetWaitingBytes = newByte != 0x00;
            }
        }
        catch (TimeoutException)
        {
            _logger.LogInformation("[{Port}] Read timeout", port.PortName);
            return;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[{Port}] Serial closed", port.PortName);
            return;
        }
        catch (InvalidOperationException)
        {
            _logger.LogError("[{Port}] Invalid operation on Serial port. Closing", port.PortName);
            return;
        }
        catch (Exception e)
        {
            _logger.LogError("[{Port}] Serial port error. Closing {Error}", e.Message, port.PortName);
            DisconnectPort(port.PortName);
            DisposePort(port.PortName);
            return;
        }

        if (buffer.Count <= 3)
        {
            _logger.LogWarning("[{Port}] Illegal packet length <3. Skipping", port.PortName);
            return;
        }

        var listBuffer = buffer.ToList();
        var startByteIndex = listBuffer.FindIndex(val => val == 0xFF);
        var dataLength = listBuffer[startByteIndex + 1];
        var endIndex = listBuffer.FindIndex(val => val == 0x00);
        _logger.LogDebug(
            "-- [{Port}] MSG RX BUFFER {RawLen} Start {StartByteIndex} DataLen {DataLen} EndFound {HasEnd}",
            port.PortName,
            listBuffer.Count, startByteIndex, dataLength, endIndex);

        // Packet bytes: Start, Length, ...Data..., End
        var overhead = 3;
        if (dataLength + overhead < listBuffer.Count)
        {
            _logger.LogWarning("[{Port}] Packet length {DataCount} smaller than buffer length {PacketLength}. Skipping",
                port.PortName,
                listBuffer.Count, dataLength + overhead);
            return;
        }

        if (startByteIndex > 0) // Clear unknown bytes
            listBuffer.RemoveRange(0, startByteIndex);
        listBuffer.RemoveAt(0); // Start Byte
        listBuffer.RemoveAt(0); // UART Length Byte
        listBuffer.RemoveAt(listBuffer.Count - 1); // End Byte

        var payload = SerialUtil.ByteArrayToString(listBuffer.ToArray());
        _logger.LogDebug(
            "[{Port}] PRE-COBS {Data} of {Bytes} bytes, left-over {LeftOver}, PAYLOAD\n\t{Payload}",
            port.PortName, dataLength, listBuffer.Count, port.BytesToRead, payload);

        await ProcessMessage(port.PortName, listBuffer.ToArray());
    }

    public async Task<int> ProcessMessage(string portName, byte[] buffer)
    {
        _logger.LogDebug("[{Port}] Serial decoding COBS buffer ({ByteLength})", portName, buffer.Length);
        var outputBuffer = Cobs.Decode(buffer);
        if (outputBuffer.Count == 0)
        {
            _logger.LogError("[{Port}] COBS output empty - input \n\t {HexString}", portName, SerialUtil.ByteArrayToString(buffer));
            return 1;
        }

        // Remove COBS overhead of 2
        outputBuffer.RemoveAt(0);

        _logger.LogDebug("[{Port}] POST-COBS \n\t{Message}", portName,
            SerialUtil.ByteArrayToString(outputBuffer.ToArray()));

        var decodedBuffer = outputBuffer.ToArray();
        UartResponse response;
        try
        {
            response = UartResponse.Parser.ParseFrom(decodedBuffer);
        }
        catch (InvalidProtocolBufferException e)
        {
            _logger.LogError("[{Port}] PROTO ERROR decoding error {Error} - skipping packet \n\t {Payload}", portName,
                e.Message, SerialUtil.ByteArrayToString(decodedBuffer));
            return 2;
        }

        _logger.LogDebug("[{Port}] PROTO SUCCESS Type {Type}", portName, response.BodyCase);

        var bodyCase = response.BodyCase;
        if (bodyCase.Equals(UartResponse.BodyOneofCase.BootMessage))
        {
            var deviceFullId = response.BootMessage.DeviceIdentifier;
            var deviceId = deviceFullId.DeviceIdAsString();
            var firmwareVersion = response.BootMessage.GetFirmwareAsString();
            var measurementCount = response.BootMessage.MeasurementCount;
            var measurementDisabled = response.BootMessage.MeasurementsDisabled;
            var device = await _deviceStore.GetOrAddDevice(new Device
            {
                HardwareId = deviceId,
                Id = deviceFullId.Id0,
                FirmwareVersion = firmwareVersion,
                LastPortName = portName
            });

            _logger.LogInformation("[{Port} {Name}, MC:{Count}, MD:{Disabled}] heart beat {DeviceId}", portName,
                device?.NickName,
                measurementCount, measurementDisabled, deviceId);
        }
        else if (bodyCase.Equals(UartResponse.BodyOneofCase.AckMessage))
        {
            var ackNumber = response.AckMessage.Code;
            _logger.LogInformation("[{Name}] ACK {Int}", portName, ackNumber);
        }
        else if (bodyCase.Equals(UartResponse.BodyOneofCase.DebugMessage))
        {
            var payload = response.Payload;
            var code = response.DebugMessage.Code;

            _logger.LogInformation("[{Name}, Debug] {Payload} Code:{Code}", portName, payload.ToStringUtf8(), code);
        }
        else if (bodyCase.Equals(UartResponse.BodyOneofCase.ExceptionMessage))
        {
            var payload = response.Payload;
            var code = response.DebugMessage.Code;

            _logger.LogError("[{Name}, Exception] {Payload} Code:{Code}", portName, payload.ToStringUtf8(), code);
        }
        else if (bodyCase.Equals(UartResponse.BodyOneofCase.DecodingResult))
        {
            var decodingResult = response.DecodingResult;
            var success = decodingResult.Success;
            _logger.LogInformation(
                "[{Name}, DecodingResult] Success: {Payload} Rank: {MatrixRank} FirstNumber: {FirstNumber} LastNumber: {LastNumber}",
                portName,
                success,
                decodingResult.MatrixRank,
                decodingResult.FirstDecodedNumber,
                decodingResult.LastDecodedNumber
            );
        }
        else if (bodyCase.Equals(UartResponse.BodyOneofCase.LoraMeasurement))
        {
            if (!response.LoraMeasurement.Success)
            {
                _logger.LogInformation("[{Name}] LoRa RX error!", portName);
                return 3;
            }

            var snr = response.LoraMeasurement.Snr;
            var rssi = response.LoraMeasurement.Rssi;
            var sequenceNumber = response.LoraMeasurement.SequenceNumber;
            var isMeasurement = response.LoraMeasurement.IsMeasurementFragment;

            var result = await _measurementsService.AddMeasurement(sequenceNumber, snr, rssi);
            if (sequenceNumber > 60000) _measurementsService.SetLocationText("");

            LoRaPacketHandler(response?.LoraMeasurement?.DownlinkPayload);
            
            // Debug for now
            _logger.LogInformation(
                "[{Name}] LoRa RX snr: {SNR} rssi: {RSSI} sequence-id:{Index} is-measurement:{IsMeasurement}, skipped:{Skipped}",
                portName,
                snr, rssi, sequenceNumber, isMeasurement, result);
        }
        else
        {
            _logger.LogInformation("Got an unknown message");
        }

        _logger.LogDebug("-- [{Port}] MSG RX DONE --", portName);

        return 0;
    }

    private void LoRaPacketHandler(LoRaMessage? message)
    {
        if (message == null) return;
        
        if (message.BodyCase ==LoRaMessage.BodyOneofCase.ExperimentResponse)
        {
            var flashMeasureCount = message.ExperimentResponse.MeasurementCount;
           _logger.LogInformation("Flash {FlashMeasureCount}", flashMeasureCount); 
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