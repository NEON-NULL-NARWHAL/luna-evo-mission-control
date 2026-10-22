// LUNA EVO · LunaEvo.MissionControl.Core/Live/LiveFeed.cs
// [Core] Reads <server>/LunaEvo/live.json, written by the LUNA EVO server plugin: every vessel's latest position, who flies what.
// Used by: MapPage, LiveVessels
// Guide:   Meat and Potatoes/26-Server-Plugin.md

using System.Globalization;
using System.Text.Json;

namespace LunaEvo.MissionControl.Core.Live;

public sealed record LivePlayer(string Name, int Subspace, string? Vessel);

/// <summary>A vessel's last position message, as the server plugin saw it.</summary>
public sealed record LiveVessel(string Id, int Body, string BodyName, double Lat, double Lon, double Alt, Map.Orbit? Orbit,
    bool Landed, bool Splashed, string? FlownBy, DateTime ReceivedUtc);

public sealed record LiveFeed(DateTime WrittenUtc, string PluginVersion, string LmpVersion, IReadOnlyList<LivePlayer> Players,
    IReadOnlyList<LiveVessel> Vessels)
{
    /// <summary>The format the plugin writes (LiveState.Format); anything else is ignored.</summary>
    public const int Format = 1;

    /// <summary>The plugin rewrites the file at least every 2 s while the server runs; older means it stopped.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(6);

    /// <summary>A real file is about 600 bytes per vessel; far bigger isn't ours.</summary>
    public const int MaxBytes = 16 * 1024 * 1024;

    public static string PathFor(string serverDirectory) => Path.Combine(serverDirectory, "LunaEvo", "live.json");

    /// <summary>The current feed, or null if there's none, it's stale (server stopped or crashed) or it can't be read.</summary>
    public static LiveFeed? Read(string serverDirectory, DateTime? utcNow = null)
    {
        var path = PathFor(serverDirectory);
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxBytes)
                return null;
            var feed = Parse(File.ReadAllBytes(path));
            var now = utcNow ?? DateTime.UtcNow;
            return feed != null && now - feed.WrittenUtc < MaxAge && feed.WrittenUtc - now < MaxAge ? feed : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null; // the plugin is replacing it right now; the next read gets it
        }
    }

    /// <summary>Parses the plugin's JSON. It comes from a file anyone on this PC could write, so nothing is assumed.</summary>
    public static LiveFeed? Parse(ReadOnlySpan<byte> json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Int(root, "Format") != Format || !Date(root, "WrittenUtc", out var written))
                return null;
            var players = Array(root, "Players").Take(1000).Select(p => new LivePlayer(Str(p, "Name"), Int(p, "Subspace") ?? 0, Opt(p, "Vessel")))
                .Where(p => p.Name.Length > 0).ToList();
            var vessels = new List<LiveVessel>();
            foreach (var v in Array(root, "Vessels").Take(20_000))
            {
                var id = Str(v, "Id");
                if (id.Length == 0)
                    continue;
                // INC, ECC, SMA, LAN, LPE, MNA, EPH, REF: the order LMP's position message uses.
                var o = Array(v, "Orbit").Select(x => x.ValueKind == JsonValueKind.Number && x.TryGetDouble(out var d) && double.IsFinite(d) ? d : double.NaN).ToArray();
                var orbit = o.Length == 8 && o.All(double.IsFinite) && o[2] != 0 && o[1] >= 0 ? new Map.Orbit(o[2], o[1], o[0], o[3], o[4], o[5], o[6]) : null;
                Date(v, "ReceivedUtc", out var received);
                vessels.Add(new LiveVessel(id, Int(v, "Body") ?? -1, Str(v, "BodyName"), Num(v, "Lat") ?? 0, Num(v, "Lon") ?? 0, Num(v, "Alt") ?? 0,
                    orbit, Bool(v, "Landed"), Bool(v, "Splashed"), Opt(v, "FlownBy"), received));
            }
            return new LiveFeed(written, Str(root, "Plugin"), Str(root, "Lmp"), players, vessels);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<JsonElement> Array(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Where(x => x.ValueKind is JsonValueKind.Object or JsonValueKind.Number) : [];

    private static int? Int(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

    private static double? Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) && double.IsFinite(d) ? d : null;

    private static bool Bool(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static string Str(JsonElement e, string name) => Opt(e, name) ?? "";

    private static string? Opt(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s
            ? s.Length > ServerInfoClient.MaxTextLength ? s[..ServerInfoClient.MaxTextLength] : s
            : null;

    private static bool Date(JsonElement e, string name, out DateTime value)
    {
        value = default;
        return Opt(e, name) is { } text && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out value);
    }
}
