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
    private readonly TextBlock _discordResult = Ui.Text("", "sublabel").WithId("settings/discordResult");
    private readonly TextBlock _dailyResult = Ui.Text("", "sublabel").WithId("settings/dailyResult");
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
        page.Children.Add(appearance);

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
        page.Children.Add(behaviour);

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
        page.Children.Add(automation);

        // Discord
        var (discord, discordBody, _) = Ui.Group("Discord");
        var webhook = new TextBox { Width = 420, PlaceholderText = "https://discord.com/api/webhooks/...", Text = settings.DiscordWebhookUrl, PasswordChar = '•' }
            .WithId("settings/discordWebhook");
        webhook.Classes.Add("revealPasswordButton");
        webhook.LostFocus += (_, _) => _host.UpdateSettings(s => s with { DiscordWebhookUrl = webhook.Text?.Trim() ?? "" });
        var test = Ui.Button("Send a test message", "text").WithId("settings/discordTest");
        test.Click += async (_, _) =>
        {
            _host.UpdateSettings(s => s with { DiscordWebhookUrl = webhook.Text?.Trim() ?? "" });
            _discordResult.Text = !DiscordNotifier.IsWebhookUrl(_host.Settings.DiscordWebhookUrl)
                ? "That isn't a Discord webhook URL."
                : await new DiscordNotifier(Http.Client).SendAsync(_host.Settings.DiscordWebhookUrl, "👋 LUNA EVO is connected to this channel.")
                    ? "Sent! Check the channel."
                    : "Discord didn't accept it. Check the URL.";
        };
        discordBody.Children.Add(Ui.Row("Webhook URL",
            "Posts when the server starts, stops or crashes. In Discord: channel settings → Integrations → Webhooks → New webhook → Copy URL. Keep it secret.",
            new StackPanel { Spacing = 4, Children = { webhook, new StackPanel { Orientation = Orientation.Horizontal, Children = { test, _discordResult } } } }));
        _discordResult.VerticalAlignment = VerticalAlignment.Center;
        discordBody.Children.Add(Ui.Row("Also post players joining and leaving", "Needs live player info (see the Players page).",
            Toggle("settings/discordPlayers", s => s.DiscordPlayerEvents, (s, v) => s with { DiscordPlayerEvents = v })));
        page.Children.Add(discord);

        // Updates
        var (updates, updatesBody, _) = Ui.Group("LMP updates");
        updatesBody.Children.Add(Ui.Row("Check for new LMP versions", "When a server is opened, ask GitHub if there's a newer Luna Multiplayer server.",
            Toggle("settings/checkUpdates", s => s.CheckForLmpUpdates, (s, v) => s with { CheckForLmpUpdates = v })));
        updatesBody.Children.Add(Ui.Row("Include pre-releases", "Newer, less tested versions. Players need the matching LMP mod version.",
            Toggle("settings/prereleases", s => s.IncludeLmpPrereleases, (s, v) => s with { IncludeLmpPrereleases = v })));
        updatesBody.Children.Add(_updates);
        page.Children.Add(updates);

        // About and folders are filled in by Refresh, since they depend on the open server.
        var (about, aboutBody, _) = Ui.Group("About");
        aboutBody.Children.Add(_about);
        page.Children.Add(about);
        var (folders, foldersBody, _) = Ui.Group("Folders");
        foldersBody.Children.Add(_folders);
        page.Children.Add(folders);

        var (links, linksBody, _) = Ui.Group("Links");
        linksBody.Children.Add(Ui.Row("Luna Multiplayer on GitHub", LmpRepository, LinkButton("Open", LmpRepository)));
        linksBody.Children.Add(Ui.Row("LMP wiki", "Server setup, mod control and commands.", LinkButton("Open", LmpWiki)));
        linksBody.Children.Add(Ui.Row("Credits",
            "Font: JetBrains Mono (SIL OFL 1.1). Icons: Material Symbols (Apache 2.0). Look inspired by NOMM, the Nuclear Option Mod Manager.",
            LinkButton("NOMM", Nomm)));
        page.Children.Add(links);

        View = new ScrollViewer { Content = page };
        Refresh();
    }

    public Control View { get; }

    /// <summary>Re-reads settings and the open server (versions, folders).</summary>
    public void Refresh()
    {
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
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidDataException or TaskCanceledException)
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
