using System.Text;
using LoraGateway.Services.Firmware.RandomLinearCoding;

namespace LoraGateway.Services.Firmware;

/// <summary>
/// Accepts unencoded packets so the generation size can be applied and encoding is performed to get RLNC encoded packets 
/// </summary>
public class RlncEncodingService
{
    private EncodingSettings _settings;
    private List<EncodedPacket> _encodedPackets;
    private List<UnencodedPacket> _unencodedPackets;

    public void ConfigureEncoding(EncodingSettings settings)
    {
        _settings = settings;
    }

    public void StoreUnencodedPackets(List<UnencodedPacket> unencodedPackets)
    {
        _unencodedPackets = unencodedPackets;
    }
}