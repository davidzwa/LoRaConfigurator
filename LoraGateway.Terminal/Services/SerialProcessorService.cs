using System.IO.Ports;
using System.Text;
using Google.Protobuf;
using JKang.EventBus;
using LoRa;
using LoraGateway.Handlers;
using LoraGateway.Models;
using LoraGateway.Utils;

namespace LoraGateway.Services;

public partial class SerialProcessorService
{
    private readonly ILogger<SerialProcessorService> _logger;
    private readonly MeasurementsService _measurementsService;
    private readonly SelectedDeviceService _selectedDeviceService;
    private readonly DeviceDataStore _deviceStore;
    private readonly IEventPublisher _eventPublisher;
    public static readonly byte EndByte = 0x00;
    public static readonly byte StartByte = 0xFF;

    public SerialProcessorService(
        DeviceDataStore deviceStore,
        IEventPublisher eventPublisher,
        SelectedDeviceService selectedDeviceService,
        MeasurementsService measurementsService,
        ILogger<SerialProcessorService> logger
    )
    {
        _deviceStore = deviceStore;
        _eventPublisher = eventPublisher;
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
        port.BaudRate = 230400;
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

        var devices = _deviceStore.GetDeviceByPort(portName);
        var names = devices.Select(d => d?.NickName);
        _logger.LogInformation("[{PortName}] Connected to device - {Names}", portName, names);
        SerialPorts.Add(port);
    }

    public SerialPort? GetPort(string portName)
    {
        return SerialPorts.Find(p => p.PortName.Equals(portName));
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
                var newByte = (byte) port.ReadByte();
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
        var decodingResult = DecodeBuffer(portName, buffer);

        if (decodingResult.DecodingResult != DecodingStatus.Success)
        {
            return (int)decodingResult.DecodingResult;
        }

        var response = decodingResult.Response;
        var bodyCase = response!.BodyCase;
        if (bodyCase.Equals(UartResponse.BodyOneofCase.BootMessage))
        {
            await ReceiveBootMessage(portName, response);
        }
        else if (bodyCase.Equals(UartResponse.BodyOneofCase.AckMessage))
        {
            var ackNumber = response.AckMessage.Code;
            _logger.LogInformation("[{Name}] ACK {Int}", portName, ackNumber);
        }
        else if (bodyCase.Equals(UartResponse.BodyOneofCase.DebugMessage))
        {
            return await ReceiveDebugMessage(portName, response);
        }
        else if (bodyCase.Equals(UartResponse.BodyOneofCase.ExceptionMessage))
        {
            ReceiveExceptionMessage(portName, response);
        }
        else if (bodyCase.Equals(UartResponse.BodyOneofCase.DecodingMatrix))
        {
            ReceiveDecodingMatrix(portName, response);
        }
        else if (bodyCase.Equals(UartResponse.BodyOneofCase.DecodingUpdate))
        {
            await ReceiveDecodingUpdate(portName, response);
        }
        else if (bodyCase.Equals(UartResponse.BodyOneofCase.DecodingResult))
        {
            ReceiveDecodingResult(portName, response);
        }
        else if (bodyCase.Equals(UartResponse.BodyOneofCase.LoraMeasurement))
        {
            await ReceiveLoRaMeasurement(portName, response);
        }
        else
        {
            _logger.LogInformation("Got an unknown message");
        }

        _logger.LogDebug("-- [{Port}] MSG RX DONE --", portName);

        return 0;
    }

    private UartDecodingResultDto DecodeBuffer(string portName, byte[] buffer)
    {
        _logger.LogDebug("[{Port}] Serial decoding COBS buffer ({ByteLength})", portName, buffer.Length);
        var outputBuffer = Cobs.Decode(buffer);
        if (outputBuffer.Count == 0)
        {
            _logger.LogError("[{Port}] COBS output empty - input \n\t {HexString}", portName,
                SerialUtil.ByteArrayToString(buffer));
            return new ()
            {
                DecodingResult = DecodingStatus.FailCobs
            };
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
            return new () {
                DecodingResult = DecodingStatus.FailProto
            };
        }
        
        _logger.LogDebug("[{Port}] PROTO SUCCESS Type {Type}", portName, response.BodyCase);

        return new()
        {
            DecodingResult = DecodingStatus.Success,
            Response = response
        };
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