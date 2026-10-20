# Changelog

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
  Behaviour / Automation & alerts / Updates / About), Join (Server / My servers / Check my mods, Play always shown).

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
  summary cards; live colour-coded console with filters, search, follow, copy and clear; command box with history
  and quick commands; log file browser; follows and can stop a server started outside the app; .NET 6 runtime check.
- **LUNA EVO Settings** page: accent colour wheel with RGB/hex and presets, dark/light/system theme, behaviour options
  (reopen last server, backups to keep, console lines, stop timeout, confirm stop), about (versions, runtime), folders, links.
- `--settings <file>` for a portable settings file.
- Old config backups are pruned (20 kept by default).
- Prompts when closing with unsaved changes or with the server running.

### Changed
- The navigation rail is grouped into Control / Game / Network / Advanced / Mods / LUNA EVO.
- Changing a value back to what's on disk no longer leaves the file marked as changed.

## 0.1.0 (2026-10-20)

- First version: GUI generated from LMP's own `Server.dll`; in-place XML editing that preserves every byte;
  validation from LMP's descriptions plus cross-setting rules; greyed-out settings; difficulty-preset defaults;
  reset per setting; timestamped backups; mod-control list editor with dedupe; NOMM-inspired look; single-file exe.
