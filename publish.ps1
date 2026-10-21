# Builds LunaEvo.MissionControl.exe: one self-contained file (no .NET install needed) for dropping next to Server.exe.
# Output: dist\LunaEvo-MissionControl-<version>-<runtime>\
param([string]$Runtime = "win-x64")
$ErrorActionPreference = "Stop"
$project = "$PSScriptRoot\src\LunaEvo.MissionControl"
$version = ([xml](Get-Content "$project\LunaEvo.MissionControl.csproj")).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$out = Join-Path $PSScriptRoot "dist\LunaEvo-MissionControl-$version-$Runtime"
dotnet publish $project -c Release -r $Runtime --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o $out
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
# SkiaSharp/HarfBuzz ship ~100 MB of native debug symbols that nobody needs at runtime.
Remove-Item "$out\*.pdb" -ErrorAction SilentlyContinue
Get-ChildItem $out
