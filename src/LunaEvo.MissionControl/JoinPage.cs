// LUNA EVO · LunaEvo.MissionControl/JoinPage.cs
// [UI] For players (not hosting): check a friend's server (IP, ping, server info, players, vessels), check your mods, launch KSP into it.
// Used by: MainWindow
// Guide:   Meat and Potatoes/15-Joining-Servers.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LunaEvo.MissionControl.Core;
using LunaEvo.MissionControl.Core.Join;
using LunaEvo.MissionControl.Core.Live;
using LunaEvo.MissionControl.Core.Mods;
using LunaEvo.MissionControl.Core.Ops;
using LunaEvo.MissionControl.Core.Setup;

namespace LunaEvo.MissionControl;

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
    private readonly StackPanel _versionMatch = new() { Spacing = 4 };
    private readonly StackPanel _patches = new() { Spacing = 2 };
    private readonly Button _installMods = Ui.Button("Install the server's mods...", "text").WithId("join/installMods");
    /// <summary>Tests replace this so a check never pings a real address: (address) → result.</summary>
    internal static Func<string, Task<PingResult>> PingServer { get; set; } = address => ServerProbe.PingAsync(address);

    /// <summary>Tests replace this: the clipboard's text (pasting into the address box), or null.</summary>
    internal static Func<MainWindow, Task<string?>> ReadClipboard { get; set; } = async host =>
        host.Clipboard is { } clipboard ? await clipboard.TryGetTextAsync() : null;

    private Version? _serverVersion;  // the LMP version the server runs, when known (server browser, or a pasted invite)
    private Version? _pendingVersion; // set by the server browser for the next check
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
        _address.PastingFromClipboard += async (_, e) =>
        {
            // Avalonia keeps only the first line of a paste in a one-line box, and an invite's address is further down:
            // this paste is ours.
            e.Handled = true;
            await PasteAsync(await ReadClipboard(_host));
        };
        var save = Ui.Button("Save to my servers", "text", "IconSave").WithId("join/save");
        save.Click += (_, _) => SaveCurrent();
        _refresh.Tick += async (_, _) =>
        {
            if (_autoRefresh.IsChecked == true && _server != null)
                await RefreshLiveAsync(); // not DNS and 4 pings every 10 seconds
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
        playBody.Children.Add(new StackPanel { Margin = new Thickness(12, 0, 12, 8), Spacing = 4, Children = { kspRow, _kspStatus, _versionMatch, _patches, _ckanInstances } });
        ShowCkanInstances();
        playBody.Children.Add(Ui.Row("Server password", "Leave empty if the server has none. It's only given to LMP, never kept by LUNA EVO.", _password));
        playBody.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(12, 4, 12, 4),
            Children = { _launch, _addToList, _installMods },
        });
        ToolTip.SetTip(_installMods, "Open the .ckan mod list your host sent; CKAN checks it, then installs it into this KSP.");
        _installMods.Click += async (_, _) => await InstallServerModsAsync();
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
            var result = await Task.Run(() => ModCheck.Check(ModRules.Load(rules), GameDataScanner.ScanCached(Path.Combine(ksp, "GameData"))));
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
        // Hosting yourself: put them on your open server's allowed lists in one go.
        if ((result.PartsNotAllowed.Count > 0 || result.ResourcesNotAllowed.Count > 0) && _host.Session?.HasFile(MainWindow.ModControlFile) == true)
        {
            var allow = Ui.Button("Allow them on my server...", "text").WithId("join/allowAll");
            allow.Click += async (_, _) => await AllowOnMyServerAsync(result);
            _modResults.Children.Add(Ui.Row("Hosting this server?",
                "Add these parts and resources to the allowed lists of the server open in LUNA EVO (saved with a backup).", allow));
        }
    }

    /// <summary>
    /// "Allow them on my server": adds the parts and resources the check found to the open server's LMPModControl.xml
    /// AllowedParts/AllowedResources, after explaining the alternative (mod control off) and its risk. Saved like Save.
    /// </summary>
    internal async Task<bool> AllowOnMyServerAsync(ModCheckResult result)
    {
        if (_host.Session is not { } session || !session.HasFile(MainWindow.ModControlFile))
            return false;
        var name = session.GetValue(Core.Validation.LmpRules.General, "ServerName") ?? "your server";
        if (!await Dialogs.ConfirmAsync(_host, "Allow these on your server?",
                $"This adds {result.PartsNotAllowed.Count} part(s) and {result.ResourcesNotAllowed.Count} resource(s) to the allowed lists of \"{name}\", " +
                "so vessels that use them aren't dropped.\n\n" +
                "A private server may not need an allowed list at all: turning off Mod Control (General settings → Mod Control) skips " +
                "every mod check, so anyone can join with any mods. That's simpler, but nothing stops a player's mismatched or " +
                "unwanted mods from breaking shared vessels and saves. Keep the list if you want your group on the same mods.", "Add them"))
            return false;
        session.SetList(MainWindow.ModControlFile, "AllowedParts", session.GetList(MainWindow.ModControlFile, "AllowedParts").Union(result.PartsNotAllowed, StringComparer.Ordinal));
        session.SetList(MainWindow.ModControlFile, "AllowedResources", session.GetList(MainWindow.ModControlFile, "AllowedResources").Union(result.ResourcesNotAllowed, StringComparer.Ordinal));
        if (!await _host.SaveAsync())
        {
            _modSummary.Text = "Added to the allowed lists but not saved yet: press Save when you're ready.";
            return false;
        }
        _modSummary.Text = $"Added to \"{name}\"'s allowed lists and saved. Restart the server for LMP to use them.";
        return true;
    }

    internal ServerAddress? Server => _server;
    internal LiveInfoResult? Live => _live;

    /// <summary>When <see cref="Live"/> was read (the Map shows a checked server to players who host nothing).</summary>
    internal DateTime LiveAt { get; private set; }
    internal TextBox AddressBox => _address;
    internal TextBox KspBox => _ksp;
    internal TextBox PasswordBox => _password;

    /// <summary>Re-reads only the live info (players, vessels) for the checked server. Never throws.</summary>
    internal async Task RefreshLiveAsync()
    {
        if (_checking || _server is not { } server)
            return;
        _checking = true;
        try
        {
            _live = await ServerProbe.LiveInfoAsync(Http.Client, server);
            LiveAt = DateTime.UtcNow;
            ShowSnapshot(_live.Snapshot);
        }
        catch (Exception)
        {
            // A refresh is best effort; the next one tries again.
        }
        finally
        {
            _checking = false;
        }
    }

    /// <summary>From the server browser: fill in a public server and check it, ready to launch.</summary>
    /// <param name="version">The LMP version the master server lists for it, if any.</param>
    internal async Task OpenServerAsync(string host, int port, string? version = null)
    {
        _tabs.Select("server");
        _address.Text = new ServerAddress(host, port).ToString();
        _pendingVersion = Core.Setup.LmpVersion.Parse(version);
        await CheckAsync();
    }

    internal Version? ServerVersion => _serverVersion;

    /// <summary>
    /// "This server runs LMP 0.30.0; you have 0.29.2" with **Install LMP 0.30.0**, when the server's version is known
    /// and yours is different (or missing). LMP refuses to connect across versions.
    /// </summary>
    private void ShowVersionMatch()
    {
        ShowPatches();
        _versionMatch.Children.Clear();
        var ksp = _ksp.Text?.Trim() ?? "";
        if (_serverVersion is not { } server || !KspLauncher.IsKspFolder(ksp))
            return;
        var mine = KspLauncher.ClientVersion(ksp);
        if (mine != null && SameLmp(mine, server))
        {
            _versionMatch.Children.Add(Ui.Text($"This server runs LMP {Show(server)}, the same as yours.", "sublabel").WithId("join/versionOk"));
            return;
        }
        var install = Ui.Button($"Install LMP {Show(server)}", "text").WithId("join/installClient");
        install.Click += async (_, _) => await InstallMatchingClientAsync();
        var text = Ui.Text(mine == null
            ? $"This server runs LMP {Show(server)}, and this KSP has no LMP client yet."
            : $"This server runs LMP {Show(server)}, but you have {Show(mine)}. LMP only connects when they match.", "sublabel");
        text.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        _versionMatch.Children.Add(new StackPanel { Spacing = 2, Children = { text, install } }.WithId("join/versionMismatch"));
    }

    /// <summary>LUNA EVO's patches for the LMP client, carried inside this exe (absent in builds made without KSP).</summary>
    internal static Func<byte[]?> PatchBytes { get; set; } = () =>
    {
        using var stream = typeof(JoinPage).Assembly.GetManifestResourceStream(ClientPatches.FileName);
        if (stream == null)
            return null;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    };

    /// <summary>"LUNA EVO patches for LMP: installed / Install", for an LMP 0.29 client in the chosen KSP.</summary>
    private void ShowPatches()
    {
        _patches.Children.Clear();
        var ksp = _ksp.Text?.Trim() ?? "";
        if (!KspLauncher.IsKspFolder(ksp) || !ClientPatches.Supports(KspLauncher.ClientVersion(ksp)) || PatchBytes() is not { } bytes)
            return;
        var status = ClientPatches.Status(ksp, bytes);
        var button = Ui.Button(status switch
        {
            PluginStatus.Installed => "Remove",
            PluginStatus.Different => "Update",
            _ => "Install the LUNA EVO patches",
        }, "text").WithId("join/patches");
        button.Click += async (_, _) => await TogglePatchesAsync();
        var text = Ui.Text(status == PluginStatus.NotInstalled
            ? "LUNA EVO's patches for the LMP client: join any server by itself (LMP alone can't with a port other than 8800 " +
              "or a password), no time jumps when you're alone in your subspace, and accurate position times. Nothing other " +
              "players need changes."
            : "LUNA EVO patches are in this KSP: Launch KSP and join connects by itself to any server.", "sublabel").WithId("join/patchesStatus");
        text.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        _patches.Children.Add(text);
        _patches.Children.Add(button);
    }

    internal async Task TogglePatchesAsync()
    {
        var ksp = _ksp.Text?.Trim() ?? "";
        if (!KspLauncher.IsKspFolder(ksp) || PatchBytes() is not { } bytes)
            return;
        var installed = ClientPatches.Status(ksp, bytes) == PluginStatus.Installed;
        try
        {
            if (installed)
            {
                if (!await Dialogs.ConfirmAsync(_host, "Remove the LUNA EVO patches?",
                        "LMP goes back to how it was: joining by itself only on port 8800 without a password, and jumping your game's clock " +
                        "when it falls behind.", "Remove"))
                    return;
                ClientPatches.Remove(ksp);
                _launchResult.Text = "LUNA EVO patches removed from this KSP.";
            }
            else
            {
                if (!await Dialogs.ConfirmAsync(_host, "Install the LUNA EVO patches for LMP?",
                        $"Copies one file into GameData\\{ClientPatches.FolderName} of this KSP. When KSP starts, it fixes these in LMP 0.29:\n" +
                        "- the server address KSP is started with (a port other than 8800 makes LMP stop starting up), and it uses the " +
                        "password saved in LMP's own server list;\n" +
                        "- when you're alone in your subspace and your game falls behind, you get a subspace at your own time instead of " +
                        "a jump that teleports your vessel and cuts its engines;\n" +
                        "- each position LMP sends carries the right time (it was one physics tick early);\n" +
                        "- far fewer lost vessel updates on a jittery connection, and less lag.\n" +
                        "It also adds a LUNA EVO button to KSP's toolbar: your connection, who's online, a clock sync, watching " +
                        "a nearby player's vessel, and LMP's settings in one window.\n" +
                        "No new kind of message is sent, so you play with unpatched players as before. On any other LMP version it turns " +
                        "itself off. Remove takes it out.", "Install"))
                    return;
                ClientPatches.Install(ksp, bytes);
                _launchResult.Text = "LUNA EVO patches installed. Launch KSP and join now connects by itself.";
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _launchResult.Text = e.Message;
        }
        ShowPatches();
    }

    private static bool SameLmp(Version a, Version b) => a.Major == b.Major && a.Minor == b.Minor && Math.Max(a.Build, 0) == Math.Max(b.Build, 0);

    private static string Show(Version v) => v.ToString(v.Build >= 0 ? 3 : 2);

    /// <summary>Install LMP x.y.z: the server's version of the LMP client, into this KSP (verified, old version backed up).</summary>
    internal async Task InstallMatchingClientAsync()
    {
        var ksp = _ksp.Text?.Trim() ?? "";
        if (_serverVersion is not { } server || !KspLauncher.IsKspFolder(ksp))
            return;
        if (KspLauncher.IsRunning(ksp))
        {
            _launchResult.Text = "Close KSP first, then install the LMP client.";
            return;
        }
        if (!await Dialogs.ConfirmAsync(_host, $"Install LMP {Show(server)}?",
                $"Into {ksp}: LMP's official client from GitHub, checked against its SHA-256. Your LMP server list is kept, " +
                "the version you have now is backed up, and other mods aren't touched.", "Install"))
            return;
        try
        {
            _launchResult.Text = "Asking GitHub for LMP's releases...";
            var releases = await LmpReleaseFeed.GetAllAsync(Http.Client, LmpReleaseFeed.ReleasesUrl, includePrereleases: true);
            if (releases.FirstOrDefault(r => r.Version is { } v && SameLmp(v, server) && LmpClientInstaller.ClientAsset(r) is { Sha256: not null }) is not { } release)
            {
                _launchResult.Text = $"GitHub has no verifiable LMP {Show(server)} client. Ask your host which version to use.";
                return;
            }
            var done = false;
            var progress = new Progress<double>(p => { if (!done) _launchResult.Text = $"Downloading LMP {release.Tag}... {p:P0}"; });
            var backup = await LmpClientInstaller.DownloadAndInstallAsync(Http.Client, release, ksp, progress);
            done = true;
            _launchResult.Text = $"Installed LMP {release.Tag}." + (backup != null ? $" The previous version is in {backup}." : "");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or DownloadVerificationException or IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or System.Text.Json.JsonException)
        {
            _launchResult.Text = "Couldn't install the LMP client: " + e.Message;
        }
        ShowKspStatus();
    }

    /// <summary>
    /// Install the server's mods: the host's .ckan mod list (CKAN mods → Export mod list, or Connect → Export my mod list)
    /// goes to the CKAN page for this KSP, which checks it with CKAN straight away. Installing is one more button there.
    /// </summary>
    internal async Task InstallServerModsAsync()
    {
        if (await CkanPage.PickModList(_host) is not { } path)
            return;
        try
        {
            if (new FileInfo(path).Length > CkanPage.MaxListBytes)
            {
                _launchResult.Text = "That file is too big for a mod list (over 5 MB).";
                return;
            }
            var text = await File.ReadAllTextAsync(path);
            _host.Navigate(MainWindow.CkanKey);
            await _host.Ckan.LoadForInstallAsync(text, _ksp.Text?.Trim());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _launchResult.Text = "Couldn't read that file: " + e.Message;
        }
    }

    /// <summary>
    /// A paste into the address box. A whole LUNA EVO invite: its address goes in the box, its LMP version is kept, and
    /// the server is looked up at once. Anything else is pasted as one line (line breaks become spaces).
    /// </summary>
    internal async Task PasteAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        if (ServerAddress.InviteAddress(text) is { } address)
        {
            _pendingVersion = ServerAddress.InviteVersion(text);
            _address.Text = address;
            _address.CaretIndex = address.Length;
            await CheckAsync();
            return;
        }
        _address.SelectedText = string.Join(" ", text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));
    }

    /// <summary>Looks the server up: addresses, ping, and live info if the host shares it.</summary>
    internal async Task CheckAsync()
    {
        if (_checking)
            return;
        // The server's LMP version: from the server browser, or the "LMP version:" line of a pasted invite.
        _serverVersion = _pendingVersion ?? ServerAddress.InviteVersion(_address.Text);
        _pendingVersion = null;
        ShowVersionMatch();
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

            var ping = await PingServer(addresses[0].Address);
            _network.Children.Add(Ui.Row(
                ping.AnyReply ? $"Ping {ping.AverageMs} ms" : "No reply to ping",
                ping.AnyReply
                    ? $"Best {ping.BestMs} ms, {ping.Received} of {ping.Sent} replies. Under ~100 ms plays well; LMP copes with more."
                    : "Many PCs block ping in their firewall, so this doesn't mean the server is down. LMP itself uses UDP, which can only be tested by joining.",
                null).WithId("join/ping"));

            _live = await ServerProbe.LiveInfoAsync(Http.Client, server);
            LiveAt = DateTime.UtcNow;
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
        var patched = ClientPatches.IsInstalled(ksp);
        var mode = KspLauncher.PlanFor(server, hasPassword, patched);
        var listed = AddToList(showResult: false);
        if (mode == JoinMode.PickFromList && !listed)
            return; // AddToList explained why

        // Launch the way CKAN's Play button does for this install (its options), then LMP's connect option.
        bool started;
        try
        {
            started = StartKsp(ksp, [.. CkanTools.LaunchOptions(ksp), .. KspLauncher.LaunchArguments(server, hasPassword, patched)]);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _launchResult.Text = $"Windows didn't start KSP: {e.Message}";
            return;
        }
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
        ShowVersionMatch();
    }
}
