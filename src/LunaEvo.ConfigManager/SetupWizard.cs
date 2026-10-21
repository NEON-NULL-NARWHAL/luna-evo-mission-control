// LUNA EVO · LunaEvo.ConfigManager/SetupWizard.cs
// [UI] "Set up a new server": download LMP from GitHub, write the config from a few questions, check .NET, open it.
// Used by: MainWindow (welcome page)
// Guide:   Meat and Potatoes/10-Setup-and-Updates.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using LunaEvo.ConfigManager.Core;
using LunaEvo.ConfigManager.Core.Schema;
using LunaEvo.ConfigManager.Core.Server;
using LunaEvo.ConfigManager.Core.Setup;
using LunaEvo.ConfigManager.Core.Validation;

namespace LunaEvo.ConfigManager;

internal sealed class SetupWizard
{
    private readonly MainWindow _host;
    private readonly TextBox _folder = new TextBox { Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "LMPServer"), Width = 420 }.WithId("wizard/folder");
    private readonly ComboBox _version = new ComboBox { Width = 260, PlaceholderText = "Loading releases..." }.WithId("wizard/version");
    private readonly ToggleSwitch _prereleases = Toggle("wizard/prereleases", false);
    private readonly TextBox _name = new TextBox { Text = "LUNA EVO Server", Width = 320, MaxLength = 30 }.WithId("wizard/name");
    private readonly TextBox _password = new TextBox { Width = 320, MaxLength = 30, PasswordChar = '•', PlaceholderText = "Optional" }.WithId("wizard/password");
    private readonly ComboBox _mode = new ComboBox { ItemsSource = new[] { "Sandbox", "Science", "Career" }, SelectedItem = "Sandbox", Width = 200 }.WithId("wizard/mode");
    private readonly ComboBox _difficulty = new ComboBox { ItemsSource = new[] { "Easy", "Normal", "Moderate", "Hard" }, SelectedItem = "Normal", Width = 200 }.WithId("wizard/difficulty");
    private readonly NumericUpDown _maxPlayers = new NumericUpDown { Value = 10, Minimum = 1, Maximum = 100, Increment = 1, FormatString = "0", Width = 200 }.WithId("wizard/maxPlayers");
    private readonly NumericUpDown _port = new NumericUpDown { Value = 8800, Minimum = 1024, Maximum = 65535, Increment = 1, FormatString = "0", Width = 200 }.WithId("wizard/port");
    private readonly ToggleSwitch _listed = Toggle("wizard/listed", false);
    private readonly ToggleSwitch _upnp = Toggle("wizard/upnp", false);
    private readonly ToggleSwitch _website = Toggle("wizard/website", true);
    private readonly ToggleSwitch _modControl = Toggle("wizard/modControl", false);
    private readonly ToggleSwitch _copyApp = Toggle("wizard/copyApp", true);
    private readonly ToggleSwitch _getCkan = Toggle("wizard/getCkan", false);
    private readonly ToggleSwitch _getClient = Toggle("wizard/getClient", false);
    private readonly Button _install = Ui.Button("Download and set up", "fab", "IconPlay").WithId("wizard/install");
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 1, Height = 6, IsVisible = false };
    private readonly StackPanel _log = new() { Spacing = 2 };
    private IReadOnlyList<LmpRelease> _releases = [];

    public SetupWizard(MainWindow host)
    {
        _host = host;
        _prereleases.IsCheckedChanged += async (_, _) => await LoadReleasesAsync();
        _install.Click += async (_, _) => await InstallAsync();
        _install.HorizontalAlignment = HorizontalAlignment.Left;
        _install.Padding = new Thickness(22, 12);

        var browse = Ui.Button("Browse...", "text", "IconFolder");
        browse.Click += async (_, _) =>
        {
            var folders = await _host.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Where should the server live?" });
            if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path)
                _folder.Text = path;
        };
        var back = Ui.Button("Back", "text").WithId("wizard/back");
        back.Click += (_, _) => _host.ShowWelcomePage();

        var (where, whereBody, _) = Ui.Group("1 · Where and which version");
        whereBody.Children.Add(Ui.Row("Folder", "A new or empty folder. The server, its config and the game universe all live here.",
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _folder, browse } }));
        whereBody.Children.Add(Ui.Row("LMP version", "Downloaded from Luna Multiplayer's GitHub releases. Players need the same version of the LMP mod.", _version));
        whereBody.Children.Add(Ui.Row("Show pre-releases", "Newer, less tested versions.", _prereleases));

        var (server, serverBody, _) = Ui.Group("2 · Your server");
        serverBody.Children.Add(Ui.Row("Server name", "Shown to players. Up to 30 characters.", _name));
        serverBody.Children.Add(Ui.Row("Password", "Leave empty for no password. Players type it when they join.", _password));
        serverBody.Children.Add(Ui.Row("Game mode", "Sandbox: everything unlocked. Science: tech tree. Career: tech tree, funds and contracts.", _mode));
        serverBody.Children.Add(Ui.Row("Difficulty", "Sets the gameplay rules (reverting, re-entry heat, etc.). Fine-tune later on the Gameplay page.", _difficulty));
        serverBody.Children.Add(Ui.Row("Max players", null, _maxPlayers));
        serverBody.Children.Add(Ui.Row("Game port (UDP)", "8800 is LMP's usual port.", _port));
        serverBody.Children.Add(Ui.Row("Show on the public server list", "Off for a private server: friends join with your address instead. LMP's default is on.", _listed));
        serverBody.Children.Add(Ui.Row("Let LMP open router ports (UPnP)", "Only needed for players outside your network. LMP's default is on.", _upnp));
        serverBody.Children.Add(Ui.Row("Check players' mods (Mod Control)",
            "On: players can only use the parts and plugins allowed on the Mod Control page (stock, unless you import your GameData). " +
            "Off: any mods. LMP's default is on, but it suggests off is fine for a private server.", _modControl));
        serverBody.Children.Add(Ui.Row("Live player info", "Turns on LMP's website for this PC only, so LUNA EVO can show who's playing.", _website));
        serverBody.Children.Add(Ui.Row("Put LUNA EVO in the server folder", "So double-clicking it there opens this server straight away.", _copyApp));

        var (play, playBody, _) = Ui.Group("3 · Also for playing (optional)");
        playBody.Children.Add(Ui.Row("Download CKAN", "The KSP mod manager, verified and kept by LUNA EVO (Settings → Downloads has it too, any time).", _getCkan));
        var ksp = Core.Join.KspLauncher.FindKsp();
        playBody.Children.Add(Ui.Row("Install the matching LMP client into my KSP",
            ksp != null ? $"The same LMP version as this server, into {ksp}. Your LMP settings are kept." : "No KSP found in your Steam libraries; use Settings → Downloads later.", _getClient));
        _getClient.IsEnabled = ksp != null;

        var (go, goBody, _) = Ui.Group("4 · Set it up");
        goBody.Children.Add(new StackPanel { Margin = new Thickness(12), Spacing = 10, Children = { _install, _progress, _log } });

        View = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(28, 20, 28, 28),
                Spacing = 4,
                Children =
                {
                    new DockPanel { Children = { back } },
                    Ui.Text("Set up a new LMP server", "headline"),
                    Ui.Text("Answer a few questions and LUNA EVO downloads Luna Multiplayer's server and configures it. Nothing starts until you press Start.", "sublabel"),
                    new Border { Height = 12 },
                    where, server, play, go,
                },
            },
        };
        DockPanel.SetDock(back, Dock.Left);
    }

    public Control View { get; }

    /// <summary>Fills the version list from GitHub. Called when the wizard opens.</summary>
    internal async Task LoadReleasesAsync()
    {
        _version.ItemsSource = null;
        _version.PlaceholderText = "Loading releases...";
        try
        {
            _releases = await new LmpReleaseFeed(Http.Client).GetAsync(_prereleases.IsChecked == true);
            _version.ItemsSource = _releases;
            _version.SelectedIndex = _releases.Count > 0 ? 0 : -1;
            _version.PlaceholderText = _releases.Count > 0 ? "" : "No releases found";
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            _version.PlaceholderText = "Couldn't reach GitHub";
            Log($"Couldn't load LMP's releases: {e.Message}. Check the internet connection.");
        }
    }

    /// <summary>The optional extras: CKAN and the matching LMP client. A failure here never fails the server setup.</summary>
    private async Task GetPlayerToolsAsync(LmpRelease release)
    {
        if (_getCkan.IsChecked == true)
        {
            try
            {
                Log("Downloading CKAN...");
                var available = await CkanTools.AvailableAsync(Http.Client);
                Log(available.Count == 0 ? "No verifiable CKAN release found; skipped." :
                    $"CKAN {available[0].Release.Tag} downloaded and verified: {SettingsPage.Friendly(await CkanTools.DownloadAsync(Http.Client, available[0].Release, available[0].Exe))}");
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or DownloadVerificationException or IOException or UnauthorizedAccessException)
            {
                Log($"CKAN wasn't downloaded ({e.Message}). Try Settings → Downloads later.");
            }
        }
        if (_getClient.IsChecked == true && Core.Join.KspLauncher.FindKsp() is { } ksp && LmpClientInstaller.ClientAsset(release) is { Sha256: { } sha } client)
        {
            try
            {
                Log($"Installing the LMP {release.Tag} client into {ksp}...");
                var zip = await new ServerInstaller(Http.Client).DownloadAsync(client, null, allowUnverified: false);
                await Task.Run(() => LmpClientInstaller.Install(zip, ksp, sha));
                Log("LMP client installed (checksum verified).");
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or DownloadVerificationException or IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException)
            {
                Log($"The LMP client wasn't installed ({e.Message}). Try Settings → Downloads later.");
            }
        }
    }

    /// <summary>Download, extract, write config, check .NET, copy LUNA EVO, open. Public for tests.</summary>
    internal async Task InstallAsync()
    {
        var folder = _folder.Text?.Trim() ?? "";
        if (folder.Length == 0 || _version.SelectedItem is not LmpRelease release || release.ServerAsset() is not { } asset)
        {
            Log("Pick a folder and a version first.");
            return;
        }
        if (ServerInstaller.IsServerFolder(folder))
        {
            if (await Dialogs.ConfirmAsync(_host, "There's already a server here",
                    $"{folder} already has an LMP server. Open it instead?", "Open it"))
                _host.Open(folder, null);
            return;
        }
        if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any() &&
            !await Dialogs.ConfirmAsync(_host, "Folder isn't empty", $"{folder} already has files in it. Set the server up there anyway?", "Use it"))
            return;

        _install.IsEnabled = false;
        _log.Children.Clear();
        try
        {
            if (asset.Sha256 == null && !await Dialogs.ConfirmAsync(_host, "Can't verify this download",
                    $"GitHub has no checksum for {asset.Name}, so LUNA EVO can't check the file wasn't tampered with on the way. Install it anyway?",
                    "Install anyway"))
                return;
            Log($"Downloading {asset.Name} ({asset.Size / 1024.0 / 1024.0:0.0} MB) from LMP {release.Tag}...");
            _progress.IsVisible = true;
            var zip = await new ServerInstaller(Http.Client).DownloadAsync(asset, new Progress<double>(v => _progress.Value = v), allowUnverified: asset.Sha256 == null);
            Log(asset.Sha256 != null ? "Checksum verified against GitHub's SHA-256." : "Downloaded (not verified: GitHub has no checksum for it).");

            Log("Unpacking...");
            await Task.Run(() => ServerInstaller.Install(zip, folder, asset.Sha256));

            Log("Writing your config (the server hasn't started yet, so nothing is public)...");
            var schema = await Task.Run(() => LmpSchemaLoader.Load(folder));
            ConfigGenerator.CreateMissing(schema, Path.Combine(folder, "Config"), Choices());

            if (_copyApp.IsChecked == true)
                CopyAppInto(folder);

            if (DotNetRuntime.Check(folder) is { Installed: false } runtime)
            {
                Log($"This LMP version needs the .NET {runtime.Major} Runtime, which isn't installed.");
                if (await Dialogs.ChooseAsync(_host, $".NET {runtime.Major} Runtime needed",
                        $"LMP {release.Tag} needs the .NET {runtime.Major} Runtime to run. Install it now with Windows' package manager? Windows will ask for permission.",
                        "Install it now", "Later") == 0)
                {
                    Log($"Installing .NET {runtime.Major} with winget...");
                    Log(await RuntimeInstaller.InstallAsync(runtime.Major) ? "Installed." : $"That didn't work; get it from {SettingsPage.DotNet6Download}");
                }
            }

            await GetPlayerToolsAsync(release);

            Log("Done! Opening your server.");
            _host.Open(folder, null);
            _host.Navigate(MainWindow.ConsolePage);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or TaskCanceledException or DownloadVerificationException)
        {
            Log($"Setup failed: {e.Message}");
        }
        finally
        {
            _install.IsEnabled = true;
            _progress.IsVisible = false;
        }
    }

    internal IReadOnlyDictionary<(string File, string Setting), string> Choices() => new Dictionary<(string, string), string>
    {
        [(LmpRules.General, "ServerName")] = _name.Text?.Trim() is { Length: > 0 } name ? name : "LUNA EVO Server",
        [(LmpRules.General, "Password")] = _password.Text ?? "",
        [(LmpRules.General, "GameMode")] = _mode.SelectedItem as string ?? "Sandbox",
        [(LmpRules.General, "GameDifficulty")] = _difficulty.SelectedItem as string ?? "Normal",
        [(LmpRules.General, "MaxPlayers")] = ((int)(_maxPlayers.Value ?? 10)).ToString(),
        [(LmpRules.Connection, "Port")] = ((int)(_port.Value ?? 8800)).ToString(),
        [("MasterServerSettings.xml", "RegisterWithMasterServer")] = _listed.IsChecked == true ? "true" : "false",
        [(LmpRules.Connection, "Upnp")] = _upnp.IsChecked == true ? "true" : "false",
        [(LmpRules.Website, "EnableWebsite")] = _website.IsChecked == true ? "true" : "false",
        [(LmpRules.Website, "ListenAddress")] = "127.0.0.1",
        [(LmpRules.General, "ModControl")] = _modControl.IsChecked == true ? "true" : "false",
    };

    internal TextBox FolderBox => _folder;
    internal TextBox NameBox => _name;

    private void CopyAppInto(string folder)
    {
        var exe = Environment.ProcessPath;
        // Only the published single-file exe can be copied on its own (a dev build needs its DLLs next to it).
        if (exe == null || File.Exists(Path.Combine(Path.GetDirectoryName(exe)!, "LunaEvo.ConfigManager.dll")))
        {
            Log("(Running from a dev build, so LUNA EVO wasn't copied into the folder.)");
            return;
        }
        File.Copy(exe, Path.Combine(folder, Path.GetFileName(exe)), overwrite: true);
        Log($"Copied LUNA EVO to {folder}.");
    }

    private void Log(string text) => _log.Children.Add(Ui.Text(text, "sublabel"));

    private static ToggleSwitch Toggle(string id, bool on) => new ToggleSwitch { OnContent = "", OffContent = "", MinWidth = 0, IsChecked = on }.WithId(id);
}
