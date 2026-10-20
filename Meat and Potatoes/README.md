# 🥩🥔 Meat and Potatoes

**The maintainer's guide to LUNA EVO Config Manager.** The main [README](../README.md) says *what* the app does;
this folder says *how*, *where* and *why*, so you can change it with confidence.

Every source file starts with a short header pointing back here, like this:

```csharp
// LUNA EVO · LunaEvo.ConfigManager.Core/Server/ServerRunner.cs
// [Core] Starts Server.exe hidden with its console piped to us; sends commands; stops it cleanly.
// Used by: ServerPage
// Guide:   Meat and Potatoes/03-Server-Control.md
```

## 🗺️ Chapters

| # | Chapter | Read it when you want to... |
|---|---|---|
| 01 | [Architecture](01-Architecture.md) | see the big picture: projects, layers, and how data flows from XML to screen and back |
| 02 | [Core: settings, files, validation](02-Core.md) | understand the schema from Server.dll, in-place XML editing, rules and saving |
| 03 | [Server control](03-Server-Control.md) | change how the server is started, stopped, logged or commanded |
| 04 | [App UI](04-App-UI.md) | change a page, a control, the theme or the styles |
| 05 | [Tests](05-Tests.md) | run, read or write tests (including the fake server) |
| 06 | [Build and release](06-Build-and-Release.md) | make the exe, bump the version, ship a release |
| 07 | [LMP gotchas](07-LMP-Gotchas.md) | avoid the traps we already fell into. **Read before touching XML or server control.** |
| 08 | [Recipes](08-Recipes.md) | follow step-by-step guides for common changes |
| 09 | [How it was built](09-How-It-Was-Built.md) | learn how the app was written with AI, and how to keep working that way |
| 10 | [Setup wizard and LMP updates](10-Setup-and-Updates.md) | change how servers are downloaded, installed, configured the first time, or updated |
| 11 | [Live players](11-Live-Players.md) | work on the Players page or LMP's website JSON |
| 12 | [Mod control from GameData](12-Mod-Control-From-GameData.md) | change the GameData scan, the KSP `.cfg` parser or the plugin-list editors |
| 13 | [Automation, Connect, Profiles](13-Automation.md) | change auto/daily restarts, Universe backups, profiles, the Connect page, or review-before-save |
| 14 | [Security](14-Security.md) | download, parse, launch or store anything. The rules from the 0.4.0 and 0.4.1 audits. |
| 15 | [Joining servers](15-Joining-Servers.md) | work on the player side: the Join page, ping and live info, launching KSP, LMP's server list |

## 🔣 Legend

Symbols used in the map below and throughout these chapters:

| Symbol | Meaning |
|:---:|---|
| ⭐ | **Start here.** A good first file to read in its area. |
| 🧠 | **Core logic.** No UI, so it can be unit-tested directly. Lives in `LunaEvo.ConfigManager.Core`. |
| 🖥️ | **UI.** Avalonia controls and pages. Lives in `LunaEvo.ConfigManager`. |
| 🔌 | **Touches LMP.** Reads its `Server.dll`, edits its XML, or runs its `Server.exe`. Behaviour depends on LMP's version. |
| ⚠️ | **Gotcha inside.** There's a non-obvious reason for how it's written; read its comments before changing it. |
| 🧪 | **Tests or test data.** |
| 📦 | **Build, packaging or release.** |
| 🎨 | **Look and feel.** Colours, fonts, styles. |

## 🧭 Map: every folder and file

### Repo root

| | File | What it is |
|:---:|---|---|
| 📦 | `LunaEvo.ConfigManager.slnx` | The solution (new XML format). Open this in VS Code or Visual Studio. |
| 📦 | `publish.ps1` | Builds the single self-contained exe into `dist/`. See [06](06-Build-and-Release.md). |
| | `README.md` | The front page. |
| | `CHANGELOG.md` | What changed in each version. |
| | `SECURITY.md` | What the app touches, both audits, how to report a problem. |
| | `LICENSE` | GPL-3.0. |
| ⚠️ | `.gitattributes` | Keeps `tests/Fixtures` byte-for-byte (CRLF, no BOM); the round-trip tests depend on it. |
| | `.gitignore` | Build output, `dist/`, backups, and dev settings. |
| | `docs/screenshots/` | Images used by the README. |

### `src/LunaEvo.ConfigManager.Core/`: the logic 🧠

