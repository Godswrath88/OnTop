# OnTop — Session State (checkpoint ledger — READ THIS FIRST IN A NEW SESSION)

> **Purpose:** this is the resume-OR-rollback contract for a multi-stage session
> that may be cut off at any point (credits, context, environment reset).
> Every stage is a complete, self-consistent, independently-buildable delivery:
> its own version bump + CHANGELOG + WHATS_NEW + PROJECT_SCOPE line + zip.
> **No half-stage is ever delivered.** So any delivered zip IS a valid restore
> point, and this file tells you which one it is and what came before.
>
> **Also read HANDOFF.md** (session-level bootstrap) — this file is finer-grained
> (stage-level). Keep both current. Delete/overwrite this file only when I
> declare the staged plan finished or abandoned.

---

## How to ROLL BACK (total or partial)

The zip is the only backup. Rollback = restore an earlier delivered zip:

| To go back to… | Restore this zip | Notes |
|---|---|---|
| **Pre-session baseline (v0.33.8, schema 18)** | `OnTopWPF_v0.33.8_baseline.zip` | Byte-identical to what I uploaded at the start of S28. Total rollback point. |
| The end of any completed stage | That stage's zip (name in the ledger below) | Each stage zip is complete on its own. |

Rules:
- `unzip -o <zip>` over an empty directory; never mix trees.
- A rollback that crosses a **schema bump** is safe in the DOWNGRADE direction
  only because newer configs load best-effort (S20 rule) — the older build won't
  clobber the newer config. Fields it doesn't know are dropped ON THE NEXT SAVE,
  so if you roll back a build, expect settings added by the rolled-back stage to
  be lost (they are listed per stage below).
- If a stage was **partially applied** in a session that got cut off (zip never
  delivered), it does not exist — restore the previous stage's zip and redo it.
  Never try to "finish" a tree that was never delivered.

## How to RESUME

1. Restore the newest delivered zip (highest version in the ledger).
2. Read HANDOFF.md, then PROJECT_SCOPE.md / ROADMAP.md / WHATS_NEW.md as usual.
3. Find the first stage below that is not ✅ and continue from its plan. Stage
   plans are written in enough detail to be picked up cold.
4. My build feedback for an earlier stage always comes FIRST.

---

## Stage ledger (S28 — "finish everything" session, started 2026-09-16)

Legend: ✅ delivered (zip exists) · 🔧 in progress in a session (NOT delivered —
does not exist if the session died) · ⏳ planned.

### ✅ Stage 0 — Rollback scaffolding (no code)
- Baseline zip delivered: `OnTopWPF_v0.33.8_baseline.zip`.
- This file created; HANDOFF.md / MASTER_PROMPT.md / PROJECT_SCOPE.md updated to
  reference it. Delivered inside every later stage zip.

### ✅ Stage A — v0.34.0 — Config integrity hardening — zip: `OnTopWPF_updated.zip` at the time of delivery (I keep a copy named `OnTopWPF_v0.34.0_stageA.zip`)
Delivered 2026-09-16. Also carries Stage 0. `ImportMacrosFile`/`ExportMacrosFile` are service-side only (UI in Stage B).
Original plan: (ROADMAP "🔺 Config integrity hardening pass")
Schema 18 → **19**. Fields added: `PowerfulFieldsStamp` (string).
Plan:
1. `SettingsService.ClampMacros(List<Macro>)` — shared, range-checks every
   event (K 0..9; X/Y 0..65535 for mouse kinds; wheel D ±12000; vk 1..254 for
   key kinds; T non-decreasing, ≤ 24h); used by `Validate` AND by macro import.
2. Updater URL re-validated on load (`https://` + parseable host, else blanked +
   load-warning); `ReadSettingsFile` now ALSO runs `Validate` (restore path was
   skipping it — real gap found S28).
3. Inert-load guard: `SettingsService.IsLoading` is true during any load/read;
   the three execution choke-points (`RunZoneCommand`, `KillZoneProcess`,
   `MacroService.Play/StartRecording`) refuse + `Log.Error` if ever called
   while loading. This is the standing "inert load" regression check in
   runtime form.
