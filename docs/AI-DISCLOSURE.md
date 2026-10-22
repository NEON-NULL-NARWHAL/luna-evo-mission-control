# How this app was made (AI disclosure)


We want to be completely clear about this.

**Who did what**

| The project owner (human) | Claude (AI), via Claude Code in VS Code |
|---|---|
| Set the goals, release by release: a GUI config manager (0.1), then server control and theming, the setup wizard, players, mod import and automation (0.3), the Join page (0.4), player history, alerts and the mod check (0.5), self-update (0.6), crafts, home server mode and vessel cleanup (0.7 to 1.0), CKAN and the LMP client (1.1 to 1.3), the server browser and simple mode (1.4), and the rename to Mission Control (2.0), with a security audit at each milestone. Tested it on the real server and game, and found the first two security issues. | Read LMP's source to learn how settings, logging, commands, shutdown, releases, the web API, the master servers and mod checks really work. |
| Made the decisions: desktop app, Avalonia, .NET 10, GPL-3.0 (the repo started private and is now public), the look (inspired by [NOMM](https://github.com/Combat787/NOMM)), the name. | Wrote all of the code: the C#, XAML and build scripts. |
| Supplied the real server, its configs, the requirements and lists of what to fix. | Wrote all 303 automated tests, a fake LMP server, and fakes for every network call. |
| Reviewed the results and asked for changes. | Ran the tests, checked the real thing against real LMP and CKAN (below), took the screenshots, and wrote these docs. |

- **Model and tool:** Claude Opus 5.5 by Anthropic, working in Claude Code (an AI coding agent that can read files, run commands and build/test the code) inside VS Code.
- **When:** October 2026.
- **How much:** essentially every line in this repo was written by the AI. The human role was direction, decisions, review and testing.

**How we checked it**

AI-written code can look right and still be wrong, so nothing here is trusted on looks:

- **303 automated tests**, run on GitHub on every push: byte-for-byte file round trips; every validation rule;
  generated and edited configs deserialized by **LMP's own classes** exactly as the server loads them; install and
  update from zips (refusing to overwrite your config or escape the folder); a fake server that really starts, takes
  commands, crashes and stops; UI tests that click through **every tab and control**; and a fuzz test that throws
  thousands of mangled inputs at every parser of outside data.
- **Against the real thing:**
  - The wizard downloaded the real LMP 0.29.2 from GitHub, and the real server started on the generated config **with no errors**.
  - The Players page read the real server's live JSON; a Universe backup was taken while it ran, and it stopped cleanly.
  - The Join page checked a real running server and found a real KSP install with LMP 0.29.2.
  - Downloads matched GitHub's published SHA-256 for the real LMP 0.29.2 server and client and CKAN 1.36.4.
  - The real `ckan.exe` installed and dry-ran mod lists (up to 108 mods) on a throwaway KSP folder, and LUNA EVO's
    export of a real install matched the modpack it was set up from. It also showed that CKAN's command line silently
    skips "which provider?" questions; LUNA EVO now asks them (checked with Parallax's four on a clean KSP folder).
  - The server browser read 368 real public servers from LMP's master servers.
  - The GameData import scanned a real modded KSP install (4,382 `.cfg` files): 1,527 parts and 132 plugins with hashes.
- **Visual checks:** every page was screenshotted and reviewed during development.

The tests caught real mistakes along the way, which is why they exist. Examples: Windows passing an "ignore Ctrl+C"
flag down to every later server, LMP's files claiming `utf-16` while being UTF-8, a background thread touching a UI
control, and an update message being hidden by the update offer. They're documented in
[`Meat and Potatoes/07-LMP-Gotchas.md`](../Meat%20and%20Potatoes/07-LMP-Gotchas.md) and
[`Meat and Potatoes/09-How-It-Was-Built.md`](../Meat%20and%20Potatoes/09-How-It-Was-Built.md).

**What that means for you:** treat this like any open-source project. Read the code, run the tests, and keep the backups
it makes. The exe isn't code-signed, so Windows SmartScreen may warn the first time you run it.
