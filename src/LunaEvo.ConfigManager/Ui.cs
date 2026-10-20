// LUNA EVO · LunaEvo.ConfigManager/Ui.cs
// [UI] Little helpers for building styled controls in code (text, buttons, groups, rows, icons).
// Used by: All UI files
// Guide:   Meat and Potatoes/04-App-UI.md

using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace LunaEvo.ConfigManager;

/// <summary>Small helpers for building the NOMM-style controls in code. Styling lives in App.axaml.</summary>
internal static partial class Ui
{
    /// <summary>"DedicatedServer" → "Dedicated Server", "LMPModControl" → "LMP Mod Control".</summary>
    public static string Humanize(string name) => WordBoundary().Replace(name, " ");

    /// <summary>A TextBlock with style classes from App.axaml, e.g. "label", "sublabel", "headline".</summary>
    public static TextBlock Text(string text, params string[] classes)
    {
        var block = new TextBlock { Text = text };
        block.Classes.AddRange(classes);
        return block;
    }

    /// <summary>Sets the AutomationId, which the UI tests (and screen readers) use to find the control.</summary>
    public static T WithId<T>(this T control, string automationId) where T : Control
    {
        AutomationProperties.SetAutomationId(control, automationId);
        return control;
    }

    /// <summary>A button with a style class ("fab", "text", "chip"...) and an optional icon resource key.</summary>
    public static Button Button(string text, string cssClass, string? icon = null)
    {
        var button = new Button { Content = IconAndText(text, icon) };
        button.Classes.Add(cssClass);
        return button;
    }

    /// <summary>Button content: an optional icon followed by text.</summary>
    public static Control IconAndText(string text, string? icon)
    {
        if (icon == null)
            return new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(Icon(icon));
        panel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }

    /// <summary>An icon from a StreamGeometry in App.axaml (e.g. "IconSave"), bound so it follows theme changes.</summary>
    public static PathIcon Icon(string resourceKey)
    {
        var icon = new PathIcon();
        icon[!PathIcon.DataProperty] = icon.GetResourceObservable(resourceKey).ToBinding();
        return icon;
    }

    /// <summary>A NOMM settings group: a small primary-coloured title over a rounded panel.</summary>
    public static (Control View, StackPanel Body, TextBlock Title) Group(string title)
    {
        var heading = Text(title, "groupTitle");
        var body = new StackPanel { Spacing = 2 };
        var panel = new Border { Child = body };
        panel.Classes.Add("group");
        var view = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 0, 20), Children = { heading, panel } };
        return (view, body, heading);
    }

    /// <summary>A settings row in the same layout as the generated ones: label and description left, control right.</summary>
    public static Border Row(string label, string? description, Control? editor)
    {
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { Text(label, "label") } };
        if (!string.IsNullOrWhiteSpace(description))
            text.Children.Add(Text(description, "sublabel"));

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(text);
        if (editor != null)
        {
            editor.VerticalAlignment = VerticalAlignment.Center;
            editor.Margin = new Thickness(16, 0, 0, 0);
            Grid.SetColumn(editor, 1);
            grid.Children.Add(editor);
        }

        var row = new Border { Child = grid };
        row.Classes.Add("row");
        return row;
    }

    /// <summary>Opens a folder in Explorer or a link in the browser.</summary>
    public static void Open(string target)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Nothing registered to open it; not worth a dialog.
        }
    }

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex WordBoundary();
}
