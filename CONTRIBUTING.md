# Contributing to LUNA EVO

Thanks for helping. LUNA EVO is a small hobby project by two friends; issues and pull requests are welcome.

## Before you change anything

1. Read [`Meat and Potatoes/`](Meat%20and%20Potatoes/README.md): a map of every file, how the pieces fit, and
   [07 · LMP gotchas](Meat%20and%20Potatoes/07-LMP-Gotchas.md) (traps we already fell into).
2. Pick a task from [20 · Next steps](Meat%20and%20Potatoes/20-Next-Steps.md), or open an issue to suggest one.

## The rules

- **Tests with every change.** `dotnet test` must pass; GitHub runs it on every push and pull request.
- **Never test against a live server or a real KSP install.** Use `tests/Fixtures`, the fake server
  (`tests/FakeLmpServer`), or a copy.
- **No personal data in the repo:** passwords, IP addresses, player names, paths with user names. Screenshots come
  from a sanitised demo copy.
- **Match the surrounding code:** every source file starts with a `// LUNA EVO · <path>` header pointing to its guide
  chapter, and every control gets an AutomationId with `.WithId(...)`.
- **Security first** for anything that downloads, parses outside data, starts a process or stores a secret: see
  [14 · Security](Meat%20and%20Potatoes/14-Security.md). Report vulnerabilities privately ([`SECURITY.md`](SECURITY.md)).

## Build, test, release

```powershell
dotnet build
dotnet test
.\publish.ps1     # the single-file exe, in dist\
.\release.ps1     # maintainers: tests, exe, GitHub release (the app's updater picks it up)
```

By contributing you agree your work is licensed under the project's [GPL-3.0](LICENSE).
