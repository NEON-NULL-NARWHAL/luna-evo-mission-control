// LUNA EVO · LunaEvo.ClientPatches/CommandLinePatch.cs
// [Patch] Replaces LMP 0.29's command-line reading, which breaks LMP's startup whenever "-lmp" has a port.
// Used by: PatchLoader (Harmony prefix on LmpClient.MainSystem.HandleCommandLineArgs)
// Guide:   Meat and Potatoes/27-Client-Patches.md

using System;
using System.Linq;
using LmpClient;
using LmpClient.Network;
using LmpClient.Systems.SettingsSys;
using LmpClient.Utilities;

namespace LunaEvo.ClientPatches
{
    /// <summary>
    /// LMP's own version (LmpClient/MainSystem.cs, HandleCommandLineArgs) cuts "lmp://host:port" at a position measured
    /// on the whole text, which throws for any normal port. It runs inside MainSystem.Awake, so the exception stops the
    /// rest of LMP's startup (its network included). It also has no way to pass a password. This does the same job,
    /// the same way, with those two fixed. Nothing that goes over the network changes.
    /// </summary>
    internal static class CommandLinePatch
    {
        /// <summary>Set when -lmp named a server; the password is filled in at the main menu (see FillPassword).</summary>
        internal static bool PasswordPending;

        /// <summary>Harmony prefix: false means LMP's original method doesn't run.</summary>
        internal static bool Prefix()
        {
            var commands = Environment.GetCommandLineArgs();

            // The same two options LMP reads, unchanged.
            if (Array.IndexOf(commands, "-debug") >= 0)
            {
                NetworkMain.Config.ConnectionTimeout = float.MaxValue;
                NetworkMain.RandomizeBadConnectionValues();
            }
            var logFileIndex = Array.IndexOf(commands, "-logFile") + 1;
            if (logFileIndex > 0 && commands.Length > logFileIndex)
                CommonUtil.OutputLogFilePath = commands[logFileIndex];

            var addressIndex = Array.IndexOf(commands, "-lmp") + 1;
            if (addressIndex > 0 && commands.Length > addressIndex)
            {
                if (LmpAddress.TryParse(commands[addressIndex], out var host, out var port))
                {
                    MainSystem.CommandLineServer = new ServerEntry { Address = host, Port = port };
                    PasswordPending = true;
                    LunaLog.Log($"[LUNA EVO] Connecting via command line to: {host}, port: {port}");
                }
                else
                {
                    LunaLog.LogError("[LUNA EVO] Command line address is invalid (expected lmp://host:port)");
                }
            }
            return false;
        }

        /// <summary>
        /// The password comes from LMP's own server list (the entry with the same address and port), never from the
        /// command line, which any program on the PC can read. Called at the main menu, just before LMP connects.
        /// </summary>
        internal static void FillPassword()
        {
            PasswordPending = false;
            var target = MainSystem.CommandLineServer;
            if (target == null)
                return;
            var saved = SettingsSystem.CurrentSettings.Servers?.FirstOrDefault(s =>
                s != null && s.Port == target.Port && string.Equals(s.Address?.Trim(), target.Address, StringComparison.OrdinalIgnoreCase));
            if (saved != null)
            {
                target.Password = saved.Password ?? string.Empty;
                target.Name = saved.Name;
                LunaLog.Log($"[LUNA EVO] Using the saved server entry for {target.Address}:{target.Port}" +
                            (string.IsNullOrEmpty(target.Password) ? "" : " (with its password)"));
            }
        }
    }
}
