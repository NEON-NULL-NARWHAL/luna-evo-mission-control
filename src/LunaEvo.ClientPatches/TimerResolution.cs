// LUNA EVO · LunaEvo.ClientPatches/TimerResolution.cs
// [Patch] Asks Windows for 1 ms timers in this process, so LMP's short polling waits last ~1 ms instead of ~15.6 ms.
// Used by: PatchLoader (KSP), LunaEvoPlugin (LMP's server), FakeLmpClient (the rig's observer); compiled into each
// Guide:   Meat and Potatoes/27-Client-Patches.md

using System;
using System.Runtime.InteropServices;

namespace LunaEvo.ClientPatches
{
    /// <summary>
    /// LMP polls at every hop (Thread.Sleep(3) in the client, 5 ms timers in the server). Windows rounds short waits up to
    /// its timer resolution, 15.6 ms by default, so each hop adds up to ~16 ms (audit 11, P3: 40-60 ms between two games
    /// on one PC). timeBeginPeriod(1) asks for 1 ms; since Windows 11 a hidden or minimized process (the server's hidden
    /// console, KSP in the background) may have that ignored, so it also asks not to be throttled for it. Nothing on the
    /// network changes. Elsewhere than Windows (Linux servers) it does nothing: their timers are fine already.
    /// </summary>
    public static class TimerResolution
    {
        /// <summary>True if it was raised (Windows only).</summary>
        public static bool Raise()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return false;
            try
            {
                var state = new PowerThrottlingState { Version = 1, ControlMask = IgnoreTimerResolution, StateMask = 0 };
                SetProcessInformation(GetCurrentProcess(), ProcessPowerThrottling, ref state, (uint)Marshal.SizeOf(typeof(PowerThrottlingState)));
                return timeBeginPeriod(1) == 0;
            }
            catch (Exception)
            {
                // Missing winmm/kernel32 entry points (very old Windows, Wine): nothing raised, nothing broken.
                return false;
            }
        }

        public static void Lower()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return;
            try
            {
                timeEndPeriod(1);
            }
            catch (Exception)
            {
            }
        }

        private const int ProcessPowerThrottling = 4;
        private const uint IgnoreTimerResolution = 0x4; // PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION

        [StructLayout(LayoutKind.Sequential)]
        private struct PowerThrottlingState
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint milliseconds);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint milliseconds);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessInformation(IntPtr process, int informationClass, ref PowerThrottlingState information, uint size);
    }

    /// <summary>
    /// Turns single LUNA EVO patches off for the test rig's before-and-after runs: LUNAEVO_PATCHES_OFF="P1,P3,P4".
    /// Players never set it, so for them every patch is on.
    /// </summary>
    public static class PatchSwitches
    {
        public static bool IsOff(string name)
        {
            var off = Environment.GetEnvironmentVariable("LUNAEVO_PATCHES_OFF");
            if (string.IsNullOrEmpty(off))
                return false;
            foreach (var part in off.Split(','))
                if (string.Equals(part.Trim(), name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
