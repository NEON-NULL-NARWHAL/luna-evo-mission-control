// LUNA EVO · LunaEvo.ConfigManager/PlayersPage.cs
// [UI] Live players, vessels and subspaces from LMP's website JSON, with Kick/Ban; alerts, player history and bans.
// Used by: MainWindow
// Guide:   Meat and Potatoes/11-Live-Players.md, 16-Players-History-And-Alerts.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using LunaEvo.ConfigManager.Core;
using LunaEvo.ConfigManager.Core.Live;
using LunaEvo.ConfigManager.Core.Validation;

namespace LunaEvo.ConfigManager;

internal sealed class PlayersPage : IDisposable
{
    private readonly MainWindow _host;
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly WrapPanel _cards = new() { Margin = new Thickness(0, 16, 0, 8) };
    private readonly Border _banner = new() { Margin = new Thickness(0, 0, 0, 12) };
    private readonly TextBlock _bannerText = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _enableWebsite = Ui.Button("Turn on the website (this PC only)", "text").WithId("players/enableWebsite");
    private readonly StackPanel _players = new() { Spacing = 2 };
    private readonly StackPanel _vessels = new() { Spacing = 2 };
    private readonly TextBlock _updated = Ui.Text("", "sublabel").WithId("players/updated");
    private readonly StackPanel _alerts = new() { Spacing = 2 };
    private readonly Control _alertsGroup;
    private readonly StackPanel _history = new() { Spacing = 2 };
    private readonly TextBox _historySearch = new TextBox { PlaceholderText = "Search players...", Width = 260 }.WithId("players/historySearch");
    private readonly StackPanel _bans = new() { Spacing = 2 };
    private readonly TabSet _tabs = new("players/tab");
    private ServerSnapshot? _last;
    private bool _polling;

    public PlayersPage(MainWindow host)
    {
        _host = host;
        _banner.Classes.Add("banner");
        _banner.Child = new StackPanel { Spacing = 6, Children = { _bannerText, _enableWebsite } };
        _enableWebsite.Click += (_, _) => EnableWebsite();

        var refresh = Ui.Button("Refresh", "text", "IconRestart").WithId("players/refresh");
        refresh.Click += async (_, _) => await PollAsync();
        var header = new DockPanel();
        DockPanel.SetDock(refresh, Dock.Right);
        header.Children.Add(refresh);
        header.Children.Add(new StackPanel { Spacing = 2, Children = { Ui.Text("Players", "headline"), _updated } });

        var (players, playersBody, _) = Ui.Group("Online now");
        playersBody.Children.Add(_players);
        var (vessels, vesselsBody, _) = Ui.Group("Vessels");
        vesselsBody.Children.Add(_vessels);
        var (alerts, alertsBody, _) = Ui.Group("Alerts");
        alertsBody.Children.Add(_alerts);
        _alertsGroup = alerts;
        var (history, historyBody, _) = Ui.Group("Player history");
        _historySearch.HorizontalAlignment = HorizontalAlignment.Left;
        historyBody.Children.Add(new Border { Padding = new Thickness(12, 8, 12, 4), Child = _historySearch });
        historyBody.Children.Add(_history);
        _historySearch.TextChanged += (_, _) => RefreshHistory();
        var (bans, bansBody, _) = Ui.Group("Bans");
        bansBody.Children.Add(_bans);

        // Alerts stay above the tabs: they matter whichever tab is open.
        _tabs.Add("online", "Online now", new StackPanel { Children = { players, vessels } });
        _tabs.Add("history", "History", history);
        _tabs.Add("bans", "Bans", bans);
        var page = new StackPanel { Margin = new Thickness(28, 20, 28, 28), Children = { header, _cards, _banner, alerts, _tabs.Strip, _tabs.Content } };
        View = new ScrollViewer { Content = page };

        _poll.Tick += async (_, _) => await PollAsync();
        _poll.Start();
        Show(null, Message());
    }

    public Control View { get; }

    /// <summary>The latest data, for tests and the console's join/leave notices.</summary>
    internal ServerSnapshot? Last => _last;

