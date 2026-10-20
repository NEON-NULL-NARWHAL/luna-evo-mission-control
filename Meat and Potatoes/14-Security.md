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
| **Unverified means asked.** No `digest` → the UI confirms "Can't verify this download" and passes `allowUnverified: true`. | Older releases have no digest; refusing them outright would strand old servers. | `SetupWizard`, `MainWindow.UpdateServerAsync` |
| **Limits on everything unpacked**: `MaxDownloadBytes`, `MaxEntries`, `MaxUnpackedBytes`, plus the zip-slip check. | Zip bombs; entries like `..\..\evil.dll`. | `ServerInstaller` |
| **Never call `TryGetInt32`/`TryGetDouble`/`TryGetInt64` without checking `ValueKind` first.** Use the `Int`/`Double`/`Bool`/`Str`/`Array` helpers. | They **throw** on a string or object, so a bad response crashes whoever called them. | `ServerInfoClient`, `LmpReleases`, `DotNetRuntime` |
| **Cap lists that come from the network.** | A hostile server could send millions of "players". | `ServerInfoClient` |
| **Timer callbacks catch everything.** | An exception in a timer callback ends the app. | `PlayersPage.PollAsync`, `JoinPage` |
| **System tools by absolute path**: `NetworkHelper.NetshPath`, `RuntimeInstaller.WingetPath`, `/bin/kill`. | `netsh` runs elevated; a PATH lookup would give a planted exe the admin prompt. | `NetworkHelper`, `RuntimeInstaller`, `ConsoleControl` |
| **File walks skip reparse points** (`AttributesToSkip = ReparsePoint`, `IgnoreInaccessible`). | Junction loops hang scans. | `GameDataScanner`, `UniverseBackups` |
| **`Ui.Open` only opens `http(s)` URLs and existing, fully-qualified folders** (`Ui.CanOpen`). | `Process.Start` with `UseShellExecute` runs anything. | `Ui.cs` |
| **No secrets in LUNA EVO's settings file.** Passwords typed on the Join page go to LMP's list only. | `settings.json` is plaintext in `%APPDATA%`. | `JoinPage`, `AppSettings` |
| **Tests never touch the network or KSP.** `Http.Override` = `FakeHttp`; `JoinPage.StartKsp` throws unless a test replaces it. | A test must never start a real game or hit a real server. | `UiHarness` |

## Things that look like holes but are design

- **Loading `Server.dll` runs LMP code** (settings constructors), in a collectible `AssemblyLoadContext` loaded from
  bytes. That's the price of generating the GUI from LMP itself. Same trust as running `Server.exe`.
- **LMP's own passwords are plaintext XML.** We edit them in place; we don't add copies anywhere else.
- **LMP's website has no auth.** It's local-only unless the host presses **Share with players** (Connect page), which
  confirms first.

## Discord (removed in 0.4.0)

Removed rather than fixed, for now. `AppSettingsStore.Load` re-saves `settings.json` if its raw JSON still contains
`"DiscordWebhookUrl"`, so the old plaintext webhook is scrubbed on first start (`LiveModsOpsTests` covers it). If it
comes back: encrypt the URL (DPAPI on Windows), send `allowed_mentions: { "parse": [] }` on every post, and escape
player names (`@everyone`, Markdown).

## Checklist for new code

- Anything downloaded: trusted host, size limit, hash.
- Anything parsed from the network: no throwing reads, capped sizes, errors as values.
- Anything started as a process: absolute path, arguments as a list (`ArgumentList`), never a shell string.
- Anything stored: no secrets in `AppSettings`.
- A test for each of the above.
