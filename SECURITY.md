# Security

LUNA EVO is made for a private, home-hosted LMP server: run on your own PC, played on your own network, VPN or
mesh. It isn't hardened as a public internet service. This page covers what it protects against, what it doesn't, and
what the ten security audits (0.4.0 to 2.1.1) found and changed, newest first, and which real
software each version was tested against.

## Reporting a problem

Please **don't** open a public issue for a security problem. Use GitHub's private
**[Security → Report a vulnerability](https://github.com/NEON-NULL-NARWHAL/luna-evo-mission-control/security/advisories/new)**
on this repository, or message a maintainer ([@NEON-NULL-NARWHAL](https://github.com/NEON-NULL-NARWHAL)). Include what you did, what happened, and the LUNA EVO version
(LUNA EVO App settings → About). We'll reply as soon as we can. This is a two-person hobby project, so there's no fixed
response time.

## What LUNA EVO touches

| | When | Notes |
|---|---|---|
| **GitHub** (`api.github.com`, `github.com`, `*.githubusercontent.com`) | Setup wizard, LMP update check and update | HTTPS only. Downloads are checked against GitHub's SHA-256 (see H1). The update check can be turned off. |
| **LUNA EVO's own GitHub releases** | On startup (can be turned off) and "Check now"; "Update and restart" | The same verified download as LMP's: HTTPS from GitHub only, SHA-256 checked while downloading and again before installing, the exe's version checked. Releases without a checksum are never offered. |
| **LMP's website on your server** (default TCP 8900) | Players page; Join page | Plain HTTP, read-only. Only what LMP itself publishes there. |
| **A friend's server** | Join page, when you press Check | DNS lookup, 4 pings (ICMP) and one HTTP request to the address you typed. Nothing else. |
| **Windows Firewall** (`netsh`, as administrator) | Only when you press "Allow through firewall" | Windows asks for permission each time. The exact command is in the button's tooltip. |
| **winget** | Only when you accept installing the .NET runtime | Installs Microsoft's `Microsoft.DotNet.Runtime.6`. |
| **Your KSP install** | Join page: "Launch KSP and join" or "Add to LMP's server list" | Starts `KSP_x64.exe` and edits LMP's own `settings.xml` server list (backed up first, and never while KSP is running). "Check my mods" and the Map only read your GameData. |
| **Discord** (`discord.com`) | Only if you save a webhook (App settings → Notifications) | HTTPS posts of the notifications. The URL is stored encrypted for your Windows account (DPAPI) and never shown again; posts can't mention anyone. |
| **Windows notifications** | App settings → Notifications (on by default) | Shown on this PC only. |
| **Windows startup list** (`HKCU\...\Run`) | Only while Home server mode is on | One entry, "LUNA EVO Mission Control" ("LUNA EVO Config Manager" before 2.0, removed on upgrade), for your user only; removed when you switch the mode off. |
| **CKAN and the LMP client** (GitHub) | CKAN page and App settings → Downloads; setup wizard step 3 if switched on | The same verified download as LMP's server: GitHub only, SHA-256 checked (and again where it's stored or before unpacking). Nothing unverified. The client is unpacked with size and stay-inside checks, only into `GameData/LunaMultiplayer` (and `000_Harmony` if missing). |
| **CKAN's command line** (`ckan.exe`: LUNA EVO's verified copy, or your own if you chose one) | CKAN page → Install with CKAN, after you confirm | `ckan update` and `ckan install <mod names>` for the KSP install you pick, never while KSP or CKAN has it open. Only mod names and versions from a list are passed (a `.ckan` file itself never is). CKAN then downloads from its own catalog. |
| **Public servers' addresses** (ICMP ping) | Server browser: the 40 servers at the top of the list, once each | Two pings per server, nothing else, to measure the delay. |
| **LMP's master servers** (`ms.lmp.dasskelett.dev`, `lmp.nightshade.fun`, and their nightly lists) | Server browser: the first time you open it, and Refresh | One HTTPS GET each for the public list (LMP's own `/json`). Read with an 8 MB cap; names and descriptions cut and cleaned; only IP addresses accepted; a server's website link opens only if it's http(s). Joining a listed server is the Join page's usual check (DNS, pings, its LMP website). |
| **Crafts from friends** | Crafts page → Import | Read as data with size limits; written only into a KSP save's Ships folder or the server's craft library, never overwriting. |

