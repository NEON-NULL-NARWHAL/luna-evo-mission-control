# For developers


> [!TIP]
> **New to the code? Start in [`Meat and Potatoes/`](../Meat%20and%20Potatoes/README.md).** It's the maintainer's guide:
> a map of every folder and file with a symbol legend, how the pieces fit together, the LMP quirks we discovered, and
> step-by-step recipes for common changes.

House rules are in [`CONTRIBUTING.md`](../CONTRIBUTING.md); what's next is in
[`Meat and Potatoes/20-Next-Steps.md`](../Meat%20and%20Potatoes/20-Next-Steps.md).

## Build it

Requirements: Windows, the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build                     # build everything
dotnet test                      # 303 tests: core logic and the real UI, headless; no network, no real server
.\publish.ps1                    # -> dist\LunaEvo-MissionControl-<version>-win-x64\LunaEvo.MissionControl.exe
.\release.ps1                    # tests, build, and a GitHub release with the exe: every copy then offers the update
```

Run from source against the copies in `tests/Fixtures` (no real server needed):

```powershell
dotnet run --project src/LunaEvo.MissionControl --launch-profile "Fixtures (in repo)"
```

Command-line options: `--server <folder>` (contains Server.dll), `--config <folder>` (defaults to `<server>\Config`),
`--settings <file>` (LUNA EVO's own preferences, for portable use; defaults to `%APPDATA%\LunaEvo\MissionControl\settings.json`).

## Repository layout

```
luna-evo-mission-control/
├── Meat and Potatoes/        📖 the maintainer's guide: start here
├── src/
│   ├── LunaEvo.MissionControl.Core/   🧠 all the logic, no UI: schema, XML editing, validation, server control,
│   │                                    setup & updates, live data, GameData scanning, automation, joining, CKAN
│   ├── LunaEvo.MissionControl/        🖥️ the Avalonia desktop app
│   ├── LunaEvo.ClientPatches/         🔌 LUNA EVO's patches for the LMP client (a KSP mod; builds anywhere, with stand-ins for KSP)
│   └── LunaEvo.ServerPlugin/          🔌 the optional plugin that runs inside LMP's server (live map data)
├── tests/
│   ├── LunaEvo.MissionControl.Core.Tests/   🧪 logic tests (251)
│   ├── LunaEvo.MissionControl.Tests/        🧪 UI tests: the real window, driven headlessly (105)
│   ├── FakeLmpClient/                      🧪 a pretend LMP player (no KSP) for testing against a real server
│   ├── RigRunner/                          🧪 drives the in-game test rig (a stock KSP) through kRPC and measures sync
│   ├── NetJitter/                          🧪 a UDP relay that delays packets at random (a bad network, for the rig)
│   ├── FakeLmpServer/                      🧪 a pretend Server.exe so server control can be tested safely
│   └── Fixtures/                           🧪 sanitized real configs, LMP 0.29.2's Server.dll, sample logs and website JSON
├── docs/screenshots/
├── publish.ps1               📦 builds the single-file exe
├── release.ps1               📦 ships a version: tests, exe, GitHub release (the in-app updater picks it up)
├── QUICKSTART.md             ⚡ the short version: host, play, mods, troubleshooting
├── SECURITY.md               🔒 what LUNA EVO touches, every audit, how to report a problem
├── DECISIONS.md              🧭 what the owner decided and what was done about it; open questions
└── CHANGELOG.md
```

The full file-by-file map is in [`Meat and Potatoes/README.md`](../Meat%20and%20Potatoes/README.md).
