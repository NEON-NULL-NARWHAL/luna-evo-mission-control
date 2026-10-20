# 05 · Tests

[← Meat and Potatoes](README.md) · Code: `tests/` 🧪

```powershell
dotnet test                                             # everything (183 tests, ~40 s)
dotnet test tests/LunaEvo.ConfigManager.Core.Tests      # logic only (~3 s)
dotnet test --filter "FullyQualifiedName~ServerConsole" # one class
```

## The three test projects

| Project | Framework | What it covers |
|---|---|---|
| `LunaEvo.ConfigManager.Core.Tests` | xUnit v2 | Round trips of all 13 real files, in-place edits, schema from `Server.dll`, comment parsing, every validation and greying rule, presets, dirty tracking, save/backup/prune, log parsing and tailing, runtime check, app settings, and the real `ServerRunner` against the fake server (clean stop, kill fallback, startup failure). |
| `LunaEvo.ConfigManager.Tests` | xUnit v3 + Avalonia.Headless | The real `MainWindow`, without a screen: **every tab and every control** edited, saved and re-read; reset everywhere; error badges blocking save; greying across tabs; the list editor; backups; close prompts; the Server Console (start, commands, filters, stop, restart, log files, summary cards); the Settings page (presets, wheel, hex, theme, backups to keep, about). |
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
