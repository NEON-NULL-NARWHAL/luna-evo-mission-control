# 13 · Automation, Connect, Profiles & Backups

[← Meat and Potatoes](README.md) · Code: `Core/Ops/`, `App/ServerPage.cs`, `App/ConnectPage.cs`, `App/ProfilesPage.cs`, `App/SettingsPage.cs`

All of it is switched on and tuned in **LUNA EVO Settings** (`AppSettings`); nothing runs unless LUNA EVO is running
the server.

## Auto-restart after a crash

`ServerRunner.Exited(code, requested)` says whether *we* asked it to stop. If not (a crash, or someone closed it),
`ServerPage` restarts it after `CrashRestartPolicy.Delay` (10 s), unless `CrashRestartPolicy` sees 3 crashes in 10
minutes. Then it gives up and says so (a broken config would otherwise loop forever). Automatic starts skip the
"unsaved changes" and "config has errors" questions.

## Daily restart

`AppSettings.DailyRestartTime` ("HH:mm"). `ServerPage.CheckDailyRestartAsync(now)` runs every second:
one minute before, it sends `/say Scheduled server restart in 1 minute...`; at the time, a clean stop and start; then it
schedules tomorrow. `DailyRestart.Next` does the date maths.

## Universe backups

`UniverseBackups` zips `<server>/Universe` into `<server>/LunaEvo/UniverseBackups/universe_<time>_<label>.zip`.
Files are read with sharing on, so it works while the server runs. LUNA EVO first sends `/backup now` so LMP writes
fresh files. **Restore** needs the server stopped and always snapshots the current Universe first (`beforerestore`),
so a restore can be undone. Scheduled every *N* hours (`UniverseBackupHours`), pruned to `UniverseBackupsToKeep`.

## Config profiles

`ProfileStore` copies all `Config/*.xml` into `<server>/LunaEvo/Profiles/<name>/`. Switching backs up the current
config (`Config/Backups/...`) and copies the profile over, then the app reloads (keeping a running server running;
restart it to use the new config). Profiles are the files **on disk**, so unsaved changes are saved first.

Discord notifications were removed in 0.4.0; see [14](14-Security.md#discord-removed-in-040) for why and what a
return would need.

## Connect page

| Row | Source |
|---|---|
| Addresses to share | `NetworkHelper.LocalAddresses()`: IPv4 on adapters that are up (Ethernet, Wi-Fi, VPN/mesh like Tailscale `100.64/10` or ZeroTier); loopback and link-local removed. |
| Internet address | LMP's own Debug line `Detected NAT addresses: <ip>:<port>`, captured by `ServerPage` into `PublicAddress`. **Hidden until you press Show**, so it doesn't end up in screenshots. |
| Server list / UPnP | The config values, explained. |
| Firewall | `netsh advfirewall firewall show rule name="LUNA EVO - LMP server (UDP <port>)"` (no admin needed). **Allow through firewall** runs `netsh ... add rule ... protocol=UDP localport=<port>` elevated (Windows asks for permission). The exact command is in the button's tooltip. `netsh` is run from System32 by full path ([14](14-Security.md)). |
| Share live info | Shared when `EnableWebsite` is on and `ListenAddress` isn't loopback. **Share with players** (confirms first) sets `EnableWebsite=true`, `ListenAddress=""`; **Stop sharing** sets `127.0.0.1`; a TCP firewall rule for the website port. Feeds players' Join page ([15](15-Joining-Servers.md)). |
| Listening | `IPGlobalProperties.GetActiveUdpListeners()` contains the game port. |

## Review before saving

`ConfigSession.GetChanges()` lists every changed setting with its on-disk and in-memory values;
`MainWindow.DescribeChanges` turns it into "General › Server Name: Luna Server → LUNA EVO" lines, and summarises list
changes ("Allowed Parts: 1 added, 531 removed") instead of dumping hundreds of part names.
