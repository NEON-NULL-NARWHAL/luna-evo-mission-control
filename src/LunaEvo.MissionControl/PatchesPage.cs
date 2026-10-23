// LUNA EVO · LunaEvo.MissionControl/PatchesPage.cs
// [UI] The LUNA EVO patches page: every LUNA EVO fix for LMP, what it does and what it measured, and installing them.
// Used by: MainWindow (rail: LUNA EVO → LUNA EVO patches)
// Guide:   Meat and Potatoes/27-Client-Patches.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using LunaEvo.MissionControl.Core.Join;
using LunaEvo.MissionControl.Core.Ops;

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
    private readonly TextBlock _result = Ui.Text("", "sublabel").WithId("patches/result");

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
        View = new ScrollViewer
        {
            Content = new StackPanel { Margin = new Thickness(28, 20, 28, 28), Spacing = 16, Children = { header, _result, ksp, server } },
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
    }

    private static Control Row(PatchInfo patch) =>
        Ui.Row(patch.Name, patch.What + (patch.Measured.Length > 0 ? $" Measured: {patch.Measured}." : "") + $" (since {patch.Since})", null);
}
