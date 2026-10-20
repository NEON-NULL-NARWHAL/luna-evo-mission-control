// LUNA EVO · LunaEvo.ConfigManager.Tests/SettingsPageTests.cs
// [Test] Accent colour, theme, behaviour settings and the about section.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using LunaEvo.ConfigManager.Core;
using LunaEvo.ConfigManager.Core.Files;
using LunaEvo.ConfigManager.Core.Validation;

namespace LunaEvo.ConfigManager.Tests;

/// <summary>The LUNA EVO settings page: accent colour, theme, behaviour and the about section.</summary>
public class SettingsPageTests
{
    [AvaloniaFact]
    public void Preset_recolours_the_app_and_is_remembered()
    {
        using var ui = new UiHarness();
        ui.OpenTab(MainWindow.SettingsKey);
        var before = PrimaryColor();

        ui.Click(ui.Find<Button>("settings/preset/Kerbin blue"));

        var after = PrimaryColor();
        Assert.NotEqual(before, after);
        Assert.True(after.B > after.R && after.B > after.G, $"Primary should be blue, was {after}");
        Assert.Equal("#2F7FD8", ui.Window.Settings.AccentColor);
        Assert.Equal("#2F7FD8", AppSettingsStore.Load(ui.SettingsPath).AccentColor);
        Assert.Equal("#2F7FD8", ui.Find<TextBox>("settings/accentHex").Text);
        Assert.Contains("selected", ui.Find<Button>("settings/preset/Kerbin blue").Classes);
    }

    [AvaloniaFact]
    public void Colour_wheel_and_hex_box_set_the_accent()
    {
        using var ui = new UiHarness();
        ui.OpenTab(MainWindow.SettingsKey);

        ui.Find<ColorView>("settings/accent").Color = Color.Parse("#C8452E");
        ui.Pump();
        Assert.Equal("#C8452E", ui.Window.Settings.AccentColor);
        Assert.True(PrimaryColor().R > PrimaryColor().G);

        var hex = ui.Find<TextBox>("settings/accentHex");
        hex.Text = "8E4FD1"; // no # needed
        hex.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        ui.Pump();
        Assert.Equal("#8E4FD1", ui.Window.Settings.AccentColor);

        hex.Text = "not a colour";
        hex.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        ui.Pump();
        Assert.Equal("#8E4FD1", ui.Window.Settings.AccentColor);
        Assert.Equal("#8E4FD1", hex.Text);

        ui.Click(ui.Find<Button>("settings/resetAppearance"));
        Assert.Equal(AppSettings.DefaultAccent, ui.Window.Settings.AccentColor);
    }

    [AvaloniaFact]
    public void Theme_can_switch_to_light_and_back()
    {
        using var ui = new UiHarness();
        ui.OpenTab(MainWindow.SettingsKey);

        ui.Find<ComboBox>("settings/theme").SelectedItem = "Light";
        ui.Pump();
        Assert.Equal(ThemeVariant.Light, Application.Current!.RequestedThemeVariant);
        Assert.Equal("Light", AppSettingsStore.Load(ui.SettingsPath).Theme);

        ui.Find<ComboBox>("settings/theme").SelectedItem = "Dark";
        ui.Pump();
        Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void About_shows_app_and_lmp_versions()
    {
        using var ui = new UiHarness();
        ui.OpenTab(MainWindow.SettingsKey);

        Assert.Contains($"Version {SettingsPage.AppVersion}", Texts(ui.Find<Border>("settings/appVersion")));
        Assert.Equal("0.3.0", SettingsPage.AppVersion);
        Assert.Contains(Texts(ui.Find<Border>("settings/lmpVersion")), t => t.StartsWith("Version 0.29.2"));
    }

    [AvaloniaFact]
    public void Backups_to_keep_is_applied_when_saving()
    {
        using var ui = new UiHarness();
        ui.OpenTab(MainWindow.SettingsKey);
        ui.Find<NumericUpDown>("settings/backups").Value = 2;
        ui.Pump();
        Assert.Equal(2, ui.Window.Settings.BackupsToKeep);

        ui.OpenTab(LmpRules.General);
        for (var i = 0; i < 4; i++)
        {
            ui.Find<TextBox>($"{LmpRules.General}/ServerName").Text = $"LUNA EVO {i}";
            ui.Pump();
            ui.Click(ui.Find<Button>("save"));
            Thread.Sleep(1100); // backup folders are named by the second
        }

        Assert.Equal(2, Directory.GetDirectories(Path.Combine(ui.ConfigDirectory, BackupService.BackupFolderName)).Length);
    }

    [AvaloniaFact]
    public void Settings_page_works_before_any_server_is_open()
    {
        Dialogs.Responder = (_, _, _) => 0;
        var settings = Path.Combine(Path.GetTempPath(), "LunaEvoTests", Guid.NewGuid().ToString("N"), "settings.json");
        var window = new MainWindow(new StartupOptions(null, null, settings));
        window.Show();
        try
        {
            window.Navigate(MainWindow.SettingsKey);
            window.UpdateLayout();
            var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
            Assert.Contains("LUNA EVO Settings", texts);
            Assert.Contains("No server open", texts);
        }
        finally
        {
            Dialogs.Responder = null;
            window.Close();
        }
    }

    private static Color PrimaryColor()
    {
        var app = Application.Current!;
        app.Resources.ThemeDictionaries.TryGetValue(ThemeVariant.Dark, out var dark);
        return ((ISolidColorBrush)((IResourceDictionary)dark!)["Primary"]!).Color;
    }

    private static List<string> Texts(Control control) =>
        control.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
}
