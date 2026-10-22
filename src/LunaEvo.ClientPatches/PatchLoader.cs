// LUNA EVO · LunaEvo.ClientPatches/PatchLoader.cs
// [Patch] Applies the LUNA EVO patches to the official LMP client when KSP starts, if this LMP version is one they're for.
// Used by: KSP (KSPAddon, Startup.Instantly); lives in GameData/000_LunaEvoPatches so it loads before LunaMultiplayer
// Guide:   Meat and Potatoes/27-Client-Patches.md

using System;
using HarmonyLib;
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
                var lmp = typeof(LmpClient.MainSystem).Assembly.GetName().Version;
                if (!Supports(lmp))
                {
                    Debug.Log($"[LUNA EVO] Patches are for LMP 0.29; this is LMP {lmp}. Nothing patched.");
                    Destroy(this);
                    return;
                }
                var target = AccessTools.Method(typeof(LmpClient.MainSystem), "HandleCommandLineArgs");
                if (target == null)
                {
                    Debug.Log("[LUNA EVO] LMP's HandleCommandLineArgs not found. Nothing patched.");
                    Destroy(this);
                    return;
                }
                new Harmony("LunaEvo.ClientPatches").Patch(target, prefix: new HarmonyMethod(typeof(CommandLinePatch), nameof(CommandLinePatch.Prefix)));
                Debug.Log($"[LUNA EVO] Client patches {typeof(PatchLoader).Assembly.GetName().Version} applied to LMP {lmp}: -lmp with a port and a saved password");
            }
            catch (Exception e)
            {
                // A patch that can't apply must never take LMP down with it.
                Debug.LogError($"[LUNA EVO] Patches not applied: {e}");
                Destroy(this);
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
