// LUNA EVO · LunaEvo.ConfigManager/CkanPage.cs
// [UI] CKAN: is it installed and used, your CKAN installs, and "install a mod list" (paste or load a .ckan, CKAN downloads it).
// Used by: MainWindow
// Guide:   Meat and Potatoes/22-CKAN-and-LMP-Client.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LunaEvo.ConfigManager.Core;
using LunaEvo.ConfigManager.Core.Setup;

namespace LunaEvo.ConfigManager;

internal sealed class CkanPage
{
    /// <summary>Tests replace this so CKAN's window is never opened: (ckan.exe) → started.</summary>
    internal static Func<string, bool> StartCkan { get; set; } = exe =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! }) != null;

    /// <summary>Tests replace this: runs ckan.exe with arguments, reporting each output line; returns the exit code.</summary>
    internal static Func<string, IReadOnlyList<string>, Action<string>, Task<int>> RunCkan { get; set; } = RunProcessAsync;

    /// <summary>Tests replace the open dialog: → chosen .ckan path, or null.</summary>
    internal static Func<MainWindow, Task<string?>> PickModList { get; set; } = async host =>
        (await host.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load a CKAN mod list",
            FileTypeFilter = [new FilePickerFileType("CKAN mod list") { Patterns = ["*.ckan"] }],
        })).FirstOrDefault()?.TryGetLocalPath();

    private readonly MainWindow _host;
    private readonly StackPanel _status = new() { Spacing = 2 };
    private readonly StackPanel _installs = new() { Spacing = 2 };
    private readonly TextBox _list = new TextBox
    {
        AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Height = 150,
        PlaceholderText = "Paste a .ckan mod list (CKAN → File → Export modpack), or mod names one per line, e.g.\nKerbalEngineerRedux\nMechJeb2",
    }.WithId("ckan/list");
    private readonly ComboBox _target = new ComboBox { MinWidth = 360 }.WithId("ckan/target");
    private readonly Button _install = Ui.Button("Install with CKAN", "fab").WithId("ckan/install");
    private readonly TextBlock _result = Ui.Text("", "sublabel").WithId("ckan/result");
    private readonly ListBox _log = new ListBox { Height = 160, IsVisible = false }.WithId("ckan/log");
    private bool _busy;

    public CkanPage(MainWindow host)
    {
        _host = host;
        _result.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        _log.Classes.Add("console");
        var load = Ui.Button("Load a .ckan file...", "text", "IconFolder").WithId("ckan/load");
        load.Click += async (_, _) =>
        {
            if (await PickModList(_host) is { } path)
                _list.Text = File.ReadAllText(path);
        };
        _install.HorizontalAlignment = HorizontalAlignment.Left;
        _install.Click += async (_, _) => await InstallListAsync();
        var refresh = Ui.Button("Refresh", "text", "IconRestart").WithId("ckan/refresh");
        refresh.Click += (_, _) => Refresh();

        var (status, statusBody, _) = Ui.Group("CKAN");
        statusBody.Children.Add(_status);
        var (installs, installsBody, _) = Ui.Group("Your KSP installs (from CKAN)");
        installsBody.Children.Add(_installs);
        var (list, listBody, _) = Ui.Group("Install a mod list");
        listBody.Children.Add(Ui.Row("Mods to install",
            "From a friend or a server host: a CKAN modpack (.ckan) or mod names. LUNA EVO reads only the mod names and versions " +
            "and CKAN installs them from its own catalogue (a file's own download links are never used).", null));
        listBody.Children.Add(new StackPanel { Margin = new Thickness(12, 0, 12, 8), Spacing = 6, Children = { _list, load } });
        listBody.Children.Add(Ui.Row("Into this KSP install", "Close KSP and CKAN's window for this install first.", _target));
        listBody.Children.Add(new StackPanel { Margin = new Thickness(12, 4, 12, 12), Spacing = 6, Children = { _install, _result, _log } });

        var header = new DockPanel();
        DockPanel.SetDock(refresh, Dock.Right);
        header.Children.Add(refresh);
        header.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Ui.Text("CKAN", "headline"),
                Ui.Text("The KSP mod manager. LUNA EVO keeps a verified copy and uses it to install mod lists; your CKAN installs are listed here and on the Join page.", "sublabel"),
            },
        });
        View = new ScrollViewer
        {
            Content = new StackPanel { Margin = new Thickness(28, 20, 28, 28), Spacing = 16, Children = { header, status, installs, list } },
        };
    }

    public Control View { get; }

    /// <summary>The newest CKAN copy LUNA EVO has, or null.</summary>
    internal static string? CkanExe => CkanTools.Installed().FirstOrDefault().Exe;

    public void Refresh()
    {
        _status.Children.Clear();
        var installed = CkanTools.Installed();
        var download = Ui.Button(installed.Count == 0 ? "Download CKAN" : "Get the latest CKAN", "fab").WithId("ckan/download");
        download.Click += async (_, _) => await DownloadAsync();
        if (installed.Count == 0)
        {
            _status.Children.Add(Ui.Row("Not installed", "LUNA EVO hasn't downloaded CKAN yet. It needs CKAN to install mod lists.", download).WithId("ckan/state"));
        }
        else
        {
            var (version, exe) = installed[0];
            var open = Ui.Button("Open CKAN", "text").WithId("ckan/open");
            open.Click += (_, _) => _result.Text = StartCkan(exe) ? $"CKAN {version} is starting." : "CKAN didn't start.";
            _status.Children.Add(Ui.Row($"Installed: CKAN {version}",
                $"LUNA EVO uses this copy for mod lists. Kept in {SettingsPage.Friendly(Path.GetDirectoryName(exe)!)}, checked against GitHub's SHA-256.",
                new StackPanel { Orientation = Orientation.Horizontal, Children = { open, download } }).WithId("ckan/state"));
            foreach (var (older, _) in installed.Skip(1))
            {
                var remove = Ui.Button("Remove", "text").WithId("ckan/remove/" + older);
                remove.Click += (_, _) =>
                {
                    CkanTools.Remove(older);
                    Refresh();
                };
                _status.Children.Add(Ui.Row($"Also kept: CKAN {older}", "An older version, in case you need it.", remove));
            }
        }
        var running = System.Diagnostics.Process.GetProcessesByName("ckan");
        foreach (var process in running)
            process.Dispose();
        if (running.Length > 0)
            _status.Children.Add(Ui.Row("CKAN is open right now", "Close it before installing a mod list into the install it has open.", null).WithId("ckan/running"));

        _installs.Children.Clear();
        var instances = CkanTools.Instances();
        foreach (var instance in instances)
        {
            var lmp = Core.Join.KspLauncher.ClientVersion(instance.Path) is { } v ? $"LMP {v}" : "no LMP";
            var mods = CkanModList.InstalledCount(instance.Path) is { } n ? $"{n} mods through CKAN" : "no CKAN mod list";
            _installs.Children.Add(Ui.Row(instance.Name, $"{instance.Path} · {lmp} · {mods}{(CkanModList.IsLocked(instance.Path) ? " · open in CKAN" : "")}", null)
                .WithId("ckan/instance/" + instance.Name));
        }
        if (instances.Count == 0)
            _installs.Children.Add(Ui.Row("No CKAN installs found", "Open CKAN once and add your KSP folder, or use Join → Browse for an install without CKAN.", null));

        var targets = instances.Select(i => i.Path).ToList();
        if (_host.Settings.KspFolder is { } own && Core.Join.KspLauncher.IsKspFolder(own) && !targets.Contains(own, StringComparer.OrdinalIgnoreCase))
            targets.Add(own);
        var selected = _target.SelectedItem as string;
        _target.ItemsSource = targets;
        _target.SelectedItem = selected != null && targets.Contains(selected) ? selected : targets.FirstOrDefault();
        _install.IsEnabled = !_busy;
    }

    internal async Task DownloadAsync()
    {
        _result.Text = "Asking GitHub for CKAN's releases...";
        try
        {
            var available = await CkanTools.AvailableAsync(Http.Client);
            if (available.Count == 0)
            {
                _result.Text = "No verifiable CKAN release found on GitHub.";
                return;
            }
            var (release, exe) = available[0];
            var done = false;
            var progress = new Progress<double>(p => { if (!done) _result.Text = $"Downloading CKAN {release.Tag}... {p:P0}"; });
            var path = await CkanTools.DownloadAsync(Http.Client, release, exe, progress);
            done = true;
            _result.Text = $"CKAN {release.Tag} downloaded and verified.";
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or DownloadVerificationException or IOException or UnauthorizedAccessException)
        {
            _result.Text = "Couldn't download CKAN: " + e.Message;
        }
        Refresh();
    }

    internal async Task InstallListAsync()
    {
        if (_busy)
            return;
        var (mods, problems) = CkanModList.Parse(_list.Text);
        if (mods.Count == 0)
        {
            _result.Text = problems.Count > 0 ? string.Join(" ", problems) : "Paste a mod list or load a .ckan file first.";
            return;
        }
        if (_target.SelectedItem is not string ksp || !Core.Join.KspLauncher.IsKspFolder(ksp))
        {
            _result.Text = "Pick the KSP install to put the mods in.";
            return;
        }
        if (Core.Join.KspLauncher.IsRunning(ksp))
        {
            _result.Text = "Close KSP first.";
            return;
        }
        if (CkanModList.IsLocked(ksp))
        {
            _result.Text = "CKAN has this install open. Close CKAN's window, then try again.";
            return;
        }
        if (CkanExe is not { } ckan)
        {
            _result.Text = "Download CKAN first (above).";
            return;
        }
        var names = string.Join(", ", mods.Take(12).Select(m => m.Identifier)) + (mods.Count > 12 ? $" and {mods.Count - 12} more" : "");
        if (!await Dialogs.ConfirmAsync(_host, $"Install {mods.Count} mod(s) with CKAN?",
                $"Into {ksp}: {names}. CKAN works out the dependencies and downloads everything from its catalogue. " +
                (problems.Count > 0 ? string.Join(" ", problems) : ""), "Install"))
            return;

        _busy = true;
        _install.IsEnabled = false;
        _log.IsVisible = true;
        var lines = new System.Collections.ObjectModel.ObservableCollection<string>();
        _log.ItemsSource = lines;
        void Line(string text) => Dispatcher.UIThread.Post(() =>
        {
            lines.Add(text);
            if (lines.Count > 500)
                lines.RemoveAt(0);
            _log.ScrollIntoView(lines.Count - 1);
        });
        try
        {
            _result.Text = "Refreshing CKAN's catalogue...";
            await RunCkan(ckan, CkanModList.UpdateArguments(ksp), Line);
            _result.Text = $"Installing {mods.Count} mod(s)...";
            var exit = 0;
            foreach (var command in CkanModList.InstallCommands(ksp, mods))
                if ((exit = await RunCkan(ckan, command, Line)) != 0)
                    break;
            _result.Text = exit == 0
                ? $"Done: CKAN installed the mod list into {ksp}."
                : $"CKAN stopped with an error (code {exit}), often a mod name it doesn't know or one that doesn't fit this KSP version; " +
                  "see its messages below. CKAN undoes an install it couldn't finish.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _result.Text = "Couldn't run CKAN: " + e.Message;
        }
        finally
        {
            _busy = false;
            Refresh();
        }
    }

    private static async Task<int> RunProcessAsync(string exe, IReadOnlyList<string> arguments, Action<string> line)
    {
        var info = new System.Diagnostics.ProcessStartInfo(exe)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(exe)!,
        };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(info) ?? throw new InvalidOperationException("CKAN didn't start.");
        process.OutputDataReceived += (_, e) => { if (e.Data != null) line(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) line(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
