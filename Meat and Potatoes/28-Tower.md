# 28. The KSC tower

A traffic controller for AI pilots, the owner's idea: [KSP Wingman](https://github.com/NEON-NULL-NARWHAL/ksp-wingman)
agents ask it for an approach, it asks their intentions, clears one plane at a time for a published RNAV approach to
KSC's runway 09 with the whole route, holds the next, and tells a landed plane to vacate. It lives in LUNA EVO (rail:
Play → KSC tower), not in KSP: the conversation is on its own channel, not LMP's chat.

## 28.1 The pieces

| File | What |
|---|---|
| `Core/Tower/Tower.cs` | The rules (pure: no network, no clock): planes, their states, the channel, KSC's runway frame and approaches (`Ksc`). |
| `Core/Tower/TowerServer.cs` | The channel over HTTP on **localhost only** (`http://localhost:8920/`): `POST /v1/report` and `POST /v1/say`. `Answer()` is the whole request handling, tested without a socket. |
| `TowerPage.cs` | Start/Stop, the planes (state, number, approach, height, speed), the channel (newest last, 200 lines), and the channel log's folder. |
| `Program.cs` | `--tower [port]` starts it at startup and opens the page (rig tests: SUPPORT TOOLS' `RigRun tower`). |

Wingman's side is `TowerClient.cs` in KSP Wingman: with `WINGMAN_TOWER_URL` set (localhost only), it reports every 3 s,
answers "say intentions" from what its pilot is doing, and flies what it's told (a route, a hold, vacating).

## 28.2 The protocol

Both endpoints take a small JSON object and answer `{"messages": [...]}`: the tower's messages for that plane that it
hasn't had yet. A message has `id`, `time`, `from`, `to`, `kind`, `text`, and for some kinds a `route` (`name`,
`runway`, `fixes`: `name`, `latitude`, `longitude`, `altitude`, `speed`) or a `hold` (`fix`, `latitude`, `longitude`,
`altitude`, `radius`, `number`).

| Request | Fields |
|---|---|
| `/v1/report` | `callSign`, `latitude`, `longitude`, `altitude`, `heading`, `speed`, `situation` (KSP's: Flying, Landed...), `phase` |
| `/v1/say` | `callSign`, `kind` (`intent`, `request-approach`), `text` |

Kinds the tower sends: `ask-intent`, `info`, `clearance` (with the route), `hold` (with the hold), `cleared-to-land`,
`vacate`.

The whole channel is also kept as data: `logs/tower-<date>.jsonl` next to LUNA EVO's settings file, one message per
line in the same JSON as above (`TowerServer.LogPath`; a failing disk loses the log, not the tower).

Checks, like every outside input (chapter 14): POST only, 16 KB at most, JSON object with a `callSign`, numbers must be
JSON numbers and finite, strings are cut (call sign 40, text 300) and control characters dropped; anything else is a
4xx answer, never an exception (`FuzzTests.Tower_channel_requests`). Localhost only: no LAN, no firewall prompt.

## 28.3 The rules

- A new call sign gets "say intentions". An intent with "land" in it, or a `request-approach`, asks for the runway;
  anything else is a local flight ("roger, report when you want to land").
- **One plane on the runway at a time.** The first to ask is cleared (`Ksc.Pick`: far out west and facing the runway,
  straight in; otherwise its side's pattern, north or south). Anyone asking while a plane is on approach or still on
  the runway holds: its side's hold point (4 km past touchdown, 7 km out, a 2 km circle), stacked 300 m apart from
  1,200 m.
- On final within 9.5 km and 1.5 km of the centerline: "cleared to land", once. Stopped on the runway: "vacate".
  A plane can't taxi in Wingman, so it recovers; when its next report isn't Landed (back at the Space Center), the
  next holding plane is cleared from where it is.
- A plane silent for 60 s is lost from the channel and gives up its place.

## 28.4 Tested

- Core: `TowerTests` (the whole two-plane sequence with a timeline of reports, a silent plane, the frame, the HTTP
  answers) and the fuzz test. UI: `TowerPageTests` (two agents through `Answer`, the rows and channel lines, Stop).
- In the rig (2026-10-23, two KSPs and two Wingman agents, RigRun): RigPilot cleared RNAV 09 SOUTH, RigViewer held as
  number 2 at KSC-HS for six minutes, RigPilot cleared to land, landed and vacated, RigViewer cleared the same second.

## 28.5 Next

The tower talking in LMP's chat too (for human players), runway 27 and other airports, a map of the traffic on the
page, and a tower an AI can speak for (it's plain rules today).
