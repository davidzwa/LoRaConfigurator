using System.IO.Ports;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using LoraGateway.Models;

namespace LoraGateway.Utils;

public static class SerialUtil
{
    public static IEnumerable<PortWithCaption> GetStmDevicePorts(string captionFilter = "STMicroelectronics")
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return SerialPort.GetPortNames().Select(p => new PortWithCaption
            {
                PortName = p
            }).ToList();
        }

        using var searcher = new ManagementObjectSearcher("SELECT * FROM WIN32_SerialPort");
        var portNames = SerialPort.GetPortNames();
        var ports = searcher.Get().Cast<ManagementBaseObject>().ToList();
#pragma warning disable CA1416
        var portList = (from n in portNames
                join p in ports on n equals p["DeviceID"].ToString()
                where p["Caption"].ToString().Contains(captionFilter)
                select new PortWithCaption
                {
                    PortName = n,
                    Caption = p["Caption"].ToString()
                })
            .ToList();
#pragma warning restore CA1416
        return portList;
    }

    public static string ByteArrayToString(byte[] ba)
    {
        var hex = new StringBuilder(ba.Length * 2);
        foreach (var b in ba)
            hex.AppendFormat("{0:x2} ", b);
        return hex.ToString();
    }
}