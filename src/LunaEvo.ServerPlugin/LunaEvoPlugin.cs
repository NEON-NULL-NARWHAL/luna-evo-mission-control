// LUNA EVO · LunaEvo.ServerPlugin/LunaEvoPlugin.cs
// [Plugin] The LMP server plugin: watches position messages and writes LunaEvo/live.json for Mission Control's live map.
// Used by: LMP's server (Plugins folder, ILmpPlugin)
// Guide:   Meat and Potatoes/26-Server-Plugin.md

using HarmonyLib;
using LmpCommon.Locks;
using LmpCommon.Message.Server;
using LunaEvo.ClientPatches;
using LmpCommon.Message.Data.Vessel;
using LmpCommon.Message.Interface;
using Server.Client;
using Server.Context;
using Server.Plugin;
using Server.System;

namespace LunaEvo.ServerPlugin;

/// <summary>
/// It never blocks or sends a message and opens no network port. What it learns goes into one file next to the server,
/// &lt;server&gt;/LunaEvo/live.json, rewritten at most twice a second. Since 1.1 it also makes two server-side fixes from
/// audit 11 that change nothing in any message: 1 ms timers on Windows (P3) and a Lidgren channel per vessel for the
/// fast updates it relays (P1). Each can be switched off for the test rig (LUNAEVO_PATCHES_OFF).
/// </summary>
public sealed class LunaEvoPlugin : ILmpPlugin
{
    /// <summary>Written while something changes; otherwise at least this often, so a fresh file means "running".</summary>
    internal static readonly TimeSpan WriteInterval = TimeSpan.FromMilliseconds(500);
    internal static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(2);

    internal LiveState State { get; } = new();
    private string? _file;
    private DateTime _lastWrite = DateTime.MinValue;
    private long _writtenChanges = -1;
    private int _writing;

    internal static string Version => typeof(LunaEvoPlugin).Assembly.GetName().Version?.ToString(3) ?? "?";

