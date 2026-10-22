// LUNA EVO · LunaEvo.MissionControl/MapPage.cs
// [UI] Solar system map: the bodies (stock plus planet packs) and every vessel on the server, on its orbit, where it is now.
// Used by: MainWindow
// Guide:   Meat and Potatoes/24-Solar-System-Map.md

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LunaEvo.MissionControl.Core.Join;
using LunaEvo.MissionControl.Core.Live;
using LunaEvo.MissionControl.Core.Map;
using LunaEvo.MissionControl.Core.Ops;

namespace LunaEvo.MissionControl;

internal sealed class MapPage
{
    private readonly MainWindow _host;
    private readonly MapCanvas _canvas = new();
    private readonly ComboBox _focus = new ComboBox { MinWidth = 220, IsTextSearchEnabled = true }.WithId("map/focus");
    private readonly CheckBox _rocks = new CheckBox { Content = "Asteroids & comets", IsChecked = true }.WithId("map/rocks");
    private readonly CheckBox _debris = new CheckBox { Content = "Debris", IsChecked = true }.WithId("map/debris");
    private readonly TextBlock _time = Ui.Text("", "sublabel").WithId("map/time");
    private readonly TextBlock _packs = Ui.Text("", "sublabel").WithId("map/packs");
    private readonly StackPanel _details = new() { Spacing = 2 };
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private SolarSystem _system = SolarSystem.Stock();
    private string? _systemFrom; // the GameData folder the bodies were read from
    private IReadOnlyList<UniverseVessel> _saved = []; // Universe/Vessels as LMP last wrote it
    private IReadOnlyList<UniverseVessel> _vessels = []; // what's shown: the save, or the save merged with the live server
    private DateTime _vesselsRead;
    private ServerSnapshot? _liveFrom; // the live snapshot _vessels was merged from, if any
    private LiveFeed? _feed; // the LUNA EVO server plugin's live.json, if it's running
    private readonly Button _plugin = Ui.Button("", "text").WithId("map/plugin");
    private readonly TextBlock _source = Ui.Text("", "sublabel").WithId("map/source");
    private readonly Button _goLive = Ui.Button("Turn on the website for a live map (this PC only)", "text").WithId("map/goLive");
    private bool _updatingFocus;

