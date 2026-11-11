---
name: ontop-threat-review
description: Use before opening a PR for any OnTop change to config import/migration, macros, zone actions, placement/launch, hooks, the updater, or before any version handed to other people: fresh subagents take attacker roles (hostile config author, same-desktop process, anti-cheat observer, unlucky user, updater tamperer) against the whole subsystem and must produce an attack path or a written "what I tried".
---

# OnTop — threat review (attacker roles)

`ontop-independent-review` asks "is this diff wrong?". This skill asks "how
do I make the PROGRAM do something its author did not intend?" It reads whole
subsystems, not diffs, and it is adversarial by role. It is a READING of the
code: it finds design flaws, missing checks and trust mistakes. It cannot
prove absence, cannot see what Windows actually does, and does not replace
the `OnTop.Tests` suite (`dotnet test`) or a real test on Windows
(`docs/testing/CHECKLIST.md`).

## When
- Any change under: `SettingsService` (load/migrate/validate/quarantine),
  `Models/*`, `MacroService` + macro file format, zone actions /
  `RunZoneCommand`, `PlacementService` (anything that launches), hook
  install/remove, `UpdateService`, `EmergencyStopService`.
- Before any version given to someone else, and once per Round (whole tree).
- Not for UI-only, docs-only or text changes.

## Independence (same as the diff review)
Each role is a FRESH Agent (general-purpose). It gets the files in step 1 and
its role brief — never this conversation's reasoning, CHANGELOG/PR-description
explanations, or another role's findings. The main session does not argue;
it records dispositions. Findings are pasted verbatim.

## 1. Files every role receives
- `SECURITY.md` (threat model + standing rules), `CLAUDE.md` "Hard rules".
- The subsystem's full source: for config → `Models/*.cs`,
  `Services/SettingsService.cs`, every `Services/*` that reads a config field
  it turned into an action (`MainViewModel.RunZoneCommand` and callers,
  `PlacementService`, `MacroService`, `AutoCloseService`, `WindowRuleService`),
  `Views/*` that import/export. For hooks/hotkeys → `Helpers/Win32.cs`,
  `Services/HotkeyService.cs`, `Services/EmergencyStopService.cs`,
  `Services/HoldChordService.cs`, `Services/MacroService.cs`, `App.xaml.cs`.
  For the updater → `Services/UpdateService.cs`, `Views/UpdatePromptDialog.cs`,
  `installer/*`, `portable/*`, `tools/build-installer.ps1`, `.github/workflows/release.yml`.
- `app.manifest`, `OnTop.csproj`.

## 2. Roles — spawn one agent per role that applies (all five for a Round)
Common preamble, verbatim, before each role brief:

> You are attacking a C# WPF (.NET 8) Windows tray utility that installs
> low-level hooks, registers global hotkeys, moves/pins other programs'
> windows, runs user-configured commands, and plays back recorded input. You
> have its full source. You did not write it; assume every trust boundary is
> wrong until the code shows otherwise. Produce FINDINGS ONLY. For each: the
> attack path as a chain of file:line references from the input you control
> to the effect, the exact input or state that triggers it, the effect, and
> severity (code-exec / elevation / persistence / covert-hook / data-loss /
> hang / detectable / info-leak). If you cannot find a path for an objective,
> write "NO PATH FOUND" for it and then list, concretely, what you tried
> (which entry points, which fields, which checks you attempted to bypass and
> what stopped you, with file:line). That list is required and is the
> deliverable when nothing is found. Do not pad, do not praise, do not
> summarize the design.

**Role A — hostile config / profile author.** You control every byte of a
JSON file the user imports or that sits in `%APPDATA%\OnTop`. Objectives:
(1) execute a command or launch a process without the import quarantine /
per-item approval firing; (2) make validation or migration ACCEPT a value
that later code treats as trusted (paths, process names, hotkey strings,
numbers out of range, huge arrays, duplicate keys, wrong types, nulls,
strings with shell metacharacters, Unicode confusables in process names);
(3) persist — survive a config reset, a profile switch, or a quarantine
release; (4) install or keep a hook/hotkey the UI shows as off; (5) crash
or hang the load so the app never reaches the UI (denial via config).

**Role B — same-desktop process.** Another NON-elevated program runs on the
same desktop. Objectives: (1) make OnTop act on the wrong window (title or
class spoofing, HWND reuse, a window created to match a rule or placement);
(2) steal or shadow a hotkey (RegisterHotKey conflicts, timing of
unregister/re-register windows); (3) race a file OnTop writes (config,
backup, log, macro file, update download) — TOCTOU, symlink/junction in
AppData, partial write on crash; (4) send window messages or WM_HOTKEY to
OnTop's windows/threads to trigger actions; (5) read anything OnTop exposes
(log contents, DPAPI scope — same user can decrypt).

**Role C — anti-cheat observer.** You are a kernel/user-mode anti-cheat
enumerating the system. Objectives: (1) list every observable artifact OnTop
creates in each state (idle, armed, Stealth, kill-switched, during macro
playback, during recording): hooks, SendInput calls, window enumeration
patterns, timers touching foreign windows, named threads, process-name
reads, registered hotkeys; (2) find any state where SECURITY.md or the UI
says "nothing installed" but something observable remains — cite the
install site and the missing removal; (3) find any path where the kill
switch leaves an artifact; (4) say which artifacts are indistinguishable
from cheat tooling and whether the app documents that honestly.

**Role D — unlucky user.** No malice, worst conditions. Objectives: crash
mid-save; disk full; AppData redirected to a slow/offline share; two
instances; target window elevated; monitor hotplug / DPI change / 3→1
monitors; sleep/resume mid-macro; clock jump; 10,000 zones; hotkey held
down across a rebind. For each: what corrupts, what hangs, what the log
SAYS vs what happened. Specifically attack `EmergencyStopService`: find a
state where Kill does not end the process within 1 s.

**Role E — updater tamperer.** You control the network (or the manifest
host). Objectives: serve a manifest that downgrades, points outside the
install dir, mismatches the hash yet installs, or installs over a running
exe; bypass the SHA-256 check (where is it computed, on what bytes, before or
after write?); make the portable/installer paths differ in what they trust.
If no updater code is reachable yet, review the design in `installer/`, `release.yml` and
`docs/dev/ROADMAP.md` updater section and report what MUST be true before it ships.

## 3. Dispositions — every finding gets exactly one, in the reply
- **FIXED** — then `ontop-delivery-gate` runs (a fix is a new change), and a
  regression test is added to `OnTop.Tests` when the path is testable
  without Windows (config, migration, parsing, hashing).
- **NOT A BUG** — file:line and the specific check that stops it.
- **ACCEPTED RISK** — added to SECURITY.md's threat model as a named,
  accepted item with the reason, and to `docs/testing/CHECKLIST.md` if it needs a real
  Windows check.
Every "NO PATH FOUND + what I tried" block is appended to SECURITY.md under
"Adversarial passes" with the date and version, so the next pass starts from
the map, not from zero, and so the checklist is backed by evidence.

## 4. Record
The reply ends with: roles run, findings count by severity, dispositions,
and the one sentence each role gave as its most likely real-world break.
The gate scorecard gets a `T threat review: ran (roles A,B,…) / not required`
row for this PR, and the dispositions go in the PR description.