    /// <summary>Fetches the JSON once. Runs every 5 s while a server is up; tests call it directly.</summary>
    internal async Task PollAsync()
    {
        if (_polling || _host.Session is not { } session)
            return;
        var endpoint = ServerInfoClient.EndpointFor(session);
        if (endpoint == null || _host.Server?.IsRunning != true)
        {
            Show(null, Message());
            return;
        }

        _polling = true;
        try
        {
            var snapshot = await new ServerInfoClient(Http.Client).GetAsync(endpoint);
            _last = snapshot;
            _host.Tracker?.ObserveOnline(snapshot.Players);
            Show(snapshot, null);
        }
        catch (Exception e)
        {
            // Whatever answers on the website port is outside our control; this runs from a timer, where an
            // escaping exception would crash the app, so any failure just shows as a message.
            Show(_last, $"Couldn't read the server's live data at {endpoint} ({e.Message}). It can take a few seconds after starting.");
        }
        finally
        {
            _polling = false;
        }
    }

    public void Dispose() => _poll.Stop();

    private string? Message()
    {
        if (_host.Session is not { } session)
            return "Open a server first.";
        if (ServerInfoClient.EndpointFor(session) == null)
            return "Live player info comes from LMP's built-in website, which is off. Turn it on below (only this PC can see it), " +
                   "save, and restart the server.";
        return _host.Server?.IsRunning == true ? null : "Start the server to see who's playing.";
    }

    private void Show(ServerSnapshot? snapshot, string? message)
    {
        _bannerText.Text = message ?? "";
        _banner.IsVisible = message != null;
        _enableWebsite.IsVisible = _host.Session is { } session && ServerInfoClient.EndpointFor(session) == null;

        _cards.Children.Clear();
        _players.Children.Clear();
        _vessels.Children.Clear();
        RefreshHistory();
        if (snapshot == null)
        {
            _updated.Text = "";
            _players.Children.Add(Ui.Row("Nobody yet", null, null));
            return;
        }

        _updated.Text = $"Updated {DateTime.Now:HH:mm:ss} · refreshes every 5 seconds";
        Card("Players", snapshot.MaxPlayers is { } max ? $"{snapshot.Players.Count} / {max}" : snapshot.Players.Count.ToString());
        Card("Vessels", snapshot.Vessels.Count(v => v.Type != "SpaceObject").ToString());
        Card("Asteroids & comets", snapshot.Vessels.Count(v => v.Type == "SpaceObject").ToString());
        Card("Subspaces", snapshot.Subspaces.Count.ToString());
        Card("Server memory", $"{snapshot.BytesUsed / 1024.0 / 1024.0:0} MB");

        if (snapshot.Players.Count == 0)
            _players.Children.Add(Ui.Row("Nobody is online", null, null));
        foreach (var player in snapshot.Players)
        {
            var kick = Ui.Button("Kick", "text").WithId($"players/kick/{player}");
            kick.Click += async (_, _) => await ModerateAsync("kick", player);
            var ban = Ui.Button("Ban", "text").WithId($"players/ban/{player}");
            ban.Click += async (_, _) => await ModerateAsync("ban", player);
            var subspace = snapshot.Subspaces.FirstOrDefault(s => s.Creator == player);
            _players.Children.Add(Ui.Row(player, subspace != null ? $"Warped ahead (subspace {subspace.Id})" : null,
                new StackPanel { Orientation = Orientation.Horizontal, Children = { kick, ban } }).WithId($"players/row/{player}"));
        }

        foreach (var vessel in Ui.ListedVessels(snapshot))
            _vessels.Children.Add(Ui.Row(vessel.Name, Ui.DescribeVessel(vessel), null));
        if (_vessels.Children.Count == 0)
            _vessels.Children.Add(Ui.Row("No vessels yet", "Asteroids and comets aren't listed.", null));
    }

    private async Task ModerateAsync(string command, string player)
    {
        var verb = command == "ban" ? "Ban" : "Kick";
        var reason = await Dialogs.PromptAsync(_host, $"{verb} {player}?",
            command == "ban" ? $"{player} will be disconnected and can't come back. Reason (shown to them):" : $"{player} will be disconnected. Reason (shown to them):",
            "", verb);
        if (reason == null)
            return;
        _host.Server?.SendServerCommand($"/{command} {player}{(reason.Trim().Length > 0 ? " " + reason.Trim() : "")}");
        await Task.Delay(800);
        await PollAsync();
    }

