# OnTop — working notes for Claude Code

OnTop is a Windows tray utility (C# WPF, .NET 8, MVVM): always-on-top pinning,
desktop "zones" that fire actions on a double-click, Stealth Mode (complete
unhook), and optional modules (macros, placement, action library, profiles,
window rules). It is a rewrite of a lost AutoHotkey script, runs as one
non-elevated process, and is developed by me (Godswrath88) with Claude Code.
**Security is priority #1:** [`SECURITY.md`](SECURITY.md) is the contract and
outranks everything else.

## Where things are

| Path | What |
|---|---|
| `OnTop/` | The app (`OnTop.csproj`). |
| `OnTop.Tests/` | Unit tests (xUnit); `dotnet test` runs them. |
| `Directory.Build.props` | **The only place the version number lives.** |
| `installer/`, `portable/` | Inno Setup script; portable publish script. |
| `tools/` | Helper scripts: version/privacy checks, installer build, icon, git hooks. |
| `.github/` | CI workflows, issue templates, PR template. |
| `.claude/skills/` | Project skills (session start, feedback intake, reviews, delivery gate). |
| `CHANGELOG.md` | One entry per version (Keep a Changelog). |
| `SECURITY.md` | Threat model, standing rules, per-change checklist. |
| `docs/dev/PROJECT_SCOPE.md` | Standing requirements, build rules, architecture map, rules learned. Read before changing code. |
| `docs/dev/ROADMAP.md` | Approved and pending work (Round 3 at the top). |
| `docs/testing/CHECKLIST.md` | The test checklist ("the testing doc"), by feature, stable IDs, my marks. |
| `docs/TROUBLESHOOTING.md`, `docs/BUILD.md`, `docs/UPDATES.md`, `docs/PORTABLE.md` | User and build docs. |
| `docs/history/` | Frozen records from the zip era (WHATS_NEW, SESSION_STATE, SESSION_HISTORY). Read-only. |

## Starting a session
1. `git fetch --prune`, switch to `main`, pull, and `git branch -d` any local
   branch already merged (I dislike branch clutter; auto-delete is on).
2. Check open issues and the newest "Test pass" issue for my test results
   before starting new work. Feedback on a build comes before new features.
3. The development PC has the .NET SDK: `dotnet build OnTop/OnTop.csproj` and
   `dotnet test` work locally. Use them.

## Workflow
- **One branch per piece of work** → commit → PR to `main` → CI green →
  **I merge** (never merge without my say-so) → tag `vX.Y.Z` → GitHub Release.
- **Version:** bump `Directory.Build.props` once per PR that ships code or
  user-visible docs (patch for fixes, minor for features). The csproj and the
  installer read it from there; CI fails if anything disagrees.
- **Settings schema:** every change to the stored settings shape (new field,
  new default, removed field) bumps `SettingsService.CurrentSchemaVersion` with
  a migration block, even a no-op one. Grep `AppSettings` for an existing field
  first.
- **Documenting a change** (in the same PR):
  - one `CHANGELOG.md` entry (Added / Changed / Fixed; terse, user-facing);
  - the PR description tells the story (what was reported, the root cause,
    what changed, what's still unverified). It becomes the release notes;
  - `docs/testing/CHECKLIST.md`: new items for anything testable, `⟳ retest`
    on items a fix touches; never change my marks;
  - Help text and tooltips for any user-visible feature;
  - `docs/dev/PROJECT_SCOPE.md` only when a requirement, rule or the
    architecture changes; `docs/dev/ROADMAP.md` when an item ships.
- **Reviews:** changes touching Win32 interop, hooks, hotkeys, threading,
  process launch, file I/O, config import/migration, defaults or macros get an
  independent review (`ontop-independent-review` skill) before the PR, and
  config/macro/hook/updater changes a threat review (`ontop-threat-review`).
  Record every finding's disposition in the PR description.
- **Testing:** I test by running from Visual Studio. Under the debugger an
  exception pauses ALL threads (e-stop hotkeys die and the hooks lag the whole
  PC), so for a crash or freeze ask me to run with Ctrl+F5 (no debugger).
  Logs: `%APPDATA%\OnTop\logs\`; crash bundles: `%APPDATA%\OnTop\config\crashes\`.
  Read them yourself rather than asking me to paste.
- **Test results:** failures and retests are GitHub issues (labels `bug`,
  `retest`, `needs-log`, `blocker`, `test-pass`); checklist items link `#N`.
  Never paste a whole log into a public issue (window titles are private).

## Releases
Every tag gets a GitHub Release whose first line is its status:
- **✅ Stable:** built clean and passed the smoke test AND the full checklist,
  or I explicitly say it's "good enough" (noted in the body). Only I grant
  Stable; never mark it unasked.
- **⚠ Restore point:** built clean, known issues linked.
- **🧪 Untested:** marked Pre-release.

Only Stable or Restore point may be "Latest". When a version earns Stable,
untick Pre-release, mark it Latest, and update the README "Download"
paragraph. Tags are the restore points; rolling back across a settings schema
bump shows the "newer config" notice (expected, it protects the config).
Release assets are built by CI from the tag (`.github/workflows/release.yml`).
Installer, updater and portable build are **untested until I say otherwise.**

## Hard rules
- Least privilege always: `asInvoker`, never elevate, no elevation
  workarounds. Placement failure on an elevated target is reported, not worked
  around.
- No silent `catch`. Every hook is installed only while its feature is armed,
  removed in Stealth and on exit. Any capture UI suspends global hotkeys.
- Exit and Kill are delivered on the e-stop thread, never the WPF window; no
  stop path may wait on the UI thread.
- Every `*Brush` in XAML is a `DynamicResource`. Under-the-cursor logic uses
  `Win32.TopWindowAtPoint`.
- Never write my real name, Windows username, e-mail or machine name into any
  file, commit, PR, issue or skill. The public handle is **Godswrath88**;
  commits use the GitHub noreply address; release builds use `PathMap`.
  Enforced three ways: the hooks in `tools/git-hooks/` (`core.hooksPath`)
  block commits and pushes matching the private patterns in
  `.git/info/identity-patterns` (never uploaded; never copy or print it);
  CI's `tools/check-privacy.ps1` blocks real user paths, real e-mail addresses
  and non-noreply authors; and the delivery gate scans every PR. If a check
  fires, remove the text; never bypass a hook (`--no-verify`).
- Never record a known, unfixed weakness in a public file (private advisory
  until the fix ships).
- Docs and comments are first person (as me) or neutral imperative.
- Verify the actual inserted text after an edit, never a proxy.
- More in `docs/dev/PROJECT_SCOPE.md` ("Standing Requirements", "Build Rules",
  "Rules learned the hard way").

## Build gotchas
`<Style.Triggers>` after all `<Setter>`s; extension methods need their
namespace `using`; `Owner=` on a never-shown window throws; `UniformGrid` is in
`System.Windows.Controls.Primitives`; commit DataGrid edits before rebinding
(`DeferRefresh` crash); `SetWindowPos` error 5 = the target runs elevated; the
build needs a NuGet restore (`System.Security.Cryptography.ProtectedData`).
Editing: don't run perl with non-ASCII `\x{…}` literals on repo files (it
double-encoded a whole file once); use the Edit tool or sed.

## Decided against (don't re-propose without new information)
- Multi-process / broker architecture (same-user processes aren't isolated; an
  input-synthesis helper looks like cheat-tool architecture).
- Signing or encrypting the config for tamper-proofing (validation + import
  quarantine is the real defense).
- An elevated "window mover" helper for Placement (shelved 2026-11-10; reasons
  in ROADMAP → Security / Distribution). Revisit only after code signing.
- AHK importer (scrapped). Paid code signing (not paying; Azure Trusted Signing
  or SignPath noted as options).

## Next up
1. I test v0.45.3+ with `docs/testing/CHECKLIST.md` (smoke test, then the
   `retest` issues); failures become issues and get fixed first.
2. Installer / updater / portable when I say I'm testing them.
3. Round-3 features (ROADMAP "🎨 ROUND 3"), then 1.0.0.
