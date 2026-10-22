# 27 · LUNA EVO patches for the LMP client (2.5)

[← Meat and Potatoes](README.md) · Code: `src/LunaEvo.ClientPatches/` (`PatchLoader`, `CommandLinePatch`, `LmpAddress`),
`Core/Join/ClientPatches.cs`, `Core/Join/KspLauncher.cs`, `App/JoinPage.cs`

The owner's plan from [25](25-LMP-Under-The-Hood.md#delivering-fixes-as-patches-through-mission-control-owners-idea-2026-10-22):
fix LMP with patches on top of the official client, delivered by Mission Control, never changing what goes over the
network. This is the first one.

## The bug it fixes (LMP 0.29.2, seen in the test rig)

KSP started with `-lmp lmp://host:port` is supposed to join that server at the main menu. LMP's
`MainSystem.HandleCommandLineArgs` cuts the address at `address.LastIndexOf(":")` measured on the **whole** text but
applied to the text without `lmp://`, so for any normal port it's past the end and `Substring` throws
`ArgumentOutOfRangeException`. That happens inside `MainSystem.Awake`, so the rest of LMP's startup (its network
included) never runs: no "LMP Finished awakening", no connection, for the whole session. There's also no way to give a
password. So until now Mission Control could only auto-connect to port 8800 without a password, and put every other
server in LMP's list for the player to click.

## How the patch works

- A KSP mod, one DLL in `GameData/000_LunaEvoPatches/` ("000_" so KSP loads it before `LunaMultiplayer`; checked in
  KSP.log). `PatchLoader` is a `KSPAddon(Startup.Instantly)`: it checks the LMP client is 0.29.x and that
  `HandleCommandLineArgs` exists, then uses **LMP's own Harmony** (`000_Harmony`) to put `CommandLinePatch.Prefix`
  in front of it. On any other version, or any error, it patches nothing and LMP runs as it always has.
- `Prefix` does what LMP's method does (`-debug`, `-logFile`, `-lmp`) and returns false so the original doesn't run.
  The address goes through `LmpAddress.TryParse`: `lmp://host`, `lmp://host:port`, `host:port`, `[IPv6]:port`, a
  trailing slash; anything else is refused (no host, port outside 1-65535, spaces, `@`, `/`, over 300 characters).
- **The password never goes on the command line** (any program on the PC can read a process's command line). At the
  main menu, before LMP connects, `FillPassword` takes it from LMP's own server list: the entry with the same address
  and port, which Mission Control writes anyway. The log says "(with its password)", never the password.
- Nothing sent to the server changes, so patched and unpatched players play together on any LMP server.

## Building

`src/LunaEvo.ClientPatches` targets net472 (C# 7.3, like LMP's client) and compiles against KSP's and LMP's own DLLs
from a KSP install: `KspDir`, by default `TestRig/KSP` next to the repo folder. Those DLLs can't be in a public repo,
so the project isn't in the solution, CI never builds it, and the app only embeds it when that KSP exists
(`HasKsp` in `LunaEvo.MissionControl.csproj`). `release.ps1` refuses to publish an exe that doesn't carry it.
`LmpAddress.cs` has no KSP types, so Core.Tests compiles the same file to test and fuzz it.

## In Mission Control

- **Join → Install the LUNA EVO patches** (`join/patches`), shown when the chosen KSP has the LMP 0.29 client and this
  build carries the patches. A dialog says exactly what it does; KSP must be closed. **Update** when a different copy
  is there, **Remove** takes it out (and the folder if empty).
- With the patches in that KSP, `KspLauncher.PlanFor(..., patched: true)` is always `AutoConnect`, and
  `LaunchArguments` gives `-lmp lmp://host:port` (IPv6 in brackets). The server, with its password, is saved in LMP's
  list first, as before.
- Tests fake the bytes with `JoinPage.PatchBytes`.

## Checked for real (2026-10-22, test rig)

1. Without the patch, KSP with `-lmp lmp://127.0.0.1:8800`: `ArgumentOutOfRangeException` in
   `HandleCommandLineArgs` from `MainSystem.Awake`, no "LMP Finished awakening", no connection.
2. With the patch, same command, server now **with a password** (saved only in LMP's list): "Client patches applied
   to LMP 0.29.2", "Connecting via command line to: 127.0.0.1, port: 8800", "LMP Finished awakening", "Using the saved
   server entry ... (with its password)", "Connected", "Handshake successful". Then the sync measurement ran in the same
   session ([26](26-Server-Plugin.md)).

## Next patches

The list is W1-W5 in [25](25-LMP-Under-The-Hood.md): smoother remote vessels (W1) and no time jumps when you're alone
in your subspace (W2) are client-side and protocol-safe, and the rig can now measure them before and after.