    public MapPage(MainWindow host)
    {
        _host = host;
        _canvas.Height = 560;
        _canvas.VesselClicked += vessel => Select(vessel);
        _canvas.BodyClicked += body => Focus(body);
        _focus.SelectionChanged += (_, _) =>
        {
            if (!_updatingFocus && _focus.SelectedItem is ComboBoxItem { Tag: string name })
                Focus(name);
        };
        _rocks.IsCheckedChanged += (_, _) => Redraw();
        _debris.IsCheckedChanged += (_, _) => Redraw();
        var up = Ui.Button("Up", "text").WithId("map/up");
        ToolTip.SetTip(up, "Show the body this one orbits");
        up.Click += (_, _) =>
        {
            if (_system.Find(_canvas.FocusBody)?.Parent is { } parent)
                Focus(parent);
        };
        var fit = Ui.Button("Zoom to vessels", "text").WithId("map/fit");
        ToolTip.SetTip(fit, "Fit the view to the vessels' orbits, or back to the whole system around this body");
        fit.Click += (_, _) =>
        {
            _canvas.FitVessels = !_canvas.FitVessels;
            fit.Content = _canvas.FitVessels ? "Show all" : "Zoom to vessels";
            _canvas.ResetView();
        };
        var refresh = Ui.Button("Refresh", "text", "IconRestart").WithId("map/refresh");
        refresh.Click += (_, _) => Refresh(force: true);
        _plugin.Click += async (_, _) => await TogglePluginAsync();
        _goLive.Click += (_, _) =>
        {
            _host.Players?.EnableWebsite();
            _source.Text = "Website turned on for this PC only. Save, then restart the server, and the map goes live.";
            _goLive.IsVisible = false;
        };
        _tick.Tick += (_, _) => OnTick();

        var header = new DockPanel();
        DockPanel.SetDock(refresh, Dock.Right);
        header.Children.Add(refresh);
        header.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Ui.Text("Solar system map", "headline"),
                Ui.Text("Every vessel on the server, on its orbit, where it is now. While the server runs with its website on, positions " +
                        "come live from it (a few seconds behind); otherwise from the save, which LMP writes every few minutes. Scroll to zoom, drag to move, click a vessel " +
                        "or a moon.", "sublabel"),
            },
        });
        foreach (var c in new Control[] { _rocks, _debris, up, fit })
            c.Margin = new Thickness(12, 0, 0, 0);
        var controls = new WrapPanel { Children = { _focus, up, fit, _rocks, _debris } };
        var frame = new Border { Child = _canvas, ClipToBounds = true }.WithId("map/canvas");
        frame.Classes.Add("console");
        var (details, detailsBody, _) = Ui.Group("Selected");
        detailsBody.Children.Add(new Border { Padding = new Thickness(12, 4, 12, 12), Child = _details });
        var (list, listBody, _) = Ui.Group("Around this body");
        listBody.Children.Add(_list);
        View = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(28, 20, 28, 28), Spacing = 12,
                Children = { header, controls, new StackPanel { Spacing = 2, Children = { _time, _source, _goLive, _plugin, _packs } }, frame, details, list },
            },
        };
        _canvas.FocusBody = _system.Star.Name;
    }

    public Control View { get; }

    internal MapCanvas Canvas => _canvas;
    internal SolarSystem System => _system;
    internal IReadOnlyList<UniverseVessel> Vessels => _vessels;

    /// <summary>Opening the page: bodies (from your KSP's GameData when it changed), vessels and the time; then a redraw every 2 s.</summary>
    public void Refresh(bool force = false)
    {
        var gameData = GameData();
        if (force || gameData != _systemFrom)
        {
            _system = SolarSystem.Load(gameData);
            _systemFrom = gameData;
            _canvas.System = _system;
            if (_system.Find(_canvas.FocusBody) == null)
                _canvas.FocusBody = _system.Star.Name;
            _packs.Text = _system.FromPacks.Count == 0
                ? gameData == null ? "Stock bodies (no KSP install found for planet packs)." : "Stock bodies."
                : $"{_system.Bodies.Count} bodies, {_system.FromPacks.Count} added or changed by planet packs in your GameData.";
        }
        ReadVessels();
        FillFocus();
        Redraw();
        _tick.Start();
    }

    internal void Stop() => _tick.Stop();

    /// <summary>Shows one body and everything around it.</summary>
    internal void Focus(string body)
    {
        if (_system.Find(body) is not { } found)
            return;
        _canvas.FocusBody = found.Name;
        _canvas.ResetView();
        FillFocus();
        Redraw();
    }

    internal void Select(UniverseVessel? vessel)
    {
        _canvas.Selected = vessel?.Id;
        _details.Children.Clear();
        if (vessel == null)
        {
            _details.Children.Add(Ui.Text("Click a vessel on the map or in the list.", "sublabel"));
        }
        else
        {
            var body = _system.ForVessel(vessel.BodyName, vessel.Body);
            _details.Children.Add(Ui.Text(vessel.Name, "title").WithId("map/selected"));
            _details.Children.Add(Ui.Text($"{vessel.Type} · {Situation(vessel.Situation)} · {body?.Name ?? "?"} · {vessel.PartCount} part(s)", "sublabel"));
            if (vessel.Orbit is { } orbit && body != null && !vessel.IsLanded)
            {
                var a = orbit.Sma;
                var pe = a * (1 - orbit.Ecc) - body.Radius;
                var line = orbit.IsOpen
                    ? $"Escaping {body.Name}: periapsis {Distance(pe)}, inclination {orbit.Inc:0.#}°"
                    : $"Apoapsis {Distance(a * (1 + orbit.Ecc) - body.Radius)} · periapsis {Distance(pe)} · inclination {orbit.Inc:0.#}° · " +
                      $"period {Duration(orbit.Period(body.Mu) ?? 0)}";
                _details.Children.Add(Ui.Text(line, "sublabel"));
            }
            else if (vessel.Surface is { } surface)
            {
                _details.Children.Add(Ui.Text($"On the surface at {surface.Lat:0.00}°, {surface.Lon:0.00}°", "sublabel"));
            }
            if (vessel.HasCrew)
                _details.Children.Add(Ui.Text("Crew: " + string.Join(", ", vessel.Crew), "sublabel"));
            if (vessel.FlownBy != null)
                _details.Children.Add(Ui.Text("Flown by " + vessel.FlownBy, "sublabel").WithId("map/flownBy"));
        }
        _canvas.InvalidateVisual();
    }

    private void OnTick()
    {
        if (!View.IsEffectivelyVisible)
            return;
        // While the server runs LMP rewrites the vessel files; pick up the new ones now and then.
        if (DateTime.UtcNow - _vesselsRead > TimeSpan.FromSeconds(30))
        {
            ReadVessels();
            FillList();
        }
        else if (Live() != _liveFrom || _feed != null || LiveFeedFor() != null)
        {
            // The Players page read the server again (every 5 s), or the plugin wrote (every second): move the vessels to
            // their new orbits. The list below only changes when vessels come or go.
            var before = string.Join(",", _vessels.Select(v => v.Id));
            MergeLive();
            if (string.Join(",", _vessels.Select(v => v.Id)) != before)
                FillList();
            else if (_canvas.Selected is { } id && _vessels.FirstOrDefault(v => v.Id == id) is { } selected)
                Select(selected); // its orbit numbers changed
        }
        UpdateTime();
        _canvas.InvalidateVisual();
    }

    private void ReadVessels()
    {
        _saved = _host.Session is { } session ? UniverseVessels.List(session.ServerDirectory) : [];
        _vesselsRead = DateTime.UtcNow;
        MergeLive();
    }

    /// <summary>The running server's latest vessel list (read by the Players page), if it's recent.</summary>
    private ServerSnapshot? Live() =>
        _host.Server?.IsRunning == true && _host.Players is { Last: { } last } players && DateTime.UtcNow - players.LastAt < LiveMaxAge ? last : null;

    /// <summary>Older than this, the live list is treated as gone (the website stopped answering).</summary>
    internal static readonly TimeSpan LiveMaxAge = TimeSpan.FromSeconds(30);

    private LiveFeed? LiveFeedFor() => _host.Session is { } session ? LiveFeed.Read(session.ServerDirectory) : null;

    private void MergeLive()
    {
        _liveFrom = Live();
        _vessels = _liveFrom is { } live ? LiveVessels.Merge(_saved, live.Vessels) : _saved;
        // The server plugin's positions are newer still (every message, not every 2.5 s) and say who flies what.
        _feed = LiveFeedFor();
        if (_feed != null)
            _vessels = LiveVessels.Overlay(_vessels, _feed, LiveVessels.BodyNames(_saved));
        _canvas.Vessels = Shown();
        UpdateSource();
    }

    /// <summary>The plugin's DLL, carried inside this exe (see the csproj).</summary>
    internal static byte[]? PluginBytes() => Resource(ServerPluginInstaller.FileName);

    /// <summary>The Harmony the plugin uses since 2.7 (its P1 fix), carried inside this exe too.</summary>
    internal static byte[]? HarmonyBytes() => Resource("LunaEvo.ServerPlugin.0Harmony.dll");

    private static byte[]? Resource(string name)
    {
        using var stream = typeof(MapPage).Assembly.GetManifestResourceStream(name);
        if (stream == null)
            return null;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private PluginStatus? PluginState() =>
        _host.Session is { } session && PluginBytes() is { } bytes && ServerPluginInstaller.Supports(Core.Setup.LmpVersion.Parse(session.Schema.ServerVersion))
            ? ServerPluginInstaller.Status(session.ServerDirectory, bytes)
            : null;

    internal async Task TogglePluginAsync()
    {
        if (_host.Session is not { } session || PluginBytes() is not { } bytes || HarmonyBytes() is not { } harmony || PluginState() is not { } state)
            return;
        if (_host.Server?.IsRunning == true)
        {
            await Dialogs.InfoAsync(_host, "Stop the server first",
                "LMP keeps its plugins open while it runs. Stop the server, change the plugin, then start it again.");
            return;
        }
        try
        {
            if (state == PluginStatus.Installed)
            {
                if (!await Dialogs.ConfirmAsync(_host, "Remove the LUNA EVO server plugin?",
                        "The map goes back to the server's website (every few seconds) or its save.", "Remove"))
                    return;
                ServerPluginInstaller.Remove(session.ServerDirectory);
            }
            else
            {
                if (!await Dialogs.ConfirmAsync(_host, state == PluginStatus.Different ? "Replace the server plugin?" : "Install the LUNA EVO server plugin?",
                        $"Copies {ServerPluginInstaller.FileName} and the Harmony library it uses into this server's Plugins\\LunaEvo folder. " +
                        "LMP loads them on its next start. The plugin watches the server's messages and writes every vessel's latest " +
                        "position, and who flies what, to LunaEvo\\live.json, next to the server, for this map. It never blocks a " +
                        "message, opens no network port and stores no addresses. It also makes two speed fixes that change nothing " +
                        "players need: 1 ms timers on Windows, and its own network channel for each vessel's updates. Players don't need " +
                        "anything.", state == PluginStatus.Different ? "Replace" : "Install"))
                    return;
                ServerPluginInstaller.Install(session.ServerDirectory, bytes, harmony);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await Dialogs.InfoAsync(_host, "Couldn't change the plugin", e.Message);
        }
        UpdateSource();
    }

    private void UpdateSource()
    {
        var websiteOff = _host.Session is { } session && ServerInfoClient.EndpointFor(session) == null;
        _goLive.IsVisible = websiteOff && _host.Session != null;
        var plugin = PluginState();
        _plugin.IsVisible = plugin != null;
        _plugin.Content = plugin switch
        {
            PluginStatus.Installed => "Remove the LUNA EVO server plugin",
            PluginStatus.Different => "Update the LUNA EVO server plugin",
            _ => "Install the LUNA EVO server plugin: live every second, and who flies what",
        };
        _source.Text = _feed is { } feed
            ? $"Live from the LUNA EVO server plugin, {Math.Max(0, (int)(DateTime.UtcNow - feed.WrittenUtc).TotalSeconds)} s ago · " +
              $"{feed.Players.Count} player(s), {feed.Players.Count(p => p.Vessel != null)} flying."
            : _liveFrom != null && _host.Players is { } players
            ? $"Live from the running server, read {Math.Max(0, (int)(DateTime.UtcNow - players.LastAt).TotalSeconds)} s ago."
            : _host.Session == null ? ""
            : websiteOff ? "From the save, which LMP writes every few minutes. The map can be live while the server runs:"
            : _host.Server?.IsRunning == true ? "From the save for now; live positions appear once the server's website answers."
            : "From the save (the server is stopped).";
    }

    private IReadOnlyList<UniverseVessel> Shown() =>
        _vessels.Where(v => (_rocks.IsChecked == true || !v.IsSpaceObject) && (_debris.IsChecked == true || !v.IsDebris)).ToList();

    private void Redraw()
    {
        _canvas.Vessels = Shown();
        UpdateTime();
        FillList();
        if (_canvas.Selected is { } id && _vessels.FirstOrDefault(v => v.Id == id) is { } selected)
            Select(selected);
        else
            Select(null);
    }

    private void UpdateTime()
    {
        // The server's clock; without its files (no server open), the newest vessel epoch.
        var now = _host.Session is { } session ? UniverseClock.Now(session.ServerDirectory) : null;
        now ??= _vessels.Select(v => v.Orbit?.Epoch ?? 0).DefaultIfEmpty(0).Max();
        _canvas.Time = now.Value;
        _time.Text = $"Universe time: {UniverseClock.Kerbin(now.Value)} · {_vessels.Count} vessel(s) on the server";
        UpdateSource();
    }

    private void FillFocus()
    {
        _updatingFocus = true;
        var items = new List<ComboBoxItem>();
        void Add(CelestialBody body, int depth)
        {
            var item = new ComboBoxItem { Content = new string(' ', depth * 3) + body.Name, Tag = body.Name };
            // Typing a name jumps to it ("du" → Duna): match the name, not the indented text shown.
            Avalonia.Controls.Primitives.TextSearch.SetText(item, body.Name);
            items.Add(item);
            foreach (var child in _system.ChildrenOf(body.Name).OrderBy(c => Math.Abs(c.Orbit?.Sma ?? 0)))
                Add(child, depth + 1);
        }
        Add(_system.Star, 0);
        _focus.ItemsSource = items;
        _focus.SelectedItem = items.FirstOrDefault(i => (string)i.Tag! == _canvas.FocusBody);
        _updatingFocus = false;
    }

    private void FillList()
    {
        _list.Children.Clear();
        var around = _canvas.Vessels.Where(v => string.Equals(_system.ForVessel(v.BodyName, v.Body)?.Name, _canvas.FocusBody, StringComparison.OrdinalIgnoreCase))
            .OrderBy(v => v.IsSpaceObject).ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase).Take(300).ToList();
        foreach (var vessel in around)
        {
            var show = Ui.Button("Show", "text").WithId("map/show/" + vessel.Id);
            show.Click += (_, _) => Select(vessel);
            _list.Children.Add(Ui.Row(vessel.Name, $"{vessel.Type} · {Situation(vessel.Situation)}", show).WithId("map/vessel/" + vessel.Id));
        }
        if (around.Count == 0)
            _list.Children.Add(Ui.Row(_host.Session == null ? "Open a server to see its vessels" : $"No vessels around {_canvas.FocusBody}", null, null));
    }

    private string? GameData()
    {
        var ksp = _host.Settings.KspFolder is { } k && KspLauncher.IsKspFolder(k) ? k : KspLauncher.FindKsp();
        return ksp == null ? null : Path.Combine(ksp, "GameData");
    }

    internal static string Situation(string sit) => sit switch
    {
        "ORBITING" => "in orbit", "LANDED" => "landed", "SPLASHED" => "splashed down", "ESCAPING" => "escaping",
        "FLYING" => "flying", "SUB_ORBITAL" => "sub-orbital", "PRELAUNCH" => "on the pad", _ => sit.ToLowerInvariant(),
    };

    internal static string Distance(double metres) =>
        Math.Abs(metres) >= 1e9 ? $"{metres / 1e9:0.##} Gm" : Math.Abs(metres) >= 1e6 ? $"{metres / 1e6:0.##} Mm" : $"{metres / 1e3:0.#} km";

    internal static string Duration(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        var kerbinDays = (int)(seconds / 21600); // 6-hour days
        return kerbinDays > 0 ? $"{kerbinDays} d {t.Hours % 6} h" : t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes} min" : $"{t.Minutes} min {t.Seconds} s";
    }
}