    public void OnServerStart()
    {
        try
        {
            // ServerContext.UniverseDirectory is <server>/Universe; LUNA EVO keeps its files beside it.
            var server = Path.GetDirectoryName(ServerContext.UniverseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (server == null)
                return;
            var folder = Directory.CreateDirectory(Path.Combine(server, "LunaEvo")).FullName;
            _file = Path.Combine(folder, "live.json");
            State.Clear();
            var fixes = new List<string>();
            if (!PatchSwitches.IsOff("P3") && TimerResolution.Raise())
                fixes.Add("1 ms timers");
            if (!PatchSwitches.IsOff("P1") && ApplyChannelFix())
                fixes.Add("a channel per vessel");
            Server.Log.LunaLog.Normal($"LUNA EVO server plugin {Version}: writing live positions to LunaEvo/live.json" +
                                      (fixes.Count > 0 ? "; " + string.Join(", ", fixes) : ""));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _file = null; // nowhere to write: the plugin just does nothing
        }
    }

    private Harmony? _harmony;

    // P1: VesselSrvMsg puts every relayed position, flight state, update and resource message on channel 0.
    private bool ApplyChannelFix()
    {
        try
        {
            var target = AccessTools.PropertyGetter(typeof(VesselSrvMsg), "DefaultChannel");
            if (target == null)
                return false;
            _harmony = new Harmony("LunaEvo.ServerPlugin");
            _harmony.Patch(target, postfix: new HarmonyMethod(typeof(ServerChannelPatch), nameof(ServerChannelPatch.Postfix)));
            return true;
        }
        catch (Exception)
        {
            return false; // LMP's own channels: never worse than without the plugin
        }
    }

    public void OnServerStop()
    {
        TimerResolution.Lower();
        try
        {
            _harmony?.UnpatchAll("LunaEvo.ServerPlugin");
        }
        catch (Exception)
        {
        }

        // No file means "not running" to Mission Control, so a stopped server never looks live.
        try
        {
            if (_file != null)
                File.Delete(_file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    public void OnUpdate()
    {
        var now = DateTime.UtcNow;
        var changes = State.Changes;
        if (_file == null || now - _lastWrite < (changes != _writtenChanges ? WriteInterval : HeartbeatInterval))
            return;
        if (Interlocked.Exchange(ref _writing, 1) == 1)
            return; // the last write hasn't finished
        _lastWrite = now;
        _writtenChanges = changes;
        Func<byte[]> json;
        try
        {
            json = Snapshot(now);
        }
        catch (Exception)
        {
            // LMP's own collections change under us; the next tick tries again.
            Interlocked.Exchange(ref _writing, 0);
            return;
        }
        var file = _file;
        // Off LMP's main thread: a slow disk must never slow the server.
        Task.Run(() =>
        {
            try
            {
                var temp = file + ".tmp";
                File.WriteAllBytes(temp, json());
                File.Move(temp, file, overwrite: true);
            }
            catch (Exception)
            {
                // Mission Control may be reading it right now, or a player name LMP accepted can't be written: the next
                // write replaces it. Nothing here may escape into LMP.
            }
            finally
            {
                Interlocked.Exchange(ref _writing, 0);
            }
        });
    }

    public void OnClientConnect(ClientStructure client)
    {
    }

    public void OnClientAuthenticated(ClientStructure client)
    {
    }

    public void OnClientDisconnect(ClientStructure client)
    {
    }

    public void OnMessageReceived(ClientStructure client, IClientMessageBase messageData)
    {
        // LMP calls plugins before it checks the sender has logged in (Server/Server/MessageReceiver.cs), so a stranger
        // without the password could otherwise put made-up vessels on the map.
        if (client is { Authenticated: true })
            Observe(client.PlayerName, messageData?.Data, DateTime.UtcNow);
    }

    public void OnMessageSent(ClientStructure client, IServerMessageBase messageData)
    {
    }

    /// <summary>The part of <see cref="OnMessageReceived"/> that doesn't need a live connection (tests call it).</summary>
    internal void Observe(string player, IMessageData? data, DateTime nowUtc)
    {
        switch (data)
        {
            case VesselPositionMsgData p when p.LatLonAlt?.Length == 3 && p.Orbit?.Length == 8 && p.VelocityVector?.Length == 3:
                State.Position(new VesselState(p.VesselId, p.BodyIndex, p.BodyName ?? "", p.LatLonAlt[0], p.LatLonAlt[1], p.LatLonAlt[2],
                    (double[])p.Orbit.Clone(), (double[])p.VelocityVector.Clone(), p.GameTime, p.SubspaceId, p.Landed, p.Splashed, player, nowUtc));
                break;
            case VesselRemoveMsgData r:
                State.Removed(r.VesselId);
                break;
        }
    }

    /// <summary>
    /// Gathers LMP's players, locks and subspaces now (cheap, on LMP's main thread) and returns the JSON building, which
    /// runs on the background write with the vessel list (audit 11: hundreds of vessels took real time on the main thread).
    /// </summary>
    private Func<byte[]> Snapshot(DateTime nowUtc)
    {
        var controllers = new Dictionary<Guid, string>();
        foreach (var l in LockSystem.LockQuery.GetAllLocks())
            if (l.Type == LockType.Control && l.VesselId != Guid.Empty)
                controllers[l.VesselId] = l.PlayerName;
        var flying = controllers.GroupBy(c => c.Value).ToDictionary(g => g.Key, g => g.First().Key.ToString());
        var players = ServerContext.Clients.Values.Where(c => c.Authenticated)
            .Select(c => new PlayerState(c.PlayerName, c.Subspace, flying.GetValueOrDefault(c.PlayerName))).ToList();
        var subspaces = WarpContext.Subspaces.Values.Select(s => new SubspaceState(s.Id, s.Time)).ToList();
        var lmp = typeof(ServerContext).Assembly.GetName().Version?.ToString(3) ?? "?";
        return () => State.ToJson(Version, lmp, nowUtc, players, subspaces, controllers);
    }
}

/// <summary>The server leg of P1: the relayed copy of a fast vessel update goes on that vessel's own channel too.</summary>
internal static class ServerChannelPatch
{
    internal static void Postfix(VesselSrvMsg __instance, ref int __result)
    {
        if (__result == 0 && __instance?.Data is VesselBaseMsgData data)
            __result = LmpChannels.ForVessel(data.VesselId, data.SubType);
    }
}
