<div align="center">

<img src="src/LunaEvo.ConfigManager/Assets/icon.png" width="96" alt="LUNA EVO icon">

# LUNA EVO Config Manager

**Host, find and join [Luna Multiplayer](https://github.com/LunaMultiplayer/LunaMultiplayer) (LMP) servers for Kerbal Space Program: set up and run your own server without touching a config file or a console window, browse public servers, and get your game modded with CKAN and straight into a server.**

[![Latest release](https://img.shields.io/github/v/release/NEON-NULL-NARWHAL/luna-evo-multiplayer-config-manager?label=download)](https://github.com/NEON-NULL-NARWHAL/luna-evo-multiplayer-config-manager/releases/latest)
[![Tests](https://github.com/NEON-NULL-NARWHAL/luna-evo-multiplayer-config-manager/actions/workflows/tests.yml/badge.svg)](https://github.com/NEON-NULL-NARWHAL/luna-evo-multiplayer-config-manager/actions/workflows/tests.yml)
![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-blue)
[![License: GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-green)](LICENSE)

**Version 1.4.0** · Windows 10/11 · .NET 10 · Avalonia 12 · GPL-3.0

**[Download](#download)** · **[Features](#what-it-does)** · **[Screenshots](#screenshots)** · **[Host a server](#set-up-a-new-server-step-by-step)** · **[Join a friend](#join-a-friends-server)** · **[No open ports (Tailscale)](#play-together-without-opening-any-ports-tailscale-twingate)** · **[For developers](#for-developers)**

</div>

> [!TIP]
> **New to the code? Start in [`Meat and Potatoes/`](Meat%20and%20Potatoes/README.md).**
> It's the maintainer's guide: a map of every folder and file with a symbol legend, how the pieces fit together,
> the LMP quirks we discovered, and step-by-step recipes for common changes.

> [!NOTE]
> **Security:** downloads are checked against GitHub's SHA-256. All eight security audits (0.4.0 to 1.4.0), their fixes,
> and the versions of LMP, KSP and CKAN each release was tested with are in [`SECURITY.md`](SECURITY.md), newest first.

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

**LUNA EVO 1.4: server browser, simple mode, quick edits.** **You only download it once.** From 0.6, LUNA EVO tells you when there's a new version and updates itself with one
click (App settings → Updates): it checks the download against GitHub's SHA-256, swaps the exe and restarts.

## What it does

LUNA EVO is the first project of **LUNA Evolved**: two friends improving Luna Multiplayer. Hosting an LMP server
normally means downloading a zip, installing the right .NET runtime, hand-editing 13 XML files, and keeping a
console window open. This app does all of that for you, and adds what a server host actually wants day to day.

### Get a server running

| | |
|---|---|
| 🧙 **Setup wizard** | Pick a folder and answer a few questions (name, password, game mode, difficulty, players, port). LUNA EVO downloads the LMP server from GitHub, writes the config, checks for (and can install) the .NET runtime it needs, and opens it. Private by default: not on the public list, no router changes, until you choose. |
| ⬆️ **LMP updates** | Tells you when a newer LMP server is out and updates it in one click, keeping your Config, Universe, logs and Plugins, and saving the old version first. |
| 🧭 **Simple mode** | One switch (App settings → Behavior) for new hosts: only the Server console, Connect, the **Essentials** settings, Join and CKAN. Everything else is a switch away. |
| ▶️ **Server console** | **Cards at the top** (server name, game port, max players...) you click to change right there, saved with a backup. Start, stop and restart, with a live color-coded console, filters, search, commands with history and quick buttons, and a log file browser. Stops are **clean** (LMP backs up the universe), never a kill. Live **memory and CPU** of the server while it runs. |
| 👥 **Players** | Who's online, their vessels and warp subspaces, with **Kick** and **Ban** buttons (reason optional). Click a card (players, vessels, asteroids) for the list of just those. |
| 📜 **Player history** | Everyone who's played: last seen, sessions, time played, LMP version, and your own notes. Built from LMP's own log and live data; IP addresses are never kept. |
| 🚨 **Alerts** | Someone guessing the admin password (LMP doesn't limit guesses) or a banned player trying to get back in shows at the top of the Players page and as a notification. |
| 🚫 **Bans** | LMP's ban list with names instead of ids, and **Unban** (LMP has no unban command). |
| 🔔 **Notifications** | Windows notifications and an optional Discord channel when the server starts, stops or crashes, for alerts, and (if you like) players joining and leaving. The Discord webhook is stored encrypted and posts can never ping @everyone. |
| 🔌 **Connect** | The exact addresses to give your friends (LAN, VPN/mesh, internet), whether you're on the public list, UPnP, a one-click Windows Firewall rule, whether the port is actually listening, **Share with players** so their Join page can see live info, and **Copy invite**: a ready-to-send message friends paste straight into their Join page. |

### Join a server (for players)

You don't have to host anything to use LUNA EVO. Choose **Join a friend's server** on the welcome page.

| | |
|---|---|
| 🌍 **Server browser** | Every public LMP server, the same list LMP shows in game: search, only your LMP version, no password, has players. **Join** checks it and launches KSP into it. |
| 🛰️ **Check a server first** | Type a friend's address: see its IPs and what kind of network they're on (this PC, LAN, VPN/mesh, internet), your ping, and, if the host shares it, the server's name, mode, difficulty, players online and vessels in game. |
| 🚀 **Launch KSP straight into it** | One button starts KSP and LMP connects by itself. For a server with a password or a non-default port (which LMP can't take on the command line), it's put at the top of LMP's server list with the password, ready to click. |
| 🧩 **Check my mods** | Compares your GameData with the server's mod rules exactly the way LMP will when you connect: banned mods, mods it doesn't allow, missing or different-version required mods, missing parts and expansions, and parts the server would drop. Hosting it yourself? **Allow them on my server** adds the missing parts to your allowed list (or explains turning Mod Control off instead). |
| 📌 **My servers** | Save the servers you play on. Passwords are never stored by LUNA EVO. |
| 🧰 **CKAN and the LMP client, managed** | A CKAN page shows whether CKAN is installed and used, lists your CKAN installs, **exports** their mod lists, and **checks** and **installs a pasted mod list** (a `.ckan` modpack or mod names) with CKAN, using its own verified copy or yours, with CKAN's errors in plain words. App settings → Downloads installs the right LMP client into any KSP install. Launching uses CKAN's own options, then connects. |
| 🚀 **Share crafts** | The **Crafts** page lists your KSP craft designs and the server's craft library. Export one to a `.lunacraft` file for friends; import theirs into your save or the server, with a check of which parts (mods) you're missing. |

### Configure it

| | |
|---|---|
| ⭐ **Essentials first** | Server settings opens with an **Essentials** tab: the dozen settings most servers need, with LMP's descriptions. |
| ⚙️ **Every setting, in a GUI** | Each of LMP's 13 config files gets a page; every setting gets the right control and LMP's own description. |
| 🔎 **Search every setting** | Type "heartbeat" or "password" and jump straight to it, whichever tab it's on. |
| 🔍 **Always matches your server** | The settings list is read **from your server's own `Server.dll`**, so new or renamed LMP settings appear automatically. |
| ✅ **Validation** | Limits from LMP's descriptions plus the rules it only mentions in passing (heartbeat < timeout, port clashes, case-sensitive values). Problems show inline and block saving. Short server and admin passwords get a warning, because LMP doesn't limit guesses, and one click generates a strong one. |
| 🧾 **Review before saving** | See every change with old → new values before anything is written. Every save makes a timestamped backup. |
| 🌗 **Greys out what doesn't apply** | Rainbow colors, Career-only funds/reputation, Career-and-Science science settings. |
| 🎯 **Knows LMP's hidden behavior** | Gameplay defaults follow your difficulty preset, with a warning before LMP silently switches you to *Custom*. |
| 🧩 **Mod control, done for you** | **Import from GameData**: point it at your KSP install and it fills the allowed parts, resources, plugins (with SHA-256) and expansions, the same way LMP's in-game generator does. Every list is editable, including the plugin and mandatory-part lists. **Share these rules with players** saves a copy for friends to check their mods against. |

### Keep it running

| | |
|---|---|
| 🏠 **Home server mode** | One switch for an always-on host PC: starts with Windows in the notification area, starts the server, hides instead of quitting, and keeps the PC awake while the server runs. |
| 💬 **Chat announcements** | Messages sent to the game chat on a schedule ("every 60 minutes: join our Discord"). |
| 🔄 **Auto-restart** | Restarts the server after a crash (and gives up if it keeps crashing), plus an optional daily restart with an in-game one-minute warning. |
| 🌌 **Universe backups** | Dated snapshots of the game state: on demand, every few hours, **every time the server stops**, and before every LMP update. One-click restore (which backs up first, so it's undoable). |
| 🧹 **Vessel cleanup** | See every vessel in the Universe and remove debris, asteroids or abandoned ships in a few clicks (server stopped, backed up first, crewed ships protected). The biggest performance win a host has. |
| 🗂️ **Config profiles** | Save the whole setup as "Career campaign" or "Sandbox chaos night" and switch in one click. |
| 🎨 **Make it yours** | RGB color wheel and KSP-themed presets regenerate the whole theme; dark, light or follow Windows. |
| 📦 **One file** | A single self-contained `.exe`. Drop it next to `Server.exe` and it finds the server by itself. |
| ⬆️ **Updates itself** | Checks LUNA EVO's GitHub releases on startup; **Update and restart** downloads the new version, verifies it, swaps the exe and restarts, keeping your server running if you want. |
| 🗂️ **Tidy layout** | The rail shows the main pages (Server: console, Players, Vessels, Connect, Server settings, Mod Control, Profiles & backups; Play: Server browser, Join, Crafts, CKAN mods; App settings), with every version in use (LUNA EVO, LMP server and client, KSP, CKAN) at the bottom, and the problem count there opens a list of what to fix. LMP's config files are tabs on one **Server settings** page, grouped Game / Network / Advanced, and the busier pages have tabs too. |

### Safe by default

| | |
|---|---|
| 🔒 **Verified downloads** | LMP is only downloaded from GitHub over HTTPS and checked against the SHA-256 GitHub publishes, before it's installed. |
| 💾 **Backups everywhere** | A timestamped config backup before every save, the old server kept on every update, the Universe snapshotted before every restore, LMP's server list backed up before every change. |
| 🌐 **Says when it's on the internet** | A banner on the Server console whenever UPnP or the public list makes the server reachable from the internet. |
| 🏠 **Private until you say so** | New servers aren't on the public list, don't touch your router, and keep live info on this PC. The Connect page explains each step before you open anything up. |
| 🤫 **No secrets in the open** | LUNA EVO never stores passwords; a Discord webhook is kept encrypted for your Windows account. No telemetry, no account. See [`SECURITY.md`](SECURITY.md). |
| 🧾 **Error log** | Anything unexpected is written to `%APPDATA%\LunaEvo\ConfigManager\logs\errors.log` instead of crashing, so a bug report can say what happened. |
| ⚠️ **Warns before it matters** | Saving while the server runs, stopping it (players get disconnected), closing with unsaved changes, settings LMP would reject. |

## Screenshots

| | |
|:---:|:---:|
| ![Setup wizard](docs/screenshots/wizard.png) | ![Server console](docs/screenshots/console.png) |
| **Setup wizard:** a few questions, then LUNA EVO downloads and configures LMP. | **Server console:** the real LMP server running inside the app. |
| ![Players](docs/screenshots/players.png) | ![Player history and bans](docs/screenshots/players-history.png) |
| **Players:** live from LMP's own data, with alerts for admin-password guessing and banned players. | **Player history and bans:** sessions, time played, notes; unban by name. |
| ![Check my mods](docs/screenshots/join-mods.png) | ![Updates](docs/screenshots/settings-updates.png) |
| **Check my mods:** what LMP will refuse, before you join. | **Updates:** LUNA EVO updates itself from its GitHub releases. (Taken while the repository was still private, hence the message.) |
| ![Crafts](docs/screenshots/crafts.png) | ![Settings search](docs/screenshots/search.png) |
| **Crafts:** export your designs, import friends', with a missing-parts check. | **Search:** find any setting across the tabs. |
| ![Server health](docs/screenshots/console-health.png) | ![Home server mode](docs/screenshots/home-server.png) |
| **Server health:** memory, CPU and threads of the running server. | **Home server mode:** one switch for an always-on host PC. |
| ![Chat announcements](docs/screenshots/announcements.png) | ![Vessel cleanup](docs/screenshots/vessels.png) |
| **Chat announcements:** messages to game chat on a schedule. | **Vessel cleanup:** debris selected, crewed ships protected, backup first. |
| ![Server console cards](docs/screenshots/console-cards.png) | ![Essentials](docs/screenshots/essentials.png) |
| **Server console:** click a card to change it; every version in use at the bottom left. | **Essentials:** the settings most servers need, first. |
| ![CKAN page](docs/screenshots/ckan.png) | ![Join with CKAN](docs/screenshots/join-ckan.png) |
| **CKAN:** is it installed, your CKAN installs, and install a mod list. | **Join + CKAN:** pick a CKAN install; launch with CKAN's own options. |
| ![Downloads](docs/screenshots/downloads.png) | |
| **Downloads:** the LMP client into any KSP install, any version. | |
| ![Setup wizard, extras](docs/screenshots/wizard-extras.png) | |
| **Setup wizard, step 3:** also get CKAN and the matching LMP client. | |
| ![Profiles and backups](docs/screenshots/profiles.png) | ![A Windows notification](docs/screenshots/notification-toast.png) |
| **Profiles & backups:** switch whole configs; snapshot and restore the Universe. | **A Windows notification** from LUNA EVO. |
| ![Import from GameData](docs/screenshots/mod-import.png) | ![Plugin lists](docs/screenshots/mod-lists.png) |
| **Import from GameData:** fill mod control from a KSP install. | **Plugin lists:** LMP's DllFile entries, editable. |
| ![Validation](docs/screenshots/validation.png) | ![Gameplay](docs/screenshots/gameplay.png) |
| **Validation:** errors inline, a badge on the tab and on Server settings, the count in the status area. | **Server settings:** one page, a tab per LMP config file. Career-only settings grayed out in Sandbox, with the reason. |
| ![Log files](docs/screenshots/logfiles.png) | ![Automation settings](docs/screenshots/settings-automation.png) |
| **Log files:** every `lmpserver_*.log`, color-coded. | **Automation & alerts:** auto-restart, daily restart, Universe backups, Windows and Discord notifications. |
| ![Join a server](docs/screenshots/join.png) | ![Join: play](docs/screenshots/join-play.png) |
| **Join a server:** network, ping and live server info for players. | **Launch KSP and join:** straight into the server, or via LMP's list. |
| ![Connect](docs/screenshots/connect.png) | ![First start](docs/screenshots/first-start.png) |
| **Connect:** what to tell your friends, and what might block them. | **First start:** LMP's own startup, inside the app. |
| ![Settings](docs/screenshots/settings.png) | ![Light theme](docs/screenshots/light.png) |
| **App settings:** color wheel, presets, theme. | **Light theme.** |

<sub>Screenshots are LUNA EVO 0.6 to 1.2 running a sandbox copy of a real LMP 0.29.2 server, with personal details removed (network addresses are replaced with examples; the players are made-up Kerbals). The setup wizard is shown step by step in [Set up a new server](#set-up-a-new-server-step-by-step).</sub>

## Getting started

Get `LunaEvo.ConfigManager.exe` from the [latest release](https://github.com/NEON-NULL-NARWHAL/luna-evo-multiplayer-config-manager/releases/latest),
or build it with `publish.ps1` (see [Build it](#build-it)). It's a single file with nothing to install.

- **No server yet?** Follow [Set up a new server](#set-up-a-new-server-step-by-step) below. It takes about two minutes.
- **Already have an LMP server?** Put the exe in your server folder, next to `Server.exe`, and double-click it.
  Edit, **Save** (Ctrl+S), then **Server console → Start**.
- **Just playing?** Choose **Join a friend's server** on the welcome page. See [Join a friend's server](#join-a-friends-server).
- **Playing over the internet?** Use a mesh VPN and never open a port: [Tailscale or Twingate](#play-together-without-opening-any-ports-tailscale-twingate).

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
will ask for permission). When it's done, the new server opens on the **Server console** page.

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
Everything else is in the rail: **Server settings** (tabs for General, Gameplay and every other LMP file) for the
rules, **Mod Control** to import your mods, **Profiles & backups** to save setups and snapshot the universe, and
**LUNA EVO → App settings** for automatic restarts, scheduled backups, notifications, updates and the app's colors. Remember to **Save**, then restart the
server for changes to take effect.

**What your players need:** KSP 1.12 with the **LMP client mod of the same version** as your server. It's on the same
[LMP releases page](https://github.com/LunaMultiplayer/LunaMultiplayer/releases) (`LunaMultiplayer-Client-Release.zip`).

### Get set up to play (CKAN and the LMP mod)

You need the **LMP client mod**, in the same version as the server, and most groups use **CKAN** for their other mods.
LUNA EVO handles both, any time:

- **Play → CKAN mods** shows at a glance whether LUNA EVO has CKAN (and which version it uses), whether CKAN is open,
  and your CKAN installs with their LMP version and mod count. **Download CKAN** gets the official `ckan.exe`, checked
  against GitHub's SHA-256 and kept by LUNA EVO (one copy per version, no mystery copies in Downloads).
- **Install a mod list:** your friend or server host exports their mods from CKAN (**File → Export modpack**, a
  `.ckan` file). Paste it in, or **Load a .ckan file...**, or just type mod names one per line, pick your KSP install
  and press **Install with CKAN**. CKAN works out the dependencies and downloads everything from its own catalog
  (LUNA EVO takes only the mod names from the list, never a file's own download links). Close KSP and CKAN first.
- **Already have CKAN?** Press **Choose ckan.exe...** and LUNA EVO uses your copy instead. It's checked against CKAN's
  official releases, so the page tells you whether it's an official build.
- **Share your mods:** every CKAN install has **Export mod list...**, which saves the mods you picked as a `.ckan` file
  for friends (without your Windows user name, which CKAN's own file contains), and **Use as mod list**, which copies one
  install's mods into another.
- **LMP client mod** (App settings → Downloads): pick your KSP install and the LMP version (the open server's by default),
  then **Install into this KSP**. Your LMP server list is kept and the old version is backed up.

Setting up a server? Step 3 of the setup wizard can get CKAN and the LMP client for you.

![CKAN page](docs/screenshots/ckan.png)

### Using the CKAN page

Open **Play → CKAN mods** in the left rail. The page has three parts, top to bottom.

**1. CKAN (which CKAN LUNA EVO uses)**
- **Not installed?** Press **Download CKAN**. LUNA EVO gets the official `ckan.exe` from CKAN's GitHub, checks it
  against GitHub's SHA-256 and keeps it in `%LOCALAPPDATA%\LunaEvo\Tools\CKAN`.
- **Already have CKAN?** Press **Choose ckan.exe...** and pick yours. The page then says **Using your own CKAN** and
  whether it's an official build ("Official CKAN v1.36.4"). **Use LUNA EVO's copy** switches back.
- **Open CKAN** starts CKAN's normal window. If CKAN is already open, the page says so.

**2. Your KSP installs (from CKAN)**
- Every KSP install CKAN knows about, with its LMP version, how many mods CKAN installed and how many you picked.
- **Export mod list...** saves the mods you picked as a `.ckan` file to send to friends or post for your server.
  Leave **Exact versions** on so everyone gets the same versions; turn it off to let CKAN pick the newest compatible ones.
- **Use as mod list** copies that install's mods into the box below, for installing them into another KSP install.

**3. Install a mod list**
1. Close KSP, and close CKAN's window if it has that install open.
2. Put the list in the box: **Load a .ckan file...** (a modpack from a friend, or one exported above), paste a
   `.ckan` file's text, or type mod names one per line (`#` starts a comment).
3. Pick the KSP install in **Into this KSP install**.
4. Optional: **Check with CKAN** asks CKAN to work the list out without installing anything (it's told "no" when it asks
   to continue): it says whether everything can be installed, or which mod it doesn't know.
5. Press **Install with CKAN** and confirm. CKAN refreshes its catalogue, works out the dependencies and downloads
   everything; its messages show underneath. If a mod name is unknown or doesn't fit your KSP version, CKAN stops and
   undoes that install, and the page says so.

Only mod names and versions are read from a list. Anything else in the file, such as a download link, is ignored.

![Downloads](docs/screenshots/downloads.png)

### Find a public server (server browser)

**Play → Server browser** lists every public LMP server, read from LMP's own master servers (the list LMP shows in
game). Filters: **Only my LMP version** (your KSP's LMP client; on by default, because the versions must match),
**No password**, **Has players**, and a search. **Join** fills the server into the Join page and checks it, ready for
**Launch KSP and join**. A server's own website link opens in your browser.

### Join a friend's server

1. Start LUNA EVO and choose **Join a friend's server** (or **Play → Join a server** in the rail).
2. Type the address your host gave you (e.g. `192.168.1.20` or `100.101.102.103:8800`) and press **Check server**.
   You see where it is and your ping. Server info, players and vessels appear if the host pressed **Share with
   players** on their Connect page.
3. Check the **Kerbal Space Program** folder (found automatically for Steam; your CKAN installs are one click away)
   and that your LMP version matches the server's (install the right one from App settings → Downloads).
4. Type the password if there is one, and press **Launch KSP and join**.
   - KSP starts with the same options as CKAN's Play button, so your modded install runs exactly as usual.
   - Default port (8800), no password: LMP connects by itself.
   - Otherwise: the server is at the top of LMP's server list with your password filled in. Click it and connect.
     (LMP's own command-line option can't handle a port or a password; we plan to fix that in the LUNA EVO fork.)
5. **Save to my servers** to skip typing next time.
6. **Check my mods** (optional): LUNA EVO fills in the rules LMP saved the last time you joined a server with mod
   control, or use the file your host sent (Mod Control → Share these rules with players).
   It lists exactly what LMP would refuse.

### Play together without opening any ports (Tailscale, Twingate)

The safest way to host for friends is a **mesh VPN**: everyone's PC joins a private network that works over the
internet, so friends connect to your server as if they were on your LAN. **Nothing is opened on your router**, and
nobody outside your mesh can even see the server. This is how we run ours.

**Tailscale** (easiest, free for personal use):

1. Install [Tailscale](https://tailscale.com/download) on the server PC and sign in.
2. Each friend installs Tailscale too, and you invite them to your tailnet (admin console → *Share* the server PC, or
   add them as users).
3. On the server PC, open LUNA EVO's **Connect** page: the Tailscale address (`100.x.y.z`, labeled VPN or mesh) is
   in *Addresses to share*. Press **Copy** and send it to your friends. With MagicDNS on, the PC's name works too.
4. Friends use that address on LUNA EVO's **Join a server** page (it shows "VPN or mesh network") or in LMP's own
   server list.

**Twingate** works too: run a Twingate **Connector** on your home network, add the server PC as a **Resource**
(allow UDP 8800 for the game, and TCP 8900 if you share live info), and give your friends access. They connect to the
Resource's address with the Twingate client running.

**Then keep the server private.** In LUNA EVO:

| Setting | For a mesh server |
|---|---|
| Connection → **Upnp** | **Off.** Nothing needs opening, and UPnP would open ports to the whole internet. LUNA EVO shows a "reachable from the internet" banner if it's on. |
| Master Server → **Register With Master Server** | **Off.** Friends join by address; the public list is for strangers. |
| Connect → **Allow through firewall** | Press it once. Windows Firewall still applies to mesh traffic. |
| Connect → **Share with players** (optional) | Lets friends' Join page show live info (players, vessels) over the mesh, on TCP 8900. |
| General → **Password** | Still worth setting a long one (**Generate a strong password**), as is a long admin password. |

Mesh addresses are private to your group, so they're fine to share with friends, but they're still yours: don't
post them publicly.

LMP 0.29.x itself needs the [.NET 6 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0); LUNA EVO checks for it and can install it for you (via Windows' `winget`).

### Tested with

| Software | Version tested |
|---|---|
| Windows | 11 (x64); LUNA EVO is Windows-only |
| Luna Multiplayer server | **0.29.2** (run for real); 0.30.0's settings match 0.29.2's |
| Luna Multiplayer client mod | **0.29.2** |
| Kerbal Space Program | **1.12.5** (Steam, modded) |
| CKAN | **1.36.4** |

Which LUNA EVO version was checked against what, release by release, is in [`SECURITY.md`](SECURITY.md#audit-and-test-history).

> [!IMPORTANT]
> LMP reads its config only when it starts, and rewrites the files as it does. Save your changes, *then* (re)start the server.
> LUNA EVO warns you if you save while it's running.

### What it connects to

Everything is local except, and only when you use them:
**GitHub** (LMP's, CKAN's and LUNA EVO's release lists and downloads, for the wizard, the CKAN page and update checks;
the update checks can be turned off), **LMP's own website on this PC** (for the Players page), **the server you type on
the Join page** (a DNS lookup, 4 pings and its LMP website), **LMP's master servers** for the server browser (one HTTPS request each, only when you
open it or press Refresh), **CKAN** when you install a mod list (it downloads from its
own catalog), and **your Discord webhook** if you add one. Details are in [`SECURITY.md`](SECURITY.md).

## For developers

Start with the maintainer's guide, [`Meat and Potatoes/`](Meat%20and%20Potatoes/README.md), and
[`CONTRIBUTING.md`](CONTRIBUTING.md) for the house rules. What's next is in
[`Meat and Potatoes/20-Next-Steps.md`](Meat%20and%20Potatoes/20-Next-Steps.md).

### Build it

Requirements: Windows, the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build                     # build everything
dotnet test                      # 276 tests: core logic and the real UI, headless; no network, no real server
.\publish.ps1                    # -> dist\LunaEvo-ConfigManager-<version>-win-x64\LunaEvo.ConfigManager.exe
.\release.ps1                    # tests, build, and a GitHub release with the exe: every copy then offers the update
```

Run from source against the copies in `tests/Fixtures` (no real server needed):

```powershell
dotnet run --project src/LunaEvo.ConfigManager --launch-profile "Fixtures (in repo)"
```

Command-line options: `--server <folder>` (contains Server.dll), `--config <folder>` (defaults to `<server>\Config`),
`--settings <file>` (LUNA EVO's own preferences, for portable use; defaults to `%APPDATA%\LunaEvo\ConfigManager\settings.json`).

### Repository layout

```
ConfigManager/
├── Meat and Potatoes/        📖 the maintainer's guide: start here
├── src/
│   ├── LunaEvo.ConfigManager.Core/   🧠 all the logic, no UI: schema, XML editing, validation, server control,
│   │                                    setup & updates, live data, GameData scanning, automation, joining, joining
│   └── LunaEvo.ConfigManager/        🖥️ the Avalonia desktop app
├── tests/
│   ├── LunaEvo.ConfigManager.Core.Tests/   🧪 logic tests (161)
│   ├── LunaEvo.ConfigManager.Tests/        🧪 UI tests: the real window, driven headlessly (79)
│   ├── FakeLmpServer/                      🧪 a pretend Server.exe so server control can be tested safely
│   └── Fixtures/                           🧪 sanitized real configs, LMP 0.29.2's Server.dll, sample logs and website JSON
├── docs/screenshots/
├── publish.ps1               📦 builds the single-file exe
├── release.ps1               📦 ships a version: tests, exe, GitHub release (the in-app updater picks it up)
├── SECURITY.md               🔒 what LUNA EVO touches, the audit, how to report a problem
└── CHANGELOG.md
```

The full file-by-file map is in [`Meat and Potatoes/README.md`](Meat%20and%20Potatoes/README.md).

## How this app was made (AI disclosure)

We want to be completely clear about this.

**Who did what**

| The project owner (human) | Claude (AI), via Claude Code in VS Code |
|---|---|
| Set the goals: a GUI config manager, then server control, theming, this repo, the 0.3 feature set (wizard, players, mod import, automation...), 0.4 (a security audit, the Join page, removing Discord), 0.5 (player history, alerts, bans, mod check, notifications), 0.6 (self-update, a tidier layout), then crafts sharing, home server mode, vessel cleanup, CKAN and the LMP client, and installing mod lists with CKAN (0.7 to 1.2), with a security audit at each milestone. Found the first two security issues. | Read LMP's source to learn how settings, logging, commands, shutdown, releases, the web API and mod checks really work. |
| Made the decisions: desktop app, Avalonia, .NET 10, GPL-3.0 (the repo started private and is now public), the look (inspired by [NOMM](https://github.com/Combat787/NOMM)). | Wrote all of the code: the C#, XAML and build scripts. |
| Supplied the real server, its configs and the requirements. | Wrote all 276 automated tests, a fake LMP server, and fakes for every network call. |
| Reviewed the results and asked for changes. | Ran the tests, checked the real thing against real LMP (below), took the screenshots, and wrote these docs. |

- **Model and tool:** Claude Opus 5.5 by Anthropic, working in Claude Code (an AI coding agent that can read files, run commands and build/test the code) inside VS Code.
- **When:** October 2026, in one long working session.
- **How much:** essentially every line in this repo was written by the AI. The human role was direction, decisions, review and testing.

**How we checked it**

AI-written code can look right and still be wrong, so nothing here is trusted on looks:

- **276 automated tests.** They also run on GitHub on every push. Byte-for-byte file round trips; every validation rule; generated and edited configs deserialized by **LMP's own classes** exactly as the server loads them; install/update from zips (including refusing to overwrite your config or escape the folder); a fake server that really starts, takes commands, crashes and stops; and UI tests that click through **every tab and control** plus every new page.
- **Against the real thing:**
  - The wizard downloaded the real LMP 0.29.2 from GitHub, and the real server started on the generated config **with no errors**.
  - The Players page read the real server's live JSON. A Universe backup was taken while it ran, and it stopped cleanly.
  - The Join page checked a real running server (resolve, ping, live info) and found a real KSP install with LMP 0.29.2.
  - The download checksum was checked against the real LMP 0.29.2 release's published SHA-256.
  - The real CKAN 1.36.4 `ckan.exe` and LMP 0.29.2 client zip matched GitHub's published SHA-256, and the real `ckan.exe` installed a mod list by name into a throwaway KSP folder.
  - The GameData import scanned a real modded KSP install (4,382 `.cfg` files): 1,527 parts, 132 plugins with hashes, and every stock part that still exists in the game.
- **Visual checks:** every page was screenshotted and reviewed during development.

The tests caught real mistakes along the way, which is why they exist. Examples: Windows passing an "ignore Ctrl+C"
flag down to every later server, LMP's files claiming `utf-16` while being UTF-8, a background thread touching a UI
control, and an update message being hidden by the update offer. They're documented in
[`Meat and Potatoes/07-LMP-Gotchas.md`](Meat%20and%20Potatoes/07-LMP-Gotchas.md) and
[`Meat and Potatoes/09-How-It-Was-Built.md`](Meat%20and%20Potatoes/09-How-It-Was-Built.md).

**What that means for you:** treat this like any open-source project. Read the code, run the tests, and keep the backups
it makes. The exe isn't code-signed, so Windows SmartScreen may warn the first time you run it.

## Credits and licenses

- **LUNA EVO Config Manager**: GPL-3.0, see [LICENSE](LICENSE).
- **[Luna Multiplayer](https://github.com/LunaMultiplayer/LunaMultiplayer)** by Gabriel Vazquez and contributors: MIT. LUNA EVO contains no LMP source code; it reads your server's `Server.dll` at runtime and downloads LMP's official releases. The test fixtures include LMP 0.29.2's server binaries under their MIT license ([`tests/Fixtures/LMPServer/LMP-LICENSE.txt`](tests/Fixtures/LMPServer/LMP-LICENSE.txt)).
- **[Avalonia UI](https://avaloniaui.net/)**: MIT.
- **[JetBrains Mono](https://github.com/JetBrains/JetBrainsMono)** font: SIL Open Font License 1.1 ([`Assets/Fonts/OFL.txt`](src/LunaEvo.ConfigManager/Assets/Fonts/OFL.txt)).
- **Material Symbols** icons by Google: Apache 2.0.
- Look and feel inspired by **[NOMM](https://github.com/Combat787/NOMM)**, the Nuclear Option Mod Manager.

Kerbal Space Program is a trademark of Take-Two Interactive. This project isn't affiliated with them or with the LMP team.
