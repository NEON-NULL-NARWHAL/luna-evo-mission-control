// LUNA EVO · LunaEvo.MissionControl/ServerPage.cs
// [UI] The Server console page: start/stop/restart, summary cards, live console, commands, log files.
// Used by: MainWindow
// Guide:   Meat and Potatoes/03-Server-Control.md

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Threading;
using LunaEvo.MissionControl.Core;
using LunaEvo.MissionControl.Core.Ops;
using LunaEvo.MissionControl.Core.Server;
using LunaEvo.MissionControl.Core.Setup;
using LunaEvo.MissionControl.Core.Validation;

namespace LunaEvo.MissionControl;

/// <summary>
/// Server control, DCS-server-manager style: start/stop/restart, a summary of the key settings, the live console
/// with filters and a command line, and a browser for the log files LMP keeps.
/// </summary>
internal sealed class ServerPage : IDisposable
{
    /// <summary>Tests swap in the fake server.</summary>
    internal static string ExecutableName { get; set; } = "Server.exe";

    /// <summary>Tests pass flags to the fake server (e.g. --crash).</summary>
    internal static string[] ExtraArguments { get; set; } = [];

    // From LMP 0.29.2's CommandHandler.RegisterCommands.
    private static readonly (string Command, string Help)[] QuickCommands =
    [
        ("/help", "List LMP's server commands"),
        ("/listclients", "Who is connected"),
        ("/countclients", "How many players are connected"),
        ("/connectionstats", "Network traffic usage"),
        ("/say ", "Broadcast a chat message. Text without a slash is sent as chat too"),
        ("/kick ", "Kick a player: /kick name"),
        ("/dekessler", "Clear out debris"),
        ("/nukeksc", "Clear ALL vessels from the KSC and runway"),
        ("/backup now", "Back up the universe right now"),
    ];

    private static readonly (string Label, LogKind[] Kinds)[] Filters =
    [
        ("Errors", [LogKind.Error, LogKind.Fatal]),
        ("Warnings", [LogKind.Warning]),
        ("Info", [LogKind.Normal, LogKind.Info]),
        ("Debug", [LogKind.Debug]),
        ("Chat", [LogKind.Chat]),
        ("Network", [LogKind.Network]),
        ("LUNA EVO", [LogKind.Manager]),
    ];

    private readonly MainWindow _host;
    private readonly string _serverDirectory;
    private readonly ServerRunner _runner;
    private readonly ConcurrentQueue<LogLine> _incoming = new();
    private readonly List<LogLine> _all = [];
    private ObservableCollection<LogLine> _visible = [];
    private readonly HashSet<LogKind> _hidden = [LogKind.Network];
    private readonly List<string> _history = [];
    private int _historyIndex;
    private readonly DispatcherTimer _flush = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _ticks;
    private LogTail? _tail;
    private int? _externalPid;

    // Automation
    private readonly CrashRestartPolicy _crashPolicy = new();
    private DateTime? _nextDailyRestart;
    private string? _dailyRestartSetting;
    private bool _dailyRestartWarned;
    private DateTime _lastUniverseBackup = DateTime.Now;
    private bool _automaticRestartPending;

    private readonly TextBlock _status = Ui.Text("", "sublabel").WithId("console/status");
    private readonly Button _start = Ui.Button("Start", "fab", "IconPlay").WithId("console/start");
    private readonly Button _stop = Ui.Button("Stop", "text", "IconStop").WithId("console/stop");
    private readonly Button _restart = Ui.Button("Restart", "text", "IconRestart").WithId("console/restart");
    private readonly WrapPanel _cards = new() { Margin = new Thickness(0, 16, 0, 8) };
    private readonly WrapPanel _health = new() { Margin = new Thickness(0, 0, 0, 8), IsVisible = false };
    private readonly ProcessStats _stats = new();
    private readonly Border _banner = new() { IsVisible = false, Margin = new Thickness(0, 0, 0, 12) };
    private readonly TextBlock _bannerText = new();
    private readonly Border _exposure = new Border { IsVisible = false, Margin = new Thickness(0, 0, 0, 12) }.WithId("console/exposure");
    private readonly TextBlock _exposureText = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly ListBox _console = new ListBox { SelectionMode = SelectionMode.Multiple }.WithId("console/lines");
    private readonly TextBox _search = new TextBox { PlaceholderText = "Search the console...", Width = 220 }.WithId("console/search");
    private readonly ToggleSwitch _follow = new ToggleSwitch { OnContent = "Follow", OffContent = "Follow", IsChecked = true }.WithId("console/follow");
    private readonly TextBox _command = new TextBox { PlaceholderText = "Type /help for commands, or a message to broadcast it as chat" }.WithId("console/command");
    private readonly Button _send = Ui.Button("Send", "fab").WithId("console/send");
    private readonly Control _liveView;
    private readonly Control _filesView;
    private readonly Button _liveTab = new Button { Content = "Live console" }.WithId("console/liveTab");
    private readonly Button _filesTab = new Button { Content = "Log files" }.WithId("console/filesTab");
    private readonly ListBox _fileList = new ListBox().WithId("console/files");
    private readonly ListBox _fileLines = new ListBox().WithId("console/fileLines");

