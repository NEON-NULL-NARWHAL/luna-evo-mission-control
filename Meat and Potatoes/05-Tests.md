# 05 · Tests

[← Meat and Potatoes](README.md) · Code: `tests/` 🧪

```powershell
dotnet test                                             # everything (214 tests, ~50 s)
dotnet test tests/LunaEvo.MissionControl.Core.Tests      # logic only (~3 s)
dotnet test --filter "FullyQualifiedName~ServerConsole" # one class
```

## The three test projects

| Project | Framework | What it covers |
|---|---|---|
| `LunaEvo.MissionControl.Core.Tests` | xUnit v2 | Round trips of all 13 real files, in-place edits, schema from `Server.dll`, comment parsing, every validation and greying rule, presets, dirty tracking, save/backup/prune, log parsing and tailing, runtime check, app settings, and the real `ServerRunner` against the fake server (clean stop, kill fallback, startup failure). |
| `LunaEvo.MissionControl.Tests` | xUnit v3 + Avalonia.Headless | The real `MainWindow`, without a screen: **every tab and every control** edited, saved and re-read; reset everywhere; error badges blocking save; greying across tabs; the list editor; backups; close prompts; the Server console (start, commands, filters, stop, restart, log files, summary cards); the Settings page (presets, wheel, hex, theme, backups to keep, about). |
| `FakeLmpServer` | console app | Not tests: a stand-in `Server.exe`. |

0.3 added: `SetupTests` (release feed, versions, install/update/zip-slip, generated config read by **LMP's own classes**,
object lists), `LiveModsOpsTests` (website JSON, cfg parser, GameData scan, restart policy and schedule,
network, profiles, Universe backups), and `NewFeatureTests` (wizard, updater, Players + Kick, object list editor,
GameData import, review dialog, profiles, Universe restore, Connect, auto-restart giving up, daily restart, scheduled
backup).

0.4 added: `JoinTests` (address parsing, network kinds, real ping of 127.0.0.1, live info and its errors, launch plans,
LMP's client server list), `JoinPageTests` (the Join page end to end, the host's Share flow, `Ui.CanOpen`), and
security tests in `SetupTests` (tampered, oversized, wrong-host and unverified downloads; a zip swapped before
install; too many entries) and `LiveModsOpsTests` (hostile JSON, runtimeconfig, `netsh` path, webhook scrub).

0.4.1 added `SecurityRound2Tests`: oversized live info, long text cut, Universe restores that escape the folder or are
damaged (the current Universe must survive), DTDs refused, Windows device names as profile names, weak-password
warnings, and a hand-edited settings file full of nulls.

The UI tests use **xunit v3**; the Core tests stay on **xunit 2.9.3** (marked legacy). Moving them to v3 was tried
after 2.1.1: all 303 passed locally, but on GitHub's runner every UI test that starts the fake server timed out
("server running"), probably because v3 runs the Core tests as their own process and the console Ctrl+C the server
tests use then reaches the other test run's fake servers. Before trying again, find that cause (e.g. run the two
projects one after the other on CI).

2.5.0-2.6.0 added `ClientPatchTests` (the patch's `-lmp` address parser, compiled from `src/LunaEvo.ClientPatches/LmpAddress.cs`
and fuzzed; the KSP stand-ins' enum values against the recorded ones, and against KSP itself where `../TestRig/KSP`
exists), `ClientPatches` install/remove and patched launch plans in `JoinTests`, and a Join page test with fake patch
bytes (`JoinPage.PatchBytes`). In-game checks run by hand on the test rig: `tests/RigRunner/Start-Rig.ps1`, then
`RigRunner` (`--selftest` checks its own maths; `--mode slow` for the time patch), see [26](26-Server-Plugin.md).

2.4.0 added `ServerPluginTests` (the server plugin with LMP 0.29.2's real message classes; Core.Tests references
`Server.dll` from the fixtures for this) and `tests/FakeLmpClient`, a no-KSP LMP player for sandbox checks against a
real server (not run by `dotnet test`). 2.3.0 added live-map tests to `MapTests`/`MapPageTests`.

