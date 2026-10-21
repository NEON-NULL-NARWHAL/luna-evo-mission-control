// LUNA EVO · LunaEvo.ConfigManager/SettingsPage.cs
// [UI] The LUNA EVO Settings page: colour wheel, theme, behaviour options, versions, folders, links.
// Used by: MainWindow
// Guide:   Meat and Potatoes/04-App-UI.md

using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using LunaEvo.ConfigManager.Core;
using LunaEvo.ConfigManager.Core.Files;
using LunaEvo.ConfigManager.Core.Ops;
using LunaEvo.ConfigManager.Core.Server;
using LunaEvo.ConfigManager.Core.Setup;

namespace LunaEvo.ConfigManager;

/// <summary>The "LUNA EVO" page: the app's own preferences (appearance, behaviour) and an about section.</summary>
internal sealed class SettingsPage
{
    public const string LmpRepository = "https://github.com/LunaMultiplayer/LunaMultiplayer";
    public const string LmpWiki = "https://github.com/LunaMultiplayer/LunaMultiplayer/wiki";
    public const string Nomm = "https://github.com/Combat787/NOMM";
    public const string DotNet6Download = "https://dotnet.microsoft.com/en-us/download/dotnet/6.0";

    private readonly MainWindow _host;
    private readonly ColorView _wheel;
    private readonly TextBox _hex;
    private readonly ComboBox _theme;
    private readonly WrapPanel _swatches = new();
    private readonly StackPanel _about = new() { Spacing = 2 };
    private readonly StackPanel _folders = new() { Spacing = 2 };
    private readonly StackPanel _updates = new() { Spacing = 2 };
    private readonly StackPanel _appUpdates = new() { Spacing = 2 };
    private readonly TextBlock _appStatus = Ui.Text("", "sublabel").WithId("settings/appUpdateStatus");
    private readonly TabSet _tabs = new("settings/tab");
    private readonly TextBlock _homeResult = Ui.Text("", "sublabel").WithId("settings/homeResult");
    private readonly TextBlock _announceResult = Ui.Text("", "sublabel").WithId("settings/announceResult");
    private readonly Dictionary<string, StackPanel> _tabPages = new()
    {
        ["look"] = new(), ["behaviour"] = new(), ["automation"] = new(), ["updates"] = new(), ["downloads"] = new(), ["about"] = new(),
    };
    private readonly TextBlock _dailyResult = Ui.Text("", "sublabel").WithId("settings/dailyResult");
    private readonly TextBox _webhook = new TextBox { Width = 420, PasswordChar = '•' }.WithId("settings/discordWebhook");
    private readonly TextBlock _discordResult = Ui.Text("", "sublabel").WithId("settings/discordResult");
    private string? _updateMessage;
    private bool _updating;