LUNA EVO has no telemetry, no accounts and no server of its own.

**What it keeps about players** (0.5): `<server>/LunaEvo/players.json` holds player names, LMP's ids for them (needed
to unban), when they played, their LMP/KSP versions, wrong admin passwords, ban reasons and your notes. **Never IP
addresses**, even though LMP's own log lines contain them. It stays next to your server; delete it to forget everyone.

## Audit and test history

Newest first. Each audit's findings and fixes are below the table; the real software each version was tested against
is under [Tested with](#tested-with).

| Version | What it added (security-relevant) | Audit | Checked for real | Tests |
|---|---|---|---|---|
| 2.2.0 | Solar system map: reads `Universe/Vessels`, `StartTime.txt`, `Subspace.txt` and Kopernicus `.cfg` files in GameData, read-only | Notes below | The owner's real GameData (47 bodies, 33 from planet packs) and a sanitized copy of a real 51-vessel universe; a looping or star-removing pack fixed before release | 315 |
| 2.1.1 | | **Audit 10** (J1-J9) | 2.1.0's features against live services (LMP client from GitHub into a sandbox KSP, browser pings, CKAN choices); a 3-minute soak; 10,500 fuzzed parser inputs; firewall rule quoting against the real netsh | 303 |
| 2.1.0 | Install the server's mods, LMP version helper, browser pings, CKAN provider choices, Cancel | - | Real ckan.exe 1.36.4: headless installs skip "which provider" questions (found and fixed); the new choice flow dry-ran Parallax with 4 choices on a clean KSP folder | 291 |
| 2.0.1 | GitHub repository renamed to `luna-evo-mission-control` | - | Old API and download URLs redirect (checked live, including with the updater built with the old name) | 282 |
| 2.0.0 | Renamed to LUNA EVO Mission Control (exe, settings folder, startup entry; old copies still update) | **Audit 9** (I1-I8) | Old-name update path and settings migration tested; the published exe started from the demo copy | 282 |
| 1.4.0 | Server browser, simple mode, quick edits from the console cards, allowing a player's parts, CKAN dry run | **Audit 8** (H1-H3) | The browser read 368 real public servers from LMP's master servers; Check with CKAN dry-ran a real 108-mod list on a throwaway KSP folder | 276 |
| 1.3.1 | | **Audit 7** (G1) | The owner's own `ckan.exe` identified as official v1.36.4; the export of their real install matched the original modpack they installed it from (same 108 mods) | 260 |
| 1.3.0 | Use your own ckan.exe, export an install's mod list | Notes below | Exported the owner's real 108-mod CKAN list (no user name in it); the real `ckan.exe` 1.36.4 installed an exported list with `-c` into a throwaway KSP folder | 259 |
| 1.2.1 | | **Audit 6** (F1-F5) | The real `ckan.exe` 1.36.4 runs with its input closed | 256 |
| 1.2.0 | CKAN page, install a mod list with CKAN | **Audit 5** (E1-E4) | The real `ckan.exe` 1.36.4 installed a mod list by name into a throwaway KSP folder | 248 |
| 1.1.0 / 1.1.1 | CKAN copies, LMP client installer, Join with CKAN | - | GitHub's digests matched the real CKAN 1.36.4 `ckan.exe` and LMP 0.29.2 client zip | 240 |
| 1.0.1 | | **Audit 4** (D1-D2) | | |
| 0.9.0 / 1.0.0 | Server health, invites, vessel cleanup | - | | 234 |
| 0.8.1 | | **Audit 3** (C1-C3) | | |
| 0.8.0 | Settings search, chat announcements, home server mode | - | | |
| 0.7.x | Crafts sharing, automatic Universe backups | - | | |
| 0.6.0 / 0.6.1 | Self-update, password generator, internet-exposure banner, CI | Notes below | | |
| 0.5.0 | Player history, alerts, bans, mod check, notifications | Notes below | | |
| 0.4.1 | | **Audit 2** (A1-A7) | | 183 |
| 0.4.0 | Join page, verified downloads, Discord removed | **Audit 1** (H1-H3, M1-M5, L1-L6) | The real LMP 0.29.2 release's SHA-256 digest matched; Join checked a real running server | |
| 0.3.0 | Setup wizard and LMP updater, server control, players, mod import | - | The wizard installed the real LMP 0.29.2 server, which started cleanly on the generated config; GameData scan of a real modded KSP 1.12.5 (1,527 parts, 132 plugins) | |

Blank cells mean nothing new of that kind for that version, not that nothing was checked: every version ran the full
automated test suite before release.

## Security notes on 2.2.0 (the solar system map)

- **Read-only.** The Map reads the server's `Universe/Vessels/*.txt`, `StartTime.txt` and `Subspace.txt`, and
  Kopernicus configs in your KSP's GameData. It writes nothing and connects to nothing.
- **GameData is outside text** (mods anyone can publish): only `*.cfg` files under 4 MB, reparse points skipped,
  parsed with the same `KspConfigParser` as the GameData scan (fuzzed in audit 10); numbers must be finite.
- **Found while testing:** a pack that made two bodies orbit each other would have hung the map (walking up the
  parents never ends), and one that removed or re-parented the Sun would have crashed it. Bodies are now kept only
  if their parents reach the star within 64 steps, and the Sun can't be removed or moved. `MapTests` covers both.
- **Odd save values** (ECC exactly 1, huge or zero SMA, times far from the epoch) give finite positions; Kepler's
  equation is solved with an iteration cap. Fuzzed in `MapTests`.

## Audit 10 (2.1.1, October 2026)

The deep dive planned in 2.0: line-by-line reading of what earlier audits hadn't read (player tracking, the firewall
commands, the server and LMP client installers, the self-update swap), every background event checked for UI-thread
use, a fuzz test of every parser that reads outside text, a 3-minute soak with the fake server, code coverage, and a
full security check of the repository, its history and the published exe. 2.1.0's new features were also run against
the real services.

