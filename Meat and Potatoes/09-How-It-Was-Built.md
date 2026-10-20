# 09 · How it was built (with AI)

[← Meat and Potatoes](README.md)

LUNA EVO Config Manager was written by **Claude** (Anthropic's AI model, Claude Opus 5.5) working as a coding agent
in **Claude Code** inside VS Code, directed by the project owner. This chapter records how, so the project can keep
being developed the same way, or by hand, with full knowledge of where things came from.

## The workflow

Claude Code is an agent: it reads and writes files, runs shell commands, builds, runs tests, launches the app and
reads screenshots of it. Each feature went roughly like this:

1. **The owner asked** in plain language, for example *"build out a section where you could start the server and view
   its logs, like DCS server managers"*.
2. **Claude read the real source** before designing anything. For server control it read LMP 0.29.2's `MainServer.cs`,
   `CommandHandler.cs`, `WinExitSignal.cs` and `BaseLogger.cs`, and found there's no stop command and that the log
   format is fixed.
3. **Decisions that belonged to the owner were asked**, not assumed: desktop vs web panel, .NET version, licence,
   repo visibility, commit name.
4. **Claude wrote the code and the tests together**, ran them, and fixed what failed.
5. **Claude checked the real thing**: launched the built exe, drove it with Windows UI Automation, and looked at
   screenshots, including a live run of the real LMP server in a sandbox with networking off.
6. **Project notes were kept** in `CLAUDE.md` (outside this repo) so later sessions knew the decisions and gotchas.

## What the tests and checks caught

Real mistakes, all made by the AI and caught by the process, not by luck:

| Found by | Mistake | Fix |
|---|---|---|
| Round-trip test | Saving added an extra `\r` to every line (`XElement` already writes platform newlines). | Normalise before re-applying the file's line endings. |
| Probe against real files | LMP's files claim UTF-16 but are UTF-8; `XDocument.Load` throws. | Decode bytes ourselves. |
| Server runner tests | Using `SetConsoleCtrlHandler(NULL, TRUE)` made *later* servers ignore Ctrl+C. | Helper process. |
| Repro program | Attaching the app to the server's console killed the app. | Helper process. |
| Screenshot review | Reverting a value left the file marked as changed. | A file is dirty only while a setting really differs. |
| Screenshot review | Times mixed UTC (LMP) and local (ours). | Ours are UTC too. |
| Safety review | Real logs contained IP addresses and player names. | Synthetic fixture logs; screenshots taken from a sanitized demo copy. |

## Guidelines we followed (keep following them)

- **Read before you change.** LMP's behaviour comes from its source, not from guesses. Link the file you learned from.
- **Test what matters for real.** Files are compared byte-for-byte; the server runner starts a real (fake) process;
  the UI tests click the real window.
- **Never test against the live server.** Use `tests/Fixtures`, the fake server, or a sandbox copy with networking off.
- **Keep personal data out of the repo.** Passwords, IPs, player names, and paths with usernames.
- **Write down the surprising bits** in [07 · LMP gotchas](07-LMP-Gotchas.md) and in a code comment where they bite.

## Continuing with AI

- Point the assistant at this folder first: *"Read `Meat and Potatoes/` before changing anything."*
- Ask for tests with every change, and run `dotnet test` yourself before committing.
- For UI changes, ask it to launch the app and check screenshots, not just compile.
- Review diffs like you would a human contributor's. The AI is fast and thorough, but it was also confidently wrong
  several times above. The tests are what made that safe.

## Continuing without AI

Nothing here depends on AI tooling: it's ordinary C#/.NET 10 and Avalonia, with ordinary xUnit tests.
`CLAUDE.md` and this folder are just notes.