4. Powerful-field change notice: `PowerfulFieldsStamp` = SHA-256 of every
   action command / process / macro-id / updater URL / watched list, written on
   every save; on load, a stored stamp that differs from the recomputed one →
   one toast + Warn log ("config's powerful fields changed outside OnTop").
5. Import quarantine + review: `ImportZonesFile` returns quarantined zones
   (RunCommand/KillProcess/ToggleWindow/RunMacro → Exit) plus a
   `List<QuarantineItem>` of what was neutralized (verbatim); new
   `Views/ImportReviewWindow` lets the user opt each item back in. Same for
   `RestoreFrom` when the file is OUTSIDE ConfigDir (own backups stay trusted —
   provenance rule). Macros arriving via restore-from-outside get hotkeys
   blanked + Enabled=false until reviewed.
6. Help + SECURITY.md + tooltips updated; checklist run.
Revert = restore the v0.33.8 baseline zip; you lose `PowerfulFieldsStamp` only.

### ✅ Stage B — v0.35.0 — Macro subsystem completion — zip: `OnTopWPF_v0.35.0_stageB.zip`
Delivered 2026-09-16. All 9 plan items built. Condition loops / scripting stay gated.
Original plan: (ROADMAP End Goal remainder that passes the safety gate)
Schema 19 → **20**. Fields added: `MacroGuardKnownAntiCheat` (bool, default
true), `MacroKillSwitch` (bool, default true), `MacroRecordPath` (bool, default
true), `HotkeyMacros` (string), `Macro.Jitter` (int px), `Profile.MacroPolicy`
(0 Inherit, 1 Deny).
Plan:
1. Known anti-cheat list (`MacroService.KnownAntiCheat`) folded into the guard
   (behind `MacroGuardKnownAntiCheat`).
2. Kill switch (3rd guard layer): a guard hit mid-run → abort AND
   `MacrosEnabled=false` + unbind + toast + Action log (behind `MacroKillSwitch`).
3. Always-visible indicator: tray icon variants (red dot recording, blue dot
   playing) + tray tooltip text + main status bar.
4. Literal-keystroke warning: MacrosWindow banner + first-record confirm.
5. Actions-only capture (`MacroRecordPath=false` drops pure-move events at stop).
6. Humanizing jitter per macro (±N px on mouse emit).
7. Per-profile policy: Deny → stop playback, no macro hotkeys bind while active.
8. Macro Export.../Import... (JSON, size-gated, ClampMacros, imported = Enabled
   false + hotkey blank + review via ImportReviewWindow; export opt-in warning).
9. `HotkeyMacros` (open the Macros window) — OPTIONAL — ACTIONS row + tray item.
Revert = restore Stage A zip; lose the fields above.

### ✅ Stage C — v0.36.0 — Features panel, Minimal Mode, optional DPAPI confidentiality — zip: `OnTopWPF_v0.36.0_stageC.zip`
Delivered 2026-09-16. Items 1–2 built; item 3: Help/tooltips + layout review done, close motion DEFERRED (cosmetic).
Original plan:
Schema 20 → **21**. Fields added: `EncryptMacros` (bool, default false),
`Macro.EncryptedEvents` (string).
Plan:
1. Settings "Features" section: every non-core toggle in one place (Standing
   Design Law consolidation) + "Minimal Mode" button (everything off, confirm).
2. Optional DPAPI (ProtectedData, CurrentUser) encryption of macro event streams
   at rest — NuGet `System.Security.Cryptography.ProtectedData`. Export always
   decrypts locally (sharing never blocked). NOTE: this adds a NuGet dependency —
   NuGet restore must work on the build machine; documented in BUILD notes.
3. Help/tooltip sweep, UI layout review, close-motion if cheap.
Revert = restore Stage B zip; lose the fields above.

---

### ✅ Stage D — v0.37.0 — Test feedback round — zip: `OnTopWPF_v0.37.0_stageD.zip`
Delivered 2026-09-16. 14 feedback items from my testing (see WHATS_NEW). Build
was confirmed clean at 0.36.1. Installer / updater / portable still untested.

