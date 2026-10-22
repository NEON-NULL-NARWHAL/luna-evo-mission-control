// LUNA EVO · LunaEvo.ClientPatches/EvoWindow.cs
// [Patch] The in-game LUNA EVO window (KSP toolbar button): connection, players, quick actions and LMP's own settings.
// Used by: PatchLoader (toolbar events, OnGUI)
// Guide:   Meat and Potatoes/27-Client-Patches.md

using System;
using System.Linq;
using KSP.UI.Screens;
using LmpClient;
using LmpClient.Network;
using LmpClient.Systems.Lock;
using LmpClient.Systems.PlayerColorSys;
using LmpClient.Systems.SettingsSys;
using LmpClient.Systems.Status;
using LmpClient.Systems.Warp;
using LmpClient.Windows.Chat;
using LmpClient.Windows.Options;
using LmpClient.Windows.Status;
using LmpCommon.Enums;
using UnityEngine;

namespace LunaEvo.ClientPatches
{
    /// <summary>
    /// Everything here only reads LMP's state or calls what LMP's own windows call (its sync button, its color "Set"
    /// button, switching to another player's vessel, which LMP turns into spectating). Nothing new goes over the network.
    /// </summary>
    internal static class EvoWindow
    {
        private const int WindowId = 0x4C45564F; // "LEVO"
        private const float Width = 340;

        private static ApplicationLauncherButton _button;
        private static Texture2D _icon;
        private static bool _open;
        private static Rect _rect = new Rect(Screen.width / 2 - Width - 40, 90, Width, 100); // left of LMP's own windows
        private static Vector2 _scroll;
        private static Guid _ownVessel; // the player's own vessel while they watch someone else's
        private static string _lastError;

        /// <summary>
        /// "Stay in step with other players": a player who falls behind another (a slow frame, a load, the other's short
        /// warp) can't see them until they sync, which in the owner's two-player game (2026-10-22) happened again and
        /// again. On by default; switched in the window.
        /// </summary>
        internal static bool StayInStep = true;

        /// <summary>Auto-sync only for this much (s). More is someone's deliberate warp: the window asks instead.</summary>
        internal const double AutoSyncLimit = 60;

        private static DateTime _lastStepCheck;

        /// <summary>
        /// Every 2 s (from PatchLoader.Update): if another player is ahead by 1-60 s and this game isn't warping, sync to
        /// them, as LMP's own sync button does.
        /// </summary>
        internal static void KeepInStep()
        {
            if (!StayInStep || DateTime.UtcNow - _lastStepCheck < TimeSpan.FromSeconds(2))
                return;
            _lastStepCheck = DateTime.UtcNow;
            try
            {
                if (!InGame || !CanSync())
                    return;
                var warp = WarpSystem.Singleton;
                if (warp == null || warp.CurrentlyWarping || warp.WaitingSubspaceIdFromServer)
                    return;
                var behind = Behind(warp, out var latest);
                if (behind > 1 && behind <= AutoSyncLimit)
                {
                    LunaLog.Log($"[LUNA EVO] {behind:0.0} s behind subspace {latest}: syncing to stay in step with the other players");
                    warp.SyncToSubspace(latest);
                }
            }
            catch (Exception e)
            {
                if (_lastError != e.Message)
                    Debug.LogError("[LUNA EVO] Stay in step: " + e);
                _lastError = e.Message;
            }
        }

        /// <summary>LMP's own scenes for time sync: not while loading, not in the main menu.</summary>
        private static bool CanSync()
        {
            switch (HighLogic.LoadedScene)
            {
                case GameScenes.FLIGHT:
                case GameScenes.SPACECENTER:
                case GameScenes.TRACKSTATION:
                case GameScenes.EDITOR:
                    return true;
                default:
                    return false;
            }
        }

        internal static void AddButton()
        {
            if (_button != null || ApplicationLauncher.Instance == null)
                return;
            _button = ApplicationLauncher.Instance.AddModApplication(Open, Close, Nothing, Nothing, Nothing, Nothing,
                ApplicationLauncher.AppScenes.SPACECENTER | ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW |
                ApplicationLauncher.AppScenes.TRACKSTATION, Icon());
            // The test rig can't click KSP's toolbar: LUNAEVO_OPEN_WINDOW=1 opens the window with its button (players never set it).
            if (Environment.GetEnvironmentVariable("LUNAEVO_OPEN_WINDOW") == "1")
                _button.SetTrue(true);
        }

        internal static void RemoveButton()
        {
            if (_button != null && ApplicationLauncher.Instance != null)
                ApplicationLauncher.Instance.RemoveModApplication(_button);
            _button = null;
            _open = false;
        }

        private static void Open() => _open = true;

        private static void Close() => _open = false;

        private static void Nothing()
        {
        }

