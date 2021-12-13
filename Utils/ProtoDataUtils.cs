namespace LoraGateway.Utils;

public static class ProtoDataUtils
{
    private static string ConvertFirmwareVersion(Version? version)
    {
        if (version == null) return "";
        return $"{version.Major}.{version.Minor}.{version.Patch}.{version.Revision}";
    }
    
    public static string GetFirmwareAsString(this BootMessage response)
    {
        return ConvertFirmwareVersion(response.FirmwareVersion);
    } 
    
    public static string DeviceIdAsString(this DeviceId? spec)
    {
        if (spec == null) return "";
        return $"{spec.Id0}-{spec.Id1}-{spec.Id2}";
    }
}