# Security

LUNA EVO is made for a private, home-hosted LMP server: run on your own PC, played on your own network, VPN or
mesh. It isn't hardened as a public internet service. This page covers what it protects against, what it doesn't, and
what the two security audits (0.4.0 and 0.4.1) found and changed.

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
| **Your KSP install** | Join page: "Launch KSP and join" or "Add to LMP's server list" | Starts `KSP_x64.exe` and edits LMP's own `settings.xml` server list (backed up first, and never while KSP is running). "Check my mods" only reads your GameData. |
| **Discord** (`discord.com`) | Only if you save a webhook (Settings → Notifications) | HTTPS posts of the notifications. The URL is stored encrypted for your Windows account (DPAPI) and never shown again; posts can't mention anyone. |
| **Windows notifications** | Settings → Notifications (on by default) | Shown on this PC only. |

LUNA EVO has no telemetry, no accounts and no server of its own.

**What it keeps about players** (0.5): `<server>/LunaEvo/players.json` holds player names, LMP's ids for them (needed
to unban), when they played, their LMP/KSP versions, wrong admin passwords, ban reasons and your notes. **Never IP
addresses**, even though LMP's own log lines contain them. It stays next to your server; delete it to forget everyone.

## Security notes on the 0.5 features

- **Discord is back, with audit 1's H2 and H3 fixed:** the webhook is encrypted with Windows DPAPI (useless on
  another account or PC), never displayed again after saving, and every post sends `allowed_mentions: { parse: [] }`
  and escapes player-controlled text, so a player named `@everyone` can't ping anyone or add links.
- **Alerts come from LMP's log.** LUNA EVO only reads LMP's own server messages, never chat, and only accepts player
  names LMP itself allows. A modified client could still fake a log line with a line break in a chat message; the
  worst that does is a wrong history entry or a false alert, never a command.
- **Unban edits LMP's ban list** only while the server is stopped, after backing it up.
- **The mod check** reads files only; it never changes your GameData.

## Audit 2 (0.4.1, October 2026)

A second pass over the whole codebase, focused on the 0.4.0 Join code and on areas the first audit only skimmed:
files LUNA EVO unpacks or names, XML parsing, text sent to the server console, and the release itself. Also checked:
NuGet packages (no known vulnerabilities), console commands (line breaks are already stripped, so a kick reason
can't smuggle in a second command), the published exe (no build paths or user names inside), and **the whole git
history** (no real passwords, IP addresses, player names or user paths).

| # | Severity | Finding | Fix |
|---|---|---|---|
| A1 | Medium | **LMP itself doesn't limit password guesses.** The admin password is compared on every admin message with no rate limit, and the admin can ban, kick, clear the KSC and restart the server. A short admin password can be guessed by any connected player in seconds. | LUNA EVO now warns when the admin password is under 10 characters and the server password under 6 (saving still works). The real fix, rate limiting, belongs in our LMP fork. **If your passwords are short numbers, change them.** |
| A2 | Medium | **Live info had no size limit.** The Join page reads from any address you type; whatever answers could send a reply of up to 2 GB (memory) or follow a redirect to another host. | Headers first, a 4 MB cap, a 15-second limit, no redirect to another host, text cut to 256 characters. |
| A3 | Medium | **Universe restore deleted the current Universe before unpacking.** A damaged or hostile backup zip would leave a half-restored Universe (the safety snapshot made it recoverable, but by hand). No size limits on unpacking. | Unpacks into a staging folder with file-count, size and stay-inside checks, and only then swaps it in. |
| A4 | Low | **XML files could contain a DTD**, allowing entity expansion (bounded by .NET to 10 million characters). | DTDs are refused; LMP's files never have one. |
| A5 | Low | **A profile named `CON`, `NUL`, `COM1`...** maps to a Windows device, not a folder. | Such names get a suffix; names are capped at 80 characters. |
| A6 | Low | **A hand-edited LUNA EVO settings file with `null` values** could cause errors later. | Values are normalised on load. |
| A7 | Low | **Plugin hashing read whole DLLs into memory.** | Streamed. |

## Audit 1 (0.4.0, October 2026)

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

Each fix has automated tests (183 in total after audit 2): tampered, oversized, wrong-host, redirected and unverified downloads; a
zip changed between download and install; a zip with too many entries; hostile and malformed website, GitHub and
runtimeconfig JSON; the absolute `netsh` path; the webhook scrub; `Ui.Open` refusing programs and `file:` links. The
checksum check was also run against the real LMP 0.29.2 release, whose published digest matched. Audit 2's fixes
are covered by `SecurityRound2Tests`: oversized and long-text live info, escaping and damaged Universe zips (the
current Universe survives), DTDs, device names, password warnings and null settings.
