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
2. Add an entry to `CHANGELOG.md`.
3. `dotnet test` must be green.
4. `.\publish.ps1`, and smoke-test the exe in a **copy** of a server folder.
5. Commit, tag (`git tag v0.3.0`), push, and attach the exe to a GitHub release:
   `gh release create v0.3.0 "dist\LunaEvo-ConfigManager-0.3.0-win-x64\LunaEvo.ConfigManager.exe" --notes-file CHANGELOG.md`

## Things not done yet

- The exe isn't code-signed (SmartScreen warns on first run).
- No CI yet. A GitHub Actions workflow running `dotnet test` on `windows-latest` would be the next step.
- Linux/macOS builds should work (Avalonia is cross-platform; `kill -INT` replaces the Ctrl+C helper) but haven't been tried.