| # | Severity | Finding | Fix |
|---|---|---|---|
| J1 | Medium | **A whole number too big for the setting's own type passed validation** (3000000000 for an Int32 like MaxPlayers). LMP can't read such a file, and then runs that whole file on its defaults, which for General means no password. | Each whole number must fit its setting's .NET type (byte, short, int, uint...). |
| J2 | Low | **A pasted mod list with a line of only commas or spaces** made the parser throw, so Check and Install silently did nothing. Found by the fuzz test. | Such lines are skipped. |
| J3 | Low | **Broken Unicode in pasted mod-list text** threw an error the parser didn't catch. Found by the fuzz test. | Reported as "not a valid .ckan file". |
| J4 | Low | **Huge numbers** in an invite's "LMP version" line or a setting description's limits ("Max 99999999999 char") threw OverflowException. Found by the fuzz test. | Read with non-throwing parses; an impossible number is no version or no limit. |
| J5 | Low | **players.json was rewritten for every online player every 5 seconds** (each website poll), for days on a home server. Ban reasons and clients' version strings were stored at any length. | "Last seen" alone is written at most once a minute; real changes still at once. Text is capped. |
| J6 | Low | **The LMP server installer checked for unsafe paths while extracting**, so a bad entry could leave a half-updated server, and **its size limit trusted the sizes the zip claims**. (Downloads are SHA-256 checked, so this needs an unverified old release you agreed to.) | Every path is checked before anything is written; the bytes actually written are counted. |
| J7 | Low | **A netsh that didn't answer within 5 s** made the firewall-rule check throw. | Treated as "don't know". |
| J8 | Low | **The repository's GitHub security features were off**: secret scanning, push protection, Dependabot alerts and private vulnerability reporting, so "Report a vulnerability" (below) wasn't actually there. | Switched on with the owner's OK after 2.1.1: secret scanning with push protection, Dependabot alerts and security updates, and private vulnerability reporting. GitHub reported no alerts. |
| J9 | Info | The Core tests used **xunit 2.9.3, marked legacy** (the UI tests are on xunit v3). Test-only. | Tried after 2.1.1 and reverted: with the Core tests on xunit v3 (their own process), GitHub's runner never got the UI tests' fake server running (every server test timed out); locally all passed. Kept on 2.9.3 until that's understood; the warning is a deprecation notice, not a vulnerability. |

