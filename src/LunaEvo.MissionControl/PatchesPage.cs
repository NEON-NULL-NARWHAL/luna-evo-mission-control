// LUNA EVO · LunaEvo.MissionControl/PatchesPage.cs
// [UI] The LUNA EVO patches page: every LUNA EVO fix for LMP, what it does and what it measured, and installing them;
//      and Tools: KSP Wingman (AI pilots), its mod in your KSP, kRPC, and a local model (Ollama).
// Used by: MainWindow (rail: LUNA EVO → LUNA EVO patches)
// Guide:   Meat and Potatoes/27-Client-Patches.md, 29-Wingman.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using LunaEvo.MissionControl.Core;
using LunaEvo.MissionControl.Core.Join;
using LunaEvo.MissionControl.Core.Ops;
using LunaEvo.MissionControl.Core.Setup;
using LunaEvo.MissionControl.Core.Wingman;

namespace LunaEvo.MissionControl;

/// <summary>
/// One place for all the fixes, for players and hosts alike: the client patches go into a KSP (the Join page's
/// folder), the server plugin into the open server. Installing uses the same steps, questions and checks as Join and
/// Map (KSP must be closed, the server stopped).
/// </summary>
internal sealed class PatchesPage
{
    private readonly MainWindow _host;
    private readonly StackPanel _ksp = new() { Spacing = 2 };
    private readonly StackPanel _server = new() { Spacing = 2 };
    private readonly StackPanel _tools = new() { Spacing = 2 };
    private readonly TextBlock _result = Ui.Text("", "sublabel").WithId("patches/result");
    private (string Version, IReadOnlyList<string> Models)? _ollama;
    private bool _ollamaChecked;
    private bool _busy;

