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
                Apply(harmony, applied, "no time jumps when alone on the server", AccessTools.Method(typeof(TimeSyncSystem), "CheckGameTime"),
                    typeof(TimeSyncPatch), nameof(TimeSyncPatch.Prefix));
            Apply(harmony, applied, "position time stamps one physics tick right",
                AccessTools.Method(typeof(LmpClient.Systems.VesselPositionSys.VesselPositionMessageSender), "CreateMessageFromVessel"),
                typeof(StampPatch), nameof(StampPatch.Postfix), postfix: true);
            Apply(harmony, applied, "P1 a channel per vessel for fast updates",
                AccessTools.PropertyGetter(typeof(LmpCommon.Message.Client.VesselCliMsg), "DefaultChannel"),
                typeof(ChannelPatch), nameof(ChannelPatch.Postfix), postfix: true, off: "P1");
            if (PatchSwitches.IsOff("P3"))
                Debug.Log("[LUNA EVO] Not applied (P3 1 ms timers): switched off for a test (LUNAEVO_PATCHES_OFF)");
            else if (TimerResolution.Raise())
                applied.Add("P3 1 ms timers");
            Debug.Log($"[LUNA EVO] Client patches {typeof(PatchLoader).Assembly.GetName().Version} on LMP {lmp}: " +
                      (applied.Count == 0 ? "none applied" : string.Join("; ", applied.ToArray())));
            _windowPending = true;
        }

        /// <summary>Set once the patches are on; the window is added in <see cref="Update"/> when KSP's events exist.</summary>
        private static bool _windowPending;

        /// <summary>
        /// The LUNA EVO button on KSP's toolbar, and its window (EvoWindow). Optional: never stops the patches. KSP creates
        /// GameEvents' events after this addon starts (Startup.Instantly): until then, try again next frame. KSP's events
        /// only take methods of an object (a static method's delegate has no target, and EventVoid.Add throws on it).
        /// </summary>
        private void AddWindow()
        {
            if (GameEvents.onGUIApplicationLauncherReady == null || GameEvents.onGUIApplicationLauncherDestroyed == null)
                return;
            _windowPending = false;
            try
            {
                GameEvents.onGUIApplicationLauncherReady.Add(OnToolbarReady);
                GameEvents.onGUIApplicationLauncherDestroyed.Add(OnToolbarDestroyed);
                if (KSP.UI.Screens.ApplicationLauncher.Ready)
                    EvoWindow.AddButton();
            }
            catch (Exception e)
            {
                Debug.LogError($"[LUNA EVO] No toolbar button: {e}");
            }
        }

        private void OnToolbarReady() => EvoWindow.AddButton();

        private void OnToolbarDestroyed() => EvoWindow.RemoveButton();

        public void OnGUI() => EvoWindow.OnGUI();

        private static void Apply(Harmony harmony, List<string> applied, string what, System.Reflection.MethodBase target, Type patch, string method,
            bool postfix = false, string off = null)
        {
            if (off != null && PatchSwitches.IsOff(off))
            {
                Debug.Log($"[LUNA EVO] Not applied ({what}): switched off for a test (LUNAEVO_PATCHES_OFF)");
                return;
            }
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
            if (_windowPending)
                AddWindow();
            EvoWindow.KeepInStep();
            EvoWindow.ReadRadio();
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
