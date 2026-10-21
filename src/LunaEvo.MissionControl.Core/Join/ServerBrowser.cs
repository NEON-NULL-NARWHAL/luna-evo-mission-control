// LUNA EVO · LunaEvo.MissionControl.Core/Join/ServerBrowser.cs
// [Core] The public LMP server list, read from LMP's master servers' web lists (the same servers LMP's in-game browser shows).
// Used by: BrowserPage
// Guide:   Meat and Potatoes/23-Server-Browser.md

using System.Text.Json;

namespace LunaEvo.MissionControl.Core.Join;

/// <summary>One public server, as a master server lists it. All text is cut to a safe length.</summary>
public sealed record PublicServer(
    string Name, string Description, string Host, int Port, string Version, bool Password, bool Cheats,
    string GameMode, int Players, int MaxPlayers, string Country, bool Dedicated, string? Website, string? WebsiteText)
{
    public string Address => Host.Contains(':') ? $"[{Host}]:{Port}" : $"{Host}:{Port}";
}

/// <summary>
/// LMP's master servers (the ones in LMP's MasterServersList.txt) each publish their list at <c>/json</c> on their web
/// port. LUNA EVO asks every one over HTTPS, keeps whatever answers, and merges them by address. Read-only; nothing is
/// sent but the request.
/// </summary>
public sealed class ServerBrowser(HttpClient http)
{
    /// <summary>The web lists of LMP's master servers (https; the UDP ports in LMP's list are 8700/8750).</summary>
    public static IReadOnlyList<string> MasterLists { get; set; } =
    [
        "https://ms.lmp.dasskelett.dev/json",
        "https://ms-nightly.lmp.dasskelett.dev/json",
        "https://lmp.nightshade.fun/json",
        "https://lmp.nightshade.fun/nightly/json",
    ];

    public const int MaxResponseBytes = 8 * 1024 * 1024;
    public const int MaxServers = 5000;
    private const int MaxText = 256;

    /// <summary>Every server any master server lists, merged; and how many master servers answered.</summary>
    public async Task<(IReadOnlyList<PublicServer> Servers, int Answered)> GetAsync(CancellationToken cancel = default)
    {
        var lists = await Task.WhenAll(MasterLists.Select(url => TryGetAsync(url, cancel)));
        var servers = lists.Where(l => l != null).SelectMany(l => l!)
            .GroupBy(s => s.Address, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .Take(MaxServers).ToList();
        return (servers, lists.Count(l => l != null));
    }

    private async Task<List<PublicServer>?> TryGetAsync(string url, CancellationToken cancel)
    {
        try
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            limit.CancelAfter(TimeSpan.FromSeconds(15));
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, limit.Token);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxResponseBytes)
                return null;
            await using var stream = await response.Content.ReadAsStreamAsync(limit.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, limit.Token)) > 0)
            {
                if (buffer.Length + read > MaxResponseBytes)
                    return null;
                buffer.Write(chunk, 0, read);
            }
            buffer.Position = 0;
            using var json = await JsonDocument.ParseAsync(buffer, cancellationToken: limit.Token);
            return Parse(json.RootElement);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            return null; // one master server down doesn't stop the others
        }
    }

    /// <summary>A master server's JSON (an array of LMP's ServerJson), read defensively: anything odd is skipped.</summary>
    public static List<PublicServer> Parse(JsonElement root)
    {
        var servers = new List<PublicServer>();
        if (root.ValueKind != JsonValueKind.Array)
            return servers;
        foreach (var item in root.EnumerateArray().Take(MaxServers))
        {
            if (item.ValueKind != JsonValueKind.Object || Endpoint(Str(item, "ExternalEndpoint")) is not var (host, port))
                continue;
            var website = Str(item, "Website");
            servers.Add(new PublicServer(
                Clean(Str(item, "ServerName")) is { Length: > 0 } name ? name : host,
                Clean(Str(item, "Description")), host, port, Clean(Str(item, "Version")),
                Bool(item, "Password"), Bool(item, "Cheats"),
                Int(item, "GameMode") switch { 0 => "Sandbox", 1 => "Science", 2 => "Career", _ => "?" },
                Math.Max(0, Int(item, "PlayerCount")), Math.Max(0, Int(item, "MaxPlayers")),
                Clean(Str(item, "Country")), Bool(item, "DedicatedServer"),
                Uri.TryCreate(website, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri.ToString() : null,
                Clean(Str(item, "WebsiteText")) is { Length: > 0 } text ? text : null));
        }
        return servers;
    }

    /// <summary>"1.2.3.4:8800" or "[::1]:8800" → (host, port); null if it isn't a usable public endpoint.</summary>
    public static (string Host, int Port)? Endpoint(string text)
    {
        var colon = text.LastIndexOf(':');
        if (colon <= 0 || !int.TryParse(text[(colon + 1)..], out var port) || port is < 1 or > 65535)
            return null;
        var host = text[..colon].Trim('[', ']');
        return System.Net.IPAddress.TryParse(host, out _) ? (host, port) : null;
    }

    // Server names and descriptions come from strangers: no control characters, no novels.
    private static string Clean(string text)
    {
        var clean = new string(text.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return clean.Length > MaxText ? clean[..MaxText] + "…" : clean;
    }

    private static string Str(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    private static bool Bool(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.True;
    private static int Int(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : -1;
}
