// LUNA EVO · LunaEvo.ConfigManager/PlayersPage.cs
// [UI] Live players, vessels and subspaces from LMP's website JSON, with Kick/Ban buttons.
// Used by: MainWindow
// Guide:   Meat and Potatoes/11-Live-Players.md

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
    // KSP's stock body indexes, as LMP sends them in ReferenceBody.
    private static readonly string[] Bodies =
        ["Sun", "Kerbin", "Mun", "Minmus", "Moho", "Eve", "Duna", "Ike", "Jool", "Laythe", "Vall", "Bop", "Tylo", "Gilly", "Pol", "Dres", "Eeloo"];

    private readonly MainWindow _host;
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly WrapPanel _cards = new() { Margin = new Thickness(0, 16, 0, 8) };
    private readonly Border _banner = new() { Margin = new Thickness(0, 0, 0, 12) };
    private readonly TextBlock _bannerText = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _enableWebsite = Ui.Button("Turn on the website (this PC only)", "text").WithId("players/enableWebsite");
    private readonly StackPanel _players = new() { Spacing = 2 };
    private readonly StackPanel _vessels = new() { Spacing = 2 };
    private readonly TextBlock _updated = Ui.Text("", "sublabel").WithId("players/updated");
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

        var page = new StackPanel { Margin = new Thickness(28, 20, 28, 28), Children = { header, _cards, _banner, players, vessels } };
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
            var (joined, left) = ServerInfoClient.Diff(_last, snapshot);
            if (_last != null && _host.Settings.DiscordPlayerEvents)
            {
                foreach (var player in joined)
                    _host.Notify($"👋 **{player}** joined.");
                foreach (var player in left)
                    _host.Notify($"🚪 **{player}** left.");
            }
            _last = snapshot;
            Show(snapshot, null);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException)
        {
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

        foreach (var vessel in snapshot.Vessels.Where(v => v.Type != "SpaceObject").OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).Take(100))
        {
            var body = vessel.ReferenceBody >= 0 && vessel.ReferenceBody < Bodies.Length ? Bodies[vessel.ReferenceBody] : "?";
            _vessels.Children.Add(Ui.Row(vessel.Name, $"{vessel.Type} · {body} · {FormatAltitude(vessel.Altitude)}", null));
        }
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

    private static string FormatAltitude(double metres) =>
        metres >= 1_000_000 ? $"{metres / 1_000_000:0.#} Mm" : metres >= 1000 ? $"{metres / 1000:0.#} km" : $"{metres:0} m";
}
