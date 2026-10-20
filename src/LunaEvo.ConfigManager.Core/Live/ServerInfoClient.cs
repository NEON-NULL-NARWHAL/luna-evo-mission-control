// LUNA EVO · LunaEvo.ConfigManager.Core/Live/ServerInfoClient.cs
// [Core] Reads the live JSON LMP publishes when EnableWebsite is on: players, vessels, subspaces, memory.
// Used by: PlayersPage, ServerPage (player join/leave notices)
// Guide:   Meat and Potatoes/11-Live-Players.md

using System.Text.Json;
using LunaEvo.ConfigManager.Core.Validation;

namespace LunaEvo.ConfigManager.Core.Live;

public sealed record VesselSummary(string Id, string Name, string Type, double Altitude, int ReferenceBody);

public sealed record SubspaceSummary(int Id, double Time, string Creator);

public sealed record ServerSnapshot(
    DateTime? UniverseStart,
    IReadOnlyList<string> Players,
    IReadOnlyList<VesselSummary> Vessels,
    IReadOnlyList<SubspaceSummary> Subspaces,
    long BytesUsed,
    int? MaxPlayers,
    string? ServerName);

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

    public async Task<ServerSnapshot> GetAsync(Uri endpoint, CancellationToken cancel = default)
    {
        using var response = await http.GetAsync(endpoint, cancel);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancel);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancel);
        return Parse(json.RootElement);
    }

    /// <summary>Parses LMP's JSON (the array, or a single object for safety).</summary>
    public static ServerSnapshot Parse(JsonElement root)
    {
        var info = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().First() : root;
        var state = info.GetProperty("CurrentState");

        DateTime? start = state.TryGetProperty("StartTime", out var s) && s.ValueKind == JsonValueKind.String && s.TryGetDateTime(out var t) ? t : null;
        var players = state.TryGetProperty("CurrentPlayers", out var p)
            ? p.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList()
            : [];
        var vessels = state.TryGetProperty("CurrentVessels", out var v)
            ? v.EnumerateArray().Select(x => new VesselSummary(
                Str(x, "Id"), Str(x, "Name"), Str(x, "Type"),
                x.TryGetProperty("Alt", out var alt) && alt.TryGetDouble(out var a) ? a : 0,
                x.TryGetProperty("ReferenceBody", out var body) && body.TryGetInt32(out var b) ? b : -1)).ToList()
            : [];
        var subspaces = state.TryGetProperty("Subspaces", out var ss)
            ? ss.EnumerateArray().Select(x => new SubspaceSummary(
                x.GetProperty("Id").GetInt32(), x.GetProperty("Time").GetDouble(), Str(x, "Creator"))).ToList()
            : [];
        var bytes = state.TryGetProperty("BytesUsed", out var used) && used.TryGetInt64(out var n) ? n : 0;

        int? maxPlayers = null;
        string? name = null;
        if (info.TryGetProperty("GeneralSettings", out var general))
        {
            if (general.TryGetProperty("MaxPlayers", out var max) && max.TryGetInt32(out var m)) maxPlayers = m;
            name = general.TryGetProperty("ServerName", out var sn) ? sn.GetString() : null;
        }
        return new ServerSnapshot(start, players, vessels, subspaces, bytes, maxPlayers, name);
    }

    /// <summary>Who joined and who left between two snapshots.</summary>
    public static (IReadOnlyList<string> Joined, IReadOnlyList<string> Left) Diff(ServerSnapshot? before, ServerSnapshot after)
    {
        var old = before?.Players ?? [];
        return (after.Players.Except(old, StringComparer.Ordinal).ToList(), old.Except(after.Players, StringComparer.Ordinal).ToList());
    }

    private static string Str(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
}
