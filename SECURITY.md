# Security

LUNA EVO is made for a private, home-hosted LMP server: run on your own PC, played on your own network, VPN or
mesh. It isn't hardened as a public internet service. This page covers what it protects against, what it doesn't, and
what the 0.4.0 security audit found and changed.

## Reporting a problem

Please **don't** open a public issue for a security problem. Use GitHub's **Security → Report a vulnerability** on
this repository, or message a maintainer directly. Include what you did, what happened, and the LUNA EVO version
(LUNA EVO Settings → About). We'll reply as soon as we can. This is a two-person hobby project, so there's no fixed
response time.

## What LUNA EVO touches

| | When | Notes |
|---|---|---|
| **GitHub** (`api.github.com`, `github.com`, `*.githubusercontent.com`) | Setup wizard, LMP update check and update | HTTPS only. Downloads are checked against GitHub's SHA-256 (see H1). The update check can be turned off. |
| **LMP's website on your server** (default TCP 8900) | Players page; Join page | Plain HTTP, read-only. Only what LMP itself publishes there. |
| **A friend's server** | Join page, when you press Check | DNS lookup, 4 pings (ICMP) and one HTTP request to the address you typed. Nothing else. |
| **Windows Firewall** (`netsh`, as administrator) | Only when you press "Allow through firewall" | Windows asks for permission each time. The exact command is in the button's tooltip. |
| **winget** | Only when you accept installing the .NET runtime | Installs Microsoft's `Microsoft.DotNet.Runtime.6`. |
| **Your KSP install** | Join page: "Launch KSP and join" or "Add to LMP's server list" | Starts `KSP_x64.exe` and edits LMP's own `settings.xml` server list (backed up first, and never while KSP is running). |

LUNA EVO has no telemetry, no accounts and no server of its own.

## Audit (0.4.0, October 2026)

The whole codebase was reviewed (downloads, archive handling, every JSON and XML parser, process launching, file
system walks, stored settings, and the network calls). The two findings marked *reported* were raised by the project
owner; the rest came from the review. Nothing used a known-vulnerable package, and HTTPS certificate checking is never
weakened anywhere.

### High

| # | Finding | Fix |
|---|---|---|
| H1 | **LMP server downloads weren't verified** *(reported)*. A tampered or corrupted zip would be installed and then run as your server. | Every download is checked against the SHA-256 that GitHub publishes for the release asset (`digest`), hashed while it streams, and **hashed again right before unpacking**. Downloads must be HTTPS from `github.com` or `*.githubusercontent.com`, including after redirects, and may not be bigger than the size GitHub lists. A mismatch deletes the file and stops. An old release with no published checksum is only installed after you confirm "Can't verify this download". (`ServerInstaller.DownloadAsync`, `Install`) |
| H2 | **The Discord webhook was stored in plaintext** in `%APPDATA%\LunaEvo\ConfigManager\settings.json` *(reported)*. Anyone with the file could post to that channel. | **Discord support was removed** in 0.4.0. On first start, 0.4.0 rewrites `settings.json` without the old `DiscordWebhookUrl`. If you ever set a webhook, **delete it in Discord** (channel settings → Integrations → Webhooks), because the old file may have been copied or backed up somewhere. |
| H3 | **Player names went into Discord messages unescaped.** A player named `@everyone` would ping the whole server, and names could inject formatting. | Removed along with Discord. If notifications come back, they must set `allowed_mentions: { parse: [] }` and escape Markdown. |

### Medium

| # | Finding | Fix |
|---|---|---|
| M1 | **JSON from LMP's website was read with calls that throw** (`TryGetInt32` and friends throw when a value isn't a number). A malformed or hostile response on port 8900 could crash the app from a timer. The GitHub release feed and `Server.runtimeconfig.json` had the same pattern. | Tolerant parsing everywhere: every value's type is checked before it's read, list sizes are capped, and "not LMP data" is reported as an error instead of thrown. The Players page poller can't throw. (`ServerInfoClient`, `LmpReleases`, `DotNetRuntime`) |
| M2 | **`netsh` (run as administrator) and `winget` were found through `PATH`.** A `netsh.exe` planted earlier on the PATH would receive the administrator prompt. | Absolute paths: `%SystemRoot%\System32\netsh.exe` and the WindowsApps `winget.exe`. On Linux/macOS, `/bin/kill`. |
| M3 | **No limits when unpacking zips.** A zip bomb could fill the disk. | Caps: 500 MB download, 20,000 entries, 2 GB unpacked. The existing zip-slip check (no entry may land outside the folder) is unchanged. |
| M4 | **Folder scans followed symlinks and junctions.** A loop could hang the GameData scan or a Universe backup. | Scans skip reparse points and unreadable folders. |
| M5 | **`Ui.Open` would launch anything it was given.** It was only ever given safe values, but it was a trap for future code. | It only opens `http(s)` links and existing folders (full paths). |

### Low, and things we accept by design

| # | Finding | What we did |
|---|---|---|
| L1 | **The downloaded zip had a predictable temp path**, so another local program could swap it between download and install. | A unique temp folder per download, and the hash is checked again right before unpacking (H1). |
| L2 | **Opening a server folder runs code from its `Server.dll`.** LUNA EVO reads the settings list by loading that DLL and creating LMP's settings classes, which runs their constructors. | By design: that's what keeps the GUI in step with your LMP version. It's the same trust as running that folder's `Server.exe`, so **only open server folders you'd run.** |
| L3 | **LMP keeps passwords in plaintext**: the server and admin passwords in `Config/*.xml`, and players' saved passwords in LMP's client `settings.xml`. | LMP's design. LUNA EVO never copies a password into its own settings file, backups stay inside the server's own folder, and the Join page's password goes only to LMP's list. Keep `Config/` out of anything you share (and out of git). |
| L4 | **LMP's website has no login.** When shared with players, anyone who can reach the port sees the server name, settings (never passwords), who's online and where vessels are. | Off the network by default (`ListenAddress` 127.0.0.1). Connect → **Share with players** asks first and explains this, and **Stop sharing** undoes it. Only share on a network you trust. |
| L5 | **The exe isn't code-signed**, so Windows SmartScreen may warn. | Build it yourself with `publish.ps1` if you'd rather not trust a downloaded binary. |
| L6 | **The firewall rules allow any remote address** on the one port (UDP for the game, TCP for the website). | Right for a server friends join from VPNs or the internet. To restrict it, edit the "LUNA EVO - ..." rule in Windows Firewall (Scope → Local subnet, or your mesh range). |

### How the fixes were checked

Each fix has automated tests (171 in total): tampered, oversized, wrong-host, redirected and unverified downloads; a
zip changed between download and install; a zip with too many entries; hostile and malformed website, GitHub and
runtimeconfig JSON; the absolute `netsh` path; the webhook scrub; `Ui.Open` refusing programs and `file:` links. The
checksum check was also run against the real LMP 0.29.2 release, whose published digest matched.
