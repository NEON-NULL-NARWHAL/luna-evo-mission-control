// LUNA EVO · LunaEvo.MissionControl/WingmanPage.cs
// [UI] The Wingman page: AI pilots (KSP Wingman agents) started from LUNA EVO, one per KSP (kRPC port), each with its
//      phase, its flights, the tower's messages, its tool calls and answers, a box for orders and free flight; and
//      what the server's agents know (skills from their flights, lessons), kept by the tower.
// Used by: MainWindow (rail: Play → Wingman, once KSP Wingman is installed on LUNA EVO patches → Tools)
// Guide:   Meat and Potatoes/29-Wingman.md, 30-Knowledge.md

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using LunaEvo.MissionControl.Core.Tower;
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
    private readonly TextBox _planner = new TextBox { PlaceholderText = "e.g. gemma4:e4b (recommended)", Width = 260 }.WithId("wingman/planner");
    private readonly CheckBox _tower = new CheckBox { Content = "Use the KSC tower", IsChecked = true }.WithId("wingman/useTower");
    private readonly StackPanel _knowledge = new() { Spacing = 2 };
    private readonly TextBlock _result = Ui.Text("", "sublabel").WithId("wingman/result");
    private readonly TextBlock _none = Ui.Text("No agents yet.", "sublabel").WithId("wingman/none");
    private readonly Dictionary<WingmanAgent, AgentCard> _cards = [];
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(15) };
    private AgentHost _agentHost;
    private bool _refreshQueued;

    /// <param name="logFolder">Where each agent's session is recorded (LUNA EVO's logs folder), or null.</param>
    public WingmanPage(MainWindow host, string? logFolder = null)
    {
        _host = host;
        _agentHost = new AgentHost(logFolder: logFolder);
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
        foreach (var (label, box) in new (string, Control)[] { ("Call sign", _callSign), ("kRPC port", _port), ("Model", _model), ("Model server", _llm), ("Planner (thinks)", _planner) })
            form.Children.Add(new StackPanel { Margin = new Thickness(0, 0, 12, 8), Spacing = 2, Children = { Ui.Text(label, "sublabel"), box } });
        var (setup, setupBody, _) = Ui.Group("Start an agent");
        setupBody.Children.Add(_setup);
        setupBody.Children.Add(form);
        setupBody.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { start, _tower } });
        var (agents, agentsBody, _) = Ui.Group("Agents");
        agentsBody.Children.Add(_none);
        agentsBody.Children.Add(_agents);
        var (knowledge, knowledgeBody, _) = Ui.Group("What the agents know");
        knowledgeBody.Children.Add(_knowledge);
        View = new ScrollViewer
        {
            Content = new StackPanel { Margin = new Thickness(28, 20, 28, 28), Spacing = 16, Children = { header, _result, setup, agents, knowledge } },
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

    /// <summary>
    /// The command line's --wingman-agent CallSign:port[:model] (and --wingman-planner, --wingman-free minutes): start
    /// agents at launch, and their free flight once they're connected (rig tests).
    /// </summary>
    public void StartFromCommandLine(IEnumerable<string> specs, string? planner = null, double? freeMinutes = null, IReadOnlyList<string>? train = null)
    {
        if (planner != null)
            _planner.Text = planner;
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
        if (freeMinutes != null || train is { Count: > 0 })
            _ = Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(_ => Dispatcher.UIThread.Post(() =>
            {
                var running = _agentHost.Agents.Where(a => a.Running).ToList();
                for (var i = 0; i < running.Count; i++)
                    if (train is { Count: > 0 } && i < train.Count)
                        running[i].Train(train[i], freeMinutes ?? 30);
                    else if (freeMinutes is { } minutes)
                        running[i].FreeFlight(minutes);
            }), TaskScheduler.Default);
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
        var start = new AgentStart(_callSign.Text?.Trim() ?? "", port, _model.Text?.Trim() ?? "", _llm.Text?.Trim() ?? "", tower,
            _planner.Text?.Trim() is { Length: > 0 } planner ? planner : null);
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
        RefreshKnowledge();
    }

    /// <summary>The tower's knowledge store: the skill tree with everyone's numbers, and the lessons (the host can delete them).</summary>
    private void RefreshKnowledge()
    {
        _knowledge.Children.Clear();
        if (_host.Tower.Server is not { } server)
        {
            _knowledge.Children.Add(Ui.Text("Start the KSC tower (Play → KSC tower): it keeps what the agents learn, on the open server.", "sublabel").WithId("wingman/knowledge/off"));
            return;
        }
        lock (server.Knowledge)
        {
            var k = server.Knowledge;
            _knowledge.Children.Add(Ui.Row("Kept in", (server.KnowledgePath ?? "memory only") + $" · {k.Flights.Count} flight(s), {k.Lessons.Count} lesson(s). " +
                "Each finished flight grades the skills it used; a lesson must cite a flight that showed it.", null).WithId("wingman/knowledge/where"));
            foreach (var skill in Knowledge.Tree)
            {
                var all = k.Of(skill.Id);
                var open = k.Unlocked(skill.Id);
                _knowledge.Children.Add(Ui.Row(skill.Name, $"{skill.What} · {all}" +
                    (open ? "" : $" · locked until {string.Join(" and ", skill.Requires)} have {Knowledge.UnlockAfter} good flights"), null).WithId($"wingman/skill/{skill.Id}"));
            }
            // The planes (the owner's idea): what each has done, what's still to learn about it, and its handling notes.
            foreach (var craft in k.Crafts())
            {
                var open = craft.ToLearn.Where(i => !i.Done).Select(i => i.What).ToList();
                var facts = craft.ToLearn.Where(i => i.Note.Length > 0).Select(i => i.Note);
                _knowledge.Children.Add(Ui.Row("✈ " + craft.Name,
                    $"{craft.Flights} flight(s) by {string.Join(", ", craft.FlownBy)} · {craft.Landed} landed, {craft.Crashed} crashed · " +
                    string.Join(" · ", facts) + (open.Count > 0 ? " · still to learn: " + string.Join(", ", open).ToLowerInvariant() : " · nothing left on its list") +
                    string.Concat(craft.Notes.Select(n => $"\n• {n.Text}")), null).WithId($"wingman/craft/{craft.Name}"));
            }
            foreach (var lesson in k.Lessons.OrderBy(l => l.Skill).ThenByDescending(l => l.Weight))
            {
                var delete = Ui.Button("Delete", "text").WithId($"wingman/lesson/{lesson.Id}/delete");
                var id = lesson.Id;
                delete.Click += (_, _) =>
                {
                    server.DeleteLesson(id);
                    Refresh();
                };
                _knowledge.Children.Add(Ui.Row(lesson.Text, $"{lesson.Skill} · from {lesson.Author} · {lesson.Evidence.Count} flight(s)" +
                    (lesson.Confirmations > 0 ? $", confirmed {lesson.Confirmations}×" : ""), delete).WithId($"wingman/lesson/{lesson.Id}"));
            }
        }
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
        private readonly Button _free;
        private readonly ComboBox _training;
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
            _free = Ui.Button("Free flight", "text").WithId(id + "/free");
            _free.Click += (_, _) =>
            {
                if (_agent.FreeFlying)
                    _agent.StopFreeFlight();
                else
                    _agent.FreeFlight(30);
            };
            ToolTip.SetTip(_free, "30 minutes of practice on its own: it picks what to practise (its weakest landings first), flies it, and learns from each flight. Any order stops it.");
            _training = new ComboBox { ItemsSource = WingmanAgent.Trainings.Select(t => t.Label).ToList(), SelectedIndex = 0, MinWidth = 170 }.WithId(id + "/training");
            var train = Ui.Button("Train", "text").WithId(id + "/train");
            ToolTip.SetTip(train, "Practise one ability on its own until it goes well (3 good flights in a row) or 30 minutes are up, learning as it goes. Any order stops it.");
            train.Click += (_, _) => _agent.Train(WingmanAgent.Trainings[Math.Max(0, _training.SelectedIndex)].Focus);
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
                        new DockPanel { Children = { Dock(_stop, Avalonia.Controls.Dock.Right), Dock(status, Avalonia.Controls.Dock.Right), Dock(_free, Avalonia.Controls.Dock.Right), Dock(train, Avalonia.Controls.Dock.Right), Dock(_training, Avalonia.Controls.Dock.Right), Ui.Text(agent.Start.CallSign, "label") } },
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
            _free.Content = Ui.IconAndText(_agent.FreeFlying ? "Stop free flight" : "Free flight", null);
            _free.IsEnabled = _agent.Running;
            _summary.Text = $"{(_agent.Running ? Ui.Humanize(_agent.Phase).ToLowerInvariant() : "stopped")}{(_agent.FreeFlying ? " · free flight" : "")} · kRPC port {s.KrpcPort} · {s.Model}" +
                            (s.PlannerModel != null ? $" (planner {s.PlannerModel})" : "") +
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
                    e.Kind is "answer" or "flight" or "error" or "debrief" ? "label" : "sublabel");
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
            "free" => "free",
            "debrief" or "lesson" or "knowledge" => "learn",
            "error" => "error",
            _ => "·",
        };
    }
}
