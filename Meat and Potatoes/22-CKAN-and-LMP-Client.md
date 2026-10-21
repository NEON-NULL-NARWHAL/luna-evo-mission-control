# 22 · CKAN and the LMP client

[← Meat and Potatoes](README.md) · Code: `Core/Setup/Tools.cs` (`CkanTools`, `LmpClientInstaller`), `App/SettingsPage.cs` (Downloads tab), `App/SetupWizard.cs` (step 3), `App/JoinPage.cs`

## Why not fork CKAN (yet)

CKAN's client is MIT, so a fork ("LNAKAN") is allowed under another name, but it's a large, fast-moving C# project.
What we needed was **one known, verified copy** instead of copies scattered around Downloads, and that doesn't need a
fork: LUNA EVO downloads the **official** `ckan.exe` from `KSP-CKAN/CKAN` releases (SHA-256 `digest`, checked again
where it's stored) into `%LOCALAPPDATA%\LunaEvo\Tools\CKAN\<version>\`, several versions side by side. If we ever
need CKAN changes, fork then and point `CkanTools.ReleasesUrl` at the fork's releases.

## What LUNA EVO reads from CKAN

| File | Used for |
|---|---|
| `%LOCALAPPDATA%\CKAN\config.json` → `GameInstances` (Name, Path) | KSP installs to pick on the Join page ("CKAN: <name>" buttons) and the Downloads tab. |
| `<ksp>\CKAN\GUIConfig.json` → `CommandLines[0]` | The options CKAN's Play button passes (e.g. `-single-instance`); **Launch KSP and join** passes the same, then LMP's `-lmp`. Only plain `-option` words are taken. |
| `<ksp>\CKAN\installed-<instance>.ckan` | (Next) the install's modpack: share a server's mods, install missing ones with CKAN. |
| `<ksp>\CKAN\registry.json` | (Next) installed mods by name, for a friendlier mod check. |
| `<ksp>\CKAN\registry.locked` | CKAN is open: don't run its command line against that install. |

## The LMP client installer

`LunaMultiplayer-Client-Release.zip` holds `GameData/LunaMultiplayer/` and `GameData/000_Harmony/`.
`LmpClientInstaller.Install`:
- KSP must be closed; the zip is re-hashed against GitHub's digest; entry count, size and stay-inside checks.
- Unpacks into a staging folder, **keeps `GameData/LunaMultiplayer/Data`** (LMP's server list and saved mod rules),
  moves the old version to `<ksp>/LunaEvo/Backups/LunaMultiplayer_<time>` (outside GameData, or KSP would load it),
  then moves the new one in.
- **Never replaces an existing `000_Harmony`**: other mods share it, and CKAN may manage a newer one.

## Where it shows up

- **Settings → Downloads:** CKAN copies (download latest, open, remove) and the LMP client (pick a KSP install,
  including CKAN's; pick a version, defaulting to the open server's).
- **Setup wizard, step 3 (optional):** download CKAN, and install the client matching the new server into the KSP
  found in Steam. Failures are logged and never fail the server setup.
- **Join page:** CKAN installs as quick picks; launches with CKAN's options.

Verified for real: GitHub's digests match the actual CKAN 1.36.4 `ckan.exe` and LMP 0.29.2 client zip.

## Tests

`ToolsTests` (Core) and `NavigationAndUpdateTests` (Downloads tab, Join with CKAN). `UiHarness` points
`CkanTools.Root`/`ConfigFolder` at temp folders and makes `SettingsPage.StartCkan` throw.
