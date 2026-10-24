// LUNA EVO · LunaEvo.MissionControl/WingmanPage.cs
// [UI] The Wingman page: AI pilots (KSP Wingman agents) started from LUNA EVO, one per KSP (kRPC port), each with its
//      phase, its flights, the tower's messages, its tool calls and answers, and a box for orders.
// Used by: MainWindow (rail: Play → Wingman, once KSP Wingman is installed on LUNA EVO patches → Tools)
// Guide:   Meat and Potatoes/29-Wingman.md

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using LunaEvo.MissionControl.Core.Wingman;

namespace LunaEvo.MissionControl;

/// <summary>
/// Each agent is KSP Wingman's console run with --ipc: LUNA EVO writes orders to it and reads one JSON event a line
/// back (AgentHost). The agents run on this PC next to their KSPs and the model server; nothing goes on the network
/// except to the model server's address and the tower on localhost.
/// </summary>
internal sealed class WingmanPage
{
    /// <summary>Events shown on a card, newest last (the agent keeps more).</summary>
    public const int ShownEvents = 40;

    private readonly MainWindow _host;
    private readonly StackPanel _setup = new() { Spacing = 2 };
    private readonly StackPanel _agents = new() { Spacing = 12 };
    private readonly TextBox _callSign = new TextBox { Text = "Wingman1", Width = 160 }.WithId("wingman/callSign");
    private readonly TextBox _port = new TextBox { Text = "50000", Width = 100 }.WithId("wingman/port");
    private readonly TextBox _model = new TextBox { Text = WingmanTools.DefaultModel, Width = 240 }.WithId("wingman/model");
    private readonly TextBox _llm = new TextBox { Text = new Uri(new Uri(WingmanTools.OllamaUrl), "v1/").ToString(), Width = 280 }.WithId("wingman/llm");
    private readonly CheckBox _tower = new CheckBox { Content = "Use the KSC tower", IsChecked = true }.WithId("wingman/useTower");
    private readonly TextBlock _result = Ui.Text("", "sublabel").WithId("wingman/result");
    private readonly TextBlock _none = Ui.Text("No agents yet.", "sublabel").WithId("wingman/none");
    private readonly Dictionary<WingmanAgent, AgentCard> _cards = [];
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(15) };
    private AgentHost _agentHost = new();
    private bool _refreshQueued;

    public WingmanPage(MainWindow host)
    {
        _host = host;
        var header = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Ui.Text("Wingman", "headline"),
                Ui.Text("AI pilots for your KSPs. Each agent flies one KSP through kRPC with a local model (Ollama), takes orders " +
                        "in plain words (\"take off and fly east at 1000 m\", \"land at KSC\"), and works with the KSC tower when it's on.", "sublabel"),
            },
        };
        _result.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        var start = Ui.Button("Start the agent", "fab").WithId("wingman/start");
        start.Click += (_, _) => StartAgent();
        var form = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var (label, box) in new (string, Control)[] { ("Call sign", _callSign), ("kRPC port", _port), ("Model", _model), ("Model server", _llm) })
            form.Children.Add(new StackPanel { Margin = new Thickness(0, 0, 12, 8), Spacing = 2, Children = { Ui.Text(label, "sublabel"), box } });
        var (setup, setupBody, _) = Ui.Group("Start an agent");
        setupBody.Children.Add(_setup);
        setupBody.Children.Add(form);
        setupBody.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { start, _tower } });
        var (agents, agentsBody, _) = Ui.Group("Agents");
        agentsBody.Children.Add(_none);
        agentsBody.Children.Add(_agents);
        View = new ScrollViewer
        {
            Content = new StackPanel { Margin = new Thickness(28, 20, 28, 28), Spacing = 16, Children = { header, _result, setup, agents } },
        };
        Host = _agentHost;
        // Their planes' numbers now and then (the status tool, no model involved), while LUNA EVO is open.
        _statusTimer.Tick += (_, _) =>
        {
            foreach (var agent in _agentHost.Agents.Where(a => a.Running))
                agent.AskStatus();
        };
        _statusTimer.Start();
    }

    public Control View { get; }

    /// <summary>Starts and keeps the agents' processes. Tests put in one with fake processes.</summary>
    internal AgentHost Host
    {
        get => _agentHost;
        set
        {
            _agentHost = value;
            _agentHost.Changed += QueueRefresh;
            _cards.Clear();
            _agents.Children.Clear();
            _none.IsVisible = true;
        }
    }

    internal string LastMessage => _result.Text ?? "";

    /// <summary>The command line's --wingman-agent CallSign:port[:model]: start agents at launch (rig tests).</summary>
    public void StartFromCommandLine(IEnumerable<string> specs)
    {
        foreach (var spec in specs)
        {
            var parts = spec.Split(':', 3);
            _callSign.Text = parts[0];
            if (parts.Length > 1)
                _port.Text = parts[1];
            if (parts.Length > 2)
                _model.Text = parts[2];
            StartAgent();
        }
    }

    internal void StartAgent()
    {
        if (WingmanTools.Installed() is not { } install)
        {
            _result.Text = "KSP Wingman isn't installed: LUNA EVO patches → Tools.";
            return;
        }
        if (!int.TryParse(_port.Text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var port))
        {
            _result.Text = "The kRPC port is a number (kRPC's default is 50000).";
            return;
        }
        var tower = _tower.IsChecked == true && _host.Tower.Server != null ? $"http://localhost:{_host.Tower.Server.Port}/" : null;
        var start = new AgentStart(_callSign.Text?.Trim() ?? "", port, _model.Text?.Trim() ?? "", _llm.Text?.Trim() ?? "", tower);
        if (_agentHost.Check(start) is { } problem)
        {
            _result.Text = problem;
            return;
        }
        try
        {
            _agentHost.Start(install, start);
            _result.Text = $"{start.CallSign} started on kRPC port {port} with {start.Model}" + (tower != null ? ", with the tower." : ".");
            if (int.TryParse(start.CallSign[^1..], out var n) && n < 9) // ready for the next one
                _callSign.Text = start.CallSign[..^1] + (n + 1).ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            _result.Text = "Couldn't start the agent: " + e.Message;
        }
        Refresh();
    }

    private void QueueRefresh()
    {
        // Agents can say a lot at once: one refresh for a burst.
        if (_refreshQueued)
            return;
        _refreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshQueued = false;
            Refresh();
        }, DispatcherPriority.Background);
    }

    public void Refresh()
    {
        _setup.Children.Clear();
        var install = WingmanTools.Installed();
        var towerOn = _host.Tower.Server != null;
        _setup.Children.Add(Ui.Row("KSP Wingman", install == null
            ? "Not installed: LUNA EVO patches → Tools installs it, its mod for your KSP and a model."
            : $"{install.Version} · each KSP needs kRPC and Wingman's mod, its kRPC server running (on its own port), and a game loaded. " +
              (towerOn ? "The tower is on." : "The tower is off (Play → KSC tower)."), null).WithId("wingman/status"));

        foreach (var gone in _cards.Keys.Except(_agentHost.Agents).ToList())
        {
            _agents.Children.Remove(_cards[gone].View);
            _cards.Remove(gone);
        }
        foreach (var agent in _agentHost.Agents)
        {
            if (!_cards.TryGetValue(agent, out var card))
            {
                card = new AgentCard(this, agent);
                _cards[agent] = card;
                _agents.Children.Add(card.View);
            }
            card.Update();
        }
        _none.IsVisible = _agentHost.Agents.Count == 0;
    }

    /// <summary>Stops every agent (LUNA EVO closing). Their planes keep their last orders.</summary>
    public void StopAll()
    {
        _statusTimer.Stop();
        _agentHost.QuitAll(TimeSpan.FromSeconds(3));
        _agentHost.Dispose();
    }

    private sealed class AgentCard
    {
        private readonly WingmanPage _page;
        private readonly WingmanAgent _agent;
        private readonly TextBlock _summary;
        private readonly TextBlock _status;
        private readonly StackPanel _events = new() { Spacing = 1 };
        private readonly ScrollViewer _scroll;
        private readonly TextBox _order;
        private readonly Button _stop;
        private int _shown = -1;

        public AgentCard(WingmanPage page, WingmanAgent agent)
        {
            _page = page;
            _agent = agent;
            var id = $"wingman/agent/{agent.Start.CallSign}";
            _summary = Ui.Text("", "sublabel").WithId(id + "/summary");
            _status = Ui.Text("", "sublabel").WithId(id + "/plane");
            _status.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
            _scroll = new ScrollViewer { Content = _events, MaxHeight = 260 }.WithId(id + "/events");
            _order = new TextBox { PlaceholderText = "An order, e.g. \"take off and fly east at 1000 m\" or \"land at KSC\"", MinWidth = 420 }.WithId(id + "/order");
            var send = Ui.Button("Send", "fab").WithId(id + "/send");
            send.Click += (_, _) => Send();
            _order.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                    Send();
            };
            var status = Ui.Button("Status", "text").WithId(id + "/askStatus");
            status.Click += (_, _) => _agent.AskStatus();
            _stop = Ui.Button("Stop", "text").WithId(id + "/stop");
            _stop.Click += async (_, _) =>
            {
                if (_agent.Running)
                    await _agent.StopAsync();
                else
                    _page._agentHost.Remove(_agent);
                _page.Refresh();
            };
            View = new Border
            {
                Classes = { "row" },
                Child = new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new DockPanel { Children = { Dock(_stop, Avalonia.Controls.Dock.Right), Dock(status, Avalonia.Controls.Dock.Right), Ui.Text(agent.Start.CallSign, "label") } },
                        _summary,
                        _status,
                        _scroll,
                        new DockPanel { Children = { Dock(send, Avalonia.Controls.Dock.Right), _order } },
                    },
                },
            }.WithId(id);
        }

        public Control View { get; }

        private static Control Dock(Control c, Dock dock)
        {
            DockPanel.SetDock(c, dock);
            c.Margin = new Thickness(6, 0, 0, 0);
            return c;
        }

        private void Send()
        {
            var text = _order.Text?.Trim() ?? "";
            if (text.Length == 0)
                return;
            _agent.Order(text);
            _order.Text = "";
        }

        public void Update()
        {
            var s = _agent.Start;
            _summary.Text = $"{(_agent.Running ? Ui.Humanize(_agent.Phase).ToLowerInvariant() : "stopped")} · kRPC port {s.KrpcPort} · {s.Model}" +
                            (s.TowerUrl != null ? " · with the tower" : "") + (_agent.LastOutcome is { } last ? " · last flight: " + last : "");
            _status.Text = _agent.Status ?? "";
            _status.IsVisible = _agent.Status != null;
            _stop.Content = Ui.IconAndText(_agent.Running ? "Stop" : "Remove", null);
            _order.IsEnabled = _agent.Running;
            var events = _agent.Events;
            if (events.Count == _shown)
                return;
            _shown = events.Count;
            _events.Children.Clear();
            foreach (var e in events.TakeLast(ShownEvents))
            {
                var line = Ui.Text(string.Create(CultureInfo.InvariantCulture, $"{e.Time.ToLocalTime():HH:mm:ss}  {Label(e.Kind)}  {e.Text}"),
                    e.Kind is "answer" or "flight" or "error" ? "label" : "sublabel");
                line.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
                _events.Children.Add(line);
            }
            _scroll.ScrollToEnd();
        }

        private static string Label(string kind) => kind switch
        {
            "order" => "you",
            "chat" => "chat",
            "call" => "tool",
            "result" => "  →",
            "answer" => "ai",
            "flight" => "flight",
            "tower" => "tower",
            "phase" => "phase",
            "error" => "error",
            _ => "·",
        };
    }
}
