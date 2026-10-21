// LUNA EVO · LunaEvo.MissionControl/ListEditor.cs
// [UI] The searchable list editor used for LMPModControl.xml's part/resource/expansion lists.
// Used by: MainWindow (one per list setting)
// Guide:   Meat and Potatoes/04-App-UI.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using LunaEvo.MissionControl.Core;
using LunaEvo.MissionControl.Core.Schema;

namespace LunaEvo.MissionControl;

/// <summary>Searchable editor for the string lists in LMPModControl.xml (allowed parts, resources, expansions).</summary>
internal sealed class ListEditor
{
    private readonly ConfigSession _session;
    private readonly string _fileName;
    private readonly SettingDefinition _setting;
    private readonly Action _changed;
    private readonly TextBlock _title;
    private readonly TextBox _search;
    private readonly TextBox _newEntry;
    private readonly ListBox _list;
    private readonly Button _remove;
    private readonly Button _dedupe;
    private List<string> _items = [];

    /// <summary>A list position, so identical entries (duplicates) can be selected and removed one at a time.</summary>
    internal sealed record Entry(int Index, string Text, bool IsDuplicate);

    /// <summary>Builds the editor for one list setting. <paramref name="changed"/> is called after every edit so the window can refresh.</summary>
    public ListEditor(ConfigSession session, string fileName, SettingDefinition setting, Action changed)
    {
        _session = session;
        _fileName = fileName;
        _setting = setting;
        _changed = changed;

        var id = $"{fileName}/{setting.Name}";
        _search = new TextBox { PlaceholderText = "Search..." }.WithId(id + "/search");
        _newEntry = new TextBox { PlaceholderText = "New entry", Width = 240, Margin = new Thickness(8, 0) }.WithId(id + "/new");
        var add = Ui.Button("Add", "text").WithId(id + "/add");
        _remove = Ui.Button("Remove selected", "text").WithId(id + "/remove");
        _dedupe = Ui.Button("Remove duplicates", "text").WithId(id + "/dedupe");
        var reset = Ui.Button("Reset to LMP defaults", "text").WithId(id + "/reset");
        ToolTip.SetTip(reset, "Replace the list with the one LMP generates for a new server.");

        _list = new ListBox
        {
            MaxHeight = 320,
            MinHeight = 44,
            SelectionMode = SelectionMode.Multiple,
            ItemTemplate = new FuncDataTemplate<Entry>((entry, _) => EntryView(entry), supportsRecycling: false),
        }.WithId(id + "/items");
        _list.Classes.Add("entries");

        _search.TextChanged += (_, _) => ShowItems();
        add.Click += (_, _) => AddEntry();
        _newEntry.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) AddEntry();
        };
        _list.SelectionChanged += (_, _) => _remove.IsEnabled = _list.SelectedItems?.Count > 0;
        _remove.Click += (_, _) =>
        {
            var selected = _list.SelectedItems?.OfType<Entry>().Select(entry => entry.Index).ToHashSet() ?? [];
            Commit(_items.Where((_, i) => !selected.Contains(i)));
        };
        _dedupe.Click += (_, _) => Commit(_items.Distinct(StringComparer.Ordinal));
        reset.Click += (_, _) => Commit(_session.DefaultFor(_fileName, _setting.Name).Split('\n', StringSplitOptions.RemoveEmptyEntries));

        var toolbar = new DockPanel { Margin = new Thickness(8, 8, 8, 4) };
        DockPanel.SetDock(add, Dock.Right);
        DockPanel.SetDock(_newEntry, Dock.Right);
        toolbar.Children.Add(add);
        toolbar.Children.Add(_newEntry);
        toolbar.Children.Add(_search);

        var actions = new WrapPanel { Margin = new Thickness(4, 4, 4, 4), Children = { _remove, _dedupe, reset } };

        var (view, body, title) = Ui.Group(Ui.Humanize(setting.Name));
        _title = title;
        if (!string.IsNullOrWhiteSpace(setting.Description))
            body.Children.Add(new Border { Padding = new Thickness(12, 8, 12, 0), Child = Ui.Text(setting.Description, "sublabel") });
        body.Children.Add(toolbar);
        body.Children.Add(new Border { Margin = new Thickness(8, 4), Child = _list });
        body.Children.Add(actions);
        View = view;
    }

    public Control View { get; }

    /// <summary>Re-reads the list from the session (cheap when nothing changed).</summary>
    public void Refresh()
    {
        var current = _session.GetList(_fileName, _setting.Name);
        if (current.SequenceEqual(_items) && _list.ItemsSource != null)
            return;
        _items = current.ToList();
        ShowItems();
    }

    private void ShowItems()
    {
        var duplicates = _items.Count - _items.Distinct(StringComparer.Ordinal).Count();
        var modified = _session.IsModified(_fileName, _setting.Name) ? " · modified" : "";
        _title.Text = $"{Ui.Humanize(_setting.Name)} · {_items.Count} entries{(duplicates > 0 ? $" · {duplicates} duplicates" : "")}{modified}";
        _dedupe.IsEnabled = duplicates > 0;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var filter = _search.Text?.Trim() ?? "";
        _list.ItemsSource = _items
            .Select((text, i) => new Entry(i, text, !seen.Add(text)))
            .Where(e => filter.Length == 0 || e.Text.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
        _remove.IsEnabled = false;
    }

    private static Control EntryView(Entry? entry)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        if (entry == null)
            return panel;
        panel.Children.Add(new TextBlock { Text = entry.Text, VerticalAlignment = VerticalAlignment.Center });
        if (entry.IsDuplicate)
            panel.Children.Add(Ui.Text("duplicate", "warning"));
        return panel;
    }

    private void AddEntry()
    {
        var text = _newEntry.Text?.Trim() ?? "";
        if (text.Length == 0)
            return;
        _newEntry.Text = "";
        if (!_items.Contains(text, StringComparer.Ordinal))
            Commit(_items.Append(text));
    }

    private void Commit(IEnumerable<string> items)
    {
        _session.SetList(_fileName, _setting.Name, items);
        _changed();
    }
}
