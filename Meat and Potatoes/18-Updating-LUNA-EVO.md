# 18 · Updating LUNA EVO itself

[← Meat and Potatoes](README.md) · Code: `Core/Setup/AppUpdater.cs`, `App/MainWindow.cs` (`CheckForAppUpdateAsync`, `UpdateAppAsync`), `App/SettingsPage.cs` (Updates tab), `release.ps1`

Since 0.6, LUNA EVO checks its own GitHub releases when it starts and can replace itself. **For you, releasing an update is
one command.** For players and hosts, it's one button.

## Shipping an update (maintainers)

1. Bump `<Version>` in `src/LunaEvo.ConfigManager/LunaEvo.ConfigManager.csproj`.
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
| Check | `AppUpdater.CheckAsync`, on startup (Settings → Updates → "Check for new LUNA EVO versions") and "Check now" | `GET api.github.com/repos/NEON-NULL-NARWHAL/luna-evo-multiplayer-config-manager/releases`. Picks the highest version newer than this one with an asset named `LunaEvo.ConfigManager.exe` **that has a SHA-256 digest**. Pre-releases and drafts are skipped. A 404 means the repo is private (or gone): the page says so. |
| Tell | Rail note "LUNA EVO x.y.z is available" (click → Settings → Updates) | |
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
- **The repository must be public** for unauthenticated checks. While it's private, GitHub answers 404 and Settings →
  Updates says "The repository may be private."
- Each copy updates itself. The setup wizard copies LUNA EVO into each server folder, so a PC with several copies
  updates each when it's opened.
- The folder must be writable. Under `Program Files` the update says so and points to the release page.

## Tests

`AppUpdaterTests` (Core: release choice, 404, download+swap, tampered download, swapped file before install, not an
exe, dev builds) and `NavigationAndUpdateTests` (UI: the rail note, Update and restart, the dev-build message).
`UiHarness` sets `MainWindow.CurrentExe = null` and makes `StartNewVersion` throw, so no test can replace or restart
the test host.