        internal static void OnGUI()
        {
            if (!_open)
                return;
            GUI.skin = HighLogic.Skin;
            _rect = GUILayout.Window(WindowId, _rect, Draw, "LUNA EVO", GUILayout.Width(Width));
        }

        private static void Draw(int id)
        {
            try
            {
                _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(420));
                DrawConnection();
                GUILayout.Space(8);
                DrawActions();
                GUILayout.Space(8);
                DrawSettings();
                GUILayout.EndScrollView();
                if (GUILayout.Button("Close"))
                {
                    if (_button != null)
                        _button.SetFalse(true);
                    _open = false;
                }
            }
            catch (Exception e)
            {
                // A draw that throws would throw every frame: show it in the window, log it once.
                GUILayout.Label("Something went wrong: " + e.Message);
                if (_lastError != e.Message)
                    Debug.LogError("[LUNA EVO] Window: " + e);
                _lastError = e.Message;
            }
            GUI.DragWindow();
        }

        private static bool InGame => MainSystem.NetworkState >= ClientState.Running;

        private static void DrawConnection()
        {
            GUILayout.Label("CONNECTION");
            if (!InGame)
            {
                GUILayout.Label("Not playing on a server right now (" + MainSystem.NetworkState + ").");
                return;
            }
            var endPoint = NetworkMain.ClientConnection != null && NetworkMain.ClientConnection.ServerConnection != null
                ? NetworkMain.ClientConnection.ServerConnection.RemoteEndPoint
                : null;
            GUILayout.Label("Server: " + (endPoint == null ? "?"
                : endPoint.Address.IsIPv4MappedToIPv6 ? endPoint.Address.MapToIPv4() + ":" + endPoint.Port : endPoint.ToString()));
            GUILayout.Label($"Ping: {NetworkStatistics.PingSec * 1000:0} ms");

            var warp = WarpSystem.Singleton;
            var behind = Behind(warp, out _);
            GUILayout.Label(warp.CurrentlyWarping ? "Time: warping"
                : behind > 1 ? $"Time: subspace {warp.CurrentSubspace}, {Seconds(behind)} behind the latest player"
                : $"Time: subspace {warp.CurrentSubspace}, in step with the latest player");

            var me = SettingsSystem.CurrentSettings.PlayerName;
            var players = StatusSystem.Singleton.PlayerStatusList.Values.Where(p => p.PlayerName != me).OrderBy(p => p.PlayerName).ToList();
            GUILayout.Label(players.Count == 0 ? "No one else is online." : players.Count == 1 ? "1 other player:" : players.Count + " other players:");
            foreach (var p in players)
            {
                var elsewhere = warp.ClientSubspaceList.TryGetValue(p.PlayerName, out var theirs) && theirs != warp.CurrentSubspace
                    ? " · another time" : "";
                GUILayout.Label($"  {p.PlayerName}: {Text(Localized(p.VesselText), p.StatusText)}{elsewhere}");
            }
        }

        private static void DrawActions()
        {
            GUILayout.Label("QUICK ACTIONS");
            if (!InGame)
            {
                GUILayout.Label("Join a server first.");
                return;
            }
            var warp = WarpSystem.Singleton;
            var behind = Behind(warp, out var latest);
            StayInStep = GUILayout.Toggle(StayInStep, $"Stay in step with other players (sync by itself, up to {AutoSyncLimit:0} s)");
            if (!warp.CurrentlyWarping && behind > 1 && (!StayInStep || behind > AutoSyncLimit))
            {
                if (GUILayout.Button($"Sync my clock to the latest player (+{Seconds(behind)})"))
                    warp.SyncToSubspace(latest);
            }
            else
            {
                GUILayout.Label(behind > 1 ? "Clock: catching up..." : "Clock: in step.");
            }

            if (HighLogic.LoadedScene != GameScenes.FLIGHT || !FlightGlobals.ready || FlightGlobals.ActiveVessel == null)
            {
                GUILayout.Label("Watching other players works in flight.");
                return;
            }
            var me = SettingsSystem.CurrentSettings.PlayerName;
            var active = FlightGlobals.ActiveVessel;
            var watched = 0;
            foreach (var vessel in FlightGlobals.VesselsLoaded.ToList())
            {
                if (vessel == null || vessel == active)
                    continue;
                var owner = LockSystem.LockQuery.GetControlLockOwner(vessel.id);
                if (string.IsNullOrEmpty(owner) || owner == me)
                    continue;
                watched++;
                if (GUILayout.Button($"Watch {owner}: {vessel.GetDisplayName()}"))
                {
                    if (_ownVessel == Guid.Empty && LockSystem.LockQuery.GetControlLockOwner(active.id) == me)
                        _ownVessel = active.id;
                    FlightGlobals.SetActiveVessel(vessel); // LMP turns this into spectating
                }
            }
            if (watched == 0)
                GUILayout.Label("No other player's vessel is close enough to watch (2.2 km).");

            if (_ownVessel != Guid.Empty && active.id != _ownVessel)
            {
                var own = FlightGlobals.VesselsLoaded.FirstOrDefault(v => v != null && v.id == _ownVessel);
                if (own == null)
                    GUILayout.Label("Your vessel is out of range: switch back with [ or ].");
                else if (GUILayout.Button("Back to my vessel: " + own.GetDisplayName()))
                {
                    FlightGlobals.SetActiveVessel(own);
                    _ownVessel = Guid.Empty;
                }
            }
            else if (active.id == _ownVessel)
            {
                _ownVessel = Guid.Empty;
            }
        }

