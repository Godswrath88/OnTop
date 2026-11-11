---
name: ontop-independent-review
description: Use before opening a PR for any OnTop change that touches Win32 interop, hooks, hotkeys, threading, process launch, file I/O, config import/migration, defaults or the macro module: a fresh subagent that has seen none of the reasoning reviews the diff adversarially against SECURITY.md and the project rules; every finding gets a written disposition, and non-trivial fixes get a second pass on the fix delta.
---

# OnTop — independent review

The session that wrote the code can't grade it: it already believes the code
works. This hands the DIFF to an agent that has seen none of the reasoning
and is told to break it. Track record: it has caught Kill queuing behind a
3 s Exit wait, a log write in front of `TerminateProcess`, a Stealth change
that would park PCs with resident anti-cheat in permanent Stealth, hotkey ids
that ran out, a stale-id unregister that removed the macro panic key, and an
export that could truncate settings.json. None were compile errors.

## 0. Independence rules
- The reviewer is a fresh Agent (general-purpose) that gets ONLY step 2's
  items: never this conversation's reasoning, CHANGELOG/PR text explaining why
  it's safe, earlier findings, or "note: X is fine because…".
- Don't argue with the reviewer. Record dispositions (step 3). If a finding
  needs clarification, spawn a fresh reviewer with the clarified FILES.
- Paste the reviewer's findings verbatim into the reply (or a file when long),
  with the disposition table.
- Claims are written to be disprovable: "entering Stealth removes the
  recorder hooks", not "Stealth is improved".

## 1. Produce the diff (scratchpad, never /tmp or the repo)
```
R=<session scratchpad>/review; rm -rf "$R"; mkdir -p "$R"
git diff main...HEAD -- OnTop OnTop.Tests installer portable tools > "$R/code.diff"
git diff --stat main...HEAD
```
Include uncommitted work if any (`git diff HEAD -- ...`). If the diff touches
something the CHANGELOG entry / PR draft doesn't mention, that's the first
finding: fix the entry or the diff before spawning.

## 2. Spawn the reviewer with ONLY
- `$R/code.diff`; the full current paths of every file it touches (and
  permission to open anything they call into); `SECURITY.md`; `CLAUDE.md`
  ("Hard rules", "Build gotchas") and `docs/dev/PROJECT_SCOPE.md` ("Build
  Rules", "Rules learned the hard way");
- permission to run `dotnet build OnTop.sln -c Debug` and `dotnet test`
  (read-only otherwise; no file edits);
- the claims, one falsifiable line each;
- "Don't read CHANGELOG.md or the PR description until check 9, then only
  the top entry";
- this brief, verbatim:

> You are reviewing a change to a C# WPF (.NET 8) Windows tray utility that
> installs low-level hooks and global hotkeys. You did not write it. Assume it
> is wrong until shown otherwise. Report findings only — no praise, no summary
> of what the change does. For each finding: file:line, what fails, the
> concrete input or state that triggers it, severity (compile / crash / hang /
> security / regression / data-loss / docs). Check specifically:
> 1. Will it compile and do the tests pass? Run the build and tests. Every
>    called member exists with that signature; every type has a `using`; no
>    duplicate members; no shadowing (CS0136); static methods using instance
>    members; changed return types (check every caller); removed members still
>    referenced; initializers capturing fields assigned later.
> 2. Threading: DependencyObjects touched off the dispatcher; `.Wait()` /
>    `.Result` / sync `Invoke` on the UI thread; any path from a hotkey, the
>    exit function or the kill function that waits on the UI thread or disk
>    (forbidden); timer callbacks that aren't exception-safe.
> 3. Event chains and re-entrancy: a throwing handler aborts later
>    subscribers (what does the skipped one do?); handlers changing other
>    properties; BeginInvoke work racing Dispose, shutdown or a second call.
> 4. Hooks/hotkeys: every SetWindowsHookEx has an Unhook on feature disable,
>    Stealth and exit; every RegisterHotKey an Unregister; ids never reach a
>    different owner (stale fields, queued WM_HOTKEY); Exit/Kill never go
>    through HotkeyService; Stealth installs nothing.
> 5. Unbounded growth: counters/ids that only climb, a log line per message
>    or per retry tick, collections that never shrink, retries that never
>    give up or never recover. Estimate time-to-failure.
> 6. Real machines: for each changed default or new automatic behavior, what
>    happens with an existing config, resident anti-cheat/overlay/AV, a
>    blanked setting, sleep/resume, mixed-DPI monitors, an elevated target?
>    Does the state persist and survive restart?
> 7. SECURITY.md: each new P/Invoke, process launch, file write, network use
>    or config field maps to a threat-model line; no new silent `catch`; no
>    capability or weakening reachable from an imported config; asInvoker,
>    no elevation workaround.
> 8. Settings: if the stored shape or a default changed,
>    CurrentSchemaVersion is bumped with a migration block; no existing field
>    already covered the new one.
> 9. Try to DISPROVE each stated claim with a concrete scenario. Then read the
>    top CHANGELOG entry: every sentence the code doesn't make true, anything
>    the diff does that it doesn't mention, and stale comments / Help /
>    tooltips in touched files.
> For any category with nothing found, write "none found". End with the
> single most likely reason this build fails, and the single most likely way
> it misbehaves on a real user's PC if it builds.

## 3. Dispositions (in the reply AND the PR description)
- **FIXED** → then the gate (a fix is a new change).
- **NOT A BUG** → file:line and the specific reason.
- **ACCEPTED RISK** → recorded in the PR description under "Still
  unverified" / accepted risks (and SECURITY.md when it's a security
  posture), plus a checklist item if it needs a test.
A disproved doc claim is always FIXED by rewriting the sentence. A new
compile-error pattern goes into CLAUDE.md "Build gotchas" in the same PR.
When a finding is testable without Windows UI, add a test to `OnTop.Tests`.

## 4. Second pass on the fix delta
When fixes added logic, state or deferral: save `$R/code.diff` as
`code_before_fixes.diff`, diff the fixes (`git diff <before>..HEAD`), and
spawn a FRESH reviewer on only that, with a neutral list of WHICH members
changed (names, not reasons) and: is double teardown safe; what state exists
between a synchronous step and its deferred half; does exit/Dispose race it;
allocator collisions; initializer order. A second round of real findings
means the fix was too clever: simplify it.

## 5. If no reviewer can run
Retry once on a lighter model (`model: "sonnet"`). If that fails too, run the
brief yourself, tell the user plainly the change has NOT had an independent
review, and offer to hold the PR. Never present a self-review as the review.

## Skip
Docs-only and single-line text/label changes. Record it in the PR:
"Independent review: not required (docs-only)" or "SKIPPED-BECAUSE: …".
