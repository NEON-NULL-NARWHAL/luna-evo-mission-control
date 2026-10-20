// LUNA EVO · LunaEvo.ConfigManager/TabSet.cs
// [UI] A row of tabs over panels, used by Server settings, Players, Join and LUNA EVO Settings.
// Used by: MainWindow, PlayersPage, JoinPage, SettingsPage
// Guide:   Meat and Potatoes/04-App-UI.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace LunaEvo.ConfigManager;

/// <summary>
/// Tabs: put <see cref="Strip"/> where the tab buttons go and <see cref="Content"/> where the pages go. Every page
/// stays in the visual tree and only <see cref="Visual.IsVisible"/> changes, so a page keeps its state (scroll
/// position, half-typed text) and UI tests and Windows UI Automation find controls on any tab.
/// </summary>
internal sealed class TabSet
{
    private readonly string _idPrefix;
    private readonly StackPanel _strip = new() { Margin = new Thickness(0, 0, 0, 12) };
    private WrapPanel? _row;
    private readonly Grid _content = new();
    private readonly Dictionary<string, (Button Header, Control Page)> _tabs = [];

    /// <param name="idPrefix">Tab buttons get the AutomationId "&lt;prefix&gt;/&lt;key&gt;".</param>
    public TabSet(string idPrefix) => _idPrefix = idPrefix;

    public Control Strip => _strip;
    public Control Content => _content;
    public string? Selected { get; private set; }
    public IReadOnlyCollection<string> Keys => _tabs.Keys;

    /// <summary>Raised after the user (or code) switches tab.</summary>
    public event Action<string>? Changed;

    public bool Has(string key) => _tabs.ContainsKey(key);

    public Button Header(string key) => _tabs[key].Header;

    /// <summary>Adds a tab. The first one added is selected.</summary>
    public Button Add(string key, string title, Control page) => Add(key, new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center }, page);

    /// <param name="header">The tab's content, e.g. a title with an error badge.</param>
    public Button Add(string key, Control header, Control page)
    {
        var button = new Button { Content = header }.WithId($"{_idPrefix}/{key}");
        button.Classes.Add("tab");
        button.Click += (_, _) => Select(key);
        CurrentRow().Children.Add(button);
        page.IsVisible = false;
        _content.Children.Add(page);
        _tabs[key] = (button, page);
        if (Selected == null)
            Select(key);
        return button;
    }

    /// <summary>Starts a new row of tabs with a small heading, e.g. "Game" before General, Gameplay...</summary>
    public void AddLabel(string text)
    {
        _row = null;
        var label = Ui.Text(text, "tabLabel");
        label.VerticalAlignment = VerticalAlignment.Center;
        label.Width = 84;
        label.Margin = new Thickness(2, 0, 0, 6); // same bottom margin as the tabs, so they line up
        CurrentRow().Children.Add(label);
    }

    private WrapPanel CurrentRow()
    {
        if (_row == null)
        {
            _row = new WrapPanel();
            _strip.Children.Add(_row);
        }
        return _row;
    }

    public void Select(string key)
    {
        if (!_tabs.ContainsKey(key))
            return;
        foreach (var (name, (header, page)) in _tabs)
        {
            header.Classes.Set("selected", name == key);
            page.IsVisible = name == key;
        }
        Selected = key;
        Changed?.Invoke(key);
    }
}