        private static void DrawSettings()
        {
            GUILayout.Label("LMP SETTINGS");
            var settings = SettingsSystem.CurrentSettings;
            GUILayout.Label("Player name: " + settings.PlayerName + "  (LMP changes it only while disconnected)");

            GUILayout.BeginHorizontal();
            var before = GUI.color;
            GUI.color = settings.PlayerColor;
            GUILayout.Label("Your color", GUILayout.Width(90));
            GUI.color = before;
            if (GUILayout.Button("New random color"))
            {
                // What LMP's own options window does on "Set".
                StatusWindow.Singleton.ColorEventHandled = false;
                settings.PlayerColor = PlayerColorSystem.GenerateRandomColor();
                SettingsSystem.SaveSettings();
                if (MainSystem.NetworkState == ClientState.Running)
                    PlayerColorSystem.Singleton.MessageSender.SendPlayerColorToServer();
            }
            GUILayout.EndHorizontal();

            var ignore = GUILayout.Toggle(settings.IgnoreSyncChecks, "Ignore LMP's sync checks");
            if (ignore != settings.IgnoreSyncChecks)
            {
                settings.IgnoreSyncChecks = ignore;
                SettingsSystem.SaveSettings();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("LMP chat"))
                ChatWindow.Singleton.Display = !ChatWindow.Singleton.Display;
            if (GUILayout.Button("LMP players"))
                StatusWindow.Singleton.Display = !StatusWindow.Singleton.Display;
            if (GUILayout.Button("LMP options"))
                OptionsWindow.Singleton.Display = !OptionsWindow.Singleton.Display;
            GUILayout.EndHorizontal();
        }

        /// <summary>How far (s) the latest subspace is ahead of ours, and which one it is.</summary>
        private static double Behind(WarpSystem warp, out int latest)
        {
            latest = warp.CurrentSubspace;
            if (warp.CurrentlyWarping)
                return 0;
            var mine = warp.GetSubspaceTime(warp.CurrentSubspace);
            var best = mine;
            foreach (var subspace in warp.Subspaces.Keys.ToList())
            {
                var time = warp.GetSubspaceTime(subspace);
                if (time > best)
                {
                    best = time;
                    latest = subspace;
                }
            }
            return best - mine;
        }

        private static string Seconds(double s)
        {
            var t = TimeSpan.FromSeconds(Math.Round(s));
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes} min" : t.TotalMinutes >= 1 ? $"{t.Minutes} min {t.Seconds} s" : $"{t.Seconds} s";
        }

        /// <summary>LMP sends stock vessel names as KSP's translation keys ("#autoLOC_501232"): KSP turns them into names.</summary>
        private static string Localized(string text) =>
            text != null && text.StartsWith("#autoLOC", StringComparison.Ordinal) ? KSP.Localization.Localizer.Format(text) : text;

        private static string Text(string vessel, string status) =>
            string.IsNullOrEmpty(vessel) ? (string.IsNullOrEmpty(status) ? "?" : status) : string.IsNullOrEmpty(status) ? vessel : vessel + " (" + status + ")";

        /// <summary>The toolbar icon, drawn here (the patch is one DLL, no image files): a green planet with an orbit.</summary>
        private static Texture2D Icon()
        {
            if (_icon != null)
                return _icon;
            const int size = 38;
            _icon = new Texture2D(size, size);
            var green = new Color(0.40f, 0.86f, 0.52f, 1);
            var white = new Color(1, 1, 1, 1);
            var clear = new Color(0, 0, 0, 0);
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    double dx = x - 18.5, dy = y - 18.5;
                    var r = Math.Sqrt(dx * dx + dy * dy);
                    // A tilted orbit: an ellipse ring around the planet.
                    double ex = (dx * 0.94 + dy * 0.34) / 17.0, ey = (-dx * 0.34 + dy * 0.94) / 7.0;
                    var ring = Math.Abs(Math.Sqrt(ex * ex + ey * ey) - 1) < 0.09;
                    _icon.SetPixel(x, y, r <= 10 ? green : ring ? white : clear);
                }
            _icon.Apply();
            return _icon;
        }
    }
}
