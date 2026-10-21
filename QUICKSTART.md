# Quickstart

The short version of everything in the [README](README.md). Pick what you want to do, follow a few steps, and use
the **Full guide** links whenever you want the details.

**[Get it](#get-it)** · **[Host a server](#host-a-server)** · **[Already have a server](#already-have-an-lmp-server)** · **[Play on a server](#play-on-a-server)** · **[Mods with CKAN](#mods-with-ckan)** · **[Play over the internet](#play-over-the-internet-safely)** · **[Leave it running](#leave-it-running)** · **[If something's wrong](#if-somethings-wrong)**

## Get it

1. Download **`LunaEvo.MissionControl.exe`** from the [latest release](https://github.com/NEON-NULL-NARWHAL/luna-evo-mission-control/releases/latest).
   One file, Windows 10/11, nothing to install.
2. Run it. If Windows SmartScreen asks: **More info → Run anyway** (the exe isn't code-signed yet).
3. That's the last download: it updates itself (App settings → Updates).

New to all this? Turn on **Simple mode** (App settings → Behavior) to see only what you need.

Full guide: [Download](README.md#download) · [Getting started](README.md#getting-started)

## Host a server

About two minutes, no files to edit.

1. Start LUNA EVO and choose **Set up a new server**.
2. Pick an empty folder (e.g. `D:\Games\LMPServer`) and the LMP version (the newest is picked for you).
3. Answer the questions: name, password, game mode, difficulty, players. Leave the rest as it is; it starts private.
4. Press **Download and set up**. Accept the .NET 6 install if it offers one.
5. Press **Start** and wait for **"All systems up and running"**.
6. Open **Connect**, press **Copy invite** and send it to your friends.

Players need the **LMP client mod in the same version** as your server; they can install it from LUNA EVO
(App settings → Downloads).

Full guide: [Set up a new server, step by step](README.md#set-up-a-new-server-step-by-step) (every question explained, with screenshots)

## Already have an LMP server

1. Put the exe next to your `Server.exe` and double-click it. It opens that server by itself.
2. Change settings on **Server settings** (start with the **Essentials** tab), or click a card on the Server console.
3. **Save** (Ctrl+S), then **Start** or restart the server. LMP only reads its config when it starts.

Full guide: [Configure it](README.md#configure-it) · [Host a server features](README.md#host-a-server)

## Play on a server

You don't need to host anything.

**A public server**
1. Open **Play → Server browser**. It shows servers that match your LMP version.
2. Press **Join** on one, then **Launch KSP and join**.

**A friend's server**
1. Choose **Join a friend's server** (or **Play → Join a server**).
2. Paste the address or invite your friend sent and press **Check server**.
3. Check your KSP folder and that your LMP version matches the server's.
4. Type the password if there is one and press **Launch KSP and join**.

Full guide: [Find a public server](README.md#find-a-public-server-server-browser) · [Join a friend's server](README.md#join-a-friends-server)

## Mods with CKAN

1. Open **Play → CKAN mods**.
2. **Download CKAN**, or **Choose ckan.exe...** if you already have it.
3. Close KSP (and CKAN's window), then load or paste the mod list your host sent (a `.ckan` file, or mod names one per line).
4. Pick your KSP install, press **Check with CKAN** to see that it works, then **Install with CKAN**.

Hosting? **Export mod list...** on your own install gives friends a `.ckan` file, and **Join → Check my mods** shows
anyone what LMP would refuse before they connect.

Full guide: [Using the CKAN page](README.md#using-the-ckan-page) · [Find, mod and join](README.md#find-mod-and-join-for-players)

## Play over the internet safely

Use a mesh VPN and never open a port on your router.

1. Everyone installs [Tailscale](https://tailscale.com/download) (free for personal use) and joins your tailnet.
2. On your server, keep **Upnp** and **Register With Master Server** off, and press **Allow through firewall** on Connect.
3. Share the Tailscale address (`100.x.y.z`) from **Connect**; friends use it on their Join page.

Full guide: [Play together without opening any ports](README.md#play-together-without-opening-any-ports-tailscale-twingate) (Tailscale and Twingate)

## Leave it running

- **Home server mode** (App settings → Behavior): starts with Windows, starts the server, keeps the PC awake.
- **Automation & alerts** (App settings): restart after a crash, a daily restart, scheduled Universe backups,
  Windows or Discord notifications, chat announcements.
- **Profiles & backups**: save whole setups, snapshot and restore the Universe.

Full guide: [Keep it running](README.md#keep-it-running)

## If something's wrong

| Problem | Try |
|---|---|
| Friends can't connect | **Connect** shows what's blocking them; press **Allow through firewall**. Same LMP version on both sides? |
| LMP says the versions don't match | Install the server's LMP version: App settings → Downloads. |
| My changes did nothing | **Save**, then restart the server: LMP reads its config only at start. |
| LMP refuses my mods | **Join → Check my mods** lists exactly what and why. |
| CKAN won't install a list | **Check with CKAN** says which mod it doesn't know; try turning off **Exact versions**. |
| A warning count under the server status | Click it: every problem is listed and opens the setting to fix. |
| Something crashed or did nothing | Look in `%APPDATA%\LunaEvo\MissionControl\logs\errors.log` and include it in a bug report. |

Still stuck? [Open an issue](https://github.com/NEON-NULL-NARWHAL/luna-evo-mission-control/issues). For security
problems, see [SECURITY.md](SECURITY.md) instead.

---

**Everything else:** [Features](README.md#what-it-does) · [Screenshots](README.md#screenshots) · [Tested with](README.md#tested-with) · [Security](SECURITY.md) · [Contributing](CONTRIBUTING.md) · [Changelog](CHANGELOG.md)
