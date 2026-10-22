// LUNA EVO · LunaEvo.ClientPatches/PatchLoader.cs
// [Patch] Applies the LUNA EVO patches to the official LMP client when KSP starts, if this LMP version is one they're for.
// Used by: KSP (KSPAddon, Startup.Instantly); lives in GameData/000_LunaEvoPatches so it loads before LunaMultiplayer
// Guide:   Meat and Potatoes/27-Client-Patches.md

using System;
using System.Collections.Generic;
using HarmonyLib;
using LmpClient.Systems.TimeSync;
using UnityEngine;

namespace LunaEvo.ClientPatches
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class PatchLoader : MonoBehaviour
    {
        /// <summary>The LMP versions these patches were written and tested for. Any other: nothing is patched.</summary>
        internal static bool Supports(Version lmp) => lmp != null && lmp.Major == 0 && lmp.Minor == 29;

        public void Awake()
        {
            DontDestroyOnLoad(this);
            try
            {
                ApplyAll();
            }
            catch (Exception e)
            {
                // LMP missing or not loadable: patch nothing, and never take KSP or LMP down with us.
                Debug.LogError($"[LUNA EVO] Patches not applied: {e.Message}");
                Destroy(this);
            }
        }

        private void ApplyAll()
        {
            var lmp = typeof(LmpClient.MainSystem).Assembly.GetName().Version;
            if (!Supports(lmp))
            {
                Debug.Log($"[LUNA EVO] Patches are for LMP 0.29; this is LMP {lmp}. Nothing patched.");
                Destroy(this);
                return;
            }
            var harmony = new Harmony("LunaEvo.ClientPatches");
            var applied = new List<string>();
            // Each patch on its own: one that can't apply never stops the others, or LMP.
            Apply(harmony, applied, "-lmp with a port and a saved password", AccessTools.Method(typeof(LmpClient.MainSystem), "HandleCommandLineArgs"),
                typeof(CommandLinePatch), nameof(CommandLinePatch.Prefix));
            if (TimeSyncPatch.RequestNewSubspace != null)
                Apply(harmony, applied, "no time jumps when alone in a subspace", AccessTools.Method(typeof(TimeSyncSystem), "CheckGameTime"),
                    typeof(TimeSyncPatch), nameof(TimeSyncPatch.Prefix));
            Apply(harmony, applied, "position time stamps one physics tick right",
                AccessTools.Method(typeof(LmpClient.Systems.VesselPositionSys.VesselPositionMessageSender), "CreateMessageFromVessel"),
                typeof(StampPatch), nameof(StampPatch.Postfix), postfix: true);
            Debug.Log($"[LUNA EVO] Client patches {typeof(PatchLoader).Assembly.GetName().Version} on LMP {lmp}: " +
                      (applied.Count == 0 ? "none applied" : string.Join("; ", applied.ToArray())));
        }

        private static void Apply(Harmony harmony, List<string> applied, string what, System.Reflection.MethodBase target, Type patch, string method,
            bool postfix = false)
        {
            try
            {
                if (target == null)
                {
                    Debug.Log($"[LUNA EVO] Not applied ({what}): LMP's method isn't there");
                    return;
                }
                if (postfix)
                    harmony.Patch(target, postfix: new HarmonyMethod(patch, method));
                else
                    harmony.Patch(target, prefix: new HarmonyMethod(patch, method));
                applied.Add(what);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LUNA EVO] Not applied ({what}): {e}");
            }
        }

        public void Update()
        {
            if (CommandLinePatch.PasswordPending && HighLogic.LoadedScene == GameScenes.MAINMENU)
            {
                try
                {
                    CommandLinePatch.FillPassword();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LUNA EVO] Couldn't read LMP's server list: {e.Message}");
                }
            }
        }
    }
}