    /// <summary>Builds the page for one server folder and starts its timers (console flush, uptime, outside-server detection).</summary>
    public ServerPage(MainWindow host, string serverDirectory)
    {
        _host = host;
        _serverDirectory = serverDirectory;
        _runner = new ServerRunner(serverDirectory, ExecutableName);
        _runner.LineReceived += _incoming.Enqueue;
        _runner.StateChanged += state => Dispatcher.UIThread.Post(() => OnStateChanged(state));
        _runner.Exited += (code, requested) => Dispatcher.UIThread.Post(() => _ = OnExitedAsync(code, requested));

        _start.Click += async (_, _) => await StartAsync();
        _stop.Click += async (_, _) => await StopAsync();
        _restart.Click += async (_, _) => await RestartAsync();
        _send.Click += (_, _) => SendCommand();
        _command.KeyDown += OnCommandKey;
        _search.TextChanged += (_, _) => Rebuild();
        _follow.IsCheckedChanged += (_, _) => ScrollToEnd();
        _liveTab.Click += (_, _) => ShowFiles(false);
        _filesTab.Click += (_, _) => ShowFiles(true);
        _fileList.SelectionChanged += (_, _) => ShowFile();

        _console.Classes.Add("console");
        _console.ItemTemplate = new FuncDataTemplate<LogLine>((line, _) => LineView(line), supportsRecycling: false);
        _console.ItemsSource = _visible;
        _fileLines.Classes.Add("console");
        _fileLines.ItemTemplate = _console.ItemTemplate;
        _fileList.ItemTemplate = new FuncDataTemplate<FileInfo>((file, _) => FileView(file), supportsRecycling: false);
        _bannerText.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        _banner.Child = _bannerText;
        _banner.Classes.Add("banner");

        _liveView = BuildLiveView();
        _filesView = BuildFilesView();
        View = BuildPage();

        _flush.Tick += (_, _) => Flush();
        _tick.Tick += (_, _) => Tick();
        _flush.Start();
        _tick.Start();
        Dispatcher.UIThread.Post(CheckDiskSpace); // at once, not after the first 30 s

        ShowFiles(false);
        CheckExternal();
        RefreshSummary();
        UpdateState();
    }

    public Control View { get; }

    /// <summary>True while a server LUNA EVO started (and so owns the console of) is up.</summary>
    public bool IsOwnedActive => _runner.IsActive;

    public bool IsRunning => _runner.IsActive || _externalPid != null;

    public string StatusText => _runner.State switch
    {
        ServerState.Starting => "Server starting...",
        ServerState.Running => $"Server running · up {Uptime()}",
        ServerState.Stopping => "Server stopping...",
        _ => _externalPid != null ? "Running outside LUNA EVO" : "Server stopped",
    };

    internal IReadOnlyList<LogLine> VisibleLines => _visible;

    /// <summary>The public address LMP detected ("Detected NAT addresses: ..."), once it has logged it. Shown on the Connect page.</summary>
    public string? PublicAddress { get; private set; }

    /// <summary>When the next scheduled daily restart is due, or null if none is set.</summary>
    public DateTime? NextDailyRestart => _nextDailyRestart;
    /// <summary>Hard kill, no backup. Only for tests and emergencies.</summary>
    internal void Kill() => _runner.Kill();
    internal ServerState State => _runner.State;

