using System.IO.Ports;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

namespace LoraGateway.Utils;

public static class SerialUtil
{
    public static List<PortWithCaption> GetStmDevicePorts(string captionFilter)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            using (var searcher = new ManagementObjectSearcher
                       ("SELECT * FROM WIN32_SerialPort"))
            {
                var portnames = SerialPort.GetPortNames();
                var ports = searcher.Get().Cast<ManagementBaseObject>().ToList();
#pragma warning disable CA1416
                var tList = (from n in portnames
                        join p in ports on n equals p["DeviceID"].ToString()
                        where p["Caption"].ToString().Contains(captionFilter)
                        select new PortWithCaption
                        {
                            Port = n,
                            Caption = p["Caption"].ToString()
                        })
                    .ToList();
#pragma warning restore CA1416
                return tList;
            }

        return SerialPort.GetPortNames().Select(p => new PortWithCaption
        {
            Port = p
        }).ToList();
    }

    public static string ByteArrayToString(byte[] ba)
    {
        var hex = new StringBuilder(ba.Length * 2);
        foreach (var b in ba)
            hex.AppendFormat("{0:x2} ", b);
        return hex.ToString();
    }
}