/// <summary>
/// Draws one body (the focus), the bodies orbiting it and every vessel around it, top-down. Distances in metres are
/// scaled to fit; bodies get a minimum size so they stay visible. Scroll zooms around the pointer, drag pans.
/// </summary>
internal sealed class MapCanvas : Control
{
    private const double MinBodyPixels = 5;
    private readonly Dictionary<(string, string), IReadOnlyList<Point3>> _paths = []; // (kind:name, focus) → orbit path
    private double _zoom = 1;
    private Vector _pan;
    private Point? _dragFrom;
    private Point _pressedAt;
    private readonly List<(Point At, UniverseVessel Vessel)> _vesselMarks = [];
    private readonly List<(Point At, double Radius, string Body)> _bodyMarks = [];
    private readonly List<Rect> _labels = []; // drawn this frame, so vessel labels don't pile up

    public SolarSystem System { get; set; } = SolarSystem.Stock();
    public IReadOnlyList<UniverseVessel> Vessels { get; set; } = [];
    public string FocusBody { get; set; } = "Sun";
    public string? Selected { get; set; }
    public double Time { get; set; }

    /// <summary>Fit the vessels' orbits around the focus instead of its whole system of moons.</summary>
    public bool FitVessels { get; set; }

    public event Action<UniverseVessel>? VesselClicked;
    public event Action<string>? BodyClicked;

