// LUNA EVO · LunaEvo.MissionControl/Dialogs.cs
// [UI] Small message box (Avalonia has none), with a hook so tests can answer dialogs.
// Used by: MainWindow, ServerPage
// Guide:   Meat and Potatoes/04-App-UI.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace LunaEvo.MissionControl;

/// <summary>Avalonia has no built-in message box, so this is a minimal one in the app's style.</summary>
internal static class Dialogs
{
    /// <summary>Tests set this to answer dialogs instead of showing them: (title, message, buttons) → chosen index.</summary>
    internal static Func<string, string, IReadOnlyList<string>, int>? Responder { get; set; }

    /// <summary>Tests set this to answer text prompts: (title, suggested text) → typed text, or null for Cancel.</summary>
    internal static Func<string, string, string?>? PromptResponder { get; set; }

    /// <summary>Asks for a line of text. Returns null if canceled.</summary>
    public static async Task<string?> PromptAsync(Window owner, string title, string message, string initial = "", string confirmText = "OK")
    {
        if (PromptResponder != null)
            return PromptResponder(title, initial);

        var dialog = new Window
        {
            Title = title,
            Icon = MainWindow.AppIcon,
            SizeToContent = SizeToContent.WidthAndHeight,
            MaxWidth = 600,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var box = new TextBox { Text = initial, Width = 420 };
        var confirm = Ui.Button(confirmText, "fab");
        confirm.IsDefault = true;
        confirm.Padding = new Thickness(18, 10);
        confirm.Click += (_, _) => dialog.Close(box.Text ?? "");
        var cancel = Ui.Button("Cancel", "text");
        cancel.IsCancel = true;
        cancel.Click += (_, _) => dialog.Close(null);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 14,
            Children =
            {
                Ui.Text(title, "headline"),
                Ui.Text(message, "label"),
                box,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { confirm, cancel } },
            },
        };
        dialog.Opened += (_, _) => box.Focus();
        return await dialog.ShowDialog<string?>(owner);
    }

    /// <summary>Two-button question. True if the user picked <paramref name="confirmText"/>.</summary>
    public static async Task<bool> ConfirmAsync(Window owner, string title, string message, string confirmText) =>
        await ChooseAsync(owner, title, message, confirmText, "Cancel") == 0;

    /// <summary>A message with a single OK button.</summary>
    public static Task InfoAsync(Window owner, string title, string message) =>
        ChooseAsync(owner, title, message, "OK");

    /// <summary>Shows the buttons in order (the first is the main action, the last cancels). Returns the index chosen, or -1 if closed.</summary>
    public static async Task<int> ChooseAsync(Window owner, string title, string message, params string[] buttons)
    {
        if (Responder != null)
            return Responder(title, message, buttons);

        var dialog = new Window
        {
            Title = title,
            Icon = MainWindow.AppIcon,
            SizeToContent = SizeToContent.WidthAndHeight,
            MaxWidth = 600,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        // More than three answers (e.g. CKAN's "which provider?" options): one per line, so long names aren't cut off.
        var many = buttons.Length > 3;
        var row = new StackPanel
        {
            Orientation = many ? Orientation.Vertical : Orientation.Horizontal, Spacing = 8,
            HorizontalAlignment = many ? HorizontalAlignment.Stretch : HorizontalAlignment.Right,
        };
        for (var i = 0; i < buttons.Length; i++)
        {
            var index = i;
            var button = Ui.Button(buttons[i], i == 0 ? "fab" : "text");
            if (i == 0)
            {
                button.IsDefault = true;
                button.Padding = new Thickness(18, 10);
            }
            if (i == buttons.Length - 1 && buttons.Length > 1)
                button.IsCancel = true;
            button.Click += (_, _) => dialog.Close(index);
            row.Children.Add(button);
        }

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 18,
            Children = { Ui.Text(title, "headline"), Ui.Text(message, "label"), row },
        };

        return await dialog.ShowDialog<int?>(owner) ?? -1;
    }
}
