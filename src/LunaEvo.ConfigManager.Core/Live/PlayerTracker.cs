// LUNA EVO · LunaEvo.ConfigManager.Core/Live/PlayerTracker.cs
// [Core] Player history (who played, when, for how long), notes, and alerts such as admin-password guessing.
// Used by: MainWindow (one per open server), ServerPage (log lines), PlayersPage (live player list, UI)
// Guide:   Meat and Potatoes/16-Players-History-And-Alerts.md

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LunaEvo.ConfigManager.Core.Server;

namespace LunaEvo.ConfigManager.Core.Live;

public enum PlayerEventKind
{
    Joined,
    Left,
    Kicked,
    Banned,

    /// <summary>Someone got the admin password wrong <see cref="PlayerTracker.AlertAttempts"/> times within <see cref="PlayerTracker.AlertWindow"/>.</summary>
    AdminPasswordGuessing,

    /// <summary>A banned player tried to join.</summary>
    BannedPlayerTried,
}

public sealed record PlayerEvent(DateTime Time, PlayerEventKind Kind, string Player, string Detail = "");

/// <summary>Everything LUNA EVO remembers about one player. Never their IP address.</summary>
public sealed class PlayerRecord
{
    public string Name { get; set; } = "";

    /// <summary>LMP's unique ids for this player (needed to unban: LMP's ban list stores ids, not names).</summary>
    public List<string> Ids { get; set; } = [];

    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public int Sessions { get; set; }
    public double MinutesPlayed { get; set; }
    public string? LmpVersion { get; set; }
    public string? KspVersion { get; set; }
    public int WrongAdminPasswords { get; set; }
    public string? BanReason { get; set; }
    public string Note { get; set; } = "";
}

public sealed class PlayerHistoryData
{
    public List<PlayerRecord> Players { get; set; } = [];
    public List<PlayerEvent> Events { get; set; } = [];
}

/// <summary>
/// Builds player history from what LMP 0.29.2 already says:
/// <list type="bullet">
/// <item>Log lines (HandshakeSystem, BanPlayerCommand, KickCommand, AdminMsgReader, ClientConnectionHandler), which LUNA
///   EVO sees for a server it runs or follows. "Left" lines are Debug-level in LMP, so they're often missing.</item>
/// <item>The website's player list (Players page, every 5 s), which catches joins and leaves either way.</item>
/// </list>
/// Stored in &lt;server&gt;/LunaEvo/players.json. LMP's log lines include players' IP addresses; those are never kept.
/// </summary>
public sealed partial class PlayerTracker
{
    public const int AlertAttempts = 3;
    public static readonly TimeSpan AlertWindow = TimeSpan.FromMinutes(10);
    public const int MaxEvents = 1000;
    public const int MaxPlayers = 5000;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    private readonly Func<DateTime> _clock;
    private readonly PlayerHistoryData _data;
    private readonly Dictionary<string, DateTime> _online = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<DateTime>> _wrongAdmin = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> _alerted = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public PlayerTracker(string serverDirectory, Func<DateTime>? clock = null)
    {
        _clock = clock ?? (() => DateTime.Now);
        FilePath = Path.Combine(serverDirectory, "LunaEvo", "players.json");
        _data = Load(FilePath);
    }

    public string FilePath { get; }

    /// <summary>Raised for joins, leaves, kicks, bans and alerts (on the thread that observed them).</summary>
    public event Action<PlayerEvent>? EventRaised;

    public IReadOnlyList<PlayerRecord> Players
    {
        get { lock (_gate) return _data.Players.OrderByDescending(p => p.LastSeen).ToList(); }
    }

    /// <summary>Newest first.</summary>
    public IReadOnlyList<PlayerEvent> Events
    {
        get { lock (_gate) return Enumerable.Reverse(_data.Events).ToList(); }
    }

    public IReadOnlyCollection<string> Online
    {
        get { lock (_gate) return _online.Keys.ToList(); }
    }

    public PlayerRecord? Find(string name)
    {
        lock (_gate) return _data.Players.FirstOrDefault(p => p.Name == name);
    }

    public PlayerRecord? FindById(string id)
    {
        lock (_gate) return _data.Players.FirstOrDefault(p => p.Ids.Contains(id));
    }

