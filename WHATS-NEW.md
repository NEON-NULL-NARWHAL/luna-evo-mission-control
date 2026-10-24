# What's new in LUNA EVO Mission Control

The short version, newest first. Every detail is in the [CHANGELOG](CHANGELOG.md); downloads are on the
[releases page](https://github.com/NEON-NULL-NARWHAL/luna-evo-mission-control/releases). The app shows the same under
**App settings → What's new**, and tells you when there's a newer version.

## 2.16: go-arounds back in sequence

- A plane that goes around is put back in the sequence (or cleared again for the short visual), and the tower gives
  traffic information. KSP Wingman 0.10 also stops a takeoff that starts badly.
- **Each plane gets a profile**: what the agents have done in it and what's still to learn about it, with notes on
  how it handles.

## 2.15: training, flight plans and the radio in the game

- Ask an agent to **train** (fly fast, fly high, landings, different planes, flight plans) and it practises until
  it's learned. Agents **file their own flight plans** from the tower's table; the tower **holds over the runway** and
  gives **directs**; the tower's radio shows **in KSP** too.

## 2.14: "runway in sight"

- Agents call the runway in sight on final and the KSC tower clears them to land, like real pilots. gemma4:e4b now
  works as both the pilot and the planner.

## 2.13.1: straighter visual landings

- The short visual now ends on the centerline (10-11 m off in the test rig, was 16-23), and the Wingman page suggests
  gemma4:e4b as the planner (faster and better lessons than qwen3:4b in our eval).

## 2.13: AI pilots that practise and learn

- **Free flight**: agents practise landings on their own and write lessons; **What the agents know** shows each
  skill's numbers and the lessons, kept on your server. The tower clears a **short visual** when it's quicker, and can
  **talk on LMP chat** so everyone hears it.

## 2.12: AI pilots, installed in a click

- **Tools** on LUNA EVO patches installs KSP Wingman, its mod in your KSP and a model; the new **Wingman** page starts
  AI pilots and shows what each one is doing, with a box for orders. Landed planes now taxi off the runway.

## 2.11: a tower for AI pilots

- **KSC tower** (Play → KSC tower): a traffic controller for KSP Wingman agents. It asks their intentions, clears one
  plane at a time for an approach to KSC's runway with the whole route, holds the next, and tells a landed plane to
  vacate. The whole conversation is on the page, and saved as data.

## 2.10: all the patches on one page

- **LUNA EVO patches** in the rail: every fix for LMP, what it does, what it measured, and Install, Update or Remove for
  your KSP and your server.

## 2.9.3: tidier

- A shorter README with the details on pages of their own; planet names on the map no longer pile up.

## 2.9.2: a warning before the disk is full

- The Server console tells you when the server's drive is nearly full, before LMP can lose save files.

## 2.9.1: you stay in sight of each other

- Players no longer drift into separate times and lose sight of each other: the game keeps you in step by itself
  (switch in the LUNA EVO window in KSP).

## 2.9: a map for everyone, and invites that paste

- **Not hosting? You still get the map.** Check a server on Join and open Map: its vessels live, on the planets of your
  own KSP.
- **Pasting an invite works.** Paste the whole message from your host into Join's address box: it's checked at once.
- **What's new** in the app, and a notification when an update is out.
- Type a planet's name in the map's list to jump to it.

## 2.8: LUNA EVO inside KSP

- **A LUNA EVO button on KSP's toolbar** (with the LUNA EVO patches): your connection, who's online and what they fly,
  a clock sync, watching a nearby player's vessel, and LMP's settings in one window.

## 2.7: smoother multiplayer

- **Less lag** (29 → 22 ms from one game to another) and **far fewer lost vessel updates** on a jittery connection
  (63% → 18%), in the LUNA EVO patches and the server plugin. Measured in a test rig with real KSP.
- 2.7.1: smoother other-player vessels were tried two ways and measured; neither was reliably better, so neither ships.

## 2.6 and before

- **Patches for LMP** (2.5-2.6): join on any port with a saved password, no clock jumps when you're alone in a
  subspace, positions with the right time.
- **Live solar system map** (2.2-2.4) and the **LUNA EVO server plugin** for live positions every second.
- **CKAN, the server browser, crafts, player history, alerts**, and the rest: see the
  [README](README.md#what-it-does).