    /// <summary>Server name, ports and mode, from the (possibly unsaved) config.</summary>
    public void RefreshSummary()
    {
        var session = _host.Session;
        _cards.Children.Clear();
        if (session == null)
            return;

        string? Get(string file, string setting) => session.GetValue(file, setting);
        var website = Get(LmpRules.Website, "EnableWebsite") == "true" ? $"{Get(LmpRules.Website, "Port")} TCP" : "Off";
        // Click a card to change it right here (saved with a backup) or, for the bigger ones, to open the setting.
        Card("Server name", Get(LmpRules.General, "ServerName"), () => _host.QuickEditAsync(LmpRules.General, "ServerName", "Server name"));
        Card("Game port", $"{Get(LmpRules.Connection, "Port")} UDP", () => _host.QuickEditAsync(LmpRules.Connection, "Port", "Game port (UDP)"));
        Card("Website", website, () => Go(LmpRules.Website, "EnableWebsite"));
        Card("Mode", $"{Get(LmpRules.General, "GameMode")} · {Get(LmpRules.General, "GameDifficulty")}", () => Go(LmpRules.General, "GameMode"));
        Card("Max players", Get(LmpRules.General, "MaxPlayers"), () => _host.QuickEditAsync(LmpRules.General, "MaxPlayers", "Max players"));
        Card("Server list", Get("MasterServerSettings.xml", "RegisterWithMasterServer") == "true" ? "Listed" : "Hidden",
            () => Go("MasterServerSettings.xml", "RegisterWithMasterServer"));

        // Make it obvious when the server is reachable from the internet (UPnP opens router ports without asking).
        var exposure = LmpRules.InternetExposure(session);
        _exposureText.Text = exposure.Count == 0 ? "" :
            "This server is reachable from the internet. " + string.Join(" ", exposure) +
            " Fine if you mean it; for friends on your network or a VPN/mesh, turn these off (Connection: Upnp, Master Server).";
        _exposure.IsVisible = exposure.Count > 0;
    }

    /// <summary>Start button: offers to save unsaved changes, warns about config errors, checks the .NET runtime, then starts the server.</summary>
    /// <param name="automatic">An auto-restart: no questions asked about unsaved changes or config errors.</param>
    internal async Task StartAsync(bool automatic = false)
    {
        var session = _host.Session;
        if (session == null || _runner.IsActive || _externalPid != null)
            return;

        if (!automatic && session.IsDirty)
        {
            var choice = await Dialogs.ChooseAsync(_host, "Unsaved changes",
                "You have config changes that aren't saved yet. LMP only reads its config when it starts.",
                "Save and start", "Start without saving", "Cancel");
            if (choice == 0 && !await _host.SaveAsync())
                return;
            if (choice is not (0 or 1))
                return;
        }

        var errors = automatic ? 0 : session.Validate().Count(i => i.Severity == Severity.Error);
        if (errors > 0 && !await Dialogs.ConfirmAsync(_host, "Config has problems",
                $"{errors} setting(s) have errors, so the server may refuse to load them. Start anyway?", "Start anyway"))
            return;

        if (DotNetRuntime.Check(_serverDirectory) is { Installed: false } runtime)
        {
            await OfferRuntimeInstallAsync(runtime.Major);
            if (DotNetRuntime.Check(_serverDirectory) is { Installed: false })
                return;
        }

        try
        {
            _lastUniverseBackup = DateTime.Now;
            _runner.Start(ExtraArguments);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _incoming.Enqueue(LogLine.FromManager($"Couldn't start the server: {e.Message}"));
        }
        UpdateState();
    }

    /// <returns>False if the user canceled.</returns>
    internal async Task<bool> StopAsync(bool confirm = true)
    {
        if (!IsRunning)
            return true;
        if (confirm && _host.Settings.ConfirmStop && !await Dialogs.ConfirmAsync(_host, "Stop the server?",
                "Everyone connected will be disconnected. LMP saves a backup of the universe while it shuts down.", "Stop server"))
            return false;

        var timeout = TimeSpan.FromSeconds(Math.Max(5, _host.Settings.StopTimeoutSeconds));
        if (_runner.IsActive)
        {
            await _runner.StopAsync(timeout);
            // LMP has just written its final backup; keep a dated copy in case the next start goes wrong. A failed
            // copy is only reported: it must never stop a daily restart from starting the server again.
            try
            {
                if (await Task.Run(() => new UniverseBackups(_serverDirectory).Safety("onstop")) is { } snapshot)
                    _incoming.Enqueue(LogLine.FromManager($"Universe backed up to {snapshot.Name} (the last 5 stops are kept)."));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _incoming.Enqueue(LogLine.FromManager($"Couldn't back up the Universe after stopping: {e.Message}"));
            }
        }
        else if (_externalPid is { } pid)
        {
            await StopExternalAsync(pid, timeout);
        }
        UpdateState();
        return true;
    }

    /// <summary>Restart button: a clean stop followed by a normal start.</summary>
    internal async Task RestartAsync()
    {
        if (_host.Settings.ConfirmStop && !await Dialogs.ConfirmAsync(_host, "Restart the server?",
                "Everyone connected will be disconnected while it restarts.", "Restart"))
            return;
        await StopAsync(confirm: false);
        await StartAsync();
    }

