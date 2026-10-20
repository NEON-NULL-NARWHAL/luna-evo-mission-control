<div align="center">

<img src="src/LunaEvo.ConfigManager/Assets/icon.png" width="96" alt="LUNA EVO icon">

# LUNA EVO Config Manager

**Set up, configure, run and watch a [Luna Multiplayer](https://github.com/LunaMultiplayer/LunaMultiplayer) (LMP) server for Kerbal Space Program, without touching a config file or a console window.**

Version 0.4.0 · Windows · .NET 10 · Avalonia 12 · GPL-3.0

</div>

> [!TIP]
> **New to the code? Start in [`Meat and Potatoes/`](Meat%20and%20Potatoes/README.md).**
> It's the maintainer's guide: a map of every folder and file with a symbol legend, how the pieces fit together,
> the LMP quirks we discovered, and step-by-step recipes for common changes.

> [!NOTE]
> **Security:** downloads are checked against GitHub's SHA-256, and the 0.4.0 audit and its fixes are in [`SECURITY.md`](SECURITY.md).

> [!NOTE]
> **This app was written with AI.** The code, tests and docs were written by Claude (Anthropic's AI model) through
> Claude Code, directed and reviewed by the project owner. See [How this app was made](#how-this-app-was-made-ai-disclosure)
> for exactly who did what and how it was checked.

![Welcome](docs/screenshots/welcome.png)

## Download

**[⬇️ Download the latest release](https://github.com/NEON-NULL-NARWHAL/luna-evo-multiplayer-config-manager/releases/latest)**:
`LunaEvo.ConfigManager.exe`, one file for Windows 10/11 (x64), nothing to install. Put it next to your LMP
`Server.exe`, or anywhere if you're setting up a new server or just joining one. The exe isn't code-signed yet, so
Windows SmartScreen may ask you to confirm the first time (**More info → Run anyway**). Each release lists the exe's
SHA-256 so you can check your download.

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
| 🔌 **Connect** | The exact addresses to give your friends (LAN, VPN/mesh, internet), whether you're on the public list, UPnP, a one-click Windows Firewall rule, whether the port is actually listening, and **Share with players** so their Join page can see live info. |

### Join a server (for players)

You don't have to host anything to use LUNA EVO. Choose **Join a friend's server** on the welcome page.

| | |
|---|---|
| 🛰️ **Check a server first** | Type a friend's address: see its IPs and what kind of network they're on (this PC, LAN, VPN/mesh, internet), your ping, and, if the host shares it, the server's name, mode, difficulty, players online and vessels in game. |
| 🚀 **Launch KSP straight into it** | One button starts KSP and LMP connects by itself. For a server with a password or a non-default port (which LMP can't take on the command line), it's put at the top of LMP's server list with the password, ready to click. |
| 📌 **My servers** | Save the servers you play on. Passwords are never stored by LUNA EVO. |

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
| 🎨 **Make it yours** | RGB colour wheel and KSP-themed presets regenerate the whole theme; dark, light or follow Windows. |
| 📦 **One file** | A single self-contained `.exe`. Drop it next to `Server.exe` and it finds the server by itself. |

### Safe by default

| | |
|---|---|
| 🔒 **Verified downloads** | LMP is only downloaded from GitHub over HTTPS and checked against the SHA-256 GitHub publishes, before it's installed. |
| 💾 **Backups everywhere** | A timestamped config backup before every save, the old server kept on every update, the Universe snapshotted before every restore, LMP's server list backed up before every change. |
| 🏠 **Private until you say so** | New servers aren't on the public list, don't touch your router, and keep live info on this PC. The Connect page explains each step before you open anything up. |
| 🤫 **No secrets kept** | LUNA EVO never stores passwords or webhooks in its own settings, and has no telemetry or account. See [`SECURITY.md`](SECURITY.md). |
| ⚠️ **Warns before it matters** | Saving while the server runs, stopping it (players get disconnected), closing with unsaved changes, settings LMP would reject. |

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
| **Log files:** every `lmpserver_*.log`, colour-coded. | **Automation:** auto-restart, daily restart, Universe backups, updates. |
| ![Join a server](docs/screenshots/join.png) | ![Join: play](docs/screenshots/join-play.png) |
| **Join a server:** network, ping and live server info for players. | **Launch KSP and join:** straight into the server, or via LMP's list. |
| ![Connect](docs/screenshots/connect.png) | ![First start](docs/screenshots/first-start.png) |
| **Connect:** what to tell your friends, and what might block them. | **First start:** LMP's own startup, inside the app. |
| ![Settings](docs/screenshots/settings.png) | ![Light theme](docs/screenshots/light.png) |
| **LUNA EVO Settings:** colour wheel, presets, theme. | **Light theme.** |

<sub>All screenshots are LUNA EVO 0.4.0 running a sandbox copy of a real LMP 0.29.2 server, with personal details removed (network addresses are replaced with examples). The setup wizard is shown step by step in [Set up a new server](#set-up-a-new-server-step-by-step).</sub>

## Getting started

Get `LunaEvo.ConfigManager.exe` from the [latest release](https://github.com/NEON-NULL-NARWHAL/luna-evo-multiplayer-config-manager/releases/latest),
or build it with `publish.ps1` (see [Build it](#build-it)). It's a single file with nothing to install.

- **No server yet?** Follow [Set up a new server](#set-up-a-new-server-step-by-step) below. It takes about two minutes.
- **Already have an LMP server?** Put the exe in your server folder, next to `Server.exe`, and double-click it.
  Edit, **Save** (Ctrl+S), then **Server Console → Start**.
- **Just playing?** Choose **Join a friend's server** on the welcome page. See [Join a friend's server](#join-a-friends-server).

### Set up a new server, step by step

**1. Start LUNA EVO and choose "Set up a new server".**
Run the exe from anywhere (your Desktop or Downloads is fine). With no server open, you get the welcome page.

![Welcome page](docs/screenshots/welcome.png)

**2. Choose where the server lives and which LMP version.**
- **Folder:** a new or empty folder, e.g. `D:\Games\LMPServer`. The server, its settings and your game universe all live here.
- **LMP version:** the newest stable release is picked for you, straight from Luna Multiplayer's GitHub.
  **Everyone who plays needs the same version of the LMP mod in their KSP**, so note the number.
- **Show pre-releases:** leave it off unless you want to test newer, less-tested versions.

![Setup wizard, step 1 and 2](docs/screenshots/wizard.png)

**3. Answer the questions about your server.**

| Question | What to pick |
|---|---|
| Server name | Anything up to 30 characters. Players see it when they connect. |
| Password | Optional. Players type it to join. |
| Game mode | **Sandbox** (everything unlocked), **Science** (tech tree) or **Career** (tech tree, money and contracts). |
| Difficulty | Easy / Normal / Moderate / Hard. You can fine-tune each rule later on the **Gameplay** page. |
| Max players | How many people can be on at once. |
| Game port | Leave it at **8800** unless you know you need another. |
| Show on the public server list | **Off** for a server for friends (they join with your address). Turn it on to be listed in LMP's in-game browser. |
| Let LMP open router ports (UPnP) | **Off** if everyone is on your network or a VPN/mesh. On to let players from the internet in (if your router allows it). |
| Check players' mods (Mod Control) | **Off** lets players use any mods. On restricts them to the parts and plugins you allow (see **Mod Control → Import from GameData**). |
| Live player info | Leave it **on**: it powers the **Players** page, and only this PC can see the data. |
| Put LUNA EVO in the server folder | Leave it **on**, so next time you just double-click LUNA EVO in that folder. |

![Setup wizard, options](docs/screenshots/wizard-options.png)

**4. Press "Download and set up".**
LUNA EVO downloads the LMP server, unpacks it, and writes your settings **before** the server ever starts, so nothing
is public until you said so. If this PC is missing the **.NET 6 Runtime** LMP needs, it offers to install it (Windows
will ask for permission). When it's done, the new server opens on the **Server Console** page.

![The new server, ready to start](docs/screenshots/wizard-opened.png)

**5. Press Start.**
The console fills with LMP's startup messages. When you see **"All systems up and running"**, the server is ready.
The yellow dot in the rail means it's running. To stop it, press **Stop**: LMP saves the universe as it shuts down.

![First start](docs/screenshots/first-start.png)

**6. Tell your friends how to join.**
Open **Connect**. It lists the addresses to share (on your network, VPN or mesh: copy one of these), whether you're on
the public list, and whether anything is blocking players. If Windows Firewall might block them, press
**Allow through firewall** (Windows asks for permission once). Your friends then pick **Add server** in LMP's
in-game server list and enter that address and port.

![Connect page](docs/screenshots/connect.png)
<sub>The address shown is an example; your own is filled in automatically. Your internet address stays hidden until you press Show.</sub>

**7. Make it yours (optional).**
Everything else is on the pages in the rail: **General** and **Gameplay** for the rules, **Mod Control** to import your
mods, **Profiles & Backups** to save setups and snapshot the universe, and **LUNA EVO → Settings** for automatic
restarts, scheduled backups and the app's colours. Remember to **Save**, then restart the
server for changes to take effect.

**What your players need:** KSP 1.12 with the **LMP client mod of the same version** as your server. It's on the same
[LMP releases page](https://github.com/LunaMultiplayer/LunaMultiplayer/releases) (`LunaMultiplayer-Client-Release.zip`).

### Join a friend's server

1. Start LUNA EVO and choose **Join a friend's server** (or **Play → Join a server** in the rail).
2. Type the address your host gave you (e.g. `192.168.1.20` or `100.101.102.103:8800`) and press **Check server**.
   You see where it is and your ping. Server info, players and vessels appear if the host pressed **Share with
   players** on their Connect page.
3. Check the **Kerbal Space Program** folder (found automatically for Steam) and that your LMP version matches the server's.
4. Type the password if there is one, and press **Launch KSP and join**.
   - Default port (8800), no password: KSP starts and LMP connects by itself.
   - Otherwise: the server is at the top of LMP's server list with your password filled in. Click it and connect.
     (LMP's own command-line option can't handle a port or a password; we plan to fix that in the LUNA EVO fork.)
5. **Save to my servers** to skip typing next time.

LMP 0.29.x itself needs the [.NET 6 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0); LUNA EVO checks for it and can install it for you (via Windows' `winget`).

> [!IMPORTANT]
> LMP reads its config only when it starts, and rewrites the files as it does. Save your changes, *then* (re)start the server.
> LUNA EVO warns you if you save while it's running.

### What it connects to

Everything is local except, and only when you use them:
**GitHub** (LMP's release list and downloads, for the wizard and update check; the update check can be turned off),
**LMP's own website on this PC** (for the Players page), and **the server you type on the Join page** (a DNS lookup,
4 pings and its LMP website). Details are in [`SECURITY.md`](SECURITY.md).

### Build it

Requirements: Windows, the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build                     # build everything
dotnet test                      # 171 tests: core logic and the real UI, headless; no network, no real server
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
│   │                                    setup & updates, live data, GameData scanning, automation, joining, joining
│   └── LunaEvo.ConfigManager/        🖥️ the Avalonia desktop app
├── tests/
│   ├── LunaEvo.ConfigManager.Core.Tests/   🧪 logic tests (113)
│   ├── LunaEvo.ConfigManager.Tests/        🧪 UI tests: the real window, driven headlessly (58)
│   ├── FakeLmpServer/                      🧪 a pretend Server.exe so server control can be tested safely
│   └── Fixtures/                           🧪 sanitized real configs, LMP 0.29.2's Server.dll, sample logs and website JSON
├── docs/screenshots/
├── publish.ps1               📦 builds the single-file exe
├── SECURITY.md               🔒 what LUNA EVO touches, the audit, how to report a problem
└── CHANGELOG.md
```

The full file-by-file map is in [`Meat and Potatoes/README.md`](Meat%20and%20Potatoes/README.md).

## How this app was made (AI disclosure)

We want to be completely clear about this.

**Who did what**

| The project owner (human) | Claude (AI), via Claude Code in VS Code |
|---|---|
| Set the goals: a GUI config manager, then server control, theming, this repo, the 0.3 feature set (wizard, players, mod import, automation...), and 0.4 (a security audit, the Join page, removing Discord). Found the first two security issues. | Read LMP's source to learn how settings, logging, commands, shutdown, releases, the web API and mod checks really work. |
| Made the decisions: desktop app, Avalonia, .NET 10, private repo, GPL-3.0, the look (inspired by [NOMM](https://github.com/Combat787/NOMM)). | Wrote all of the code: the C#, XAML and build scripts. |
| Supplied the real server, its configs and the requirements. | Wrote all 171 automated tests, a fake LMP server, and fakes for every network call. |
| Reviewed the results and asked for changes. | Ran the tests, checked the real thing against real LMP (below), took the screenshots, and wrote these docs. |

- **Model and tool:** Claude Opus 5.5 by Anthropic, working in Claude Code (an AI coding agent that can read files, run commands and build/test the code) inside VS Code.
- **When:** October 2026, in one long working session.
- **How much:** essentially every line in this repo was written by the AI. The human role was direction, decisions, review and testing.

**How we checked it**

AI-written code can look right and still be wrong, so nothing here is trusted on looks:

- **171 automated tests.** Byte-for-byte file round trips; every validation rule; generated and edited configs deserialized by **LMP's own classes** exactly as the server loads them; install/update from zips (including refusing to overwrite your config or escape the folder); a fake server that really starts, takes commands, crashes and stops; and UI tests that click through **every tab and control** plus every new page.
- **Against the real thing:**
  - The wizard downloaded the real LMP 0.29.2 from GitHub, and the real server started on the generated config **with no errors**.
  - The Players page read the real server's live JSON. A Universe backup was taken while it ran, and it stopped cleanly.
  - The Join page checked a real running server (resolve, ping, live info) and found a real KSP install with LMP 0.29.2.
  - The download checksum was checked against the real LMP 0.29.2 release's published SHA-256.
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
