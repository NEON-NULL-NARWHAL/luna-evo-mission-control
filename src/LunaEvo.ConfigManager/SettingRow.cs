// LUNA EVO · LunaEvo.ConfigManager/SettingRow.cs
// [UI] One generated settings row: picks the right editor for the setting's type, shows errors and reset.
// Used by: MainWindow (one per setting)
// Guide:   Meat and Potatoes/04-App-UI.md

using System.Globalization;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LunaEvo.ConfigManager.Core;
using LunaEvo.ConfigManager.Core.Schema;
using LunaEvo.ConfigManager.Core.Validation;

namespace LunaEvo.ConfigManager;

/// <summary>
/// One generated row, NOMM style: name and description on the left, an editor picked from the setting's type
/// on the right, then a reset button. Problems show as a coloured line under the description.
/// </summary>
internal sealed class SettingRow
{
    private readonly ConfigSession _session;
    private readonly string _fileName;
    private readonly SettingDefinition _setting;

    internal string FileName => _fileName;
    internal SettingDefinition Setting => _setting;
    private readonly Action _changed;
    private readonly Border _row = new();
    private readonly TextBlock _label;
    private readonly TextBlock _message = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly Button _reset = new() { Content = Ui.Icon("IconRestart") };
    private readonly Control _editor;
    private bool _updating;

    /// <summary>Builds the row and its editor. <paramref name="changed"/> is called after every edit so the window can re-validate.</summary>
    public SettingRow(ConfigSession session, string fileName, SettingDefinition setting, Action changed)
    {
        _session = session;
        _fileName = fileName;
        _setting = setting;
        _changed = changed;

        _label = Ui.Text(Ui.Humanize(setting.Name), "label");
        _editor = CreateEditor().WithId($"{fileName}/{setting.Name}");
        _editor.VerticalAlignment = VerticalAlignment.Center;
        _editor.Margin = new Thickness(16, 0, 4, 0);

        ToolTip.SetTip(_label, $"<{setting.Name}>  ({setting.ClrTypeName})");
        _reset.Classes.Add("icon");
        _reset.WithId($"{fileName}/{setting.Name}/reset");
        _reset.Click += (_, _) => Commit(_session.DefaultFor(_fileName, _setting.Name));

        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { _label } };
        // Many LMP comments just repeat the name ("Missing Crews Respawn"); only show ones that add something.
        if (!string.IsNullOrWhiteSpace(setting.Description) && Squash(setting.Description) != Squash(setting.Name))
            text.Children.Add(Ui.Text(setting.Description, "sublabel"));
        text.Children.Add(_message);
        if (_editor is TextBox passwordBox && passwordBox.PasswordChar != default)
        {
            // LMP doesn't limit password guesses, so make a long random one easy.
            var generate = Ui.Button("Generate a strong password", "text").WithId($"{fileName}/{setting.Name}/generate");
            generate.HorizontalAlignment = HorizontalAlignment.Left;
            generate.Padding = new Thickness(0, 4);
            generate.Click += (_, _) =>
            {
                passwordBox.Text = LmpRules.GeneratePassword();
                passwordBox.RevealPassword = true; // so it can be read out to friends
            };
            text.Children.Add(generate);
        }

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,32") };
        grid.Children.Add(text);
        Grid.SetColumn(_editor, 1);
        grid.Children.Add(_editor);
        Grid.SetColumn(_reset, 2);
        grid.Children.Add(_reset);

