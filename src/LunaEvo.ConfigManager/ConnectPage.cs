// LUNA EVO · LunaEvo.ConfigManager/ConnectPage.cs
// [UI] "How do my friends connect?": addresses to share, server list status, firewall rule, port check.
// Used by: MainWindow
// Guide:   Meat and Potatoes/13-Automation.md

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using LunaEvo.ConfigManager.Core.Ops;
using LunaEvo.ConfigManager.Core.Validation;

namespace LunaEvo.ConfigManager;

internal sealed class ConnectPage
{
    private readonly MainWindow _host;
    private readonly StackPanel _addresses = new() { Spacing = 2 };
    private readonly StackPanel _status = new() { Spacing = 2 };
    private bool _showPublic;

    public ConnectPage(MainWindow host)
    {
        _host = host;
        var refresh = Ui.Button("Refresh", "text", "IconRestart").WithId("connect/refresh");
        refresh.Click += (_, _) => Refresh();
        var header = new DockPanel();
        DockPanel.SetDock(refresh, Dock.Right);
        header.Children.Add(refresh);
        header.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Ui.Text("Connect", "headline"),
                Ui.Text("What to tell your friends, and what stands between them and your server.", "sublabel"),
            },
        });

        var (addresses, addressBody, _) = Ui.Group("Addresses to share");
        addressBody.Children.Add(_addresses);
        var (status, statusBody, _) = Ui.Group("Can they reach it?");
        statusBody.Children.Add(_status);

        View = new ScrollViewer
        {
            Content = new StackPanel { Margin = new Thickness(28, 20, 28, 28), Spacing = 16, Children = { header, addresses, status } },
        };
    }

    public Control View { get; }

    public void Refresh()
    {
        _addresses.Children.Clear();
        _status.Children.Clear();
        if (_host.Session is not { } session)
        {
            _addresses.Children.Add(Ui.Row("Open a server first", null, null));
            return;
        }

        var portText = session.GetValue(LmpRules.Connection, "Port") ?? "8800";
        var port = int.TryParse(portText, out var p) ? p : 8800;

        foreach (var address in NetworkHelper.LocalAddresses())
        {
            var description = address.IsPrivate
                ? $"{address.Adapter} · for players on the same network, VPN or mesh (Tailscale, ZeroTier...)"
                : $"{address.Adapter} · a public address on this PC";
            _addresses.Children.Add(Ui.Row($"{address.Address}:{port}", description, CopyButton($"{address.Address}:{port}")).WithId("connect/address/" + address.Address));
        }
        if (_addresses.Children.Count == 0)
            _addresses.Children.Add(Ui.Row("No network adapters are up", null, null));

        var publicAddress = _host.Server?.PublicAddress;
        Control? publicControl = null;
        if (publicAddress != null)
        {
            var toggle = Ui.Button(_showPublic ? "Hide" : "Show", "text").WithId("connect/showPublic");
            toggle.Click += (_, _) =>
            {
                _showPublic = !_showPublic;
                Refresh();
            };
            publicControl = new StackPanel { Orientation = Orientation.Horizontal, Children = { toggle, CopyButton(publicAddress) } };
        }
        _addresses.Children.Add(Ui.Row(
            publicAddress == null ? "Internet address: not known yet" : _showPublic ? publicAddress : "Internet address: hidden",
            publicAddress == null
                ? "LMP works this out a few seconds after it starts (it logs it at Debug level). Hidden by default so it doesn't end up in screenshots."
                : "For players outside your network. They also need the port to be open on your router (or UPnP).",
            publicControl).WithId("connect/public"));

        var listed = session.GetValue("MasterServerSettings.xml", "RegisterWithMasterServer") == "true";
        _status.Children.Add(Ui.Row(listed ? "Listed on the public server list" : "Not on the public server list",
            listed ? "Anyone can find it in LMP's in-game server browser (a password keeps strangers out)." : "Friends join with an address above. Change it in Master Server settings.", null));

        var upnp = session.GetValue(LmpRules.Connection, "Upnp") == "true";
        _status.Children.Add(Ui.Row(upnp ? "UPnP is on" : "UPnP is off",
            upnp ? "LMP asks your router to open the port by itself (if the router allows it)." : "For players outside your network, forward UDP port " + port + " on your router, or turn UPnP on in Connection settings.", null));

        var firewall = NetworkHelper.FirewallRuleExists(port);
        Control? firewallButton = null;
        if (!firewall)
        {
            var add = Ui.Button("Allow through firewall", "text").WithId("connect/firewall");
            ToolTip.SetTip(add, "Runs: netsh " + string.Join(" ", NetworkHelper.FirewallAddArguments(port)));
            add.Click += async (_, _) =>
            {
                var ok = await NetworkHelper.AddFirewallRuleAsync(port);
                await Dialogs.InfoAsync(_host, ok ? "Firewall rule added" : "Firewall unchanged",
                    ok ? $"Windows Firewall now allows UDP port {port} in." : "The rule wasn't added (permission was declined, or netsh failed).");
                Refresh();
            };
            firewallButton = add;
        }
        _status.Children.Add(Ui.Row(firewall ? $"Windows Firewall allows UDP {port}" : $"No LUNA EVO firewall rule for UDP {port}",
            firewall ? "Added by LUNA EVO." : "Windows may block players on other PCs. Adding the rule needs administrator permission; Windows will ask.",
            firewallButton).WithId("connect/firewallRow"));

        // Live info for players on the Join page: LMP's website must listen on the network, not just this PC.
        var websiteOn = session.GetValue(LmpRules.Website, "EnableWebsite") == "true";
        var listen = session.GetValue(LmpRules.Website, "ListenAddress")?.Trim() ?? "";
        var websitePort = int.TryParse(session.GetValue(LmpRules.Website, "Port"), out var wp) ? wp : 8900;
        var shared = websiteOn && listen is not ("127.0.0.1" or "::1" or "localhost");
        Control? shareControl;
        if (shared)
        {
            var stop = Ui.Button("Stop sharing", "text").WithId("connect/stopSharing");
            stop.Click += (_, _) =>
            {
                session.SetValue(LmpRules.Website, "ListenAddress", "127.0.0.1");
                _host.RefreshAll();
                Refresh();
            };
            var allow = Ui.Button("Allow through firewall", "text").WithId("connect/websiteFirewall");
            ToolTip.SetTip(allow, "Runs: netsh " + string.Join(" ", NetworkHelper.FirewallAddArguments(websitePort, "TCP")));
            allow.Click += async (_, _) =>
            {
                var ok = await NetworkHelper.AddFirewallRuleAsync(websitePort, "TCP");
                await Dialogs.InfoAsync(_host, ok ? "Firewall rule added" : "Firewall unchanged",
                    ok ? $"Windows Firewall now allows TCP port {websitePort} in." : "The rule wasn't added (permission was declined, or netsh failed).");
                Refresh();
            };
            shareControl = NetworkHelper.FirewallRuleExists(websitePort, "TCP")
                ? stop
                : new StackPanel { Orientation = Orientation.Horizontal, Children = { allow, stop } };
        }
        else
        {
            var share = Ui.Button("Share with players", "text").WithId("connect/share");
            share.Click += async (_, _) =>
            {
                if (!await Dialogs.ConfirmAsync(_host, "Share live info with players?",
                        "Players on your network will be able to see the server's name, settings (never passwords), who's online and " +
                        "where vessels are, on LUNA EVO's Join page. LMP's website listens on every network address for that, so only do it " +
                        "on a network you trust (home, VPN or mesh). Save and restart the server afterwards.", "Share"))
                    return;
                session.SetValue(LmpRules.Website, "EnableWebsite", "true");
                session.SetValue(LmpRules.Website, "ListenAddress", ""); // empty = the game's listen address (all networks)
                _host.RefreshAll();
                Refresh();
            };
            shareControl = share;
        }
        _status.Children.Add(Ui.Row(
            shared ? $"Sharing live info on TCP {websitePort}" : "Live info is only visible on this PC",
            shared
                ? "Players see server info, players and vessels on their Join page. Save and restart the server if you just changed this."
                : "Players' Join page can still ping you, but can't show server info, players or vessels.",
            shareControl).WithId("connect/shareRow"));

        var running = _host.Server?.IsRunning == true;
        var listening = running && NetworkHelper.IsUdpPortListening(port);
        _status.Children.Add(Ui.Row(!running ? "Server is stopped" : listening ? $"Listening on UDP {port}" : $"Nothing is listening on UDP {port}",
            !running ? "Start it on the Server Console page." : listening ? "The server is ready for players." : "The server may still be starting, or another program has the port.", null)
            .WithId("connect/listening"));
    }

    private Button CopyButton(string text)
    {
        var copy = Ui.Button("Copy", "text");
        copy.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(View)?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(text);
        };
        return copy;
    }
}
