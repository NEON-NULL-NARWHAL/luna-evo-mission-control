// LUNA EVO · LunaEvo.ConfigManager.Tests/BehaviourTests.cs
// [Test] Rules and safety features through the UI: errors block saving, greying out, list editor, backups, close prompt.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using LunaEvo.ConfigManager.Core.Files;
using LunaEvo.ConfigManager.Core.Validation;

namespace LunaEvo.ConfigManager.Tests;

/// <summary>The rules and safety features, checked through the UI.</summary>
public class BehaviourTests
{
    private const string General = LmpRules.General;

    [AvaloniaFact]
    public void Invalid_value_shows_error_and_badge_and_blocks_saving()
    {
        using var ui = new UiHarness();
        var path = Path.Combine(ui.ConfigDirectory, General);
        var before = File.ReadAllBytes(path);

        ui.OpenTab(General);
        ui.Find<TextBox>($"{General}/ServerName").Text = new string('x', 31);
        ui.Pump();

        Assert.Contains(Messages(ui), m => m.Contains("Too long: 31 characters, max 30"));
        Assert.Contains("1", NavBadges(ui, General));
        Assert.Contains("error", ui.Find<TextBlock>("status/issues").Text);

        ui.Click(ui.Find<Button>("save"));
        Assert.Equal(["Can't save yet"], ui.DialogsShown);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.True(ui.Session.IsDirty);
    }

    [AvaloniaFact]
    public void Heartbeat_must_stay_below_timeout()
    {
        using var ui = new UiHarness();
        ui.OpenTab(LmpRules.Connection);
        ui.Find<NumericUpDown>($"{LmpRules.Connection}/HearbeatMsInterval").Value = 30000;
        ui.Pump();

        Assert.Contains(Messages(ui), m => m.Contains("Must be lower than ConnectionMsTimeout"));
    }

    [AvaloniaFact]
    public void Rainbow_effect_greys_out_colours()
    {
        using var ui = new UiHarness();
        ui.OpenTab(LmpRules.DedicatedServer);
        var rainbow = ui.Find<ToggleSwitch>($"{LmpRules.DedicatedServer}/UseRainbowEffect");
        var red = ui.Find<NumericUpDown>($"{LmpRules.DedicatedServer}/Red");

        rainbow.IsChecked = true;
        ui.Pump();
        Assert.False(red.IsEnabled);

        rainbow.IsChecked = false;
        ui.Pump();
        Assert.True(red.IsEnabled);
    }

    [AvaloniaFact]
    public void Game_mode_on_general_tab_controls_career_settings_on_gameplay_tab()
    {
        using var ui = new UiHarness();
        ui.OpenTab(General);
        var mode = ui.Find<ComboBox>($"{General}/GameMode");

        mode.SelectedItem = "Science";
        ui.Pump();
        ui.OpenTab(LmpRules.Gameplay);
        Assert.False(ui.Find<NumericUpDown>($"{LmpRules.Gameplay}/StartingFunds").IsEnabled);
        Assert.True(ui.Find<NumericUpDown>($"{LmpRules.Gameplay}/StartingScience").IsEnabled);

        ui.OpenTab(General);
        mode.SelectedItem = "Career";
        ui.Pump();
        ui.OpenTab(LmpRules.Gameplay);
        Assert.True(ui.Find<NumericUpDown>($"{LmpRules.Gameplay}/StartingFunds").IsEnabled);
    }

    [AvaloniaFact]
    public void Gameplay_change_off_preset_shows_custom_difficulty_banner()
    {
        using var ui = new UiHarness();
        ui.OpenTab(LmpRules.Gameplay);
        var banner = ui.Find<Border>($"{LmpRules.Gameplay}/banner");
        var revert = ui.Find<ToggleSwitch>($"{LmpRules.Gameplay}/CanRevert");
        var wasOffPreset = banner.IsVisible;

        revert.IsChecked = !revert.IsChecked;
        ui.Pump();
        Assert.True(banner.IsVisible || wasOffPreset);

        ui.Click(ui.Find<Button>($"{LmpRules.Gameplay}/CanRevert/reset"));
        Assert.Equal(wasOffPreset, banner.IsVisible);
    }

