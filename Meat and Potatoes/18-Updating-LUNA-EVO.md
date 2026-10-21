# 18 · Updating LUNA EVO itself

[← Meat and Potatoes](README.md) · Code: `Core/Setup/AppUpdater.cs`, `App/MainWindow.cs` (`CheckForAppUpdateAsync`, `UpdateAppAsync`), `App/SettingsPage.cs` (Updates tab), `release.ps1`

> **The 2.0 rename (Config Manager → Mission Control).** `AppUpdater.ExeName` is `LunaEvo.MissionControl.exe`;
> `LegacyExeName` (`LunaEvo.ConfigManager.exe`) is what 0.6-1.4 look for, so `release.ps1` uploads the same exe under
> both names. `CheckAsync` prefers the new name, `CanUpdateItself` accepts either, and an install keeps its own file
> name when it updates. Settings: `AppSettingsStore.MigrateFrom(LegacyPath, DefaultPath)` copies once. The Windows
> startup entry: `HomeServer` deletes `LUNA EVO Config Manager` when it writes `LUNA EVO Mission Control`. Keep
> uploading the legacy asset for as long as old copies may be around; dropping it strands them on their version.

Since 0.6, LUNA EVO checks its own GitHub releases when it starts and can replace itself. **For you, releasing an update is
one command.** For players and hosts, it's one button.

## Shipping an update (maintainers)

1. Bump `<Version>` in `src/LunaEvo.MissionControl/LunaEvo.MissionControl.csproj`.
2. Add a `## <version> (date)` section to `CHANGELOG.md`.
3. Commit and push.
4. `.\release.ps1`

`release.ps1` refuses to run with uncommitted changes, without a CHANGELOG section, or if the tag already exists. It
runs the tests, builds the exe (`publish.ps1`), and creates GitHub release `v<version>` with the exe attached and that
CHANGELOG section (plus the exe's SHA-256) as the notes. `-Draft` makes a draft to look over first; `-SkipTests` if you
just ran them.

**Why releases, not pushes:** a push is source code. Only a release carries a built, checksummed exe, and only a
release is something you decided to ship.

## What the app does

| Step | Where | Notes |
|---|---|---|
| Check | `AppUpdater.CheckAsync`, on startup (App settings → Updates → "Check for new LUNA EVO versions") and "Check now" | `GET api.github.com/repos/NEON-NULL-NARWHAL/luna-evo-mission-control/releases`. Picks the highest version newer than this one with an asset named `LunaEvo.MissionControl.exe` **that has a SHA-256 digest**. Pre-releases and drafts are skipped. A 404 means the repo is private (or gone): the page says so. |
| Tell | Rail note "LUNA EVO x.y.z is available" (click → App settings → Updates) | |
| Ask | `UpdateAppAsync` | Unsaved changes: save / update without saving / cancel. A running server: keep it running (the new version follows its log; console commands come back after the server's next restart) / stop it first / cancel. |
| Download | `ServerInstaller.DownloadAsync` | The same verified download as LMP updates: HTTPS from GitHub hosts only (also after redirects), size limit, streaming SHA-256, never unverified. |
| Install | `AppUpdater.Install` | Copies to `<exe>.new`, checks the hash **again** there, checks it starts with `MZ` and that its version is the release's, then renames the running exe to `<exe>.old` (Windows allows renaming a running exe, not overwriting it) and `.new` into its name. Any failure puts the old one back. |
| Restart | `MainWindow.StartNewVersion` | Starts the new exe with the same command-line options, then closes. |
| Clean up | `AppUpdater.CleanUp` | The new version deletes `.old` on startup, and again 15 s later (the old process may still be closing). |

Checked on real Windows: a running 0.5.0 exe was renamed to `.old`, 0.6.0 started in its place, and it deleted the
`.old` file once 0.5.0 had closed.

## Limits

- **Only the published single-file exe updates itself** (`CanUpdateItself`). A development build (DLLs next to the exe,
  or `dotnet run`) shows the release page instead.
- **The repository must stay public**: the check isn't signed in. If it were made private again, GitHub would answer
  404 and App settings → Updates would say "The repository may be private." (It was public from 0.6.0's release on; checked
  that an anonymous request lists v0.6.0 and v0.4.0 with their SHA-256 digests.)
- Each copy updates itself. The setup wizard copies LUNA EVO into each server folder, so a PC with several copies
  updates each when it's opened.
- The folder must be writable. Under `Program Files` the update says so and points to the release page.

## Tests

`AppUpdaterTests` (Core: release choice, 404, download+swap, tampered download, swapped file before install, not an
exe, dev builds) and `NavigationAndUpdateTests` (UI: the rail note, Update and restart, the dev-build message).
`UiHarness` sets `MainWindow.CurrentExe = null` and makes `StartNewVersion` throw, so no test can replace or restart
the test host.
