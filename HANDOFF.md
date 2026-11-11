# OnTop — Handoff Context (read this first in a new session)

**This file is the short bootstrap for a fresh development session.** It
summarizes where things stand so a new session can continue without re-reading
everything. It is safe to overwrite once the new session is rolling — but keep it
current: it is the first thing read after a break.

---

## What OnTop is
A Windows WPF (.NET 8) tray utility — always-on-top pinning, desktop "zones" that
fire actions, Stealth Mode (complete unhook), and optional modules (macros,
placement, action library, profiles, window rules). Rewrite of a lost AutoHotkey
version. Single non-elevated process. **Security is priority #1** — `SECURITY.md`
is the contract.

How development works: I direct the project and test on Windows; the code and
docs are written in development sessions whose editing environment CANNOT
compile WPF — verification there is STRUCTURAL only (xmllint, brace/paren
balance, grep cross-refs, dup-member scans). I compile on Windows and paste
build errors verbatim into the next session; fixes come from those.

## Where things stand (updated 2026-11-09)
- Current version **0.45.2**, schema **29**, 91 source files (.cs/.xaml).
- The project is in git (public repo github.com/Godswrath88/OnTop); each
  delivery is a commit + version tag. 0.45.1 = SECURITY.md vulnerability
  reporting; 0.45.2 = testing rework (docs + GitHub templates only).
- **Development sessions on the Windows machine CAN compile** (.NET 8 SDK
  installed): `dotnet build OnTop/OnTop.csproj`. 0.45.1 built clean there
  (0 errors, 0 warnings). Runtime behavior is still mine to test.
- 0.45.0 = the EMERGENCY STOP: Exit/Kill on their own thread (`Services/EmergencyStopService`), UI watchdog log lines, Pinned Windows scan off-thread. Built after the Pinned Windows button froze 0.44.x and the Exit hotkey died with it. Cause of the freeze itself is NOT confirmed — the next log (watchdog WARN + the `[Pins]` DEBUG lines before it) decides.
- 0.44.2 = license + identity cleanup only: `LICENSE` (GPL-3.0), README
  disclosure, first-person docs, metadata. **No code behavior changed** since
  0.44.0.
- 0.44.0 (Stage K) fixed the eight root causes from my first real test round on
  0.43.0 and added the Save & Close / Cancel (Revert) footer contract on every
  sub-GUI. 0.44.1 added the repo dotfiles + README rewrite.
- **Confirmed clean builds:** 0.36.1, 0.42.1, 0.43.0, 0.45.1.
- **Installer, updater and portable build: never tested.** Never assume they
  work. I will say explicitly when I test them.
- `docs/testing/CHECKLIST.md` is the standing checklist, by feature, with
  stable item IDs (Z1, M2, …) and my marks. Failures and retests are GitHub
  issues (labels `bug`, `retest`, `needs-log`, `blocker`); a checklist item
  with an open issue links `#N`. A test round is a "Test pass" issue.
  `docs/TROUBLESHOOTING.md` is the user-facing guide.
- **Releases:** every version tag gets a GitHub Release whose first line is
  its status: ✅ Stable (built clean + passed the smoke test AND the full
  feature checklist in docs/testing/CHECKLIST.md — or I override and say a
  build is "good enough"; only I grant Stable, never mark it unasked; an
  override is noted in the release body), ⚠ Restore point (built clean, known issues
  linked), or 🧪 Untested (marked Pre-release). Only Stable/Restore point may
  be "Latest". When a version earns Stable, edit its release, untick
  Pre-release, mark it Latest, and update the README "Download" paragraph.
- Round-3 features (Professional appearance mode etc.) are the approved list at
  the TOP of `ROADMAP.md` ("🎨 ROUND 3"). They wait until I have tested the
  current stages. No additions before that.

## The zip is the ONLY backup
- Every session delivers `OnTopWPF_updated.zip` (plus a versioned copy such as
  `OnTopWPF_v0.44.2_license.zip`). I keep every delivered zip until the next
  one builds clean.
- On an environment reset, RESTORE FROM THE ZIP — never rebuild from memory.
- Zip command (from the repo root):
  `zip -rq OnTopWPF_updated.zip LICENSE README.md WHATS_NEW.md ROADMAP.md PROJECT_SCOPE.md CHANGELOG.md MASTER_PROMPT.md SECURITY.md HANDOFF.md SESSION_STATE.md docs/ .github/ .gitignore .gitattributes .editorconfig OnTop.sln OnTop/ installer/ portable/ -x '*/bin/*' '*/obj/*' '*/publish/*' '*/Output/*' '*/out/*'`

