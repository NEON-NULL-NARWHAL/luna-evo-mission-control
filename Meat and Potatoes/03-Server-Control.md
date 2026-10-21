# 03 · Server control

[← Meat and Potatoes](README.md) · Code: `Core/Server/`, `Core/ServerProcess.cs`, `App/ServerPage.cs` 🔌

## How LMP's server behaves (0.29.2)

We read LMP's source (`Server/MainServer.cs`, `Command/CommandHandler.cs`, `LmpCommon/BaseLogger.cs`) to learn this:

| Thing | LMP behavior | What we do |
|---|---|---|
| Output | Every line is `[HH:mm:ss][Type]: message`, time in **UTC**. Types: `LMP`, `Info`, `Debug`, `Warning`, `Error`, `Fatal`, `Chat`, `NetworkDebug`, `VerboseNetwork`. | `LogLine.Parse`; untagged lines (stack traces) inherit the kind above them. Our own lines are UTC too. |
| Commands | Reads stdin lines. `/name args` runs a command; text without a slash is broadcast as chat. At most one command per 500 ms. | `ServerRunner.SendCommand` writes a line (newlines flattened). Quick commands come from LMP 0.29.2's `RegisterCommands`. |
| Stopping | **No stop command.** Ctrl+C (also close/logoff/shutdown) runs `Exit()`, which backs up and exits cleanly. | Send Ctrl+C via a helper process (below); kill only after the timeout. |
| Restart | `/restartserver` starts a *new, detached* `Server.exe`. | Our Restart is stop then start, so we keep the console. Avoid `/restartserver` from the app. |
| Startup failure | Logs `Fatal`, then waits on `Console.ReadLine()` "to avoid closing". | After a Fatal while starting, `ServerRunner` closes stdin so it exits instead of hanging. |
| Runtime | 0.29.x is framework-dependent on **.NET 6**. | `DotNetRuntime.Check` reads `Server.runtimeconfig.json` and looks in the usual install folders. |
| Config | Read once at startup, and re-saved. | Saving while running warns; Start offers to save first. |

## `ServerRunner`

```text
Start(args?)          Server.exe with CreateNoWindow + redirected stdin/stdout/stderr (UTF-8)
State                 Stopped → Starting → Running (on "All systems up and running") → Stopping → Stopped
LineReceived          event, background thread
StateChanged          event, background thread
SendCommand(text)
StopAsync(timeout)    Ctrl+C, wait; kill after timeout. True if it stopped by itself.
Kill()                hard kill, no backup
```

`CreateNoWindow` still gives the server a (hidden) console, which Ctrl+C needs.

## `ConsoleControl` ⚠️: sending Ctrl+C on Windows

Windows only lets you send Ctrl+C to a console **you are attached to**, and it signals *every* process on that console.
Two approaches failed before this one; don't repeat them:

1. ❌ **Attach the app itself** to the server's console and send. The sending process died with it (seen in a repro).
2. ❌ **Protect ourselves with `SetConsoleCtrlHandler(NULL, TRUE)`**. That "ignore Ctrl+C" flag is **inherited by
   child processes**, so every server started afterwards silently ignored Ctrl+C, and every stop became a kill with
   no backup. The tests caught this.
3. ✅ **A helper process.** `SendCtrlC(pid)` runs `HelperPath --luna-evo-send-ctrl-c <pid>`. The helper (our own exe;
   `Program.Main` checks for that argument first) detaches from any console, attaches to the server's, ignores
   Ctrl+C *for itself only*, sends it, and exits. The same idea as CliWrap's signaler.

`AllowCtrlCInChildren()` clears any inherited ignore flag before each start, in case *we* were launched with it.
In tests, `HelperPath` points at `FakeLmpServer.exe`, which also implements the helper.
On Linux/macOS it uses `kill -INT` instead (untested so far).

## Logs

- `LogFiles.List/Read`: `logs/lmpserver_*.log`, newest first; files are opened with `FileShare.ReadWrite` because the
  server keeps the current one open.
- `LogTail`: polls the newest file every second, keeps a partial last line until it's finished, and switches when LMP
  rotates to a new file. Used when the server was **started outside LUNA EVO** (we can't read its console then).

## `ServerPage` (the UI side) 🖥️

| Part | Notes |
|---|---|
| Buttons | Start/Stop/Restart enabled by state. Stop/Restart confirm when *Ask before stopping* is on. |
| Summary cards | Built from the session's *current* (maybe unsaved) values via `RefreshSummary()`. |
| Console | Lines go `ConcurrentQueue` → `Flush()` every 100 ms → `_all` (capped at *Console lines to keep*) → `_visible` (filters + search). `Rebuild()` re-filters. Follow scrolls to the end. |
| Commands | Enter sends; Up/Down walks the history; quick-command chips fill the box (they don't send, so `/nukeksc` is never one click). |
| Outside server | Every 2 s, if we're not running one, `ServerProcess.Find` looks for this folder's `Server.exe`; if found, a `LogTail` follows its log and Stop sends it Ctrl+C. |
| Closing the app | If we started the server: *Stop server and close* / *Leave it running* / *Cancel*. Left running, it keeps going without a window; the next launch finds it as an outside server. |

## Testing it safely

Never point tests at the real server. `tests/FakeLmpServer` mimics all of the above (`--fatal`, `--ignore-ctrl-c`).
For a manual check with the real `Server.exe`, use a **copy** with `RegisterWithMasterServer`, `Upnp` and
`EnableWebsite` set to `false`, so nothing touches your router or the public server list.