    internal IReadOnlyList<(Point At, UniverseVessel Vessel)> VesselMarks => _vesselMarks;
    internal IReadOnlyList<(Point At, double Radius, string Body)> BodyMarks => _bodyMarks;

    public void ResetView()
    {
        _zoom = 1;
        _pan = default;
        _paths.Clear();
        InvalidateVisual();
    }

    internal static string ColorFor(string type) => type switch
    {
        "Ship" => "#4FC3F7", "Station" => "#FFB74D", "Base" or "Lander" or "Rover" => "#81C784", "Relay" or "Probe" => "#BA68C8",
        "Debris" => "#9E9E9E", "SpaceObject" => "#A1887F", "Plane" => "#F06292", "EVA" => "#FFF176", _ => "#E0E0E0",
    };

    /// <summary>How far out to show: the farthest moon or vessel orbit around the focus, within its sphere of influence.</summary>
    private double Extent(CelestialBody focus)
    {
        var soi = focus.SphereOfInfluence(System);
        // Every moon counts (they're the frame of reference); vessels count unless one is an outlier, i.e. beyond a
        // jump of more than 4x (one far body or comet-like orbit would otherwise squash everything into the middle).
        var moons = System.ChildrenOf(focus.Name).Select(c => Math.Abs(c.Orbit!.Sma) * (1 + Math.Min(c.Orbit.Ecc, 0.99))).Where(r => r > 0).Order().ToList();
        var vessels = AroundFocus().Where(v => v.Orbit is { IsOpen: false } && !v.IsSpaceObject && !v.IsLanded)
            .Select(v => Math.Abs(v.Orbit!.Sma) * (1 + v.Orbit.Ecc)).Where(r => r > 0).Order().ToList();
        static double MainGroup(List<double> reach)
        {
            var farthest = reach.Count == 0 ? 0 : reach[0];
            for (var i = 1; i < reach.Count; i++)
            {
                if (i >= 3 && reach[i] > reach[i - 1] * 4)
                    break;
                farthest = reach[i];
            }
            return farthest;
        }
        var farthest = FitVessels && vessels.Count > 0 ? MainGroup(vessels) : Math.Max(MainGroup(moons), MainGroup(vessels));
        if (farthest <= 0)
            farthest = focus.Radius * 4;
        return Math.Max(focus.Radius * 2, Math.Min(farthest * 1.08, double.IsInfinity(soi) ? double.MaxValue : soi));
    }

