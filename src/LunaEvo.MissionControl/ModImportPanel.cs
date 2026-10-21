// LUNA EVO · LunaEvo.MissionControl/ModImportPanel.cs
// [UI] "Import from GameData" on the Mod Control page: scan a KSP install and fill the mod-control lists.
// Used by: MainWindow (Mod Control page)
// Guide:   Meat and Potatoes/12-Mod-Control-From-GameData.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using LunaEvo.MissionControl.Core;
using LunaEvo.MissionControl.Core.Mods;

namespace LunaEvo.MissionControl;

internal sealed class ModImportPanel
{
    private const string File = "LMPModControl.xml";

    private readonly MainWindow _host;
    private readonly ConfigSession _session;
    private readonly Action _changed;
    private readonly TextBox _path = new TextBox { PlaceholderText = @"...\Kerbal Space Program\GameData" }.WithId("modimport/path");
    private readonly ToggleSwitch _replace = new ToggleSwitch { OnContent = "", OffContent = "", MinWidth = 0, IsChecked = true }.WithId("modimport/replace");
    private readonly ToggleSwitch _plugins = new ToggleSwitch { OnContent = "", OffContent = "", MinWidth = 0, IsChecked = true }.WithId("modimport/plugins");
    private readonly ToggleSwitch _expansions = new ToggleSwitch { OnContent = "", OffContent = "", MinWidth = 0, IsChecked = false }.WithId("modimport/expansions");
    private readonly TextBlock _result = Ui.Text("", "sublabel").WithId("modimport/result");
    private readonly TextBlock _shareResult = Ui.Text("", "sublabel").WithId("modimport/shareResult");

