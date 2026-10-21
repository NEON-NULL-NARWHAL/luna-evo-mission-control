# 20 · Next steps (handoff)

[← Meat and Potatoes](README.md)

A ready-to-pick-up list for whoever works on LUNA EVO next (a person, a local model, or Claude). Read
[07 · LMP gotchas](07-LMP-Gotchas.md) and [14 · Security](14-Security.md) first, then pick a task.

## House rules (don't skip)

- `LunaMultiplayer/` is read-only reference. All code goes in `ConfigManager/`.
- Never test against the live server or the owner's real KSP; use `tests/Fixtures`, the fake server, or the demo copy.
- Never commit real passwords, IPs, player names or user paths. The repo is **public**.
- Every change gets tests; `dotnet test` must be green (CI runs it on every push).
- Match the code around you: file header (`// LUNA EVO · path`, `[Core]/[UI]`, `Used by`, `Guide`), comment style, `.WithId(...)` on every control.
- Ship with: bump `<Version>` in `src/LunaEvo.ConfigManager/LunaEvo.ConfigManager.csproj` and the version in
  `tests/LunaEvo.ConfigManager.Tests/SettingsPageTests.cs`, add a `## x.y.z` CHANGELOG section, commit, push, `.\release.ps1`.
- If a UI test fails with "No precompiled XAML found", it's a stale build: `dotnet build-server shutdown`, `dotnet clean`, retest.
- Bash heredocs mangle `\n` inside C# strings; edit files with a proper editor.

## Quick wins (about an hour each)

1. **"Recommended for a mesh server" button** (Server settings page header). Applies: Connection `MaximumTransmissionUnit` = 1200,
   `Upnp` = false; MasterServer `RegisterWithMasterServer` = false; Log `LogLevel` = Normal; General `PrintMotdInChat` = true.
   Show the changes with `MainWindow.DescribeChanges` before applying. Values live in `ConfigSession.SetValue(file, setting, value)`.
   Why 1200: Tailscale's MTU is 1280, LMP's default 1408 gets fragmented.
2. ~~**Settings search.**~~ (done in 0.8.0: `MainWindow.ShowSearch`) A TextBox in the Server settings header (`MainWindow.BuildPages`, `settingsHeader`). Match setting
   names and descriptions across `session.Schema.Files`; on pick, `Navigate(fileName)` and bring the row into view
   (`SettingRow.View.BringIntoView()`). Rows are in `_rows`.
3. ~~Universe backup on every stop and before an LMP update~~ (done in 0.7.1: `UniverseBackups.Safety`).
4. ~~**Scheduled chat announcements.**~~ (done in 0.8.0: `Core/Ops/Announcements.cs`, `ServerPage.CheckAnnouncements`) `AppSettings`: list of (minutes, text). `ServerPage.Tick` sends `/say <text>` via
   `TrySend` when due (LMP takes one command per 500 ms). Settings → Automation tab gets an editor.
5. ~~**Copy invite**~~ (done in 0.9.0: `ConnectPage.Invite`, `ServerAddress.Parse` reads pasted invites). Was: (Connect page): one button copying "Server: <name>, address <addr>:<port>, LMP <version>, password: yes/no".
   `ServerAddress.Parse` on the Join page should accept it pasted (take the first `host:port` in the text).
6. ~~**Crafts page screenshots**~~ (done in 0.9.0). for the README (demo copy only; keep UI automation short, it steals focus).
7. **Commit email**: switch `git config user.email` (repo-local) to the GitHub noreply address. Ask the owner first.

- ~~Two notification-area icons~~ (0.9.0: `DesktopNotifier` removes its icon `ShowFor` after each notification).
  Untested on real Windows: whether the notification centre keeps the entry after the icon goes.

## CKAN (next steps; 1.1.0 did downloads, instances and launch options: [22](22-CKAN-and-LMP-Client.md))

- **Share the server's modpack:** the host's `<ksp>/CKAN/installed-<instance>.ckan` (Connect page, invite, Kystack).
  Players compare by mod name using `<ksp>/CKAN/registry.json` (`installed_modules`).
- **Install missing mods with CKAN:** `ckan.exe install -c <modpack.ckan> --headless` against the chosen instance
  (check `CKAN/registry.locked`: CKAN open → ask to close, or open CKAN's GUI instead). Test only on a cloned instance.
- **Kystack server browser** (see the conversation notes): own LMP master server (`LmpMasterServer`, Docker), PR to
  LMP's `MasterServersList.txt`, a web list with "Join with LUNA EVO" links.

## Bigger

- **Click-to-join links:** register `lmp://` for LUNA EVO (HKCU\Software\Classes\lmp), `Program.cs` receives the URL,
  opens the Join page with the address filled in. Ask before touching the registry.
- **Share flying vessels** (not just designs): `Universe/Vessels/<guid>.txt` between servers. Server must be stopped;
  new GUID on import; strip crew or map kerbals; check parts against the target's mod control. Read
  `Server/System/VesselStoreSystem.cs` first.

## LMP fork (game code, needs KSP to test)

- **Rate-limit admin passwords:** `Server/Message/AdminMsgReader.cs:22` compares with no limit. Count failures per client,
  back off / kick after N. Constant-time compare.
- **Direct connect with a port and a password:** `LmpClient/MainSystem.cs` ~527 slices `lmp://host:port` with the
  wrong index; add `-lmppassword`. Then `KspLauncher.PlanFor` can always auto-connect.
- **Plumes on other players' engines:** throttle *is* synced (`VesselFlightStateSystem.cs:249`); the engine modules on
  remote vessels probably don't update their effect state. Investigate `ModuleEngines` FX for vessels LMP controls.
- **Rubber-banding:** the client vessel position system. Read the real code before changing anything (CLAUDE.md "Later").
