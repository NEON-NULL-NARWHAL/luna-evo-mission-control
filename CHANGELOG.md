# Changelog

## 2.7.1 (2026-10-22)

### Fixed
- Join → Install the LUNA EVO patches: the dialog now lists what 2.7 added (fewer lost vessel updates, less lag).

### Tested, not shipped
- **Smoother vessels of other players** (they stop now and then on a jittery connection): two versions measured with two
  real games, both ways. Neither was reliably better (one sometimes moved vessels backwards, the other stopped them more
  often), so neither is included.
- A separate network channel per vessel for staging and docking: could make vessels appear twice, so not included.

## 2.7.0 (2026-10-22)

### Added
- **Faster, steadier multiplayer**, in the LUNA EVO patches for LMP (Join → Update) and the LUNA EVO server plugin
  (Map → Update), each measured in the test rig before and after:
  - **Less lag:** LMP's network loops wait in steps Windows rounds up to 15.6 ms; asking Windows for 1 ms timers cut
    the time from one game to another from 29 to 22 ms (worst case 82 to 46 ms).
  - **Far fewer lost updates on a jittery connection:** LMP sent every vessel's fast updates on one network channel,
    where an update arriving after another vessel's is thrown away. One channel per vessel: with 20-120 ms of jitter,
    lost updates went from 63% to 18% (the rest are only ones a newer update of the same vessel had already replaced).
    Works with unpatched players and servers.
- The server plugin now brings the Harmony library and lives in `Plugins\LunaEvo`; Update moves an older install.

### Tested, not shipped
- Moving far-away vessels less often: no measurable difference in frames per second with 135 vessels, so it isn't
  included.

## 2.6.1 (2026-10-22)

Eleventh audit; details in `SECURITY.md`.

### Fixed
- The LUNA EVO server plugin builds its live file off LMP's main thread, so a big universe can't make the server
  pause every half second.