    [AvaloniaFact]
    public void Mod_control_list_editor_searches_adds_removes_dedupes_and_resets()
    {
        const string file = "LMPModControl.xml";
        const string id = file + "/AllowedParts";
        using var ui = new UiHarness();
        ui.OpenTab(file);
        var items = ui.Find<ListBox>(id + "/items");
        var all = ui.Session.GetList(file, "AllowedParts");

        // Search
        ui.Find<TextBox>(id + "/search").Text = "delta.small";
        ui.Pump();
        Assert.Equal(all.Count(p => p.Contains("delta.small", StringComparison.OrdinalIgnoreCase)), Count(items));
        ui.Find<TextBox>(id + "/search").Text = "";
        ui.Pump();
        Assert.Equal(all.Count, Count(items));

        // Add
        ui.Find<TextBox>(id + "/new").Text = "lunaEvo.testPart";
        ui.Click(ui.Find<Button>(id + "/add"));
        Assert.Equal("lunaEvo.testPart", ui.Session.GetList(file, "AllowedParts")[^1]);

        // Remove duplicates
        Assert.True(ui.Find<Button>(id + "/dedupe").IsEnabled);
        ui.Click(ui.Find<Button>(id + "/dedupe"));
        var deduped = ui.Session.GetList(file, "AllowedParts");
        Assert.Equal(deduped.Distinct().Count(), deduped.Count);
        Assert.False(ui.Find<Button>(id + "/dedupe").IsEnabled);

        // Remove selected
        items.SelectedItems!.Add(items.ItemsSource!.Cast<object>().Last());
        ui.Pump();
        ui.Click(ui.Find<Button>(id + "/remove"));
        Assert.DoesNotContain("lunaEvo.testPart", ui.Session.GetList(file, "AllowedParts"));

        // Save, then check the file on disk
        ui.Click(ui.Find<Button>("save"));
        var onDisk = ConfigFile.Load(Path.Combine(ui.ConfigDirectory, file)).GetList("AllowedParts")!;
        Assert.Equal(ui.Session.GetList(file, "AllowedParts"), onDisk);

        // Reset to LMP's defaults
        ui.Click(ui.Find<Button>(id + "/reset"));
        Assert.Equal(ui.Session.DefaultFor(file, "AllowedParts").Split('\n'), ui.Session.GetList(file, "AllowedParts"));
    }

    [AvaloniaFact]
    public void Save_backs_up_first_and_clears_unsaved_markers()
    {
        using var ui = new UiHarness();
        ui.OpenTab(General);
        var original = File.ReadAllBytes(Path.Combine(ui.ConfigDirectory, General));

        ui.Find<TextBox>($"{General}/ServerName").Text = "LUNA EVO";
        ui.Pump();
        Assert.True(DirtyDot(ui, General).IsVisible);
        Assert.True(ui.Find<Button>("save").IsEnabled);

        ui.Click(ui.Find<Button>("save"));

        var backup = Assert.Single(Directory.GetDirectories(Path.Combine(ui.ConfigDirectory, BackupService.BackupFolderName)));
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(backup, General)));
        Assert.Contains("<ServerName>LUNA EVO</ServerName>", File.ReadAllText(Path.Combine(ui.ConfigDirectory, General)));
        Assert.False(DirtyDot(ui, General).IsVisible);
        Assert.False(ui.Find<Button>("save").IsEnabled);
        Assert.StartsWith("Saved", ui.Find<TextBlock>("status/save").Text);
        Assert.Equal(["Review changes"], ui.DialogsShown);
    }

    [AvaloniaFact]
    public void Closing_with_unsaved_changes_asks_first()
    {
        using var ui = new UiHarness();
        ui.OpenTab(General);
        ui.Find<TextBox>($"{General}/ServerName").Text = "LUNA EVO";
        ui.Pump();

        ui.ConfirmDialogs = false;
        ui.Window.Close();
        ui.Pump();
        Assert.Equal(["Unsaved changes"], ui.DialogsShown);
        Assert.True(ui.Window.IsVisible);

        ui.ConfirmDialogs = true;
        ui.Window.Close();
        ui.Pump();
        Assert.False(ui.Window.IsVisible);
    }

    [AvaloniaFact]
    public void Missing_server_dll_shows_welcome_page_with_error()
    {
        Dialogs.Responder = (_, _, _) => 0;
        var settings = Path.Combine(Path.GetTempPath(), "LunaEvoTests", Guid.NewGuid().ToString("N"), "settings.json");
        var window = new MainWindow(new StartupOptions(Path.GetTempPath(), null, settings));
        window.Show();
        try
        {
            var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
            Assert.Contains("Welcome to LUNA EVO", texts);
            Assert.Contains(texts, t => t.Contains("No Server.dll found"));
            Assert.Null(window.Session);
        }
        finally
        {
            Dialogs.Responder = null;
            window.Close();
        }
    }

    private static int Count(ListBox list) => list.ItemsSource!.Cast<object>().Count();

    private static IEnumerable<string> Messages(UiHarness ui) =>
        ui.Window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? "");

    private static IEnumerable<string> NavBadges(UiHarness ui, string fileName) =>
        ui.Find<Button>("nav/" + fileName).GetVisualDescendants().OfType<Border>()
            .Where(b => b.Classes.Contains("badge") && b.IsVisible)
            .Select(b => (b.Child as TextBlock)?.Text ?? "");

    private static Avalonia.Controls.Shapes.Ellipse DirtyDot(UiHarness ui, string fileName) =>
        ui.Find<Button>("nav/" + fileName).GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>()
            .Single(e => e.Classes.Contains("dirtyDot"));
}
