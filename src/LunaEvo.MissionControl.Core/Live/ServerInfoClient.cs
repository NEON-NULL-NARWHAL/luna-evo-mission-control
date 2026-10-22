// LUNA EVO · LunaEvo.MissionControl.Core/Live/ServerInfoClient.cs
// [Core] Reads the live JSON LMP publishes when EnableWebsite is on: players, vessels, subspaces, memory.
// Used by: PlayersPage, ServerPage (player join/leave notices), MapPage (live vessel orbits)
// Guide:   Meat and Potatoes/11-Live-Players.md

using System.Text.Json;
using LunaEvo.MissionControl.Core.Validation;

namespace LunaEvo.MissionControl.Core.Live;

public sealed record VesselSummary(string Id, string Name, string Type, double Altitude, int ReferenceBody)
{
    /// <summary>Its orbit as LMP's server holds it right now (the save's ORBIT values), or null if it's unusable.</summary>
    public Map.Orbit? Orbit { get; init; }

    public double Lat { get; init; }
    public double Lon { get; init; }
}

public sealed record SubspaceSummary(int Id, double Time, string Creator);

public sealed record ServerSnapshot(
    DateTime? UniverseStart,
    IReadOnlyList<string> Players,
    IReadOnlyList<VesselSummary> Vessels,
    IReadOnlyList<SubspaceSummary> Subspaces,
    long BytesUsed,
    int? MaxPlayers,
    string? ServerName)
{
    // More of LMP's GeneralSettings / WarpSettings, for the Join page.
    public string Description { get; init; } = "";
    public string GameMode { get; init; } = "";
    public string GameDifficulty { get; init; } = "";
    public string WarpMode { get; init; } = "";
    public bool? HasPassword { get; init; }
    public bool? ModControl { get; init; }
}

/// <summary>
/// LMP's web server (Server/Web/WebServer.cs) answers GET / with a JSON array holding one ServerInformation:
/// CurrentState { StartTime, CurrentPlayers[], CurrentVessels[], Subspaces[], BytesUsed } plus every setting.
/// It refreshes every WebsiteSettings.RefreshIntervalMs (5 s by default).
/// </summary>
public sealed class ServerInfoClient(HttpClient http)
{
    /// <summary>
    /// Where to ask, from the config: null when the website is off. A specific ListenAddress is used as-is;
    /// "any" addresses (empty, ::, 0.0.0.0) mean we can ask on localhost.
    /// </summary>
    public static Uri? EndpointFor(ConfigSession session)
    {
        if (session.GetValue(LmpRules.Website, "EnableWebsite") != "true")
            return null;
        var port = session.GetValue(LmpRules.Website, "Port");
        var address = session.GetValue(LmpRules.Website, "ListenAddress")?.Trim();
        var host = string.IsNullOrEmpty(address) || address is "::" or "[::]" or "0.0.0.0" ? "127.0.0.1"
            : address.Contains(':') && !address.StartsWith('[') ? $"[{address}]" : address;
        return Uri.TryCreate($"http://{host}:{port}/", UriKind.Absolute, out var uri) ? uri : null;
    }

    /// <summary>LMP's real answer is a few KB per player and vessel; anything far bigger isn't LMP.</summary>
    public const int MaxResponseBytes = 4 * 1024 * 1024;

    public async Task<ServerSnapshot> GetAsync(Uri endpoint, CancellationToken cancel = default)
    {
        // Whatever answers decides the size and speed of the response, so read headers first, cap the body, and
        // never wait longer than 15 seconds (the shared client's own timeout is minutes, for downloads).
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        limit.CancelAfter(TimeSpan.FromSeconds(15));
        using var response = await http.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, limit.Token);
        response.EnsureSuccessStatusCode();
        // A redirect to somewhere else isn't the LMP server we asked.
        if (response.RequestMessage?.RequestUri is { } final && (final.Host != endpoint.Host || final.Port != endpoint.Port))
            throw new JsonException("That isn't LMP's server information (it redirected elsewhere).");
        if (response.Content.Headers.ContentLength > MaxResponseBytes)
            throw new JsonException("That isn't LMP's server information (far too big).");

