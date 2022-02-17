using LoraGateway.Services.Firmware.LoRaPhy;

namespace LoraGateway.Services.Firmware;

public class BlobFragmentationService
{
    // List<List<byte>> 
    public BlobFragmentationService()
    {
        
    }
    
    /// <summary>
    /// Mimicks firmware by repeating 1 to ... in the payloads
    /// </summary>
    /// <param name="path"></param>
    public void LoadFirmware()
    {
        var result = LoRaWanTimeOnAir.GetTimeOnAir(10);
        
        Console.WriteLine(result.TimePacket);
    }
}