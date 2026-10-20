# Changelog

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