    /// <summary>A join, leave, ban or alert was just recorded.</summary>
    internal void OnPlayerEvent(PlayerEvent e) => RefreshHistory();

    /// <summary>Redraws alerts, player history and bans from the tracker and LMP's ban list.</summary>
    private string? _historyKey;

    internal void RefreshHistory()
    {
        var tracker = _host.Tracker;
        // Runs every 5 s with the poll; redraw only when the history, bans, search or server state changed.
        var bansFile = _host.Session is { } session ? new BanList(session.ServerDirectory).FilePath : null;
        var key = $"{tracker?.GetHashCode()}|{tracker?.Version}|{(bansFile != null && File.Exists(bansFile) ? File.GetLastWriteTimeUtc(bansFile).Ticks : 0)}|" +
                  $"{_historySearch.Text}|{_host.Server?.IsRunning}|{string.Join(",", tracker?.Online ?? [])}|{DateTime.Now:yyyyMMddHH}";
        if (key == _historyKey)
            return;
        _historyKey = key;
        var online = tracker?.Online ?? [];
        var banned = _host.Session is { } s ? new BanList(s.ServerDirectory).Ids() : [];

        _alerts.Children.Clear();
        foreach (var e in (tracker?.Events ?? [])
                     .Where(e => e.Kind is PlayerEventKind.AdminPasswordGuessing or PlayerEventKind.BannedPlayerTried && DateTime.Now - e.Time < TimeSpan.FromDays(1))
                     .Take(20))
        {
            var row = e.Kind == PlayerEventKind.AdminPasswordGuessing
                ? Ui.Row($"{e.Player} is guessing the admin password",
                    $"{e.Detail}, last at {e.Time:HH:mm}. LMP doesn't limit guesses, so kick or ban them, and use a long admin password (General settings).",
                    online.Contains(e.Player) ? Moderation(e.Player, "alert") : null)
                : Ui.Row($"{e.Player} tried to join, but is banned", $"Refused at {e.Time:HH:mm}.", null);
            row.Classes.Add("alert");
            _alerts.Children.Add(row.WithId($"players/alert/{e.Kind}/{e.Player}"));
        }
        _alertsGroup.IsVisible = _alerts.Children.Count > 0;

        _history.Children.Clear();
        var search = _historySearch.Text?.Trim() ?? "";
        var records = (tracker?.Players ?? []).Where(p => search.Length == 0 || p.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var record in records.Take(100))
        {
            var isBanned = record.Ids.Any(banned.Contains);
            var note = Ui.Button(record.Note.Length > 0 ? "Edit note" : "Add note", "text").WithId($"players/note/{record.Name}");
            note.Click += async (_, _) => await EditNoteAsync(record.Name, record.Note);
            _history.Children.Add(Ui.Row(record.Name, Describe(record, online.Contains(record.Name), isBanned), note).WithId($"players/history/{record.Name}"));
        }
        if (records.Count > 100)
            _history.Children.Add(Ui.Row($"{records.Count - 100} more", "Search to find them.", null));
        if (_history.Children.Count == 0)
            _history.Children.Add(Ui.Row(search.Length > 0 ? "No player matches" : "Nobody has played yet",
                search.Length > 0 ? null : "Players appear here once they join while LUNA EVO runs or follows the server.", null));

        _bans.Children.Clear();
        var running = _host.Server?.IsRunning == true;
        foreach (var id in banned)
        {
            var record = tracker?.FindById(id);
            var name = record?.Name ?? "Unknown player";
            var unban = Ui.Button("Unban", "text").WithId($"players/unban/{id}");
            unban.IsEnabled = !running;
            ToolTip.SetTip(unban, running ? "Stop the server first: LMP keeps the ban list in memory while it runs and would put the ban back." : "Lets them join again.");
            unban.Click += async (_, _) => await UnbanAsync(id, name);
            var reason = record?.BanReason is { Length: > 0 } r ? $" · {r}" : "";
            _bans.Children.Add(Ui.Row(name, $"LMP id {Short(id)}{reason}", unban).WithId($"players/ban/{id}"));
        }
        if (_bans.Children.Count == 0)
            _bans.Children.Add(Ui.Row("Nobody is banned", "Ban players from the list above while they're online.", null));
    }

