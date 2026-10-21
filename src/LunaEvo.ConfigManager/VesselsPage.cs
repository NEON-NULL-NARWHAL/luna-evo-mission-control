// LUNA EVO · LunaEvo.ConfigManager/VesselsPage.cs
// [UI] Vessel cleanup: every vessel in the Universe, quick selections (debris, asteroids), remove with a backup first.
// Used by: MainWindow
// Guide:   Meat and Potatoes/21-Vessel-Cleanup.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using LunaEvo.ConfigManager.Core.Ops;

namespace LunaEvo.ConfigManager;

internal sealed class VesselsPage
{
    private const int MaxRows = 400;

    private readonly MainWindow _host;
    private readonly WrapPanel _cards = new() { Margin = new Thickness(0, 12, 0, 8) };
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly TextBox _search = new TextBox { PlaceholderText = "Search vessels...", Width = 300, HorizontalAlignment = HorizontalAlignment.Left }.WithId("vessels/search");
    private readonly Button _remove = Ui.Button("Remove selected", "fab").WithId("vessels/remove");
    private readonly TextBlock _result = Ui.Text("", "sublabel").WithId("vessels/result");
    private readonly HashSet<string> _selected = [];
    private IReadOnlyList<UniverseVessel> _vessels = [];
    private string? _kind; // the card filtering the list, or null for everything

    // The cards, and which vessels each one shows.
    private static readonly (string Label, Func<UniverseVessel, bool> Is)[] Kinds =
    [
        ("Vessels", v => !v.IsSpaceObject && !v.IsDebris),
        ("Debris", v => v.IsDebris),
        ("Asteroids & comets", v => v.IsSpaceObject),
        ("Crewed", v => v.HasCrew),
    ];

