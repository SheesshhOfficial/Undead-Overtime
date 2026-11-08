using System;
using System.Globalization;

namespace UndeadOvertime.Core
{
    /// <summary>Prototype accepts explicit loopback/private/link-local IPv4 only; no DNS, Internet hosting or wildcard bind.</summary>
    public static class LanEndpoint
    {
        public static bool TryParse(string address, string portText, out ushort port)
        {
            port = 0;
            if (string.IsNullOrWhiteSpace(address) || !ushort.TryParse(portText, NumberStyles.None,
                CultureInfo.InvariantCulture, out ushort parsed) || parsed == 0) return false;
            string[] parts = address.Split('.');
            if (parts.Length != 4) return false;
            var octets = new byte[4];
            for (int i = 0; i < 4; i++)
                if (parts[i].Length == 0 || parts[i].Length > 3 ||
                    !byte.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out octets[i]) ||
                    parts[i] != octets[i].ToString(CultureInfo.InvariantCulture)) return false;
            bool local = octets[0] == 127 || octets[0] == 10 ||
                (octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31) ||
                (octets[0] == 192 && octets[1] == 168) || (octets[0] == 169 && octets[1] == 254);
            if (!local || octets[3] == 255) return false;
            port = parsed; return true;
        }
    }
}