2.2.0 added `MapTests` (Core: Kepler orbits against known answers, stock periods, planet packs with moves, removals
and a broken pack, the universe clock, vessel orbits, odd orbits fuzzed) and `MapPageTests` (UI: a fake KSP with a
pack body, vessels from `TestData.AddOrbitingVessels`, focus, click, details, filters). Headless windows don't paint:
the map test renders the canvas into a `RenderTargetBitmap` so its marks exist.

2.1.1 (audit 10) added `FuzzTests` (every parser of outside text, 1,500 seeded mutations each; a parser may only throw
what it documents, and the seed is fixed so a failure reproduces), `CodeHygieneTests` (no `/// <summary>` left above
another one: the sign of code inserted between a member and its comment) and `SupportCoverageTests` (`ErrorLog`,
`ServerProcess.Find`). Coverage: `dotnet test tests/LunaEvo.MissionControl.Core.Tests --collect:"XPlat Code Coverage"`
(Core was 85%).

2.1.0 added `PlayTogetherTests` (UI: CKAN provider choices with real ckan.exe output, Cancel, Copy log, Join →
install the server's mods, the LMP version check from the browser and an invite, browser ping/sort/save/password,
Connect's mod list export). `UiHarness` fakes `CkanPage.CopyText` (`ui.Copied`), `BrowserPage.Ping` and
`JoinPage.PingServer`: tests never ping real addresses.

1.4.0 added `BrowserTests` (Core: master server lists merged and read defensively, endpoints, KSP's game version, port
range) and `QuickAccessTests` (UI: versions, issues list, quick edit, vessel cards, simple mode, browser to Join,
allowing a player's parts). On GitHub, `UiHarness.WaitFor` waits three times longer (`CI=true`).

1.2.1 added `SecurityRound6Tests` (audit 6): `Remove("..")` deletes nothing above its folder, crafts named after
Windows devices, a release list over 10 MB refused; plus a UI test refusing a `.ckan` file over 5 MB.

0.5 added `PlayerFeaturesTests` (history from real LMP log lines, replayed and chat lines ignored, no IPs stored,
admin-password alerts, bans and the ban list, the mod check against LMP's client rules, Discord escaping and
`allowed_mentions`, DPAPI) and `PlayerFeaturesUiTests` (a running fake server driving history and alerts end to end,
notifications, unban, notes, the encrypted webhook, Check my mods, sharing rules). The fake server's `/fakejoin`,
`/fakeguess` and `/fakeleave` print LMP's real lines. `UiHarness` now copies the fixture server to a temp folder,
and collects Windows notifications in `ui.Notifications`.

0.6 added `AppUpdaterTests` (release choice, private repo, verified download and exe swap, tampering, not an exe,
dev builds) and `NavigationAndUpdateTests` (the rail, Server settings tabs and badges, page tabs, Update and restart,
the dev-build message). `ui.OpenTab(key)` clicks the button if it's on screen and otherwise navigates, so file tabs
work from any page. `MainWindow.CurrentExe` is null and `StartNewVersion` throws in every harness.

## No network in tests

`FakeHttp` (an `HttpMessageHandler`) serves canned responses by URL and records every request. `UiHarness` routes the
whole app through it (`Core.Http.Override`), so GitHub and every LMP website are fake, and unknown URLs get a
404. `TestData.ReleasesJson()` is a GitHub releases response shaped like LMP's; `TestData.MakeServerZip()` builds a zip
shaped like LMP's download from the fixture binaries.

## Fixtures (`tests/Fixtures/`) ⚠️

- `Config/`: our real server's 13 files, with **passwords blanked** and the server name changed. They keep LMP's exact
  bytes (CRLF, no BOM, the misleading `utf-16` declaration), and `.gitattributes` stops git from converting them.
  **Don't edit them in an editor**; a changed byte fails the round-trip tests. Tests always work on a temp copy
  (`TestPaths.CopyConfigs()`).
- `LMPServer/`: LMP 0.29.2's `Server.dll` with its dependencies (MIT, `LMP-LICENSE.txt`) for the schema, plus two
  **synthetic** logs (no real IPs or player names).

## The fake server

`tests/FakeLmpServer/Program.cs` prints LMP's startup lines in LMP's format, echoes `Command input: ...`, answers
`/help`, broadcasts plain text as `[Chat]`, and on Ctrl+C prints `Exiting...` and `So long and thanks for all the
fish!` before exiting with 0.

| Flag | Simulates |
|---|---|
| `--fatal` | A startup failure followed by LMP's wait on stdin. |
| `--ignore-ctrl-c` | A hung server, to test the kill fallback. |
| `--crash` | Exits with code 3 shortly after starting, to test auto-restart. (UI tests set `ServerPage.ExtraArguments`.) |

It also implements the Ctrl+C helper (`ConsoleControl.RunHelper`), because the test host can't play that role.
`FakeServer.CreateServerFolder()` copies its build to a temp folder; UI tests add LMP's `Server.dll` next to it, so
the window has a real schema *and* a startable "server".

## Writing a UI test

```csharp
[AvaloniaFact]
public void My_feature_works()
{
    using var ui = new UiHarness();            // or new UiHarness(fakeServer: true)
    ui.OpenTab(LmpRules.General);              // clicks the nav item
    ui.Find<TextBox>("GeneralSettings.xml/ServerName").Text = "LUNA EVO";
    ui.Pump();                                 // let the UI process events
    ui.Click(ui.Find<Button>("save"));
    Assert.False(ui.Session.IsDirty);
}
```

- Find controls by **AutomationId** (see [04](04-App-UI.md#generated-controls)).
- `ui.WaitFor(condition, "what")` pumps the UI until something asynchronous happens (server output, state changes).
- `ui.Await(task)` waits for an async app method. ⚠️ **Never** use `.Result` or `.GetAwaiter().GetResult()` in a UI
  test: the awaited code needs the UI thread to continue, so blocking it deadlocks (this hung a test run for 10 minutes once).
- `Dialogs.PromptResponder` answers text prompts (kick reason, profile name).
- Dialogs are answered automatically: `ui.ConfirmDialogs = false` picks Cancel; `ui.Answer = (title, buttons) => index`
  picks a specific button; `ui.DialogsShown` lists what appeared.
- Each harness uses temp configs and a temp settings file, so tests never touch your real files or preferences.
- `JoinPage.StartKsp` **throws** in every harness unless the test replaces it, so no test can start a real KSP.
  `TestData.FakeKsp()` makes a KSP folder with LMP's client `settings.xml`.
- `TestData.ReleasesJson(zip)` gives each asset the zip's real size and SHA-256 `digest`; serve the zip at
  `TestData.ServerZipUrl` with `.Bytes(...)`.

## The PC's real KSP (2.9)

`GameDataScanner.SteamSearch` finds KSP in the Steam libraries of the PC running the tests, which on the owner's PC
is their real, modded KSP. A CKAN test once picked it up as its target (CKAN itself is faked, so nothing ran). UiHarness
now sets `SteamSearch = () => null`: tests only ever see the fake KSPs they make.

## Temp folders (2.9.2)

Each UI test copies the fixture server into the temp folder. They were never deleted: 34 GB on the owner's C: drive.
`TempCleanup` (in Core.Tests, compiled into the UI tests too): `UiHarness.Dispose` deletes the test's server copy,
settings and CKAN folders, and a module initializer in each test project deletes `LunaEvoTests*` leftovers older than
2 hours (not newer: the other project may be running). It only ever deletes inside the temp folder. Local runs set
`TEMP`/`TMP` to `D:\LUNA EVOLVED\TestTemp`. `DiskSpace.FreeBytes` is faked too (100 GB), so a full drive on the test PC
doesn't change the tests.
