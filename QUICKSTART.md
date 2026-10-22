# Quickstart

The short version of everything in the [README](README.md). Pick what you want to do and follow a few steps. Under
each button, **↳** says what it actually does, so nothing happens behind your back. **Full guide** links take you to
the details.

**[Get it](#get-it)** · **[Where things are](#where-things-are)** · **[Host a server](#host-a-server)** · **[Already have a server](#already-have-an-lmp-server)** · **[Play on a server](#play-on-a-server)** · **[Mods with CKAN](#mods-with-ckan)** · **[Play over the internet](#play-over-the-internet-safely)** · **[Leave it running](#leave-it-running)** · **[If something's wrong](#if-somethings-wrong)**

## Get it

1. Download **`LunaEvo.MissionControl.exe`** from the [latest release](https://github.com/NEON-NULL-NARWHAL/luna-evo-mission-control/releases/latest).
   One file, Windows 10/11, nothing to install.
2. Run it. If Windows SmartScreen asks: **More info → Run anyway**.<br>
   ↳ The exe isn't code-signed yet, so Windows doesn't recognize it. Each release lists its SHA-256 if you want to check
   the file (`Get-FileHash .\LunaEvo.MissionControl.exe`).
3. That's the last download you need.<br>
   ↳ On startup it asks GitHub whether there's a newer version; **App settings → Updates → Update and restart** downloads
   it, checks it against GitHub's SHA-256, swaps the exe and restarts. You can turn the check off.

💡 **Tips**
- New to hosting? **App settings → Behavior → Simple mode** hides everything but the basics. Nothing is removed; turn
  it off any time.
- Your preferences live in `%APPDATA%\LunaEvo\MissionControl\settings.json`. LUNA EVO never stores passwords there.

Full guide: [Download](README.md#download) · [Getting started](README.md#getting-started)

## Where things are

The rail on the left has everything; the bottom of it shows the server status and every version in use (LUNA EVO,
LMP server and client, KSP, CKAN).

| Rail | Page | What it's for |
|---|---|---|
| Server | **Server console** | Start, stop, restart; the live console; cards you click to change name, port, players |
| | **Players** · **Vessels** · **Map** | Who's online, history, kick/ban; every vessel, cleanup; the solar system map |
| | **Connect** | What to tell friends, firewall, invite |
| | **Server settings** | Every LMP setting (start with **Essentials**) |
| | **Mod Control** | Which parts and mods players may use |
| | **Profiles & backups** | Saved setups; Universe snapshots |
| Play | **Server browser** · **Join a server** | Find a server; check it; launch KSP into it |
| | **Crafts** · **CKAN mods** | Share craft files; install and share mod lists |
| LUNA EVO | **App settings** | Look, behavior (simple mode, home server mode), automation, updates, LMP client downloads |

## Host a server

About two minutes, no files to edit.

1. Start LUNA EVO and choose **Set up a new server**.
2. Pick an empty folder (e.g. `D:\Games\LMPServer`) and the LMP version.<br>
   ↳ The newest stable version is picked for you from LMP's GitHub. Everyone who plays needs this same version.
3. Answer the questions: name, password, game mode, difficulty, players. Leave the rest as it is.<br>
   ↳ The defaults keep it private: not on the public list, no router changes (UPnP off), live player info only on this PC.
4. Press **Download and set up**.<br>
   ↳ Downloads the LMP server from GitHub, checks it against GitHub's SHA-256, unpacks it into your folder and writes
   your settings **before** it ever starts. If the .NET 6 runtime LMP needs is missing, it offers to install it
   through Windows' `winget` (Windows asks for permission).
5. Press **Start** and wait for **"All systems up and running"**.<br>
   ↳ Runs `Server.exe` without a window and shows its console inside LUNA EVO. **Stop** sends it Ctrl+C, so LMP saves
   the universe before closing (it's only force-closed if it hangs past the timeout), and LUNA EVO keeps a copy of
   the Universe from that moment.
6. Open **Connect**, press **Copy invite** and send it to your friends.<br>
   ↳ Copies a ready-to-send message: server name, the best address to use, the LMP version needed and whether there's
   a password (never the password itself). Friends can paste the whole message into their Join page.

💡 **Tips**
- **Allow through firewall** (Connect) adds one Windows Firewall rule for the game port; Windows asks for permission.
- **Generate a strong password** sits under the password settings. LMP doesn't limit password guesses, so use it.
- Players need the **LMP client mod in the same version** as your server; **App settings → Downloads** installs it.

Full guide: [Set up a new server, step by step](docs/HOSTING.md#set-up-a-new-server-step-by-step) (every question explained, with screenshots)

## Already have an LMP server

1. Put the exe next to your `Server.exe` and double-click it.<br>
   ↳ It reads the settings list from your server's own `Server.dll`, so it always matches your LMP version, and opens
   that server by itself.
2. Change settings on **Server settings** (start with **Essentials**), or click a card on the Server console.<br>
   ↳ Every setting shows LMP's own description and is checked as you type; problems appear on the setting, on its
   tab, and as a count under the server status. The name, port and player cards ask for the new value and save it
   straight away (checked first, with the same backup as Save).
3. **Save** (Ctrl+S), then **Start** or restart the server.<br>
   ↳ Shows every change (old → new) for you to confirm, copies the files it's about to change to
   `Config\Backups\<date and time>\`, then writes them. LMP only reads its config when it starts.

💡 **Tips**
- **↺** next to a setting puts LMP's default back.
- Click the warning count under the server status: every problem is listed, and each one opens the setting to fix.
- **Search all settings** (top of Server settings) finds a setting by name or description, e.g. "heartbeat".

Full guide: [Configure it](docs/FEATURES.md#configure-it) · [Host a server features](docs/FEATURES.md#host-a-server)

## Play on a server

You don't need to host anything.

**A public server**
1. Open **Play → Server browser**.<br>
   ↳ Asks LMP's own master servers (the list LMP shows in game) for every public server. **Only my LMP version** reads
   the LMP client in your KSP and hides servers you can't join.
2. Press **Join** on one (it asks for the password if the server has one), then **Launch KSP and join**.<br>
   ↳ **Join** fills the server into the Join page and checks it (below). The list shows ping times for the top 40
   servers; sort by them, and **Save** keeps a server in My servers.

**A friend's server**
1. Choose **Join a friend's server** (or **Play → Join a server**).
2. Paste the address or invite your friend sent and press **Check server**.<br>
   ↳ Looks the address up, pings it 4 times and asks the server's LMP website for live info: name, mode, players
   and vessels appear if the host shares them. It sends nothing else.
3. Check your KSP folder and that your LMP version matches the server's.<br>
   ↳ Your Steam KSP is found by itself; your CKAN installs are one click away. If the server's version is known (the
   browser and invites say it) and yours differs, **Install LMP x.y.z** appears: it downloads LMP's official client
   from GitHub, checks it, keeps your LMP server list and backs up the old version.
4. Type the password if there is one and press **Launch KSP and join**.<br>
   ↳ Starts KSP the way CKAN's Play button does. On the default port with no password, LMP connects by itself;
   otherwise the server (and password) is put at the top of LMP's own server list, so you click it once. That list
   is backed up first and never changed while KSP runs. LUNA EVO doesn't keep your password.

💡 **Tips**
- Need the server's mods? **Install the server's mods...** (next to Launch) opens the `.ckan` list your host sent and
  takes you to the CKAN page, already checked for your KSP.
- **Save to my servers** remembers the address (never the password).
- **Check my mods** (Join page) tells you what LMP would refuse before you connect.

Full guide: [Find a public server](docs/PLAYING.md#find-a-public-server-server-browser) · [Join a friend's server](docs/PLAYING.md#join-a-friends-server)

## Mods with CKAN

1. Open **Play → CKAN mods**.
2. **Download CKAN**, or **Choose ckan.exe...** if you already have it.<br>
   ↳ **Download** gets the official `ckan.exe` from CKAN's GitHub, checks it against GitHub's SHA-256 and keeps it
   in `%LOCALAPPDATA%\LunaEvo\Tools\CKAN\`. **Choose** uses yours, and tells you whether it matches an official
   CKAN release.
3. Close KSP (and CKAN's window), then load or paste the mod list your host sent.<br>
   ↳ A `.ckan` file, or mod names one per line. LUNA EVO reads **only the mod names and versions** from it; anything
   else in a file (like its own download links) is ignored, so CKAN installs only from its official catalog.
4. Pick your KSP install and press **Check with CKAN**.<br>
   ↳ A dry run: CKAN works out the whole list and dependencies, then LUNA EVO answers "no" when CKAN asks to
   continue. Nothing is downloaded or changed. It tells you whether everything can be installed, or which mod CKAN
   doesn't know. If a mod needs "one of several" others (Parallax needs a KSPBurst), LUNA EVO asks you which, with
   CKAN's own options, and adds your pick to the list.
5. Press **Install with CKAN** and confirm.<br>
   ↳ Runs `ckan update`, the same check (so any "one of several" choice is made first: CKAN's command line would
   silently leave it out), then `ckan install` with those mod names for that KSP install; CKAN's messages show as it
   goes. If something fails, CKAN undoes that install and the page says why.

💡 **Tips**
- **Cancel** stops CKAN while it runs (an install asks first: it may be part-way). **Copy log** copies CKAN's messages.
- Each install shows the other KSP versions CKAN accepts mods for there (set in CKAN's "Compatible game versions").
- Hosting? **Connect → Export my mod list...** (or **Export mod list...** here) writes a `.ckan` file for friends: just the mods you picked,
  without your Windows user name (which CKAN's own file contains). **Exact versions** gives everyone the same versions.
- **Use as mod list** copies one install's mods into the box, to set up a second KSP install the same way.

Full guide: [Using the CKAN page](docs/PLAYING.md#using-the-ckan-page) · [Find, mod and join](docs/FEATURES.md#find-mod-and-join-for-players)

## Play over the internet safely

Use a mesh VPN and never open a port on your router.

1. Everyone installs [Tailscale](https://tailscale.com/download) (free for personal use) and joins your tailnet.<br>
   ↳ Your PCs get private addresses (`100.x.y.z`) that work over the internet, as if you were on one home network.
2. On your server, keep **Upnp** and **Register With Master Server** off, and press **Allow through firewall** on
   Connect.<br>
   ↳ UPnP would open ports on your router to the whole internet; the master server would list you publicly. A banner
   on the Server console warns you whenever either is on.
3. Share the Tailscale address from **Connect**; friends use it on their Join page.

💡 **Tip:** mesh addresses are private to your group, but they're still yours: don't post them publicly.

Full guide: [Play together without opening any ports](docs/PLAYING.md#play-together-without-opening-any-ports-tailscale-twingate) (Tailscale and Twingate)

## Leave it running

- **Home server mode** (App settings → Behavior)<br>
  ↳ Adds LUNA EVO to Windows' start-up list for your user (removed when you switch it off), starts it in the
  notification area, starts the server, hides instead of quitting, and keeps the PC awake while the server runs.
- **Automation & alerts** (App settings)<br>
  ↳ Restarts the server after a crash (it stops trying after 3 crashes in 10 minutes), a daily restart with a
  one-minute warning in game chat, scheduled Universe backups, Windows or Discord notifications, chat announcements.
- **Profiles & backups**<br>
  ↳ Saves a copy of your whole config as a named profile, and zips the Universe (your game world) to restore later.
  Restoring makes a backup first, so it can be undone.

- **Map**<br>
  ↳ Reads the server's `Universe/Vessels` and your KSP's GameData (for planet packs) and draws every vessel on its
  orbit; while the server runs with its website on, positions come live from the server. Read-only: it changes nothing.
- **Map → Install the LUNA EVO server plugin** (optional, server stopped)<br>
  ↳ Copies one DLL into your server's `Plugins` folder. LMP loads it on the next start; it only watches, and writes
  every vessel's position and who flies what to `LunaEvo\live.json` for the map, every second. No network port.
  **Remove** takes it out again. Pick a body at the top, scroll to zoom, drag to move, click a ship.

💡 **Tip:** these only run while LUNA EVO is open; home server mode makes sure it is.

Full guide: [Keep it running](docs/FEATURES.md#keep-it-running)

## If something's wrong

| Problem | Try |
|---|---|
| Friends can't connect | **Connect** shows what's blocking them; press **Allow through firewall**. Same LMP version on both sides? |
| Launch KSP and join only adds the server to LMP's list | **Join → Install the LUNA EVO patches** (KSP closed): then any server, any port, with a password, connects by itself. |
| LMP says the versions don't match | Check the server on the Join page: **Install LMP x.y.z** appears when the version is known. Or App settings → Downloads. |
| CKAN asks "Which ...?" | A mod needs one of several others; pick one (the first is usually the standard choice). |
| My changes did nothing | **Save**, then restart the server: LMP reads its config only at start. |
| LMP refuses my mods | **Join → Check my mods** lists exactly what and why. |
| CKAN won't install a list | **Check with CKAN** says which mod it doesn't know; try turning off **Exact versions**. |
| A warning count under the server status | Click it: every problem is listed and opens the setting to fix. |
| A setting went wrong | `Config\Backups\` has the files from before every save; **Profiles & backups** has your saved setups. |
| Something crashed or did nothing | Look in `%APPDATA%\LunaEvo\MissionControl\logs\errors.log` and include it in a bug report. |

Still stuck? [Open an issue](https://github.com/NEON-NULL-NARWHAL/luna-evo-mission-control/issues). For security
problems, see [SECURITY.md](SECURITY.md) instead.

---

**Everything else:** [Features](README.md#what-it-does) · [Screenshots](docs/SCREENSHOTS.md#screenshots) · [Tested with](docs/FEATURES.md#tested-with) · [What it connects to](docs/FEATURES.md#what-it-connects-to) · [Security](SECURITY.md) · [Contributing](CONTRIBUTING.md) · [Changelog](CHANGELOG.md)