    /// <summary>Builds the page once; <see cref="Refresh"/> updates it when settings or the open server change.</summary>
    public SettingsPage(MainWindow host)
    {
        _host = host;
        var settings = host.Settings;
        var accent = ThemeManager.Parse(settings.AccentColor, Color.Parse(AppSettings.DefaultAccent));

        _wheel = new ColorView
        {
            ColorSpectrumShape = ColorSpectrumShape.Ring,
            IsAlphaEnabled = false,
            IsAlphaVisible = false,
            IsColorPaletteVisible = false,
            IsAccentColorsVisible = false,
            Color = accent,
            Width = 340,
            HorizontalAlignment = HorizontalAlignment.Left,
        }.WithId("settings/accent");
        _wheel.ColorChanged += (_, e) =>
        {
            if (!_updating)
                SetAccent(e.NewColor);
        };

        _hex = new TextBox { Width = 120, Text = ThemeManager.ToHex(accent) }.WithId("settings/accentHex");
        _hex.LostFocus += (_, _) => CommitHex();
        _hex.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter) CommitHex();
        };

        foreach (var (name, hex) in ThemeManager.Presets)
        {
            var color = Color.Parse(hex);
            var swatch = new Button { Background = new SolidColorBrush(color), Tag = hex }.WithId("settings/preset/" + name);
            swatch.Classes.Add("swatch");
            ToolTip.SetTip(swatch, $"{name} ({hex})");
            swatch.Click += (_, _) => SetAccent(color);
            _swatches.Children.Add(swatch);
        }

        _theme = new ComboBox { ItemsSource = new[] { "Dark", "Light", "System" }, SelectedItem = settings.Theme, Width = 160 }.WithId("settings/theme");
        _theme.SelectionChanged += (_, _) =>
        {
            if (!_updating && _theme.SelectedItem is string theme)
                _host.UpdateSettings(s => s with { Theme = theme });
        };

        var resetLook = Ui.Button("Reset to LUNA green", "text").WithId("settings/resetAppearance");
        resetLook.Click += (_, _) =>
        {
            _host.UpdateSettings(s => s with { AccentColor = AppSettings.DefaultAccent, Theme = "Dark" });
            Refresh();
        };

        var page = new StackPanel { Margin = new Thickness(28, 20, 28, 28) };
        page.Children.Add(Ui.Text("LUNA EVO Settings", "headline"));
        var stored = Ui.Text($"Preferences for this app, saved to {host.SettingsPath}", "sublabel");
        stored.Margin = new Thickness(0, 2, 0, 20);
        page.Children.Add(stored);
        page.Children.Add(new Border { Height = 14 });
        _tabs.Add("look", "Appearance", _tabPages["look"]);
        _tabs.Add("behaviour", "Behaviour", _tabPages["behaviour"]);
        _tabs.Add("automation", "Automation & alerts", _tabPages["automation"]);
        _tabs.Add("updates", "Updates", _tabPages["updates"]);
        _tabs.Add("downloads", "Downloads", _tabPages["downloads"]);
        _tabs.Add("about", "About", _tabPages["about"]);
        BuildDownloads();
        _tabs.Changed += key =>
        {
            if (key == "downloads")
                _ = RefreshDownloadsAsync();
        };
        page.Children.Add(_tabs.Strip);
        page.Children.Add(_tabs.Content);

        // Appearance
        var (appearance, look, _) = Ui.Group("Appearance");
        look.Children.Add(Ui.Row("Accent colour", "Pick any colour on the wheel, or type a hex code. The whole theme is generated from it.", _hex));
        var wheelRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(12, 4, 12, 12) };
        wheelRow.Children.Add(_wheel);
        var presets = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(24, 8, 0, 0),
            Children = { Ui.Text("Presets", "sublabel"), _swatches, resetLook },
        };
        Grid.SetColumn(presets, 1);
        wheelRow.Children.Add(presets);
        look.Children.Add(wheelRow);
        look.Children.Add(Ui.Row("Theme", "Dark like NOMM, light, or follow Windows.", _theme));
        _tabPages["look"].Children.Add(appearance);

        // Behaviour
        var (behaviour, rows, _) = Ui.Group("Behaviour");
        rows.Children.Add(Ui.Row("Open the last server on startup", "When LUNA EVO isn't sitting next to Server.exe, reopen the folder you used last.",
            Toggle("settings/openLast", s => s.OpenLastServer, (s, v) => s with { OpenLastServer = v })));
        rows.Children.Add(Ui.Row("Config backups to keep", $"Older folders in Config/{BackupService.BackupFolderName} are deleted after each save. 0 keeps them all.",
            Number("settings/backups", 0, 1000, s => s.BackupsToKeep, (s, v) => s with { BackupsToKeep = v })));
        rows.Children.Add(Ui.Row("Console lines to keep", "Older lines drop off the live console. The full history is always in the log files.",
            Number("settings/consoleLines", 500, 100_000, s => s.ConsoleLines, (s, v) => s with { ConsoleLines = v })));
        rows.Children.Add(Ui.Row("Seconds to wait for a clean stop", "LMP backs up the universe as it shuts down. After this long it is killed instead.",
            Number("settings/stopTimeout", 5, 600, s => s.StopTimeoutSeconds, (s, v) => s with { StopTimeoutSeconds = v })));
        rows.Children.Add(Ui.Row("Ask before stopping the server", "Stopping disconnects everyone who's playing.",
            Toggle("settings/confirmStop", s => s.ConfirmStop, (s, v) => s with { ConfirmStop = v })));
        rows.Children.Add(Ui.Row("Review changes before saving", "Shows a list of what's about to change, with old and new values.",
            Toggle("settings/reviewChanges", s => s.ReviewChangesBeforeSave, (s, v) => s with { ReviewChangesBeforeSave = v })));
        _tabPages["behaviour"].Children.Add(behaviour);

        // Home server mode
        var (home, homeBody, _) = Ui.Group("Home server mode");
        var homeToggle = Toggle("settings/homeServer", s => s.HomeServerMode, (s, v) => s with { HomeServerMode = v });
        homeToggle.IsCheckedChanged += (_, _) =>
        {
            if (_updating)
                return;
            var on = homeToggle.IsChecked == true;
            var registered = HomeServer.SetStartWithWindows(on, MainWindow.CurrentExe, _host.SettingsPath, _host.SettingsPath != AppSettingsStore.DefaultPath);
            _homeResult.Text = !on ? "Off. LUNA EVO no longer starts with Windows." :
                registered ? "On. LUNA EVO starts with Windows, minimised, and starts the server." :
                "On, except starting with Windows: only the downloaded LUNA EVO exe can do that (not a development build).";
            _host.OnServerStatusChanged(); // keep-awake follows straight away
        };
        homeBody.Children.Add(Ui.Row("Run as a home server",
            "For a PC that hosts all the time: starts with Windows minimised to the notification area, starts the server by itself, " +
            "closing the window hides it there instead of quitting (right-click the icon to quit), and the PC doesn't go to sleep while the server runs.",
            homeToggle));
        homeBody.Children.Add(new Border { Padding = new Thickness(12, 0, 12, 10), Child = _homeResult });
        _tabPages["behaviour"].Children.Add(home);

        // Server automation
        var (automation, auto, _) = Ui.Group("Server automation");
        auto.Children.Add(Ui.Row("Restart after a crash", "If the server stops without being asked, start it again after 10 seconds. Gives up after 3 crashes in 10 minutes.",
            Toggle("settings/autoRestart", s => s.AutoRestartOnCrash, (s, v) => s with { AutoRestartOnCrash = v })));
        var daily = new TextBox { Width = 120, PlaceholderText = "HH:mm", Text = settings.DailyRestartTime }.WithId("settings/dailyRestart");
        daily.LostFocus += (_, _) => CommitDaily(daily);
        daily.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter) CommitDaily(daily);
        };
        auto.Children.Add(Ui.Row("Restart every day at", "24-hour time, e.g. 04:00. Players get a chat warning a minute before. Empty = never.",
            new StackPanel { Spacing = 2, Children = { daily, _dailyResult } }));
        auto.Children.Add(Ui.Row("Back up the Universe every (hours)", "While LUNA EVO runs the server. 0 = off. Backups are on the Profiles & Backups page.",
            Number("settings/universeHours", 0, 168, s => s.UniverseBackupHours, (s, v) => s with { UniverseBackupHours = v })));
        auto.Children.Add(Ui.Row("Universe backups to keep", "Older ones are deleted. 0 keeps them all.",
            Number("settings/universeKeep", 0, 1000, s => s.UniverseBackupsToKeep, (s, v) => s with { UniverseBackupsToKeep = v })));
        _tabPages["automation"].Children.Add(automation);

        // Scheduled announcements
        var (announce, announceBody, _) = Ui.Group("Chat announcements");
        var lines = new TextBox
        {
            AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Height = 110, Width = 460,
            Text = settings.Announcements,
            PlaceholderText = "60 Join our Discord: discord.gg/...\n180 Universe backups run every 3 hours",
        }.WithId("settings/announcements");
        void ShowAnnouncements()
        {
            var (items, problems) = Announcements.Parse(lines.Text);
            _announceResult.Text = problems.Count > 0 ? string.Join(" ", problems) :
                items.Count == 0 ? "None set." : $"{items.Count} announcement(s), sent as /say while LUNA EVO runs the server.";
        }
        lines.LostFocus += (_, _) =>
        {
            _host.UpdateSettings(s => s with { Announcements = lines.Text ?? "" });
            ShowAnnouncements();
        };
        announceBody.Children.Add(Ui.Row("Every N minutes, say...",
            $"One per line: minutes, then the message (at least every {Announcements.MinMinutes} minutes). Counted from when the server starts.",
            new StackPanel { Spacing = 4, Children = { lines, _announceResult } }));
        ShowAnnouncements();
        _tabPages["automation"].Children.Add(announce);

        // Notifications
        var (notify, notifyBody, _) = Ui.Group("Notifications");
        notifyBody.Children.Add(Ui.Row("Windows notifications",
            "When the server starts, stops or crashes, and for alerts (someone guessing the admin password, a banned player trying to join).",
            Toggle("settings/notifyDesktop", s => s.NotifyDesktop, (s, v) => s with { NotifyDesktop = v })));
        notifyBody.Children.Add(Ui.Row("Also players joining and leaving", "On Windows and Discord. Needs live player info or LMP's log (see the Players page).",
            Toggle("settings/notifyPlayers", s => s.NotifyPlayerActivity, (s, v) => s with { NotifyPlayerActivity = v })));
        _webhook.Classes.Add("revealPasswordButton");
        var saveWebhook = Ui.Button("Save", "text").WithId("settings/discordSave");
        saveWebhook.Click += (_, _) => SaveWebhook();
        var removeWebhook = Ui.Button("Remove", "text").WithId("settings/discordRemove");
        removeWebhook.Click += (_, _) =>
        {
            _host.UpdateSettings(s => s with { DiscordWebhookProtected = null });
            _webhook.Text = "";
            ShowWebhookState();
        };
        var test = Ui.Button("Send a test message", "text").WithId("settings/discordTest");
        test.Click += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_webhook.Text) && !SaveWebhook())
                return;
            var url = SecretProtector.Unprotect(_host.Settings.DiscordWebhookProtected);
            _discordResult.Text = url == null
                ? "Save a webhook URL first."
                : await new DiscordNotifier(Http.Client).PostAsync(url, "👋 LUNA EVO is connected to this channel.")
                    ? "Sent! Check the channel."
                    : "Discord didn't accept it. Check the URL.";
        };
        notifyBody.Children.Add(Ui.Row("Discord webhook",
            "Posts the same notifications to a Discord channel. In Discord: channel settings → Integrations → Webhooks → New webhook → Copy URL. " +
            "It's stored encrypted for your Windows account, and posts can never ping @everyone.",
            new StackPanel { Spacing = 4, Children =
            {
                _webhook,
                new StackPanel { Orientation = Orientation.Horizontal, Children = { saveWebhook, removeWebhook, test } },
                _discordResult,
            } }));
        _tabPages["automation"].Children.Add(notify);
        ShowWebhookState();

        // Updates
        var (updates, updatesBody, _) = Ui.Group("LMP updates");
        updatesBody.Children.Add(Ui.Row("Check for new LMP versions", "When a server is opened, ask GitHub if there's a newer Luna Multiplayer server.",
            Toggle("settings/checkUpdates", s => s.CheckForLmpUpdates, (s, v) => s with { CheckForLmpUpdates = v })));
        updatesBody.Children.Add(Ui.Row("Include pre-releases", "Newer, less tested versions. Players need the matching LMP mod version.",
            Toggle("settings/prereleases", s => s.IncludeLmpPrereleases, (s, v) => s with { IncludeLmpPrereleases = v })));
        updatesBody.Children.Add(_updates);
        var (appUpdates, appUpdatesBody, _) = Ui.Group("LUNA EVO updates");
        appUpdatesBody.Children.Add(Ui.Row("Check for new LUNA EVO versions",
            "When LUNA EVO starts, look for a newer release on GitHub. Updating downloads it, checks it against GitHub's SHA-256, and restarts.",
            Toggle("settings/checkAppUpdates", s => s.CheckForAppUpdates, (s, v) => s with { CheckForAppUpdates = v })));
        appUpdatesBody.Children.Add(_appUpdates);
        appUpdatesBody.Children.Add(new Border { Padding = new Thickness(12, 0, 12, 10), Child = _appStatus });
        _tabPages["updates"].Children.Add(appUpdates);
        _tabPages["updates"].Children.Add(updates);

        // About and folders are filled in by Refresh, since they depend on the open server.
        var (about, aboutBody, _) = Ui.Group("About");
        aboutBody.Children.Add(_about);
        _tabPages["about"].Children.Add(about);
        var (folders, foldersBody, _) = Ui.Group("Folders");
        foldersBody.Children.Add(_folders);
        _tabPages["about"].Children.Add(folders);

        var (links, linksBody, _) = Ui.Group("Links");
        linksBody.Children.Add(Ui.Row("Luna Multiplayer on GitHub", LmpRepository, LinkButton("Open", LmpRepository)));
        linksBody.Children.Add(Ui.Row("LMP wiki", "Server setup, mod control and commands.", LinkButton("Open", LmpWiki)));
        linksBody.Children.Add(Ui.Row("Credits",
            "Font: JetBrains Mono (SIL OFL 1.1). Icons: Material Symbols (Apache 2.0). Look inspired by NOMM, the Nuclear Option Mod Manager.",
            LinkButton("NOMM", Nomm)));
        _tabPages["about"].Children.Add(links);

        View = new ScrollViewer { Content = page };
        Refresh();
    }

    public Control View { get; }

    // ---- Downloads: the LMP client, any time after setup, any version (CKAN has its own page)

    private readonly ComboBox _clientKsp = new ComboBox { MinWidth = 360 }.WithId("settings/clientKsp");
    private readonly ComboBox _clientVersion = new ComboBox { MinWidth = 200 }.WithId("settings/clientVersion");
    private readonly TextBlock _clientStatus = Ui.Text("", "sublabel").WithId("settings/clientStatus");
    private IReadOnlyList<LmpRelease> _lmpReleases = [];

    private void BuildDownloads()
    {
        var page = _tabPages["downloads"];
        _clientStatus.TextWrapping = Avalonia.Media.TextWrapping.Wrap;

        var (ckan, ckanBody, _) = Ui.Group("CKAN (KSP mod manager)");
        var goCkan = Ui.Button("Open the CKAN page", "text").WithId("settings/goCkan");
        goCkan.Click += (_, _) => _host.Navigate(MainWindow.CkanKey);
        ckanBody.Children.Add(Ui.Row("Moved to its own page", "Play → CKAN mods: whether CKAN is installed, your CKAN installs, and installing a mod list.", goCkan));
        page.Children.Add(ckan);

        var (client, clientBody, _) = Ui.Group("LMP client mod");
        var install = Ui.Button("Install into this KSP", "fab").WithId("settings/clientInstall");
        install.HorizontalAlignment = HorizontalAlignment.Left;
        install.Click += async (_, _) => await InstallClientAsync();
        _clientKsp.SelectionChanged += (_, _) => ShowClientVersion();
        clientBody.Children.Add(Ui.Row("KSP install", "Your KSP folders, including every install CKAN manages.", _clientKsp));
        clientBody.Children.Add(Ui.Row("LMP version", "Players need the same LMP version as the server. Your LMP settings (server list) are kept; the old version is backed up.", _clientVersion));
        clientBody.Children.Add(new StackPanel { Margin = new Thickness(12, 4, 12, 12), Spacing = 6, Children = { install, _clientStatus } });
        page.Children.Add(client);
    }

    /// <summary>Re-reads the KSP installs and loads the LMP versions from GitHub.</summary>
    internal async Task RefreshDownloadsAsync()
    {
        var folders = new List<string>();
        void Add(string? folder)
        {
            if (folder != null && Core.Join.KspLauncher.IsKspFolder(folder) && !folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
                folders.Add(folder);
        }
        Add(_host.Settings.KspFolder);
        Add(Core.Join.KspLauncher.FindKsp());
        foreach (var instance in CkanTools.Instances())
            Add(instance.Path);
        var selected = _clientKsp.SelectedItem as string;
        _clientKsp.ItemsSource = folders;
        _clientKsp.SelectedItem = selected != null && folders.Contains(selected) ? selected : folders.FirstOrDefault();
        ShowClientVersion();

        if (_lmpReleases.Count == 0)
        {
            try
            {
                _lmpReleases = (await LmpReleaseFeed.GetAllAsync(Http.Client, LmpReleaseFeed.ReleasesUrl, _host.Settings.IncludeLmpPrereleases))
                    .Where(r => LmpClientInstaller.ClientAsset(r) is { Sha256: not null }).ToList();
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                _clientStatus.Text = "Couldn't reach GitHub for LMP's versions.";
            }
            var server = _host.Session?.Schema.ServerVersion;
            _clientVersion.ItemsSource = _lmpReleases.Select(r => r.Tag).ToList();
            // The open server's version first: that's the one players need.
            _clientVersion.SelectedItem = _lmpReleases.FirstOrDefault(r => server != null && server.StartsWith(r.Tag, StringComparison.OrdinalIgnoreCase))?.Tag
                                          ?? _lmpReleases.FirstOrDefault()?.Tag;
        }
    }

    /// <summary>"%LOCALAPPDATA%\LunaEvo\..." instead of a path with the user name in it (screenshots, shared help).</summary>
    internal static string Friendly(string path)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return path.StartsWith(local, StringComparison.OrdinalIgnoreCase) ? "%LOCALAPPDATA%" + path[local.Length..] : path;
    }

    private void ShowClientVersion()
    {
        if (_clientKsp.SelectedItem is not string ksp)
        {
            _clientStatus.Text = "No KSP install found. Set your KSP folder on the Join page.";
            return;
        }
        _clientStatus.Text = Core.Join.KspLauncher.ClientVersion(ksp) is { } version ? $"This KSP has LMP {version}." : "LMP isn't installed in this KSP yet.";
    }

    internal async Task InstallClientAsync()
    {
        if (_clientKsp.SelectedItem is not string ksp || _lmpReleases.FirstOrDefault(r => r.Tag == _clientVersion.SelectedItem as string) is not { } release ||
            LmpClientInstaller.ClientAsset(release) is not { Sha256: { } sha } asset)
        {
            _clientStatus.Text = "Pick a KSP install and an LMP version first.";
            return;
        }
        try
        {
            var done = false;
            var progress = new Progress<double>(p => { if (!done) _clientStatus.Text = $"Downloading LMP {release.Tag}... {p:P0}"; });
            var zip = await new ServerInstaller(Http.Client).DownloadAsync(asset, progress, allowUnverified: false);
            done = true;
            var backup = await Task.Run(() => LmpClientInstaller.Install(zip, ksp, sha));
            _clientStatus.Text = $"Installed LMP {release.Tag} into {ksp}." + (backup != null ? $" The previous version is in {backup}." : "");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or DownloadVerificationException or IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException)
        {
            _clientStatus.Text = "Couldn't install: " + e.Message;
        }
    }

    /// <summary>Switches to a tab: "look", "behaviour", "automation", "updates", "downloads" or "about".</summary>
    internal void ShowTab(string key) => _tabs.Select(key);

    /// <summary>The "LUNA EVO updates" rows: up to date, or the new version with Update and restart.</summary>
    private void RefreshAppUpdates()
    {
        _appUpdates.Children.Clear();
        var check = Ui.Button("Check now", "text").WithId("settings/appCheckNow");
        check.Click += async (_, _) =>
        {
            _appStatus.Text = "Checking GitHub...";
            await _host.CheckForAppUpdateAsync(force: true);
            // A problem is already shown in the row; only say "up to date" here.
            _appStatus.Text = _host.AppUpdateProblem == null && _host.AvailableAppUpdate == null ? $"You have the newest LUNA EVO ({AppVersion})." : "";
        };
        if (_host.AvailableAppUpdate is { } update)
        {
            var install = Ui.Button($"Update to {update.Release.Tag} and restart", "fab", "IconRestart").WithId("settings/appUpdate");
            install.Click += async (_, _) =>
            {
                install.IsEnabled = false;
                _appStatus.Text = "Downloading...";
                var done = false; // late progress reports must not overwrite the result
                var progress = new Progress<double>(p => { if (!done) _appStatus.Text = $"Downloading... {p:P0}"; });
                var message = await _host.UpdateAppAsync(progress);
                done = true;
                _appStatus.Text = message;
                install.IsEnabled = true;
            };
            var notes = update.Release.Notes.Trim();
            var summary = notes.Length == 0 ? "" : " " + (notes.Length > 280 ? notes[..280] + "…" : notes).Replace("\r", "").Replace("\n\n", " · ").Replace("\n", " ");
            _appUpdates.Children.Add(Ui.Row($"LUNA EVO {update.Version} is available", $"You have {AppVersion}.{summary}",
                new StackPanel { Spacing = 4, Children = { install, check } }).WithId("settings/appUpdateRow"));
        }
        else
        {
            _appUpdates.Children.Add(Ui.Row("LUNA EVO version", _host.AppUpdateProblem ?? $"You have {AppVersion}.", check).WithId("settings/appUpdateRow"));
        }
        _appUpdates.Children.Add(Ui.Row("Release history", AppUpdater.ReleasesPage, LinkButton("Open", AppUpdater.ReleasesPage)));
    }

    /// <summary>Re-reads settings and the open server (versions, folders).</summary>
    public void Refresh()
    {
        RefreshAppUpdates();
        var settings = _host.Settings;
        var session = _host.Session;
        _updating = true;
        try
        {
            var accent = ThemeManager.Parse(settings.AccentColor, Color.Parse(AppSettings.DefaultAccent));
            _wheel.Color = accent;
            _hex.Text = ThemeManager.ToHex(accent);
            _theme.SelectedItem = settings.Theme;
            foreach (var swatch in _swatches.Children.OfType<Button>())
                swatch.Classes.Set("selected", string.Equals(swatch.Tag as string, settings.AccentColor, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _updating = false;
        }

        _about.Children.Clear();
        _about.Children.Add(Ui.Row("LUNA EVO Config Manager", $"Version {AppVersion}", null).WithId("settings/appVersion"));
        _about.Children.Add(Ui.Row("Luna Multiplayer server",
            session == null ? "No server open" : $"Version {session.Schema.ServerVersion} · settings read from {Path.Combine(session.ServerDirectory, "Server.dll")}", null)
            .WithId("settings/lmpVersion"));
        if (session != null && DotNetRuntime.Check(session.ServerDirectory) is { } runtime)
        {
            _about.Children.Add(Ui.Row($"Server runtime (.NET {runtime.Major})",
                runtime.Installed
                    ? $"Installed. Server.exe needs {runtime.Framework} {runtime.Major}.x."
                    : $"Not installed: the server can't start until you install the .NET {runtime.Major} Runtime.",
                runtime.Installed ? null : LinkButton("Download", DotNet6Download)));
        }
        _updates.Children.Clear();
        if (session != null)
        {
            var check = Ui.Button("Check now", "text").WithId("settings/checkNow");
            check.Click += async (_, _) =>
            {
                _updateMessage = "Checking...";
                Refresh();
                await _host.CheckForUpdateAsync(force: true);
                _updateMessage = _host.AvailableUpdate == null ? $"You're on the newest version ({session.Schema.ServerVersion})." : null;
                Refresh();
            };
            Control action = check;
            var description = _updateMessage ?? "Your server's version: " + session.Schema.ServerVersion;
            if (_host.AvailableUpdate is { } update)
            {
                var install = Ui.Button($"Update to {update.Tag}", "fab").WithId("settings/updateServer");
                install.Padding = new Thickness(16, 8);
                install.Click += async (_, _) => await UpdateAsync(update);
                action = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { check, install } };
                // A result message ("Updated to...", "Update failed...") wins over the generic offer.
                description = _updateMessage ?? $"LMP {update.Tag} is available (you have {session.Schema.ServerVersion}). Updating keeps Config, Universe, logs and Plugins, " +
                              "and saves the old version in LunaEvo/ServerBackups. Players need the matching LMP mod.";
            }
            _updates.Children.Add(Ui.Row("LMP server version", description, action).WithId("settings/updateRow"));
        }

        _about.Children.Add(Ui.Row("This app runs on", $".NET {Environment.Version} · {RuntimeInformation.OSDescription}", null));

        _folders.Children.Clear();
        if (session != null)
        {
            _folders.Children.Add(FolderRow("Server folder", session.ServerDirectory));
            _folders.Children.Add(FolderRow("Config files", session.ConfigDirectory));
            _folders.Children.Add(FolderRow("Config backups", Path.Combine(session.ConfigDirectory, BackupService.BackupFolderName)));
            _folders.Children.Add(FolderRow("Server logs", LogFiles.Folder(session.ServerDirectory)));
        }
        _folders.Children.Add(FolderRow("LUNA EVO settings", Path.GetDirectoryName(_host.SettingsPath)!));
    }

    public static string AppVersion { get; } =
        typeof(SettingsPage).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(SettingsPage).Assembly.GetName().Version?.ToString() ?? "?";

    private async Task UpdateAsync(Core.Setup.LmpRelease release)
    {
        if (_host.Server?.IsRunning == true)
        {
            await Dialogs.InfoAsync(_host, "Stop the server first", "The server has to be stopped to update it.");
            return;
        }
        if (!await Dialogs.ConfirmAsync(_host, $"Update to LMP {release.Tag}?",
                "LUNA EVO downloads it from GitHub and replaces the server's program files. Config, Universe, logs and Plugins " +
                "aren't touched, and the current version is zipped into LunaEvo/ServerBackups first. Everyone who plays needs the matching LMP mod version.",
                "Update"))
            return;
        _updateMessage = "Downloading...";
        Refresh();
        try
        {
            _updateMessage = await _host.UpdateServerAsync(release);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidDataException or TaskCanceledException or Core.Setup.DownloadVerificationException)
        {
            _updateMessage = $"Update failed: {e.Message}";
        }
        Refresh();
    }

    private void CommitDaily(TextBox box)
    {
        var text = box.Text?.Trim() ?? "";
        if (text.Length > 0 && DailyRestart.ParseTime(text) == null)
        {
            _dailyResult.Text = "Use 24-hour HH:mm, like 04:00.";
            return;
        }
        _host.UpdateSettings(s => s with { DailyRestartTime = text });
        _dailyResult.Text = text.Length == 0 ? "No daily restart." : $"Next restart: {DailyRestart.Next(DateTime.Now, text):ddd HH:mm}";
    }

    private void SetAccent(Color color)
    {
        _host.UpdateSettings(s => s with { AccentColor = ThemeManager.ToHex(color) });
        Refresh();
    }

    private void CommitHex()
    {
        var text = _hex.Text?.Trim() ?? "";
        if (!text.StartsWith('#'))
            text = "#" + text;
        if (Color.TryParse(text, out var color))
            SetAccent(Color.FromRgb(color.R, color.G, color.B));
        else
            _hex.Text = _host.Settings.AccentColor;
    }

    /// <summary>Encrypts and stores the URL in the box, then clears the box. False (with a message) if it isn't a webhook.</summary>
    private bool SaveWebhook()
    {
        var url = _webhook.Text?.Trim() ?? "";
        if (!DiscordNotifier.IsWebhookUrl(url))
        {
            _discordResult.Text = "That isn't a Discord webhook URL (https://discord.com/api/webhooks/...).";
            return false;
        }
        if (SecretProtector.Protect(url) is not { } encrypted)
        {
            _discordResult.Text = "Encrypted storage isn't available on this system, so LUNA EVO won't keep the URL.";
            return false;
        }
        _host.UpdateSettings(s => s with { DiscordWebhookProtected = encrypted });
        _webhook.Text = "";
        ShowWebhookState();
        return true;
    }

    /// <summary>The saved URL is never shown again, only whether there is one.</summary>
    private void ShowWebhookState()
    {
        var saved = SecretProtector.Unprotect(_host.Settings.DiscordWebhookProtected) != null;
        _webhook.PlaceholderText = saved ? "Saved (encrypted). Paste a new URL to replace it." : "https://discord.com/api/webhooks/...";
        _discordResult.Text = saved ? "A webhook is saved." :
            _host.Settings.DiscordWebhookProtected != null ? "The saved webhook can't be read on this Windows account. Paste it again." : "";
    }

    private ToggleSwitch Toggle(string id, Func<AppSettings, bool> get, Func<AppSettings, bool, AppSettings> set)
    {
        var toggle = new ToggleSwitch { OnContent = "", OffContent = "", MinWidth = 0, IsChecked = get(_host.Settings) }.WithId(id);
        toggle.IsCheckedChanged += (_, _) => _host.UpdateSettings(s => set(s, toggle.IsChecked == true));
        return toggle;
    }

    private NumericUpDown Number(string id, int min, int max, Func<AppSettings, int> get, Func<AppSettings, int, AppSettings> set)
    {
        var box = new NumericUpDown { Width = 180, Minimum = min, Maximum = max, Increment = 1, FormatString = "0", Value = get(_host.Settings) }.WithId(id);
        box.ValueChanged += (_, e) =>
        {
            if (e.NewValue is { } value)
                _host.UpdateSettings(s => set(s, (int)value));
        };
        return box;
    }

    private static Button LinkButton(string text, string target)
    {
        var button = Ui.Button(text, "text");
        ToolTip.SetTip(button, target);
        button.Click += (_, _) => Ui.Open(target);
        return button;
    }

    private static Border FolderRow(string label, string path)
    {
        var open = Ui.Button("Open", "text", "IconFolder");
        open.IsEnabled = Directory.Exists(path);
        open.Click += (_, _) => Ui.Open(path);
        return Ui.Row(label, path, open);
    }
}
