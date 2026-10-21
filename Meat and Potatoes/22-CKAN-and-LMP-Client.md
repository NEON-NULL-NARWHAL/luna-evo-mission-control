# 22 · CKAN and the LMP client

[← Meat and Potatoes](README.md) · Code: `Core/Setup/Tools.cs` (`CkanTools`, `CkanModList`, `LmpClientInstaller`), `App/CkanPage.cs`, `App/SettingsPage.cs` (Downloads tab), `App/SetupWizard.cs` (step 3), `App/JoinPage.cs`

## Why not fork CKAN (yet)

CKAN's client is MIT, so a fork ("LNAKAN") is allowed under another name, but it's a large, fast-moving C# project.
What we needed was **one known, verified copy** instead of copies scattered around Downloads, and that doesn't need a
fork: LUNA EVO downloads the **official** `ckan.exe` from `KSP-CKAN/CKAN` releases (SHA-256 `digest`, checked again
where it's stored) into `%LOCALAPPDATA%\LunaEvo\Tools\CKAN\<version>\`, several versions side by side. If we ever
need CKAN changes, fork then and point `CkanTools.ReleasesUrl` at the fork's releases.

## What LUNA EVO reads from CKAN

| File | Used for |
|---|---|
| `%LOCALAPPDATA%\CKAN\config.json` → `GameInstances` (Name, Path) | KSP installs on the CKAN page, the Join page ("CKAN: <name>" buttons) and the Downloads tab. |
| `<ksp>\CKAN\GUIConfig.json` → `CommandLines[0]` | The options CKAN's Play button passes (e.g. `-single-instance`); **Launch KSP and join** passes the same, then LMP's `-lmp`. Only plain `-option` words are taken. |
| `<ksp>\CKAN\installed-<instance>.ckan` | Same format as an exported modpack: what "Install a mod list" reads. |
| `<ksp>\CKAN\registry.json` → `installed_modules` | The mod count on the CKAN page (`CkanModList.InstalledCount`). |
| `<ksp>\CKAN\registry.locked` | The process id of the CKAN that has the install open: no mod-list install then (`CkanModList.IsLocked`; a stale lock from a closed CKAN doesn't count). |

## Install a mod list (1.2.0)

The CKAN page takes a pasted or loaded `.ckan` modpack, or plain mod names one per line (`#` comments allowed).
`CkanModList.Parse` reads `depends` and `recommends` (name, optional exact version), checks each identifier against
CKAN's rule (`^[A-Za-z0-9][A-Za-z0-9_-]{0,99}$`) and drops duplicates. Then, after a confirm:

```
ckan update  --headless --gamedir <ksp>
ckan install --headless --no-recommends --gamedir <ksp> Mod1 Mod2=1.2.3 ...
```

split over several commands if the list wouldn't fit one Windows command line. Output streams into the page.

- **Never `install -c <their file>`:** a `.ckan` can have its own `download` URL, which CKAN would install as-is
  (audit 5, E1). Names only, from CKAN's catalogue.
- **Not our own metapackage either:** tried with the real ckan.exe 1.36.4; it works, but CKAN keeps the metapackage in
  its installed list and skips it next time ("already installed"), so it goes stale after the second list.
- CKAN's exit code is the result; unknown mod names make it fail (non-zero) and it undoes that install.
- `--gamedir` doesn't register an instance; CKAN only refreshes `builds-ksp.json` and rewrites `config.json` unchanged.

## The LMP client installer

`LunaMultiplayer-Client-Release.zip` holds `GameData/LunaMultiplayer/` and `GameData/000_Harmony/`.
`LmpClientInstaller.Install`:
- KSP must be closed; the zip is re-hashed against GitHub's digest; entry count, size and stay-inside checks.
- Unpacks into a staging folder, **keeps `GameData/LunaMultiplayer/Data`** (LMP's server list and saved mod rules),
  moves the old version to `<ksp>/LunaEvo/Backups/LunaMultiplayer_<time>` (outside GameData, or KSP would load it),
  then moves the new one in.
- **Never replaces an existing `000_Harmony`**: other mods share it, and CKAN may manage a newer one.

## Where it shows up

- **CKAN page (Play → CKAN mods, 1.2.0):** status (LUNA EVO's copy and version, CKAN open?), copies (download latest,
  open, remove older), your CKAN installs (LMP version, mod count, open in CKAN), and Install a mod list.
- **Settings → Downloads:** the LMP client (pick a KSP install, including CKAN's; pick a version, defaulting to the
  open server's), plus a link to the CKAN page.
- **Setup wizard, step 3 (optional):** download CKAN, and install the client matching the new server into the KSP
  found in Steam. Failures are logged and never fail the server setup.
- **Join page:** CKAN installs as quick picks; launches with CKAN's options.

Verified for real: GitHub's digests match the actual CKAN 1.36.4 `ckan.exe` and LMP 0.29.2 client zip.

## Tests

`ToolsTests` (Core: parsing, commands, lock, mod count) and `NavigationAndUpdateTests` (CKAN page status, mod-list
install with a fake runner, refusing while locked; Join with CKAN). `UiHarness` points `CkanTools.Root`/`ConfigFolder`
at temp folders and makes `CkanPage.StartCkan`, `RunCkan` and `PickModList` throw unless a test fakes them.
