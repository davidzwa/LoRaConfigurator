using LoraGateway.Services.Firmware.RandomLinearCoding;
using LoraGateway.Services.Firmware.Utils;

namespace LoraGateway.Services.Firmware;

public class RlncDecodingService
{
    // https://github.com/elsheimy/Elsheimy.Components.Linears/tree/main/Matrix
    public uint FindNextReducibleRow(List<EncodedPacket> packets, uint rowRedProgression)
    {
        var encodingMatrix = packets.ToEncodingMatrix();
        // rowRedProgression indicates which column we need to aim for in looking for a best 
        return (uint)packets.FindIndex(p => p.Payload[rowRedProgression] != 0x00);
    }

    public uint DetermineEncodingMatrixRank(List<EncodedPacket> packets)
    {
        // Rows should at least exceed col counts

        return 0;
    }
}