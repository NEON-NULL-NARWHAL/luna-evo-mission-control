# 06 · Build and release

[← Meat and Potatoes](README.md) 📦

## Requirements

- Windows 10/11, the **.NET 10 SDK**.
- To *run* a real LMP 0.29 server you also need the **.NET 6 Runtime** (LMP's requirement, not ours).

## Everyday commands

```powershell
dotnet build
dotnet test
dotnet run --project src/LunaEvo.ConfigManager --launch-profile "Fixtures (in repo)"
dotnet run --project src/LunaEvo.ConfigManager --launch-profile "Local LMP folders"   # ../LMPServer + ../server-configs
```

> The *Fixtures* profile edits `tests/Fixtures/Config` if you press Save. If you do, put the files back with
> `git checkout tests/Fixtures`.

## Making the exe

```powershell
.\publish.ps1            # or: .\publish.ps1 -Runtime win-arm64
```

This produces `dist\LunaEvo-ConfigManager-<version>-<runtime>\LunaEvo.ConfigManager.exe`:

| Setting | Why |
|---|---|
| `--self-contained` | The server PC needs no .NET 10 install. |
| `PublishSingleFile` + `IncludeNativeLibrariesForSelfExtract` | One file to copy. |
| `EnableCompressionInSingleFile` | About 49 MB instead of about 100 MB. |
| PDBs deleted | SkiaSharp/HarfBuzz ship about 100 MB of native symbols nobody needs. |
| No trimming | The app uses reflection (on `Server.dll`) and Avalonia; trimming would break it. |

Each version gets its own folder, so an older exe that's still running doesn't block a new build.

## Releasing a version

1. Bump `<Version>` in `src/LunaEvo.ConfigManager/LunaEvo.ConfigManager.csproj` (shown in Settings → About).
2. Add a `## <version> (date)` section to `CHANGELOG.md`.
3. Smoke-test `.\publish.ps1`'s exe in a **copy** of a server folder.
4. Commit and push.
5. `.\release.ps1` runs the tests, builds the exe and creates the GitHub release with it. Every copy of LUNA EVO 0.6+
   then offers the update by itself. See [18](18-Updating-LUNA-EVO.md).

## Things not done yet

- The exe isn't code-signed (SmartScreen warns on first run).
- No CI yet. A GitHub Actions workflow running `dotnet test` on `windows-latest` would be the next step.
- Linux/macOS builds should work (Avalonia is cross-platform; `kill -INT` replaces the Ctrl+C helper) but haven't been tried.