        _row.Classes.Add("row");
        _row.Child = grid;
        View = _row;
    }

    public Control View { get; }

    /// <summary>Shows the current value, greys out when inactive, marks modified, and shows the most severe problem.</summary>
    public void Refresh(IReadOnlyList<Issue> issues)
    {
        var value = _session.GetValue(_fileName, _setting.Name) ?? "";
        _updating = true;
        try
        {
            ShowValue(value);
        }
        finally
        {
            _updating = false;
        }

        var inactive = LmpRules.InactiveReason(_session, _fileName, _setting.Name);
        _editor.IsEnabled = inactive == null;
        _row.Classes.Set("inactive", inactive != null);
        _label.Classes.Set("modified", _session.IsModified(_fileName, _setting.Name));

        var defaultValue = _session.DefaultFor(_fileName, _setting.Name);
        _reset.IsEnabled = _setting.Kind != SettingKind.Unsupported && value != defaultValue;
        ToolTip.SetTip(_reset, $"Reset to default: {(defaultValue.Length == 0 ? "(empty)" : defaultValue)}");

        var issue = issues.Where(i => i.FileName == _fileName && i.Setting == _setting.Name)
            .OrderByDescending(i => i.Severity).FirstOrDefault();
        if (issue != null)
            SetMessage(issue.Message, issue.Severity == Severity.Error ? "error" : "warning");
        else if (inactive != null)
            SetMessage(inactive, "sublabel");
        else
            _message.IsVisible = false;
    }

    private Control CreateEditor()
    {
        switch (_setting.Kind)
        {
            case SettingKind.Bool:
            {
                var toggle = new ToggleSwitch { OnContent = "", OffContent = "", MinWidth = 0 };
                toggle.IsCheckedChanged += (_, _) => Commit(toggle.IsChecked == true ? "true" : "false");
                return toggle;
            }
            case SettingKind.Integer:
            case SettingKind.Decimal:
            {
                // No Minimum/Maximum on the control: it would silently clamp bad values from the file instead of flagging them.
                var isInteger = _setting.Kind == SettingKind.Integer;
                var box = new NumericUpDown
                {
                    Width = 200,
                    Increment = isInteger ? 1 : 0.1m,
                    FormatString = isInteger ? "0" : "",
                    NumberFormat = CultureInfo.InvariantCulture.NumberFormat,
                };
                box.ValueChanged += (_, e) => Commit(e.NewValue is { } v ? FormatNumber(v, isInteger) : "");
                return box;
            }
            case SettingKind.Enum:
            {
                var combo = new ComboBox { ItemsSource = _setting.EnumValues, Width = 200 };
                combo.SelectionChanged += (_, _) =>
                {
                    if (combo.SelectedItem is string selected)
                        Commit(selected);
                };
                return combo;
            }
            case SettingKind.Text:
            case SettingKind.MultilineText:
            {
                var multiline = _setting.Kind == SettingKind.MultilineText;
                var box = new TextBox
                {
                    Width = 340,
                    AcceptsReturn = multiline,
                    TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                    Height = multiline ? 100 : double.NaN,
                };
                if (_setting.Name.Contains("Password", StringComparison.Ordinal))
                {
                    box.PasswordChar = '•';
                    box.Classes.Add("revealPasswordButton");
                }
                box.TextChanged += (_, _) => Commit(box.Text ?? "");
                return box;
            }
            default:
                return Ui.Text("Edit in the XML file for now", "sublabel");
        }
    }

    private void ShowValue(string value)
    {
        switch (_editor)
        {
            case ToggleSwitch toggle:
                toggle.IsChecked = bool.TryParse(value, out var b) && b;
                break;
            case NumericUpDown box:
                var parsed = decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : (decimal?)null;
                if (box.Value != parsed)
                    box.Value = parsed;
                break;
            case ComboBox combo:
                combo.SelectedItem = _setting.EnumValues.Contains(value) ? value : null;
                break;
            case TextBox box:
                // Don't reset the text (and the caret) while the user is typing the same value.
                if ((box.Text ?? "").Replace("\r\n", "\n") != value)
                    box.Text = value;
                break;
        }
    }

    private void Commit(string value)
    {
        if (_updating)
            return;
        _session.SetValue(_fileName, _setting.Name, value);
        _changed();
    }

    private void SetMessage(string text, string cssClass)
    {
        _message.Text = text;
        _message.Classes.Clear();
        _message.Classes.Add(cssClass);
        _message.IsVisible = true;
    }

    private static string Squash(string text) =>
        new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string FormatNumber(decimal value, bool isInteger) =>
        isInteger
            ? decimal.Round(value).ToString(CultureInfo.InvariantCulture)
            : XmlConvert.ToString((double)value);
}
