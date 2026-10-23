// LUNA EVO · LunaEvo.MissionControl/TowerPage.cs
// [UI] The KSC tower page: the traffic controller for AI agents (KSP Wingman), its channel (agents asking for
//      approaches, the tower asking intentions, clearances, holds) and the planes it's handling.
// Used by: MainWindow (rail: Play → KSC tower)
// Guide:   Meat and Potatoes/28-Tower.md

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using LunaEvo.MissionControl.Core.Tower;

namespace LunaEvo.MissionControl;

/// <summary>
/// The tower runs in LUNA EVO (not in KSP): agents' Wingman servers on this PC connect to it, report where they are
/// every few seconds and get the tower's messages back. This page starts and stops it and shows the conversation.
/// </summary>
internal sealed class TowerPage
{
    /// <summary>Channel lines shown, newest last (the tower keeps them all).</summary>
    public const int MaxLines = 200;

    private readonly StackPanel _status = new() { Spacing = 2 };
    private readonly StackPanel _planes = new() { Spacing = 2 };
    private readonly StackPanel _channel = new() { Spacing = 1 };
    private readonly ScrollViewer _channelScroll;
    private int _port = TowerServer.DefaultPort;
    private string? _problem;
    private readonly string? _logFolder;

    /// <param name="logFolder">Where the channel's data goes (tower-&lt;date&gt;.jsonl), or null for nowhere (tests).</param>
    public TowerPage(string? logFolder = null)
    {
        _logFolder = logFolder;
        var header = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Ui.Text("KSC tower", "headline"),
                Ui.Text("A traffic controller for AI pilots (KSP Wingman). Agents on this PC ask it for an approach; it asks " +
                        "their intentions, clears one plane at a time for a published RNAV approach to KSC's runway 09, holds the " +
                        "next one, and tells a landed plane to vacate the runway. Its channel is separate from LMP's chat.", "sublabel"),
            },
        };
        var (status, statusBody, _) = Ui.Group("Tower");
        statusBody.Children.Add(_status);
        var (traffic, trafficBody, _) = Ui.Group("Traffic");
        trafficBody.Children.Add(_planes);
        var (channel, channelBody, _) = Ui.Group("Channel");
        _channelScroll = new ScrollViewer { Content = _channel, MaxHeight = 420 }.WithId("tower/channel");
        channelBody.Children.Add(_channelScroll);
        View = new ScrollViewer
        {
            Content = new StackPanel { Margin = new Thickness(28, 20, 28, 28), Spacing = 16, Children = { header, status, traffic, channel } },
        };
        Refresh();
    }

    public Control View { get; }

    /// <summary>The running tower, if any (tests talk to it through Answer, without the network).</summary>
    public TowerServer? Server { get; private set; }

    /// <summary>Starts the tower on a port (the command line's --tower, or the button). A busy port is reported, not thrown.</summary>
    public void Start(int port)
    {
        Stop();
        _port = port;
        try
        {
            var server = new TowerServer(port)
            {
                LogPath = _logFolder == null ? null : Path.Combine(_logFolder, $"tower-{DateTime.Now:yyyy-MM-dd}.jsonl"),
            };
            server.Changed += () => Dispatcher.UIThread.Post(Refresh);
            server.Start();
            Server = server;
            _problem = null;
        }
        catch (System.Net.HttpListenerException e)
        {
            _problem = $"Couldn't listen on localhost:{port}: {e.Message}";
        }
        Refresh();
    }

    public void Stop()
    {
        Server?.Dispose();
        Server = null;
        Refresh();
    }

    public void Refresh()
    {
        _status.Children.Clear();
        var running = Server != null;
        var button = Ui.Button(running ? "Stop the tower" : "Start the tower", running ? "text" : "fab").WithId("tower/start");
        button.Click += (_, _) => { if (Server != null) Stop(); else Start(_port); };
        var state = _problem ?? (running
            ? $"On, on localhost:{_port}. Agents connect with WINGMAN_TOWER_URL=http://localhost:{_port}/ (KSP Wingman 0.5 or later)."
            : "Off. Start it before the agents ask to land.");
        _status.Children.Add(Ui.Row("KSC tower · runway 09", state, button).WithId("tower/status"));
        if (_logFolder != null)
        {
            var open = Ui.Button("Open the log folder", "text").WithId("tower/logs");
            open.Click += (_, _) => { Directory.CreateDirectory(_logFolder); Ui.Open(_logFolder); };
            _status.Children.Add(Ui.Row("Channel log", "Every message, with its route or hold, as data: tower-<date>.jsonl, one JSON line each.", open));
        }

        _planes.Children.Clear();
        _channel.Children.Clear();
        if (Server is not { } server)
        {
            _planes.Children.Add(Ui.Text("No tower running.", "sublabel"));
            return;
        }
        List<TowerPlane> planes;
        List<TowerMessage> lines;
        lock (server.Tower)
        {
            planes = server.Tower.Planes.Where(p => p.State != PlaneState.Gone).OrderBy(p => p.Number == 0 ? 99 : p.Number).ToList();
            lines = server.Tower.Channel.TakeLast(MaxLines).ToList();
        }
        if (planes.Count == 0)
            _planes.Children.Add(Ui.Text("No planes on the channel yet.", "sublabel"));
        foreach (var plane in planes)
            _planes.Children.Add(Ui.Row(plane.CallSign, Describe(plane), null).WithId($"tower/plane/{plane.CallSign}"));
        foreach (var m in lines)
        {
            var line = Ui.Text(string.Create(CultureInfo.InvariantCulture, $"{m.Time.ToLocalTime():HH:mm:ss}  {m.From} → {m.To}:  {m.Text}"),
                m.From == Tower.Name ? "label" : "sublabel");
            line.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
            _channel.Children.Add(line);
        }
        _channelScroll.ScrollToEnd();
    }

    private static string Describe(TowerPlane plane)
    {
        var state = plane.State switch
        {
            PlaneState.Approach => $"number 1, on the {plane.Approach}",
            PlaneState.Holding => $"number {plane.Number}, holding",
            PlaneState.Landed => "landed, vacating the runway",
            PlaneState.Local => $"local flight ({plane.Intent})",
            PlaneState.Asked => "asked for its intentions",
            _ => "new",
        };
        var r = plane.Last;
        return r == null ? state : string.Create(CultureInfo.InvariantCulture,
            $"{state} · {r.Situation.ToLowerInvariant()} · {r.Altitude:0} m · {r.Speed:0} m/s · heading {r.Heading:0}°");
    }
}