    /// <summary>Tests replace the save dialog: returns the chosen path, or null if canceled.</summary>
    internal static Func<MainWindow, Task<string?>> PickSharePath { get; set; } = async host =>
        (await host.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save the mod rules for your players",
            SuggestedFileName = File,
            DefaultExtension = "xml",
        }))?.TryGetLocalPath();

    public ModImportPanel(MainWindow host, ConfigSession session, Action changed)
    {
        _host = host;
        _session = session;
        _changed = changed;
        _path.Text = GameDataScanner.FindKspGameData() ?? "";

        var browse = Ui.Button("Browse...", "text", "IconFolder").WithId("modimport/browse");
        browse.Click += async (_, _) => await BrowseAsync();
        var import = Ui.Button("Scan and import", "fab").WithId("modimport/import");
        import.Padding = new Thickness(18, 10);
        import.Click += async (_, _) => await ImportAsync();

        var pathRow = new DockPanel { Margin = new Thickness(12, 8) };
        DockPanel.SetDock(browse, Dock.Right);
        pathRow.Children.Add(browse);
        pathRow.Children.Add(_path);

        var (view, body, _) = Ui.Group("Import from GameData");
        body.Children.Add(new Border
        {
            Padding = new Thickness(12, 8, 12, 0),
            Child = Ui.Text("Point this at a KSP install's GameData folder with the mods you want to allow, and LUNA EVO fills in the " +
                            "allowed parts and resources (and optionally the plugin list), the same way LMP's in-game generator does. " +
                            "Turn on Mod Control in General settings for LMP to enforce it.", "sublabel"),
        });
        body.Children.Add(pathRow);
        body.Children.Add(Ui.Row("Replace the current lists", "Off adds to what's already allowed instead.", _replace));
        body.Children.Add(Ui.Row("Add plugins to Optional Plugins", "Every mod DLL (with its SHA-256), so these mods are allowed when non-listed plugins are off. LMP only checks hashes on Mandatory Plugins.", _plugins));
        body.Children.Add(Ui.Row("Require the expansions found", "Making History / Breaking Ground become Required Expansions.", _expansions));
        body.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Margin = new Thickness(12, 4, 12, 12), Children = { import, _result } });
        _result.VerticalAlignment = VerticalAlignment.Center;

        var share = Ui.Button("Save a copy...", "text", "IconSave").WithId("modimport/share");
        share.Click += async (_, _) => await ShareAsync();
        body.Children.Add(Ui.Row("Share these rules with players",
            "Saves a copy of LMPModControl.xml (as saved on disk) to send to your friends. On LUNA EVO's Join page, " +
            "\"Check my mods\" tells them exactly what to add or remove before they join.",
            new StackPanel { Spacing = 4, Children = { share, _shareResult } }));
        View = view;
    }

    public Control View { get; }

    /// <summary>Copies the saved LMPModControl.xml to a file the host can send to players.</summary>
    internal async Task ShareAsync()
    {
        if (_session.DirtyFiles.Contains(File))
        {
            _shareResult.Text = "Save first: the copy is the file on disk.";
            return;
        }
        var target = await PickSharePath(_host);
        if (string.IsNullOrEmpty(target))
            return;
        try
        {
            System.IO.File.Copy(Path.Combine(_session.ConfigDirectory, File), target, overwrite: true);
            _shareResult.Text = $"Saved {Path.GetFileName(target)}. Send it to your players.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _shareResult.Text = "Couldn't save it: " + e.Message;
        }
    }

    /// <summary>Scans the folder in the box and applies the result. Public for tests.</summary>
    internal async Task ImportAsync()
    {
        var folder = _path.Text?.Trim() ?? "";
        if (!Directory.Exists(folder))
        {
            _result.Text = "That folder doesn't exist.";
            return;
        }
        if (!string.Equals(Path.GetFileName(folder.TrimEnd('\\', '/')), "GameData", StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(Path.Combine(folder, "GameData")))
            folder = Path.Combine(folder, "GameData"); // they picked the KSP folder itself

        _result.Text = "Scanning...";
        var hashPlugins = _plugins.IsChecked == true; // read controls on the UI thread, not inside Task.Run
        GameDataScan scan;
        try
        {
            scan = await Task.Run(() => GameDataScanner.Scan(folder, hashPlugins));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _result.Text = $"Couldn't scan: {e.Message}";
            return;
        }

        var replace = _replace.IsChecked == true;
        IEnumerable<string> Merge(string setting, IEnumerable<string> found) =>
            replace ? found : _session.GetList(File, setting).Concat(found).Distinct(StringComparer.Ordinal);

        _session.SetList(File, "AllowedParts", Merge("AllowedParts", scan.Parts));
        _session.SetList(File, "AllowedResources", Merge("AllowedResources", scan.Resources));
        if (_expansions.IsChecked == true)
            _session.SetList(File, "RequiredExpansions", Merge("RequiredExpansions", scan.Expansions));
        if (_plugins.IsChecked == true)
        {
            var found = scan.Plugins.Select(p => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
            {
                ["Text"] = p.Text, ["Link"] = "", ["FilePath"] = p.FilePath, ["Sha"] = p.Sha,
            });
            var existing = replace ? [] : _session.GetObjectList(File, "OptionalPlugins").Where(e => scan.Plugins.All(p => p.FilePath != e.GetValueOrDefault("FilePath")));
            _session.SetObjectList(File, "OptionalPlugins", existing.Concat(found));
        }

        _result.Text = $"Found {scan.Parts.Count} parts, {scan.Resources.Count} resources, {scan.Plugins.Count} plugins and " +
                       $"{scan.Expansions.Count} expansions in {scan.ConfigFilesRead} .cfg files. Review, then Save." +
                       (scan.Warnings.Count > 0 ? $" {scan.Warnings.Count} file(s) couldn't be read." : "");
        _changed();
    }

    internal TextBox PathBox => _path;

    private async Task BrowseAsync()
    {
        var folders = await _host.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose KSP's GameData folder",
            AllowMultiple = false,
        });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path)
            _path.Text = path;
    }
}
