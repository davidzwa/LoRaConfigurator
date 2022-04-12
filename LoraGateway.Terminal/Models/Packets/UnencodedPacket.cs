namespace LoraGateway.Services.Firmware.RandomLinearCoding;

public class UnencodedPacket : IPacket, IEquatable<UnencodedPacket>
{
    /// <summary>
    ///     The encoded payload in bytes (regardless of what original symbol size, although 8-bit is taken for now)
    /// </summary>
    public List<GFSymbol> Payload { get; set; } = new();

    public override bool Equals(object? obj)
    {
        return base.Equals(obj);
    }

    public bool Equals(UnencodedPacket packet)
    {
        if (packet is null) return false;

        // Optimization for a common success case.
        if (ReferenceEquals(this, packet)) return true;

        // If run-time types are not exactly the same, return false.
        if (GetType() != packet.GetType()) return false;

        if (Payload.Count != packet.Payload.Count) return false;

        for (var i = 0; i < packet.Payload.Count(); i++)
            if (Payload[i] != packet.Payload[i])
                return false;

        return true;
    }

    public static bool operator ==(UnencodedPacket lhs, UnencodedPacket rhs)
    {
        if (lhs is null)
        {
            if (rhs is null)
            {
                return true;
            }

            // Only the left side is null.
            return false;
        }

        // Equals handles case of null on right side.
        return lhs.Equals(rhs);
    }

    public static bool operator !=(UnencodedPacket lhs, UnencodedPacket rhs) => !(lhs == rhs);
}