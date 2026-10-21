// LUNA EVO · LunaEvo.MissionControl.Tests/AllTabsTests.cs
// [Test] Clicks through every tab and drives every control, then saves and checks the files.
// Used by: dotnet test
// Guide:   Meat and Potatoes/05-Tests.md

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using LunaEvo.MissionControl.Core;
using LunaEvo.MissionControl.Core.Schema;
using LunaEvo.MissionControl.Core.Tests;
using LunaEvo.MissionControl.Core.Validation;

namespace LunaEvo.MissionControl.Tests;

/// <summary>Walks every tab and every control, the way a user would.</summary>
public class AllTabsTests
{
    public static TheoryData<string> AllFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(TestPaths.ServerConfigs, "*.xml").Order())
            data.Add(Path.GetFileName(file));
        return data;
    }

    [AvaloniaTheory]
    [MemberData(nameof(AllFiles))]
    public void Tab_shows_the_right_editor_and_value_for_every_setting(string fileName)
    {
        using var ui = new UiHarness();
        ui.OpenTab(fileName);

        foreach (var setting in ui.Session.Schema.Find(fileName)!.Settings)
        {
            var id = $"{fileName}/{setting.Name}";
            var value = ui.Session.GetValue(fileName, setting.Name) ?? "";
            switch (setting.Kind)
            {
                case SettingKind.Bool:
                    Assert.Equal(value == "true", ui.Find<ToggleSwitch>(id).IsChecked);
                    break;
                case SettingKind.Integer:
                case SettingKind.Decimal:
                    Assert.Equal(decimal.Parse(value, CultureInfo.InvariantCulture), ui.Find<NumericUpDown>(id).Value);
                    break;
                case SettingKind.Enum:
                    Assert.Equal(value, ui.Find<ComboBox>(id).SelectedItem);
                    break;
                case SettingKind.Text:
                case SettingKind.MultilineText:
                    Assert.Equal(value, (ui.Find<TextBox>(id).Text ?? "").Replace("\r\n", "\n"));
                    break;
                case SettingKind.StringList:
                    var items = ui.Find<ListBox>(id + "/items").ItemsSource!.Cast<object>().Count();
                    Assert.Equal(ui.Session.GetList(fileName, setting.Name).Count, items);
                    break;
                case SettingKind.ObjectList:
                    Assert.NotNull(ui.Find<Control>(id));
                    Assert.NotNull(ui.Find<Button>(id + "/add"));
                    break;
                default:
                    Assert.NotNull(ui.Find<TextBlock>(id));
                    break;
            }
        }
    }

    [AvaloniaFact]
    public void Every_setting_on_every_tab_can_be_edited_and_saved()
    {
        using var ui = new UiHarness();
        var expected = new Dictionary<(string File, string Setting), string>();
        var greyedOut = 0;

        foreach (var file in ui.Session.Schema.Files)
        {
            ui.OpenTab(file.FileName);
            foreach (var setting in file.Settings)
            {
                var id = $"{file.FileName}/{setting.Name}";
                var editor = ui.TryFind<Control>(id);
                var next = NextValue(setting, ui.Session.GetValue(file.FileName, setting.Name) ?? "");
                if (editor == null || next == null)
                    continue;

                if (!editor.IsEffectivelyEnabled)
                {
                    // Grayed out on purpose; the reason must be shown.
                    Assert.NotNull(LmpRules.InactiveReason(ui.Session, file.FileName, setting.Name));
                    greyedOut++;
                    continue;
                }

                Type(editor, next);
                ui.Pump();

                AssertSameValue(next, ui.Session.GetValue(file.FileName, setting.Name), id);
                Assert.True(ui.Session.IsModified(file.FileName, setting.Name), $"{id} should be marked modified");
                expected[(file.FileName, setting.Name)] = next;
            }
        }

        // Everything except grayed-out, list and read-only settings.
        var editable = ui.Session.Schema.Files.Sum(f => f.Settings.Count(s => s.Kind is not (SettingKind.StringList or SettingKind.ObjectList or SettingKind.Unsupported)));
        Assert.Equal(editable - greyedOut, expected.Count);
        Assert.DoesNotContain(ui.Session.Validate(), i => i.Severity == Severity.Error);
        Assert.Equal(ui.Session.Schema.Files.Count, ui.Session.DirtyFiles.Count);

        ui.Click(ui.Find<Button>("save"));

        Assert.False(ui.Session.IsDirty);
        Assert.False(ui.Find<Button>("save").IsEnabled);
        var reopened = ConfigSession.Open(TestPaths.LmpServer, ui.ConfigDirectory);
        foreach (var ((file, setting), value) in expected)
            AssertSameValue(value, reopened.GetValue(file, setting), $"{file}/{setting} on disk");
    }

    [AvaloniaFact]
    public void Reset_restores_the_default_on_every_tab()
    {
        using var ui = new UiHarness();
        var resets = 0;

        foreach (var file in ui.Session.Schema.Files)
        {
            ui.OpenTab(file.FileName);
            foreach (var setting in file.Settings.Where(s => s.Kind != SettingKind.StringList))
            {
                var reset = ui.TryFind<Button>($"{file.FileName}/{setting.Name}/reset");
                if (reset == null)
                    continue;

                var value = ui.Session.GetValue(file.FileName, setting.Name);
                var defaultValue = ui.Session.DefaultFor(file.FileName, setting.Name);
                Assert.Equal(value != defaultValue && setting.Kind != SettingKind.Unsupported, reset.IsEnabled);
                if (!reset.IsEnabled)
                    continue;

                ui.Click(reset);
                Assert.Equal(defaultValue, ui.Session.GetValue(file.FileName, setting.Name));
                Assert.False(reset.IsEnabled);
                resets++;
            }
        }

        Assert.True(resets > 0);
    }

    /// <summary>A different valid value for the setting.</summary>
    private static string? NextValue(SettingDefinition setting, string current)
    {
        switch (setting.Kind)
        {
            case SettingKind.Bool:
                return current == "true" ? "false" : "true";
            case SettingKind.Integer:
            {
                var value = long.Parse(current, CultureInfo.InvariantCulture);
                return (value + 1 <= (setting.Max ?? long.MaxValue) ? value + 1 : value - 1).ToString(CultureInfo.InvariantCulture);
            }
            case SettingKind.Decimal:
            {
                var value = decimal.Parse(current, NumberStyles.Float, CultureInfo.InvariantCulture);
                return (value + 0.5m <= (setting.Max ?? decimal.MaxValue) ? value + 0.5m : value - 0.5m).ToString(CultureInfo.InvariantCulture);
            }
            case SettingKind.Enum:
                return setting.EnumValues[(setting.EnumValues.ToList().IndexOf(current) + 1) % setting.EnumValues.Count];
            case SettingKind.Text:
            {
                var text = current == "LUNA EVO" ? "LUNA EVOLVED" : "LUNA EVO";
                return setting.MaxLength is { } max && text.Length > max ? text[..max] : text;
            }
            case SettingKind.MultilineText:
                return current + "\nFly safe, %Name%!";
            default:
                return null;
        }
    }

    private static void Type(Control editor, string value)
    {
        switch (editor)
        {
            case ToggleSwitch toggle: toggle.IsChecked = value == "true"; break;
            case NumericUpDown number: number.Value = decimal.Parse(value, CultureInfo.InvariantCulture); break;
            case ComboBox combo: combo.SelectedItem = value; break;
            case TextBox text: text.Text = value.Replace("\n", "\r\n"); break; // Windows TextBoxes produce \r\n
            default: throw new InvalidOperationException($"Don't know how to type into {editor.GetType().Name}");
        }
    }

    private static void AssertSameValue(string expected, string? actual, string what)
    {
        if (decimal.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var a) &&
            decimal.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
            Assert.True(a == b, $"{what}: expected {expected}, got {actual}");
        else
            Assert.True(expected == actual, $"{what}: expected '{expected}', got '{actual}'");
    }
}