    private static string Describe(PlayerRecord p, bool online, bool banned)
    {
        var parts = new List<string> { online ? "Online now" : $"Last seen {When(p.LastSeen)}" };
        parts.Add($"{p.Sessions} session{(p.Sessions == 1 ? "" : "s")}, {Played(p.MinutesPlayed)} played");
        if (p.LmpVersion is { Length: > 0 })
            parts.Add($"LMP {p.LmpVersion}");
        if (p.WrongAdminPasswords > 0)
            parts.Add($"{p.WrongAdminPasswords} wrong admin password{(p.WrongAdminPasswords == 1 ? "" : "s")}");
        if (banned)
            parts.Add(p.BanReason is { Length: > 0 } reason ? $"banned: {reason}" : "banned");
        var text = string.Join(" · ", parts);
        return p.Note.Length > 0 ? $"{text}\nNote: {p.Note}" : text;
    }

    private static string When(DateTime time) =>
        time.Date == DateTime.Today ? $"today {time:HH:mm}"
        : time.Date == DateTime.Today.AddDays(-1) ? $"yesterday {time:HH:mm}"
        : time.ToString("yyyy-MM-dd HH:mm");

    private static string Played(double minutes) =>
        minutes < 1 ? "under a minute" : minutes < 60 ? $"{minutes:0} min" : $"{(int)(minutes / 60)} h {minutes % 60:0} min";

    private static string Short(string id) => id.Length <= 12 ? id : id[..8] + "...";

    private StackPanel Moderation(string player, string where)
    {
        var kick = Ui.Button("Kick", "text").WithId($"players/{where}/kick/{player}");
        kick.Click += async (_, _) => await ModerateAsync("kick", player);
        var ban = Ui.Button("Ban", "text").WithId($"players/{where}/ban/{player}");
        ban.Click += async (_, _) => await ModerateAsync("ban", player);
        return new StackPanel { Orientation = Orientation.Horizontal, Children = { kick, ban } };
    }

    private async Task EditNoteAsync(string player, string current)
    {
        var note = await Dialogs.PromptAsync(_host, $"Note for {player}",
            "Only you see this. It's kept next to the server in LunaEvo/players.json.", current, "Save");
        if (note == null)
            return;
        _host.Tracker?.SetNote(player, note);
        RefreshHistory();
    }

    private async Task UnbanAsync(string id, string name)
    {
        if (_host.Session is not { } session)
            return;
        if (_host.Server?.IsRunning == true)
        {
            await Dialogs.InfoAsync(_host, "Stop the server first", "LMP keeps its ban list in memory while it runs and would put the ban back the next time anyone is banned.");
            return;
        }
        if (!await Dialogs.ConfirmAsync(_host, $"Unban {name}?", "They'll be able to join again. The old ban list is copied to LunaEvo/Backups first.", "Unban"))
            return;
        try
        {
            new BanList(session.ServerDirectory).Unban(id);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await Dialogs.InfoAsync(_host, "Couldn't unban", e.Message);
        }
        RefreshHistory();
    }

    private void EnableWebsite()
    {
        if (_host.Session is not { } session)
            return;
        session.SetValue(LmpRules.Website, "EnableWebsite", "true");
        session.SetValue(LmpRules.Website, "ListenAddress", "127.0.0.1"); // nobody else on the network can read it
        _host.RefreshAll();
        Show(null, "Website turned on for this PC only. Save, then restart the server.");
    }

    private void Card(string label, string value)
    {
        var card = new Border { Child = new StackPanel { Spacing = 2, Children = { Ui.Text(label, "sublabel"), Ui.Text(value, "value") } } }
            .WithId("players/card/" + label);
        card.Classes.Add("card");
        _cards.Children.Add(card);
    }

}
