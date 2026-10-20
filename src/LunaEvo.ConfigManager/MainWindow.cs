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
using LunaEvo.ConfigManager.Core;
using LunaEvo.ConfigManager.Core.Ops;
using LunaEvo.ConfigManager.Core.Schema;
using LunaEvo.ConfigManager.Core.Setup;
using LunaEvo.ConfigManager.Core.Validation;

namespace LunaEvo.ConfigManager;

/// <summary>
/// NOMM-style shell: navigation rail on the left (server console, config files grouped by area, LUNA EVO settings,
/// with status and the save button at the bottom), page on the right.
/// </summary>
public sealed class MainWindow : Window
{
    internal const string ConsolePage = "console";
    internal const string PlayersKey = "players";
    internal const string ConnectKey = "connect";
    internal const string ProfilesKey = "profiles";
    internal const string SettingsKey = "settings";

    // Rail sections for config files; files a newer LMP adds land in "Other".
    private static readonly (string Title, string[] Files)[] Sections =
    [
        ("Game", ["GeneralSettings.xml", "GameplaySettings.xml", "WarpSettings.xml", "CraftSettings.xml", "ScreenshotSettings.xml"]),
        ("Network", ["ConnectionSettings.xml", "WebsiteSettings.xml", "MasterServerSettings.xml", "DedicatedServerSettings.xml"]),
        ("Advanced", ["IntervalSettings.xml", "LogSettings.xml", "DebugSettings.xml"]),
        ("Mods", ["LMPModControl.xml"]),
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
    private readonly Dictionary<string, FileView> _files = [];
    private readonly Dictionary<string, (Button Nav, Control Page)> _pages = [];
    private readonly SettingsPage _settingsPage;
    private ConfigSession? _session;
    private ServerPage? _server;
    private PlayersPage? _players;
    private ConnectPage? _connect;
    private ProfilesPage? _profilesPage;
    private ModImportPanel? _modImport;
    private string? _current;
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
        Closed += (_, _) =>
        {
            _server?.Dispose();
            _players?.Dispose();
        };
        _updateText.IsVisible = false;
        _updateText.PointerPressed += (_, _) => Navigate(SettingsKey);
        _updateText.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);

        _settingsPage = new SettingsPage(this);

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
    internal ConnectPage? Connect => _connect;
    internal ProfilesPage? Profiles => _profilesPage;
    internal ModImportPanel? ModImport => _modImport;
    internal SetupWizard? Wizard { get; private set; }

    /// <summary>The newest LMP release found by the update check, if it's newer than the open server.</summary>
    internal LmpRelease? AvailableUpdate { get; private set; }
    internal AppSettings Settings { get; private set; }
    internal string SettingsPath { get; }

    /// <summary>Posts to the Discord webhook if one is set. Fire and forget: a failed post never bothers the user.</summary>
    internal void Notify(string message)
    {
        var url = Settings.DiscordWebhookUrl;
        if (DiscordNotifier.IsWebhookUrl(url))
            _ = new DiscordNotifier(Http.Client).SendAsync(url, message);
    }

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
            _server = new ServerPage(this, session.ServerDirectory);
            _players?.Dispose();
            _players = new PlayersPage(this);
            _connect = new ConnectPage(this);
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
        var zip = await new ServerInstaller(Http.Client).DownloadAsync(asset, progress);
        var result = await Task.Run(() => ServerInstaller.Install(zip, session.ServerDirectory));
        Open(session.ServerDirectory, session.ConfigDirectory); // new Server.dll, maybe new settings
        Navigate(SettingsKey); // back to where the user pressed Update, to see the result
        return $"Updated to LMP {release.Tag}. The previous version is saved in {result.BinariesBackup}.";
    }

    /// <summary>Shows a page, as clicking it in the rail does: a file name, <see cref="ConsolePage"/> or <see cref="SettingsKey"/>.</summary>
    internal void Navigate(string key)
    {
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
        }
    }

    /// <summary>Called by the server page whenever the server starts, stops or ticks.</summary>
    internal void OnServerStatusChanged()
    {
        var running = _server?.IsRunning == true;
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
        _connect = null;
        _profilesPage = null;
        _modImport = null;
        _session = null;
        _updateText.IsVisible = false;
        _nav.Children.Clear();
        _pages.Clear();
        _files.Clear();
        _current = null;
        _save.IsEnabled = false;
        _serverText.Text = "No server open";
        _issuesText.Text = "";
        AddLunaEvoSection();

        var setup = Ui.Button("Set up a new server", "fab", "IconPlay").WithId("welcome/setup");
        setup.HorizontalAlignment = HorizontalAlignment.Left;
        setup.Padding = new Thickness(22, 12);
        setup.Click += async (_, _) => await ShowWizardAsync();
        var open = Ui.Button("Open an existing server folder", "text", "IconFolder").WithId("welcome/open");
        open.HorizontalAlignment = HorizontalAlignment.Left;
        open.Click += async (_, _) => await PickServerFolderAsync();
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
                        "Already have one? Open the folder that contains Server.exe.", "sublabel"),
                setup,
                open,
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

        AddHeading("Control");
        var consoleContent = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        consoleContent.Children.Add(new TextBlock { Text = "Server Console", VerticalAlignment = VerticalAlignment.Center });
        if (_consoleDot.Parent is Panel oldParent)
            oldParent.Children.Remove(_consoleDot);
        Grid.SetColumn(_consoleDot, 1);
        consoleContent.Children.Add(_consoleDot);
        AddNav(ConsolePage, consoleContent, _server!.View);
        AddNav(PlayersKey, new TextBlock { Text = "Players" }, _players!.View);
        AddNav(ConnectKey, new TextBlock { Text = "Connect" }, _connect!.View);
        AddNav(ProfilesKey, new TextBlock { Text = "Profiles & Backups" }, _profilesPage!.View);

        var known = Sections.SelectMany(s => s.Files).ToHashSet();
        var sections = Sections
            .Select(s => (s.Title, Files: s.Files.Select(session.Schema.Find).OfType<SettingsFileSchema>().ToList()))
            .Append((Title: "Other", Files: session.Schema.Files.Where(f => !known.Contains(f.FileName)).ToList()))
            .Where(s => s.Files.Count > 0);

        foreach (var (title, files) in sections)
        {
            AddHeading(title);
            foreach (var file in files)
                BuildFile(session, file);
        }

        AddLunaEvoSection();
        Navigate(Sections[0].Files[0]);
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

    private void BuildFile(ConfigSession session, SettingsFileSchema file)
    {
        var badgeText = new TextBlock();
        var badge = new Border { Child = badgeText, IsVisible = false };
        badge.Classes.Add("badge");
        var dirty = new Ellipse { IsVisible = false, Margin = new Thickness(8, 0, 0, 0) };
        dirty.Classes.Add("dirtyDot");

        var navContent = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        navContent.Children.Add(new TextBlock { Text = Ui.Humanize(file.DisplayName), VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(badge, 1);
        navContent.Children.Add(badge);
        Grid.SetColumn(dirty, 2);
        navContent.Children.Add(dirty);

        var bannerText = new TextBlock();
        var banner = new Border { Child = bannerText, IsVisible = false, Margin = new Thickness(0, 0, 0, 16) }.WithId($"{file.FileName}/banner");
        banner.Classes.Add("banner");

        var page = new StackPanel { Margin = new Thickness(28, 20, 28, 28) };
        page.Children.Add(Ui.Text(Ui.Humanize(file.DisplayName), "headline"));
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

        AddNav(file.FileName, navContent, new ScrollViewer { Content = page });
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
