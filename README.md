<div align="center">

<img src="src/LunaEvo.MissionControl/Assets/icon.png" width="96" alt="LUNA EVO icon">

# LUNA EVO Mission Control

**Everything for [Luna Multiplayer](https://github.com/LunaMultiplayer/LunaMultiplayer) (LMP) in Kerbal Space Program, in one app:
host and run your own server, find public ones, get your game modded with CKAN, and launch straight into a server.**

[![Latest release](https://img.shields.io/github/v/release/NEON-NULL-NARWHAL/luna-evo-mission-control?label=download)](https://github.com/NEON-NULL-NARWHAL/luna-evo-mission-control/releases/latest)
[![Tests](https://github.com/NEON-NULL-NARWHAL/luna-evo-mission-control/actions/workflows/tests.yml/badge.svg)](https://github.com/NEON-NULL-NARWHAL/luna-evo-mission-control/actions/workflows/tests.yml)
![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-blue)
[![License: GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-green)](LICENSE)

**Version 2.0.1** · Windows 10/11 · .NET 10 · Avalonia 12 · GPL-3.0

**[Download](#download)** · **[Features](#what-it-does)** · **[Screenshots](#screenshots)** · **[Host a server](#set-up-a-new-server-step-by-step)** · **[Find and join](#join-a-friends-server)** · **[Mods with CKAN](#using-the-ckan-page)** · **[No open ports](#play-together-without-opening-any-ports-tailscale-twingate)** · **[For developers](#for-developers)**

</div>

> [!NOTE]
> **Formerly LUNA EVO Config Manager.** It outgrew configs, so 2.0 renamed it. Already have it? It updates itself to
> Mission Control and keeps your settings.

> [!NOTE]
> **Security:** every download is checked against GitHub's SHA-256. All nine security audits, their fixes, and what each
> release was tested against are in [`SECURITY.md`](SECURITY.md).
> **Written with AI:** the code, tests and docs were written by Claude (Anthropic's AI model), directed and reviewed by
> the project owner. [Who did what, and how it was checked.](#how-this-app-was-made-ai-disclosure)

## At a glance

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

### Host a server

| | |
|---|---|
| 🧙 **Setup wizard** | Pick a folder and answer a few questions (name, password, mode, difficulty, players, port). LUNA EVO downloads the LMP server from GitHub, writes the config before it ever starts, and installs the .NET runtime it needs. Private by default. It can also get CKAN and the matching LMP client for you. |
| ▶️ **Server console** | Start, stop and restart, with a live color-coded console, filters, search, commands with history and quick buttons, and a log file browser. **Cards at the top** (name, game port, max players...) change a setting right there. Live memory and CPU. Stops are clean (LMP saves the universe), never a kill. |
| 🔌 **Connect** | The exact addresses to give friends (LAN, VPN/mesh, internet), a one-click firewall rule, whether the port is listening, **Share with players** for live info, and **Copy invite**: a message friends paste straight into their Join page. |
| 👥 **Players** | Who's online, their vessels and warp subspaces, **Kick** and **Ban**. Click a card for the list of just those players, vessels or asteroids. |
| 📜 **History, alerts, bans** | Everyone who's played (last seen, time played, your notes; never IPs). Alerts for admin-password guessing and banned players returning. LMP's ban list with names, and **Unban**. |
| 🔔 **Notifications** | Windows notifications and an optional Discord channel: server up, stopped or crashed, alerts, joins and leaves. The webhook is stored encrypted; posts can never ping @everyone. |
| ⬆️ **LMP updates** | A newer LMP server in one click, keeping your Config, Universe, logs and Plugins, and saving the old version. |

### Find, mod and join (for players)

You don't have to host anything: choose **Join a friend's server** on the welcome page, or open **Play** in the rail.

| | |
|---|---|
| 🌍 **Server browser** | Every public LMP server, the list LMP shows in game: search, only your LMP version, no password, has players. **Join** checks it and gets you ready to launch. |
| 🛰️ **Check a server** | A friend's address: its network (this PC, LAN, VPN/mesh, internet), your ping, and, if the host shares it, name, mode, players and vessels. |
| 🚀 **Launch KSP straight in** | One button: KSP starts with CKAN's options and LMP connects by itself. Servers with a password or another port go to the top of LMP's server list, password included. |
| 🧰 **CKAN mods** | See whether CKAN is installed and which copy is used, **check** and **install** a mod list (a `.ckan` modpack or mod names), **export** your install's list for friends. Uses LUNA EVO's verified CKAN or yours; CKAN's errors in plain words. |
| 🧩 **Check my mods** | Your GameData against a server's mod rules, exactly as LMP checks them: banned, missing or wrong-version mods, missing parts, parts it would drop. Hosting it? **Allow them on my server** in one click. |
| 📦 **LMP client** | Install the LMP client mod in the server's version into any KSP install, your CKAN ones included (App settings → Downloads). |
| 🚀 **Crafts** | Export a craft to a `.lunacraft` file for friends; import theirs into your save or the server, with a missing-parts check. |
| 📌 **My servers** | The servers you play on, saved. Passwords never are. |

### Configure it

| | |
|---|---|
| ⭐ **Essentials first** | Server settings opens with the dozen settings most servers need. **Simple mode** (App settings → Behavior) hides everything else until you want it. |
| ⚙️ **Every setting, in a GUI** | All 13 LMP config files as tabs, grouped Game / Network / Advanced; every setting with the right control and LMP's own description. **Search** finds any of them. |
| 🔍 **Always matches your server** | The settings list is read **from your server's own `Server.dll`**, so new or renamed LMP settings appear by themselves. |
| ✅ **Validation** | LMP's limits plus the rules it only mentions in passing (heartbeat < timeout, port clashes). Problems show inline and block saving; the problem count in the rail lists them and jumps to each. Weak passwords get a warning and a **Generate** button (LMP doesn't limit guesses). |
| 🧾 **Review before saving** | Every change as old → new before anything is written, and a timestamped backup on every save. |
| 🎯 **Knows LMP's quirks** | Grays out what doesn't apply (rainbow colors, Career-only funds), follows the difficulty preset, and warns before LMP silently switches you to *Custom*. |
| 🧩 **Mod Control** | **Import from GameData** fills the allowed parts, resources, plugins (with SHA-256) and expansions the way LMP's own generator does; every list is editable, and **Share these rules with players** gives friends a file to check against. |

### Keep it running

| | |
|---|---|
| 🏠 **Home server mode** | For an always-on PC: starts with Windows in the notification area, starts the server, hides instead of quitting, keeps the PC awake while it runs. |
| 🔄 **Restarts** | After a crash (giving up if it keeps crashing), and an optional daily restart with an in-game warning. |
| 🌌 **Universe backups** | Dated snapshots on demand, every few hours, every time the server stops and before every LMP update. One-click restore, which backs up first. |
| 🧹 **Vessel cleanup** | Remove debris, asteroids and abandoned ships (server stopped, backed up first, crewed ships protected): the biggest performance win a host has. |
| 💬 **Chat announcements** | Messages to the game chat on a schedule ("every 60 minutes: join our Discord"). |
| 🗂️ **Config profiles** | Save the whole setup as "Career campaign" or "Sandbox chaos night" and switch in one click. |

### The app itself

| | |
|---|---|
| 📦 **One file** | A single self-contained `.exe`. Next to `Server.exe` it opens that server by itself. |
| ⬆️ **Updates itself** | Checks its GitHub releases on startup; **Update and restart** verifies the download, swaps the exe and restarts. |
| 🧭 **Easy to find your way** | One rail: Server (console, Players, Vessels, Connect, Server settings, Mod Control, Profiles & backups), Play (Server browser, Join, Crafts, CKAN mods) and App settings, with every version in use (LUNA EVO, LMP server and client, KSP, CKAN) at the bottom. |
| 🎨 **Make it yours** | An RGB color wheel and KSP-themed presets regenerate the whole theme; dark, light or follow Windows. |

### Safe by default

| | |
|---|---|
| 🔒 **Verified downloads** | LMP, CKAN, the LMP client and LUNA EVO itself come only from GitHub over HTTPS and are checked against GitHub's SHA-256 before use. |
| 💾 **Backups everywhere** | Before every save, update, restore and change to LMP's server list. Config files are written through a temp file, so a crash can't leave a broken one. |
| 🏠 **Private until you say so** | New servers aren't listed, don't touch your router and keep live info on this PC. A banner says whenever UPnP or the public list makes the server reachable from the internet. |
| 🤫 **No secrets in the open** | LUNA EVO never stores passwords; a Discord webhook is encrypted for your Windows account. No telemetry, no account. |
| 🧾 **Error log** | Anything unexpected goes to `%APPDATA%\LunaEvo\MissionControl\logs\errors.log` instead of crashing, so a bug report can say what happened. |
| ⚠️ **Warns before it matters** | Saving while the server runs, stopping it with players on, closing with unsaved changes, settings LMP would reject. |

## Screenshots

**Hosting**

| | |
|:---:|:---:|
| ![Setup wizard](docs/screenshots/wizard.png) | ![Server console](docs/screenshots/console.png) |
| **Setup wizard:** a few questions, then LMP is downloaded and configured. | **Server console:** the real LMP server running inside the app. |
| ![Connect](docs/screenshots/connect.png) | ![First start](docs/screenshots/first-start.png) |
| **Connect:** what to tell your friends, and what might block them. | **First start:** LMP's own startup, inside the app. |
| ![Players](docs/screenshots/players.png) | ![Player history and bans](docs/screenshots/players-history.png) |
| **Players:** live from LMP, with alerts for password guessing and banned players. | **History and bans:** sessions, time played, notes; unban by name. |
| ![Server health](docs/screenshots/console-health.png) | ![Vessel cleanup](docs/screenshots/vessels.png) |
| **Server health:** memory, CPU and threads of the running server. | **Vessel cleanup:** debris selected, crewed ships protected, backup first. |

**Playing**

| | |
|:---:|:---:|
| ![Join a server](docs/screenshots/join.png) | ![Join: play](docs/screenshots/join-play.png) |
| **Join a server:** network, ping and live server info. | **Launch KSP and join:** straight in, or via LMP's list. |
| ![CKAN page](docs/screenshots/ckan.png) | ![Join with CKAN](docs/screenshots/join-ckan.png) |
| **CKAN mods:** which CKAN, your installs, check and install a mod list. | **Join + CKAN:** pick a CKAN install; launch with its options. |
| ![Check my mods](docs/screenshots/join-mods.png) | ![Crafts](docs/screenshots/crafts.png) |
| **Check my mods:** what LMP will refuse, before you join. | **Crafts:** export your designs, import friends', with a missing-parts check. |
| ![Downloads](docs/screenshots/downloads.png) | ![Setup wizard, extras](docs/screenshots/wizard-extras.png) |
| **LMP client:** into any KSP install, any version. | **Setup wizard:** also get CKAN and the matching LMP client. |

**Configuring and running**

| | |
|:---:|:---:|
| ![Essentials](docs/screenshots/essentials.png) | ![Gameplay](docs/screenshots/gameplay.png) |
| **Essentials:** the settings most servers need, first. | **Server settings:** a tab per LMP file; Career-only settings grayed out in Sandbox, with the reason. |
| ![Validation](docs/screenshots/validation.png) | ![Settings search](docs/screenshots/search.png) |
| **Validation:** errors inline, badges on the tabs, the count in the rail. | **Search:** any setting, across the tabs. |
| ![Import from GameData](docs/screenshots/mod-import.png) | ![Plugin lists](docs/screenshots/mod-lists.png) |
| **Import from GameData:** fill Mod Control from a KSP install. | **Plugin lists:** LMP's DllFile entries, editable. |
| ![Profiles and backups](docs/screenshots/profiles.png) | ![Log files](docs/screenshots/logfiles.png) |
| **Profiles & backups:** switch whole configs; snapshot and restore the Universe. | **Log files:** every `lmpserver_*.log`, color-coded. |
| ![Automation settings](docs/screenshots/settings-automation.png) | ![Chat announcements](docs/screenshots/announcements.png) |
| **Automation & alerts:** restarts, Universe backups, notifications. | **Chat announcements:** messages to game chat on a schedule. |
| ![Home server mode](docs/screenshots/home-server.png) | ![A Windows notification](docs/screenshots/notification-toast.png) |
| **Home server mode:** one switch for an always-on PC. | **A Windows notification** from LUNA EVO. |
| ![Settings](docs/screenshots/settings.png) | ![Updates](docs/screenshots/settings-updates.png) |
| **App settings:** color wheel, presets, theme. | **Updates:** LUNA EVO updates itself from its GitHub releases. |
| ![Light theme](docs/screenshots/light.png) | ![Welcome](docs/screenshots/welcome.png) |
| **Light theme.** | **Welcome:** host, open a server, or just play. |

<sub>Screenshots are LUNA EVO 0.6 to 2.0 (older ones still say "Config Manager", its name before 2.0) running a sandbox copy of a real LMP 0.29.2 server, with personal details removed: network addresses are examples and the players are made-up Kerbals. The setup wizard is shown step by step in [Set up a new server](#set-up-a-new-server-step-by-step).</sub>

## Getting started

Get `LunaEvo.MissionControl.exe` from the [latest release](https://github.com/NEON-NULL-NARWHAL/luna-evo-mission-control/releases/latest),
or build it with `publish.ps1` (see [Build it](#build-it)).

- **No server yet?** [Set up a new server](#set-up-a-new-server-step-by-step), about two minutes.
- **Already have an LMP server?** Put the exe next to `Server.exe` and double-click it. Edit, **Save** (Ctrl+S), then
  **Server console → Start**.
- **Just playing?** [Find a public server](#find-a-public-server-server-browser) or [join a friend's](#join-a-friends-server).
- **Need mods?** [Using the CKAN page](#using-the-ckan-page).
- **Playing over the internet?** Use a mesh VPN and never open a port: [Tailscale or Twingate](#play-together-without-opening-any-ports-tailscale-twingate).
- **New to hosting?** Turn on **Simple mode** (App settings → Behavior) to see only what you need.

> [!IMPORTANT]
> LMP reads its config only when it starts, and rewrites the files as it does. Save your changes, *then* (re)start the
> server. LUNA EVO warns you if you save while it's running.

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
| Difficulty | Easy / Normal / Moderate / Hard. You can fine-tune each rule later on the **Gameplay** tab. |
| Max players | How many people can be on at once. |
| Game port | Leave it at **8800** unless you know you need another. |
| Show on the public server list | **Off** for a server for friends (they join with your address). On to be listed in the server browsers. |
| Let LMP open router ports (UPnP) | **Off** if everyone is on your network or a VPN/mesh. On to let players from the internet in (if your router allows it). |
| Check players' mods (Mod Control) | **Off** lets players use any mods. On restricts them to the parts and plugins you allow (see **Mod Control → Import from GameData**). |
| Live player info | Leave it **on**: it powers the **Players** page, and only this PC can see the data. |
| Put LUNA EVO in the server folder | Leave it **on**, so next time you just double-click LUNA EVO in that folder. |

![Setup wizard, options](docs/screenshots/wizard-options.png)

**4. Press "Download and set up".**
LUNA EVO downloads the LMP server, unpacks it and writes your settings **before** the server ever starts, so nothing
is public until you say so. LMP 0.29.x needs the [.NET 6 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0);
if this PC doesn't have it, LUNA EVO offers to install it (Windows asks for permission). Then the new server opens on
the **Server console**.

![The new server, ready to start](docs/screenshots/wizard-opened.png)

**5. Press Start.**
The console fills with LMP's startup messages. When you see **"All systems up and running"**, the server is ready, and
the dot next to Server console in the rail shows it's running. **Stop** shuts it down cleanly: LMP saves the universe.

![First start](docs/screenshots/first-start.png)

**6. Tell your friends how to join.**
Open **Connect**. It lists the addresses to share (on your network, VPN or mesh: copy one of these), whether you're on
the public list, and whether anything is blocking players. If Windows Firewall might block them, press **Allow through
firewall** (Windows asks for permission once). **Copy invite** gives you a message to send; friends paste it into
LUNA EVO's Join page, or add the address in LMP's own server list.

![Connect page](docs/screenshots/connect.png)
<sub>The address shown is an example; your own is filled in automatically. Your internet address stays hidden until you press Show.</sub>

**7. Make it yours (optional).**
**Server settings** has the rules (Essentials first, then a tab for every LMP file), **Mod Control** imports your mods,
**Profiles & backups** saves setups and snapshots the universe, and **App settings** has automatic restarts, scheduled
backups, notifications, updates and the app's colors. **Save**, then restart the server for changes to take effect.

**What your players need:** KSP 1.12 with the **LMP client mod in the same version** as your server. LUNA EVO installs
it for them (App settings → Downloads), or it's on [LMP's releases page](https://github.com/LunaMultiplayer/LunaMultiplayer/releases)
(`LunaMultiplayer-Client-Release.zip`).

### Find a public server (server browser)

**Play → Server browser** lists every public LMP server, read from LMP's own master servers (the list LMP shows in
game). Filters: **Only my LMP version** (your KSP's LMP client; on by default, because the versions must match),
**No password**, **Has players**, and a search. **Join** fills the server into the Join page and checks it, ready for
**Launch KSP and join**. A server's own website link opens in your browser.

### Join a friend's server

1. Start LUNA EVO and choose **Join a friend's server** (or **Play → Join a server** in the rail).
2. Type the address your host gave you (e.g. `192.168.1.20` or `100.101.102.103:8800`), or paste their invite, and
   press **Check server**. You see where it is and your ping; server info, players and vessels appear if the host
   pressed **Share with players** on their Connect page.
3. Check the **Kerbal Space Program** folder (found automatically for Steam; your CKAN installs are one click away)
   and that your LMP version matches the server's (install the right one from App settings → Downloads).
4. Type the password if there is one, and press **Launch KSP and join**.
   - KSP starts with the same options as CKAN's Play button, so your modded install runs exactly as usual.
   - Default port (8800), no password: LMP connects by itself.
   - Otherwise the server is at the top of LMP's server list with your password filled in: click it and connect.
     (LMP's command-line option can't take a port or a password; we plan to fix that in the LUNA EVO fork.)
5. **Save to my servers** to skip typing next time.
6. **Check my mods** (optional): LUNA EVO uses the rules LMP saved the last time you joined a server with mod
   control, or the file your host sent (Mod Control → Share these rules with players), and lists exactly what LMP
   would refuse.

### Using the CKAN page

Most groups use **CKAN** for their mods. Open **Play → CKAN mods**; the page has three parts, top to bottom.

**1. Which CKAN LUNA EVO uses**
- **Not installed?** Press **Download CKAN**: the official `ckan.exe` from CKAN's GitHub, checked against GitHub's
  SHA-256 and kept in `%LOCALAPPDATA%\LunaEvo\Tools\CKAN` (one copy per version, no mystery copies in Downloads).
- **Already have CKAN?** Press **Choose ckan.exe...** and pick yours. The page then says **Using your own CKAN** and
  whether it's an official build ("Official CKAN v1.36.4"). **Use LUNA EVO's copy** switches back.
- **Open CKAN** starts CKAN's normal window. If CKAN is already open, the page says so.

**2. Your KSP installs (from CKAN)**
- Every KSP install CKAN knows, with its LMP version, how many mods CKAN installed and how many you picked.
- **Export mod list...** saves the mods you picked as a `.ckan` file to send to friends or post for your server
  (without your Windows user name, which CKAN's own file contains). Leave **Exact versions** on so everyone gets the
  same versions; turn it off to let CKAN pick the newest compatible ones.
- **Use as mod list** copies that install's mods into the box below, to install them into another KSP install.

**3. Install a mod list**
1. Close KSP, and close CKAN's window if it has that install open.
2. Put the list in the box: **Load a .ckan file...** (a modpack from a friend, CKAN's **File → Export modpack**, or
   one exported above), paste a `.ckan` file's text, or type mod names one per line (`#` starts a comment).
3. Pick the KSP install in **Into this KSP install**.
4. Optional: **Check with CKAN** works the list out without installing anything (CKAN is told "no" when it asks to
   continue): it says whether everything can be installed, or which mod it doesn't know.
5. Press **Install with CKAN** and confirm. CKAN refreshes its catalog, works out the dependencies and downloads
   everything, with its messages underneath. If a mod is unknown or doesn't fit your KSP version, CKAN stops and
   undoes that install, and the page says why.

Only mod names and versions are read from a list. Anything else in the file, such as a download link, is ignored.
Setting up a server? Step 3 of the setup wizard can get CKAN and the LMP client for you.

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

Mesh addresses are private to your group, so they're fine to share with friends, but they're still yours: don't post
them publicly.

## Tested with

| Software | Version tested |
|---|---|
| Windows | 11 (x64); LUNA EVO is Windows-only |
| Luna Multiplayer server | **0.29.2** (run for real); 0.30.0's settings match 0.29.2's |
| Luna Multiplayer client mod | **0.29.2** |
| Kerbal Space Program | **1.12.5** (Steam, modded) |
| CKAN | **1.36.4** |

Which LUNA EVO version was checked against what, release by release, is in [`SECURITY.md`](SECURITY.md#audit-and-test-history).

### What it connects to

Everything is local except, and only when you use them: **GitHub** (LMP's, CKAN's and LUNA EVO's releases, for the
wizard, the CKAN page and update checks; the update checks can be turned off), **LMP's own website on this PC** (for
the Players page), **the server you type on the Join page** (a DNS lookup, 4 pings and its LMP website), **LMP's
master servers** for the server browser (one HTTPS request each, when you open it or press Refresh), **CKAN** when you
install a mod list (it downloads from its own catalog), and **your Discord webhook** if you add one. Details are in
[`SECURITY.md`](SECURITY.md).

## For developers

> [!TIP]
> **New to the code? Start in [`Meat and Potatoes/`](Meat%20and%20Potatoes/README.md).** It's the maintainer's guide:
> a map of every folder and file with a symbol legend, how the pieces fit together, the LMP quirks we discovered, and
> step-by-step recipes for common changes.

House rules are in [`CONTRIBUTING.md`](CONTRIBUTING.md); what's next is in
[`Meat and Potatoes/20-Next-Steps.md`](Meat%20and%20Potatoes/20-Next-Steps.md).

### Build it

Requirements: Windows, the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build                     # build everything
dotnet test                      # 282 tests: core logic and the real UI, headless; no network, no real server
.\publish.ps1                    # -> dist\LunaEvo-MissionControl-<version>-win-x64\LunaEvo.MissionControl.exe
.\release.ps1                    # tests, build, and a GitHub release with the exe: every copy then offers the update
```

Run from source against the copies in `tests/Fixtures` (no real server needed):

```powershell
dotnet run --project src/LunaEvo.MissionControl --launch-profile "Fixtures (in repo)"
```

Command-line options: `--server <folder>` (contains Server.dll), `--config <folder>` (defaults to `<server>\Config`),
`--settings <file>` (LUNA EVO's own preferences, for portable use; defaults to `%APPDATA%\LunaEvo\MissionControl\settings.json`).

### Repository layout

```
luna-evo-mission-control/
├── Meat and Potatoes/        📖 the maintainer's guide: start here
├── src/
│   ├── LunaEvo.MissionControl.Core/   🧠 all the logic, no UI: schema, XML editing, validation, server control,
│   │                                    setup & updates, live data, GameData scanning, automation, joining, CKAN
│   └── LunaEvo.MissionControl/        🖥️ the Avalonia desktop app
├── tests/
│   ├── LunaEvo.MissionControl.Core.Tests/   🧪 logic tests (189)
│   ├── LunaEvo.MissionControl.Tests/        🧪 UI tests: the real window, driven headlessly (93)
│   ├── FakeLmpServer/                      🧪 a pretend Server.exe so server control can be tested safely
│   └── Fixtures/                           🧪 sanitized real configs, LMP 0.29.2's Server.dll, sample logs and website JSON
├── docs/screenshots/
├── publish.ps1               📦 builds the single-file exe
├── release.ps1               📦 ships a version: tests, exe, GitHub release (the in-app updater picks it up)
├── SECURITY.md               🔒 what LUNA EVO touches, every audit, how to report a problem
└── CHANGELOG.md
```

The full file-by-file map is in [`Meat and Potatoes/README.md`](Meat%20and%20Potatoes/README.md).

## How this app was made (AI disclosure)

We want to be completely clear about this.

**Who did what**

| The project owner (human) | Claude (AI), via Claude Code in VS Code |
|---|---|
| Set the goals, release by release: a GUI config manager (0.1), then server control and theming, the setup wizard, players, mod import and automation (0.3), the Join page (0.4), player history, alerts and the mod check (0.5), self-update (0.6), crafts, home server mode and vessel cleanup (0.7 to 1.0), CKAN and the LMP client (1.1 to 1.3), the server browser and simple mode (1.4), and the rename to Mission Control (2.0), with a security audit at each milestone. Tested it on the real server and game, and found the first two security issues. | Read LMP's source to learn how settings, logging, commands, shutdown, releases, the web API, the master servers and mod checks really work. |
| Made the decisions: desktop app, Avalonia, .NET 10, GPL-3.0 (the repo started private and is now public), the look (inspired by [NOMM](https://github.com/Combat787/NOMM)), the name. | Wrote all of the code: the C#, XAML and build scripts. |
| Supplied the real server, its configs, the requirements and lists of what to fix. | Wrote all 282 automated tests, a fake LMP server, and fakes for every network call. |
| Reviewed the results and asked for changes. | Ran the tests, checked the real thing against real LMP and CKAN (below), took the screenshots, and wrote these docs. |

- **Model and tool:** Claude Opus 5.5 by Anthropic, working in Claude Code (an AI coding agent that can read files, run commands and build/test the code) inside VS Code.
- **When:** October 2026.
- **How much:** essentially every line in this repo was written by the AI. The human role was direction, decisions, review and testing.

**How we checked it**

AI-written code can look right and still be wrong, so nothing here is trusted on looks:

- **282 automated tests**, run on GitHub on every push: byte-for-byte file round trips; every validation rule;
  generated and edited configs deserialized by **LMP's own classes** exactly as the server loads them; install and
  update from zips (refusing to overwrite your config or escape the folder); a fake server that really starts, takes
  commands, crashes and stops; and UI tests that click through **every tab and control**.
- **Against the real thing:**
  - The wizard downloaded the real LMP 0.29.2 from GitHub, and the real server started on the generated config **with no errors**.
  - The Players page read the real server's live JSON; a Universe backup was taken while it ran, and it stopped cleanly.
  - The Join page checked a real running server and found a real KSP install with LMP 0.29.2.
  - Downloads matched GitHub's published SHA-256 for the real LMP 0.29.2 server and client and CKAN 1.36.4.
  - The real `ckan.exe` installed and dry-ran mod lists (up to 108 mods) on a throwaway KSP folder, and LUNA EVO's
    export of a real install matched the modpack it was set up from.
  - The server browser read 368 real public servers from LMP's master servers.
  - The GameData import scanned a real modded KSP install (4,382 `.cfg` files): 1,527 parts and 132 plugins with hashes.
- **Visual checks:** every page was screenshotted and reviewed during development.

The tests caught real mistakes along the way, which is why they exist. Examples: Windows passing an "ignore Ctrl+C"
flag down to every later server, LMP's files claiming `utf-16` while being UTF-8, a background thread touching a UI
control, and an update message being hidden by the update offer. They're documented in
[`Meat and Potatoes/07-LMP-Gotchas.md`](Meat%20and%20Potatoes/07-LMP-Gotchas.md) and
[`Meat and Potatoes/09-How-It-Was-Built.md`](Meat%20and%20Potatoes/09-How-It-Was-Built.md).

**What that means for you:** treat this like any open-source project. Read the code, run the tests, and keep the backups
it makes. The exe isn't code-signed, so Windows SmartScreen may warn the first time you run it.

## Credits and licenses

- **LUNA EVO Mission Control**: GPL-3.0, see [LICENSE](LICENSE).
- **[Luna Multiplayer](https://github.com/LunaMultiplayer/LunaMultiplayer)** by Gabriel Vazquez and contributors: MIT. LUNA EVO contains no LMP source code; it reads your server's `Server.dll` at runtime and downloads LMP's official releases. The test fixtures include LMP 0.29.2's server binaries under their MIT license ([`tests/Fixtures/LMPServer/LMP-LICENSE.txt`](tests/Fixtures/LMPServer/LMP-LICENSE.txt)).
- **[CKAN](https://github.com/KSP-CKAN/CKAN)**: LUNA EVO downloads and runs the official `ckan.exe` (MIT); it contains no CKAN code.
- **[Avalonia UI](https://avaloniaui.net/)**: MIT.
- **[JetBrains Mono](https://github.com/JetBrains/JetBrainsMono)** font: SIL Open Font License 1.1 ([`Assets/Fonts/OFL.txt`](src/LunaEvo.MissionControl/Assets/Fonts/OFL.txt)).
- **Material Symbols** icons by Google: Apache 2.0.
- Look and feel inspired by **[NOMM](https://github.com/Combat787/NOMM)**, the Nuclear Option Mod Manager.

Kerbal Space Program is a trademark of Take-Two Interactive. This project isn't affiliated with them, the LMP team or the CKAN team.
