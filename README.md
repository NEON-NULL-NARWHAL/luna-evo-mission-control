<div align="center">

<img src="src/LunaEvo.ConfigManager/Assets/icon.png" width="96" alt="LUNA EVO icon">

# LUNA EVO Config Manager

**Set up, configure, run and watch a [Luna Multiplayer](https://github.com/LunaMultiplayer/LunaMultiplayer) (LMP) server for Kerbal Space Program, without touching a config file or a console window.**

Version 0.3.0 · Windows · .NET 10 · Avalonia 12 · GPL-3.0

</div>

> [!TIP]
> **New to the code? Start in [`Meat and Potatoes/`](Meat%20and%20Potatoes/README.md).**
> It's the maintainer's guide: a map of every folder and file with a symbol legend, how the pieces fit together,
> the LMP quirks we discovered, and step-by-step recipes for common changes.

> [!NOTE]
> **This app was written with AI.** The code, tests and docs were written by Claude (Anthropic's AI model) through
> Claude Code, directed and reviewed by the project owner. See [How this app was made](#how-this-app-was-made-ai-disclosure)
> for exactly who did what and how it was checked.

![Welcome](docs/screenshots/welcome.png)

## What it does

LUNA EVO is the first project of **LUNA Evolved**: two friends improving Luna Multiplayer. Hosting an LMP server
normally means downloading a zip, installing the right .NET runtime, hand-editing 13 XML files, and keeping a
console window open. This app does all of that for you, and adds what a server host actually wants day to day.

### Get a server running

| | |
|---|---|
| 🧙 **Setup wizard** | Pick a folder and answer a few questions (name, password, game mode, difficulty, players, port). LUNA EVO downloads the LMP server from GitHub, writes the config, checks for (and can install) the .NET runtime it needs, and opens it. Private by default: not on the public list, no router changes, until you choose. |
| ⬆️ **LMP updates** | Tells you when a newer LMP server is out and updates it in one click, keeping your Config, Universe, logs and Plugins, and saving the old version first. |
| ▶️ **Server Console** | Start, stop and restart, with a live colour-coded console, filters, search, commands with history and quick buttons, and a log file browser. Stops are **clean** (LMP backs up the universe), never a kill. |
| 👥 **Players** | Who's online, their vessels and warp subspaces, with **Kick** and **Ban** buttons (reason optional). |
| 🔌 **Connect** | The exact addresses to give your friends (LAN, VPN/mesh, internet), whether you're on the public list, UPnP, a one-click Windows Firewall rule, and whether the port is actually listening. |

### Configure it

| | |
|---|---|
| ⚙️ **Every setting, in a GUI** | Each of LMP's 13 config files gets a page; every setting gets the right control and LMP's own description. |
| 🔍 **Always matches your server** | The settings list is read **from your server's own `Server.dll`**, so new or renamed LMP settings appear automatically. |
| ✅ **Validation** | Limits from LMP's descriptions plus the rules it only mentions in passing (heartbeat < timeout, port clashes, case-sensitive values). Problems show inline and block saving. |
| 🧾 **Review before saving** | See every change with old → new values before anything is written. Every save makes a timestamped backup. |
| 🌗 **Greys out what doesn't apply** | Rainbow colours, Career-only funds/reputation, Career-and-Science science settings. |
| 🎯 **Knows LMP's hidden behaviour** | Gameplay defaults follow your difficulty preset, with a warning before LMP silently switches you to *Custom*. |
| 🧩 **Mod control, done for you** | **Import from GameData**: point it at your KSP install and it fills the allowed parts, resources, plugins (with SHA-256) and expansions, the same way LMP's in-game generator does. Every list is editable, including the plugin and mandatory-part lists. |

### Keep it running

| | |
|---|---|
| 🔄 **Auto-restart** | Restarts the server after a crash (and gives up if it keeps crashing), plus an optional daily restart with an in-game one-minute warning. |
| 🌌 **Universe backups** | Dated snapshots of the game state, on demand or every few hours, with one-click restore (which backs up first, so it's undoable). |
| 🗂️ **Config profiles** | Save the whole setup as "Career campaign" or "Sandbox chaos night" and switch in one click. |
| 💬 **Discord** | Posts to your Discord channel when the server starts, stops or crashes, and when players join or leave. |
| 🎨 **Make it yours** | RGB colour wheel and KSP-themed presets regenerate the whole theme; dark, light or follow Windows. |
| 📦 **One file** | A single self-contained `.exe`. Drop it next to `Server.exe` and it finds the server by itself. |

## Screenshots

| | |
|:---:|:---:|
| ![Setup wizard](docs/screenshots/wizard.png) | ![Server console](docs/screenshots/console.png) |
| **Setup wizard:** a few questions, then LUNA EVO downloads and configures LMP. | **Server Console:** the real LMP server running inside the app. |
| ![Players](docs/screenshots/players.png) | ![Profiles and backups](docs/screenshots/profiles.png) |
| **Players:** live from LMP's own data, refreshed every 5 seconds. | **Profiles & Backups:** switch whole configs; snapshot and restore the Universe. |
| ![Import from GameData](docs/screenshots/mod-import.png) | ![Plugin lists](docs/screenshots/mod-lists.png) |
| **Import from GameData:** fill mod control from a KSP install. | **Plugin lists:** LMP's DllFile entries, editable. |
| ![Validation](docs/screenshots/validation.png) | ![Gameplay](docs/screenshots/gameplay.png) |
| **Validation:** errors inline, a badge on the page, the count in the status area. | **Gameplay:** Career-only settings greyed out in Sandbox, with the reason. |
| ![Log files](docs/screenshots/logfiles.png) | ![Automation settings](docs/screenshots/settings-automation.png) |
| **Log files:** every `lmpserver_*.log`, colour-coded. | **Automation:** auto-restart, daily restart, Universe backups, Discord, updates. |
| ![Settings](docs/screenshots/settings.png) | ![Light theme](docs/screenshots/light.png) |
| **LUNA EVO Settings:** colour wheel, presets, theme. | **Light theme.** |

<sub>Screenshots are from sandbox copies of a real LMP 0.29.2 server with personal details removed. The Connect page isn't shown because it displays real network addresses.</sub>

## Getting started

### Use it

1. Get `LunaEvo.ConfigManager.exe` (from a release, or build it with `publish.ps1`, see below).
2. **No server yet?** Run it anywhere and press **Set up a new server**.
   **Have one?** Put the exe in your LMP server folder, next to `Server.exe`, and double-click it.
3. Edit, **Save** (Ctrl+S), then **Server Console → Start**. Share an address from **Connect** with your friends.

LMP 0.29.x itself needs the [.NET 6 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0); LUNA EVO checks for it and can install it for you (via Windows' `winget`).

> [!IMPORTANT]
> LMP reads its config only when it starts, and rewrites the files as it does. Save your changes, *then* (re)start the server.
> LUNA EVO warns you if you save while it's running.

### What it connects to

Everything is local except, and only when you use them:
**GitHub** (LMP's release list and downloads, for the wizard and update check; the update check can be turned off),
**your Discord webhook** (if you set one), and **LMP's own website on this PC** (for the Players page).

### Build it

Requirements: Windows, the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build                     # build everything
dotnet test                      # 129 tests: core logic and the real UI, headless; no network, no real server
.\publish.ps1                    # -> dist\LunaEvo-ConfigManager-<version>-win-x64\LunaEvo.ConfigManager.exe
```

Run from source against the copies in `tests/Fixtures` (no real server needed):

```powershell
dotnet run --project src/LunaEvo.ConfigManager --launch-profile "Fixtures (in repo)"
```

Command-line options: `--server <folder>` (contains Server.dll), `--config <folder>` (defaults to `<server>\Config`),
`--settings <file>` (LUNA EVO's own preferences, for portable use; defaults to `%APPDATA%\LunaEvo\ConfigManager\settings.json`).

## Repository layout

```
ConfigManager/
├── Meat and Potatoes/        📖 the maintainer's guide: start here
├── src/
│   ├── LunaEvo.ConfigManager.Core/   🧠 all the logic, no UI: schema, XML editing, validation, server control,
│   │                                    setup & updates, live data, GameData scanning, automation
│   └── LunaEvo.ConfigManager/        🖥️ the Avalonia desktop app
├── tests/
│   ├── LunaEvo.ConfigManager.Core.Tests/   🧪 logic tests (78)
│   ├── LunaEvo.ConfigManager.Tests/        🧪 UI tests: the real window, driven headlessly (51)
│   ├── FakeLmpServer/                      🧪 a pretend Server.exe so server control can be tested safely
│   └── Fixtures/                           🧪 sanitized real configs, LMP 0.29.2's Server.dll, sample logs and website JSON
├── docs/screenshots/
├── publish.ps1               📦 builds the single-file exe
└── CHANGELOG.md
```

The full file-by-file map is in [`Meat and Potatoes/README.md`](Meat%20and%20Potatoes/README.md).

## How this app was made (AI disclosure)

We want to be completely clear about this.

**Who did what**

| The project owner (human) | Claude (AI), via Claude Code in VS Code |
|---|---|
| Set the goals: a GUI config manager, then server control, theming, this repo, and the 0.3 feature set (wizard, players, mod import, automation...). | Read LMP's source to learn how settings, logging, commands, shutdown, releases, the web API and mod checks really work. |
| Made the decisions: desktop app, Avalonia, .NET 10, private repo, GPL-3.0, the look (inspired by [NOMM](https://github.com/Combat787/NOMM)). | Wrote all of the code: the C#, XAML and build scripts. |
| Supplied the real server, its configs and the requirements. | Wrote all 129 automated tests, a fake LMP server, and fakes for every network call. |
| Reviewed the results and asked for changes. | Ran the tests, checked the real thing against real LMP (below), took the screenshots, and wrote these docs. |

- **Model and tool:** Claude Opus 5.5 by Anthropic, working in Claude Code (an AI coding agent that can read files, run commands and build/test the code) inside VS Code.
- **When:** October 2026, in one long working session.
- **How much:** essentially every line in this repo was written by the AI. The human role was direction, decisions, review and testing.

**How we checked it**

AI-written code can look right and still be wrong, so nothing here is trusted on looks:

- **129 automated tests.** Byte-for-byte file round trips; every validation rule; generated and edited configs deserialized by **LMP's own classes** exactly as the server loads them; install/update from zips (including refusing to overwrite your config or escape the folder); a fake server that really starts, takes commands, crashes and stops; and UI tests that click through **every tab and control** plus every new page.
- **Against the real thing:**
  - The wizard downloaded the real LMP 0.29.2 from GitHub, and the real server started on the generated config **with no errors**.
  - The Players page read the real server's live JSON. A Universe backup was taken while it ran, and it stopped cleanly.
  - The GameData import scanned a real modded KSP install (4,382 `.cfg` files): 1,527 parts, 132 plugins with hashes, and every stock part that still exists in the game.
- **Visual checks:** every page was screenshotted and reviewed during development.

The tests caught real mistakes along the way, which is why they exist. Examples: Windows passing an "ignore Ctrl+C"
flag down to every later server, LMP's files claiming `utf-16` while being UTF-8, a background thread touching a UI
control, and an update message being hidden by the update offer. They're documented in
[`Meat and Potatoes/07-LMP-Gotchas.md`](Meat%20and%20Potatoes/07-LMP-Gotchas.md) and
[`Meat and Potatoes/09-How-It-Was-Built.md`](Meat%20and%20Potatoes/09-How-It-Was-Built.md).

**What that means for you:** treat this like any open-source project. Read the code, run the tests, and keep the backups
it makes. The exe isn't code-signed, so Windows SmartScreen may warn the first time you run it.

## Credits and licences

- **LUNA EVO Config Manager**: GPL-3.0, see [LICENSE](LICENSE).
- **[Luna Multiplayer](https://github.com/LunaMultiplayer/LunaMultiplayer)** by Gabriel Vazquez and contributors: MIT. LUNA EVO contains no LMP source code; it reads your server's `Server.dll` at runtime and downloads LMP's official releases. The test fixtures include LMP 0.29.2's server binaries under their MIT licence ([`tests/Fixtures/LMPServer/LMP-LICENSE.txt`](tests/Fixtures/LMPServer/LMP-LICENSE.txt)).
- **[Avalonia UI](https://avaloniaui.net/)**: MIT.
- **[JetBrains Mono](https://github.com/JetBrains/JetBrainsMono)** font: SIL Open Font License 1.1 ([`Assets/Fonts/OFL.txt`](src/LunaEvo.ConfigManager/Assets/Fonts/OFL.txt)).
- **Material Symbols** icons by Google: Apache 2.0.
- Look and feel inspired by **[NOMM](https://github.com/Combat787/NOMM)**, the Nuclear Option Mod Manager.

Kerbal Space Program is a trademark of Take-Two Interactive. This project isn't affiliated with them or with the LMP team.
