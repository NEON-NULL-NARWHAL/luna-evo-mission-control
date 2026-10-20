# Changelog

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
