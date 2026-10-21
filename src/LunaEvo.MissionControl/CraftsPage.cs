// LUNA EVO · LunaEvo.MissionControl/CraftsPage.cs
// [UI] Crafts: your KSP craft designs and the server's craft library; export to .lunacraft, import with a part check.
// Used by: MainWindow
// Guide:   Meat and Potatoes/19-Sharing-Crafts.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using LunaEvo.MissionControl.Core.Join;
using LunaEvo.MissionControl.Core.Mods;

namespace LunaEvo.MissionControl;

internal sealed class CraftsPage
{
    /// <summary>Tests replace the save dialog: (suggested file name) → chosen path, or null.</summary>
    internal static Func<MainWindow, string, Task<string?>> PickExportPath { get; set; } = async (host, suggested) =>
        (await host.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export craft",
            SuggestedFileName = suggested,
            DefaultExtension = "lunacraft",
            FileTypeChoices = [new FilePickerFileType("LUNA EVO craft") { Patterns = ["*" + CraftShare.Extension] }],
        }))?.TryGetLocalPath();

    /// <summary>Tests replace the open dialog: → chosen path, or null.</summary>
    internal static Func<MainWindow, Task<string?>> PickImportPath { get; set; } = async host =>
        (await host.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import a craft",
            FileTypeFilter = [new FilePickerFileType("LUNA EVO craft") { Patterns = ["*" + CraftShare.Extension] }],
        })).FirstOrDefault()?.TryGetLocalPath();

    private readonly MainWindow _host;
    private readonly StackPanel _mine = new() { Spacing = 2 };
    private readonly StackPanel _server = new() { Spacing = 2 };
    private readonly Control _serverGroup;
    private readonly ComboBox _save = new ComboBox { MinWidth = 200 }.WithId("crafts/save");
    private readonly TextBlock _result = Ui.Text("", "sublabel").WithId("crafts/result");

    public CraftsPage(MainWindow host)
    {
        _host = host;
        var import = Ui.Button("Import a craft...", "fab").WithId("crafts/import");
        import.Padding = new Thickness(18, 8);
        import.Click += async (_, _) => await ImportAsync();
        var refresh = Ui.Button("Refresh", "text", "IconRestart").WithId("crafts/refresh");
        refresh.Click += (_, _) => Refresh();
        _result.TextWrapping = Avalonia.Media.TextWrapping.Wrap;

        var (importGroup, importBody, _) = Ui.Group("Import");
        importBody.Children.Add(Ui.Row("Import a .lunacraft file",
            "Checks the craft's parts against your KSP first. It goes into the KSP save you pick, or the server's craft library.", null));
        importBody.Children.Add(Ui.Row("KSP save to import into", null, _save));
        importBody.Children.Add(new StackPanel { Margin = new Thickness(12, 4, 12, 12), Spacing = 8, Children = { import, _result } });
        import.HorizontalAlignment = HorizontalAlignment.Left;

        var (mineGroup, mineBody, _) = Ui.Group("Your crafts (KSP)");
        mineBody.Children.Add(_mine);
        var (serverGroup, serverBody, _) = Ui.Group("Server craft library");
        serverBody.Children.Add(_server);
        _serverGroup = serverGroup;

        var header = new DockPanel();
        DockPanel.SetDock(refresh, Dock.Right);
        header.Children.Add(refresh);
        header.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Ui.Text("Crafts", "headline"),
                Ui.Text("Share craft designs with friends: export one to a .lunacraft file, import theirs. KSP's own files, checked for missing parts.", "sublabel"),
            },
        });
        View = new ScrollViewer
        {
            Content = new StackPanel { Margin = new Thickness(28, 20, 28, 28), Spacing = 16, Children = { header, importGroup, mineGroup, serverGroup } },
        };
    }

    public Control View { get; }

    private string? Ksp => _host.Settings.KspFolder ?? KspLauncher.FindKsp();

    public void Refresh()
    {
        var ksp = Ksp;
        _mine.Children.Clear();
        if (ksp == null || !KspLauncher.IsKspFolder(ksp))
            _mine.Children.Add(Ui.Row("KSP not found", "Set your Kerbal Space Program folder on the Join page (Play).", null));
        else
        {
            var saves = CraftShare.Saves(ksp);
            var selected = _save.SelectedItem as string;
            _save.ItemsSource = saves;
            _save.SelectedItem = saves.Contains(selected) ? selected : saves.FirstOrDefault();
            foreach (var craft in CraftShare.InKsp(ksp))
                _mine.Children.Add(CraftRow(craft, ksp));
            if (_mine.Children.Count == 0)
                _mine.Children.Add(Ui.Row("No crafts yet", "Crafts you save in the VAB or SPH show up here.", null));
        }

        _server.Children.Clear();
        _serverGroup.IsVisible = _host.Session != null;
        if (_host.Session is { } session)
        {
            foreach (var craft in CraftShare.InServer(session.ServerDirectory))
                _server.Children.Add(CraftRow(craft, null));
            if (_server.Children.Count == 0)
                _server.Children.Add(Ui.Row("The library is empty", "Players upload crafts from LMP's in-game craft library, or import one above.", null));
        }
    }

    private Border CraftRow(CraftInfo craft, string? ksp)
    {
        var export = Ui.Button("Export...", "text").WithId($"crafts/export/{craft.Source}/{craft.Name}");
        export.Click += async (_, _) => await ExportAsync(craft, ksp);
        return Ui.Row(craft.Name, $"{craft.Facility} · {craft.PartCount} parts · {craft.Source}", export).WithId($"crafts/row/{craft.Source}/{craft.Name}");
    }

    internal async Task ExportAsync(CraftInfo craft, string? ksp)
    {
        var target = await PickExportPath(_host, CraftShare.SafeName(craft.Name) + CraftShare.Extension);
        if (string.IsNullOrEmpty(target))
            return;
        try
        {
            CraftShare.Export(craft, target, ksp);
            _result.Text = $"Exported {craft.Name} to {Path.GetFileName(target)}. Send it to your friends.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _result.Text = "Couldn't export: " + e.Message;
        }
    }

    internal async Task ImportAsync()
    {
        var path = await PickImportPath(_host);
        if (string.IsNullOrEmpty(path))
            return;
        SharedCraft shared;
        try
        {
            shared = CraftShare.Open(path);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _result.Text = "Couldn't open it: " + e.Message;
            return;
        }

        // Part check against this KSP, the way KSP itself would complain ("missing parts") when loading it.
        var ksp = Ksp;
        var missing = "";
        if (ksp != null && Directory.Exists(Path.Combine(ksp, "GameData")))
        {
            _result.Text = "Checking parts against your GameData...";
            try
            {
                var scan = await Task.Run(() => GameDataScanner.ScanCached(Path.Combine(ksp, "GameData"), hashPlugins: false));
                var absent = CraftShare.MissingParts(shared.Craft, scan.Parts);
                missing = absent.Count == 0 ? "You have every part it uses." :
                    $"{absent.Count} of its parts aren't in your GameData (a mod you don't have?): {string.Join(", ", absent.Take(8))}{(absent.Count > 8 ? ", ..." : "")}.";
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                missing = "Couldn't check its parts: " + e.Message;
            }
            _result.Text = "";
        }

        var save = _save.SelectedItem as string;
        var choices = new List<string>();
        if (ksp != null && save != null)
            choices.Add($"Add to KSP save {save}");
        if (_host.Session != null)
            choices.Add("Add to the server's library");
        if (choices.Count == 0)
        {
            _result.Text = "Set your KSP folder (Join page) or open a server first, so the craft has somewhere to go.";
            return;
        }
        choices.Add("Cancel");
        var choice = await Dialogs.ChooseAsync(_host, $"Import {shared.Craft.Name}?",
            $"{shared.Craft.Facility} craft with {shared.Craft.PartCount} parts. {missing}".Trim(), choices.ToArray());
        if (choice < 0 || choice >= choices.Count - 1)
            return;

        try
        {
            var target = choices[choice].StartsWith("Add to KSP", StringComparison.Ordinal)
                ? CraftShare.ImportToKsp(shared, ksp!, save!)
                : await ImportToServerAsync(shared);
            if (target != null)
                _result.Text = $"Imported {shared.Craft.Name} to {target}. {missing}".Trim();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _result.Text = "Couldn't import: " + e.Message;
        }
        Refresh();
    }

    private async Task<string?> ImportToServerAsync(SharedCraft shared)
    {
        if (_host.Server?.IsRunning == true)
        {
            await Dialogs.InfoAsync(_host, "Stop the server first", "LMP reads its craft library while it runs; stop the server, import, then start it again.");
            return null;
        }
        return CraftShare.ImportToServer(shared, _host.Session!.ServerDirectory);
    }
}
