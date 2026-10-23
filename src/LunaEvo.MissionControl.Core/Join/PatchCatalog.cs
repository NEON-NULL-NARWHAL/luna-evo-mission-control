// LUNA EVO · LunaEvo.MissionControl.Core/Join/PatchCatalog.cs
// [Core] Every LUNA EVO fix for LMP, where it runs, what it does and what it measured: the LUNA EVO patches page lists them.
// Used by: PatchesPage
// Guide:   Meat and Potatoes/27-Client-Patches.md

namespace LunaEvo.MissionControl.Core.Join;

/// <summary>Where a fix runs: in the player's KSP (the client patches) or in the host's LMP server (the server plugin).</summary>
public enum PatchPlace
{
    Ksp,
    Server,
}

/// <summary>One fix: its name, what it does for the player, and what the test rig measured (or "" if nothing to measure).</summary>
public sealed record PatchInfo(PatchPlace Place, string Name, string What, string Measured, string Since);

public static class PatchCatalog
{
    /// <summary>All of them, in the order a player would care about. Keep in step with Meat and Potatoes 26 and 27.</summary>
    public static IReadOnlyList<PatchInfo> All { get; } =
    [
        new(PatchPlace.Ksp, "Join any server by itself",
            "Launch KSP and join connects to the server on any port, with the password from LMP's own server list. LMP's own " +
            "option stops LMP starting up when the address has a port.",
            "Checked in a real game: a password server on another port joined by itself", "2.5"),
        new(PatchPlace.Ksp, "Stay in step with other players",
            "When another player is up to a minute ahead and you aren't warping, your clock catches up by itself, so you keep " +
            "seeing each other. A bigger gap (someone warped on purpose) asks first. Switch it in the LUNA EVO window in KSP.",
            "Two real games: the player behind synced by itself", "2.9.1"),
        new(PatchPlace.Ksp, "No time jumps when you're alone",
            "Alone on the server, a game that falls behind gets a time of its own instead of LMP's jump, which teleports your " +
            "vessel along its orbit and cuts its engines. With anyone else online, LMP's own correction keeps you together.",
            "A slow game: 11 jumps a minute without, none with", "2.6"),
        new(PatchPlace.Ksp, "Positions with the right time",
            "Every position LMP sends was stamped one physics tick early, so other players saw you slightly in the wrong place.",
            "Stamp error 20 ms → 0", "2.6"),
        new(PatchPlace.Ksp, "Fewer lost vessel updates",
            "LMP sent every vessel's updates on one network channel, where an update that arrives late is thrown away if another " +
            "vessel's came first. Now each vessel has its own.",
            "20-120 ms of jitter: 63% of updates lost → 18%", "2.7"),
        new(PatchPlace.Ksp, "Less lag",
            "LMP's network waits in steps Windows rounds up to 15.6 ms; 1 ms timers make every hop quicker.",
            "One game to another: 29 → 22 ms (worst 82 → 46 ms)", "2.7"),
        new(PatchPlace.Ksp, "A LUNA EVO button in KSP",
            "A window on KSP's toolbar: your connection and ping, who's online and what they fly, a clock sync, watching a " +
            "nearby player's vessel, and LMP's settings (color, chat, options) in one place.",
            "", "2.8"),
        new(PatchPlace.Server, "Live map, every second",
            "The map shows every vessel's position as the server gets it, and who flies what, instead of the website's every " +
            "few seconds or the save.",
            "", "2.4"),
        new(PatchPlace.Server, "Fewer lost vessel updates, server side",
            "The same channel per vessel for what the server sends on to players, so it helps everyone, patched or not.",
            "20-120 ms of jitter: 63% lost → 19%", "2.7"),
        new(PatchPlace.Server, "Less lag, server side",
            "1 ms timers in the server too.",
            "Measured with the KSP side: 29 → 22 ms", "2.7"),
    ];
}
