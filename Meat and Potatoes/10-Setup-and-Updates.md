# 10 · Setup wizard and LMP updates

[← Meat and Potatoes](README.md) · Code: `Core/Setup/`, `Core/Http.cs`, `App/SetupWizard.cs`, `MainWindow.UpdateServerAsync` 🔌

## How LMP publishes releases

From `GET https://api.github.com/repos/LunaMultiplayer/LunaMultiplayer/releases` (needs a User-Agent header):

| Era | Server download | Runtime |
|---|---|---|
| 0.28 – 0.29.x | `LunaMultiplayer-Server-Release.zip` (~2 MB) | framework-dependent: needs the **.NET 6 Runtime** |
| 0.30 drafts onwards | `LunaMultiplayer-Server-<rid>-Release.zip`, e.g. `win-x64` (~37 MB) | self-contained |

Every zip has the server under an `LMPServer/` folder plus `LMP Readme.txt` at the root. There's **no `Config/` or
`Universe/`**: LMP creates them on first start.

## The pieces

| Class | Job |
|---|---|
| `LmpReleaseFeed` | Lists releases (drafts never; pre-releases only if asked), keeps those with a server zip, newest first. |
| `LmpRelease.ServerAsset(rid)` | Prefers the per-platform zip, falls back to the single 0.29-style one. |
| `LmpVersion` | Parses `0.29.2`, `0.29.2-compiled`, `0.30.0-Draft`; reads the installed version from `Server.dll`'s product version. |
| `ServerInstaller.DownloadAsync` | Streams the zip to `%TEMP%\LunaEvo\downloads` with progress. |
| `ServerInstaller.Install` ⚠️ | Extracts, dropping the `LMPServer/` prefix. **Never writes** `Config`, `Universe`, `logs`, `Plugins`, `LunaEvo`, `LMPPlayerBans.txt` or our own exe, even if a zip contains them. Zips any file it's about to replace into `LunaEvo/ServerBackups/server-<old version>_<time>.zip`. Refuses entries that escape the folder ("zip slip"). |
| `ConfigGenerator.CreateMissing` | Writes each missing config file from the schema with the wizard's choices, GameplaySettings from the chosen difficulty preset, and LMP's defaults for the rest. |
| `RuntimeInstaller` | `winget install --id Microsoft.DotNet.Runtime.<major> --exact ...`, in a visible window (winget asks Windows for permission itself). |
| `Http` | The one `HttpClient` (with User-Agent). Tests set `Http.Override` to a fake. |

## Why the wizard writes the config itself ⚠️

LMP's defaults have `RegisterWithMasterServer = true` and `Upnp = true`. Starting a fresh server "once to generate
the files" would list it publicly and ask your router to open ports before the user has chosen anything. So
`ConfigGenerator` writes the files first, and the very first start already uses the user's choices.

The generated XML is UTF-8 with an honest `encoding="utf-8"` declaration and CRLF. LMP rewrites it in its own format on
startup anyway. A test deserializes every generated file with **LMP's own classes** through `XmlSerializer` (exactly
what the server does), and the real 0.29.2 server has started on it with no errors.

## Wizard defaults (and why)

| Choice | Default | Why |
|---|---|---|
| Public server list | off | LMP's default is on; a beginner's first server is usually for friends. |
| UPnP | off | Only needed for players outside the network; touches the router. |
| Website (live players) | on, `127.0.0.1` | Powers the Players page; localhost means nobody else on the network can read it. |
| Mod Control | off | LMP's default is on (stock parts only), which rejects modded players. LMP itself says off is fine for private servers. |
| Copy LUNA EVO into the folder | on | So double-clicking it there opens the server. Skipped for dev builds, which need their DLLs. |

## Updating

`MainWindow.CheckForUpdateAsync` runs when a server opens (if *Check for new LMP versions* is on) and shows
"LMP x.y.z is available" in the rail. Settings → LMP updates → **Update** requires the server to be stopped, then
download → `Install` → reopen (the new `Server.dll` may bring new settings) → back to Settings with the result.
Players need the matching LMP **client** mod version.
