// LUNA EVO · LunaEvo.MissionControl.Core/Tower/TowerServer.cs
// [Core] The tower's channel over HTTP, for KSP Wingman agents on this PC: POST /v1/report and /v1/say with a small
//        JSON body; the answer is the tower's new messages for that plane. Listens on localhost only.
// Used by: TowerPage
// Guide:   Meat and Potatoes/28-Tower.md

using System.Net;
using System.Text;
using System.Text.Json;

namespace LunaEvo.MissionControl.Core.Tower;

public sealed class TowerServer : IDisposable
{
    public const int DefaultPort = 8920;
    private const int MaxBody = 16 * 1024;

    /// <summary>The day's channel log stops growing here (audit 13, T2): a local flood fills no disk.</summary>
    public const long MaxLogBytes = 20 * 1024 * 1024;

    private readonly HttpListener _listener = new();
    private readonly Func<DateTime> _clock;
    private readonly CancellationTokenSource _stop = new();

    public TowerServer(int port, Func<DateTime>? clock = null)
    {
        Port = port;
        _clock = clock ?? (() => DateTime.UtcNow);
        // localhost only: agents on this PC (the rig, or a Wingman next to the host). No firewall prompt, no LAN.
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public int Port { get; }
    public Tower Tower { get; } = new();

    /// <summary>
    /// Where the channel is kept as data, one JSON line per message (with its route or hold), or null for nowhere:
    /// tower-&lt;date&gt;.jsonl in LUNA EVO's logs folder, so a test or a session can be looked at afterwards.
    /// </summary>
    public string? LogPath { get; init; }

    private int _logged;

    /// <summary>Raised (on a pool thread) after the channel changed.</summary>
    public event Action? Changed;

    public void Start()
    {
        _listener.Start();
        _ = Task.Run(Loop);
    }

    private async Task Loop()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return; // stopped
            }
            _ = Task.Run(() => Handle(context));
        }
    }

    private void Handle(HttpListenerContext context)
    {
        var response = context.Response;
        try
        {
            var request = context.Request;
            var (status, body) = Refuse(request.Headers["Origin"], request.Headers["Host"], request.ContentType, Port) is { } why
                ? (403, Error(why))
                : Answer(request.HttpMethod, request.Url?.AbsolutePath ?? "", Read(request));
            var bytes = Encoding.UTF8.GetBytes(body);
            response.StatusCode = status;
            response.ContentType = "application/json";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes);
        }
        catch (Exception e) when (e is HttpListenerException or IOException or ObjectDisposedException)
        {
            // the agent went away mid-answer
        }
        finally
        {
            try { response.Close(); } catch (Exception e) when (e is HttpListenerException or ObjectDisposedException) { }
        }
    }

    private static string? Read(HttpListenerRequest request)
    {
        if (request.ContentLength64 > MaxBody)
            return null;
        var buffer = new byte[MaxBody + 1];
        var total = 0;
        int read;
        while ((read = request.InputStream.Read(buffer, total, buffer.Length - total)) > 0)
        {
            total += read;
            if (total > MaxBody)
                return null;
        }
        return Encoding.UTF8.GetString(buffer, 0, total);
    }

    /// <summary>
    /// Why a request isn't an agent's, or null (audit 13, T1). A web page in the user's browser can send a "simple" POST
    /// to localhost without asking first; browsers mark it with an Origin header, a DNS-rebinding page has the wrong
    /// Host, and a JSON content type makes the browser ask first (and the tower never answers yes). Agents send none of
    /// that.
    /// </summary>
    public static string? Refuse(string? origin, string? host, string? contentType, int port)
    {
        if (!string.IsNullOrEmpty(origin))
            return "not from a browser";
        var hostName = host?.Split(':')[0];
        if (hostName is not ("localhost" or "127.0.0.1") || (port != 0 && host != $"{hostName}:{port}"))
            return "localhost only";
        if (contentType?.Split(';')[0].Trim().Equals("application/json", StringComparison.OrdinalIgnoreCase) != true)
            return "application/json only";
        return null;
    }

    /// <summary>One request, without the network (tested directly): the status code and the JSON answer.</summary>
    public (int Status, string Body) Answer(string method, string path, string? body)
    {
        if (method != "POST")
            return (405, Error("POST only"));
        if (body == null)
            return (413, Error("too big"));
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (Exception e) when (e is JsonException or ArgumentException) // ArgumentException: text that isn't valid UTF-16
        {
            return (400, Error("not JSON"));
        }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Text(root, "callSign", 40) is not { Length: > 0 } callSign)
                return (400, Error("needs a callSign"));
            IReadOnlyList<TowerMessage> messages;
            lock (Tower)
            {
                if (!Tower.Accepts(callSign))
                    return (429, Error("too many planes on the channel"));
                switch (path)
                {
                    case "/v1/report":
                        if (Number(root, "latitude") is not { } lat || Number(root, "longitude") is not { } lon)
                            return (400, Error("needs latitude and longitude"));
                        messages = Tower.Report(new PlaneReport(callSign, lat, lon, Number(root, "altitude") ?? 0, Number(root, "heading") ?? 0,
                            Number(root, "speed") ?? 0, Text(root, "situation", 20) ?? "", Text(root, "phase", 200) ?? ""), _clock());
                        break;
                    case "/v1/say":
                        if (Text(root, "kind", 30) is not { Length: > 0 } kind)
                            return (400, Error("needs a kind"));
                        messages = Tower.Say(callSign, kind, Text(root, "text", 300) ?? "", _clock());
                        break;
                    default:
                        return (404, Error("unknown"));
                }
            }
            WriteLog();
            Changed?.Invoke();
            return (200, JsonSerializer.Serialize(new { messages }, Json));
        }
    }

    /// <summary>Appends the channel's new messages to the log (a failing disk loses the log, not the tower).</summary>
    private void WriteLog()
    {
        if (LogPath == null)
            return;
        List<TowerMessage> fresh;
        lock (Tower)
        {
            fresh = Tower.Channel.Where(m => m.Id > _logged).ToList();
            _logged = Tower.Channel.Count == 0 ? _logged : Tower.Channel[^1].Id;
        }
        if (fresh.Count == 0)
            return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxLogBytes)
                return;
            File.AppendAllLines(LogPath, fresh.Select(m => JsonSerializer.Serialize(m, Json)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static string Error(string why) => JsonSerializer.Serialize(new { error = why });

    /// <summary>A string field, cut to a length, without control characters; null when missing or not a string.</summary>
    private static string? Text(JsonElement root, string name, int max)
    {
        if (!root.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.String)
            return null;
        var s = new string((v.GetString() ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        return s.Length <= max ? s : s[..max];
    }

    private static double? Number(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) && double.IsFinite(d) ? d : null;

    public void Dispose()
    {
        _stop.Cancel();
        try { _listener.Stop(); _listener.Close(); } catch (ObjectDisposedException) { }
    }
}
