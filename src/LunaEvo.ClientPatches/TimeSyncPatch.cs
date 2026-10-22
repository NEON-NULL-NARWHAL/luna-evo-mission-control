// LUNA EVO · LunaEvo.ClientPatches/TimeSyncPatch.cs
// [Patch] A player alone on the server isn't yanked to the subspace's time; they get a subspace at their own time.
// Used by: PatchLoader (Harmony prefix on LmpClient.Systems.TimeSync.TimeSyncSystem.CheckGameTime)
// Guide:   Meat and Potatoes/27-Client-Patches.md

using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LmpClient;
using LmpClient.Systems.SettingsSys;
using LmpClient.Systems.TimeSync;
using LmpClient.Systems.Warp;

namespace LunaEvo.ClientPatches
{
    /// <summary>
    /// LMP keeps every game on its subspace's clock (LmpClient/Systems/TimeSync/TimeSyncSystem.cs): within 3.5 s it
    /// speeds the game up or slows it down, beyond that it sets the game's time, which teleports vessels along their
    /// orbits and cuts engines mid-burn (upstream issue #469). A player alone on the server has nobody to keep up with. For
    /// them, this does what LMP does when a time warp ends: ask the server for a new subspace at the game's own time
    /// (WarpNewSubspaceMsgData, the same messages as always). With anyone else online it stays out of the way: until
    /// 2.9 it stepped in whenever nobody else was in the player's subspace, and in the owner's two-player game
    /// (2026-10-22) every frame-rate dive moved the slower player into a subspace of their own, behind the other, who
    /// then couldn't be seen until they synced. LMP's jump keeps players together, and that matters more.
    /// </summary>
    internal static class TimeSyncPatch
    {
        /// <summary>LMP's MaxPhysicsClockMsError: beyond this, LMP jumps the game's clock.</summary>
        internal const double JumpThresholdMs = 3500;

        /// <summary>
        /// At most one new subspace this often. A game that can't keep real time at all (the rig's 0.36x test) would
        /// otherwise ask for one every few seconds; alone, it has nobody to keep up with, so it just runs at its own pace
        /// in between.
        /// </summary>
        internal static readonly TimeSpan MoveInterval = TimeSpan.FromSeconds(30);

        private static DateTime _lastMove = DateTime.MinValue;

        private static MethodInfo _requestNewSubspace;

        /// <summary>The private method LMP calls when a warp ends; null if this LMP doesn't have it (then nothing is patched).</summary>
        internal static MethodInfo RequestNewSubspace => _requestNewSubspace ?? (_requestNewSubspace = AccessTools.Method(typeof(WarpSystem), "RequestNewSubspace"));

        /// <summary>Harmony prefix: true lets LMP's own check run (everything but the alone-and-far-off case).</summary>
        internal static bool Prefix(TimeSyncSystem __instance)
        {
            try
            {
                var warp = WarpSystem.Singleton;
                if (__instance == null || !__instance.Enabled || warp == null || warp.CurrentlyWarping || warp.WaitingSubspaceIdFromServer || !CanSyncTime())
                    return true;
                var errorMs = TimeSyncSystem.CurrentErrorSec * 1000;
                if (Math.Abs(errorMs) <= JumpThresholdMs || !AloneOnServer(warp))
                    return true;
                if (DateTime.UtcNow - _lastMove < MoveInterval)
                {
                    UnityEngine.Time.timeScale = 1; // moved recently: no jump, no new subspace yet, and no racing to catch up
                    return false;
                }

                _lastMove = DateTime.UtcNow;
                LunaLog.LogWarning($"[LUNA EVO] {errorMs / 1000:0.0} s off subspace {warp.CurrentSubspace}'s time and nobody else is online: " +
                                   "taking a subspace at this game's own time instead of jumping");
                warp.CurrentSubspace = -1; // "warping", as at the start of a time warp: tells the server and the others
                RequestNewSubspace.Invoke(warp, null);
                return false;
            }
            catch (Exception e)
            {
                // Whatever goes wrong, LMP's own behavior is the fallback: never worse than unpatched.
                LunaLog.LogError($"[LUNA EVO] Time patch skipped: {e.Message}");
                return true;
            }
        }

        /// <summary>Nobody else is on the server: LMP's list of every player's subspace has only me in it.</summary>
        internal static bool AloneOnServer(WarpSystem warp)
        {
            var me = SettingsSystem.CurrentSettings.PlayerName;
            return !warp.ClientSubspaceList.Keys.Any(player => player != me);
        }

        // LMP's CanSyncTime: only in the scenes where it corrects time.
        private static bool CanSyncTime()
        {
            switch (HighLogic.LoadedScene)
            {
                case GameScenes.TRACKSTATION:
                case GameScenes.FLIGHT:
                case GameScenes.SPACECENTER:
                case GameScenes.EDITOR:
                    return true;
                default:
                    return false;
            }
        }
    }
}