### Changed
- `tests/Fixtures/THIRD-PARTY.md` lists every LMP file kept in the repo with its source and SHA-256 (all checked
  identical to LMP's official release).
- GitHub's test workflow uses current versions of its actions.

### Research
- What in LMP's client and server hurts player-to-player performance, with patch ideas (maintainer chapter 25): shared
  network channels that drop or hold up updates, polling loops that add 40-60 ms on Windows, every far-away vessel
  updated every physics tick, and every position sent to every player.

## 2.6.0 (2026-10-22)

### Added
- **Two more fixes in the LUNA EVO patches for LMP** (Join → Install, or Update if you have 2.5's):
  - **No time jumps when you're alone in your subspace.** When your game falls behind, LMP jumps its clock by 3.5 s or
    more: the clock moves, your vessel doesn't, and engines cut out (upstream issue #469). Alone, you now get a
    subspace at your own time instead. In the test rig, on a game slowed to a third of real time: 11 jumps in a minute
    unpatched, 0 patched.
  - **Positions carry the right time.** Each position LMP sent was stamped one physics tick early; now exact (measured:
    20 ms off before, 0 after).

### Changed
- **The patches build anywhere**, GitHub's test machine included, with no KSP installed: LMP's own client files (MIT)
  are in the repo, and the few KSP and Unity names the patches use come from LUNA EVO's own stand-ins (no Squad code).
  Releases no longer depend on one PC.

### Fixed
- The test rig's "positions 0.09 s ahead of their time" was mostly a measurement mix-up: LMP reports the vessel's
  top part, the rig measured its centre of mass. The one physics tick that was real is fixed above.

## 2.5.0 (2026-10-22)

### Added
- **LUNA EVO patches for the LMP client** (Join → **Install the LUNA EVO patches**, KSP closed; LMP 0.29). The first
  fix for LMP itself, as a small KSP mod on top of the official client:
  - LMP's own `-lmp lmp://host:port` option breaks LMP's whole startup when the address has a port (found and checked in
    the test rig). Patched, it works, IPv6 included.
  - It can use a password: taken from LMP's own server list, never put on the command line.
  - So **Launch KSP and join** connects by itself to any server, with any port or password, instead of asking you to pick
    it from LMP's list. Nothing sent to the server or other players changes; on other LMP versions it turns itself off.
- **Test rig: what a second player sees.** A pretend second player records what LMP sends it; in the rig, it saw the
  rocket where it was 0.17 s earlier (23 m behind at 129 m/s), with messages arriving in 54 ms.

## 2.4.0 (2026-10-22)

### Added
- **The LUNA EVO server plugin** (Map → **Install the LUNA EVO server plugin**, server stopped; LMP 0.29). A small
  plugin that LMP's own server loads from its `Plugins` folder. The map then shows every vessel where its player's game
  last said it was (every second) and **who flies what**. It only watches: it never changes a message, opens no network
  port, ignores anyone who hasn't logged in, and players need nothing. **Remove** takes it out.
- A pretend LMP player for testing (`tests/FakeLmpClient`): joins a real LMP server without KSP and flies a vessel.
- Checked for real: the real LMP 0.29.2 server with the plugin, the pretend player flying, and the map reading it
  0.1 s behind.

## 2.3.0 (2026-10-22)

### Added
- **The map is live while the server runs.** Positions come from the server itself (its website, which the Players
  page already reads), a few seconds behind instead of minutes. New launches show up and removed vessels go, before
  LMP saves them. The line under the time says where positions come from, and **Turn on the website for a live map**
  switches it on for this PC only. Checked against the real LMP 0.29.2 server.
- [`DECISIONS.md`](DECISIONS.md): what the owner decided and what was done about it, plus open questions.
- Maintainer chapter 25: how LMP really works, what's weak in it, and the plan to ship fixes as patches on top of
  the official LMP.

## 2.2.0 (2026-10-21)

### Added
- **Solar system map** (rail → Map): every vessel on the server on its orbit, where it is now, top-down. Pick any
  star, planet or moon; scroll to zoom, drag to move; click a vessel for its apoapsis, periapsis, inclination, period
  and crew, or a moon to go there. Landed craft sit on the surface; asteroids and debris can be hidden.
- **Planet packs:** the bodies come from your KSP's GameData (Kopernicus configs), so Outer Planets Mod, MPE and other
  packs show up, moved planets included (OPM's Eeloo round Sarnus). Checked on a 47-body install.
- Universe time as the server's clients see it ("Year 1, day 86, 05:15"), server running or not.

### Tests
- Orbit math against KSP's own numbers, planet packs (moves, removals, broken packs), the universe clock, odd orbits
  from save files, and the Map page clicked through. 315 tests.

## 2.1.1 (2026-10-21)

Tenth audit, the deep dive; details in `SECURITY.md`.

### Fixed
- A whole number too big for its setting (e.g. 3000000000 players) is an error now. LMP can't read such a file and
  runs that whole file on its defaults, which for General means no password.
- Pasting a mod list with a line of only commas, or with broken Unicode, no longer stops Check and Install silently.
- Huge numbers in an invite or a setting's description can't crash the app.
- `players.json` is no longer rewritten every 5 seconds for each player online (at most once a minute when only
  "last seen" changed).
- The LMP server installer checks every path before writing anything and counts the bytes it actually unpacks.
- The firewall-rule check can't throw if Windows' netsh hangs.

### Tests
- A fuzz test feeds every parser that reads outside text thousands of mutated inputs (it found three of the bugs
  above); a check that no code comment is separated from its code; tests for the error log and for finding a running
  server. 303 tests.

## 2.1.0 (2026-10-21)

### Added
- **Install the server's mods** (Join): open the `.ckan` mod list your host sent; the CKAN page loads it for your KSP
  and checks it with CKAN straight away. Hosts make that file with **Connect → Export my mod list...**.
- **Wrong LMP version?** Join knows the server's LMP version (from the server browser, or a pasted invite) and, when
  yours differs or is missing, offers **Install LMP x.y.z** right there.
- **Server browser:** sort (most players, lowest ping, name), ping times for the servers at the top, **Save** to My
  servers, and a password prompt when you join a server that has one.
- **CKAN page:** **Cancel** while CKAN runs, **Copy log** for CKAN's output, and each install shows the other KSP
  versions CKAN accepts mods for there (its "Compatible game versions").

### Fixed
- **CKAN mods that need "one of several" mods** (Parallax needs a KSPBurst, Scatterer a sun flare...) were installed
  without it: CKAN's command line silently skips that question. LUNA EVO now asks you, with CKAN's own options, and
  includes your pick. Found and checked with the real ckan.exe 1.36.4.
- Dialogs with many answers list them one per line instead of cutting them off.
- Tests no longer ping real addresses.

## 2.0.1 (2026-10-21)

### Changed
- **The GitHub repository is now [`luna-evo-mission-control`](https://github.com/NEON-NULL-NARWHAL/luna-evo-mission-control)**
  (it was `luna-evo-multiplayer-config-manager`). Old links, clones and every older copy's update check are
  redirected by GitHub; checked against the live API with the updater built with the old name.
- LUNA EVO checks for updates at the new name. README links, badges and the file tree, and the maintainer docs,
  use it.
- `SECURITY.md` names the current Windows startup entry and log folder.

## 2.0.0 (2026-10-21)

**LUNA EVO Config Manager is now LUNA EVO Mission Control.** It hosts, finds, mods and joins servers now, not just
configs. Ninth audit (a deep dive); details in `SECURITY.md`.

### Changed
- New name everywhere: window, rail, docs, `LunaEvo.MissionControl.exe`, projects and namespaces
  (`LunaEvo.MissionControl`), settings in `%APPDATA%\LunaEvo\MissionControl\` and the Windows startup entry.
- **Nothing to do when updating:** copies of the Config Manager (0.6 to 1.4) update themselves as before (every
  release also carries the exe under its old name), your settings are copied over on first start, and the old startup
  entry is replaced. A saved Discord webhook keeps working.

### Fixed
- LMP config files are saved through a temp file, so a crash or full disk can't leave a half-written one.
- Following a server started outside LUNA EVO can no longer close the app on an unexpected error, nor skip lines.
- A failed Universe backup after stopping no longer stops a daily restart from starting the server again.
- A Server.exe Windows refuses to run no longer leaves Start stuck; an instant startup failure can't crash the app.
- A daily restart time missed while the server was stopped no longer restarts it on the next start.
- Clear messages instead of silence: profile switch/save failures, restoring a damaged backup, Windows refusing to
  start KSP or CKAN, killing an outside server that had just stopped, commands after LMP closed its input.

## 1.4.0 (2026-10-20)

From the owner's testing list. Eighth audit; details in `SECURITY.md`.

### Added
- **Server browser** (Play): every public LMP server from LMP's master servers, with search and filters (your LMP
  version, no password, has players). **Join** checks the server on the Join page, ready to launch.
- **Simple mode** (App settings → Behavior): only Server console, Connect, the Essentials settings, Join and CKAN.
- **Essentials tab** first on Server settings: the dozen settings most servers need.
- **Editable console cards:** click Server name, Game port or Max players to change it there (validated, saved with a
  backup); Website, Mode and Server list open their setting.
- **Clickable cards** on Players (open the list) and Vessels (filter the list to that kind).
- **Versions at the bottom left:** LUNA EVO, the LMP server and client, KSP and CKAN in use.
- **The problem count** under the server status opens a list of every problem; each jumps to its setting. The
  internet-exposure banner jumps to UPnP.
- **Check my mods → Allow them on my server:** adds the parts and resources the check found to your open server's
  allowed lists, after explaining that a private server can turn Mod Control off instead (and the risk).
- **Check with CKAN:** a dry run of a mod list; CKAN's errors are now explained in plain words.
- An error log (`%APPDATA%\LunaEvo\ConfigManager\logs\errors.log`).

### Changed
- Naming: "App settings" (LUNA EVO's own) vs "Server settings" (LMP's); sentence case in the rail.
- README: new description at the top, server browser section, CKAN steps include the check.

### Fixed
- Ports outside 1-65535 are errors.
- Two flaky tests (shared temp folder; slow CI runners).

## 1.3.2 (2026-10-20)

### Changed
- Spelling: American English throughout the app and docs (color, behavior, catalog, canceled, minimized...).
- README: a step-by-step **Using the CKAN page** section; the security note now counts all seven audits.

## 1.3.1 (2026-10-20)

Seventh audit, with real-world CKAN checks; details in `SECURITY.md`.

### Fixed
- Mod lists with "one of these mods" choices (`any_of`) now say they were skipped instead of dropping them silently.

### Checked
- Your own `ckan.exe` is recognized as official CKAN 1.36.4; exporting a real 108-mod install matched the original
  modpack it was installed from.

## 1.3.0 (2026-10-20)

### Added
- **Use the CKAN you already have:** CKAN page → **Choose ckan.exe...**. LUNA EVO uses it for mod lists and says
  whether it's an official CKAN build (its SHA-256 compared with GitHub's). **Use LUNA EVO's copy** switches back.
- **Export mod list...** for every CKAN install: the mods you picked there, as a `.ckan` file to send to friends (they
  install it on this page, or in CKAN with File → Install from .ckan). Exact versions optional. Written fresh, so your
  Windows user name (in CKAN's own file) isn't in it.
- **Use as mod list:** copy one install's mods into the list box, to install them into another.

### Changed
- `SECURITY.md` lists audits newest first again, with the 1.3.0 notes on top.
- Paths under your user folder show as `%USERPROFILE%\...` (for example a ckan.exe in Downloads).

## 1.2.1 (2026-10-20)

Sixth security audit, a sweep of the whole codebase; details in `SECURITY.md`.

### Security
- Names made only of dots can no longer slip through the CKAN version and download file name filters.
- Loading a `.ckan` file over 5 MB is refused instead of freezing the window.
- CKAN's command line can't wait forever for an answer (its input is closed).
- A friend's craft named after a Windows device (`CON`, `NUL`...) gets a safe file name.
- GitHub release lists are read with a 10 MB cap.

### Changed
- `SECURITY.md` is in chronological order (oldest audit first), with a release-by-release history of audits, real
  checks and test counts, and a "Tested with" table. The README lists the tested versions too (Windows 11, LMP 0.29.2,
  KSP 1.12.5, CKAN 1.36.4) and its security note is up to date.

## 1.2.0 (2026-10-20)

### Added
- **CKAN page** (Play → CKAN mods): whether LUNA EVO has CKAN and which version it uses, whether CKAN is open right now,
  and your CKAN installs with their LMP version and number of CKAN mods. Download, open and remove CKAN copies here
  (moved from Settings → Downloads, which now links to it).
- **Install a mod list:** paste a `.ckan` modpack (CKAN → File → Export modpack) or mod names one per line, or load a
  `.ckan` file, pick the KSP install, and CKAN downloads and installs them, with its messages shown as it goes. Only the
  mod names and versions are taken from the list; CKAN installs them from its own catalog (a file's own download
  link is never used). Refuses while KSP or CKAN has that install open.

### Changed
- LMP's misspelled `HearbeatMsInterval` is shown as "Heartbeat Ms Interval" (the file keeps LMP's name).
- Faster: the Players history is only redrawn when something changed, chat announcements are parsed once instead of
  every second, the Join page's auto-refresh no longer re-runs DNS and pings every 10 seconds, and GameData scans are
  reused for two minutes (mod check, craft import).
- `settings.json` is saved through a temp file, so a crash mid-save can't lose your settings.
- CKAN's install paths are shown like any other Windows path (`M:\Steam\...`, not `M:/Steam/...`).

### Security
- Fifth audit; details in `SECURITY.md`.

## 1.1.1 (2026-10-20)

### Changed
- Paths under your user folder show as `%LOCALAPPDATA%\...` on the Downloads tab and in the wizard log (no user name
  in screenshots or shared help).
- README: version line, "Get set up to play (CKAN and the LMP mod)", screenshots of Downloads, Join with CKAN and the
  wizard's new step. `release.ps1` refuses to ship if the README's version line is out of date.

## 1.1.0 (2026-10-20)

### Added
- **Settings → Downloads:** LUNA EVO keeps its own verified copies of **CKAN** (the official `ckan.exe`, SHA-256
  checked, one folder per version: download the latest, open, remove) and installs the **LMP client mod** into any KSP
  install, including every install CKAN manages, in any version (the open server's by default). Your LMP settings
  (server list) are kept, the old version is backed up outside GameData, and an existing `000_Harmony` is never replaced.
- **Setup wizard, step 3 (optional):** download CKAN, and install the LMP client matching the new server into your KSP.
- **Join page + CKAN:** your CKAN installs as one-click picks, and **Launch KSP and join** uses the same options as
  CKAN's Play button (e.g. `-single-instance`) before connecting.

### Fixed
- Progress messages arriving after a download finished could overwrite the result (CKAN, LMP client, LUNA EVO update).
- Two LMP client installs within the same second could collide on the backup folder name.

## 1.0.1 (2026-10-20)

Fourth security audit (the 1.0 review); details in `SECURITY.md`.

### Security
- Vessel cleanup removes nothing unless its Universe backup copied every file (a locked file used to be skipped while
  the removal went ahead).
- The Windows-notifications icon is shown and removed under a lock (its removal timer runs on another thread).

## 1.0.0 (2026-10-20)

**LUNA EVO 1.0.** Everything a home LMP host needs, from first download to an always-on server: setup wizard, every
setting in a searchable GUI read from your own LMP, the server console with health and automation, players with
history, alerts and bans, Connect and Join (with invites, mod checks and launching KSP straight in), crafts sharing,
notifications, self-updating, and now vessel cleanup. Three security audits, 234 automated tests.

### Added
- **Vessels** page (rail → Server): every vessel in the Universe with type, body, situation, parts and crew;
  "Select all debris" / "Select asteroids & comets" (never crewed vessels), search, and **Remove selected** while the
  server is stopped, with a whole-Universe backup first (`beforecleanup`, restorable on Profiles & Backups). Fewer
  vessels means less for every player's game to keep in step.

## 0.9.0 (2026-10-20)

### Added
- **Server health** on the Server Console: memory, CPU and threads of `Server.exe`, every 2 seconds while it runs
  (also for a server started outside LUNA EVO).
- **Copy invite** on the Connect page: a ready-to-send message (server name, the best address to use with mesh/VPN
  first, the LMP version needed, whether there's a password, never the password). Pasting the whole message into the
  Join page's address box works.
- README screenshots for Crafts, settings search, server health, home server mode and chat announcements.

### Changed
- Automatic safety backups (on stop, before updates) use fast compression, so stopping a big server isn't slowed down.
- The Windows-notifications icon only stays in the notification area while a notification shows, so home server mode
  has a single LUNA EVO icon.

## 0.8.1 (2026-10-20)

Third security audit; details in `SECURITY.md`.

### Security
- `.lunacraft` files are read with a hard size limit per entry, so a "zip bomb" craft can't fill memory.
- Mod rules files from a host are size-limited before parsing.

### Fixed
- The clean-up after a self-update could act on the wrong exe path if more than one window had been open.

## 0.8.0 (2026-10-20)

### Added
- **Settings search** on the Server settings page: type part of a name or description (heartbeat, password, warp)
  and pick a result to open its tab and scroll to it.
- **Chat announcements** (Settings → Automation & alerts): one per line, "minutes then message", sent as `/say`
  while LUNA EVO runs the server (at least every 5 minutes, counted from when the server starts).
- **Home server mode** (Settings → Behavior): one switch for an always-on host PC. LUNA EVO starts with Windows
  minimized to the notification area, starts the server by itself, hides to the notification area instead of quitting
  (right-click the icon → Quit), and keeps the PC from sleeping while the server runs. The Windows startup entry is
  per-user (no admin) and is removed when you switch it off.

## 0.7.1 (2026-10-20)

### Added
- **Automatic Universe backups** every time LUNA EVO stops the server (right after LMP's own final save; the last 5
  are kept) and before every LMP update. They're labeled `onstop` / `beforeupdate` on the Profiles & Backups page,
  are pruned separately, and never push out your manual backups.

## 0.7.0 (2026-10-20)

### Added
- **Crafts** page (rail → Play): your KSP craft designs (every save, VAB and SPH) and, with a server open, the
  server's LMP craft library. **Export** any craft to a `.lunacraft` file (KSP's own `.craft`, its thumbnail and its
  parts list) to send to friends; **Import** one into a KSP save or the server's library, with a check of which of its
  parts your GameData doesn't have. Never overwrites an existing craft.

## 0.6.1 (2026-10-20)

### Added
- **Generate a strong password** under the server and admin password settings (20 random letters and digits). LMP
  doesn't limit password guesses, so this pairs with the short-password warning.
- **"This server is reachable from the internet"** banner on the Server Console when UPnP is on (LMP opens the game
  port, and the website port too, on your router) or the server is on the public list.
- GitHub Actions runs every test on each push and pull request.

## 0.6.0 (2026-10-20)

### Added
- **LUNA EVO updates itself.** On startup it checks its GitHub releases; Settings → Updates → **Update and restart**
  downloads the new exe (GitHub only, SHA-256 verified, checked again before installing, version checked), swaps it
  for the running one, and restarts. Unsaved changes and a running server are asked about first. Development builds
  point to the release page instead.
- `release.ps1`: tests, builds and publishes a GitHub release (with the CHANGELOG section and SHA-256 as notes) in one
  command.

### Changed
- **Tidier layout.** The rail has just the main pages: Server Console, Players, Connect, **Server settings**, Mod
  Control, Profiles & Backups, Join a server, Settings. LMP's config files are tabs on the Server settings page,
  grouped Game / Network / Advanced, with error badges on the tabs and on the rail entry.
- Tabs on the busier pages: Players (Online now / History / Bans, alerts above), LUNA EVO Settings (Appearance /
  Behavior / Automation & alerts / Updates / About), Join (Server / My servers / Check my mods, Play always shown).

## 0.5.0 (2026-10-20)

### Added
- **Player history** on the Players page: everyone who's played, last seen, sessions, time played, LMP/KSP version,
  wrong admin passwords, ban reason, and your own notes. Stored in `<server>/LunaEvo/players.json`; never IPs.
- **Alerts**: 3 wrong admin passwords from one player in 10 minutes, or a banned player trying to join, shown at the
  top of the Players page (with Kick/Ban) and as a notification.
- **Bans**: LMP's ban list with names, and **Unban** (while the server is stopped; the old list is backed up).
- **Check my mods** on the Join page: checks a player's GameData against a server's mod rules the way LMP's client
  does, and lists parts the server would drop. Uses the rules LMP saved, or a file from the host.
- **Share these rules with players** on the Mod Control page.
- **Notifications**: Windows notifications and Discord, for server up/stopped/crashed/gave up/scheduled restart,
  alerts, and optionally joins and leaves. The Discord webhook is stored encrypted (DPAPI), never shown again, and
  posts set `allowed_mentions` to nothing and escape player names.

### Fixed
- The Mod Control page said optional plugins were checked by hash; LMP only checks hashes on mandatory plugins.
- UI tests now run on a copy of the fixture server, so nothing is ever written into `tests/Fixtures`.
- Universe restore swaps folders by renaming (and puts the old Universe back if the swap fails), instead of
  delete-then-move, which could fail on Windows while the deleted folder was still going away.

## 0.4.1 (2026-10-20)

Second security audit; details in `SECURITY.md`.

### Security
- Live info (Join and Players pages) is read with a 4 MB cap, a 15-second limit and no redirects to another host;
  long names and descriptions are cut to 256 characters.
- Restoring a Universe backup unpacks into a staging folder first (with size, file-count and stay-inside checks), so
  a damaged or hostile zip can't leave a half-restored Universe.
- Config files with a DTD are refused.
- Profile names can't become Windows device names (`CON`, `NUL`...).
- Warnings for short server (< 6) and admin (< 10) passwords: LMP doesn't limit guesses, and the admin password can
  ban players, clear the KSC and restart the server.
- A hand-edited LUNA EVO settings file with `null`s no longer causes errors.
- Plugin hashing streams files instead of loading them whole.

## 0.4.0 (2026-10-20)

### Added
- **Join a server** page for players (no server needed): address and network type, ping, live server info (name,
  players, mode, difficulty, password, mod control, warp), players online and vessels in game.
- **Launch KSP and join**: starts KSP with LMP's `-lmp` auto-connect on the default port without a password;
  otherwise adds the server (and password) to the top of LMP's own server list. **Add to LMP's server list** on its own.
- **My servers**: saved servers (never with passwords). The KSP folder is found and remembered.
- Connect page: **Share with players** / **Stop sharing** for LMP's website, and a TCP firewall rule for it.
- `SECURITY.md`: what LUNA EVO touches, the full audit, and how to report a problem.

### Security
- LMP downloads are verified against GitHub's published SHA-256 (and again before unpacking), only from GitHub over
  HTTPS, with size limits. Releases without a checksum need confirmation.
- Limits on zip entries and unpacked size.
- Tolerant parsing of LMP's website JSON, GitHub's release feed and `Server.runtimeconfig.json`: bad data can't crash
  the app.
- `netsh` and `winget` are run from fixed system paths, not looked up on `PATH`.
- Folder scans skip symlinks/junctions. `Ui.Open` only opens web links and folders.

### Removed
- **Discord notifications**, for now. 0.4.0 removes the old plaintext webhook URL from `settings.json` on first start.

## 0.3.0 (2026-10-20)

### Added
- **Setup wizard**: downloads the LMP server from GitHub, writes its config from a few questions (private by
  default), checks for and installs the .NET runtime with winget, copies LUNA EVO into the folder and opens it.
- **LMP updates**: checks GitHub when a server opens; one-click update that keeps Config, Universe, logs and Plugins
  and backs up the old version.
- **Players** page: live players, vessels and subspaces from LMP's website JSON, with Kick/Ban.
- **Connect** page: addresses to share (LAN, VPN/mesh, internet), server list and UPnP status, a Windows Firewall
  rule, and a port-listening check.
- **Profiles & Backups** page: config profiles; Universe backups with restore.
- **Import from GameData**: fill mod control (parts, resources, plugins with SHA-256, expansions) from a KSP install.
- Editors for the plugin and mandatory-part lists (previously read-only).
- **Automation**: restart after a crash (gives up after 3 in 10 minutes), daily restart with an in-game warning,
  scheduled Universe backups.
- **Discord** webhook notifications (up, stopped, crashed, joined, left) with a test button.
- **Review changes before saving** (old → new values).
- Welcome page with "Set up a new server".

### Fixed
- A setting missing from an older config file was treated as modified, which could keep a file marked as unsaved.

## 0.2.0 (2026-10-20)

### Added
- **Server Console** page: start, stop (clean, via Ctrl+C, so LMP makes its backup) and restart the LMP server;
  summary cards; live color-coded console with filters, search, follow, copy and clear; command box with history
  and quick commands; log file browser; follows and can stop a server started outside the app; .NET 6 runtime check.
- **LUNA EVO Settings** page: accent color wheel with RGB/hex and presets, dark/light/system theme, behavior options
  (reopen last server, backups to keep, console lines, stop timeout, confirm stop), about (versions, runtime), folders, links.
- `--settings <file>` for a portable settings file.
- Old config backups are pruned (20 kept by default).
- Prompts when closing with unsaved changes or with the server running.

### Changed
- The navigation rail is grouped into Control / Game / Network / Advanced / Mods / LUNA EVO.
- Changing a value back to what's on disk no longer leaves the file marked as changed.

## 0.1.0 (2026-10-20)

- First version: GUI generated from LMP's own `Server.dll`; in-place XML editing that preserves every byte;
  validation from LMP's descriptions plus cross-setting rules; grayed-out settings; difficulty-preset defaults;
  reset per setting; timestamped backups; mod-control list editor with dedupe; NOMM-inspired look; single-file exe.
