// LUNA EVO · LunaEvo.ConfigManager/JoinPage.cs
// [UI] For players (not hosting): check a friend's server (IP, ping, server info, players, vessels), check your mods, launch KSP into it.
// Used by: MainWindow
// Guide:   Meat and Potatoes/15-Joining-Servers.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LunaEvo.ConfigManager.Core;
using LunaEvo.ConfigManager.Core.Join;
using LunaEvo.ConfigManager.Core.Live;
using LunaEvo.ConfigManager.Core.Mods;
using LunaEvo.ConfigManager.Core.Setup;

namespace LunaEvo.ConfigManager;

internal sealed class JoinPage : IDisposable
{
    /// <summary>Tests replace this so they never start KSP: (KSP folder, arguments) → started.</summary>
    internal static Func<string, IReadOnlyList<string>, bool> StartKsp { get; set; } =
        (folder, arguments) => KspLauncher.Launch(folder, arguments) != null;

    private readonly MainWindow _host;
    private readonly TextBox _address = new TextBox { PlaceholderText = "Friend's address, e.g. 192.168.1.20 or kerbin.example.com:8800" }.WithId("join/address");
    private readonly NumericUpDown _websitePort = new NumericUpDown { Value = ServerAddress.DefaultWebsitePort, Minimum = 1, Maximum = 65535, Increment = 1, FormatString = "0", Width = 160 }.WithId("join/websitePort");
    private readonly Button _check = Ui.Button("Check server", "fab").WithId("join/check");
    private readonly ToggleSwitch _autoRefresh = new ToggleSwitch { OnContent = "", OffContent = "", MinWidth = 0 }.WithId("join/autoRefresh");
    private readonly StackPanel _saved = new() { Spacing = 2 };
    private readonly Border _banner = new() { IsVisible = false, Margin = new Thickness(0, 0, 0, 12) };
    private readonly TextBlock _bannerText = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly StackPanel _network = new() { Spacing = 2 };
    private readonly WrapPanel _cards = new() { Margin = new Thickness(0, 4, 0, 8) };
    private readonly StackPanel _info = new() { Spacing = 2 };
    private readonly StackPanel _players = new() { Spacing = 2 };
    private readonly StackPanel _vessels = new() { Spacing = 2 };
    private readonly Control _results;
    private readonly TextBox _ksp = new TextBox { PlaceholderText = @"...\steamapps\common\Kerbal Space Program" }.WithId("join/ksp");
    private readonly TextBlock _kspStatus = Ui.Text("", "sublabel").WithId("join/kspStatus");
    private readonly TextBox _password = new TextBox { PlaceholderText = "Only if the server has one", PasswordChar = '•', Width = 260 }.WithId("join/password");
    private readonly Button _launch = Ui.Button("Launch KSP and join", "fab", "IconPlay").WithId("join/launch");
    private readonly Button _addToList = Ui.Button("Add to LMP's server list", "text").WithId("join/addToList");
    private readonly TextBlock _launchResult = Ui.Text("", "sublabel").WithId("join/launchResult");
    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly TextBox _modRules = new TextBox { PlaceholderText = @"...\LMPModControl.xml" }.WithId("join/modRules");
    private readonly Button _modCheck = Ui.Button("Check my mods", "fab").WithId("join/modCheck");
    private readonly TextBlock _modSummary = Ui.Text("", "sublabel").WithId("join/modSummary");
    private readonly StackPanel _modResults = new() { Spacing = 2 };
    private string? _autoModRules;
    private readonly TabSet _tabs = new("join/tab");
    private readonly WrapPanel _ckanInstances = new();

    /// <summary>One button per KSP install CKAN manages, to pick it as the KSP to launch.</summary>
    internal void ShowCkanInstances()
    {
        _ckanInstances.Children.Clear();
        foreach (var instance in CkanTools.Instances())
        {
            var pick = Ui.Button($"CKAN: {instance.Name}", "chip").WithId("join/ckan/" + instance.Name);
            pick.Margin = new Thickness(0, 0, 6, 6);
            pick.Click += (_, _) =>
            {
                _ksp.Text = instance.Path;
                RememberKsp();
            };
            _ckanInstances.Children.Add(pick);
        }
    }
    private ServerAddress? _server;
    private LiveInfoResult? _live;
    private bool _checking;

