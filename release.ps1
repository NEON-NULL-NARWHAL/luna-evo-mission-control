# Ships a LUNA EVO release: runs the tests, builds the exe, and publishes a GitHub release with it attached.
# Every copy of LUNA EVO (0.6+) then offers the update by itself (Settings → Updates).
# Before running: bump <Version> in src\LunaEvo.MissionControl\LunaEvo.MissionControl.csproj, add a "## <version>"
# section to CHANGELOG.md, commit and push. Guide: Meat and Potatoes/18-Updating-LUNA-EVO.md
param([switch]$SkipTests, [switch]$Draft)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$version = ([xml](Get-Content "src\LunaEvo.MissionControl\LunaEvo.MissionControl.csproj")).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$tag = "v$version"
$gh = (Get-Command gh -ErrorAction SilentlyContinue).Source
if (-not $gh) { $gh = "C:\Program Files\GitHub CLI\gh.exe" }

# The release notes are this version's CHANGELOG section; refuse to ship without one.
$changelog = Get-Content CHANGELOG.md -Raw
$section = [regex]::Match($changelog, "(?ms)^## $([regex]::Escape($version))\b.*?(?=^## |\z)").Value.Trim()
if (-not $section) { throw "CHANGELOG.md has no '## $version' section. Write one first." }
if (-not (Select-String -Path README.md -SimpleMatch "**Version $version**" -Quiet)) { throw "README.md doesn't say **Version $version**. Update the version line first." }
if (git status --porcelain) { throw "There are uncommitted changes. Commit and push them first, so the release matches the code." }
# "release not found" on stderr is the answer we want; Windows PowerShell would treat it as an error under "Stop".
$ErrorActionPreference = "Continue"
& $gh release view $tag 2>&1 | Out-Null
$exists = $LASTEXITCODE -eq 0
$ErrorActionPreference = "Stop"
if ($exists) { throw "Release $tag already exists. Bump the version first." }

if (-not $SkipTests) {
    dotnet test
    if ($LASTEXITCODE -ne 0) { throw "Tests failed; nothing was released." }
}
.\publish.ps1 | Out-Null
# The exe must carry both pieces that run inside LMP: the server plugin and the client patches.
$built = "src\LunaEvo.MissionControl\obj\Release\net10.0\win-x64\LunaEvo.MissionControl.dll"
foreach ($piece in "LunaEvo.ServerPlugin.dll", "LunaEvo.ClientPatches.dll") {
    if (-not (Select-String -Path $built -SimpleMatch $piece -Quiet)) { throw "The exe doesn't carry $piece; check the build." }
}
$exe = "dist\LunaEvo-MissionControl-$version-win-x64\LunaEvo.MissionControl.exe"
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
# Copies from 0.6 to 1.4 (LUNA EVO Config Manager) only look for this file name; the same exe under it keeps them updating.
$legacyDir = "dist\legacy-$version"
New-Item -ItemType Directory -Force $legacyDir | Out-Null
$legacy = "$legacyDir\LunaEvo.ConfigManager.exe"
Copy-Item $exe $legacy -Force

$notes = @"
**Download ``LunaEvo.MissionControl.exe`` below.** One file for Windows 10/11 x64, nothing to install. Already have LUNA EVO 0.6 or newer? It offers this update by itself (App settings → Updates). ``LunaEvo.ConfigManager.exe`` is the same file under the old name, for copies from before the rename.

SHA-256: ``$hash`` (check with ``Get-FileHash .\LunaEvo.MissionControl.exe``). The exe isn't code-signed, so SmartScreen may ask the first time.

$section
"@
$notesFile = New-TemporaryFile
Set-Content $notesFile $notes -Encoding utf8
$flags = @("--title", "LUNA EVO Mission Control $version", "--notes-file", $notesFile, "--target", (git rev-parse HEAD))
if ($Draft) { $flags += "--draft" } else { $flags += "--latest" }
& $gh release create $tag $exe $legacy @flags
Remove-Item $notesFile
if ($LASTEXITCODE -ne 0) { throw "gh couldn't create the release." }
"Released $tag ($hash)."
