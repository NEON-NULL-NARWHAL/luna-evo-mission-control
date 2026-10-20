# 07 · LMP gotchas ⚠️

[← Meat and Potatoes](README.md)

Things about LMP (0.29.2, and master at commit `b3b9efce`) that are surprising and that the code depends on.
**Check this list before changing XML handling or server control.**

## Config files

| # | Gotcha | Consequence | Where handled |
|---|---|---|---|
| 1 | Files declare `encoding="utf-16"` but are **UTF-8 without a BOM** (0.29.x; master writes honest UTF-8). | `XDocument.Load(path)` throws *"There is no Unicode byte order mark"*. | `ConfigFile.Load` decodes the bytes itself. |
| 2 | LMP **rewrites every config file on startup** (`SettingsBase.Load` calls `Save`), regenerating comments. | Edits made while it runs are pointless and may be overwritten. | Save warns; Start offers to save first. |
| 3 | `HearbeatMsInterval` is **misspelled** in LMP. | Renaming it breaks the server. | Kept as-is; the schema comes from LMP. |
| 4 | Enum values are **case-sensitive** (`XmlSerializer`). | `sandbox` instead of `Sandbox` stops the file loading. | `SettingValidator` (exact match). |
| 5 | Booleans may appear as `True`/`False`; 0.29's `XmlConvert` only accepts lowercase. | Mixed case may fail on older servers. | We always write lowercase. |
| 6 | **Gameplay defaults are difficulty presets** (`SetEasy`...), not property defaults; if values differ from the preset, LMP silently switches `GameDifficulty` to `Custom`. | "Reset to default" would be wrong without the preset; users get surprised by Custom. | `LmpSchemaLoader` (presets), `LmpRules.PresetFor` and the warning in `LmpRules.Check`. |
| 7 | New mod-control files get their lists from `SetDefaultAllowedParts()` / `SetDefaultAllowedResources()`. | A fresh instance has empty lists. | The schema runs every `SetDefault*()` for defaults. |
| 8 | `IntervalSettings.xml` in older files has **no comments**, though the code has `[XmlComment]`s. | Descriptions from the file would be missing. | Descriptions always come from `Server.dll`. |
| 9 | Some settings exist in the code but not in an older file. | The element is missing. | `GetValue` falls back to the default; `SetValue` adds the element with its comment. |

## Server process

| # | Gotcha | Consequence | Where handled |
|---|---|---|---|
| 10 | **No stop command.** Only Ctrl+C (or console close) runs the clean `Exit()` with its backup. | Killing loses anything since the last backup. | `ConsoleControl` + `ServerRunner.StopAsync`. |
| 11 | Windows' "ignore Ctrl+C" flag is **inherited by child processes**. | Using `SetConsoleCtrlHandler(NULL, TRUE)` in the app made every later server ignore Ctrl+C. | The helper process; `AllowCtrlCInChildren()`. |
| 12 | Attaching the app to the server's console to send Ctrl+C killed the app. | | The helper process. |
| 13 | On a startup error LMP waits on `Console.ReadLine()` forever. | The process hangs with a redirected stdin. | `ServerRunner` closes stdin after a Fatal line while starting. |
| 14 | Log times are **UTC**, whatever your time zone. | Mixed local and UTC times read out of order. | Our own console lines are UTC as well. |
| 15 | `/restartserver` starts a **new detached** `Server.exe`. | We'd lose its console. | Restart = stop + start. |
| 16 | 0.29.x needs the **.NET 6** runtime; without it `Server.exe` flashes an error and closes. | Looks like "nothing happens". | `DotNetRuntime.Check` before Start. |
| 17 | It logs **"Detected NAT addresses: <your public IP>"** at Debug level, even with the master server off. | Screenshots or shared logs can leak your IP. | Set LogLevel to Normal for screenshots; the fixture logs are synthetic. |

## Releases, website, mods (0.3)

| # | Gotcha | Consequence | Where handled |
|---|---|---|---|
| 18 | LMP's defaults list the server publicly and turn on UPnP. | "Start once to generate the config" exposes a new server before the user chooses. | `ConfigGenerator` writes the config first. |
| 19 | Release zip names changed in 0.30 (`Server-Release.zip` → `Server-<rid>-Release.zip`), and 0.30's are self-contained. | Hard-coding one name breaks updates. | `LmpRelease.ServerAsset`. |
| 20 | GitHub's API rejects requests with no User-Agent. | 403s. | `Http` sets one. |
| 21 | The website JSON is an **array** holding one object, not an object. | Naive parsing fails. | `ServerInfoClient.Parse` handles both. |
| 22 | LMP's stock part list contains **both** `Decoupler_0` and `Decoupler.0`, and some retired names (`mark3Cockpit`). | A scan of a real install "misses" 15 of LMP's 531 stock names, correctly. | Documented; the scan follows KSP (dots). |
| 23 | ModuleManager patches aren't in the `.cfg` files as KSP sees them. | `+PART` copies and `!PART` deletions are missed by a file scan. | Documented limitation; LMP's in-game generator sees patches. |
| 24 | Plugin `FilePath`s are lowercase, relative to GameData, with `/`; hashes are `BitConverter.ToString(SHA256)`. | Any other format never matches on the client. | `GameDataScanner`. |
| 25 | `Environment.ProcessPath` in a dev build is an exe that needs its DLLs next to it. | Copying it alone into a server folder makes a broken app. | `SetupWizard` only copies the published single-file exe. |
| 26 | Avalonia controls can only be touched on the UI thread. | Reading a toggle inside `Task.Run` threw. | Read values first, then `Task.Run`. |

## Our server

`ModControl` is **false on purpose** (we run modded). Don't flag it as a problem.