    public VesselsPage(MainWindow host)
    {
        _host = host;
        _search.TextChanged += (_, _) => ShowList();
        _remove.Click += async (_, _) => await RemoveSelectedAsync();
        var debris = Ui.Button("Select all debris", "text").WithId("vessels/selectDebris");
        debris.Click += (_, _) => SelectWhere(v => v.IsDebris);
        var rocks = Ui.Button("Select asteroids & comets", "text").WithId("vessels/selectSpaceObjects");
        rocks.Click += (_, _) => SelectWhere(v => v.IsSpaceObject);
        var clear = Ui.Button("Clear selection", "text").WithId("vessels/clear");
        clear.Click += (_, _) =>
        {
            _selected.Clear();
            ShowList();
        };
        var refresh = Ui.Button("Refresh", "text", "IconRestart").WithId("vessels/refresh");
        refresh.Click += (_, _) => Refresh();
        _result.TextWrapping = Avalonia.Media.TextWrapping.Wrap;

        var header = new DockPanel();
        DockPanel.SetDock(refresh, Dock.Right);
        header.Children.Add(refresh);
        header.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Ui.Text("Vessels", "headline"),
                Ui.Text("Everything in the Universe. Fewer vessels (especially debris) means less for every player's game to keep in step. " +
                        "Stop the server to remove any; a Universe backup is made first.", "sublabel"),
            },
        });
        var (listGroup, listBody, _) = Ui.Group("In the Universe");
        listBody.Children.Add(new StackPanel
        {
            Margin = new Thickness(12, 8, 12, 4), Spacing = 8,
            Children =
            {
                _search,
                new WrapPanel { Children = { debris, rocks, clear } },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { _remove } },
                _result,
            },
        });
        listBody.Children.Add(_list);
        View = new ScrollViewer
        {
            Content = new StackPanel { Margin = new Thickness(28, 20, 28, 28), Children = { header, _cards, listGroup } },
        };
    }

    public Control View { get; }

    public void Refresh()
    {
        _vessels = _host.Session is { } session ? UniverseVessels.List(session.ServerDirectory) : [];
        _selected.IntersectWith(_vessels.Select(v => v.Id));
        ShowCards();
        ShowList();
    }

    /// <summary>Shows only one kind of vessel (a card's label), or everything (null). Clicking the active card clears it.</summary>
    internal void ShowKind(string? kind)
    {
        _kind = Kinds.Any(k => k.Label == kind) ? kind : null;
        ShowCards();
        ShowList();
    }

    private void ShowCards()
    {
        _cards.Children.Clear();
        foreach (var (label, isKind) in Kinds)
            _cards.Children.Add(Ui.Card("vessels/card/" + label, label, _vessels.Count(isKind).ToString(),
                () => ShowKind(_kind == label ? null : label), _kind == label, _kind == label ? "Show everything" : "Show only these"));
    }

    private void ShowList()
    {
        _list.Children.Clear();
        var running = _host.Server?.IsRunning == true;
        _remove.IsEnabled = !running && _selected.Count > 0;
        ToolTip.SetTip(_remove, running ? "Stop the server first: LMP rewrites these files while it runs." : null);
        _remove.Content = _selected.Count == 0 ? "Remove selected" : $"Remove {_selected.Count} selected";

        var search = _search.Text?.Trim() ?? "";
        var kind = Kinds.FirstOrDefault(k => k.Label == _kind).Is;
        var shown = _vessels.Where(v => kind == null || kind(v))
            .Where(v => search.Length == 0 || v.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || v.Type.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(v => _selected.Contains(v.Id)).ThenBy(v => v.IsDebris ? 0 : v.IsSpaceObject ? 1 : 2).ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var vessel in shown.Take(MaxRows))
        {
            var check = new CheckBox { IsChecked = _selected.Contains(vessel.Id) }.WithId("vessels/select/" + vessel.Id);
            check.IsCheckedChanged += (_, _) =>
            {
                if (check.IsChecked == true) _selected.Add(vessel.Id);
                else _selected.Remove(vessel.Id);
                _remove.IsEnabled = _host.Server?.IsRunning != true && _selected.Count > 0;
                _remove.Content = _selected.Count == 0 ? "Remove selected" : $"Remove {_selected.Count} selected";
            };
            var crew = vessel.HasCrew ? $" · crew: {string.Join(", ", vessel.Crew.Take(4))}{(vessel.Crew.Count > 4 ? "..." : "")}" : "";
            _list.Children.Add(Ui.Row(vessel.Name, $"{vessel.Type} · {Ui.BodyName(vessel.Body)} · {Situation(vessel.Situation)} · {vessel.PartCount} part{(vessel.PartCount == 1 ? "" : "s")}{crew}", check)
                .WithId("vessels/row/" + vessel.Id));
        }
        if (shown.Count > MaxRows)
            _list.Children.Add(Ui.Row($"{shown.Count - MaxRows} more", "Search to narrow the list.", null));
        if (_list.Children.Count == 0)
            _list.Children.Add(Ui.Row(_host.Session == null ? "Open a server first" : "No vessels", null, null));
    }

    private void SelectWhere(Func<UniverseVessel, bool> filter)
    {
        // Never crewed vessels in a bulk selection: removing one would leave its kerbals stranded as "assigned".
        foreach (var vessel in _vessels.Where(v => filter(v) && !v.HasCrew))
            _selected.Add(vessel.Id);
        ShowList();
    }

    internal async Task RemoveSelectedAsync()
    {
        if (_host.Session is not { } session || _selected.Count == 0)
            return;
        if (_host.Server?.IsRunning == true)
        {
            _result.Text = "Stop the server first: LMP rewrites the vessel files while it runs.";
            return;
        }
        var targets = _vessels.Where(v => _selected.Contains(v.Id)).ToList();
        var crewed = targets.Count(v => v.HasCrew);
        if (!await Dialogs.ConfirmAsync(_host, $"Remove {targets.Count} vessel(s)?",
                "They're removed from the Universe for everyone. The whole Universe is backed up first (Profiles & backups → " +
                "\"beforecleanup\"), so this can be undone." +
                (crewed > 0 ? $" {crewed} of them have crew; those kerbals will show as missing until you restore." : ""), "Remove"))
            return;
        try
        {
            var backup = await Task.Run(() => UniverseVessels.Remove(session.ServerDirectory, targets));
            _result.Text = $"Removed {targets.Count} vessel(s). Backup: {backup.Name}.";
            _selected.Clear();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _result.Text = "Couldn't remove them: " + e.Message;
        }
        Refresh();
    }

    private void Card(string label, string value)
    {
        var card = new Border { Child = new StackPanel { Spacing = 2, Children = { Ui.Text(label, "sublabel"), Ui.Text(value, "value") } } }
            .WithId("vessels/card/" + label);
        card.Classes.Add("card");
        _cards.Children.Add(card);
    }

    private static string Situation(string sit) => sit.Length == 0 ? "?" : sit[..1] + sit[1..].ToLowerInvariant().Replace('_', ' ');
}