        await using var stream = await response.Content.ReadAsStreamAsync(limit.Token);
        using var body = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, limit.Token)) > 0)
        {
            if (body.Length + read > MaxResponseBytes)
                throw new JsonException("That isn't LMP's server information (far too big).");
            body.Write(buffer, 0, read);
        }
        body.Position = 0;
        using var json = await JsonDocument.ParseAsync(body, cancellationToken: limit.Token);
        return Parse(json.RootElement);
    }

    /// <summary>Parses LMP's JSON (the array, or a single object for safety).</summary>
    public static ServerSnapshot Parse(JsonElement root)
    {
        // This comes from whatever answers on the website port, so never assume its shape.
        var info = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0 ? root[0] : root;
        if (info.ValueKind != JsonValueKind.Object || !info.TryGetProperty("CurrentState", out var state) || state.ValueKind != JsonValueKind.Object)
            throw new JsonException("That isn't LMP's server information.");

        DateTime? start = state.TryGetProperty("StartTime", out var s) && s.ValueKind == JsonValueKind.String && s.TryGetDateTime(out var t) ? t : null;
        var players = Array(state, "CurrentPlayers")
            .Where(x => x.ValueKind == JsonValueKind.String).Select(x => Cap(x.GetString())).Where(x => x.Length > 0)
            .Take(1000).ToList();
        var vessels = Array(state, "CurrentVessels").Where(x => x.ValueKind == JsonValueKind.Object).Take(10_000)
            .Select(x => new VesselSummary(
                Str(x, "Id"), Str(x, "Name"), Str(x, "Type"),
                Double(x, "Alt") ?? 0,
                Int(x, "ReferenceBody") ?? -1)
            {
                // LMP's VesselInfo copies the save's ORBIT values: SMA m, angles in degrees, mean anomaly in radians.
                Orbit = Double(x, "SemiMajorAxis") is { } sma && Double(x, "Eccentricity") is { } ecc && ecc >= 0 && sma != 0
                    ? new Map.Orbit(sma, ecc, Double(x, "Inclination") ?? 0, Double(x, "LongitudeOfAscendingNode") ?? 0,
                        Double(x, "ArgumentOfPeriapsis") ?? 0, Double(x, "MeanAnomaly") ?? 0, Double(x, "Epoch") ?? 0)
                    : null,
                Lat = Double(x, "Lat") ?? 0,
                Lon = Double(x, "Lon") ?? 0,
            }).ToList();
        var subspaces = Array(state, "Subspaces").Where(x => x.ValueKind == JsonValueKind.Object).Take(1000)
            .Select(x => new SubspaceSummary(
                Int(x, "Id") ?? -1,
                Double(x, "Time") ?? 0,
                Str(x, "Creator"))).ToList();
        var bytes = state.TryGetProperty("BytesUsed", out var used) && used.ValueKind == JsonValueKind.Number && used.TryGetInt64(out var n) ? n : 0;

        int? maxPlayers = null;
        string? name = null;
        if (info.TryGetProperty("GeneralSettings", out var general) && general.ValueKind == JsonValueKind.Object)
        {
            maxPlayers = Int(general, "MaxPlayers");
            name = Str(general, "ServerName");
        }
        var snapshot = new ServerSnapshot(start, players, vessels, subspaces, bytes, maxPlayers, name);
        if (info.TryGetProperty("GeneralSettings", out var g) && g.ValueKind == JsonValueKind.Object)
            snapshot = snapshot with
            {
                Description = Str(g, "Description"),
                GameMode = Str(g, "GameMode"),
                GameDifficulty = Str(g, "GameDifficulty"),
                HasPassword = Bool(g, "HasPassword"),
                ModControl = Bool(g, "ModControl"),
            };
        if (info.TryGetProperty("WarpSettings", out var w) && w.ValueKind == JsonValueKind.Object)
            snapshot = snapshot with { WarpMode = Str(w, "WarpMode") };
        return snapshot;
    }

    /// <summary>Who joined and who left between two snapshots.</summary>
    public static (IReadOnlyList<string> Joined, IReadOnlyList<string> Left) Diff(ServerSnapshot? before, ServerSnapshot after)
    {
        var old = before?.Players ?? [];
        return (after.Players.Except(old, StringComparer.Ordinal).ToList(), old.Except(after.Players, StringComparer.Ordinal).ToList());
    }

    // JsonElement.TryGetInt32/TryGetDouble throw (not return false) when the value isn't a number at all,
    // so check the kind first: this JSON comes from outside.
    private static int? Int(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i) ? i : null;

    private static double? Double(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d) && double.IsFinite(d) ? d : null;

    private static bool? Bool(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;

    private static IEnumerable<JsonElement> Array(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];

    private static string Str(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? Cap(value.GetString()) : "";

    /// <summary>LMP's own limits are 30-200 characters; longer text from outside is cut so it can't swamp the UI or LMP's server list.</summary>
    public const int MaxTextLength = 256;

    private static string Cap(string? text) => text is null ? "" : text.Length > MaxTextLength ? text[..MaxTextLength] : text;
}
