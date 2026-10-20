# Ships a LUNA EVO release: runs the tests, builds the exe, and publishes a GitHub release with it attached.
# Every copy of LUNA EVO (0.6+) then offers the update by itself (Settings → Updates).
# Before running: bump <Version> in src\LunaEvo.ConfigManager\LunaEvo.ConfigManager.csproj, add a "## <version>"
# section to CHANGELOG.md, commit and push. Guide: Meat and Potatoes/18-Updating-LUNA-EVO.md
param([switch]$SkipTests, [switch]$Draft)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$version = ([xml](Get-Content "src\LunaEvo.ConfigManager\LunaEvo.ConfigManager.csproj")).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$tag = "v$version"
$gh = (Get-Command gh -ErrorAction SilentlyContinue).Source
if (-not $gh) { $gh = "C:\Program Files\GitHub CLI\gh.exe" }

# The release notes are this version's CHANGELOG section; refuse to ship without one.
$changelog = Get-Content CHANGELOG.md -Raw
$section = [regex]::Match($changelog, "(?ms)^## $([regex]::Escape($version))\b.*?(?=^## |\z)").Value.Trim()
if (-not $section) { throw "CHANGELOG.md has no '## $version' section. Write one first." }
if (git status --porcelain) { throw "There are uncommitted changes. Commit and push them first, so the release matches the code." }
& $gh release view $tag 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) { throw "Release $tag already exists. Bump the version first." }

if (-not $SkipTests) {
    dotnet test
    if ($LASTEXITCODE -ne 0) { throw "Tests failed; nothing was released." }
}
.\publish.ps1 | Out-Null
$exe = "dist\LunaEvo-ConfigManager-$version-win-x64\LunaEvo.ConfigManager.exe"
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()

$notes = @"
**Download ``LunaEvo.ConfigManager.exe`` below.** One file for Windows 10/11 x64, nothing to install. Already have LUNA EVO 0.6 or newer? It offers this update by itself (Settings → Updates).

SHA-256: ``$hash`` (check with ``Get-FileHash .\LunaEvo.ConfigManager.exe``). The exe isn't code-signed, so SmartScreen may ask the first time.

$section
"@
$notesFile = New-TemporaryFile
Set-Content $notesFile $notes -Encoding utf8
$flags = @("--title", "LUNA EVO Config Manager $version", "--notes-file", $notesFile, "--target", (git rev-parse HEAD))
if ($Draft) { $flags += "--draft" } else { $flags += "--latest" }
& $gh release create $tag $exe @flags
Remove-Item $notesFile
if ($LASTEXITCODE -ne 0) { throw "gh couldn't create the release." }
"Released $tag ($hash)."
