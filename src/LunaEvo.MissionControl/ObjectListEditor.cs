// LUNA EVO · LunaEvo.MissionControl/ObjectListEditor.cs
// [UI] Editor for LMPModControl.xml's record lists (Mandatory/Optional/Forbidden plugins, Mandatory parts).
// Used by: MainWindow (one per object list setting)
// Guide:   Meat and Potatoes/12-Mod-Control-From-GameData.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using LunaEvo.MissionControl.Core;
using LunaEvo.MissionControl.Core.Schema;

namespace LunaEvo.MissionControl;

/// <summary>
/// One row per item with a text box per field (e.g. Text, Link, FilePath, Sha). Edits are committed when a box
/// loses focus or Enter is pressed, so typing doesn't rebuild the list under the cursor.
/// </summary>
internal sealed class ObjectListEditor
{
    private readonly ConfigSession _session;
    private readonly string _fileName;
    private readonly SettingDefinition _setting;
    private readonly Action _changed;
    private readonly StackPanel _rows = new() { Spacing = 4, Margin = new Thickness(8, 4) };
    private readonly TextBlock _title;
    private readonly TextBlock _empty;
    private List<Dictionary<string, string>> _items = [];
    private string _shown = "";

    public ObjectListEditor(ConfigSession session, string fileName, SettingDefinition setting, Action changed)
    {
        _session = session;
        _fileName = fileName;
        _setting = setting;
        _changed = changed;

        var id = $"{fileName}/{setting.Name}";
        var add = Ui.Button("Add entry", "text").WithId(id + "/add");
        add.Click += (_, _) =>
        {
            _items.Add(setting.ItemFields.ToDictionary(f => f, _ => ""));
            Commit();
        };
        var clear = Ui.Button("Remove all", "text").WithId(id + "/clear");
        clear.Click += (_, _) =>
        {
            _items.Clear();
            Commit();
        };

        _empty = Ui.Text("No entries.", "sublabel");
        _empty.Margin = new Thickness(12, 4);

        var (view, body, title) = Ui.Group(Ui.Humanize(setting.Name));
        _title = title;
        body.Children.Add(new Border { Padding = new Thickness(12, 8, 12, 0), Child = Ui.Text(Describe(setting.Name), "sublabel") });
        body.Children.Add(_empty);
        body.Children.Add(_rows);
        body.Children.Add(new WrapPanel { Margin = new Thickness(4), Children = { add, clear } });
        View = view.WithId(id);
    }

    public Control View { get; }

    /// <summary>Rebuilds the rows if the list changed in the session (e.g. after an import).</summary>
    public void Refresh()
    {
        var current = _session.GetObjectList(_fileName, _setting.Name);
        var text = Text(current);
        var modified = _session.IsModified(_fileName, _setting.Name) ? " · modified" : "";
        _title.Text = $"{Ui.Humanize(_setting.Name)} · {current.Count} entries{modified}";
        if (text == _shown)
            return;
        _shown = text;
        _items = current.Select(row => _setting.ItemFields.ToDictionary(f => f, f => row.TryGetValue(f, out var v) ? v : "")).ToList();
        Rebuild();
    }

    private void Rebuild()
    {
        _rows.Children.Clear();
        _empty.IsVisible = _items.Count == 0;
        for (var index = 0; index < _items.Count; index++)
        {
            var item = _items[index];
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", _setting.ItemFields.Select(Width)) + ",Auto") };
            for (var f = 0; f < _setting.ItemFields.Count; f++)
            {
                var field = _setting.ItemFields[f];
                var box = new TextBox { Text = item[field], PlaceholderText = field, Margin = new Thickness(0, 0, 6, 0) }
                    .WithId($"{_fileName}/{_setting.Name}/{index}/{field}");
                ToolTip.SetTip(box, field);
                box.LostFocus += (_, _) => Update(item, field, box.Text);
                box.KeyDown += (_, e) =>
                {
                    if (e.Key == Avalonia.Input.Key.Enter) Update(item, field, box.Text);
                };
                Grid.SetColumn(box, f);
                grid.Children.Add(box);
            }
            var remove = new Button { Content = Ui.Icon("IconStop") }.WithId($"{_fileName}/{_setting.Name}/{index}/remove");
            remove.Classes.Add("icon");
            ToolTip.SetTip(remove, "Remove this entry");
            remove.Click += (_, _) =>
            {
                _items.Remove(item);
                Commit();
            };
            Grid.SetColumn(remove, _setting.ItemFields.Count);
            grid.Children.Add(remove);
            _rows.Children.Add(grid);
        }
    }

    /// <summary>Sets one field (used by the text boxes, and by tests).</summary>
    internal void Update(Dictionary<string, string> item, string field, string? value)
    {
        if (item[field] == (value ?? ""))
            return;
        item[field] = value ?? "";
        Commit();
    }

    internal IReadOnlyList<Dictionary<string, string>> Items => _items;

    private void Commit()
    {
        _session.SetObjectList(_fileName, _setting.Name, _items.Select(i => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(i)));
        _changed();
    }

    private string Text(IEnumerable<IReadOnlyDictionary<string, string>> rows) =>
        string.Join("\n", rows.Select(r => string.Join("|", _setting.ItemFields.Select(f => r.TryGetValue(f, out var v) ? v : ""))));

    // FilePath and Text get more room than Link and Sha.
    private static string Width(string field) => field switch
    {
        "FilePath" or "PartName" => "2*",
        "Text" => "2*",
        _ => "*",
    };

    private static string Describe(string setting) => setting switch
    {
        "MandatoryPlugins" => "Players must have these DLLs (FilePath relative to GameData, lowercase). If Sha is set, the file must match it exactly.",
        "OptionalPlugins" => "Allowed DLLs. Only matters when Allow Non Listed Plugins is off.",
        "ForbiddenPlugins" => "Players with any of these DLLs can't join.",
        "MandatoryParts" => "Players must have these parts installed (the part's name, as KSP shows it).",
        _ => "",
    };
}
