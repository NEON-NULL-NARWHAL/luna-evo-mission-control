<div align="center">

<img src="src/LunaEvo.MissionControl/Assets/icon.png" width="96" alt="LUNA EVO icon">

# LUNA EVO Mission Control

**Everything for [Luna Multiplayer](https://github.com/LunaMultiplayer/LunaMultiplayer) (LMP) in Kerbal Space Program, in one app:
host and run your own server, find public ones, get your game modded with CKAN, and launch straight into a server.**

[![Latest release](https://img.shields.io/github/v/release/NEON-NULL-NARWHAL/luna-evo-mission-control?label=download)](https://github.com/NEON-NULL-NARWHAL/luna-evo-mission-control/releases/latest)
[![Tests](https://github.com/NEON-NULL-NARWHAL/luna-evo-mission-control/actions/workflows/tests.yml/badge.svg)](https://github.com/NEON-NULL-NARWHAL/luna-evo-mission-control/actions/workflows/tests.yml)
![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-blue)
[![License: GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-green)](LICENSE)

**Version 2.9.3** · [What's new](WHATS-NEW.md) · Windows 10/11 · .NET 10 · Avalonia 12 · GPL-3.0

**[⚡ Quickstart](QUICKSTART.md)** · **[Download](#download)** · **[Features](#what-it-does)** · **[Screenshots](docs/SCREENSHOTS.md#screenshots)** · **[Host a server](docs/HOSTING.md#set-up-a-new-server-step-by-step)** · **[Find and join](docs/PLAYING.md#join-a-friends-server)** · **[Mods with CKAN](docs/PLAYING.md#using-the-ckan-page)** · **[No open ports](docs/PLAYING.md#play-together-without-opening-any-ports-tailscale-twingate)** · **[For developers](docs/DEVELOPERS.md)**

</div>

> [!NOTE]
> **Formerly LUNA EVO Config Manager.** It outgrew configs, so 2.0 renamed it. Already have it? It updates itself to
> Mission Control and keeps your settings.

> [!NOTE]
> **Security:** every download is checked against GitHub's SHA-256. All twelve security audits, their fixes, and what each
> release was tested against are in [`SECURITY.md`](SECURITY.md).
> **Written with AI:** the code, tests and docs were written by Claude (Anthropic's AI model), directed and reviewed by
> the project owner. [Who did what, and how it was checked.](docs/AI-DISCLOSURE.md#how-this-app-was-made-ai-disclosure) Every decision the owner
> made, and what was done about it, is in [`DECISIONS.md`](DECISIONS.md).

## At a glance

In a hurry? **[⚡ Quickstart](QUICKSTART.md)** gets you hosting, playing or modded in a few steps, with links back to
the full guides below.

| | | |
|---|---|---|
| 🧙 **A server in two minutes**<br>Answer a few questions; LUNA EVO downloads LMP, writes a private config and installs what it needs. | ▶️ **Run it from one window**<br>Start, stop, a live console, and cards you click to change the name, port or player limit on the spot. | ⚙️ **Every setting, no XML**<br>All 13 LMP config files as a GUI, with an Essentials tab, search, validation and a backup on every save. |
| 🌍 **Find a server**<br>Browse every public LMP server, filtered to your LMP version, then join one in a click. | 🚀 **Straight into the game**<br>Launch KSP and LMP connects by itself, with your password and CKAN's launch options. | 🧰 **Mods, sorted**<br>Install a friend's CKAN mod list, check your mods against a server's rules, share yours. |
| 👥 **Know your players**<br>Who's online, history, kick and ban, alerts for password guessing. | 🏠 **Leave it running**<br>Auto-restart, daily restarts, scheduled backups, chat announcements, home server mode. | 🔒 **Private by default**<br>Not public, no router changes and no telemetry until you choose. Simple mode for new hosts. |

![Server console](docs/screenshots/console-cards.png)

## Download

**[⬇️ Download the latest release](https://github.com/NEON-NULL-NARWHAL/luna-evo-mission-control/releases/latest)**:
`LunaEvo.MissionControl.exe`, one file for Windows 10/11 (x64), nothing to install. Put it next to your LMP
`Server.exe`, or anywhere if you're setting up a new server or just playing.

- **You only download it once.** It tells you when there's a new version and updates itself in one click
  (App settings → Updates), checking the download against GitHub's SHA-256.
- The exe isn't code-signed yet, so Windows SmartScreen may ask the first time (**More info → Run anyway**). Each
  release lists its SHA-256 so you can check your download.
- `LunaEvo.ConfigManager.exe` on a release is the same file under the old name, so copies from before 2.0 keep updating.

## What it does

LUNA EVO Mission Control is the app of **LUNA Evolved**, two friends improving Luna Multiplayer. Hosting an LMP
server normally means downloading a zip, installing the right .NET runtime, hand-editing 13 XML files and keeping a
console window open; playing means finding a server, matching its LMP version and mods, and typing addresses into
LMP's list. Mission Control does all of that, for hosts and players alike.

| | |
|---|---|
| 🏠 **[Host a server](docs/FEATURES.md#host-a-server)** | Setup wizard, a live console, players, automation, backups. |
| 🎮 **[Find, mod and join](docs/FEATURES.md#find-mod-and-join-for-players)** | Server browser, CKAN, mod checks, launch straight into a server, a map of the server's vessels. |
| ⚙️ **[Configure it](docs/FEATURES.md#configure-it)** | All 13 LMP config files as a GUI, validated, with backups. |
| 🔁 **[Keep it running](docs/FEATURES.md#keep-it-running)** | Auto-restart, daily restarts, alerts, home server mode. |
| 🩹 **[Fixes for LMP itself](docs/FEATURES.md#fixes-for-lmp-itself)** | LUNA EVO patches and server plugin: less lag, fewer lost updates, an in-game window. |
| 🔒 **[Safe by default](docs/FEATURES.md#safe-by-default)** | Private, no telemetry, verified downloads. |

## Screenshots

| | |
|:---:|:---:|
| ![Server console](docs/screenshots/console.png) | ![Join a server](docs/screenshots/join.png) |
| **Server console:** the real LMP server running inside the app. | **Join a server:** network, ping and live server info. |
| ![Essentials](docs/screenshots/essentials.png) | ![Solar system map](docs/screenshots/map.png) |
| **Essentials:** the settings most servers need, first. | **Solar system map:** your server's vessels on their orbits. |
| ![CKAN page](docs/screenshots/ckan.png) | ![Setup wizard](docs/screenshots/wizard.png) |
| **CKAN mods:** check and install a friend's mod list. | **Setup wizard:** a few questions, then LMP is ready. |

**[All screenshots →](docs/SCREENSHOTS.md)**

## Getting started

Get `LunaEvo.MissionControl.exe` from the [latest release](https://github.com/NEON-NULL-NARWHAL/luna-evo-mission-control/releases/latest),
or build it with `publish.ps1` (see [Build it](docs/DEVELOPERS.md#build-it)).

- **No server yet?** [Set up a new server](docs/HOSTING.md#set-up-a-new-server-step-by-step), about two minutes.
- **Already have an LMP server?** Put the exe next to `Server.exe` and double-click it. Edit, **Save** (Ctrl+S), then
  **Server console → Start**.
- **Just playing?** [Find a public server](docs/PLAYING.md#find-a-public-server-server-browser) or [join a friend's](docs/PLAYING.md#join-a-friends-server).
- **Need mods?** [Using the CKAN page](docs/PLAYING.md#using-the-ckan-page).
- **Playing over the internet?** Use a mesh VPN and never open a port: [Tailscale or Twingate](docs/PLAYING.md#play-together-without-opening-any-ports-tailscale-twingate).
- **New to hosting?** Turn on **Simple mode** (App settings → Behavior) to see only what you need.

> [!IMPORTANT]
> LMP reads its config only when it starts, and rewrites the files as it does. Save your changes, *then* (re)start the
> server. LUNA EVO warns you if you save while it's running.

## Guides

| | |
|---|---|
| 🧭 **[Quickstart](QUICKSTART.md)** | Hosting, playing or modded in a few steps. |
| 🏠 **[Hosting a server](docs/HOSTING.md)** | A new LMP server, step by step. |
| 🎮 **[Playing](docs/PLAYING.md)** | Server browser, joining, CKAN mods, playing without opening ports. |
| 📋 **[Everything it does](docs/FEATURES.md)** | The full feature list, what it's tested with, and what it connects to. |
| 🩹 **[The LUNA EVO patches](Meat%20and%20Potatoes/27-Client-Patches.md)** | Fixes for LMP itself, each measured in a real game; the in-game LUNA EVO window. |
| 🆕 **[What's new](WHATS-NEW.md)** · **[Changelog](CHANGELOG.md)** | What each version brought. |
| 🔒 **[Security](SECURITY.md)** · **[Decisions](DECISIONS.md)** | Every audit, and every decision the owner made. |
| 🛠️ **[For developers](docs/DEVELOPERS.md)** | Building it, the repository, and the maintainer docs. |
| 🤖 **[How it was made](docs/AI-DISCLOSURE.md)** | Written with AI: who did what, and how it was checked. |

## Credits and licenses

- **LUNA EVO Mission Control**: GPL-3.0, see [LICENSE](LICENSE).
- **[Luna Multiplayer](https://github.com/LunaMultiplayer/LunaMultiplayer)** by Gabriel Vazquez and contributors: MIT. LUNA EVO contains no LMP source code; it reads your server's `Server.dll` at runtime and downloads LMP's official releases. The test fixtures include LMP 0.29.2's server binaries under their MIT license ([`tests/Fixtures/LMPServer/LMP-LICENSE.txt`](tests/Fixtures/LMPServer/LMP-LICENSE.txt)).
- **[CKAN](https://github.com/KSP-CKAN/CKAN)**: LUNA EVO downloads and runs the official `ckan.exe` (MIT); it contains no CKAN code.
- **[Avalonia UI](https://avaloniaui.net/)**: MIT.
- **[JetBrains Mono](https://github.com/JetBrains/JetBrainsMono)** font: SIL Open Font License 1.1 ([`Assets/Fonts/OFL.txt`](src/LunaEvo.MissionControl/Assets/Fonts/OFL.txt)).
- **Material Symbols** icons by Google: Apache 2.0.
- Look and feel inspired by **[NOMM](https://github.com/Combat787/NOMM)**, the Nuclear Option Mod Manager.

Kerbal Space Program is a trademark of Take-Two Interactive. This project isn't affiliated with them, the LMP team or the CKAN team.