    /// <summary>Reads one console/log line. Replayed lines (old log contents) and chat are ignored.</summary>
    public void Observe(LogLine line)
    {
        // Chat is text players type, so it must never be read as a server event.
        if (line.Replayed || line.Kind is not (LogKind.Normal or LogKind.Info or LogKind.Debug or LogKind.Warning))
            return;
        var text = line.Message.Trim();

        if (HandshakeOk().Match(text) is { Success: true } ok)
            Join(ok.Groups["name"].Value, ok.Groups["id"].Value, ok.Groups["lmp"].Value, ok.Groups["ksp"].Value);
        else if (HandshakeFailed().Match(text) is { Success: true } failed && failed.Groups["reason"].Value.Contains("Banned", StringComparison.OrdinalIgnoreCase))
            Raise(new PlayerEvent(_clock(), PlayerEventKind.BannedPlayerTried, failed.Groups["name"].Value));
        else if (Banned().Match(text) is { Success: true } banned)
            Ban(banned.Groups["name"].Value, banned.Groups["reason"].Value);
        else if (Kicking().Match(text) is { Success: true } kicked)
            Raise(new PlayerEvent(_clock(), PlayerEventKind.Kicked, kicked.Groups["name"].Value));
        else if (line.Kind == LogKind.Warning && WrongAdminPassword().Match(text) is { Success: true } wrong)
            WrongAdmin(wrong.Groups["name"].Value);
        else if (Disconnected().Match(text) is { Success: true } gone)
            Leave(gone.Groups["name"].Value);
    }

    /// <summary>The website's list of who's online right now: anyone new joined, anyone missing left.</summary>
    public void ObserveOnline(IReadOnlyCollection<string> players)
    {
        List<string> left;
        lock (_gate)
            left = _online.Keys.Where(p => !players.Contains(p)).ToList();
        foreach (var player in players)
            Join(player, null, null, null);
        foreach (var player in left)
            Leave(player);
    }

    /// <summary>The server stopped: everyone's session ends now.</summary>
    public void ServerStopped()
    {
        foreach (var player in Online)
            Leave(player);
    }

    public void SetNote(string name, string note)
    {
        lock (_gate)
        {
            if (_data.Players.FirstOrDefault(p => p.Name == name) is not { } record)
                return;
            record.Note = note.Trim().Length > 500 ? note.Trim()[..500] : note.Trim();
        }
        Save();
    }

    /// <summary>Wrong admin passwords from this player in the last <see cref="AlertWindow"/>.</summary>
    public int RecentWrongAdminPasswords(string name)
    {
        lock (_gate)
            return _wrongAdmin.TryGetValue(name, out var times) ? times.Count(t => _clock() - t <= AlertWindow) : 0;
    }

    private void Join(string name, string? id, string? lmp, string? ksp)
    {
        if (!ValidName(name))
            return;
        var now = _clock();
        lock (_gate)
        {
            var record = GetOrAdd(name, now);
            if (id is { Length: > 0 } && !record.Ids.Contains(id))
                record.Ids.Add(id);
            if (lmp is { Length: > 0 })
                record.LmpVersion = lmp;
            if (ksp is { Length: > 0 })
                record.KspVersion = ksp;
            record.LastSeen = now;
            if (_online.ContainsKey(name))
            {
                Save();
                return; // already counted (the log line and the website both report a join)
            }
            _online[name] = now;
            record.Sessions++;
        }
        Raise(new PlayerEvent(now, PlayerEventKind.Joined, name));
    }

    private void Leave(string name)
    {
        var now = _clock();
        lock (_gate)
        {
            if (!_online.Remove(name, out var since))
                return;
            var record = GetOrAdd(name, now);
            record.LastSeen = now;
            record.MinutesPlayed += Math.Max(0, (now - since).TotalMinutes);
        }
        Raise(new PlayerEvent(now, PlayerEventKind.Left, name));
    }

    private void Ban(string name, string reason)
    {
        var now = _clock();
        lock (_gate)
            GetOrAdd(name, now).BanReason = reason;
        Raise(new PlayerEvent(now, PlayerEventKind.Banned, name, reason));
    }

