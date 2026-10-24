# 29. KSP Wingman in LUNA EVO

[KSP Wingman](https://github.com/NEON-NULL-NARWHAL/ksp-wingman) (AI pilots for KSP: a local model flies a plane through
kRPC) as a tool LUNA EVO installs and runs. Two places: **LUNA EVO patches → Tools (for AI pilots)** installs it, its mod
in your KSP and a model; **Play → Wingman** (on the rail once it's installed) starts agents and shows what they do.

## 29.1 The pieces

| File | What |
|---|---|
| `Core/Wingman/WingmanTools.cs` | The release (GitHub, SHA-256 checked, careful unzip) into `Tools/KspWingman/<version>/` next to LUNA EVO's settings file; Wingman's mod into a KSP's `GameData/KspWingman`; kRPC found; Ollama's version and models (`/api/version`, `/api/tags`) and a model download (`/api/pull`, streamed). |
| `Core/Wingman/AgentHost.cs` | Agents: KSP Wingman's console started with `--ipc`, one per kRPC port; `AgentEvent.Parse` reads its lines; `WingmanAgent` keeps the phase, status, last flight and the last 500 events; orders and quit go in as JSON lines. `IAgentProcess` is faked in tests. |
| `PatchesPage.cs` | The Tools group: KSP Wingman (Install / Check for updates), "Wingman in your KSP" (the Join page's KSP: mod and kRPC, a button to CKAN when kRPC is missing), "Local model (Ollama)" (ready, or Download it). |
| `WingmanPage.cs` | Start an agent (call sign, kRPC port, model, model server, use the tower), and a card per agent: phase, kRPC port, model, last flight, the plane's status, its events, an order box, Status, Stop/Remove. |
| `Program.cs` | `--wingman-agent CallSign:port[:model]` (any number) starts agents at startup and opens the page (rig tests). |

## 29.2 The console's --ipc mode (KSP Wingman 0.6)

One JSON object a line each way, UTF-8:

- **In** (LUNA EVO → console): `{"type":"order","text":"land at KSC","from":"Commander"}`, `{"type":"status"}`,
  `{"type":"forget"}`, `{"type":"quit"}`. Plain text is an order. Orders are cut to 1,000 characters.
- **Out**: `{"type":"event","time":...,"kind":...,"text":...,"data":...}`. Kinds: `wingman` (the console's own
  lines), `order`, `chat`, `call` and `result` (tool calls), `answer` (the model), `status` (the answer to a status
  request, kept apart from tool results), `phase` (`data.from`, `data.to`), `flight` (an outcome: `data.result`
  landed/crashed/splashed/..., `minutes`, `goArounds`, `approach`, `landing` with `downRunway`, `offCenterline`,
  `headingError`), `tower` (the tower's messages, `data.kind`), `error`.

`phase`, `flight` and `tower` come from Wingman's MCP server (its `events` tool, which the console polls every 2 s);
they're the same events whether a model is talking or not.

Anything the console prints that isn't such a line (a .NET crash) is shown as an `error` line, cut to 4,000
characters; lines over 64 KB are dropped (`FuzzTests.Wingman_agent_lines`).

## 29.3 Rules

- **Downloads**: only a release asset named `KspWingman-<version>.zip` with a GitHub SHA-256, through
  `ServerInstaller.DownloadAsync` (GitHub hosts over HTTPS, size limit, checksum); hashed again before unpacking.
- **Unzip**: only `KspWingman.exe`, `KspWingman.Mcp.exe`, `GameData/KspWingman/**`, README, LICENSE, CHANGELOG; at most
  200 entries and 400 MB; nothing outside the folder; into a staging folder first, so a bad zip leaves nothing behind.
- **The mod**: copied into `GameData/KspWingman` only when that folder is a KSP and KSP is closed; reparse points skipped.
- **Agents**: one per kRPC port and per call sign, at most 8; call signs are 1-20 letters, digits, `-` or `_`; the model
  server must be an http(s) address. Started with an argument list (no shell), no window, from Wingman's own folder.
  The tower is passed as `WINGMAN_TOWER_URL` only when it's running (localhost).
- **Status** is asked every 15 s while LUNA EVO is open (the status tool, no model), but never queued behind an
  unanswered one (the console answers between orders).
- **Closing LUNA EVO** asks every agent to quit, waits 3 s, then kills what's left (with its MCP server). The planes keep
  their last orders (Wingman's autopilot stays on SAS), like quitting the console.

## 29.4 Tested

- Core: `WingmanTests` (verified install and the bridge, refused unverified/tampered/escaping zips, Ollama's answers
  and a pull, an agent's events, orders, status and stop, one agent per KSP), `TowerTests.A_plane_that_taxied_off_the_runway_hands_it_over`,
  and the fuzz test.
- UI: `WingmanPageTests` (install from patches → Tools with a fake GitHub, the mod, kRPC → CKAN, the rail entry, an
  agent's card from fake console lines, an order, Stop and Remove), and the rail list in `NavigationAndUpdateTests`.
- KSP Wingman: `PhaseTests` (a whole flight's phases and outcome, crash, taxi) and `IpcTests`.
- In the rig (2026-10-23, RigRun, the tower on): RigPilot cleared RNAV 09 SOUTH, landed 1,329 m down the runway (8 m
  off the centerline), taxied off and parked 98 m beside it; the tower cleared RigViewer (holding at KSC-HS) 14 s
  after the landing. RigViewer landed 1,037 m down (16 m off) and taxied off too. Then Mission Control started both
  agents with `--wingman-agent` on the rig's KSPs: cards with phase, tower lines and status (screenshot in
  docs/SCREENSHOTS.md). Found there and fixed: a plane settling after it spawns looked like a takeoff roll (now 3 m/s
  to start rolling, 1 to stop), and the end of the taxi, still braking, looked like a takeoff (Parked beside = stopped).

## 29.5 Next

A map of each agent's plane on its card, the agents' outcomes summed up across flights (for model comparisons), and
the server-side agent knowledge base (`DECISIONS.md`, Open).
