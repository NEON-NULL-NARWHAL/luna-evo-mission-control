# 23 · Server browser, simple mode and quick access (1.4)

[← Meat and Potatoes](README.md) · Code: `Core/Join/ServerBrowser.cs`, `App/BrowserPage.cs`, `App/MainWindow.cs`
(simple mode, Essentials, versions, issues), `App/ServerPage.cs`, `App/PlayersPage.cs`, `App/VesselsPage.cs` (cards)

## Where the public list comes from

LMP's in-game browser asks the master servers in `LunaMultiplayer/MasterServersList/MasterServersList.txt` over UDP
(Lidgren, ports 8700/8750). Each master server also runs a small web server (`LmpMasterServer/Http`, port 8701/8751,
behind HTTPS on the public ones) and `GET /json` returns `ServerJson[]` (`LmpMasterServer/Structure/ServerJson.cs`):
`ExternalEndpoint` as `"ip:port"`, `ServerName`, `Description`, `Version`, `Password`, `Cheats`, `GameMode`
(0 Sandbox, 1 Science, 2 Career, from `LmpCommon/Enums/GameMode.cs`), `PlayerCount`, `MaxPlayers`, `Country`,
`DedicatedServer`, `Website`, `WebsiteText`.

`ServerBrowser.MasterLists` holds the HTTPS lists (DasSkelett and Nightshade, main and nightly). All are asked at
once with a 15 s limit and an 8 MB cap; those that answer are merged by address. Checked for real (2026-10-20): 368
servers from 2 of 4 (Nightshade answered 502) in about a second. When LMP's list changes, update `MasterLists`.

Parsing is defensive like every other network read: types checked, text cut to 256 characters and control
characters removed, only IP literals accepted as endpoints, website links only if http(s).

`BrowserPage` loads on first visit, keeps the list until Refresh, filters by your LMP client version (from your KSP's
`LmpClient.dll`) and sorts by players. **Join** calls `JoinPage.OpenServerAsync(host, port)`.

## Simple mode and Essentials

- `AppSettings.SimpleMode`. `MainWindow.ApplySimpleMode` hides the rail pages in `AdvancedPages`, every settings tab
  but Essentials (`TabSet.ShowOnly`) and the settings search. Nothing is removed; code can still navigate there.
- `MainWindow.Essentials` lists (file, setting) pairs. The Essentials tab uses ordinary `SettingRow`s on the same
  session, so both copies stay in step; they're kept in `_essentialRows` so search and `GoToSetting` use the file tab's row.

## Quick access

- `Ui.Card(id, label, value, click, selected)`: a summary card; a `Button.card` when clickable (styles in `App.axaml`).
- Console cards call `MainWindow.QuickEditAsync(file, setting, title)`: prompt, validate (an error puts the old value
  back), then the normal `SaveAsync` (backup, review, running-server warning).
- `VesselsPage.ShowKind(label)` filters by a card; Players' cards open the Online tab or Vessels with a filter.
- `MainWindow.ShowIssues()` lists `session.Validate()` where search results go; `GoToSetting(file, setting)` opens
  the tab and scrolls to the row. The exposure banner uses it for `Upnp`.
- `MainWindow.ShowVersions()`: LUNA EVO, LMP server (`Schema.ServerVersion`), KSP (`KspLauncher.GameVersion`, from
  `readme.txt`), LMP client, and the CKAN in use. Refreshed on every page change.

## Errors

`Core/ErrorLog` writes to `%APPDATA%\LunaEvo\ConfigManager\logs\errors.log`. `Program.Main` hooks
`AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` and `Dispatcher.UIThread.UnhandledException`
(handled, so the app carries on). Only in `Program`: headless tests rely on errors failing the test.

## Tests

`BrowserTests` (Core: merging, broken entries, endpoints, KSP version, port range) and `QuickAccessTests` (UI: versions,
issues, quick edit, vessel cards, simple mode, browser to Join, allowing parts).
