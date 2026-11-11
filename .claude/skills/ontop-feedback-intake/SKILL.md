---
name: ontop-feedback-intake
description: Use when the user reports OnTop build errors, test results (checklist marks, a "Test pass" issue), a crash, a freeze, or says something "didn't work": pin the exact build they ran, read the logs and crash bundles yourself, and produce a root-cause table with evidence BEFORE writing any code.
---

# OnTop — feedback intake

The failure this prevents: fixing the symptom that was reported instead of the
cause, against code that isn't what the user ran.

## 1. Pin the build
- What did they run? The log's first lines (`OnTop started`), the version in
  the main-window subtitle, a tag, or "from Visual Studio" = their current
  checkout (`git -C <repo> log -1`, `git status`). If two candidates exist and
  it matters, ask once.
- Errors are located in the code they ran (`git show <tag>:<file>`); fixes go
  on a new branch from current `main`.
- Under the Visual Studio debugger, an exception pauses EVERY thread: e-stop
  hotkeys die and the low-level hooks make the whole PC lag. A "freeze" with
  no watchdog line in the log is usually this. Ask them to reproduce with
  Ctrl+F5 (no debugger) so OnTop's own handler logs it.
- What they didn't say they tested is untested. Never upgrade a mark.

## 2. Collect the evidence yourself
- Logs: `%APPDATA%\OnTop\logs\` (portable: `<exe>\Data\logs\`), newest first.
  Crash bundles: `%APPDATA%\OnTop\config\crashes\`. Read them; don't ask the
  user to paste.
- Signals worth a line each: `[ERROR]`, `Unhandled exception` (+ stack),
  `UI thread unresponsive`, `KILL —`, `Failed to bind`, `REFUSED`,
  `Stealth`, a setting flipping many times a second, an action line with no
  result line after it, and lines that are ABSENT that should be there.
- Build errors: list every one verbatim (code, file, line). Open each
  file:line. Known patterns: CS0246/CS0103 missing `using` or member;
  CS0136/CS0128 shadowed local; CS0104 WPF vs WinForms/Drawing ambiguity;
  CS1061/CS7036 member exists with another signature (open the class);
  XAML: `Style.Triggers` after setters, `*Brush` must be DynamicResource.
  Fix every occurrence of the pattern, not just the reported line.

## 3. Root-cause table (before any code)

| # | Checklist IDs / issue | Symptom (user's words) | Evidence (log line / file:line) | Root cause | Confidence | Fix | Retest |

- Group by cause; dependents get `[!]`.
- Confidence: **confirmed by reading** / **consistent with log** /
  **hypothesis**. A hypothesis is never shipped as "fixed": add
  instrumentation (specific log lines) and say exactly what to send back.
- A log line that claims success for something that failed is itself a
  finding: fix the log line too.
- Ask only what the evidence can't answer and that changes the fix.

## 4. Paper trail
- `docs/testing/CHECKLIST.md`: the user's marks and notes carried in
  unchanged (a dated `> MM-DD:` note under the item); `⟳ retest` on items a fix
  touches; new items for new behavior.
- Failures that won't be fixed in this PR become GitHub issues (labels `bug`,
  `retest`, `needs-log`, `blocker`). Paste only the relevant log lines, never
  a whole log (window titles are private), and generalize personal app names.
- The PR description carries the root-cause table in prose.

## 5. Then the normal pipeline
Fix on a branch → `ontop-threat-review` / `ontop-independent-review` when the
change touches their areas → `ontop-delivery-gate` → PR.
