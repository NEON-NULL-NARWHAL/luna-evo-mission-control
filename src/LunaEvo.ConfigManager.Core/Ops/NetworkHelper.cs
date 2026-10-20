// LUNA EVO · LunaEvo.ConfigManager.Core/Ops/NetworkHelper.cs
// [Core] "How do my friends connect?": this PC's addresses, LMP's detected public address, the Windows Firewall rule.
// Used by: ConnectPage
// Guide:   Meat and Potatoes/13-Automation.md

using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using LunaEvo.ConfigManager.Core.Server;

namespace LunaEvo.ConfigManager.Core.Ops;

public sealed record LocalAddress(string Address, string Adapter, bool IsPrivate);

public static partial class NetworkHelper
{
    /// <summary>
    /// netsh from System32, never from PATH: adding the firewall rule runs it as administrator, so a planted
    /// "netsh.exe" earlier on the PATH (or in the current folder) must not be the one that gets elevated.
    /// </summary>
    public static string NetshPath { get; } = Path.Combine(Environment.SystemDirectory, "netsh.exe");

    /// <summary>
    /// IPv4 addresses of this PC's active network adapters (Ethernet, Wi-Fi, and VPN/mesh adapters such as
    /// Tailscale or ZeroTier), without loopback. Friends on the same network or mesh connect to one of these.
    /// </summary>
    public static IReadOnlyList<LocalAddress> LocalAddresses()
    {
        var list = new List<LocalAddress>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;
            foreach (var unicast in adapter.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(unicast.Address))
                    continue;
                var bytes = unicast.Address.GetAddressBytes();
                if (bytes[0] == 169 && bytes[1] == 254)
                    continue; // link-local "no DHCP" address, useless to friends
                list.Add(new LocalAddress(unicast.Address.ToString(), adapter.Name, IsPrivate(unicast.Address)));
            }
        }
        return list;
    }

    /// <summary>RFC 1918 private ranges, plus 100.64/10 (carrier-grade NAT, also used by Tailscale).</summary>
    public static bool IsPrivate(IPAddress address)
    {
        var b = address.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 100 && b[1] is >= 64 and <= 127);
    }

    /// <summary>LMP logs "Detected NAT addresses: 1.2.3.4:8800" (Debug level) once it knows the public address.</summary>
    public static string? PublicAddressFrom(LogLine line)
    {
        var match = NatRegex().Match(line.Message);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>True if something on this PC is listening on that UDP port (the game port, once the server is up).</summary>
    public static bool IsUdpPortListening(int port) =>
        IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().Any(e => e.Port == port);

    /// <summary>The name LUNA EVO gives its firewall rule, so it can find it again.</summary>
    public static string FirewallRuleName(int port, string protocol = "UDP") =>
        protocol == "UDP" ? $"LUNA EVO - LMP server (UDP {port})" : $"LUNA EVO - LMP live info ({protocol} {port})";

    /// <summary>Arguments for netsh to add an inbound allow rule for the game port. Shown to the user before running.</summary>
    public static IReadOnlyList<string> FirewallAddArguments(int port, string protocol = "UDP") =>
        ["advfirewall", "firewall", "add", "rule", $"name={FirewallRuleName(port, Protocol(protocol))}", "dir=in", "action=allow", $"protocol={Protocol(protocol)}", $"localport={port}"];

    // Only these two ever reach netsh.
    private static string Protocol(string protocol) => protocol.Equals("TCP", StringComparison.OrdinalIgnoreCase) ? "TCP" : "UDP";

    /// <summary>True if our rule exists. Reading rules doesn't need administrator rights.</summary>
    public static bool FirewallRuleExists(int port, string protocol = "UDP")
    {
        try
        {
            var info = new ProcessStartInfo(NetshPath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            foreach (var argument in new[] { "advfirewall", "firewall", "show", "rule", $"name={FirewallRuleName(port, Protocol(protocol))}" })
                info.ArgumentList.Add(argument);
            using var process = Process.Start(info);
            if (process == null)
                return false;
            process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Adds the rule. Windows asks the user for permission (UAC), because changing the firewall needs admin rights.
    /// Returns false if they said no or it failed.
    /// </summary>
    public static async Task<bool> AddFirewallRuleAsync(int port, string protocol = "UDP")
    {
        try
        {
            var info = new ProcessStartInfo(NetshPath) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
            foreach (var argument in FirewallAddArguments(port, protocol))
                info.ArgumentList.Add(argument);
            using var process = Process.Start(info);
            if (process == null)
                return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false; // the user declined the permission prompt
        }
    }

    [GeneratedRegex(@"Detected NAT addresses?:\s*([0-9a-fA-F\.:\[\]]+:\d+)")]
    private static partial Regex NatRegex();
}
