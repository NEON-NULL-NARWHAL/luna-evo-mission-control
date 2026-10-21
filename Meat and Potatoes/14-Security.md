# 14 · Security

[← Meat and Potatoes](README.md) · Code: `Core/Setup/ServerInstaller.cs`, `Core/Live/ServerInfoClient.cs`, `Core/Ops/NetworkHelper.cs`, `App/Ui.cs` · Report: [`SECURITY.md`](../SECURITY.md)

`SECURITY.md` is the user-facing audit report (every finding and its fix). This chapter is the maintainer's side:
the rules the code follows now, so new code doesn't undo them.

## The rules

| Rule | Why | Where |
|---|---|---|
| **Every LMP download is hashed against GitHub's `digest`.** | A tampered zip becomes the server that runs on your PC. | `ServerInstaller.DownloadAsync` |
| **Hash again just before unpacking.** Pass `asset.Sha256` to `Install`. | The zip sits in a temp folder between download and install. | `ServerInstaller.Install(zip, dir, expectedSha256)` |
| **Downloads only over HTTPS from GitHub hosts**, checked again after redirects. | A poisoned release feed or redirect could point anywhere. | `ServerInstaller.IsTrustedDownload` |
| **The app never updates itself unverified.** `AppUpdater` offers only releases whose exe has a digest, rehashes the staged copy, checks `MZ` and the version, and renames rather than overwrites (rolls back on failure). | A self-update is a download that replaces the program itself. | `AppUpdater` ([18](18-Updating-LUNA-EVO.md)) |
| **Unverified means asked.** No `digest` → the UI confirms "Can't verify this download" and passes `allowUnverified: true`. | Older releases have no digest; refusing them outright would strand old servers. | `SetupWizard`, `MainWindow.UpdateServerAsync` |
| **Limits on everything unpacked**: `MaxDownloadBytes`, `MaxEntries`, `MaxUnpackedBytes`, plus the zip-slip check. | Zip bombs; entries like `..\..\evil.dll`. | `ServerInstaller` |
| **Never call `TryGetInt32`/`TryGetDouble`/`TryGetInt64` without checking `ValueKind` first.** Use the `Int`/`Double`/`Bool`/`Str`/`Array` helpers. | They **throw** on a string or object, so a bad response crashes whoever called them. | `ServerInfoClient`, `LmpReleases`, `DotNetRuntime` |
| **Cap lists that come from the network.** | A hostile server could send millions of "players". | `ServerInfoClient` |
| **Timer callbacks catch everything.** | An exception in a timer callback ends the app. | `PlayersPage.PollAsync`, `JoinPage` |
| **System tools by absolute path**: `NetworkHelper.NetshPath`, `RuntimeInstaller.WingetPath`, `/bin/kill`. | `netsh` runs elevated; a PATH lookup would give a planted exe the admin prompt. | `NetworkHelper`, `RuntimeInstaller`, `ConsoleControl` |
| **File walks skip reparse points** (`AttributesToSkip = ReparsePoint`, `IgnoreInaccessible`). | Junction loops hang scans. | `GameDataScanner`, `UniverseBackups` |
| **`Ui.Open` only opens `http(s)` URLs and existing, fully-qualified folders** (`Ui.CanOpen`). | `Process.Start` with `UseShellExecute` runs anything. | `Ui.cs` |
| **No secrets in LUNA EVO's settings file.** Passwords typed on the Join page go to LMP's list only. | `settings.json` is plaintext in `%APPDATA%`. | `JoinPage`, `AppSettings` |
| **Network reads are capped**: headers first, `MaxResponseBytes` (4 MB), 15-second limit, no redirect to another host, text cut to `MaxTextLength`. | Whatever answers on a "friend's server" picks the size and speed of the reply. | `ServerInfoClient.GetAsync` |
| **Unpack into a staging folder, then swap.** Check entry count, total size and that every entry stays inside. | A damaged or hostile zip must never leave half a Universe behind. | `UniverseBackups.Restore` |
| **No DTDs in XML.** `DtdProcessing.Prohibit`, no resolver. | Entity expansion in a planted file. | `ConfigFile` |
| **Names that become folders**: no invalid characters, no Windows device names (`CON`, `NUL`, `COM1`...), at most 80 characters. | `CON` isn't a folder on Windows. | `ProfileStore.SafeName` |
| **Settings from disk are normalized** (`null` → defaults). | Hand-edited files. | `AppSettingsStore.Load` |
| **Tests never touch the network or KSP.** `Http.Override` = `FakeHttp`; `JoinPage.StartKsp` throws unless a test replaces it. | A test must never start a real game or hit a real server. | `UiHarness` |

## Things that look like holes but are design

- **Loading `Server.dll` runs LMP code** (settings constructors), in a collectible `AssemblyLoadContext` loaded from
  bytes. That's the price of generating the GUI from LMP itself. Same trust as running `Server.exe`.
- **LMP's own passwords are plaintext XML.** We edit them in place; we don't add copies anywhere else.
- **LMP's website has no auth.** It's local-only unless the host presses **Share with players** (Connect page), which
  confirms first.

## Discord (removed in 0.4.0, back in 0.5)

Removed in 0.4.0. `AppSettingsStore.Load` still re-saves `settings.json` if its raw JSON contains
`"DiscordWebhookUrl"`, so a 0.3 plaintext webhook is scrubbed on first start (`LiveModsOpsTests` covers it).
It came back in 0.5 the safe way: the URL encrypted with DPAPI (`SecretProtector`, `AppSettings.DiscordWebhookProtected`),
`allowed_mentions: { "parse": [] }` on every post, and player-controlled text escaped. Details in [17](17-Notifications.md).

## Text from players

Player names and chat come from other people's game clients. Read only LMP's own messages (never `[Chat]` lines),
match names with LMP's rule (`A-Z a-z 0-9 - _`), escape them before sending anywhere (Discord), and never store
their IP addresses (`PlayerTracker`).

## The repository's own protection

Switched on in GitHub (audit 10, J8): secret scanning with **push protection** (a push containing a token or key is
refused), **Dependabot** alerts and security updates (a pull request when a package has a known vulnerability), and
**private vulnerability reporting** (Security → Report a vulnerability). Keep them on; check the Security tab when
GitHub emails about an alert.

## Checklist for new code

- Anything downloaded: trusted host, size limit, hash.
- Anything parsed from the network: no throwing reads, capped sizes, errors as values.
- Anything started as a process: absolute path, arguments as a list (`ArgumentList`), never a shell string.
- Anything stored: no secrets in `AppSettings`.
- Anything unpacked: limits, stay-inside check, staging folder.
- Anything parsed from outside text: add it to `FuzzTests`, and parse numbers with `TryParse` (audit 10 found three
  throwing parses that way).
- Any whole-number setting: it must fit its .NET type, or LMP runs the whole file on defaults (audit 10, J1).
- Anything turned into a file or folder name: `SafeName`-style cleaning. Also refuse names made only of dots (`..`) and Windows device
  names (`ProfileStore.IsDeviceName`); audit 6 found both gaps.
- Any file a user picks or a friend sends: a size limit before reading it (`CkanPage.MaxListBytes`).
- Any tool started in the background: close or redirect its input, so it can never wait for an answer.
- A test for each of the above.
