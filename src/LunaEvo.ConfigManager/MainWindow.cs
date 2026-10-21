// LUNA EVO · LunaEvo.ConfigManager/MainWindow.cs
// [UI] The window: navigation rail, pages, status area, save, open server, close prompts.
// Used by: App
// Guide:   Meat and Potatoes/04-App-UI.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LunaEvo.ConfigManager.Core;
using LunaEvo.ConfigManager.Core.Live;
using LunaEvo.ConfigManager.Core.Ops;
using LunaEvo.ConfigManager.Core.Schema;
using LunaEvo.ConfigManager.Core.Setup;
using LunaEvo.ConfigManager.Core.Validation;

namespace LunaEvo.ConfigManager;

/// <summary>
/// NOMM-style shell: navigation rail on the left (Server: console, players, connect, server settings, mod control,
/// profiles; Play; LUNA EVO), with status and the save button at the bottom, page on the right. The config files
/// share one "Server settings" page with a tab per file.
/// </summary>
public sealed class MainWindow : Window
{
    internal const string ConsolePage = "console";
    internal const string PlayersKey = "players";
    internal const string ConnectKey = "connect";
    internal const string ProfilesKey = "profiles";
    internal const string SettingsKey = "settings";
    internal const string JoinKey = "join";
    internal const string CraftsKey = "crafts";
    internal const string VesselsKey = "vessels";
    internal const string ServerSettingsKey = "serverSettings";
    internal const string ModControlFile = "LMPModControl.xml";

    // Tab groups on the Server settings page; files a newer LMP adds land in "Other". Mod control has its own rail entry.
    private static readonly (string Title, string[] Files)[] Sections =
    [
        ("Game", ["GeneralSettings.xml", "GameplaySettings.xml", "WarpSettings.xml", "CraftSettings.xml", "ScreenshotSettings.xml"]),
        ("Network", ["ConnectionSettings.xml", "WebsiteSettings.xml", "MasterServerSettings.xml", "DedicatedServerSettings.xml"]),
        ("Advanced", ["IntervalSettings.xml", "LogSettings.xml", "DebugSettings.xml"]),
    ];