    private IEnumerable<UniverseVessel> AroundFocus() =>
        Vessels.Where(v => string.Equals(System.ForVessel(v.BodyName, v.Body)?.Name, FocusBody, StringComparison.OrdinalIgnoreCase));

    public override void Render(DrawingContext context)
    {
        _vesselMarks.Clear();
        _bodyMarks.Clear();
        _labels.Clear();
        var size = Bounds.Size;
        if (size.Width < 10 || size.Height < 10 || System.Find(FocusBody) is not { } focus)
            return;
        var text = Brush("OnSurface", Colors.White);
        var muted = Brush("OnSurfaceVariant", Colors.Gray);
        var center = new Point(size.Width / 2, size.Height / 2) + _pan;
        var scale = Math.Min(size.Width, size.Height) / 2 / Extent(focus) * _zoom;
        Point Screen(Point3 p) => new(center.X + p.X * scale, center.Y - p.Y * scale);
        var soi = focus.SphereOfInfluence(System);

        // The focus body.
        var focusRadius = Math.Max(MinBodyPixels * 2, focus.Radius * scale);
        context.DrawEllipse(Brushes(focus.Color, 1), null, center, focusRadius, focusRadius);
        Label(context, focus.Name, center + new Vector(focusRadius + 4, -focusRadius - 14), text);

        // Moons/planets around it: orbit, position, name (and how many vessels are around them).
        foreach (var child in System.ChildrenOf(FocusBody))
        {
            var path = Path("body:" + child.Name, () => child.Orbit!.Path());
            Line(context, path, Screen, new Pen(Brushes(child.Color, 0.35), 1));
            if (System.PositionOf(child.Name, FocusBody, Time) is not { } at)
                continue;
            var p = Screen(at);
            var r = Math.Max(MinBodyPixels, child.Radius * scale);
            context.DrawEllipse(Brushes(child.Color, 1), null, p, r, r);
            _bodyMarks.Add((p, Math.Max(r, 8), child.Name));
            var count = Vessels.Count(v => string.Equals(System.ForVessel(v.BodyName, v.Body)?.Name, child.Name, StringComparison.OrdinalIgnoreCase));
            Label(context, count > 0 ? $"{child.Name} ({count})" : child.Name, p + new Vector(r + 3, -r - 12), muted);
        }

        // Vessels around the focus: orbit (escapes only out to the sphere of influence) and where they are now.
        foreach (var vessel in AroundFocus())
        {
            var color = ColorFor(vessel.Type);
            var selected = vessel.Id == Selected;
            Point at;
            if (vessel.IsLanded || vessel.Orbit == null)
            {
                // On the surface: a mark on the body's edge at its longitude (the body's rotation isn't modelled).
                var lon = (vessel.Surface?.Lon ?? 0) * Math.PI / 180;
                at = center + new Vector(Math.Cos(lon) * focusRadius, -Math.Sin(lon) * focusRadius);
            }
            else
            {
                var orbit = vessel.Orbit;
                if (!vessel.IsSpaceObject || selected)
                    Line(context, Path("vessel:" + vessel.Id, () => orbit.Path(maxRadius: double.IsInfinity(soi) ? Math.Abs(orbit.Sma) * 20 : soi)), Screen,
                        new Pen(Brushes(color, selected ? 0.95 : 0.45), selected ? 2 : 1));
                at = Screen(orbit.PositionAt(Time, focus.Mu));
            }
            var size2 = selected ? 6 : vessel.IsSpaceObject || vessel.IsDebris ? 2.5 : 4;
            context.DrawEllipse(Brushes(color, 1), selected ? new Pen(text, 1.5) : null, at, size2, size2);
            _vesselMarks.Add((at, vessel));
            // Landed vessels sit together (often at the launch site): they share one label below.
            if (selected || (!vessel.IsLanded && !vessel.IsSpaceObject && !vessel.IsDebris && _zoom >= 1))
                Label(context, vessel.Name, at + new Vector(6, -6), selected ? text : muted, skipIfCrowded: !selected);
        }
        if (AroundFocus().Count(v => v.IsLanded || v.Orbit == null) is var landed and > 0)
            Label(context, landed == 1 ? "1 on the surface" : $"{landed} on the surface", center + new Vector(-40, focusRadius + 8), muted);
        if (!double.IsInfinity(soi) && soi * scale < Math.Max(size.Width, size.Height) * 2)
            context.DrawEllipse(null, new Pen(Brushes("#FFFFFF", 0.08), 1, new DashStyle([4, 4], 0)), center, soi * scale, soi * scale);
    }