    public JoinPage(MainWindow host)
    {
        _host = host;
        _password.Classes.Add("revealPasswordButton");
        _banner.Classes.Add("banner");
        _banner.Child = _bannerText;
        _check.Padding = new Thickness(18, 8);
        _check.Click += async (_, _) => await CheckAsync();
        _address.KeyDown += async (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter) await CheckAsync();
        };
        var save = Ui.Button("Save to my servers", "text", "IconSave").WithId("join/save");
        save.Click += (_, _) => SaveCurrent();
        _refresh.Tick += async (_, _) =>
        {
            if (_autoRefresh.IsChecked == true && _server != null)
                await CheckAsync();
        };
        _refresh.Start();

        _ksp.Text = host.Settings.KspFolder ?? KspLauncher.FindKsp() ?? "";
        _ksp.LostFocus += (_, _) => RememberKsp();
        _ksp.TextChanged += (_, _) => ShowKspStatus();
        var browse = Ui.Button("Browse...", "text", "IconFolder");
        browse.Click += async (_, _) =>
        {
            var folders = await _host.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose your Kerbal Space Program folder" });
            if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path)
            {
                _ksp.Text = path;
                RememberKsp();
            }
        };
        _launch.Padding = new Thickness(18, 10);
        _launch.Click += async (_, _) => await LaunchAsync();
        _addToList.Click += (_, _) => AddToList(showResult: true);
        _modCheck.Padding = new Thickness(18, 8);
        _modCheck.Click += async (_, _) => await CheckModsAsync();
        var browseRules = Ui.Button("Browse...", "text", "IconFolder").WithId("join/modRulesBrowse");
        browseRules.Click += async (_, _) =>
        {
            var files = await _host.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose the server's LMPModControl.xml",
                FileTypeFilter = [new FilePickerFileType("LMP mod control") { Patterns = ["*.xml"] }],
            });
            if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
                _modRules.Text = path;
        };

        var addressRow = new DockPanel { Margin = new Thickness(12, 8) };
        DockPanel.SetDock(_check, Dock.Right);
        _check.Margin = new Thickness(8, 0, 0, 0);
        addressRow.Children.Add(_check);
        addressRow.Children.Add(_address);

        var (serverGroup, serverBody, _) = Ui.Group("Server");
        serverBody.Children.Add(addressRow);
        serverBody.Children.Add(Ui.Row("Live info port (TCP)", "The host's LMP website port, 8900 unless they changed it.", _websitePort));
        serverBody.Children.Add(Ui.Row("Refresh every 10 seconds", null, _autoRefresh));
        serverBody.Children.Add(new Border { Padding = new Thickness(4), Child = save });

        var (savedGroup, savedBody, _) = Ui.Group("My servers");
        savedBody.Children.Add(_saved);

        var (networkGroup, networkBody, _) = Ui.Group("Network");
        networkBody.Children.Add(_network);
        var (infoGroup, infoBody, _) = Ui.Group("Server info");
        infoBody.Children.Add(new Border { Padding = new Thickness(8, 8, 8, 0), Child = _cards });
        infoBody.Children.Add(_info);
        var (playersGroup, playersBody, _) = Ui.Group("Players online");
        playersBody.Children.Add(_players);
        var (vesselsGroup, vesselsBody, _) = Ui.Group("Vessels in game");
        vesselsBody.Children.Add(_vessels);
        _results = new StackPanel { IsVisible = false, Children = { networkGroup, infoGroup, playersGroup, vesselsGroup } };

        var (playGroup, playBody, _) = Ui.Group("Play");
        var kspRow = new DockPanel();
        DockPanel.SetDock(browse, Dock.Right);
        kspRow.Children.Add(browse);
        kspRow.Children.Add(_ksp);
        playBody.Children.Add(Ui.Row("Kerbal Space Program", null, null));
        playBody.Children.Add(new StackPanel { Margin = new Thickness(12, 0, 12, 8), Spacing = 4, Children = { kspRow, _kspStatus, _ckanInstances } });
        ShowCkanInstances();
        playBody.Children.Add(Ui.Row("Server password", "Leave empty if the server has none. It's only given to LMP, never kept by LUNA EVO.", _password));
        playBody.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(12, 4, 12, 4),
            Children = { _launch, _addToList },
        });
        playBody.Children.Add(new Border { Padding = new Thickness(12, 0, 12, 12), Child = _launchResult });

        var (modsGroup, modsBody, _) = Ui.Group("Check my mods");
        var rulesRow = new DockPanel();
        DockPanel.SetDock(browseRules, Dock.Right);
        rulesRow.Children.Add(browseRules);
        rulesRow.Children.Add(_modRules);
        modsBody.Children.Add(Ui.Row("The server's mod rules",
            "When you join a server with mod control on, LMP saves its rules in your KSP (filled in below). Or ask the host for " +
            "theirs: in LUNA EVO, Mod Control → \"Share these rules with players\".", null));
        modsBody.Children.Add(new StackPanel { Margin = new Thickness(12, 0, 12, 8), Spacing = 8, Children =
        {
            rulesRow,
            _modCheck,
            _modSummary,
        } });
        _modCheck.HorizontalAlignment = HorizontalAlignment.Left;
        _modSummary.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        modsBody.Children.Add(_modResults);

        // Play stays below the tabs, so launching is always one click away.
        _tabs.Add("server", "Server", new StackPanel { Children = { serverGroup, _results } });
        _tabs.Add("saved", "My servers", savedGroup);
        _tabs.Add("mods", "Check my mods", modsGroup);
        View = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(28, 20, 28, 28),
                Children =
                {
                    Ui.Text("Join a server", "headline"),
                    Ui.Text("For players: check a friend's server before you join, then launch KSP straight into it. You don't need to host anything.", "sublabel"),
                    new Border { Height = 16 },
                    _banner, _tabs.Strip, _tabs.Content, playGroup,
                },
            },
        };
        ShowSaved();
        ShowKspStatus();
    }

    public Control View { get; }

    internal TextBox ModRulesBox => _modRules;

    /// <summary>
    /// Scans the KSP folder's GameData and checks it against the rules file, the way LMP's client will when you
    /// connect (see <see cref="ModCheck"/>).
    /// </summary>
    internal async Task CheckModsAsync()
    {
        var ksp = _ksp.Text?.Trim() ?? "";
        var rules = _modRules.Text?.Trim() ?? "";
        _modResults.Children.Clear();
        if (!KspLauncher.IsKspFolder(ksp))
        {
            _modSummary.Text = "Choose your Kerbal Space Program folder under Play first.";
            return;
        }
        if (!File.Exists(rules))
        {
            _modSummary.Text = "Choose the server's LMPModControl.xml first.";
            return;
        }

        _modCheck.IsEnabled = false;
        _modSummary.Text = "Scanning your GameData (this can take a minute with many mods)...";
        try
        {
            var result = await Task.Run(() => ModCheck.Check(ModRules.Load(rules), GameDataScanner.Scan(Path.Combine(ksp, "GameData"))));
            ShowModResult(result);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or System.Xml.XmlException)
        {
            _modSummary.Text = "Couldn't check: " + e.Message;
        }
        finally
        {
            _modCheck.IsEnabled = true;
        }
    }

    private void ShowModResult(ModCheckResult result)
    {
        var off = _live?.Snapshot?.ModControl == false ? " (this server has mod control off, so LMP won't check anyway)" : "";
        _modSummary.Text = result.Passes
            ? $"Your mods pass this server's check{off}."
            : $"{result.Problems.Count} problem{(result.Problems.Count == 1 ? "" : "s")} LMP will stop you joining for{off}:";
        foreach (var problem in result.Problems.Take(200))
        {
            var title = problem.Kind switch
            {
                ModProblemKind.MissingExpansion => $"Missing expansion: {problem.Item}",
                ModProblemKind.ForbiddenPlugin => $"Banned mod: {problem.Item}",
                ModProblemKind.PluginNotAllowed => $"Not allowed: {problem.Item}",
                ModProblemKind.MissingPlugin => $"Missing mod: {problem.Item}",
                ModProblemKind.WrongPluginVersion => $"Different version: {problem.Item}",
                _ => $"Missing part: {problem.Item}",
            };
            _modResults.Children.Add(Ui.Row(title, problem.Detail, null).WithId("join/modProblem/" + problem.Item));
        }
        if (result.Problems.Count > 200)
            _modResults.Children.Add(Ui.Row($"...and {result.Problems.Count - 200} more", null, null));
        if (result.PartsNotAllowed.Count > 0)
            _modResults.Children.Add(Ui.Row($"{result.PartsNotAllowed.Count} of your parts {(result.PartsNotAllowed.Count == 1 ? "isn't" : "aren't")} on the server's allowed list",
                $"You can still join, but the server drops vessels that use them. For example: {string.Join(", ", result.PartsNotAllowed.Take(8))}.", null)
                .WithId("join/modParts"));
        if (result.ResourcesNotAllowed.Count > 0)
            _modResults.Children.Add(Ui.Row($"{result.ResourcesNotAllowed.Count} of your resources {(result.ResourcesNotAllowed.Count == 1 ? "isn't" : "aren't")} on the server's allowed list",
                $"For example: {string.Join(", ", result.ResourcesNotAllowed.Take(8))}.", null).WithId("join/modResources"));
    }

    internal ServerAddress? Server => _server;
    internal LiveInfoResult? Live => _live;
    internal TextBox AddressBox => _address;
    internal TextBox KspBox => _ksp;
    internal TextBox PasswordBox => _password;

    /// <summary>Looks the server up: addresses, ping, and live info if the host shares it.</summary>
    internal async Task CheckAsync()
    {
        if (_checking)
            return;
        var server = ServerAddress.Parse(_address.Text, (int)(_websitePort.Value ?? ServerAddress.DefaultWebsitePort));
        if (server == null)
        {
            ShowBanner("That doesn't look like a server address. Use a name or IP, optionally with :port (e.g. 192.168.1.20:8800).");
            return;
        }

        _checking = true;
        _check.IsEnabled = false;
        _banner.IsVisible = false;
        try
        {
            _server = server;
            _network.Children.Clear();
            IReadOnlyList<ResolvedAddress> addresses;
            try
            {
                addresses = await ServerProbe.ResolveAsync(server.Host);
            }
            catch (System.Net.Sockets.SocketException)
            {
                addresses = [];
            }
            if (addresses.Count == 0)
            {
                _results.IsVisible = false;
                ShowBanner($"Couldn't find \"{server.Host}\". Check the spelling, or ask your friend for their IP address.");
                return;
            }

            foreach (var address in addresses.Take(4))
                _network.Children.Add(Ui.Row(address.Address, address.Kind, null).WithId("join/ip/" + address.Address));

            var ping = await ServerProbe.PingAsync(addresses[0].Address);
            _network.Children.Add(Ui.Row(
                ping.AnyReply ? $"Ping {ping.AverageMs} ms" : "No reply to ping",
                ping.AnyReply
                    ? $"Best {ping.BestMs} ms, {ping.Received} of {ping.Sent} replies. Under ~100 ms plays well; LMP copes with more."
                    : "Many PCs block ping in their firewall, so this doesn't mean the server is down. LMP itself uses UDP, which can only be tested by joining.",
                null).WithId("join/ping"));

            _live = await ServerProbe.LiveInfoAsync(Http.Client, server);
            _network.Children.Add(Ui.Row(
                _live.Reachable ? $"Live info answered in {_live.LatencyMs} ms" : "No live info",
                _live.Reachable
                    ? $"From the host's LMP website (port {server.WebsitePort})."
                    : $"{_live.Error}. The host can turn it on in LUNA EVO: Connect → \"Share live info with players\". You can still join without it.",
                null).WithId("join/live"));

            ShowSnapshot(_live.Snapshot);
            _results.IsVisible = true;
        }
        catch (Exception e) // runs from button, Enter and timer handlers, so it must never throw
        {
            ShowBanner("Couldn't check the server: " + e.Message);
        }
        finally
        {
            _checking = false;
            _check.IsEnabled = true;
        }
    }

    internal async Task LaunchAsync()
    {
        var server = _server ?? ServerAddress.Parse(_address.Text, (int)(_websitePort.Value ?? ServerAddress.DefaultWebsitePort));
        var ksp = _ksp.Text?.Trim() ?? "";
        if (server == null)
        {
            _launchResult.Text = "Enter the server's address first.";
            return;
        }
        if (!KspLauncher.IsKspFolder(ksp))
        {
            _launchResult.Text = $"Can't find {KspLauncher.ExeName} in that folder. Point it at your Kerbal Space Program folder.";
            return;
        }
        if (KspLauncher.ClientVersion(ksp) == null)
        {
            _launchResult.Text = "LMP isn't installed in this KSP (no GameData/LunaMultiplayer). Install the LMP client mod first.";
            return;
        }
        if (KspLauncher.IsRunning(ksp))
        {
            _launchResult.Text = "KSP is already running. Close it first: LMP can only be told where to connect when KSP starts.";
            return;
        }

        var hasPassword = !string.IsNullOrEmpty(_password.Text);
        var mode = KspLauncher.PlanFor(server, hasPassword);
        var listed = AddToList(showResult: false);
        if (mode == JoinMode.PickFromList && !listed)
            return; // AddToList explained why

        // Launch the way CKAN's Play button does for this install (its options), then LMP's connect option.
        var started = StartKsp(ksp, [.. CkanTools.LaunchOptions(ksp), .. KspLauncher.LaunchArguments(server, hasPassword)]);
        if (!started)
        {
            _launchResult.Text = "KSP didn't start.";
            return;
        }
        _launchResult.Text = mode == JoinMode.AutoConnect
            ? $"KSP is starting. LMP connects to {server} by itself once the main menu loads."
            : $"KSP is starting. At the main menu, open LMP's server list and pick \"{EntryName(server)}\" (it's at the top). " +
              (hasPassword ? "LMP can't be given a password at launch, so it's saved in that entry." : $"LMP can only auto-connect on port {ServerAddress.DefaultPort}.");
        await Task.CompletedTask;
    }

    public void Dispose() => _refresh.Stop();

    /// <returns>True if LMP's server list now has this server.</returns>
    private bool AddToList(bool showResult)
    {
        var server = _server ?? ServerAddress.Parse(_address.Text, (int)(_websitePort.Value ?? ServerAddress.DefaultWebsitePort));
        var ksp = _ksp.Text?.Trim() ?? "";
        if (server == null || !KspLauncher.IsKspFolder(ksp))
        {
            _launchResult.Text = "Enter the server's address and your KSP folder first.";
            return false;
        }
        try
        {
            var added = KspLauncher.AddToServerList(ksp, EntryName(server), server, string.IsNullOrEmpty(_password.Text) ? null : _password.Text);
            if (showResult)
                _launchResult.Text = $"{(added ? "Added" : "Updated")} \"{EntryName(server)}\" in LMP's server list. The previous list is in settings.xml.lunaevo-backup.";
            return true;
        }
        catch (Exception e) when (e is InvalidOperationException or FileNotFoundException or IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            _launchResult.Text = e.Message;
            return false;
        }
    }

    private string EntryName(ServerAddress server)
    {
        var name = _live?.Snapshot?.ServerName;
        return string.IsNullOrWhiteSpace(name) ? server.ToString() : name.Trim();
    }

    private void ShowSnapshot(ServerSnapshot? snapshot)
    {
        _cards.Children.Clear();
        _info.Children.Clear();
        _players.Children.Clear();
        _vessels.Children.Clear();
        if (snapshot == null)
        {
            _info.Children.Add(Ui.Row("No live info from this server", "Server details, players and vessels show up here when the host shares live info.", null));
            _players.Children.Add(Ui.Row("Unknown", null, null));
            _vessels.Children.Add(Ui.Row("Unknown", null, null));
            return;
        }

        Card("Server", snapshot.ServerName);
        Card("Players", snapshot.MaxPlayers is { } max ? $"{snapshot.Players.Count} / {max}" : snapshot.Players.Count.ToString());
        Card("Mode", string.Join(" · ", new[] { snapshot.GameMode, snapshot.GameDifficulty }.Where(s => s.Length > 0)));
        Card("Password", snapshot.HasPassword switch { true => "Required", false => "None", _ => "?" });
        Card("Mod control", snapshot.ModControl switch { true => "On", false => "Off", _ => "?" });
        Card("Warp", snapshot.WarpMode);
        if (snapshot.Description.Length > 0)
            _info.Children.Add(Ui.Row("Description", snapshot.Description, null));
        if (snapshot.ModControl == true)
            _info.Children.Add(Ui.Row("This server checks mods", "Your parts and plugins must be on the host's allowed lists, or LMP will refuse some vessels.", null));
        if (snapshot.HasPassword == true)
            _info.Children.Add(Ui.Row("This server has a password", "Enter it under Play below before launching.", null));

        if (snapshot.Players.Count == 0)
            _players.Children.Add(Ui.Row("Nobody is online", null, null));
        foreach (var player in snapshot.Players)
            _players.Children.Add(Ui.Row(player, snapshot.Subspaces.FirstOrDefault(s => s.Creator == player) is { } subspace ? $"Warped ahead (subspace {subspace.Id})" : null, null)
                .WithId("join/player/" + player));

        foreach (var vessel in Ui.ListedVessels(snapshot))
            _vessels.Children.Add(Ui.Row(vessel.Name, Ui.DescribeVessel(vessel), null).WithId("join/vessel/" + vessel.Name));
        if (_vessels.Children.Count == 0)
            _vessels.Children.Add(Ui.Row("No vessels yet", "Asteroids and comets aren't listed.", null));
    }

    private void Card(string label, string? value)
    {
        var card = new Border { Child = new StackPanel { Spacing = 2, Children = { Ui.Text(label, "sublabel"), Ui.Text(string.IsNullOrEmpty(value) ? "-" : value, "value") } } }
            .WithId("join/card/" + label);
        card.Classes.Add("card");
        _cards.Children.Add(card);
    }

    private void ShowBanner(string text)
    {
        _bannerText.Text = text;
        _banner.IsVisible = true;
    }

    private void SaveCurrent()
    {
        var server = ServerAddress.Parse(_address.Text, (int)(_websitePort.Value ?? ServerAddress.DefaultWebsitePort));
        if (server == null)
        {
            ShowBanner("Enter a server address to save.");
            return;
        }
        var saved = new SavedServer(EntryName(server), server.Host, server.Port, server.WebsitePort);
        _host.UpdateSettings(s => s with
        {
            SavedServers = s.SavedServers.Where(x => !(x.Host.Equals(saved.Host, StringComparison.OrdinalIgnoreCase) && x.Port == saved.Port)).Prepend(saved).ToList(),
        });
        ShowSaved();
    }

    private void ShowSaved()
    {
        _saved.Children.Clear();
        foreach (var saved in _host.Settings.SavedServers)
        {
            var key = $"{saved.Host}:{saved.Port}";
            var check = Ui.Button("Check", "text").WithId("join/saved/check/" + key);
            check.Click += async (_, _) =>
            {
                _tabs.Select("server");
                _address.Text = new ServerAddress(saved.Host, saved.Port).ToString();
                _websitePort.Value = saved.WebsitePort;
                await CheckAsync();
            };
            var remove = Ui.Button("Remove", "text").WithId("join/saved/remove/" + key);
            remove.Click += (_, _) =>
            {
                _host.UpdateSettings(s => s with { SavedServers = s.SavedServers.Where(x => x != saved).ToList() });
                ShowSaved();
            };
            _saved.Children.Add(Ui.Row(saved.Name, new ServerAddress(saved.Host, saved.Port).ToString(),
                new StackPanel { Orientation = Orientation.Horizontal, Children = { check, remove } }).WithId("join/saved/" + key));
        }
        if (_saved.Children.Count == 0)
            _saved.Children.Add(Ui.Row("No saved servers yet", "Check a server, then \"Save to my servers\".", null));
    }

    private void RememberKsp()
    {
        var folder = _ksp.Text?.Trim();
        _host.UpdateSettings(s => s with { KspFolder = string.IsNullOrEmpty(folder) || folder == KspLauncher.FindKsp() ? null : folder });
    }

    private void ShowKspStatus()
    {
        var folder = _ksp.Text?.Trim() ?? "";
        if (!KspLauncher.IsKspFolder(folder))
        {
            _kspStatus.Text = folder.Length == 0 ? "KSP wasn't found automatically. Choose its folder." : $"No {KspLauncher.ExeName} in that folder.";
            return;
        }
        // LMP saves the rules of the last mod-controlled server you joined here; offer it unless the user chose a file.
        var saved = Path.Combine(folder, "GameData", "LunaMultiplayer", "Data", "LMPModControl.xml");
        if (File.Exists(saved) && (string.IsNullOrEmpty(_modRules.Text) || _modRules.Text == _autoModRules))
            _modRules.Text = _autoModRules = saved;

        var client = KspLauncher.ClientVersion(folder);
        _kspStatus.Text = client == null
            ? "Found KSP, but the LMP client mod isn't installed (GameData/LunaMultiplayer)."
            : $"Found KSP with LMP {client}. Your LMP version must match the server's.";
    }
}
