// LUNA EVO · LunaEvo.ConfigManager.Core/Join/ServerProbe.cs
// [Core] For players: look up a friend's server (addresses, ping, live info from its website) before joining.
// Used by: JoinPage
// Guide:   Meat and Potatoes/15-Joining-Servers.md

using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using LunaEvo.ConfigManager.Core.Live;
using LunaEvo.ConfigManager.Core.Ops;

namespace LunaEvo.ConfigManager.Core.Join;

/// <summary>A server to join: host name or IP, LMP's game port (UDP) and its website port (TCP, for live info).</summary>
public sealed record ServerAddress(string Host, int Port = ServerAddress.DefaultPort, int WebsitePort = ServerAddress.DefaultWebsitePort)
{
    public const int DefaultPort = 8800;
    public const int DefaultWebsitePort = 8900;

    public override string ToString() => Port == DefaultPort ? Host : $"{HostForUri}:{Port}";

    /// <summary>IPv6 literals need brackets in URLs and host:port text.</summary>
    public string HostForUri => Host.Contains(':') && !Host.StartsWith('[') ? $"[{Host}]" : Host;

    /// <summary>
    /// Reads "host", "host:port", "[ipv6]:port", "1.2.3.4" or "lmp://host:port". Null if it isn't a usable address.
    /// </summary>
    public static ServerAddress? Parse(string? text, int websitePort = DefaultWebsitePort)
    {
        text = text?.Trim();
        if (string.IsNullOrEmpty(text))
            return null;
        if (text.StartsWith("lmp://", StringComparison.OrdinalIgnoreCase))
            text = text[6..].TrimEnd('/');

        string host;
        var port = DefaultPort;
        if (text.StartsWith('['))
        {
            var close = text.IndexOf(']');
            if (close < 0)
                return null;
            host = text[1..close];
            var rest = text[(close + 1)..];
            if (rest.StartsWith(':') && !int.TryParse(rest[1..], out port))
                return null;
        }
        else if (text.Count(c => c == ':') == 1)
        {
            var colon = text.IndexOf(':');
            host = text[..colon];
            if (!int.TryParse(text[(colon + 1)..], out port))
                return null;
        }
        else
        {
            host = text; // a bare IPv6 address, or a name with no port
        }

        if (port is < 1 or > 65535 || host.Length == 0 || host.Any(c => char.IsWhiteSpace(c) || c is '/' or '\\' or '@' or '?' or '#'))
            return null;
        return Uri.CheckHostName(host) == UriHostNameType.Unknown ? null : new ServerAddress(host, port, websitePort);
    }
}

public sealed record ResolvedAddress(string Address, string Kind);

public sealed record PingResult(int Sent, int Received, long? AverageMs, long? BestMs)
{
    public bool AnyReply => Received > 0;
}

public sealed record LiveInfoResult(bool Reachable, long? LatencyMs, ServerSnapshot? Snapshot, string? Error);

public static class ServerProbe
{
    /// <summary>The host's IP addresses, each labelled with what kind of network it's on.</summary>
    public static async Task<IReadOnlyList<ResolvedAddress>> ResolveAsync(string host, CancellationToken cancel = default)
    {
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancel);
        return addresses
            .Where(a => a.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
            .OrderBy(a => a.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
            .Select(a => new ResolvedAddress(a.ToString(), Kind(a)))
            .ToList();
    }

    /// <summary>Plain-language network type for an address.</summary>
    public static string Kind(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return "This PC";
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            if (b[0] == 100 && b[1] is >= 64 and <= 127)
                return "VPN or mesh network (e.g. Tailscale)";
            if (NetworkHelper.IsPrivate(address))
                return "Local network";
            if (b[0] == 169 && b[1] == 254)
                return "Link-local (no network configured)";
            return "Internet";
        }
        if (address.IsIPv6LinkLocal)
            return "Local network (IPv6 link-local)";
        return (address.GetAddressBytes()[0] & 0xFE) == 0xFC ? "VPN or mesh network (IPv6)" : "Internet (IPv6)";
    }

    /// <summary>
    /// ICMP ping. Many PCs block pings in their firewall, so "no reply" doesn't mean the server is down; LMP itself
    /// uses UDP, which can't be tested without joining.
    /// </summary>
    public static async Task<PingResult> PingAsync(string address, int count = 4, int timeoutMs = 1500, CancellationToken cancel = default)
    {
        var times = new List<long>();
        using var ping = new Ping();
        for (var i = 0; i < count; i++)
        {
            cancel.ThrowIfCancellationRequested();
            try
            {
                var reply = await ping.SendPingAsync(address, timeoutMs);
                if (reply.Status == IPStatus.Success)
                    times.Add(reply.RoundtripTime);
            }
            catch (PingException)
            {
                // unreachable; counts as lost
            }
        }
        return new PingResult(count, times.Count, times.Count > 0 ? (long)times.Average() : null, times.Count > 0 ? times.Min() : null);
    }

    /// <summary>
    /// Reads LMP's live server info from its website port, timing the round trip. Only works if the host shares it
    /// (LUNA EVO's Connect page → "Share live info with players"); a failure is reported, never thrown.
    /// </summary>
    public static async Task<LiveInfoResult> LiveInfoAsync(HttpClient http, ServerAddress server, TimeSpan? timeout = null, CancellationToken cancel = default)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        limit.CancelAfter(timeout ?? TimeSpan.FromSeconds(5));
        var watch = Stopwatch.StartNew();
        try
        {
            var snapshot = await new ServerInfoClient(http).GetAsync(new Uri($"http://{server.HostForUri}:{server.WebsitePort}/"), limit.Token);
            return new LiveInfoResult(true, watch.ElapsedMilliseconds, snapshot, null);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or UriFormatException)
        {
            var reason = e switch
            {
                TaskCanceledException => "no answer within 5 seconds",
                System.Text.Json.JsonException => "something answered, but it isn't LMP's server info",
                _ => e.Message,
            };
            return new LiveInfoResult(false, null, null, reason);
        }
    }
}
