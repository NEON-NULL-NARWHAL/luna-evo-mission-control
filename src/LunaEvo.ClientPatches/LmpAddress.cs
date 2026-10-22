// LUNA EVO · LunaEvo.ClientPatches/LmpAddress.cs
// [Patch] Reads the address given to KSP as "-lmp lmp://host:port": host and port, IPv6 in brackets, port optional.
// Used by: CommandLinePatch (and the Core tests, which compile this file too)
// Guide:   Meat and Potatoes/27-Client-Patches.md

#if NETCOREAPP
#nullable disable // the Core tests compile this file too; the patch itself is C# 7.3, which has no nullable references
#endif

namespace LunaEvo.ClientPatches
{
    /// <summary>
    /// No KSP or LMP types here, so tests can run it. Kept to C# 7.3 for KSP's .NET Framework 4.7.2 build.
    /// </summary>
    public static class LmpAddress
    {
        public const int DefaultPort = 8800;

        /// <summary>
        /// "lmp://host", "lmp://host:port", "host:port", "[::1]:8800", "lmp://host:port/". False for anything else
        /// (no host, a bad port, spaces, too long).
        /// </summary>
        public static bool TryParse(string text, out string host, out int port)
        {
            host = null;
            port = DefaultPort;
            if (string.IsNullOrWhiteSpace(text) || text.Length > 300)
                return false;
            var rest = text.Trim();
            if (rest.StartsWith("lmp://", System.StringComparison.OrdinalIgnoreCase))
                rest = rest.Substring("lmp://".Length);
            rest = rest.TrimEnd('/');
            if (rest.Length == 0)
                return false;

            string portText = null;
            if (rest[0] == '[')
            {
                // [IPv6]:port
                var close = rest.IndexOf(']');
                if (close < 2)
                    return false;
                host = rest.Substring(1, close - 1);
                var after = rest.Substring(close + 1);
                if (after.Length > 0)
                {
                    if (after[0] != ':')
                        return false;
                    portText = after.Substring(1);
                }
            }
            else
            {
                var colon = rest.LastIndexOf(':');
                if (colon >= 0 && rest.IndexOf(':') == colon)
                {
                    host = rest.Substring(0, colon);
                    portText = rest.Substring(colon + 1);
                }
                else
                {
                    host = rest; // no port, or a bare IPv6 address (several colons): the default port
                }
            }

            if (host.Length == 0 || host.Length > 255)
                return false;
            foreach (var c in host)
                if (char.IsWhiteSpace(c) || char.IsControl(c) || c == '/' || c == '@')
                    return false;
            if (portText != null && (!int.TryParse(portText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535))
            {
                port = DefaultPort;
                return false;
            }
            return true;
        }
    }
}