### ✅ Stage E — v0.38.0 — Window Placement & Launcher — zip: `OnTopWPF_v0.38.0_stageE.zip`
Delivered 2026-09-16. Single launcher choke-point `LaunchViaShell`; launch double-gated.

### ✅ Stage F — v0.39.0 — Action Library — zip: `OnTopWPF_v0.39.0_stageF.zip`
Delivered 2026-09-16. Shared `FindHotkeyConflict` extracted per the S26 rule.
Original plan: `Models/LibraryAction` {Id, Name, Kind (RunCommand/KillProcess/ToggleWindow/
RunMacro/ApplyPlacement/SwitchProfile/PauseToggle/Peek/StealthToggle/
PinUnderCursor), Command, Process, MacroId, PlacementId, ProfileName, Hotkey,
Enabled}; `Cfg.LibraryActions`, `ActionLibraryEnabled` (default OFF), zone
`ActionLibraryId` + `ZoneAction.Library = 7`; VM `RunLibraryAction` dispatcher
(reuses every existing choke-point); per-action hotkeys bound only while
enabled; conflict check includes them; `Views/ActionLibraryWindow`; Set Action
→ Library submenu; quarantine (Enabled=false + hotkey blank); Features switch.

### ✅ Stage G — v0.40.0 — Additions — zip: `OnTopWPF_v0.40.0_stageG.zip`
Delivered 2026-09-16. All 16 (#8 satisfied by Library kinds). Original plan:
1 Pin+opacity (SetLayeredWindowAttributes; viewer −/+; hotkeys up/down) ·
2 click-through pins (WS_EX_TRANSPARENT toggle, auto-revert on unpin, hotkey) ·
3 pin window under cursor (WindowFromPoint→GA_ROOT) · 4 corner cycling hotkey ·
5 process→profile rules (reuse watcher timer; ProfileRulesEnabled) ·
6 display-signature→profile (bind button in Profiles window) · 7 more zone
gestures (Zone.Gesture: double-left / middle / triple / Ctrl+double / right
double; hook must forward more messages) · 9 Do-Not-Disturb (ToastService
static suppress predicate) · 10 macro countdown (MacroCountdownSec, status
strip) · 11 wait-for-window step (MacroEvent.K=10 + string S; editor Insert) ·
12 macro chaining (Macro.NextMacroId, depth ≤5) · 13 relative replay
(Macro.AnchorTitle/AnchorRect at record; translate at play) · 14 cheat-sheet
overlay hotkey · 15 command palette hotkey · 16 stats top-zone bars + CSV export.
Revert = Stage F zip.

### ✅ Stage H — v0.41.0 — Security scope recalibration + hold chords — zip: `OnTopWPF_v0.41.0_stageH.zip`
Delivered 2026-09-17. Keyboard-hook rule → lifecycle; launch sub-gate removed;
DPAPI default on (installed); honest anti-cheat wording; Stop Key.

### ✅ Stage I — v0.42.0 — The Twenty (second ideas list, all 20) — zip: `OnTopWPF_v0.42.0_stageI.zip`
Delivered 2026-09-17. I chose to add all twenty and debug from the build.

### ✅ Hotfix — v0.42.1 — one CS0246 (`UniformGrid` namespace) — zip: `OnTopWPF_v0.42.1_stageI_hotfix1.zip`
The 0.37–0.42 line compiles. `TESTING.md` (consolidated D–I sweep, ~90 items) added to the repo and the zip contract.

### ✅ Stage J — v0.43.0 — Layout round (my pre-sweep notes) — zip: `OnTopWPF_v0.43.0_stageJ.zip`
Three-card action row, Help TOC chips, MMB hint, retry interval, UI grouping rule. Schema 28.

### ✅ Stage K — v0.44.0 — First real test round: 8 root causes + UX contract — zip: `OnTopWPF_v0.44.0_stageK.zip`
Delivered 2026-09-24. See CHANGELOG. TESTING.md is now the standing checklist with my marks.

### ✅ v0.44.1 — repo readiness (dotfiles + README; LICENSE deferred) — zip: `OnTopWPF_v0.44.1_repo.zip`

### ✅ v0.44.2 — license + identity cleanup (no code behavior change) — zip: `OnTopWPF_v0.44.2_license.zip`
Delivered 2026-09-24. `LICENSE` = GPL-3.0 (verbatim FSF text); README states the
project is entirely AI-developed and directed/tested by me (Godswrath88); all
docs and code comments rewritten in first person with no personal identifiers;
csproj Authors/Copyright + Release-only `PathMap`; installer Publisher; Help
About line. Schema stays 29. Revert = v0.44.1 zip (nothing to lose but the
LICENSE and wording).

### ✅ Stage L part 1 — v0.45.0 — Emergency stop (e-stop thread + watchdog; pinned scan off-thread) — zip: `OnTopWPF_v0.45.0_stageL1.zip`
Delivered 2026-09-24 in response to the Pinned-Windows freeze. Schema stays 29.
Revert = v0.44.2 zip (lose nothing but the e-stop).

### ✅ v0.45.1 — SECURITY.md vulnerability-reporting section (docs only) — git tag `v0.45.1`
Delivered 2026-11-09. From here on, the project is in git (public repo
github.com/Godswrath88/OnTop). Each delivery is a commit plus a version tag.
Restore points are tags; the stable one is the `v0.43.0` release. Revert = tag v0.45.0.

### ✅ v0.45.2 — testing rework (docs + GitHub templates only) — git tag `v0.45.2`
Delivered 2026-11-09. TESTING.md → `docs/testing/CHECKLIST.md` (by feature,
stable IDs, every mark kept); new `docs/TROUBLESHOOTING.md`; `.github/` issue
templates (Bug report, Test pass); open retest items filed as issues #1–#14.
0.45.1 compiled clean in a development session on the Windows machine.
Revert = tag v0.45.1.

### ✅ v0.45.3 — hotfix: footer sub-windows crashed on open — git tag `v0.45.3`
Delivered 2026-11-10. `EditorFooter.Wrap` re-parented content the window still
owned (all 13 footer windows crashed on open since 0.44.0; likely #14); the
crash-bundle zip kept an entry open. Same round: hint popups + setting, own
icon, sticky Settings footer, per-hotkey "now active" note, admin-window
Placement question; review fixes (Stealth double hotkey, id reuse, retry log
noise, zip export). Schema 30. Independent review ran (findings dispositioned
in WHATS_NEW). Built clean (0 errors, 0 warnings). Revert = tag v0.45.2
(shows the "newer config" notice; expected).

### ⏳ Stage L — second test round → fixes → then Round 3 features (ROADMAP "🎨 ROUND 3" — approved list) — after I build 0.45.x and report per docs/testing/CHECKLIST.md + the retest issues (plus installer/updater/portable when I get to them). Then 1.0.0.

### ✅ Hotfix — v0.36.1 — first build feedback — zip: `OnTopWPF_v0.36.1_stageC_hotfix1.zip`
Fixed 3 pre-existing symbol errors (`MacroSvc` undeclared since 0.33.0, `_sync`
undeclared since 0.29.0, `idx` shadow since 0.29.0) + 1 warning. No feature change.
Next build report decides whether the 0.33–0.36 line is clean.

## Build status
- v0.33.2 (code baseline for everything up to v0.33.8): was **UNCONFIRMED** when
  Stage A started; I chose to proceed with staged work and confirm the build
  afterward. Confirmed clean at 0.36.1 and again at 0.42.1 / 0.43.0.
- v0.45.1: compiled clean (0 errors, 0 warnings) in a development session on
  the Windows machine, 2026-11-09, which covers the 0.44.x code as well.
- v0.44.0–0.44.2: not separately compiled. If a build error comes back, fix it in
  the newest stage and re-deliver (hotfix bump per the document-every-change rule).
- Installer, updater and portable build: **never tested**. Do not assume they
  work until I say I have run them.