## Hard rules (from PROJECT_SCOPE.md — read it in full)
- **Document every change**, including trivial and docs-only: CHANGELOG (+ link
  ref) + WHATS_NEW + PROJECT_SCOPE line per delivery. Before zipping, verify
  csproj (3 fields), installer .iss, CHANGELOG head, WHATS_NEW head and the
  PROJECT_SCOPE "current:" line all name the SAME version.
- **Verify the ACTUAL inserted text, never a proxy.** Never anchor an edit to a
  prior unverified edit (origin: 0.33.3–0.33.6 silently failed appends).
- SECURITY.md per-session checklist is mandatory. No silent catches. Help +
  tooltips updated in the same session as a feature. Every feature decides its
  hotkey story. UI grouping/layout review after any control is added.
- Bump `SettingsService.CurrentSchemaVersion` with a documented migration when
  the settings shape changes. Grep AppSettings for an existing field FIRST.
- Every `*Brush` in XAML is `DynamicResource` (frozen StaticResource brushes
  were the "accent does nothing" bug). Any capture UI suspends global hotkeys.
  Under-the-cursor logic uses `Win32.TopWindowAtPoint` (skips our overlays).
- Exit and Kill are delivered on the e-stop thread, never the WPF window. No
  stop path may wait on the UI thread (Kill: log line → TerminateProcess).
- Hooks follow the lifecycle rule: installed only while the feature is armed,
  removed in Stealth, removed on exit.
- Never write my Windows username, e-mail or machine name into any file. The
  public handle is **Godswrath88**; release builds use a `PathMap` so no local
  path is embedded.

## Key build gotchas learned
`<Style.Triggers>` must come AFTER all `<Setter>`s; extension methods need their
namespace `using` (grep can't catch it); `Owner=` on a never-shown window throws;
`UniformGrid` lives in `System.Windows.Controls.Primitives`; DataGrid edits must
be committed before rebinding (`DeferRefresh` crash); `SetWindowPos` error 5 =
the target runs elevated; one NuGet package (`System.Security.Cryptography.
ProtectedData`) means the build needs restore.

## Decided AGAINST (don't re-propose without new information)
- **Multi-process / broker architecture** — same-user processes aren't
  isolated; no plugin/script/network attack surface exists; a separate
  input-synthesis helper looks like cheat-tool architecture. Revisit only if
  plugins/scripting, untrusted binary parsing or a network service is added
  (then: low-integrity/AppContainer child + broker with a vetted verb list).
- **Signing/encrypting config for tamper-proofing** — a hostile author signs
  with their own key. Validation + import quarantine is the real defense.
- **AHK importer** — scrapped (kept in ROADMAP in case I change my mind).
- **Paid code signing** — not paying; GitHub + HTTPS + SHA-256 is the accepted
  updater stack. Azure Trusted Signing (~$10/mo) or SignPath noted as options.

## Next steps, in order
1. I run 0.45.2 through `docs/testing/CHECKLIST.md` (smoke test first, then
   the open `retest` issues #1–#14); failures become issues.
2. Stage L: fix whatever the report shows (retest items first).
3. Installer / updater / portable — when I say I am testing them.
4. Round-3 features (ROADMAP "🎨 ROUND 3", staged L1/L2/L3 then B–E), then 1.0.0.

**Repo restructure, step 2 (approved, not started; independent of testing,
docs/infra only):**
- `CLAUDE.md` at the root replaces HANDOFF.md + MASTER_PROMPT.md (Claude Code
  loads it automatically).
- Agent/process docs move into `docs/dev/` (PROJECT_SCOPE trimmed: the session
  history goes, since git log has it; ROADMAP). BUILD/UPDATES/PORTABLE move to `docs/`.
- Retire SESSION_STATE.md: tags are the restore points; the rollback rules go
  into CLAUDE.md. WHATS_NEW.md → `docs/history/`; new release prose goes to
  GitHub Release notes. Drop the zip commands and zip contract everywhere.
- Workflow: branch per stage → PR → CI green → merge → tag.
  `.github/workflows/build.yml` runs `dotnet build` on windows-latest and
  checks that the version fields agree.
- Step 3: update the OnTop skills (session-start, delivery-gate,
  feedback-intake, review skills); they still assume zips and no compiler.