    private readonly StackPanel _nav = new() { Spacing = 1, Margin = new Thickness(0, 0, 10, 0) };
    private readonly ContentControl _page = new();
    private readonly Ellipse _serverDot = new() { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center };
    private readonly Ellipse _consoleDot = new() { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    private readonly TextBlock _serverText = Ui.Text("", "sublabel").WithId("status/server");
    private readonly TextBlock _issuesText = Ui.Text("", "sublabel").WithId("status/issues");
    private readonly TextBlock _saveText = Ui.Text("", "sublabel").WithId("status/save");
    private readonly Button _save = Ui.Button("Save", "fab", "IconSave").WithId("save");
    private readonly List<SettingRow> _rows = [];
    private readonly List<ListEditor> _lists = [];
    private readonly List<ObjectListEditor> _objectLists = [];
    private readonly TextBlock _updateText = Ui.Text("", "sublabel", "modified").WithId("status/update");
    private readonly TextBlock _appUpdateText = Ui.Text("", "sublabel", "modified").WithId("status/appUpdate");
    private readonly Dictionary<string, FileView> _files = [];
    private readonly Dictionary<string, (Button Nav, Control Page)> _pages = [];
    private readonly SettingsPage _settingsPage;
    private readonly JoinPage _joinPage;
    private readonly CraftsPage _craftsPage;
    private ConfigSession? _session;
    private ServerPage? _server;
    private PlayersPage? _players;
    private VesselsPage? _vesselsPage;
    private PlayerTracker? _tracker;
    private ConnectPage? _connect;
    private ProfilesPage? _profilesPage;
    private ModImportPanel? _modImport;
    private string? _current;
    private TabSet? _settingsTabs;
    private (Border Badge, TextBlock Text, Ellipse Dirty)? _settingsNav;
    private bool _closeConfirmed;

    private sealed record FileView(Border Badge, TextBlock BadgeText, Ellipse Dirty, Border Banner, TextBlock BannerText);

    /// <summary>Loads LUNA EVO's settings, applies the theme, builds the rail, then opens the server (from options or the last one used).</summary>
    internal MainWindow(StartupOptions options)
    {
        Title = "LUNA EVO Config Manager";
        Icon = AppIcon;
        Width = 1240;
        Height = 860;
        MinWidth = 960;
        MinHeight = 600;

        SettingsPath = options.SettingsPath ?? AppSettingsStore.DefaultPath;
        Settings = AppSettingsStore.Load(SettingsPath);
        ThemeManager.Apply(ThemeManager.Parse(Settings.AccentColor, Color.Parse(AppSettings.DefaultAccent)), Settings.Theme);

        _save.IsEnabled = false;
        _save.Click += async (_, _) => await SaveAsync();
        ToolTip.SetTip(_save, "Save changed files (Ctrl+S). A timestamped backup is made first.");
        var open = Ui.Button("Open server", "text", "IconFolder").WithId("open");
        open.HorizontalAlignment = HorizontalAlignment.Stretch;
        open.Click += async (_, _) => await PickServerFolderAsync();
        _serverDot.Classes.Add("statusDot");
        _consoleDot.Classes.Add("statusDot");

        var brand = new StackPanel
        {
            Margin = new Thickness(16, 4, 0, 16),
            Children = { Ui.Text("LUNA EVO", "brand"), Ui.Text("Config Manager", "sublabel") },
        };
        var footer = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(0, 12, 0, 0),
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(8, 0), Children = { _serverDot, _serverText } },
                new Border { Margin = new Thickness(8, 0), Child = _issuesText },
                new Border { Margin = new Thickness(8, 0), Child = _saveText },
                new Border { Margin = new Thickness(8, 0), Child = _updateText },
                new Border { Margin = new Thickness(8, 0), Child = _appUpdateText },
                open,
                _save,
            },
        };

        var rail = new DockPanel { Margin = new Thickness(12, 16, 12, 16) };
        DockPanel.SetDock(brand, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        rail.Children.Add(brand);
        rail.Children.Add(footer);
        rail.Children.Add(new ScrollViewer { Content = _nav });

        var divider = new Border { Width = 1, Margin = new Thickness(0, 16) };
        divider[!Border.BackgroundProperty] = divider.GetResourceObservable("OutlineVariant").ToBinding();

        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("260,Auto,*") };
        root.Children.Add(rail);
        Grid.SetColumn(divider, 1);
        root.Children.Add(divider);
        Grid.SetColumn(_page, 2);
        root.Children.Add(_page);
        Content = root;

        KeyDown += async (_, e) =>
        {
            if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
                await SaveAsync();
        };
        Closing += OnClosing;
        Opened += (_, _) =>
        {
            if (TryGetPlatformHandle() is { } handle)
                DesktopNotifier.Attach(handle.Handle);
            if (!Settings.HomeServerMode)
                return;
            // Home server mode: started by Windows at login → straight to the notification area, and the server up.
            if (options.Minimized)
                HideToTray();
            if (_server is { IsRunning: false })
                _ = _server.StartAsync(automatic: true);
        };
        Closed += (_, _) =>
        {
            _server?.Dispose();
            _players?.Dispose();
            _joinPage?.Dispose();
            _tracker?.Save();
            DesktopNotifier.Remove();
        };
        _updateText.IsVisible = false;
        _updateText.PointerPressed += (_, _) => Navigate(SettingsKey);
        _updateText.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);

        _settingsPage = new SettingsPage(this);
        _joinPage = new JoinPage(this);
        _craftsPage = new CraftsPage(this);

        // After an update the previous exe is left as ".old"; it may still be closing, so try again shortly.
        var exe = CurrentExe; // this window's exe, not whatever CurrentExe is in 15 seconds
        AppUpdater.CleanUp(exe);
        _ = Task.Delay(TimeSpan.FromSeconds(15)).ContinueWith(_ => AppUpdater.CleanUp(exe), TaskScheduler.Default);
        _appUpdateText.IsVisible = false;
        _appUpdateText.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
        _appUpdateText.PointerPressed += (_, _) =>
        {
            Navigate(SettingsKey);
            _settingsPage.ShowTab("updates");
        };
        _ = CheckForAppUpdateAsync();

        var serverDirectory = options.ServerDirectory;
        if (serverDirectory == null && Settings.OpenLastServer && Settings.LastServerDirectory is { } last &&
            File.Exists(System.IO.Path.Combine(last, "Server.dll")))
            serverDirectory = last;

        if (serverDirectory != null)
            Open(serverDirectory, options.ConfigDirectory);
        else
            ShowWelcome(null);
    }

    internal static WindowIcon AppIcon { get; } = new(AssetLoader.Open(new Uri("avares://LunaEvo.ConfigManager/Assets/icon.png")));

    internal ConfigSession? Session => _session;
    internal ServerPage? Server => _server;
    internal PlayersPage? Players => _players;
    internal VesselsPage? Vessels => _vesselsPage;
    internal PlayerTracker? Tracker => _tracker;
    internal ConnectPage? Connect => _connect;
    internal ProfilesPage? Profiles => _profilesPage;
    internal ModImportPanel? ModImport => _modImport;
    internal SetupWizard? Wizard { get; private set; }
    internal JoinPage Join => _joinPage;
    internal CraftsPage Crafts => _craftsPage;

    /// <summary>A newer LUNA EVO found on GitHub, if any.</summary>
    internal AppUpdate? AvailableAppUpdate { get; private set; }

    /// <summary>Why the last LUNA EVO update check couldn't tell (offline, private repository...), or null.</summary>
    internal string? AppUpdateProblem { get; private set; }

    /// <summary>The exe that updates replace. Tests point it at a throwaway file.</summary>
    internal static string? CurrentExe { get; set; } = Environment.ProcessPath;

    /// <summary>Starts the updated exe with the same options. Tests replace it so nothing is started.</summary>
    internal static Func<string, IReadOnlyList<string>, bool> StartNewVersion { get; set; } = (exe, arguments) =>
    {
        var info = new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = System.IO.Path.GetDirectoryName(exe)! };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);
        return System.Diagnostics.Process.Start(info) != null;
    };

    /// <summary>Asks GitHub for a newer LUNA EVO (if enabled, or <paramref name="force"/>), and shows a note in the rail.</summary>
    internal async Task CheckForAppUpdateAsync(bool force = false)
    {
        if (!force && !Settings.CheckForAppUpdates)
            return;
        AppUpdateProblem = null;
        try
        {
            AvailableAppUpdate = await new AppUpdater(Http.Client).CheckAsync(Version.Parse(SettingsPage.AppVersion));
        }
        catch (AppUpdateUnavailableException e)
        {
            AppUpdateProblem = e.Message;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            AppUpdateProblem = "Couldn't reach GitHub to check for updates.";
        }
        _appUpdateText.Text = AvailableAppUpdate is { } update ? $"LUNA EVO {update.Version} is available" : "";
        _appUpdateText.IsVisible = AvailableAppUpdate != null;
        _settingsPage.Refresh();
    }

    /// <summary>
    /// Downloads and installs the newer LUNA EVO, then restarts into it. Asks first about unsaved changes and a running
    /// server. Returns a message if it didn't restart.
    /// </summary>
    internal async Task<string> UpdateAppAsync(IProgress<double>? progress = null)
    {
        if (AvailableAppUpdate is not { } update)
            return "There's no update to install.";
        if (!AppUpdater.CanUpdateItself(CurrentExe))
            return $"This copy of LUNA EVO can't replace itself (it's a development build). Download {update.Release.Tag} from {AppUpdater.ReleasesPage}.";

        if (_session is { IsDirty: true })
        {
            var choice = await Dialogs.ChooseAsync(this, "Unsaved changes", "LUNA EVO restarts to update.", "Save and update", "Update without saving", "Cancel");
            if (choice == 0 && !await SaveAsync())
                return "Update cancelled.";
            if (choice is not (0 or 1))
                return "Update cancelled.";
        }
        var stopServer = false;
        if (_server?.IsOwnedActive == true)
        {
            var choice = await Dialogs.ChooseAsync(this, "The server is running",
                "LUNA EVO restarts to update. The server can keep running: the new version follows its log, and console commands " +
                "work again after the server's next restart. Or stop it first (players are disconnected).",
                "Keep it running", "Stop the server first", "Cancel");
            if (choice is not (0 or 1))
                return "Update cancelled.";
            stopServer = choice == 1;
        }

        try
        {
            var downloaded = await new AppUpdater(Http.Client).DownloadAsync(update, progress);
            await Task.Run(() => AppUpdater.Install(downloaded, CurrentExe!, update));
        }
        catch (UnauthorizedAccessException)
        {
            return $"LUNA EVO can't write to {System.IO.Path.GetDirectoryName(CurrentExe)}. Move it to a folder you can write to, or download {update.Release.Tag} from {AppUpdater.ReleasesPage}.";
        }
        catch (Exception e) when (e is DownloadVerificationException or HttpRequestException or IOException or TaskCanceledException)
        {
            return $"Update failed: {e.Message} LUNA EVO {SettingsPage.AppVersion} is still installed.";
        }

        if (stopServer)
            await _server!.StopAsync(confirm: false);
        if (!StartNewVersion(CurrentExe!, Environment.GetCommandLineArgs().Skip(1).ToList()))
            return $"Updated to {update.Release.Tag}, but it didn't start. Start LUNA EVO again.";
        _tracker?.Save();
        _closeConfirmed = true;
        Close();
        return $"Updated to {update.Release.Tag}. Restarting...";
    }

    /// <summary>The newest LMP release found by the update check, if it's newer than the open server.</summary>
    internal LmpRelease? AvailableUpdate { get; private set; }
    internal AppSettings Settings { get; private set; }
    internal string SettingsPath { get; }

    /// <summary>Changes, saves and applies LUNA EVO's own settings.</summary>
    internal void UpdateSettings(Func<AppSettings, AppSettings> change)
    {
        var updated = change(Settings);
        if (updated == Settings)
            return;
        var lookChanged = updated.AccentColor != Settings.AccentColor || updated.Theme != Settings.Theme;
        Settings = updated;
        try
        {
            AppSettingsStore.Save(Settings, SettingsPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Preferences are a convenience; the app works without saving them.
        }
        if (lookChanged)
            ThemeManager.Apply(ThemeManager.Parse(Settings.AccentColor, Color.Parse(AppSettings.DefaultAccent)), Settings.Theme);
    }

    /// <summary>Tests capture notifications here instead of showing them: (title, message, is an alert).</summary>
    internal static Action<string, string, bool> ShowDesktopNotification { get; set; } = DesktopNotifier.Show;

    /// <summary>
    /// Tells the user something happened: a Windows notification (if on) and a Discord post (if a webhook is set).
    /// Joins and leaves only when "players joining and leaving" is on. Fire and forget: it never throws.
    /// </summary>
    internal void Notify(Notification notification)
    {
        if (notification.IsPlayerActivity && !Settings.NotifyPlayerActivity)
            return;
        if (Settings.NotifyDesktop)
        {
            try
            {
                ShowDesktopNotification(notification.Title, notification.Message, notification.IsAlert);
            }
            catch (Exception)
            {
                // A notification that can't be shown is never worth an error.
            }
        }
        if (SecretProtector.Unprotect(Settings.DiscordWebhookProtected) is { } webhook)
            _ = new DiscordNotifier(Http.Client).SendAsync(webhook, notification);
    }

    /// <summary>The open server's name for messages, or a fallback.</summary>
    internal string ServerName =>
        _session?.GetValue(LmpRules.General, "ServerName") is { Length: > 0 } name ? name : "The server";

    private PlayerTracker NewTracker(string serverDirectory)
    {
        var tracker = new PlayerTracker(serverDirectory);
        tracker.EventRaised += e => Dispatcher.UIThread.Post(() => OnPlayerEvent(tracker, e));
        return tracker;
    }

    private void OnPlayerEvent(PlayerTracker tracker, PlayerEvent e)
    {
        if (tracker != _tracker)
            return; // from a server that has since been closed
        _players?.OnPlayerEvent(e);
        Notification? notification = e.Kind switch
        {
            PlayerEventKind.Joined => new(NotificationKind.PlayerJoined, $"{e.Player} joined", ServerName, e.Player),
            PlayerEventKind.Left => new(NotificationKind.PlayerLeft, $"{e.Player} left", ServerName, e.Player),
            PlayerEventKind.AdminPasswordGuessing => new(NotificationKind.AdminPasswordGuessing, "Someone is guessing the admin password",
                $"{e.Player}: {e.Detail}. Kick or ban them on the Players page, and use a long admin password.", e.Player),
            PlayerEventKind.BannedPlayerTried => new(NotificationKind.BannedPlayerTried, "A banned player tried to join",
                $"{e.Player} was refused by {ServerName}.", e.Player),
            _ => null,
        };
        if (notification != null)
            Notify(notification);
    }

    /// <summary>Opens an LMP server folder: reads its schema and configs, then rebuilds every page. Shows the welcome page on failure.</summary>
    internal void Open(string serverDirectory, string? configDirectory) => Open(serverDirectory, configDirectory, keepServerPage: false);

    /// <summary>Re-reads the config from disk (after switching profile), keeping the Server Console and a running server as they are.</summary>
    internal void Reopen()
    {
        if (_session != null)
            Open(_session.ServerDirectory, _session.ConfigDirectory, keepServerPage: true);
    }

    /// <summary>Re-validates and redraws everything after an edit made outside a setting row.</summary>
    internal void RefreshAll() => Refresh();

    private void Open(string serverDirectory, string? configDirectory, bool keepServerPage)
    {
        ConfigSession session;
        try
        {
            session = ConfigSession.Open(serverDirectory, configDirectory);
        }
        catch (Exception e)
        {
            ShowWelcome($"Couldn't open {serverDirectory}: {e.Message}");
            return;
        }

        var current = _current;
        _session = session;
        if (!keepServerPage || _server == null)
        {
            _server?.Dispose();
            _tracker?.Save();
            _tracker = NewTracker(session.ServerDirectory);
            _server = new ServerPage(this, session.ServerDirectory);
            _players?.Dispose();
            _players = new PlayersPage(this);
            _connect = new ConnectPage(this);
            _vesselsPage = new VesselsPage(this);
            _profilesPage = new ProfilesPage(this);
        }
        UpdateSettings(s => s with { LastServerDirectory = session.ServerDirectory });

        _saveText.Text = $"LMP {session.Schema.ServerVersion}";
        ToolTip.SetTip(_saveText, $"Server: {session.ServerDirectory}\nConfig: {session.ConfigDirectory}");
        BuildPages(session);
        if (keepServerPage && current != null)
            Navigate(current);
        _settingsPage.Refresh();
        Refresh();
        if (!keepServerPage)
            _ = CheckForUpdateAsync();
    }

    /// <summary>Looks for a newer LMP on GitHub (if enabled in settings) and shows a note in the rail.</summary>
    internal async Task CheckForUpdateAsync(bool force = false)
    {
        AvailableUpdate = null;
        _updateText.IsVisible = false;
        if (_session == null || (!force && !Settings.CheckForLmpUpdates))
            return;
        try
        {
            var latest = await new LmpReleaseFeed(Http.Client).LatestAsync(Settings.IncludeLmpPrereleases);
            var installed = LmpVersion.OfServer(_session.ServerDirectory);
            if (latest?.Version != null && installed != null && latest.Version > installed)
            {
                AvailableUpdate = latest;
                _updateText.Text = $"LMP {latest.Tag} is available";
                _updateText.IsVisible = true;
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            // Offline or GitHub is down: just don't show an update.
        }
        _settingsPage.Refresh();
    }

    /// <summary>
    /// Updates the LMP server to a release: the server must be stopped. Old binaries are zipped into
    /// LunaEvo/ServerBackups; Config, Universe, logs and Plugins aren't touched. Returns a message for the user.
    /// </summary>
    internal async Task<string> UpdateServerAsync(LmpRelease release, IProgress<double>? progress = null)
    {
        if (_session is not { } session || release.ServerAsset() is not { } asset)
            return "Nothing to update.";
        if (_server?.IsRunning == true)
            return "Stop the server first, then update.";
        if (asset.Sha256 == null && !await Dialogs.ConfirmAsync(this, "Can't verify this download",
                $"GitHub has no checksum for {asset.Name}, so LUNA EVO can't check the file wasn't tampered with on the way. Install it anyway?",
                "Install anyway"))
            return "Update cancelled.";
        var zip = await new ServerInstaller(Http.Client).DownloadAsync(asset, progress, allowUnverified: asset.Sha256 == null);
        await Task.Run(() => new UniverseBackups(session.ServerDirectory).Safety("beforeupdate"));
        var result = await Task.Run(() => ServerInstaller.Install(zip, session.ServerDirectory, asset.Sha256));
        Open(session.ServerDirectory, session.ConfigDirectory); // new Server.dll, maybe new settings
        Navigate(SettingsKey); // back to where the user pressed Update, to see the result
        return $"Updated to LMP {release.Tag}. The previous version is saved in {result.BinariesBackup}.";
    }

    /// <summary>Shows a page, as clicking it in the rail does: a file name, <see cref="ConsolePage"/> or <see cref="SettingsKey"/>.</summary>
    internal void Navigate(string key)
    {
        // A config file on the Server settings page: select its tab, then show that page.
        if (_settingsTabs?.Has(key) == true)
        {
            _settingsTabs.Select(key);
            key = ServerSettingsKey;
        }
        if (!_pages.TryGetValue(key, out var page))
            return;
        if (_current != null && _pages.TryGetValue(_current, out var previous))
            previous.Nav.Classes.Remove("selected");
        page.Nav.Classes.Add("selected");
        _current = key;
        _page.Content = page.Page;
        switch (key)
        {
            case SettingsKey:
                _settingsPage.Refresh();
                break;
            case ConnectKey:
                _connect?.Refresh();
                break;
            case ProfilesKey:
                _profilesPage?.Refresh();
                break;
            case PlayersKey:
                _ = _players?.PollAsync();
                break;
            case CraftsKey:
                _craftsPage.Refresh();
                break;
            case VesselsKey:
                _vesselsPage?.Refresh();
                break;
        }
    }

    private TrayIcon? _tray;
    private bool _quitting;

    /// <summary>Hides the window to the notification area (home server mode). The icon's menu opens it again or quits.</summary>
    internal void HideToTray()
    {
        try
        {
            if (_tray == null && Application.Current is { } app)
            {
                var open = new NativeMenuItem("Open LUNA EVO");
                open.Click += (_, _) => ShowFromTray();
                var quit = new NativeMenuItem("Quit LUNA EVO");
                quit.Click += (_, _) =>
                {
                    ShowFromTray();
                    _quitting = true;
                    Close(); // the usual questions: unsaved changes, stop the server?
                    _quitting = false;
                };
                _tray = new TrayIcon { Icon = AppIcon, ToolTipText = "LUNA EVO Config Manager", Menu = new NativeMenu { Items = { open, quit } } };
                _tray.Clicked += (_, _) => ShowFromTray();
                TrayIcon.SetIcons(app, [_tray]);
            }
        }
        catch (Exception)
        {
            // No notification area (headless, or an unusual desktop): stay visible rather than vanish.
            return;
        }
        Hide();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>Called by the server page whenever the server starts, stops or ticks.</summary>
    internal void OnServerStatusChanged()
    {
        var running = _server?.IsRunning == true;
        HomeServer.KeepAwake(Settings.HomeServerMode && running);
        _serverDot.Classes.Set("running", running);
        _consoleDot.Classes.Set("running", running);
        _consoleDot.IsVisible = running;
        _serverText.Text = _server?.StatusText ?? "No server open";
    }

    /// <summary>Back to the welcome page (from the setup wizard's Back button).</summary>
    internal void ShowWelcomePage() => ShowWelcome(null);

    private void ShowWelcome(string? error)
    {
        _server?.Dispose();
        _server = null;
        _players?.Dispose();
        _players = null;
        _tracker?.Save();
        _tracker = null;
        _connect = null;
        _vesselsPage = null;
        _profilesPage = null;
        _modImport = null;
        _session = null;
        _updateText.IsVisible = false;
        _nav.Children.Clear();
        _pages.Clear();
        _files.Clear();
        _current = null;
        _settingsTabs = null;
        _settingsNav = null;
        _save.IsEnabled = false;
        _serverText.Text = "No server open";
        _issuesText.Text = "";
        AddPlaySection();
        AddLunaEvoSection();

        var setup = Ui.Button("Set up a new server", "fab", "IconPlay").WithId("welcome/setup");
        setup.HorizontalAlignment = HorizontalAlignment.Left;
        setup.Padding = new Thickness(22, 12);
        setup.Click += async (_, _) => await ShowWizardAsync();
        var open = Ui.Button("Open an existing server folder", "text", "IconFolder").WithId("welcome/open");
        open.HorizontalAlignment = HorizontalAlignment.Left;
        open.Click += async (_, _) => await PickServerFolderAsync();
        var join = Ui.Button("Join a friend's server", "text", "IconPlay").WithId("welcome/join");
        join.HorizontalAlignment = HorizontalAlignment.Left;
        join.Click += (_, _) => Navigate(JoinKey);
        var panel = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(40),
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 600,
            Children =
            {
                Ui.Text("Welcome to LUNA EVO", "headline"),
                Ui.Text("New to hosting? LUNA EVO can download Luna Multiplayer's server and set it up from a few questions. " +
                        "Already have one? Open the folder that contains Server.exe. Just playing? Join a friend's server.", "sublabel"),
                setup,
                open,
                join,
            },
        };
        if (error != null)
            panel.Children.Add(Ui.Text(error, "label", "error"));
        _page.Content = panel;
        _settingsPage.Refresh();
    }

    /// <summary>Shows the setup wizard and loads LMP's releases.</summary>
    internal async Task ShowWizardAsync()
    {
        Wizard = new SetupWizard(this);
        _page.Content = Wizard.View;
        await Wizard.LoadReleasesAsync();
    }

    private void BuildPages(ConfigSession session)
    {
        _nav.Children.Clear();
        _rows.Clear();
        _lists.Clear();
        _objectLists.Clear();
        _files.Clear();
        _pages.Clear();
        _current = null;

        AddHeading("Server");
        var consoleContent = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        consoleContent.Children.Add(new TextBlock { Text = "Server Console", VerticalAlignment = VerticalAlignment.Center });
        if (_consoleDot.Parent is Panel oldParent)
            oldParent.Children.Remove(_consoleDot);
        Grid.SetColumn(_consoleDot, 1);
        consoleContent.Children.Add(_consoleDot);
        AddNav(ConsolePage, consoleContent, _server!.View);
        AddNav(PlayersKey, new TextBlock { Text = "Players" }, _players!.View);
        AddNav(VesselsKey, new TextBlock { Text = "Vessels" }, _vesselsPage!.View);
        AddNav(ConnectKey, new TextBlock { Text = "Connect" }, _connect!.View);


        // One "Server settings" page, a tab per config file, grouped Game / Network / Advanced.
        var previousTab = _settingsTabs?.Selected;
        _settingsTabs = new TabSet("nav");
        var known = Sections.SelectMany(s => s.Files).Append(ModControlFile).ToHashSet();
        var sections = Sections
            .Select(s => (s.Title, Files: s.Files.Select(session.Schema.Find).OfType<SettingsFileSchema>().ToList()))
            .Append((Title: "Other", Files: session.Schema.Files.Where(f => !known.Contains(f.FileName)).ToList()))
            .Where(s => s.Files.Count > 0);
        foreach (var (title, files) in sections)
        {
            _settingsTabs.AddLabel(title);
            foreach (var file in files)
                BuildFile(session, file, inTabs: true);
        }
        var settingsHeader = new StackPanel
        {
            Margin = new Thickness(28, 20, 28, 0),
            Children =
            {
                Ui.Text("Server settings", "headline"),
                Ui.Text($"LMP's config files in {session.ConfigDirectory}. Changes are saved together with Save.", "sublabel"),
                new Border { Height = 10 },
                SearchBox(session),
                _searchResults,
                new Border { Height = 6 },
                _settingsTabs.Strip,
            },
        };
        var settingsView = new DockPanel();
        DockPanel.SetDock(settingsHeader, Dock.Top);
        settingsView.Children.Add(settingsHeader);
        settingsView.Children.Add(_settingsTabs.Content);
        AddNav(ServerSettingsKey, NavWithBadge("Server settings", out var badge, out var badgeText, out var dirty), settingsView);
        _settingsNav = (badge, badgeText, dirty);
        if (previousTab != null)
            _settingsTabs.Select(previousTab);

        if (session.Schema.Find(ModControlFile) is { } modControl)
            BuildFile(session, modControl, inTabs: false);
        AddNav(ProfilesKey, new TextBlock { Text = "Profiles & Backups" }, _profilesPage!.View);

        AddPlaySection();
        AddLunaEvoSection();
        Navigate(Sections[0].Files[0]);
    }

    private readonly WrapPanel _searchResults = new() { Margin = new Thickness(0, 6, 0, 0) };

    /// <summary>Finds settings in every config file by name or description; picking one opens its tab and scrolls to it.</summary>
    private TextBox SearchBox(ConfigSession session)
    {
        var box = new TextBox { PlaceholderText = "Search all settings (e.g. heartbeat, password, warp)...", Width = 420, HorizontalAlignment = HorizontalAlignment.Left }
            .WithId("serverSettings/search");
        box.TextChanged += (_, _) => ShowSearch(session, box.Text?.Trim() ?? "");
        _searchResults.Children.Clear();
        if (_searchResults.Parent is Panel old) // the page is rebuilt when a server is reopened
            old.Children.Remove(_searchResults);
        return box;
    }

    internal void ShowSearch(ConfigSession session, string query)
    {
        _searchResults.Children.Clear();
        if (query.Length < 2)
            return;
        var matches = _rows
            .Where(r => Ui.Humanize(r.Setting.Name).Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        r.Setting.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        (r.Setting.Description ?? "").Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Setting.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) // name matches first
            .Take(12).ToList();
        if (matches.Count == 0)
        {
            _searchResults.Children.Add(Ui.Text("No setting matches.", "sublabel"));
            return;
        }
        foreach (var row in matches)
        {
            var file = session.Schema.Find(row.FileName);
            var label = $"{(row.FileName == ModControlFile ? "Mod Control" : Ui.Humanize(file?.DisplayName ?? row.FileName))} › {Ui.Humanize(row.Setting.Name)}";
            var result = Ui.Button(label, "chip").WithId($"serverSettings/result/{row.FileName}/{row.Setting.Name}");
            result.Margin = new Thickness(0, 0, 6, 6);
            result.Click += (_, _) =>
            {
                Navigate(row.FileName);
                row.View.BringIntoView();
            };
            _searchResults.Children.Add(result);
        }
    }

    /// <summary>A rail or tab label with an error-count badge and an unsaved-changes dot.</summary>
    private static Grid NavWithBadge(string text, out Border badge, out TextBlock badgeText, out Ellipse dirty)
    {
        badgeText = new TextBlock();
        badge = new Border { Child = badgeText, IsVisible = false, Margin = new Thickness(6, 0, 0, 0) };
        badge.Classes.Add("badge");
        dirty = new Ellipse { IsVisible = false, Margin = new Thickness(8, 0, 0, 0) };
        dirty.Classes.Add("dirtyDot");
        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        content.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(badge, 1);
        content.Children.Add(badge);
        Grid.SetColumn(dirty, 2);
        content.Children.Add(dirty);
        return content;
    }

    private void AddPlaySection()
    {
        AddHeading("Play");
        AddNav(JoinKey, new TextBlock { Text = "Join a server" }, _joinPage.View);
        AddNav(CraftsKey, new TextBlock { Text = "Crafts" }, _craftsPage.View);
    }

    private void AddLunaEvoSection()
    {
        AddHeading("LUNA EVO");
        AddNav(SettingsKey, new TextBlock { Text = "Settings" }, _settingsPage.View);
    }

    private void AddHeading(string title)
    {
        var heading = Ui.Text(title, "groupTitle");
        heading.Margin = new Thickness(16, _nav.Children.Count == 0 ? 0 : 10, 0, 2);
        _nav.Children.Add(heading);
    }

    private void AddNav(string key, Control content, Control page)
    {
        var nav = new Button { Content = content }.WithId("nav/" + key);
        nav.Classes.Add("nav");
        nav.Click += (_, _) => Navigate(key);
        _nav.Children.Add(nav);
        _pages[key] = (nav, page);
    }

    /// <param name="inTabs">True: a tab on the Server settings page. False: its own rail entry (Mod Control).</param>
    private void BuildFile(ConfigSession session, SettingsFileSchema file, bool inTabs)
    {
        var title = file.FileName == ModControlFile ? "Mod Control" : Ui.Humanize(file.DisplayName);
        var navContent = NavWithBadge(title, out var badge, out var badgeText, out var dirty);

        var bannerText = new TextBlock();
        var banner = new Border { Child = bannerText, IsVisible = false, Margin = new Thickness(0, 0, 0, 16) }.WithId($"{file.FileName}/banner");
        banner.Classes.Add("banner");

        // In tabs the page header already says where the files are; only the file name is repeated.
        var page = new StackPanel { Margin = new Thickness(28, inTabs ? 4 : 20, 28, 28) };
        page.Children.Add(Ui.Text(inTabs ? Ui.Humanize(file.DisplayName) : "LMP Mod Control", inTabs ? "title" : "headline"));
        var path = Ui.Text(System.IO.Path.Combine(session.ConfigDirectory, file.FileName), "sublabel");
        path.Margin = new Thickness(0, 2, 0, 20);
        page.Children.Add(path);
        page.Children.Add(banner);

        if (!session.HasFile(file.FileName))
        {
            page.Children.Add(Ui.Text("This file doesn't exist yet. Start the server once and it will be generated.", "label"));
        }
        else
        {
            var (group, body, _) = Ui.Group(file.FileName);
            foreach (var setting in file.Settings)
            {
                if (setting.Kind == SettingKind.ObjectList)
                {
                    var editor = new ObjectListEditor(session, file.FileName, setting, Refresh);
                    _objectLists.Add(editor);
                    page.Children.Add(editor.View);
                }
                else if (setting.Kind == SettingKind.StringList)
                {
                    var list = new ListEditor(session, file.FileName, setting, Refresh);
                    _lists.Add(list);
                    page.Children.Add(list.View);
                }
                else
                {
                    var row = new SettingRow(session, file.FileName, setting, Refresh);
                    _rows.Add(row);
                    body.Children.Add(row.View);
                }
            }
            if (body.Children.Count > 0)
                page.Children.Insert(3, group);
            if (file.FileName == "LMPModControl.xml")
            {
                _modImport = new ModImportPanel(this, session, Refresh);
                page.Children.Insert(3, _modImport.View);
            }
        }

        var view = new ScrollViewer { Content = page };
        if (inTabs)
        {
            var tab = _settingsTabs!.Add(file.FileName, navContent, view);
            _pages[file.FileName] = (tab, view);
        }
        else
        {
            AddNav(file.FileName, navContent, view);
        }
        _files[file.FileName] = new FileView(badge, badgeText, dirty, banner, bannerText);
    }

    private void Refresh()
    {
        if (_session == null)
            return;

        var issues = _session.Validate();
        foreach (var row in _rows)
            row.Refresh(issues);
        foreach (var list in _lists)
            list.Refresh();
        foreach (var list in _objectLists)
            list.Refresh();

        foreach (var (fileName, view) in _files)
        {
            var errors = issues.Count(i => i.FileName == fileName && i.Severity == Severity.Error);
            view.BadgeText.Text = errors.ToString();
            view.Badge.IsVisible = errors > 0;
            view.Dirty.IsVisible = _session.DirtyFiles.Contains(fileName);

            var fileIssues = issues.Where(i => i.FileName == fileName && i.Setting == null).Select(i => i.Message).ToList();
            view.BannerText.Text = string.Join("\n", fileIssues);
            view.Banner.IsVisible = fileIssues.Count > 0;
        }

        if (_settingsNav is var (navBadge, navText, navDirty) && _settingsTabs != null)
        {
            var tabbed = _settingsTabs.Keys;
            var errors = issues.Count(i => tabbed.Contains(i.FileName) && i.Severity == Severity.Error);
            navText.Text = errors.ToString();
            navBadge.IsVisible = errors > 0;
            navDirty.IsVisible = _session.DirtyFiles.Any(tabbed.Contains);
        }

        _save.IsEnabled = _session.IsDirty;
        _server?.RefreshSummary();
        UpdateStatus(issues);
    }

    private void UpdateStatus(IReadOnlyList<Issue> issues)
    {
        if (_session == null)
            return;

        var errors = issues.Count(i => i.Severity == Severity.Error);
        var warnings = issues.Count(i => i.Severity == Severity.Warning);
        _issuesText.Text = errors == 0 && warnings == 0
            ? "No problems found"
            : string.Join(" · ", new[] { errors > 0 ? $"{errors} error(s)" : null, warnings > 0 ? $"{warnings} warning(s)" : null }.OfType<string>());
        _issuesText.Classes.Set("error", errors > 0);
        _issuesText.Classes.Set("warning", errors == 0 && warnings > 0);

        if (_session.IsDirty)
            _saveText.Text = $"{_session.DirtyFiles.Count} file(s) changed";
        OnServerStatusChanged();
    }

    /// <returns>True if everything was saved.</returns>
    internal async Task<bool> SaveAsync()
    {
        if (_session is not { IsDirty: true })
            return true;

        var errors = _session.Validate().Where(i => i.Severity == Severity.Error).ToList();
        if (errors.Count > 0)
        {
            var list = string.Join("\n", errors.Take(10).Select(i => $"• {Ui.Humanize(i.FileName.Replace("Settings.xml", ""))} / {i.Setting}: {i.Message}"));
            await Dialogs.InfoAsync(this, "Can't save yet", $"Fix these first:\n\n{list}{(errors.Count > 10 ? $"\n...and {errors.Count - 10} more" : "")}");
            return false;
        }

        if (_server?.IsRunning == true &&
            !await Dialogs.ConfirmAsync(this, "Server is running",
                "The LMP server only reads its config when it starts, so your changes will apply after a restart.\n\nSave anyway?",
                "Save"))
            return false;

        if (Settings.ReviewChangesBeforeSave && !await Dialogs.ConfirmAsync(this, "Review changes", DescribeChanges(_session.GetChanges()), "Save"))
            return false;

        try
        {
            var backup = _session.Save(Settings.BackupsToKeep);
            _saveText.Text = $"Saved {DateTime.Now:HH:mm:ss}";
            ToolTip.SetTip(_saveText, $"Backup of the previous files: {backup}");
        }
        catch (Exception e)
        {
            await Dialogs.InfoAsync(this, "Save failed", e.Message);
            return false;
        }
        Refresh();
        return true;
    }

    /// <summary>"Server Name: Luna Server → LUNA EVO" lines for the review dialog; long lists are summarised.</summary>
    internal static string DescribeChanges(IReadOnlyList<Change> changes)
    {
        string Value(string v) => v.Length == 0 ? "(empty)" : v.Length > 40 ? v[..40] + "…" : v;
        var lines = changes.Take(25).Select(c =>
        {
            var name = $"{Ui.Humanize(c.FileName.Replace("Settings.xml", "").Replace(".xml", ""))} › {Ui.Humanize(c.Setting)}";
            if (c.Kind is SettingKind.StringList or SettingKind.ObjectList)
            {
                var before = c.Before.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                var after = c.After.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                return $"• {name}: {after.Except(before).Count()} added, {before.Except(after).Count()} removed";
            }
            return $"• {name}: {Value(c.Before)} → {Value(c.After)}";
        });
        var more = changes.Count > 25 ? $"\n...and {changes.Count - 25} more" : "";
        return $"Save these {changes.Count} change(s)? A backup of the current files is made first.\n\n{string.Join("\n", lines)}{more}";
    }

    private async Task PickServerFolderAsync()
    {
        if (_server?.IsOwnedActive == true)
        {
            await Dialogs.InfoAsync(this, "Server is running", "Stop the server before opening a different one.");
            return;
        }
        if (_session is { IsDirty: true } &&
            !await Dialogs.ConfirmAsync(this, "Unsaved changes", "Open another server and discard your unsaved changes?", "Discard"))
            return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose your LMP server folder (the one with Server.exe)",
            AllowMultiple = false,
        });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path)
            Open(path, configDirectory: null);
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed)
            return;
        if (Settings.HomeServerMode && !_quitting)
        {
            // Home server mode keeps running; quitting is in the notification-area icon's menu.
            e.Cancel = true;
            HideToTray();
            return;
        }
        var dirty = _session is { IsDirty: true };
        var serverUp = _server?.IsOwnedActive == true;
        if (!dirty && !serverUp)
            return;

        e.Cancel = true;
        if (dirty && !await Dialogs.ConfirmAsync(this, "Unsaved changes",
                $"You have unsaved changes in {string.Join(", ", _session!.DirtyFiles)}. Close without saving?", "Close without saving"))
            return;

        if (serverUp)
        {
            var choice = await Dialogs.ChooseAsync(this, "The server is still running",
                "LUNA EVO started this server. If you leave it running it carries on in the background without a window; " +
                "open LUNA EVO again to follow its log and stop it.",
                "Stop server and close", "Leave it running", "Cancel");
            if (choice == 0)
                await _server!.StopAsync(confirm: false);
            else if (choice != 1)
                return;
        }

        _closeConfirmed = true;
        Close();
    }
}