    /// <summary>Offers to install a missing .NET runtime with winget, or opens the download page.</summary>
    internal async Task OfferRuntimeInstallAsync(int major)
    {
        var choice = await Dialogs.ChooseAsync(_host, $".NET {major} Runtime needed",
            $"This LMP server needs the .NET {major} Runtime, which isn't installed on this PC. " +
            "LUNA EVO can install it with Windows' package manager (winget); Windows will ask for permission.",
            "Install it now", "Open download page", "Cancel");
        if (choice == 0)
        {
            _incoming.Enqueue(LogLine.FromManager($"Installing the .NET {major} Runtime with winget..."));
            var ok = await RuntimeInstaller.InstallAsync(major);
            _incoming.Enqueue(LogLine.FromManager(ok ? $".NET {major} installed." : "winget couldn't install it. Opening the download page instead."));
            if (!ok)
                Ui.Open(SettingsPage.DotNet6Download);
        }
        else if (choice == 1)
        {
            Ui.Open(SettingsPage.DotNet6Download);
        }
        UpdateState();
    }

    /// <summary>
    /// Daily restart: warns players in chat a minute before, then does a clean restart. Runs every second from
    /// the timer; tests call it with a chosen time.
    /// </summary>
    internal async Task CheckDailyRestartAsync(DateTime now)
    {
        var setting = _host.Settings.DailyRestartTime;
        if (setting != _dailyRestartSetting || _nextDailyRestart == null)
        {
            _dailyRestartSetting = setting;
            _nextDailyRestart = DailyRestart.Next(now, setting);
            _dailyRestartWarned = false;
        }
        if (_nextDailyRestart is not { } due)
            return;
        if (_runner.State != ServerState.Running)
        {
            // A restart time that passes while the server is stopped is skipped, not saved up: otherwise starting the
            // server hours later would restart it straight away.
            if (now >= due)
            {
                _nextDailyRestart = DailyRestart.Next(now, setting);
                _dailyRestartWarned = false;
            }
            return;
        }

        if (!_dailyRestartWarned && now >= due - DailyRestart.Warning)
        {
            _dailyRestartWarned = true;
            TrySend("/say Scheduled server restart in 1 minute. Back in a moment!");
            _incoming.Enqueue(LogLine.FromManager($"Scheduled restart at {due:HH:mm}; players have been warned."));
        }
        if (now >= due)
        {
            _nextDailyRestart = DailyRestart.Next(now.AddMinutes(1), setting);
            _dailyRestartWarned = false;
            _incoming.Enqueue(LogLine.FromManager("Scheduled restart now."));
            _host.Notify(new Notification(NotificationKind.ScheduledRestart, $"{_host.ServerName}: scheduled restart", "Back in a moment.", _host.ServerName));
            await StopAsync(confirm: false);
            await StartAsync(automatic: true);
        }
    }

    /// <summary>
    /// Scheduled Universe backup: asks LMP to "/backup now", waits a few seconds for it to write, then zips the
    /// Universe folder. Runs from the timer; tests call it with a chosen time.
    /// </summary>
    internal async Task CheckUniverseBackupAsync(DateTime now, TimeSpan? settle = null)
    {
        var hours = _host.Settings.UniverseBackupHours;
        if (hours <= 0 || _runner.State != ServerState.Running || now - _lastUniverseBackup < TimeSpan.FromHours(hours))
            return;
        _lastUniverseBackup = now;
        TrySend("/backup now");
        await Task.Delay(settle ?? TimeSpan.FromSeconds(5));
        try
        {
            var backups = new UniverseBackups(_serverDirectory);
            var keep = _host.Settings.UniverseBackupsToKeep;
            var (snapshot, skipped) = await Task.Run(() =>
            {
                var result = backups.Create("scheduled");
                backups.Prune(keep);
                return result;
            });
            _incoming.Enqueue(LogLine.FromManager($"Universe backed up to {snapshot.Name}{(skipped > 0 ? $" ({skipped} busy files skipped)" : "")}."));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _incoming.Enqueue(LogLine.FromManager($"Universe backup failed: {e.Message}"));
        }
    }

    /// <summary>Sends a console command if LUNA EVO is running the server (Kick/Ban buttons, backups). False if it isn't.</summary>
    internal bool SendServerCommand(string command)
    {
        if (_runner.State is not (ServerState.Running or ServerState.Starting))
            return false;
        TrySend(command);
        return true;
    }

    private void TrySend(string command)
    {
        try
        {
            _runner.SendCommand(command);
        }
        catch (InvalidOperationException)
        {
            // Not running any more; nothing to tell.
        }
    }

    private void OnStateChanged(ServerState state)
    {
        if (state == ServerState.Running)
            _host.Notify(new Notification(NotificationKind.ServerUp, $"{_host.ServerName} is up", "Players can join.", _host.ServerName));
        UpdateState();
    }