    public PatchesPage(MainWindow host)
    {
        _host = host;
        var header = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Ui.Text("LUNA EVO patches", "headline"),
                Ui.Text("Fixes for LMP itself, each measured in a real game. Nothing new goes over the network, so you still " +
                        "play with people who don't have them. For LMP 0.29; on any other version they turn themselves off.", "sublabel"),
            },
        };
        _result.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        var (ksp, kspBody, _) = Ui.Group("In your KSP (for players)");
        kspBody.Children.Add(_ksp);
        var (server, serverBody, _) = Ui.Group("On your server (for hosts)");
        serverBody.Children.Add(_server);
        var (tools, toolsBody, _) = Ui.Group("Tools (for AI pilots)");
        toolsBody.Children.Add(_tools);
        View = new ScrollViewer
        {
            Content = new StackPanel { Margin = new Thickness(28, 20, 28, 28), Spacing = 16, Children = { header, _result, ksp, server, tools } },
        };
    }

    public Control View { get; }

    /// <summary>Re-reads where things are installed (when the page is shown, and after a change).</summary>
    public void Refresh()
    {
        _ksp.Children.Clear();
        var folder = _host.Join.KspBox.Text?.Trim() ?? "";
        var bytes = JoinPage.PatchBytes();
        string state;
        Control? action = null;
        if (!KspLauncher.IsKspFolder(folder))
            state = "No KSP chosen yet: pick its folder on Join a server → Play.";
        else if (KspLauncher.ClientVersion(folder) is not { } lmp)
            state = $"{folder}: LMP isn't installed in this KSP (Join a server → Play can install it).";
        else if (!ClientPatches.Supports(lmp))
            state = $"{folder}: LMP {lmp}. The patches are for LMP 0.29, so there's nothing to install here.";
        else if (bytes == null)
            state = "This build of LUNA EVO doesn't carry the patches.";
        else
        {
            var status = ClientPatches.Status(folder, bytes);
            state = $"{folder} · LMP {lmp} · " + status switch
            {
                PluginStatus.Installed => "installed, up to date",
                PluginStatus.Different => "an older version is installed",
                _ => "not installed",
            };
            var button = Ui.Button(status switch { PluginStatus.Installed => "Remove", PluginStatus.Different => "Update", _ => "Install" },
                status == PluginStatus.Installed ? "text" : "fab").WithId("patches/ksp");
            button.Click += async (_, _) =>
            {
                await _host.Join.TogglePatchesAsync();
                _result.Text = _host.Join.LastPatchMessage ?? "";
                Refresh();
            };
            action = button;
        }
        _ksp.Children.Add(Ui.Row("LUNA EVO patches for the LMP client", state, action).WithId("patches/kspStatus"));
        foreach (var patch in PatchCatalog.All.Where(p => p.Place == PatchPlace.Ksp))
            _ksp.Children.Add(Row(patch));

        _server.Children.Clear();
        var plugin = _host.Map.PluginState();
        string serverState;
        Control? serverAction = null;
        if (_host.Session is not { } session)
            serverState = "Open a server (Welcome → Open server) to install the plugin into it.";
        else if (plugin is not { } pluginStatus)
            serverState = $"This server runs LMP {session.Schema.ServerVersion}. The plugin is for LMP 0.29.";
        else
        {
            serverState = $"{session.ServerDirectory} · " + pluginStatus switch
            {
                PluginStatus.Installed => "installed, up to date",
                PluginStatus.Different => "an older version is installed",
                _ => "not installed",
            } + (_host.Server?.IsRunning == true ? " · stop the server to change it" : "");
            var button = Ui.Button(pluginStatus switch { PluginStatus.Installed => "Remove", PluginStatus.Different => "Update", _ => "Install" },
                pluginStatus == PluginStatus.Installed ? "text" : "fab").WithId("patches/server");
            button.Click += async (_, _) =>
            {
                await _host.Map.TogglePluginAsync();
                Refresh();
            };
            serverAction = button;
        }
        _server.Children.Add(Ui.Row("LUNA EVO server plugin", serverState, serverAction).WithId("patches/serverStatus"));
        foreach (var patch in PatchCatalog.All.Where(p => p.Place == PatchPlace.Server))
            _server.Children.Add(Row(patch));
        RefreshTools(folder);
    }

    /// <summary>KSP Wingman, its mod in the Join page's KSP (with kRPC), and Ollama with a model.</summary>
    private void RefreshTools(string ksp)
    {
        _tools.Children.Clear();
        var install = WingmanTools.Installed();
        var get = Ui.Button(install == null ? "Install" : "Check for updates", install == null ? "fab" : "text").WithId("patches/wingman");
        get.IsEnabled = !_busy;
        get.Click += async (_, _) => await InstallWingmanAsync();
        _tools.Children.Add(Ui.Row("KSP Wingman", (install == null ? "Not installed. " : $"{install.Version} installed. ") +
            "AI pilots for KSP: a local model flies your plane through kRPC, takes orders in plain words and talks to the KSC tower. " +
            "From its GitHub releases, checked against GitHub's checksum.", get).WithId("patches/wingmanStatus"));

        if (install != null)
        {
            string state;
            Control? action = null;
            var bridge = WingmanTools.Bridge(ksp, install);
            if (bridge == BridgeState.NoKsp)
                state = "No KSP chosen yet: pick its folder on Join a server → Play.";
            else
            {
                var krpc = WingmanTools.HasKrpc(ksp);
                state = $"{ksp} · " + bridge switch
                {
                    BridgeState.Installed => "Wingman's mod is installed",
                    BridgeState.Different => "another version of Wingman's mod is installed",
                    _ => "Wingman's mod isn't installed",
                } + (krpc ? " · kRPC is installed." : " · kRPC isn't: Wingman needs it (CKAN mods → kRPC).");
                if (bridge != BridgeState.Installed)
                {
                    var button = Ui.Button(bridge == BridgeState.Different ? "Update the mod" : "Install the mod", "fab").WithId("patches/bridge");
                    button.Click += (_, _) => InstallBridge(ksp, install);
                    action = button;
                }
                else if (!krpc)
                {
                    var ckan = Ui.Button("CKAN mods", "text").WithId("patches/krpc");
                    ckan.Click += (_, _) => _host.Navigate(MainWindow.CkanKey);
                    action = ckan;
                }
            }
            _tools.Children.Add(Ui.Row("Wingman in your KSP", state, action).WithId("patches/bridgeStatus"));
        }

        if (!_ollamaChecked)
        {
            _ollamaChecked = true;
            _ = CheckOllamaAsync();
        }
        string modelState;
        Control? modelAction = null;
        if (_ollama is not { } ollama)
            modelState = "Ollama isn't running on this PC (ollama.com). Wingman can also use any OpenAI-compatible model server.";
        else if (ollama.Models.Any(m => m.Equals(WingmanTools.DefaultModel, StringComparison.OrdinalIgnoreCase)))
            modelState = $"Ollama {ollama.Version} · {WingmanTools.DefaultModel} is ready ({ollama.Models.Count} model(s)).";
        else
        {
            modelState = $"Ollama {ollama.Version} · {WingmanTools.DefaultModel} (2.5 GB, the model Wingman is tested with) isn't downloaded.";
            var pull = Ui.Button("Download it", "fab").WithId("patches/pull");
            pull.IsEnabled = !_busy;
            pull.Click += async (_, _) => await PullAsync();
            modelAction = pull;
        }
        if (modelAction == null)
        {
            var again = Ui.Button("Check again", "text").WithId("patches/ollamaCheck");
            again.Click += async (_, _) => await CheckOllamaAsync();
            modelAction = again;
        }
        _tools.Children.Add(Ui.Row("Local model (Ollama)", modelState, modelAction).WithId("patches/ollamaStatus"));
    }

    private void InstallBridge(string ksp, WingmanInstall install)
    {
        try
        {
            WingmanTools.InstallBridge(ksp, install);
            _result.Text = $"Wingman's mod is in {ksp}. Start KSP, start kRPC's server and load a game.";
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            _result.Text = "Couldn't install Wingman's mod: " + e.Message;
        }
        Refresh();
    }

    internal async Task CheckOllamaAsync()
    {
        _ollama = await WingmanTools.OllamaAsync(Http.Client);
        Refresh();
    }

    internal async Task InstallWingmanAsync()
    {
        _busy = true;
        Refresh();
        try
        {
            var installed = WingmanTools.Installed();
            if (await WingmanTools.LatestAsync(Http.Client) is not { } latest)
                _result.Text = "No KSP Wingman release with a checksum was found on GitHub.";
            else if (installed != null && Version.Parse(installed.Version) >= latest.Release.Version!)
                _result.Text = $"KSP Wingman {installed.Version} is the newest.";
            else
            {
                var progress = new Progress<double>(p => _result.Text = $"Downloading KSP Wingman {latest.Release.Version}: {p:P0}");
                var install = await WingmanTools.DownloadAndInstallAsync(Http.Client, latest.Release, latest.Zip, progress);
                _result.Text = $"KSP Wingman {install.Version} installed. Next: its mod in your KSP (below), then Play → Wingman.";
                _host.ShowWingmanNav();
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or DownloadVerificationException or InvalidDataException
                                       or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            _result.Text = "Couldn't install KSP Wingman: " + e.Message;
        }
        _busy = false;
        Refresh();
    }

    private async Task PullAsync()
    {
        _busy = true;
        Refresh();
        _result.Text = $"Ollama is downloading {WingmanTools.DefaultModel}...";
        var progress = new Progress<double>(p => _result.Text = $"Ollama is downloading {WingmanTools.DefaultModel}: {p:P0}");
        var problem = await WingmanTools.PullAsync(Http.Client, WingmanTools.DefaultModel, progress);
        _result.Text = problem ?? $"{WingmanTools.DefaultModel} is ready.";
        _busy = false;
        await CheckOllamaAsync();
    }

    private static Control Row(PatchInfo patch) =>
        Ui.Row(patch.Name, patch.What + (patch.Measured.Length > 0 ? $" Measured: {patch.Measured}." : "") + $" (since {patch.Since})", null);
}
