// LUNA EVO · LunaEvo.ServerPlugin/RadioRelay.cs
// [Plugin] The radio: the KSC tower's lines (and the agents' calls to it), which Mission Control writes to
//          LunaEvo/radio.jsonl when "Talk on LMP chat" is on, said in LMP's chat as "KSC tower" so every player hears it.
// Used by: LunaEvoPlugin.OnUpdate
// Guide:   Meat and Potatoes/28-Tower.md

using System.Text;
using System.Text.Json;

namespace LunaEvo.ServerPlugin;

/// <summary>
/// Reads new lines from radio.jsonl ({"id","from","to","text"}) and hands them out at most one a second. Every line is
/// said as "KSC tower" (never as an agent's or a player's name: anything on the host can talk on the tower's channel),
/// cut short, without control characters. A missing or unreadable file just means no radio.
/// </summary>
internal sealed class RadioRelay
{
    private readonly string path;

    public RadioRelay(string path) => this.path = path;

    public const string Speaker = "KSC tower";
    public const int MaxText = 200;
    public const int MaxQueue = 20;
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly Queue<string> _queue = new();
    private long _position;
    private long _lastId;
    private DateTime _lastRead = DateTime.MinValue, _lastSaid = DateTime.MinValue;
    private bool _started;

    /// <summary>The next line to say now, or null (nothing new, or too soon after the last one).</summary>
    public string? Next(DateTime now)
    {
        if (now - _lastRead >= Interval)
        {
            _lastRead = now;
            Read();
        }
        if (_queue.Count == 0 || now - _lastSaid < Interval)
            return null;
        _lastSaid = now;
        return _queue.Dequeue();
    }

    private void Read()
    {
        try
        {
            if (!File.Exists(path))
                return;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (!_started)
            {
                // What was said before the server started is old news: start at the end.
                _started = true;
                _position = stream.Length;
                return;
            }
            if (stream.Length < _position)
                _position = 0; // a new file (a new day)
            if (stream.Length - _position > 256 * 1024)
                _position = stream.Length - 256 * 1024; // far behind: only the latest
            stream.Position = _position;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (Say(line) is { } text && _queue.Count < MaxQueue)
                    _queue.Enqueue(text);
            }
            _position = stream.Length;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Mission Control is writing it: next second.
        }
    }

    /// <summary>One radio line as chat text: the tower's own as it is, an agent's as "RigPilot → tower: ...".</summary>
    internal string? Say(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var o = doc.RootElement;
            if (o.ValueKind != JsonValueKind.Object || !o.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number ||
                !id.TryGetInt64(out var n) || n <= _lastId)
                return null;
            _lastId = n;
            static string Field(JsonElement e, string name) =>
                e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            var from = Clean(Field(o, "from"), 40);
            var text = Clean(Field(o, "text"), MaxText);
            if (text.Length == 0)
                return null;
            return from == Speaker ? text : $"{from} → tower: {text}";
        }
        catch (Exception e) when (e is JsonException or ArgumentException)
        {
            return null;
        }
    }

    private static string Clean(string s, int max)
    {
        var plain = new string(s.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return plain.Length <= max ? plain : plain[..max];
    }
}