    private void WrongAdmin(string name)
    {
        var now = _clock();
        int count;
        lock (_gate)
        {
            GetOrAdd(name, now).WrongAdminPasswords++;
            var times = _wrongAdmin.TryGetValue(name, out var list) ? list : _wrongAdmin[name] = [];
            times.Add(now);
            times.RemoveAll(t => now - t > AlertWindow);
            count = times.Count;
            // One alert per player per window, however fast they guess.
            if (count < AlertAttempts || (_alerted.TryGetValue(name, out var last) && now - last <= AlertWindow))
                return;
            _alerted[name] = now;
        }
        Raise(new PlayerEvent(now, PlayerEventKind.AdminPasswordGuessing, name, $"{count} wrong admin passwords in {AlertWindow.TotalMinutes:0} minutes"));
    }

    private PlayerRecord GetOrAdd(string name, DateTime now)
    {
        if (_data.Players.FirstOrDefault(p => p.Name == name) is { } existing)
            return existing;
        var record = new PlayerRecord { Name = name, FirstSeen = now, LastSeen = now };
        _data.Players.Add(record);
        if (_data.Players.Count > MaxPlayers)
            _data.Players.Remove(_data.Players.OrderBy(p => p.LastSeen).First());
        return record;
    }

    private void Raise(PlayerEvent e)
    {
        lock (_gate)
        {
            _data.Events.Add(e);
            if (_data.Events.Count > MaxEvents)
                _data.Events.RemoveRange(0, _data.Events.Count - MaxEvents);
        }
        Save();
        EventRaised?.Invoke(e);
    }

    /// <summary>Writes players.json (to a temp file first, so a crash mid-write can't corrupt it). Never throws.</summary>
    public void Save()
    {
        try
        {
            string json;
            lock (_gate)
                json = JsonSerializer.Serialize(_data, Json);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // History is a nice-to-have; a locked or read-only folder must never break the server console.
        }
    }

    private static PlayerHistoryData Load(string path)
    {
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<PlayerHistoryData>(File.ReadAllText(path), Json) is { } data)
            {
                data.Players = (data.Players ?? []).Where(p => p is { Name: not null } && ValidName(p.Name)).ToList();
                data.Players.ForEach(p => { p.Ids ??= []; p.Note ??= ""; });
                data.Events = (data.Events ?? []).Where(e => e is { Player: not null }).TakeLast(MaxEvents).ToList();
                return data;
            }
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // A damaged history file starts over rather than stopping the app.
        }
        return new PlayerHistoryData();
    }

    /// <summary>LMP only lets players join with A-Z, a-z, 0-9, "-" and "_" (HandshakeSystemValidator.PlayerNameIsValid).</summary>
    public static bool ValidName(string name) => name.Length is > 0 and <= 64 && ValidNameRegex().IsMatch(name);

    [GeneratedRegex(@"^[A-Za-z0-9_-]+$")]
    private static partial Regex ValidNameRegex();

    // The exact messages LMP 0.29.2 logs (see the classes named in the summary above).
    [GeneratedRegex(@"^Client (?<name>[A-Za-z0-9_-]+) \((?<id>[^)\s]*)\) handshake successful, LMP Version: (?<lmp>\S*), KSP Version: (?<ksp>\S*)$")]
    private static partial Regex HandshakeOk();

    [GeneratedRegex(@"^Client (?<name>[A-Za-z0-9_-]+) \((?<id>[^)\s]*)\) failed to handshake: (?<reason>.*)\. Disconnecting$")]
    private static partial Regex HandshakeFailed();

    [GeneratedRegex(@"^Player '(?<name>[A-Za-z0-9_-]+)' was banned from the server: (?<reason>.*)$")]
    private static partial Regex Banned();

    [GeneratedRegex(@"^Kicking (?<name>[A-Za-z0-9_-]+) from the server$")]
    private static partial Regex Kicking();

    [GeneratedRegex(@"^(?<name>[A-Za-z0-9_-]+): Tried to run an admin command with an invalid password$")]
    private static partial Regex WrongAdminPassword();

    [GeneratedRegex(@"^(?:Client (?<name>[A-Za-z0-9_-]+) disconnected in |(?<name>[A-Za-z0-9_-]+) sent Connection end message)")]
    private static partial Regex Disconnected();
}