    private IReadOnlyList<Point3> Path(string key, Func<IReadOnlyList<Point3>> make)
    {
        if (!_paths.TryGetValue((key, FocusBody), out var path))
            _paths[(key, FocusBody)] = path = make();
        return path;
    }

    private static void Line(DrawingContext context, IReadOnlyList<Point3> path, Func<Point3, Point> screen, Pen pen)
    {
        if (path.Count < 2)
            return;
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(screen(path[0]), false);
            for (var i = 1; i < path.Count; i++)
                g.LineTo(screen(path[i]));
            g.EndFigure(false);
        }
        context.DrawGeometry(null, pen, geometry);
    }

    private void Label(DrawingContext context, string text, Point at, IBrush brush, bool skipIfCrowded = false)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(Program.FontFamily), 11, brush);
        var rect = new Rect(at, new Size(formatted.Width, formatted.Height));
        if (skipIfCrowded && _labels.Any(r => r.Intersects(rect)))
            return; // the vessel's dot is still there; click it (or zoom in) for its name
        _labels.Add(rect);
        context.DrawText(formatted, at);
    }

    private IBrush Brush(string key, Color fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : new SolidColorBrush(fallback);

    private static IBrush Brushes(string hex, double opacity) =>
        new SolidColorBrush(Color.TryParse(hex, out var c) ? c : Colors.LightGray, opacity);

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        // Zoom around the pointer: the point under it stays put.
        var at = e.GetPosition(this);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2) + _pan;
        var factor = e.Delta.Y > 0 ? 1.25 : 0.8;
        var next = Math.Clamp(_zoom * factor, 0.05, 50_000);
        factor = next / _zoom;
        _zoom = next;
        _pan += (at - center) * (1 - factor);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _dragFrom = _pressedAt = e.GetPosition(this);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (_dragFrom is not { } from)
            return;
        var now = e.GetPosition(this);
        _pan += now - from;
        _dragFrom = now;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        _dragFrom = null;
        e.Pointer.Capture(null);
        var at = e.GetPosition(this);
        if (Math.Abs(at.X - _pressedAt.X) + Math.Abs(at.Y - _pressedAt.Y) > 4)
            return; // that was a drag
        Click(at);
    }

    /// <summary>A click: the nearest vessel within 10 px is selected, else a body under the pointer is focused.</summary>
    internal void Click(Point at)
    {
        var vessel = _vesselMarks.Select(m => (m.Vessel, Distance: Math.Sqrt(Math.Pow(m.At.X - at.X, 2) + Math.Pow(m.At.Y - at.Y, 2))))
            .Where(m => m.Distance <= 10).OrderBy(m => m.Distance).FirstOrDefault();
        if (vessel.Vessel != null)
        {
            VesselClicked?.Invoke(vessel.Vessel);
            return;
        }
        var body = _bodyMarks.FirstOrDefault(m => Math.Sqrt(Math.Pow(m.At.X - at.X, 2) + Math.Pow(m.At.Y - at.Y, 2)) <= m.Radius + 4);
        if (body.Body != null)
            BodyClicked?.Invoke(body.Body);
    }
}