    private async Task OnExitedAsync(int code, bool requested)
    {
        _host.Tracker?.ServerStopped();
        if (requested || _automaticRestartPending)
        {
            if (!_automaticRestartPending)
                _host.Notify(new Notification(NotificationKind.ServerStopped, $"{_host.ServerName} stopped", "Stopped from LUNA EVO.", _host.ServerName));
            return;
        }

        _host.Notify(new Notification(NotificationKind.ServerCrashed, $"{_host.ServerName} stopped unexpectedly",
            _host.Settings.AutoRestartOnCrash ? $"Exit code {code}. LUNA EVO will try to restart it." : $"Exit code {code}.", _host.ServerName));
        if (!_host.Settings.AutoRestartOnCrash)
            return;
        if (!_crashPolicy.ShouldRestart(DateTime.Now))
        {
            _incoming.Enqueue(LogLine.FromManager("The server keeps crashing (3 times in 10 minutes), so LUNA EVO stopped restarting it. Check the errors above."));
            _host.Notify(new Notification(NotificationKind.AutoRestartGaveUp, $"{_host.ServerName} keeps crashing",
                "3 crashes in 10 minutes, so LUNA EVO stopped restarting it. Check the Server console.", _host.ServerName));
            return;
        }

        _incoming.Enqueue(LogLine.FromManager($"The server stopped unexpectedly. Restarting in {CrashRestartPolicy.Delay.TotalSeconds:0} seconds..."));
        _automaticRestartPending = true;
        try
        {
            await Task.Delay(RestartDelay);
            if (!_runner.IsActive)
                await StartAsync(automatic: true);
        }
        finally
        {
            _automaticRestartPending = false;
        }
    }

    /// <summary>Tests shorten this.</summary>
    internal static TimeSpan RestartDelay { get; set; } = CrashRestartPolicy.Delay;

    /// <summary>Sends the command box's text to the server and remembers it in the Up/Down history.</summary>
    internal void SendCommand()
    {
        var text = _command.Text?.Trim() ?? "";
        if (text.Length == 0)
            return;
        try
        {
            _runner.SendCommand(text);
            _history.Remove(text);
            _history.Add(text);
            _historyIndex = _history.Count;
            _command.Text = "";
        }
        catch (InvalidOperationException e)
        {
            _incoming.Enqueue(LogLine.FromManager(e.Message));
        }
    }

    /// <summary>Moves queued lines into the view. Runs on a timer; tests call it directly.</summary>
    internal void Flush()
    {
        var added = false;
        while (_incoming.TryDequeue(out var line))
        {
            if (line.Kind == LogKind.Debug && NetworkHelper.PublicAddressFrom(line) is { } address)
                PublicAddress = address;
            if (!line.Replayed)
                _host.Tracker?.Observe(line); // player history and alerts
            _all.Add(line);
            if (Passes(line))
            {
                _visible.Add(line);
                added = true;
            }
        }

        var cap = Math.Max(500, _host.Settings.ConsoleLines);
        if (_all.Count > cap)
            _all.RemoveRange(0, _all.Count - cap);
        if (_visible.Count > cap + 250)
            Rebuild();
        else if (added)
            ScrollToEnd();
    }

    public void Dispose()
    {
        _flush.Stop();
        _tick.Stop();
        _tail?.Dispose();
        _runner.Dispose(); // leaves a running server running; it's only the handle we drop
    }