Checked and fine: every background event (server output, state, exit, player events) reaches the UI through the
dispatcher; the self-update swap (SHA-256 checked twice, a Windows program, rename with rollback); the firewall rule
name's quoting, against the real netsh; the LMP client installer (staging folder, checksum required, Harmony never
replaced). The soak (351 commands, page switches and polls over 3 minutes) ended with 427 handles and 17 threads
against 431 and 21 after warm-up. Core code coverage is 85%; what's left is mostly code that drives Windows itself
(winget, netsh, the Ctrl+C helper). No vulnerable NuGet packages; nothing personal in the git history, the tracked
files or the published exe; release checksums match the local build; the CI token is read-only.

2.1.0 checked for real: **Install LMP 0.29.2** downloaded LMP's client from GitHub, verified it and installed it into a
sandbox KSP; the browser pinged live public servers (363 listed); CKAN's provider choices were dry-run with Parallax's
four on a clean KSP folder. Also reported upstream: CKAN issues
[#4726](https://github.com/KSP-CKAN/CKAN/issues/4726) (headless installs skip provider questions),
[#4727](https://github.com/KSP-CKAN/CKAN/issues/4727) (an unknown mod crashes the command line) and
[#4728](https://github.com/KSP-CKAN/CKAN/issues/4728) (the Windows user name in `installed-*.ckan`).

Accepted: the update's version check is skipped if the new exe's version can't be read; its SHA-256 already matches
GitHub's published one, which proves it's the released file.

## Audit 9 (2.0.0, October 2026)

A deep dive with the rename: instead of searching for risky patterns, the code that matters most was read line by
line (server start/stop and the outside-server log follower, config saving, profiles and Universe restore, the
automation timers, launching KSP and CKAN).

| # | Severity | Finding | Fix |
|---|---|---|---|
| I1 | Medium | **LMP's config files were written in place** (truncate, then write). A crash, power cut or full disk mid-save left a half-written XML file, and LMP resets a file it can't read to defaults on its next start. | Written to `<file>.tmp`, flushed to disk, then swapped in. |
| I2 | Medium | **The outside-server log follower could close the app.** It runs on a timer thread and only caught `IOException`; any other error there (e.g. access denied on the logs folder) ends a .NET app. It could also skip lines written while it read. | Every error is caught (unexpected ones go to `errors.log`); it continues from what it actually read. |
| I3 | Medium | **A failed backup after stopping stopped the daily restart.** The on-stop Universe copy wasn't caught, so a full disk or locked file meant the server was stopped and never started again. | The copy's failure is reported in the console; the restart goes on. |
| I4 | Low | **A Server.exe Windows refuses to run** (blocked by antivirus, not a program) left the state on "Starting" for good: Start was dead until the app restarted. | Back to Stopped with the reason in the console. Tested with a fake Server.exe that isn't a program. |
| I5 | Low | **An instant startup failure could crash the app**: LMP's Fatal line arrived on another thread before the new process was recorded, so the previous, disposed process was used. | The process is recorded before it starts; closing its input never throws. |
| I6 | Low | **A daily restart time that passed while the server was stopped** was kept, so the next start restarted the server straight away. | Skipped and moved to the next day. |
| I7 | Low | **Unhandled errors on buttons**: switching or saving a profile, restoring a damaged Universe backup, Windows refusing to start KSP or CKAN, killing an outside server that had just exited. The page said nothing (since 1.4.0 they were only logged). | Each says what happened (a bad backup: "your current Universe wasn't changed"). |
| I8 | Low | **Commands after LMP closed its input** threw an unexpected error type. | Reported as "the server isn't accepting commands right now". |

The rename itself: the Discord webhook's DPAPI key keeps the old name (changing it would make saved webhooks
unreadable); settings are copied (not moved) from `%APPDATA%\LunaEvo\ConfigManager`; the old Windows startup entry
is removed when the new one is written; the updater accepts either exe name and releases carry both, each with its
own GitHub SHA-256. Re-checked: NuGet packages (no known vulnerabilities).

## Audit 8 (1.4.0, October 2026)

A debug, debrief and audit pass after the owner's own testing: their CKAN install failing with no explanation, a
failing test on GitHub, and everything 1.4.0 adds (server browser, simple mode, editable console cards, allowing a
player's parts, the CKAN dry run, the error log).

| # | Severity | Finding | Fix |
|---|---|---|---|
| H1 | Medium | **Impossible port numbers could be saved.** LMP's settings only say the port is a number, so 70000 or 0 passed validation; LMP would start, fail to listen, and nobody could connect. Found by the new quick-edit test. | Game and website ports must be 1-65535 (an error, which blocks saving). |
| H2 | Low | **Errors inside button handlers weren't logged anywhere**, and CKAN's own failures showed only "code 127". The owner's failed CKAN install left nothing to go on. | Unexpected errors go to `%APPDATA%\LunaEvo\ConfigManager\logs\errors.log` (`MissionControl` since 2.0; 1 MB cap) and the app carries on. The CKAN page catches everything in its flow, says what CKAN said in plain words ("CKAN doesn't know the mod X"), and has a **Check with CKAN** dry run. |
| H3 | Low | **Two flaky tests failed on GitHub**: one counted folders in a temp folder other tests use at the same time, one timed out waiting for the fake server on a slow runner. | The first checks only its own file; waits are three times longer on CI. |

Reviewed and fine: the server browser (HTTPS, size cap, stranger-supplied text cleaned, IPs only, http(s) links only);
quick edits (validated, then saved through the normal Save with backup, review and "server is running" warning;
an invalid or canceled edit is put back); "Allow them on my server" (asks first, explains turning Mod Control off and
its risk, saves with a backup; names go through XDocument, so nothing can break the XML); the CKAN dry run (CKAN is
answered "no" and its input then closed, so it can never wait or install); simple mode only hides pages.

Not reproduced: the owner's CKAN failure. CKAN, the list and both the debug and released builds work on a sandbox
install; the real install wasn't touched. With 1.4.0 the page and `errors.log` say what went wrong.

## Audit 7 (1.3.1, October 2026)

The day's last review: the 1.3.0 code (your own ckan.exe, exports), tested against the owner's real files, read-only:
their own `ckan.exe`, their KSP install's CKAN data, and the original modpack (`.ckan`) that install was set up from.

| # | Severity | Finding | Fix |
|---|---|---|---|
| G1 | Low (correctness) | **`any_of` entries in a mod list** ("one of these mods", allowed by CKAN's format) were dropped without a word. | Counted and reported ("N choice(s) skipped; install those in CKAN"). A `min_version` is treated as "no exact version". |

Checked for real: the owner's `ckan.exe` is recognized as official CKAN v1.36.4 (SHA-256 matches GitHub) and runs with
its input closed; importing the original modpack reads all 108 mods with no problems; exporting the install gives the
same 108 mods (it pins the installed versions, the original pins none, and every one is in CKAN's `registry.json`),
re-imports identically, and contains no user name. Not done: installing all 108 mods for real (many GB); the command
line LUNA EVO would run fits in one command (113 arguments).

Reviewed and fine: build has no warnings; no leftover code from the moved Downloads tab; the own-exe check runs once
per path and never blocks the window.

## Security notes on 1.3.0 (your own CKAN, exporting mod lists)

- **Your own ckan.exe** (CKAN page → Already have CKAN?) is run because you chose it: it must be a Windows program
  (`.exe`, `MZ`), and LUNA EVO compares its SHA-256 with GitHub's digests for CKAN's official releases and says
  whether it matches ("Official CKAN v1.36.4") or not. A copy that doesn't match is still used, clearly labeled.
  If it's moved or deleted, LUNA EVO's own verified copy is used again.
- **Exported mod lists are written fresh**, never copied: CKAN's own `installed-<instance>.ckan` has your **Windows
  user name** as its author. The export has only mod names, versions (optional) and the KSP version range, so it's
  safe to send. Paths under your user folder are shown as `%USERPROFILE%\...` on the page.
- Importing works as before (audit 5, E1): only names and versions are read from any list.

## Audit 6 (1.2.1, October 2026)

A sweep of the whole codebase by pattern, not just the newest code: every process start, every delete, every file
and network read, every "safe name" helper and every JSON reader. Also re-checked: NuGet packages (no known
vulnerabilities, including transitive ones), the CI workflow (read-only token), the whole git history and every
tracked file (no passwords, real IP addresses, player names or user paths; only documentation addresses like
`203.0.113.9` and fake test webhooks).

| # | Severity | Finding | Fix |
|---|---|---|---|
| F1 | Low | **Names made only of dots passed the CKAN version filter.** A release tagged `..` would have made `Remove` delete `%LOCALAPPDATA%\LunaEvo\Tools` and a download land one folder up. Only reachable through CKAN's own GitHub releases or our folder names, but a trap. The server download's file name had the same gap. | Dot-only names become `unknown` / `download.zip`. Tested with `Remove("..")`. |
| F2 | Low | **Loading a `.ckan` file had no size limit**, so picking a huge file by mistake could freeze the window. | Files over 5 MB are refused with a message (modpacks are a few KB). |
| F3 | Low | **`ckan.exe` inherited LUNA EVO's input.** If CKAN ever asked a question, the install would wait forever with no way to cancel. | Its input is closed at start, so a question gets "no answer" and CKAN stops with an error instead. Checked with the real ckan.exe. |
| F4 | Low | **A friend's craft named `CON`, `NUL`, `COM1`...** would be saved under a Windows device name (profiles already handled this). | Shared check; such crafts get a `-craft` suffix. |
| F5 | Low | **GitHub's release lists were buffered whole**, with no cap (HTTPS from `api.github.com` only, so low risk). | Read with a 10 MB cap, like every other network read. |

Reviewed and fine: every process LUNA EVO starts is a fixed system tool, its own exe, KSP's exe or a verified
download, with arguments as a list (never a shell); every recursive delete is inside a folder LUNA EVO made, under a
cleaned name; every JSON reader checks types before reading; CKAN's launch options only pass plain `-option` words.
Accepted: GitHub Actions are pinned to official major versions (`@v4`), not commit hashes.

## Audit 5 (1.2.0, October 2026)

A deeper pass over everything since 1.0 (CKAN and LMP client downloads, the new mod-list install) and a re-read of
the older hot paths for robustness and wasted work.

| # | Severity | Finding | Fix |
|---|---|---|---|
| E1 | High (design) | **A `.ckan` file can carry its own `download` URL** (and `install_to` paths). Handing a pasted file to `ckan install -c` would let whoever wrote it put any DLL into GameData, and KSP runs every DLL there. | The file is **never passed to CKAN**. Only mod identifiers (CKAN's own rule: letters, digits, `-`, `_`, not starting with `-`, so none can read as an option) and versions (`[A-Za-z0-9.:+_~-]`) are read out and passed as `ckan install Mod Mod=version` with `--headless --no-recommends`, so everything comes from CKAN's catalog. At most 2,000 mods; arguments go through `ArgumentList`, never a shell. Checked with the real ckan.exe 1.36.4 on a throwaway KSP folder. |
| E2 | Medium | **`settings.json` was written in place**: a crash or power cut mid-write could leave it half-written, losing saved servers and the Discord webhook. | Written to `settings.json.tmp`, then swapped in. |
| E3 | Low | Mod-list install could run while CKAN had the install open (two writers on CKAN's registry). | Refused while `CKAN/registry.locked` names a live process (or can't be read); a lock left by a closed CKAN doesn't block. Also refused while KSP runs. |
| E4 | Low (efficiency) | The Players history was rebuilt every 5 s, announcements parsed every second, the Join auto-refresh repeated DNS and four pings every 10 s, and every mod check / craft import re-read thousands of GameData files. | Redraw only on change (tracker version, ban file time, search, online list); parse once per change; auto-refresh reads only live info; GameData scans reused for 2 minutes. |

Reviewed and fine: the CKAN page shows `%LOCALAPPDATA%` instead of user paths; the process list check disposes what
it opens; CKAN's own output is shown as plain text, never run or parsed for commands. Spelling: a dictionary pass over
every string, comment and doc found only LMP's own `Hearbeat`, now shown spelled right.

## Audit 4 (1.0.1, October 2026)

The 1.0 review: vessel cleanup, server health, invites and the notification icon's timer, plus the usual re-checks
(NuGet packages: no known vulnerabilities; the whole git history: no passwords, IP addresses, player names or user
paths).

| # | Severity | Finding | Fix |
|---|---|---|---|
| D1 | Medium | **Vessel cleanup deleted vessels even if its safety backup had skipped files** (a file another program had open is skipped, not copied). The "undo" could then be missing data. | If the backup couldn't read every Universe file, the incomplete backup is deleted and **nothing is removed**; the page says why. Tested with a locked vessel file. |
| D2 | Low | **The notification icon's removal timer ran on another thread** with no lock, so a notification arriving at the same moment could lose its icon (and not show). | One lock around showing and removing. |

Reviewed and fine: vessel cleanup only removes files inside this server's `Universe/Vessels`, only while the server is
stopped, never crewed vessels in a bulk selection; server health only reads the server's own process; the invite
never contains the password or your internet address (it prefers your VPN/mesh address, then your home network).

## Audit 3 (0.8.1, October 2026)

Everything added since audit 2: player history and bans, the mod check, Windows and Discord notifications, the
self-updater, craft sharing, home server mode, chat announcements and settings search. Also re-checked, now that the
repository is public: NuGet packages (no known vulnerabilities) and **the whole git history** (no passwords, IP
addresses, player names or user paths; commits do carry the maintainers' contact email as author).

| # | Severity | Finding | Fix |
|---|---|---|---|
| C1 | Medium | **`.lunacraft` files from friends were read up to the size each entry *claims*.** A zip's sizes are written by whoever made it, so a crafted file could claim a small craft and unpack to gigabytes (a "zip bomb"), filling memory. | Each entry is now read with a hard 20 MB limit, whatever it claims; a test opens a real bomb (under 1 MB on disk) and is refused. |
| C2 | Low | **Mod rules files from a host had no size limit.** | 20 MB cap before parsing (they're a few MB even for big modpacks). |
| C3 | Low | **The post-update clean-up read the exe path when it ran, 15 s later**, not when it was scheduled. Harmless in normal use (one window per process), but a second window could delete another's `.old` file. | The path is captured when the clean-up is scheduled. |

Reviewed and fine as designed: Discord webhook (encrypted, never shown, no mentions, escaped); the updater (GitHub only,
SHA-256 twice, `MZ` and version checks, rename with rollback, never unverified); home server mode (a per-user Windows
startup entry with the exe path quoted, removed when switched off; keep-awake only while the server runs; no admin);
chat announcements (your own text, one line each, sent through the same path that strips line breaks); player
history (LMP's own messages only, names checked, never IPs); craft import (only into a KSP save's Ships folder or the
server's library, never overwriting, file names cleaned).

Known and accepted: a modified game client can fake a log line inside a chat message (worst case: a wrong history
entry or a false alert); craft files are KSP's own format and are passed to KSP as they are, so a hostile craft can
only do what KSP lets a craft file do.

## Security notes on self-update (0.6)

- LUNA EVO only installs an update that GitHub publishes a **SHA-256** for, downloaded over HTTPS from GitHub's own
  hosts. The copy placed next to the exe is hashed **again**, must be a Windows program (`MZ`) and must carry the
  release's version number, before the running exe is renamed aside. Any failure leaves the current version as it was.
- What this protects against: a tampered or corrupted download, a redirect elsewhere, a file swapped after downloading.
  What it doesn't: someone who can publish releases on this repository (a GitHub account takeover). Protect the
  maintainers' GitHub accounts with two-factor authentication; code signing (not done yet) would add a second check.
- Development builds never replace themselves.

## Security notes on the 0.5 features

- **Discord is back, with audit 1's H2 and H3 fixed:** the webhook is encrypted with Windows DPAPI (useless on
  another account or PC), never displayed again after saving, and every post sends `allowed_mentions: { parse: [] }`
  and escapes player-controlled text, so a player named `@everyone` can't ping anyone or add links.
- **Alerts come from LMP's log.** LUNA EVO only reads LMP's own server messages, never chat, and only accepts player
  names LMP itself allows. A modified client could still fake a log line with a line break in a chat message; the
  worst that does is a wrong history entry or a false alert, never a command.
- **Unban edits LMP's ban list** only while the server is stopped, after backing it up.
- **The mod check** reads files only; it never changes your GameData.
- **Craft files from friends** (`.lunacraft`, 0.7) are opened as data: only `craft.craft`, `manifest.json` and
  `thumbnail.png` are accepted (nothing else is ever unpacked), 20 MB at most, and the craft must parse. Imports go
  only into a KSP save's Ships folder or the server's craft library, and never overwrite.

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
| A6 | Low | **A hand-edited LUNA EVO settings file with `null` values** could cause errors later. | Values are normalized on load. |
| A7 | Low | **Plugin hashing read whole DLLs into memory.** | Streamed. |

How they were checked: `SecurityRound2Tests` covers oversized and long-text live info, escaping and damaged Universe
zips (the current Universe survives), DTDs, device names, password warnings and null settings (183 tests in total).

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

Each fix has automated tests: tampered, oversized, wrong-host, redirected and unverified downloads; a
zip changed between download and install; a zip with too many entries; hostile and malformed website, GitHub and
runtimeconfig JSON; the absolute `netsh` path; the webhook scrub; `Ui.Open` refusing programs and `file:` links. The
checksum check was also run against the real LMP 0.29.2 release, whose published digest matched.

## Tested with

The real software LUNA EVO has been run against (not just faked in tests):

| Software | Version | How |
|---|---|---|
| Windows | 11 Home (build 26200), x64 | Everything; the app and its tests are Windows-only |
| Luna Multiplayer server | **0.29.2** (needs the .NET 6 runtime) | Installed by the wizard, started, stopped with Ctrl+C, config edited, live JSON read, Universe backed up while running |
| Luna Multiplayer server | 0.30.0 (master) | Settings definitions compared with 0.29.2 in LMP's source (identical); not run |
| Luna Multiplayer client mod | **0.29.2** | Found in a real KSP install; client zip verified against GitHub's digest |
| Kerbal Space Program | **1.12.5** (Steam, heavily modded) | Found automatically, GameData scanned, CKAN install detected; LUNA EVO never changed its files during testing |
| CKAN | **1.36.4** (`ckan.exe`) | Download verified; command line used for real (`update`, `install` by name) on a throwaway KSP folder |
| .NET | 10 (LUNA EVO), 6 (LMP 0.29.x) | Build and runtime checks |