| | File | What it is |
|:---:|---|---|
| ⭐🧠 | `ConfigSession.cs` | **The heart.** Schema plus loaded files: get/set values, validate, track unsaved changes, save with backup. |
| 🧠 | `AppSettings.cs` | LUNA EVO's own preferences (accent colour, theme, backups to keep, automation, saved servers, KSP folder, updates...) and their JSON file. |
| 🧠 | `Http.cs` | The one `HttpClient` the app uses (GitHub, LMP's website, the Join page). Tests set `Http.Override`. |
| 🧠🔌 | `ServerProcess.cs` | Finds a running `Server.exe` from a folder. |
| **Schema/** | | *What settings exist* |
| ⭐🧠🔌 | `Schema/LmpSchemaLoader.cs` | Reflects over the server's `Server.dll` to list files and settings, with types, defaults, descriptions and presets. |
| 🧠 | `Schema/SettingsSchema.cs` | Plain records: `SettingDefinition`, `SettingsFileSchema`, `LmpSchema`, `SettingKind`. |
| 🧠 | `Schema/CommentRules.cs` | Regexes that read limits out of descriptions ("Max 30 char", "Min value = 5000"). |
| **Files/** | | *Reading and writing XML* |
| ⭐🧠🔌⚠️ | `Files/ConfigFile.cs` | One XML file, edited in place. Handles LMP's lying `utf-16` declaration and preserves every byte. |
| 🧠 | `Files/BackupService.cs` | Timestamped backups before each save; pruning old ones. |
| **Validation/** | | *Is this config OK?* |
| 🧠 | `Validation/SettingValidator.cs` | One value vs its type and limits. Defines `Issue` and `Severity`. |
| ⭐🧠⚠️ | `Validation/LmpRules.cs` | Cross-setting rules, greying out, difficulty presets. **Add new LMP rules here.** |
| **Server/** | | *Running LMP* |
| ⭐🧠🔌 | `Server/ServerRunner.cs` | Starts `Server.exe` hidden, streams its output, sends commands, stops it. |
| 🧠🔌⚠️ | `Server/ConsoleControl.cs` | Sends Ctrl+C through a helper process. **Read [07](07-LMP-Gotchas.md) first.** |
| 🧠 | `Server/LogLine.cs` | Parses `[HH:mm:ss][Type]: message` into time, kind and message. |
| 🧠🔌 | `Server/LogFiles.cs` | Lists and reads `logs/lmpserver_*.log`; `LogTail` follows the newest one. |
| 🧠🔌 | `Server/DotNetRuntime.cs` | Checks the .NET runtime `Server.exe` needs is installed. |
| **Setup/** | | *Getting and updating LMP* ([10](10-Setup-and-Updates.md)) |
| 🧠🔌 | `Setup/LmpReleases.cs` | LMP's GitHub releases, picking the server zip; version parsing. |
| ⭐🧠🔌⚠️ | `Setup/ServerInstaller.cs` | Download (SHA-256 verified, GitHub only, size-limited) + install/update a server zip; never touches Config/Universe/logs/Plugins; backs up old binaries. See [14](14-Security.md). |
| 🧠🔌 | `Setup/ConfigGenerator.cs` | Writes a new server's config from the schema and the wizard's choices. |
| 🧠 | `Setup/RuntimeInstaller.cs` | Installs the .NET runtime with winget. |
| **Live/** | | *What's happening on the server* ([11](11-Live-Players.md)) |
| 🧠🔌 | `Live/ServerInfoClient.cs` | Reads LMP's website JSON: players, vessels, subspaces; join/leave diff. |
| **Mods/** | | *Mod control from a KSP install* ([12](12-Mod-Control-From-GameData.md)) |
| 🧠 | `Mods/KspConfigParser.cs` | Reads KSP `.cfg` ConfigNode files. |
| ⭐🧠🔌⚠️ | `Mods/GameDataScanner.cs` | Parts, resources, plugins (SHA-256) and expansions from GameData, like LMP's client. |
| **Ops/** | | *Keeping it running* ([13](13-Automation.md)) |
| 🧠 | `Ops/Restarts.cs` | Crash-restart policy (gives up after 3 in 10 min) and daily restart times. |
| 🧠🔌⚠️ | `Ops/NetworkHelper.cs` | Local addresses, LMP's detected public address, Windows Firewall rules (UDP game port, TCP website port), port check. `netsh` by absolute path. |
| **Join/** | | *The player side* ([15](15-Joining-Servers.md)) |
| 🧠 | `Join/ServerProbe.cs` | `ServerAddress` parsing; DNS, network kind, ping, and live info from a host's LMP website. |
| 🧠🔌⚠️ | `Join/KspLauncher.cs` | Finds KSP and the LMP client, plans auto-connect vs server list (LMP's `-lmp` bug), edits LMP's client `settings.xml`, starts KSP. |
| 🧠 | `Ops/ProfileStore.cs` | Named copies of the whole config. |
| 🧠🔌 | `Ops/UniverseBackups.cs` | Zips and restores the Universe. |

### `src/LunaEvo.ConfigManager/`: the app 🖥️

| | File | What it is |
|:---:|---|---|
| 📦 | `LunaEvo.ConfigManager.csproj` | App project. **The version number lives here.** |
| ⚠️ | `Program.cs` | Entry point. First runs the Ctrl+C helper if asked, then parses options and starts Avalonia. |
| 🎨 | `App.axaml` | **All colours, icons and styles.** Theme dictionaries (dark and light) and style classes. |
| | `App.axaml.cs` | Loads `App.axaml`, opens the main window. |
| ⭐🖥️ | `MainWindow.cs` | The shell: navigation rail, pages, status area, save, close prompts. |
| 🖥️ | `SettingRow.cs` | One generated row; picks the editor for the setting's type. |
| 🖥️ | `ListEditor.cs` | Searchable list editor for mod control lists. |
| 🖥️ | `ObjectListEditor.cs` | Row editor for LMP's DllFile / MandatoryPart lists. |
| 🖥️🔌 | `ModImportPanel.cs` | "Import from GameData" on the Mod Control page. |
| ⭐🖥️🔌 | `SetupWizard.cs` | "Set up a new server": download, configure, open. |
| 🖥️🔌 | `PlayersPage.cs` | Live players and vessels, Kick/Ban. |
| 🖥️ | `ConnectPage.cs` | Addresses to share, firewall, reachability, sharing live info with players. |
| ⭐🖥️ | `JoinPage.cs` | Join a server (for players): check, save, launch KSP into it. `StartKsp` is the test seam. |
| 🖥️ | `ProfilesPage.cs` | Config profiles and Universe backups. |
| ⭐🖥️🔌 | `ServerPage.cs` | The Server Console page. |
| 🖥️ | `SettingsPage.cs` | The LUNA EVO Settings page (colour wheel, behaviour, about). |
| 🎨 | `ThemeManager.cs` | Generates the palette from one accent colour and applies it live. |
| 🖥️ | `Ui.cs` | Helpers for building styled controls in code. |
| 🖥️ | `Dialogs.cs` | Message boxes (with a hook so tests can answer them). |
| 🎨 | `Assets/` | `icon.png`/`icon.ico`, and `Fonts/` (JetBrains Mono plus its OFL licence). |
| | `Properties/launchSettings.json` | `dotnet run` profiles: in-repo fixtures, or local LMP folders. |

### `tests/` 🧪

| | Path | What it is |
|:---:|---|---|
| 🧪 | `LunaEvo.ConfigManager.Core.Tests/` | xUnit tests for everything in Core (125 tests), plus shared `FakeHttp`, `FakeServer`, `TestData`. |
| 🧪🖥️ | `LunaEvo.ConfigManager.Tests/` | Headless UI tests: the real window, clicked through like a user (58 tests). |
| ⭐🧪 | `LunaEvo.ConfigManager.Tests/UiHarness.cs` | How UI tests open the window, find controls and wait. |
| 🧪🔌 | `FakeLmpServer/` | A pretend `Server.exe` with LMP's log format, commands and Ctrl+C. Also plays the Ctrl+C helper in tests. |
| 🧪⚠️ | `Fixtures/Config/` | Our real server's 13 XML files, **passwords blanked**. Don't edit by hand; tests compare bytes. |
| 🧪🔌 | `Fixtures/LMPServer/` | LMP 0.29.2's `Server.dll` and dependencies (MIT), plus synthetic sample logs. |
| 🧪 | `Fixtures/Web/serverinfo.json` | A synthetic copy of LMP's website JSON (players, vessels, subspaces). |

## 📚 Glossary

| Term | Meaning |
|---|---|
| **LMP** | Luna Multiplayer, the KSP multiplayer mod. Its server is `Server.exe`. |
| **LUNA EVO** | LUNA Evolved: our project to improve LMP. This app is its first piece. |
| **Schema** | The list of files and settings, with types and defaults, read from `Server.dll`. |
| **Session** | `ConfigSession`: one opened server, its schema, and its loaded files. |
| **Dirty** | A file with unsaved changes. |
| **Preset** | A `Set*()` method on an LMP settings class, e.g. GameplaySettings `SetHard()`. |
| **Inactive** | A setting that has no effect right now (e.g. Red while rainbow is on); shown greyed out. |
| **Fixture** | Test input data checked into the repo (`tests/Fixtures`). |
| **Headless** | Running the real Avalonia UI without a screen, for tests. |
| **Universe** | `<server>/Universe`: the game state (vessels, kerbals, scenarios). |
| **Live info** | LMP's website JSON (TCP 8900): players, vessels, settings. Read by the Players and Join pages. |
| **Digest** | The SHA-256 that GitHub publishes for each release file; we compare downloads against it. |
| **GameData** | KSP's mod folder; every mod lives in a subfolder of it. |
| **ModuleManager (MM)** | A KSP mod that patches other mods' `.cfg` files at load time (`@PART[...]`). |
