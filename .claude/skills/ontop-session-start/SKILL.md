---
name: ontop-session-start
description: Use at the start of any OnTop development session (the WPF tray utility in this repo), or when the user asks where things stand: sync git, prune merged branches, read the working notes and open issues, check the version is coherent and the build/tests pass, and report before touching code.
---

# OnTop — session start

Runs before any edit. The repo on GitHub is the source of truth; tags are the
restore points. Companion skills: `ontop-feedback-intake` (test reports, logs,
crashes), `ontop-independent-review` and `ontop-threat-review` (risky changes),
`ontop-delivery-gate` (before every PR).

## 1. Sync (run; paste the output)
```
git fetch --prune
git status -sb
git switch main && git pull --ff-only
git branch --merged main | grep -v '^\*\| main$'      # delete these: git branch -d <name>
git log --oneline -5; git describe --tags --abbrev=0
gh pr list --state open
gh issue list --state open --limit 30
```
Uncommitted changes or a non-main branch at session start: say so and ask
before switching away. Never discard them.

## 2. Read
1. `CLAUDE.md` (workflow, hard rules). It's loaded automatically; re-read
   "Next up".
2. `docs/dev/PROJECT_SCOPE.md` before changing code: standing requirements,
   build rules, rules learned.
3. `SECURITY.md` before changing anything it covers.
4. The newest open "Test pass" issue and any `retest` / `bug` issue comments
   since the last tag: that's the test feedback.
5. `docs/dev/ROADMAP.md` top section only when planning new features.

## 3. Coherence (run; paste)
```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/check-version.ps1
grep -n 'CurrentSchemaVersion = ' OnTop/Services/SettingsService.cs
dotnet build OnTop.sln -c Debug -v q -nologo
dotnet test OnTop.sln -c Debug --no-build -nologo
gh run list --branch main --limit 3
git tag --sort=-creatordate | head -3; gh release list --limit 3
```
A failing check or a red CI run on `main` is the first thing to fix or
report. A tag without a GitHub Release is an open item (creating a release is
public: ask first).

## 4. Report, then ask
```
Version: x.y.z (props)   Schema: N   main CI: green/red   Tests: n/n
Latest tag: vX  Release status: ✅/⚠/🧪   Open PRs: ...
Test feedback since vX: <issues/comments, or none>
Untested: installer / updater / portable (until the user says otherwise)
Next per CLAUDE.md: ...
```
If the user brought a test report, crash or log, start with
`ontop-feedback-intake`: feedback on a build comes before new work.
