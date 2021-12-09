using System.IO.Ports;
using LoraGateway.Utils;
using LoraGateway.Utils.COBS;

namespace LoraGateway.Services;

public class SerialProcessorService
{
    public bool Continue = false;
    public BootMessage? LastBootMessage;
    private readonly int maxIdle = 500;

    public SerialPort? SerialPort { get; private set; }

    public Thread Initialize(string portName)
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
        SerialPort.ReadTimeout = 100;
        SerialPort.WriteTimeout = 500;

        SerialPort.Open();

        return new Thread(WaitPacket);
    }
    
    public string ConvertDeviceId(DeviceId spec)
    {
        return $"{spec.Id0}-{spec.Id1}-{spec.Id2}";
    }

    public void WaitPacket()
    {
        while (Continue)
            try
            {
                var port = SerialPort;
                if (port == null)
                {
                    throw new Exception("Serial Port was not created. Call Initialize first.");
                }
                
                var data = port.ReadByte();
                if (data == 0xFF)
                {
                    var dataLength = port.ReadByte();
                    Console.Write($"\nReceiving {dataLength} bytes ");

                    var buffer = new byte[dataLength];
                    var currentIdle = 0;
                    while (port.BytesToRead != dataLength || currentIdle > maxIdle)
                    {
                        currentIdle++;
                        Thread.Sleep(1);
                    }

                    port.BaseStream.ReadAsync(buffer, 0, dataLength);
                    Console.WriteLine($"done ({buffer.Length})");

                    var outputBuffer = COBS.Decode(buffer);
                    if (outputBuffer.Count == 0)
                    {
                        Console.WriteLine($"COBS output empty {SerialUtil.ByteArrayToString(buffer)}");
                        continue;
                    }

                    outputBuffer.RemoveAt(0);
                    outputBuffer.RemoveAt(outputBuffer.Count - 1);

                    var decodedBuffer = outputBuffer.ToArray();
                    var response = UartResponse.Parser.ParseFrom(decodedBuffer);
                    if (response.BodyCase.Equals(UartResponse.BodyOneofCase.BootMessage))
                    {
                        Console.WriteLine(
                            $"Device heart beat {ConvertDeviceId(response.BootMessage.DeviceIdentifier)}");
                        LastBootMessage = response.BootMessage;
                    }
                }
            }
            catch (TimeoutException)
            {
                // Console.WriteLine("Serial read timeout.");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Serial closed.");
            }
    }
}