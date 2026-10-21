// LUNA EVO · LunaEvo.ConfigManager/BrowserPage.cs
// [UI] Server browser (Play): the public LMP servers from LMP's master servers; Join hands one to the Join page.
// Used by: MainWindow
// Guide:   Meat and Potatoes/23-Server-Browser.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using LunaEvo.ConfigManager.Core;
using LunaEvo.ConfigManager.Core.Join;

namespace LunaEvo.ConfigManager;

internal sealed class BrowserPage
{
    private const int MaxRows = 300;

    private readonly MainWindow _host;
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly TextBox _search = new TextBox { PlaceholderText = "Search names and descriptions...", Width = 320 }.WithId("browser/search");
    private readonly CheckBox _myVersion = new CheckBox { Content = "Only my LMP version", IsChecked = true }.WithId("browser/myVersion");
    private readonly CheckBox _noPassword = new CheckBox { Content = "No password" }.WithId("browser/noPassword");
    private readonly CheckBox _withPlayers = new CheckBox { Content = "Has players" }.WithId("browser/withPlayers");
    private readonly TextBlock _status = Ui.Text("", "sublabel").WithId("browser/status");
    private IReadOnlyList<PublicServer> _servers = [];
    private bool _loading;

    public BrowserPage(MainWindow host)
    {
        _host = host;
        _status.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        _search.TextChanged += (_, _) => ShowList();
        foreach (var box in new[] { _myVersion, _noPassword, _withPlayers })
            box.IsCheckedChanged += (_, _) => ShowList();
        var refresh = Ui.Button("Refresh", "text", "IconRestart").WithId("browser/refresh");
        refresh.Click += async (_, _) => await LoadAsync();

        var header = new DockPanel();
        DockPanel.SetDock(refresh, Dock.Right);
        header.Children.Add(refresh);
        header.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Ui.Text("Server browser", "headline"),
                Ui.Text("Public LMP servers, the same list LMP shows in game (from LMP's master servers). Join one to check it and launch KSP straight into it.", "sublabel"),
            },
        });
        var (group, body, _) = Ui.Group("Public servers");
        body.Children.Add(new StackPanel
        {
            Margin = new Thickness(12, 8, 12, 4), Spacing = 8,
            Children = { _search, new WrapPanel { Children = { _myVersion, _noPassword, _withPlayers } }, _status },
        });
        foreach (var box in new[] { _myVersion, _noPassword, _withPlayers })
            box.Margin = new Thickness(0, 0, 16, 0);
        body.Children.Add(_list);
        View = new ScrollViewer { Content = new StackPanel { Margin = new Thickness(28, 20, 28, 28), Spacing = 16, Children = { header, group } } };
    }

    public Control View { get; }

    internal IReadOnlyList<PublicServer> Servers => _servers;

    /// <summary>First visit loads the list; later visits keep it until Refresh.</summary>
    public void Refresh()
    {
        if (_servers.Count == 0 && !_loading)
            _ = LoadAsync();
        else
            ShowList();
    }

    internal async Task LoadAsync()
    {
        if (_loading)
            return;
        _loading = true;
        _status.Text = "Asking LMP's master servers...";
        try
        {
            var (servers, answered) = await new ServerBrowser(Http.Client).GetAsync();
            _servers = servers;
            _status.Text = answered == 0
                ? "No master server answered. Check your internet connection, then Refresh."
                : $"{servers.Count} public servers from {answered} of {ServerBrowser.MasterLists.Count} master servers.";
        }
        finally
        {
            _loading = false;
        }
        ShowList();
    }

    /// <summary>The LMP client version in your KSP (for "Only my LMP version"), as "0.29.2"; null if none.</summary>
    private string? MyVersion()
    {
        var ksp = _host.Settings.KspFolder is { } k && KspLauncher.IsKspFolder(k) ? k : KspLauncher.FindKsp();
        return ksp != null && KspLauncher.ClientVersion(ksp) is { } v ? v.ToString(v.Build >= 0 ? 3 : 2) : null;
    }

    private void ShowList()
    {
        _list.Children.Clear();
        var mine = _myVersion.IsChecked == true ? MyVersion() : null;
        _myVersion.Content = mine != null || _myVersion.IsChecked != true ? $"Only my LMP version{(mine != null ? $" ({mine})" : "")}" : "Only my LMP version (no LMP client found)";
        var search = _search.Text?.Trim() ?? "";
        var shown = _servers
            .Where(s => mine == null || s.Version == mine)
            .Where(s => _noPassword.IsChecked != true || !s.Password)
            .Where(s => _withPlayers.IsChecked != true || s.Players > 0)
            .Where(s => search.Length == 0 || s.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || s.Description.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.Players).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var server in shown.Take(MaxRows))
        {
            var join = Ui.Button("Join", "text").WithId("browser/join/" + server.Address);
            join.Click += async (_, _) =>
            {
                _host.Navigate(MainWindow.JoinKey);
                await _host.Join.OpenServerAsync(server.Host, server.Port);
            };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Children = { join } };
            if (server.Website != null)
            {
                var site = Ui.Button(server.WebsiteText ?? "Website", "text").WithId("browser/site/" + server.Address);
                ToolTip.SetTip(site, server.Website);
                site.Click += (_, _) => Ui.Open(server.Website);
                buttons.Children.Insert(0, site);
            }
            var details = $"{server.Players}/{server.MaxPlayers} players · {server.GameMode} · LMP {server.Version}" +
                          $"{(server.Password ? " · password" : "")}{(server.Cheats ? " · cheats" : "")}{(server.Country.Length > 0 ? " · " + server.Country : "")}" +
                          $" · {server.Address}{(server.Description.Length > 0 ? Environment.NewLine + server.Description : "")}";
            _list.Children.Add(Ui.Row(server.Name, details, buttons).WithId("browser/server/" + server.Address));
        }
        if (shown.Count > MaxRows)
            _list.Children.Add(Ui.Row($"{shown.Count - MaxRows} more", "Search or filter to narrow the list.", null));
        if (_list.Children.Count == 0 && !_loading)
            _list.Children.Add(Ui.Row(_servers.Count == 0 ? "No servers loaded" : "No server matches", _servers.Count == 0 ? "Press Refresh." : "Change the search or filters.", null));
    }
}