    private Control BuildPage()
    {
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        _start.HorizontalAlignment = HorizontalAlignment.Left;
        _start.Padding = new Thickness(20, 10);
        actions.Children.Add(_restart);
        actions.Children.Add(_stop);
        actions.Children.Add(_start);

        var title = new StackPanel { Spacing = 2, Children = { Ui.Text("Server console", "headline"), _status } };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(title);
        Grid.SetColumn(actions, 1);
        header.Children.Add(actions);

        foreach (var tab in new[] { _liveTab, _filesTab })
        {
            tab.Classes.Add("nav");
            tab.HorizontalAlignment = HorizontalAlignment.Left;
        }
        var openLogs = Ui.Button("Open logs folder", "text", "IconFolder");
        openLogs.Click += (_, _) => Ui.Open(LogFiles.Folder(_serverDirectory));
        var tabs = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(openLogs, Dock.Right);
        tabs.Children.Add(openLogs);
        tabs.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { _liveTab, _filesTab } });

        _exposure.Classes.Add("banner");
        _exposure.Child = _exposureText;
        _exposure.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
        ToolTip.SetTip(_exposure, "Go to the Upnp setting");
        _exposure.PointerPressed += (_, _) => _host.GoToSetting(LmpRules.Connection, "Upnp");
        var top = new StackPanel { Children = { header, _cards, _health, _exposure, _banner, tabs } };
        var page = new DockPanel { Margin = new Thickness(28, 20, 28, 20) };
        DockPanel.SetDock(top, Dock.Top);
        page.Children.Add(top);
        page.Children.Add(new Panel { Children = { _liveView, _filesView } });
        return page;
    }

    private Control BuildLiveView()
    {
        var chips = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        foreach (var (label, kinds) in Filters)
        {
            var chip = new ToggleButton { Content = label, IsChecked = !kinds.All(_hidden.Contains) }.WithId("console/filter/" + label);
            chip.Classes.Add("chip");
            chip.IsCheckedChanged += (_, _) =>
            {
                foreach (var kind in kinds)
                {
                    if (chip.IsChecked == true) _hidden.Remove(kind);
                    else _hidden.Add(kind);
                }
                Rebuild();
            };
            chips.Children.Add(chip);
        }

        var copy = Ui.Button("Copy", "text").WithId("console/copy");
        ToolTip.SetTip(copy, "Copy the selected lines, or every line shown if none are selected.");
        copy.Click += async (_, _) => await CopyAsync();
        var clear = Ui.Button("Clear", "text").WithId("console/clear");
        clear.Click += (_, _) =>
        {
            _all.Clear();
            Rebuild();
        };

        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Top, Children = { _search, _follow, copy, clear } };
        var toolbar = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(tools, Dock.Right);
        toolbar.Children.Add(tools);
        toolbar.Children.Add(chips);

        var quick = new WrapPanel();
        foreach (var (command, help) in QuickCommands)
        {
            var chip = new Button { Content = command.Trim() }.WithId("console/quick/" + command.Trim());
            chip.Classes.Add("chip");
            ToolTip.SetTip(chip, help);
            chip.Click += (_, _) =>
            {
                _command.Text = command;
                _command.CaretIndex = command.Length;
                _command.Focus();
            };
            quick.Children.Add(chip);
        }
        var input = new DockPanel();
        DockPanel.SetDock(_send, Dock.Right);
        _send.Margin = new Thickness(8, 0, 0, 0);
        _send.Padding = new Thickness(20, 8);
        _send.HorizontalAlignment = HorizontalAlignment.Right;
        input.Children.Add(_send);
        input.Children.Add(_command);
        var commandBar = new Border { Child = new StackPanel { Margin = new Thickness(8), Spacing = 4, Children = { quick, input } } };
        commandBar.Classes.Add("group");
        commandBar.Margin = new Thickness(0, 10, 0, 0);

        var consoleBox = new Border { Child = _console };
        consoleBox.Classes.Add("console");

        var view = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(commandBar, Dock.Bottom);
        view.Children.Add(toolbar);
        view.Children.Add(commandBar);
        view.Children.Add(consoleBox);
        return view;
    }

    private Control BuildFilesView()
    {
        var refresh = Ui.Button("Refresh", "text");
        refresh.Click += (_, _) => LoadFiles();
        var listBox = new Border { Child = _fileList };
        listBox.Classes.Add("console");
        var left = new DockPanel { Margin = new Thickness(0, 0, 10, 0) };
        DockPanel.SetDock(refresh, Dock.Bottom);
        left.Children.Add(refresh);
        left.Children.Add(listBox);

        var linesBox = new Border { Child = _fileLines };
        linesBox.Classes.Add("console");
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("300,*") };
        grid.Children.Add(left);
        Grid.SetColumn(linesBox, 1);
        grid.Children.Add(linesBox);
        return grid;
    }

    private static Control LineView(LogLine? line)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("68,92,*") };
        if (line == null)
            return grid;
        var kind = line.Kind.ToString();
        var time = Ui.Text(line.Time ?? "", "logTime");
        var tag = Ui.Text(line.Time == null ? "" : line.Tag, kind);
        var message = new SelectableTextBlock { Text = line.Message, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        message.Classes.Add(kind);
        Grid.SetColumn(tag, 1);
        Grid.SetColumn(message, 2);
        grid.Children.Add(time);
        grid.Children.Add(tag);
        grid.Children.Add(message);
        return grid;
    }

    private static Control FileView(FileInfo? file)
    {
        if (file == null)
            return new TextBlock();
        // LMP names them lmpserver_yyyy-MM-dd_HH-mm-ss.log after the time the server started.
        var started = DateTime.TryParseExact(Path.GetFileNameWithoutExtension(file.Name).Replace("lmpserver_", ""),
            "yyyy-MM-dd_HH-mm-ss", null, System.Globalization.DateTimeStyles.None, out var time)
            ? $"Started {time:yyyy-MM-dd HH:mm}"
            : file.Name;
        return new StackPanel
        {
            Margin = new Thickness(4, 2),
            Children =
            {
                Ui.Text(started, "label"),
                Ui.Text($"{file.Name} · {file.Length / 1024.0:0.#} KB", "sublabel"),
            },
        };
    }

    private Task<bool> Go(string fileName, string setting)
    {
        _host.GoToSetting(fileName, setting);
        return Task.FromResult(true);
    }

    private void Card(string label, string? value, Func<Task<bool>> open) =>
        _cards.Children.Add(Ui.Card("console/card/" + label, label, string.IsNullOrEmpty(value) ? "-" : value, async () =>
        {
            if (await open())
                RefreshSummary();
        }, tip: "Click to change"));

    private bool Passes(LogLine line)
    {
        if (_hidden.Contains(line.Kind))
            return false;
        var search = _search.Text?.Trim();
        return string.IsNullOrEmpty(search) || line.ToString().Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void Rebuild()
    {
        _visible = new ObservableCollection<LogLine>(_all.Where(Passes).TakeLast(Math.Max(500, _host.Settings.ConsoleLines)));
        _console.ItemsSource = _visible;
        ScrollToEnd();
    }

    private void ScrollToEnd()
    {
        if (_follow.IsChecked == true && _visible.Count > 0)
            _console.ScrollIntoView(_visible[^1]);
    }

    private void UpdateState()
    {
        var owned = _runner.State;
        var external = _externalPid != null && owned == ServerState.Stopped;
        var hasSession = _host.Session != null;

        _start.IsEnabled = hasSession && owned == ServerState.Stopped && !external;
        _stop.IsEnabled = owned is ServerState.Starting or ServerState.Running || external;
        _restart.IsEnabled = owned is ServerState.Starting or ServerState.Running;
        _command.IsEnabled = _send.IsEnabled = owned is ServerState.Starting or ServerState.Running;

        _status.Text = owned == ServerState.Running && _runner.ProcessId is { } pid
            ? $"{StatusText} · PID {pid}"
            : external ? $"Running outside LUNA EVO · PID {_externalPid}" : StatusText;

        if (external)
            SetBanner("This server was started outside LUNA EVO, so you're seeing its newest log file instead of its console. " +
                      "You can stop it from here; to send commands, stop it and start it with LUNA EVO.");
        else if (DotNetRuntime.Check(_serverDirectory) is { Installed: false } runtime)
            SetBanner($"This server needs the .NET {runtime.Major} Runtime, which isn't installed. Get it from {SettingsPage.DotNet6Download}");
        else if (_lowDisk is { } low)
            SetBanner(low);
        else
            _banner.IsVisible = false;

        _host.OnServerStatusChanged();
    }

    private void SetBanner(string text)
    {
        _bannerText.Text = text;
        _banner.IsVisible = true;
    }

    /// <summary>The drive's warning (DiskSpace), rechecked every 30 s; alerted once until it's fine again.</summary>
    private string? _lowDisk;

    internal void CheckDiskSpace()
    {
        var low = DiskSpace.Warning(_serverDirectory);
        if (low != null && _lowDisk == null)
            _host.Notify(new Notification(NotificationKind.LowDiskSpace, "Server drive nearly full", low, _host.ServerName));
        if (low != _lowDisk)
        {
            _lowDisk = low;
            UpdateState();
        }
    }

    private void Tick()
    {
        _ticks++;
        if (_ticks % 30 == 1)
            CheckDiskSpace();
        if (_ticks % 2 == 0)
        {
            CheckExternal();
            ShowHealth();
        }
        _ = CheckDailyRestartAsync(DateTime.Now);
        _ = CheckUniverseBackupAsync(DateTime.Now);
        CheckAnnouncements(DateTime.Now);
        if (_runner.State == ServerState.Running)
            UpdateState(); // uptime
    }

    /// <summary>Memory, CPU and threads of Server.exe, every 2 seconds while it runs.</summary>
    internal void ShowHealth()
    {
        var health = IsRunning ? _stats.Sample(_runner.ProcessId ?? _externalPid) : _stats.Sample(null);
        _health.IsVisible = health != null;
        if (health == null)
            return;
        _health.Children.Clear();
        HealthCard("Memory", $"{health.MemoryBytes / 1024.0 / 1024.0:0} MB");
        HealthCard("CPU", $"{health.CpuPercent:0.#} %");
        HealthCard("Threads", health.Threads.ToString());
    }

    private void HealthCard(string label, string value)
    {
        var card = new Border { Child = new StackPanel { Spacing = 2, Children = { Ui.Text(label, "sublabel"), Ui.Text(value, "value") } } }
            .WithId("console/health/" + label);
        card.Classes.Add("card");
        _health.Children.Add(card);
    }

    private readonly Dictionary<int, DateTime> _announced = [];
    private DateTime _announceSince = DateTime.Now;
    private string? _announceSetting;
    private IReadOnlyList<Announcement> _announceItems = [];

    /// <summary>Sends scheduled chat announcements (LUNA EVO App settings → Automation). Runs every second; tests call it with a chosen time.</summary>
    internal void CheckAnnouncements(DateTime now)
    {
        if (_runner.State != ServerState.Running)
        {
            _announced.Clear();
            _announceSince = now; // counting starts when the server is up
            return;
        }
        if (_host.Settings.Announcements != _announceSetting)
        {
            _announceSetting = _host.Settings.Announcements;
            _announceItems = Announcements.Parse(_announceSetting).Items; // parsed once, not every second
            _announced.Clear();
        }
        foreach (var announcement in Announcements.Due(_announceItems, _announced, _announceSince, now))
            TrySend("/say " + announcement.Text);
    }

    /// <summary>Notices a Server.exe from this folder that someone started by hand, and follows its log file.</summary>
    private void CheckExternal()
    {
        int? pid = null;
        if (!_runner.IsActive)
        {
            using var process = ServerProcess.Find(_serverDirectory, ExecutableName);
            pid = process?.Id;
        }
        if (pid == _externalPid)
            return;

        _externalPid = pid;
        if (pid != null)
        {
            _incoming.Enqueue(LogLine.FromManager($"Found a server started outside LUNA EVO (PID {pid}). Following its log file."));
            _tail = new LogTail(_serverDirectory);
            _tail.LineReceived += _incoming.Enqueue;
        }
        else if (_tail != null)
        {
            _tail.Dispose();
            _tail = null;
            _incoming.Enqueue(LogLine.FromManager("The outside server has stopped."));
            _host.Tracker?.ServerStopped();
        }
        UpdateState();
    }

    private async Task StopExternalAsync(int pid, TimeSpan timeout)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(pid);
        }
        catch (ArgumentException)
        {
            return; // already gone
        }

        using (process)
        {
            _incoming.Enqueue(LogLine.FromManager("Stopping the outside server (Ctrl+C), waiting for LMP to finish its backup..."));
            if (!ConsoleControl.SendCtrlC(pid))
                _incoming.Enqueue(LogLine.FromManager("Couldn't send Ctrl+C to it."));
            try
            {
                await process.WaitForExitAsync().WaitAsync(timeout);
            }
            catch (TimeoutException)
            {
                if (await Dialogs.ConfirmAsync(_host, "The server didn't stop",
                        $"It's still running after {timeout.TotalSeconds:0} seconds. Kill it? Anything since its last backup may be lost.", "Kill it"))
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        _incoming.Enqueue(LogLine.FromManager("Killed the outside server."));
                    }
                    catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
                    {
                        // It stopped by itself while the question was up, or Windows won't let us.
                        _incoming.Enqueue(LogLine.FromManager("Couldn't kill it: " + e.Message));
                    }
                }
            }
        }
        CheckExternal();
    }

    private void OnCommandKey(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                SendCommand();
                e.Handled = true;
                break;
            case Key.Up when _history.Count > 0:
                _historyIndex = Math.Max(0, _historyIndex - 1);
                _command.Text = _history[_historyIndex];
                _command.CaretIndex = _command.Text.Length;
                e.Handled = true;
                break;
            case Key.Down when _history.Count > 0:
                _historyIndex = Math.Min(_history.Count, _historyIndex + 1);
                _command.Text = _historyIndex < _history.Count ? _history[_historyIndex] : "";
                _command.CaretIndex = _command.Text.Length;
                e.Handled = true;
                break;
        }
    }

    private async Task CopyAsync()
    {
        var lines = _console.SelectedItems?.Count > 0 ? _console.SelectedItems.OfType<LogLine>() : _visible;
        if (TopLevel.GetTopLevel(View)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(string.Join(Environment.NewLine, lines));
    }

    private void ShowFiles(bool files)
    {
        _liveView.IsVisible = !files;
        _filesView.IsVisible = files;
        _liveTab.Classes.Set("selected", !files);
        _filesTab.Classes.Set("selected", files);
        if (files)
            LoadFiles();
    }

    private void LoadFiles()
    {
        var files = LogFiles.List(_serverDirectory);
        _fileList.ItemsSource = files;
        if (files.Count > 0)
            _fileList.SelectedIndex = 0;
        else
            _fileLines.ItemsSource = new[] { LogLine.FromManager($"No log files yet in {LogFiles.Folder(_serverDirectory)}") };
    }

    private void ShowFile()
    {
        if (_fileList.SelectedItem is FileInfo file)
            _fileLines.ItemsSource = LogFiles.Read(file.FullName);
    }

    private string Uptime()
    {
        if (_runner.StartedAt is not { } started)
            return "";
        var up = DateTime.Now - started;
        return up.TotalHours >= 1 ? $"{(int)up.TotalHours}h {up.Minutes:00}m" : $"{up.Minutes}m {up.Seconds:00}s";
    }
}
