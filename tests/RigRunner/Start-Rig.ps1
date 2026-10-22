# Starts the in-game test rig: the rig's LMP server (hidden) and its stock KSP, joined by LMP by itself.
# Guide: Meat and Potatoes/26-Server-Plugin.md. The rig lives next to the repo (LUNA EVOLVED/TestRig, see its README).
#   -Fresh    a new game world (deletes the rig server's Universe; LMP makes a fresh one)
#   -Slow     KSP capped below real time (10 fps, 0.03 s of game per frame): a struggling PC, for the time patch
#   -NoPatch  without the LUNA EVO client patches (then without a server password: unpatched LMP can't take one)
#   -PatchesOff "P1,P3"  single patches off in KSP and the server plugin (LUNAEVO_PATCHES_OFF), for before/after runs
#   -Port 8801  KSP joins through that port (tests/NetJitter relays it to the server with random delays)
#   -Uncapped  no frame cap (the far-vessel test measures frames per second)
#   -Universe <folder>  start from a copy of that Universe folder instead of the current one
#   -Viewer   also a second KSP (TestRig/KSP2, player RigViewer, kRPC 50002) for RigRunner --viewer. It joins through
#             tests/NetJitter on 8802 (its LMP server list has that port), delayed by -ViewerJitter "min,max,loss"
# KSP takes the screen while it opens: run only when the owner says so.
param([switch]$Fresh, [switch]$Slow, [switch]$NoPatch, [string]$PatchesOff = "", [int]$Port = 8800, [switch]$Uncapped,
      [string]$Universe = "", [switch]$Viewer, [string]$ViewerJitter = "0,0,0", [string]$Rig = (Join-Path $PSScriptRoot "..\..\..\TestRig"))
$ErrorActionPreference = "Stop"
$Rig = (Resolve-Path $Rig).Path
$running = Get-CimInstance Win32_Process | Where-Object { $_.Name -in 'Server.exe', 'KSP_x64.exe' }
if ($running) { throw "Already running: $($running.ExecutablePath -join ', '). The owner's real server uses the same port: stop it first." }

if ($Fresh -or $Universe) { Remove-Item "$Rig\Server\Universe" -Recurse -Force -ErrorAction SilentlyContinue }
if ($Universe) { Copy-Item $Universe "$Rig\Server\Universe" -Recurse }
$env:LUNAEVO_PATCHES_OFF = $PatchesOff

# Slow or normal KSP.
foreach ($settings in @("$Rig\KSP\settings.cfg", "$Rig\KSP2\settings.cfg") | Where-Object { Test-Path $_ }) {
    $text = Get-Content $settings -Raw
    $text = $text -replace "(?m)^FRAMERATE_LIMIT = .*$", ("FRAMERATE_LIMIT = " + $(if ($Slow) { "10" } elseif ($Uncapped) { "500" } else { "30" }))
    $text = $text -replace "(?m)^PHYSICS_FRAME_DT_LIMIT = .*$", ("PHYSICS_FRAME_DT_LIMIT = " + $(if ($Slow) { "0.03" } else { "0.04" }))
    [IO.File]::WriteAllText($settings, $text)
}

# The patches in or out; the server's password to match.
$patch = "$Rig\KSP\GameData\000_LunaEvoPatches"
$parked = "$Rig\Parked\000_LunaEvoPatches"
if ($NoPatch -and (Test-Path $patch)) { New-Item -ItemType Directory -Force "$Rig\Parked" | Out-Null; Move-Item $patch $parked -Force }
if (-not $NoPatch -and -not (Test-Path $patch) -and (Test-Path $parked)) { Move-Item $parked $patch }
$general = "$Rig\Server\Config\GeneralSettings.xml"
$xml = Get-Content $general -Raw
$xml = $xml -replace "<Password>[^<]*</Password>", $(if ($NoPatch) { "<Password></Password>" } else { "<Password>rigtest</Password>" })
[IO.File]::WriteAllText($general, $xml)

$server = Start-Process "$Rig\Server\Server.exe" -WorkingDirectory "$Rig\Server" -WindowStyle Hidden -PassThru
Start-Sleep 6
$address = if ($NoPatch) { "lmp://127.0.0.1" } else { "lmp://127.0.0.1:$Port" }
$ksp = Start-Process "$Rig\KSP\KSP_x64.exe" -ArgumentList "-lmp", $address -WorkingDirectory "$Rig\KSP" -PassThru
if ($Viewer) {
    $j = $ViewerJitter.Split(",")
    $relay = Start-Process (Join-Path $PSScriptRoot "..\NetJitter\bin\Debug\net10.0\NetJitter.exe") -WindowStyle Hidden -PassThru `
        -ArgumentList "--listen", "8802", "--target", "8800", "--min", $j[0], "--max", $j[1], "--loss", $j[2], "--seconds", "3600"
    $viewerKsp = Start-Process "$Rig\KSP2\KSP_x64.exe" -ArgumentList "-lmp", "lmp://127.0.0.1:8802" -WorkingDirectory "$Rig\KSP2" -PassThru
    "Viewer started: KSP $($viewerKsp.Id) through NetJitter $($relay.Id) (min,max,loss = $ViewerJitter)"
}
"Rig started: server $($server.Id), KSP $($ksp.Id) (fresh=$Fresh slow=$Slow patched=$(-not $NoPatch) off='$PatchesOff' port=$Port uncapped=$Uncapped)"
