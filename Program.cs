// Use this code inside a project created with the Visual C# > Windows Desktop > Console Application template.
// Replace the code in Program.cs with this code.

using System.IO.Ports;

namespace LoraGateway;

public class PortChat
{
    private static bool _continue;
    private static SerialPort? _serialPort;

    public static void Main(int asd)
    {
        var stringComparer = StringComparer.OrdinalIgnoreCase;
        var readThread = new Thread(Read);

        // Create a new SerialPort object with default settings.
        _serialPort = new SerialPort();

        // Allow the user to set the appropriate properties.
        _serialPort.PortName = SetPortName(_serialPort.PortName);
        _serialPort.BaudRate = 821600; // SetPortBaudRate(821600); // _serialPort.BaudRate
        _serialPort.Parity = Parity.None; // SetPortParity(_serialPort.Parity);
        _serialPort.DataBits = 8; // SetPortDataBits(_serialPort.DataBits);
        _serialPort.StopBits = StopBits.One; // SetPortStopBits(_serialPort.StopBits);
        _serialPort.Handshake = Handshake.None; // SetPortHandshake(_serialPort.Handshake);

        // Set the read/write timeouts
        _serialPort.ReadTimeout = 500;
        _serialPort.WriteTimeout = 500;

        _serialPort.Open();
        _continue = true;
        readThread.Start();


        Console.Write("Name: ");
        var name = Console.ReadLine();

        Console.WriteLine("Type QUIT to exit");

        while (_continue)
        {
            var message = Console.ReadLine();

            if (stringComparer.Equals("quit", message))
                _continue = false;
            else
                _serialPort.WriteLine(
                    string.Format("<{0}>: {1}", name, message));
        }

        readThread.Join();
        _serialPort.Close();
    }

    public static void Read()
    {
        while (_continue)
            try
            {
                var message = _serialPort.ReadLine();
                Console.WriteLine(message);
            }
            catch (TimeoutException)
            {
            }
    }

    // Display Port values and prompt user to enter a port.
    public static string SetPortName(string defaultPortName)
    {
        Console.WriteLine("Available Ports:");
        foreach (var s in SerialPort.GetPortNames()) Console.WriteLine("   {0}", s);

        Console.Write("Enter COM port value (Default: {0}): ", defaultPortName);
        var portName = Console.ReadLine();

        if (portName == "" || !portName.ToLower().StartsWith("com")) portName = defaultPortName;
        return portName;
    }

    // Display BaudRate values and prompt user to enter a value.
    public static int SetPortBaudRate(int defaultPortBaudRate)
    {
        Console.Write("Baud Rate(default:{0}): ", defaultPortBaudRate);
        var baudRate = Console.ReadLine();

        if (baudRate == "") baudRate = defaultPortBaudRate.ToString();

        return int.Parse(baudRate);
    }

    // Display PortParity values and prompt user to enter a value.
    public static Parity SetPortParity(Parity defaultPortParity)
    {
        Console.WriteLine("Available Parity options:");
        foreach (var s in Enum.GetNames(typeof(Parity))) Console.WriteLine("   {0}", s);

        Console.Write("Enter Parity value (Default: {0}):", defaultPortParity.ToString(), true);
        var parity = Console.ReadLine();

        if (parity == "") parity = defaultPortParity.ToString();

        return (Parity) Enum.Parse(typeof(Parity), parity, true);
    }

    // Display DataBits values and prompt user to enter a value.
    public static int SetPortDataBits(int defaultPortDataBits)
    {
        Console.Write("Enter DataBits value (Default: {0}): ", defaultPortDataBits);
        var dataBits = Console.ReadLine();

        if (dataBits == "") dataBits = defaultPortDataBits.ToString();

        return int.Parse(dataBits.ToUpperInvariant());
    }

    // Display StopBits values and prompt user to enter a value.
    public static StopBits SetPortStopBits(StopBits defaultPortStopBits)
    {
        Console.WriteLine("Available StopBits options:");
        foreach (var s in Enum.GetNames(typeof(StopBits))) Console.WriteLine("   {0}", s);

        Console.Write("Enter StopBits value (None is not supported and \n" +
                      "raises an ArgumentOutOfRangeException. \n (Default: {0}):", defaultPortStopBits.ToString());
        var stopBits = Console.ReadLine();

        if (stopBits == "") stopBits = defaultPortStopBits.ToString();

        return (StopBits) Enum.Parse(typeof(StopBits), stopBits, true);
    }

    public static Handshake SetPortHandshake(Handshake defaultPortHandshake)
    {
        Console.WriteLine("Available Handshake options:");
        foreach (var s in Enum.GetNames(typeof(Handshake))) Console.WriteLine("   {0}", s);

        Console.Write("Enter Handshake value (Default: {0}):", defaultPortHandshake.ToString());
        var handshake = Console.ReadLine();

        if (handshake == "") handshake = defaultPortHandshake.ToString();

        return (Handshake) Enum.Parse(typeof(Handshake), handshake, true);
    }
}