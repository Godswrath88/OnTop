# Changelog

Every OnTop (WPF) version, newest first: what shipped, grouped by Added /
Changed / Fixed. Each entry is short; the full story of a change (what was
reported, the root cause, what's still unverified) is in its pull request and
its [GitHub Release](https://github.com/Godswrath88/OnTop/releases). Versions
up to 0.45.3 have a long-form narrative in
[`docs/history/WHATS_NEW.md`](docs/history/WHATS_NEW.md).

Versions follow the WPF rewrite's own line (`Directory.Build.props`), which is
distinct from the lost AutoHotkey **v9.5**. 1.0.0 is reserved for when the
shippable installer lands and v9.5 parity is complete. Format follows
[Keep a Changelog](https://keepachangelog.com).

---

## [0.45.3] — Session 28 — HOTFIX: footer sub-windows crashed on open (+ test-round fixes)
### Fixed
- **Every footer window crashed when opened** (Placement, Pinned Windows,
  Action Library, Auto-Pin, Macros, Profiles, Profile Rules, Schedules,
  Known Anti-Cheat, Statistics, Help, Config History, Toast History):
  `EditorFooter.Wrap` added the window's content to a new panel while the
  window still owned it → `InvalidOperationException: Specified element is
  already the logical child of another element`. Present since 0.44.0. The
  content is now detached first. Likely the cause of the Pinned Windows
  "freeze" (#14).
- **Diagnostics zip (crash report + Log Viewer → Export) failed:** `diag.txt`'s
  writer stayed open while the next entry was created (`Entries cannot be
  created while previously created entries are still open`); replacing an
  existing file threw (CreateNew); the live log was read with FileShare.Read
  and could throw mid-write; two crash bundles in one second collided. Each
  entry now closes before the next; the bundle is built as `<name>.partial`
  and moved over the target only when complete (after the save dialog's
  Replace confirmation); only `.zip` targets are accepted and Export defaults
  a bare name to .zip, so a mistyped name can't truncate settings.json; the
  log is read with ReadWrite sharing; crash bundle names carry milliseconds.
- **The Stealth hotkey was registered twice** (`RegisterIfSet` + a second
  `RegisterToggleHotkey`), so the second always failed with 1409: anyone with
  a Stealth combo had a permanently "blocked" hotkey and a retry loop that
  never ended. Registered once now; a really blocked Stealth combo joins the
  retry list like the others.
- **Hotkey ids only climbed** (`_nextId++` on every retry tick); after roughly
  a day of a persistent conflict they'd pass RegisterHotKey's 0xBFFF range.
  Ids now climb and wrap inside that range, skipping live ones (never
  lowest-first: a just-freed id is not handed straight to another hotkey), and
  every id field is cleared when its hotkey is released, so a stale id can't
  later unregister another owner's hotkey (e.g. the macro panic key).
- **A combo that can't be parsed was treated as "in use by another app"** and
  retried forever. It now returns -3, gets one Warning toast and a log line
  per real bind ("isn't a valid key combination — set it again in
  Hotkeys…") and is never retried. Macro/library hotkeys say "not a valid key
  combination" instead of "failed to register". (Exit/Kill live on the e-stop
  thread and still treat an unreadable combo as "in use"; unchanged.)
- **Retry ticks flooded the log:** the "Hotkeys: toggle=…" summary (INFO) and
  the macro/library "failed to register" lines (WARN) were written every
  tick, as was "Failed to bind" (ERROR). On retry ticks these are DEBUG now
  (summary skipped); a normal bind still logs them in full.
- **Hotkey recovery note named nothing and only fired when every conflict
  cleared** ("Hotkeys Bound — The Conflict Cleared", removed). Each blocked
  hotkey now gets its own note the moment the retry binds it: "Ctrl+Alt+N
  (quick record) Is Now Active — The Other App Released It" (+ INFO line).
  Only on retry ticks, so clearing a combo or switching its feature off isn't
  announced. The conflict toast shows the configured retry interval instead
  of a hard-coded 30 s.
- "No Overlays Active" toast said "Dot Column"; it now names the Overlay column.
- A corrupt user tray-icon override fell back silently; it now logs a WARN.
### Changed
- **"Do this first" messages are hint popups now** (`Views/NoticePopup`)
  instead of easy-to-miss status-bar text or toasts: Set Group / Remove /
  Duplicate with no zone selected (Remove and Duplicate used to do nothing
  silently), Record Macro with macros off, and the Action Library picker with
  nothing to pick. The hint fades in centered over the window you clicked in
  (measured and placed in device pixels, so maximized windows and monitors
  with different scaling work), stays a few seconds, and
  fades out. Nothing to click, and it never takes focus: shown without
  activation and WS_EX_NOACTIVATE, so clicking it to dismiss doesn't either.
  Hover keeps it up. With Hint Popups off it's a Warning toast, which Do Not
  Disturb suppresses while a watched game runs.
- **Settings keeps Save & Close / Cancel (Revert) in view:** the footer is
  docked below the scroll area (thin divider) instead of scrolling with the
  sections. On open, the window is fitted to the monitor it lands on (height
  cap and position), so the footer can't end up below the screen edge.
### Added
- **Placement on an administrator window asks** (`Views/ElevatedPlacementDialog`):
  "Turn This Rule Off" / "Keep Rule" + "Don't ask again for this placement".
  At most once per rule per session; never takes focus; several at once step
  down the primary screen (each takes the lowest free position) instead of
  stacking. While Do Not Disturb suppresses (a watched game is running) it's
  an Error toast and the question is saved for a later failure. Closing the
  dialog without answering (✕, Esc, Alt+F4, exit) saves nothing. The answer is applied to the rule by id, so it still lands
  correctly if the list was reverted or edited meanwhile (or is dropped, with
  a log line, if the rule is gone). Don't-ask rules log a WARN and stay
  silent; the rule editor's new "Ask if it runs as admin" switch re-enables
  the question, in the same session too. OnTop still never elevates
  (SECURITY.md); the elevated helper idea is shelved with reasons in ROADMAP.
- **Settings → Appearance → Hint Popups:** an on/off switch (off = hints show
  as a toast) and a typed "Stay Up For N Seconds" box (digits only, 1–60,
  default 4; 0, blank or out-of-range snaps back), with a Preview button.
- **OnTop's own thumbtack icon** (`OnTop/Assets/thumbtack.ico`): a generic red
  push-pin drawn from simple shapes by `tools/make_icon.ps1`, original artwork
  under the project license. Frames 16/20/24/32/40/48/64 px (32-bit BMP) +
  256 px (PNG). Replaces an unattributed single-frame 256 px PNG icon that
  older loaders couldn't read and that had no small sizes for the tray. The
  tray asks for the small-icon size so it uses the matching frame.
- Help: the admin-window question, hint popups, and the per-hotkey "now
  active" note (which hotkeys get it: the main ones and the toggle hotkeys;
  macro/library and the rest are logged only).
### Schema
- **29 → 30** (no-op migration): `AppSettings.HintPopupsEnabled` (true),
  `NoticeSeconds` (4, clamped 1–60 on load), `PlacementRule.ElevatedNoticeOff`
  (false). The bump is what makes an older build treat the config as newer
  and not save over it, dropping these fields. Rolling back to 0.45.2 or
  0.43.0 therefore shows the "newer config" notice; that's expected.

## [0.45.2] — Session 28 — Testing rework (docs + GitHub templates only)
### Added
- **`docs/TROUBLESHOOTING.md`** — user-facing: emergency stop, file locations,
  settings recovery/reset, elevated windows, hotkey conflicts, zone gating,
  anti-cheat, how to report (logs are private — paste lines only).
- **`.github/ISSUE_TEMPLATE/`** — Bug report form (version, mode, area,
  checklist ID, steps, log lines), Test pass form (smoke-test checkboxes),
  `config.yml` (blank issues off; links to private vulnerability reporting and
  the troubleshooting guide).
- GitHub labels `retest`, `needs-log`, `blocker`, `test-pass`; the 13 open
  retest items plus the unconfirmed Pinned Windows freeze filed as issues #1–#14.
### Changed
- **`TESTING.md` → `docs/testing/CHECKLIST.md`**, reorganized by feature (B, E,
  A, Z, P, H, S, M, L, AL, R, C, X) with stable item IDs and a smoke-test
  section; every mark and note carried over; retest items link their issue.
  References updated in README, HANDOFF, MASTER_PROMPT, PROJECT_SCOPE, ROADMAP,
  SESSION_STATE. No code or behavior change; schema stays 29.

## [0.45.1] — Session 28 — Vulnerability reporting (docs only)
### Changed
- **`SECURITY.md`** — new "Reporting a vulnerability" section at the top: use the
  repo's private **Report a vulnerability** button, not a public issue. No code
  or behavior change; schema stays 29.

## [0.45.0] — Session 28 (Stage L, part 1) — The Emergency Stop
### Added
- **`Services/EmergencyStopService`** — Exit and Kill hotkeys now live on a
  dedicated thread with its own Win32 message loop, so they are delivered even
  when the UI thread is frozen (from testing: the Pinned Windows button froze
  the app and the Exit hotkey died with it). **Kill** = one log line, then
  `TerminateProcess` — instant, unconditional. **Exit** = the normal clean
  shutdown on the UI thread, escalating to the hard stop if the UI thread does
  not start it within 3 s. Kill stays armed while the hotkey recorder is open.
- **UI watchdog** (same service): one WARN log line when the UI thread has been
  unresponsive for ≥ 4 s, one when it recovers — so a freeze leaves a timestamp
  next to whatever the log shows just before it.
- **Tray → "Kill Now — No Cleanup"** menu item (mouse-reachable hard stop).
### Changed
- **Pinned Windows scan runs off the UI thread** (`Task.Run`, results applied
  on the dispatcher, "Scanning…" placeholder, a WARN if it passes 3 s, DEBUG
  timing lines). The scan is the only foreign-process interaction that window
  does, and the one place a freeze could hide.
- `Win32.GetProcessName` uses `OpenProcess(QUERY_LIMITED)` +
  `QueryFullProcessImageName` instead of `Process.GetProcessById().ProcessName`
  (which enumerated every process on the machine per listed window).
- `RegisterHotKey` import gained `SetLastError = true` — the 1409 diagnostics
  added in K were reading a stale error code without it.
- Help (Hotkeys) and the Hotkeys editor describe the e-stop behavior.
- Schema stays **29**.

## [0.44.2] — Session 28 — License + identity cleanup (no code behavior change)
### Added
- **`LICENSE`** — GNU GPL-3.0 (verbatim FSF text; never edit it). The project
  is licensed **GPL-3.0-or-later**: anything built from it must stay open under
  the same terms.
- **README**: "An AI-developed project" section (the whole thing was produced by
  Anthropic's Claude across development sessions, directed and tested by me,
  Godswrath88) and a proper License section with the no-warranty notice.
- **Help → About & License** section (copyright, GPL notice, no-warranty line).
- **Assembly metadata** in `OnTop.csproj`: Authors/Company/Product/Copyright/
  PackageLicenseExpression, plus a **Release-only `PathMap`** so release builds
  don't embed the local build path (it contained the Windows user name and
  showed up in crash stack traces). Installer: `AppCopyright`.
### Changed
- **Every doc and code comment rewritten in first person / neutral voice.** No
  more "owner request", "the owner asked", "the assistant's container", "chat",
  "weaker model" — the author's notes are written as the author's notes.
  `MASTER_PROMPT.md` stays a prompt on purpose (it bootstraps the next session)
  and says so. `HANDOFF.md` rewritten from scratch (it was stale).
- **All personal identifiers removed**: real name (was in HANDOFF.md ×7 and the
  installer Publisher), and the personal hardware-tool name generalized.
  Publisher is now `Godswrath88`. Standing rule added: never write my Windows
  username, e-mail or machine name into any file.
- Zip contract now includes `LICENSE` (PROJECT_SCOPE zip command + self-check,
  MASTER_PROMPT, HANDOFF).
- ROADMAP: the approved **Round 3** list (Professional appearance mode, pinning
  extras, tooling, macro polish, accessibility — 31 items) is now filed at the
  top of the file; it previously existed only in a session transcript.
- Schema stays **29**. No runtime behavior changed.

## [0.44.1] — Session 28 — Repo readiness (docs/files only)
### Added
- `.gitignore` (build outputs, IDE files, logs, installer/portable outputs, and
  **real config files** — they can hold macro keystroke recordings),
  `.gitattributes` (CRLF for C#/XAML, LF for Markdown, binaries), `.editorconfig`.
- README rewritten to describe the app as it is (features, honest anti-cheat
  note, build, data location, hotkeys, document map).
### Deferred
- **LICENSE** — not added yet; I wanted to compare license types first
  (resolved in 0.44.2: GPL-3.0-or-later). Until a `LICENSE` file exists the
  code is all-rights-reserved by default even in a public repo.

## [0.44.0] — Session 28 (Stage K) — First Real Test Round: Root Causes + UX Contract
### Fixed (root causes from the 2026-09-24 test logs)
- **Zone double-clicks silently ignored** — Desktop Only used `WindowFromPoint`,
  which returned OnTop's OWN zone overlay (covers the monitor; click-through
  only while Overlays Locked). New `Win32.TopWindowAtPoint` walks the z-order
  skipping our overlays (`VM.IsOverlayWindow` ← `OverlayService.IsOverlayHwnd`);
  gated clicks now log why (verbose) / toast in Test Mode.
- **"Zones Trigger Stealth" swallowed every advanced action** — it now converts
  EXIT → Stealth only. New `Zone.AfterAction` (Nothing / then Stealth / then
  Exit) for advanced actions + context-menu submenu.
- **Accent / theme never applied** — brushes referenced by StaticResource are
  frozen when a Style seals, so mutation was a no-op; all 125 `*Brush` refs in
  XAML → `DynamicResource`, `ThemeOptions.SetToken` replaces frozen brushes.
- **Overlays under our own GUIs after a ping** — `SetWindowPos(HWND_TOPMOST)` is
  a no-op on an already-topmost window; `BringOverlaysToFront` toggles
  NOTOPMOST→TOPMOST; 400 ms re-assert while an OnTop window is foreground;
  called after every Refresh.
- **Recording contained the Ctrl+Alt+M chord** — `TrimChordEdges` strips orphan
  key-ups at the start and orphan key-downs at the end.
- **Placement window crash on reopen** — DataGrid left the shared collection's
  view in an EditItem transaction; `GridEdit.EnsureCommitted` before bind and
  on Closing (Placement + Auto-Pin).
- **"Placement applied" but nothing moved** — `SetWindowPos` result is now checked
  (`SetLastError`); error 5 = target runs elevated → honest log + toast.
  Candidates ranked (uncloaked, titled, largest) instead of first-hit.
- **Hotkey recorder executed the hotkeys it was capturing / duplicates ignored** —
  `VM.SuspendGlobalHotkeys` while any capture UI is open; live conflict refusal
  in Hotkeys…; new `HotkeyCaptureDialog` for macro/library hotkeys.
### Added
- Placement: `PlacementRuleDialog` (full editor; double-click / right-click /
  Edit Selected…), `DelaySec`, `LaunchWaitSec`, column header tooltips,
  `ProcessPickerDialog` (running-process list + Browse .exe); Auto-Pin gets Add
  From Focused Window / Pick Running….
- Macro status strip: shown only while Macros window open or during activity
  (+4 s linger); × button; `HotkeyMacroStatus`; countdown as a field (0 = off).
- Click-through: orange frame (`ClickThroughBorderWindow`, `ClickThroughBorder`),
  State column in Pinned Windows, grid refresh on toggle.
- `Helpers/EditorFooter`: Save & Close / Cancel (Revert) footers on every
  sub-GUI, title-bar ✕ hidden; Settings gains Cancel (Revert).
- Verbose logging: gesture dispatch, desktop-only gating reason, miss reason,
  placement candidates, overlay re-assert, hotkey suspend/resume, Win32 error on
  hotkey bind failure (`HotkeyService.LastError`).
- `TESTING.md` rewritten as the standing checklist (legend + how-to-test notes).
### Changed
- **Schema 28 → 29** (no-op).

## [0.43.0] — Session 28 (Stage J) — Layout & Cosmetic Round (my notes, pre-sweep)
### Changed
- **Main window action row → three cards**: Zone Actions (zone-only tools) |
  Zone Groups + a new **Macros** card | **App** (windows, tools, live state,
  profiles). The row is much shorter, so the Zones row above keeps its height
  (`MinHeight="300"` floor). Nothing moved off-screen; every button kept.
- **Help table of contents** rendered as bordered chips (background, accent
  border, rounded, hover-invert) instead of run-on links; the duplicate
  "Zone Map" section merged into one.
- **Zone map instructions** mention holding the MIDDLE mouse button to scroll
  (empty-map hint, map tooltip, welcome tour, Help).
### Added
- **Settings → Performance → Hotkey Re-Bind Retry** (`HotkeyRetrySeconds`,
  5–600 s, default 30; live — was a fixed 30 s timer).
- Standing rule in PROJECT_SCOPE: **UI grouping review** every session that
  adds controls — new controls go in the card they belong to, cards are split
  when they grow, star-sized rows must not be starved by Auto rows.
- **Schema 27 → 28** (no-op).

## [0.42.1] — Session 28 — HOTFIX: `UniformGrid` namespace
### Fixed
- CS0246 in `Views/CheatSheetWindow.cs` — `UniformGrid` is in
  `System.Windows.Controls.Primitives`, not `System.Windows.Controls`. Added the
  using. The ONLY build error across 0.37.0–0.42.0 (six versions).

## [0.42.0] — Session 28 (Stage I) — The Twenty (Second Ideas List)
### Added
- **Pinning**: Pin Scenes (`Models/PinScene`, VM `SaveScene`/`ApplyScene`, Pinned
  Windows Save/Apply Scene…, `LibraryKind.ApplyScene = 10`, palette); Pin Memory
  (`PinMemoryEnabled`, `PinMemory` map, reapplied in `TogglePin`); Timed Pin
  (`HotkeyPinTimed`, `PinTimedSeconds`, DispatcherTimer unpin); Send To Next
  Monitor (`HotkeySendToMonitor`, relative rect); tray "Last Pinned Opacity".
- **Zones**: gestures 5 Shift+double / 6 Alt+double; `Zone.CooldownSec`;
  `Zone.SchedStart/End/Days` + `IsScheduledNow`; `ZoneHitFlash`; context-menu
  Cooldown… / Schedule….
- **Macros**: Dry Run (`MacroSvc.DryRun`/`DryStep`, session-only, sends nothing);
  Pause/Resume (`HotkeyMacroPause`, `TogglePause`, timeline re-based);
  `MacroEvent.L` notes (editor Note column) + Hide Mouse Moves view toggle.
- **Profiles**: switch diff (`LastProfileDiff`, logged + in toast); Idle Rule
  (`Win32.IdleMilliseconds` via GetLastInputInfo; profile/unpin-all/stealth;
  reverts on activity); `Models/ProfileSchedule` + `Views/ProfileSchedulesWindow`
  (`ProfileSchedulesEnabled`); `VM.TickIdleAndSchedules` on the watcher tick.
- **Comfort**: welcome-tour "everything optional is off" step;
  `Views/ConfigHistoryWindow`; `Views/ToastHistoryWindow` (`ToastService.History`);
  `ThemeOptions.ApplyTheme` (Dark / High Contrast / Light, live; `UiTheme`).
- Help: five new sections; hotkey lines; Features switches; Minimal Mode covers
  Idle/Schedules/Pin Memory.
### Changed
- **Schema 26 → 27** (no-op; everything inert/unbound).

## [0.41.0] — Session 28 (Stage H) — Security Scope Recalibration + Hold Chords
### Changed (security scope — see SECURITY.md "Stage H notes")
- **Keyboard-hook rule rewritten** from "never" to LIFECYCLE (armed-only, removed
  on disarm/stealth/exit). Unlocks hold chords.
- **Global "Allow Launching" sub-gate REMOVED** (`PlacementLaunchEnabled` gone;
  old JSON field ignored). Per-rule Launch + import quarantine suffice.
- **Macro DPAPI encryption defaults ON** in installed mode (schema 26 migration
  switches existing configs on); portable mode defaults OFF; a user's explicit
  later choice is respected.
- **Honest anti-cheat wording**: SECURITY.md reference paragraph + Help ⚠ —
  Stealth/Kill stop evidence going forward only; injected input can't be
  un-injected; the guard reduces risk, never eliminates it.
### Added
- `Services/HoldChordService` (WH_KEYBOARD_LL only while `HoldChordsEnabled` AND
  a chord is set; removed in Stealth/dispose): **Hold-To-Peek**
  (`OverlayService.PeekHold`, VM `PeekHoldRequested`) and **Hold-To-Pin**
  (topmost while held, released on key-up or any modifier up). Features switch,
  two hotkey rows, Help section.
- **Repeat-until-key** — `Macro.StopKey` (polled via `GetAsyncKeyState`, no
  hook); "Stop Key…" button.
### Changed
- **Schema 25 → 26.**

## [0.40.0] — Session 28 (Stage G) — Sixteen Additions
### Added
- **Pin extras** — `NudgePinOpacity`/`SetPinOpacity` (layered alpha 20–100%),
  `ToggleClickThrough` (+ under-cursor; auto-revert on every unpin path),
  `CycleCorner`, `TogglePinUnderCursor` hotkey; Pinned Windows viewer −/+/
  Click-Through column; hotkeys `HotkeyPinUnderCursor/PinOpacityUp/Down/
  PinClickThrough/PinCycleCorner`.
- **Profile Rules** — `Models/ProfileRule`, `Views/ProfileRulesWindow`,
  `VM.TickProfileRules` (App watcher tick), `ProfileRulesEnabled`.
- **Display Profiles** — `Helpers/DisplaySignature`, `DisplayProfiles` map,
  `BindActiveProfileToDisplays` (Settings → Displays), switch in `OnDisplaysChanged`.
- **Zone gestures** — `Zone.Gesture` (double-left / middle / triple / Ctrl+double /
  double-right), hook now forwards L/M/R button-downs, `VM.OnMouseButton` +
  `HandleGesture` (replaces OnLeftClick/HandleDoubleClick), Trigger Gesture submenu.
- **Do Not Disturb** — `ToastService.SuppressRoutine` predicate (Errors always show).
- **Macro: countdown** (`MacroCountdownSec`, status strip), **wait-for-window**
  step (`MacroEvent.K=10`, `S`; editor Insert Wait For Window…; timeline re-based),
  **chaining** (`Macro.NextMacroId`, natural finish only, depth ≤ 5, no self-loop),
  **relative replay** (`AnchorTitle/Left/Top` captured at record; `Relative` flag;
  offset applied at emit).
- **Cheat sheet** (`Views/CheatSheetWindow`, `VM.AllBindings`) and **command
  palette** (`Views/CommandPaletteWindow`) hotkeys.
- **Statistics**: proportional bars under top zones + Export CSV.
- Help: five new sections; hotkey lines; Features panel switches.
### Changed
- **Schema 24 → 25** (no-op; new features inert). #8 (more zone verbs) is
  satisfied by Library kinds (Stage F).

## [0.39.0] — Session 28 (Stage F) — Action Library
### Added
- `Models/LibraryAction` + `LibraryKind` (RunCommand, KillProcess, ToggleWindow,
  RunMacro, ApplyPlacement, SwitchProfile, PauseToggle, Peek, StealthToggle,
  PinUnderCursor); `Cfg.LibraryActions`, `ActionLibraryEnabled` (default OFF).
- `Views/ActionLibraryWindow` (APP "Actions…"): New/Run/Payload…/Hotkey…/
  Rename/Delete rows, kind combo, per-kind pickers (macro / placement / profile).
- VM `RunLibraryAction(a, trigger)` — every kind dispatches to an EXISTING
  choke-point; `TogglePin(hwnd)` refactored out of the hotkey handler;
  `TogglePinUnderCursor` (WindowFromPoint → GA_ROOT). Library hotkeys bind only
  while enabled; `ZoneAction.Library = 7` + `Zone.ActionLibraryId` + Set Action
  → Library Action submenu; Minimal Mode turns it off.
- **`MainViewModel.FindHotkeyConflict`** — the single shared conflict check the
  S26 rule asked for at the second assignment surface; MacrosWindow now
  delegates to it; HotkeyDialog includes library hotkeys.
- Quarantine: powerful library actions arrive disabled + unbound;
  `ActionLibraryEnabled` never arrives on; stamp includes "L|" lines.
- Help "Action Library" (+ ⚠); Features panel switch.
### Changed
- **Schema 23 → 24** (no-op; ships inert).

## [0.38.0] — Session 28 (Stage E) — Window Placement & Launcher
### Added
- `Models/PlacementRule` (move half: process/title match, monitor, X/Y/W/H or
  Maximize, On Start; launch half: LaunchPath + per-rule LaunchEnabled),
  `Services/PlacementService` (FindWindow / ApplyAsync / ApplyAllAsync /
  CaptureForeground — SetWindowPos + ShowWindowAsync only), `Views/PlacementWindow`
  (grid, master + "Allow Launching" sub-gate w/ ⚠, Capture From Focused Window,
  Apply Selected/All), APP "Placement…" button, `HotkeyPlacement` (inert while
  feature off), `ZoneAction.ApplyPlacement = 6` + `Zone.ActionPlacementId` +
  Set Action submenu, startup apply 4 s after launch, Features panel switch.
- **Single launcher choke-point** `MainViewModel.LaunchViaShell(cmd, context)` —
  zone Run Command now routes through it; refuses during load and in Test Mode;
  always ACTION-logged + toasted. `Process.Start` site count unchanged (4).
- Quarantine: imported/outside launch paths blanked (+ `PlacementLaunchEnabled`
  never arrives on); powerful-field stamp includes launch paths.
- Win32: `GetWindowRect`, `GetAncestor`, `Get/SetLayeredWindowAttributes`,
  `GetDesktopWindow`, `SWP_NOZORDER`, `SW_MAXIMIZE`, `WS_EX_LAYERED`, `RECT`.
- Help "Window Placement & Launcher" (+ ⚠); hotkey line.
### Fixed
- `Zone.Clone()` dropped `ActionCommand`/`ActionProcess`/`ActionMacroId` — profile
  snapshots and Duplicate silently lost zone actions (since v0.31.0).
### Changed
- **Schema 22 → 23** (no-op; everything ships inert). AHK importer marked
  SCRAPPED in ROADMAP (entry retained).

## [0.37.0] — Session 28 (Stage D) — Test Feedback Round: Recording UX, Step Editor, Performance, Polish
### Added
- **Hotkey-driven recording** — `HotkeyMacroRecord` (default **Ctrl+Alt+M**, new
  binding for existing users; clearable), `MacroService.ToggleRecording`, VM
  `OnHotkeyMacroRecord` (binds only while macros enabled + profile allows).
- **Recording status bar** — `Views/MacroStatusWindow` (topmost, never-activating,
  draggable, placement "macrostatus"): READY gray / RECORDING red marquee +
  elapsed + steps / SAVED green / PLAYING blue. `ShowMacroStatusBar` toggle.
- **Step editor** — `Views/MacroEditorWindow` (double-click a macro / Steps… /
  right-click): searchable grid of every step with editable delays, Delete
  Selected, Remove Mouse Moves (delays folded forward), Save/Cancel; can only
  remove steps or change timing (verb list unchanged).
- **Known anti-cheat per-entry toggles** — `Views/AntiCheatListWindow`,
  `MacroGuardIgnored` (shows which entries are running right now).
- **Low Impact Mode** — `LowImpactMode`: BelowNormal priority + auto-pin 3→10 s
  + stealth watcher 5→15 s; live via VM `LowImpactChanged` → `App.ApplyLowImpact`
  + `WindowRuleService.ApplyLowImpact`. Settings → Performance + Help section
  (incl. why core affinity is deliberately NOT offered).
- **Amber ⚠ cautions** — `Helpers/WarnTip` for code tooltips; XAML tooltips on
  Macros…/Auto-Pin…; Help `W()` paragraphs (Macros, Zone Actions, Auto-Pin,
  Stealth, Updates); Features-panel toggles; rules master switch.
- **Help table of contents** — hyperlinked CONTENTS pill box under the subtitle;
  every `H()` registers an anchor (`BuildToc`).
- **Main window MACROS band** (own labeled row above APP): Macros…, ● Record
  Macro, ■ Stop (Ctrl+Alt+X). `RecordMacro_Click` / `PanicMacro_Click`.
- **■ Panic Stop (Ctrl+Alt+X)** button in the Macros window header.
- Welcome tour: "Your Hotkeys Right Now" (current bindings incl. record hotkey);
  Get Started / Add 4 Starter Zones open the main window (`VM.RequestMainWindow`).
### Fixed
- **Accent color never changed** — `ThemeOptions.ApplyAccent` replaced the brush
  resource, but styles had captured the original object; now mutates the shared
  brush in place → applies LIVE, no restart (RowAccentTint follows).
- **Kill switch left "Macros Enabled" looking ON** — Macros window now mirrors
  cfg → checkbox (`_syncing` guard); toast wording says how to re-arm.
- **Refuse-at-start no longer trips the kill switch** (a persistent service like
  PnkBstrA made macros unusable); kill switch = mid-run appearance only.
- **Zone overlays fell under OnTop's own topmost GUIs on peek/flash** —
  `OverlayService.BringOverlaysToFront` (HWND_TOPMOST re-insert, no activate) on
  Peek + preview.
- Macros window: GuiAlwaysOnTop binding added; size/position remembered
  ("macros"); default 940×660 so nothing clips.
### Changed
- Wording: "Deny In Profile" → "Block Macros In Profile"; Speed shown/entered as
  % (100 = as recorded); "Jitter" → "Wobble (px)" with plain explanations;
  repeat "once / N× / until stopped"; "events" → "steps".
- **Schema 21 → 22** (no-op; `HotkeyMacroRecord` defaults BOUND).

## [0.36.1] — Session 28 — HOTFIX: first confirmed-build errors (3 pre-existing + 1 warning)
### Fixed
- **CS0103/CS1061 `MacroSvc`** — `MainViewModel.MacroSvc` was assigned in the
  ctor but NEVER DECLARED (since v0.33.0). Added the property. Every macro
  call site in App / VM / MacrosWindow / SettingsDialog now resolves.
- **CS0103 `_sync`** — `LogService.TailText` locked on an undeclared field
  (since v0.29.0). Declared `private readonly object _sync`.
- **CS0136 `idx`** — `ZoneContextMenuFactory` Color-submenu loop local shadowed
  the later method-scope `idx` (since v0.29.0). Renamed to `colorIdx`.
- **CS8602** warning in `HotkeyDialog.FindConflict` (`raw` possibly null) → `(raw ?? "")`.
### Notes
- These confirm v0.33.2 never compiled; the whole 0.33–0.36 line was untested.
  This is the first build feedback on it. Nothing else changed.

## [0.36.0] — Session 28 (Stage C) — Features Panel, Minimal Mode, DPAPI Confidentiality
### Added
- **Settings → Features panel** — every non-core off-switch in one WrapPanel:
  Macros, Auto-Pin Rules, Auto-Close, Update Check, Statistics, Zone Actions,
  Stealth Auto-Watcher, Display Auto-Disable. **Minimal Mode** button
  (`MainViewModel.ApplyMinimalMode`, confirmed) flips all eight off.
- New feature switches (schema 21): `StatsEnabled` (gates `StatsService.Bump*`
  via `Enabled`), `ZoneActionsEnabled` (advanced zone actions refuse + toast),
  `StealthWatcherEnabled` (watcher tick inert).
- **Optional DPAPI at-rest encryption of macro recordings** — `EncryptMacros`
  (default OFF; toggle in the Macros window), `Macro.EncryptedEvents`,
  `SettingsService.SerializeSettings` (seals at Save/SaveNoBackup/SaveDraft/
  WriteBackup, live objects restored) + `UnsealMacros` in Validate (failed seal
  → empty + disabled + load warning). NuGet
  `System.Security.Cryptography.ProtectedData` 8.0.0.
- Help "Features & Minimal Mode"; SECURITY.md IMPLEMENTED block; ROADMAP ticks.
### Changed
- **Schema 20 → 21** (no-op migration).
- Settings dialog content wrapped in a ScrollViewer capped at the work area
  (UI layout review: the Features section pushed it past 1080p).
### Deferred
- Close motion (needs Closing interception; cosmetic, unverifiable here).

## [0.35.0] — Session 28 (Stage B) — Macro Subsystem Completion
### Added
- **Kill switch (guard layer 3)** — `MacroService.TripKillSwitch`: a guard hit
  at start or mid-run turns the master toggle OFF, unbinds every macro hotkey,
  saves, ACTION-logs, center-toasts. `MacroKillSwitch` (default ON).
- **Known anti-cheat list** — `MacroService.KnownAntiCheat` folded into the
  guard behind `MacroGuardKnownAntiCheat` (default ON); `FindRunningWatched` public.
- **Per-profile macro policy** — `Profile.MacroPolicy` (Allow/Deny), "Deny In
  Profile" toggle, VM `ActiveProfileMacroPolicy`; DENY = no binds, refuse
  play/record, stop on switch. Carried across `SyncActiveProfile`.
- **Always-visible indicator** — tray badge (red recording / blue playing via
  `BadgeVariant`), tray tooltip, status bar; `MacroSvc.StateChanged` wired in App.
- **Literal-keystroke warning** — banner + one-time confirm (`MacroWarningAcknowledged`).
- **Actions-only capture** — `MacroRecordPath` (default ON = full path).
- **Humanizing jitter** — `Macro.Jitter` (0–20 px), applied at emit, clamped.
- **Export... / Import...** in the Macros window (quarantined import via
  `ImportReviewWindow`; restored hotkeys re-conflict-checked).
- **Macros Window hotkey** — `HotkeyMacros` (OPTIONAL — ACTIONS row), VM
  `MacrosRequested`, tray "Macros (hk)" item, `MainWindow.ShowMacros()`.
- Help "Macros" rewritten (three paragraphs); SECURITY.md "Macro Module — Stage
  B additions"; six ROADMAP End Goal items ticked.
### Changed
- **Schema 19 → 20** (no-op migration; all new defaults = previous behavior).
- Macros window default 720×560 (was 640×460) for the options row + banner.

## [0.34.0] — Session 28 (Stage A) — Config Integrity Hardening (code) + Stage Ledger
### Added
- **Import quarantine + review** — `Models/QuarantineItem`, `Views/ImportReviewWindow`,
  `SettingsService.QuarantineZones/QuarantineSettings/QuarantineMacros`, `IsOwnPath`
  (provenance: own config dir = trusted). Zone imports and restores from outside
  paths land INERT; per-item verbatim opt-in; own backups never re-prompted.
- **Inert-load guard** — `SettingsService.IsLoading`; `RunZoneCommand` /
  `KillZoneProcess` / `ToggleZoneWindow` / `MacroService.Play/StartRecording`
  refuse + ERROR-log during any load (standing regression check, runtime form).
- **Macro event range validation** — shared `ClampMacros` (kind/coord/wheel/VK/T).
- **Powerful-field change notice** — `AppSettings.PowerfulFieldsStamp` (schema 19).
- **Updater URL re-validated on load** (`IsValidUpdateUrl`); `LoadWarnings` surfaced.
- `ImportMacrosFile` / `ExportMacrosFile` (service side; UI lands in Stage B).
- `SESSION_STATE.md` stage checkpoint ledger (resume/rollback contract) + baseline
  zip `OnTopWPF_v0.33.8_baseline.zip`; MASTER_PROMPT/PROJECT_SCOPE/HANDOFF reference it.
- Help section "Imports, Restores & Config Safety"; SECURITY.md "Config Integrity
  Hardening — IMPLEMENTED".
### Fixed
- `ReadSettingsFile` (Settings → Restore) skipped both the 4 MB size gate and
  `Validate()` — closed.
- CHANGELOG head had 27 stray `---` separators from successive prepends — collapsed.
### Changed
- **Schema 18 → 19** (no-op migration; `PowerfulFieldsStamp` empty on old files).

## [0.33.8] — Session 27 (docs) — Safety Gate For Future Macro Capabilities
### Docs
- **SECURITY.md "Future Capability Expansion — the safety gate"** standing rule
  governing any future scripting/launch/file/network capability: bounded verb
  list over a script engine; strippable engine only if ever embedded;
  capabilities granted per-macro and never self-granted by imported content;
  **no install / download-then-execute ever**; reuse of the single launcher
  choke-point; nothing auto-runs on load or import; documented multi-process
  revisit triggers + the correct form (AppContainer/low-integrity child +
  broker) if it ever is revisited.
- ROADMAP: macro End Goal remainder (condition loops etc.) explicitly bound to
  the gate.

## [0.33.7] — Session 27 (docs) — Document-Every-Change Rule + Export/Sharing Fix
### Docs
- **PROJECT_SCOPE + MASTER_PROMPT: "DOCUMENT EVERY CHANGE" standing rule** —
  every delivery (including trivial/docs-only) requires CHANGELOG + WHATS_NEW +
  PROJECT_SCOPE entries, with a pre-zip version-consistency check across
  csproj / .iss / CHANGELOG / WHATS_NEW / PROJECT_SCOPE.
- **Backfilled CHANGELOG entries for 0.33.3–0.33.6** (docs-only versions that
  had been omitted — the gap that prompted the rule).
- **SECURITY.md + ROADMAP: at-rest encryption must never block sharing** —
  profiles/layouts/hotkeys/settings are never encrypted; export is a separate
  deliberate path (decrypt locally → portable artifact) with macros excluded by
  default behind an explicit opt-in + literal-keystroke warning.

## [0.33.6] — Session 27 (docs) — Provenance Exemption + Confidentiality Correction
### Docs
- Config quarantine applies ONLY to explicit outside imports; app's own
  settings/backups are trusted → crash/corruption fallback loads fully active
  and seamless (no re-approval). SECURITY.md + ROADMAP.
- Confidentiality vs tamper-proofing distinction: sensitive data (window titles,
  paths, macro keystroke recordings) → optional DPAPI at-rest encryption of the
  high-risk subset (per-user, login-tied, no stored key); NOT for authority.

## [0.33.5] — Session 27 (docs) — Shared-Config Safety (Quarantine, Not Encryption)
### Docs
- Encryption/signing rejected for tamper-proofing a shared config (hostile
  author signs with own key; danger is content not authority). SECURITY.md
  subsection + ROADMAP deliverable: quarantine powerful content on import
  (inert until user reviews a verbatim summary + opts each in).

## [0.33.4] — Session 27 (docs) — Config & Trusted-Data Integrity
### Docs
- SECURITY.md standing rule: nothing dangerous auto-executes on load; validate
  on the way in; re-assert trust at load; powerful fields visible; imports
  untrusted; no integrity theatre. ROADMAP: config integrity hardening pass.

## [0.33.3] — Session 27 (docs) — Window-Mover Privilege Compartmentalization
### Docs
- SECURITY.md "Internal Capability Isolation" standing rule + window-mover
  security requirements (single choke-point, config-in-not-call-in, move≠launch
  split, reuse guarded ShellExecute). ROADMAP: Window Placement & Launcher
  feature + STANDING DESIGN LAW (every non-core feature fully disableable).

## [0.33.2] — Session 27 (hotfix) — Duplicate-Definition Build Fix + Roadmap

### Fixed
- **CS0102** — `HotkeyStealth` + backing field defined twice in AppSettings
  (pre-existing + S26 duplicate); removed the duplicate. Full duplicate-member
  sweep of AppSettings and the large service/VM files — no others found.

### Docs
- ROADMAP: **Window Placement & Launcher** feature (start/move windows to set
  positions on startup/hotkey/zone). **Standing Design Law** —
  every non-core feature must be fully disableable (anti-cheat + user autonomy);
  mirrored in SECURITY.md.

## [0.33.1] — Session 27 (hotfix) — Build Fix + Macro Playback Correctness

### Fixed
- **DarkTheme.xaml MC3088 build error** — the S25 open-motion `<Style.Triggers>`
  block sat mid-Setters in DarkChromeWindow (property elements can't interrupt
  content); moved to the end of the style. XAML validates; open motion intact.
- **Macro wheel playback** — scroll events no longer carry a spurious absolute
  MOVE (wheel now scrolls at the current position; buttons still re-anchor to
  their recorded spot). Found in a post-ship audit of the macro core.

## [0.33.0] — Session 27 — Macro Subsystem Core + Welcome-Tour Re-Show + Soft Restart

### Added
- **Macro module** (roadmap End Goal core): Models/Macro + MacroEvent;
  Services/MacroService (record via WH_KEYBOARD_LL+WH_MOUSE_LL installed ONLY
  while recording; SendInput playback, absolute drift-free pacing, speed,
  repeat/loop; anti-cheat guard = watched-process refuse-on-start + abort
  mid-run; Ctrl+Alt+X panic registered only during playback); Views/MacrosWindow
  (master toggle, record/stop/discard, per-macro play/hotkey/speed/repeat/
  rename/delete); Win32 SendInput+INPUT/MOUSEINPUT/KEYBDINPUT+hook helpers;
  zone RunMacro action + Set Action → Run Macro submenu; per-macro hotkeys
  (play/stop, conflict-checked incl. reserved Ctrl+Alt+X); SECURITY.md
  "Macro Module" section. Master toggle ships OFF (dark). Caps 50/20000.
- **Welcome-tour re-show** checkbox + **Restart OnTop** button (Settings →
  Maintenance); MainViewModel.RequestRestart() public path.

### Schema
- v17 → **v18**: MacrosEnabled, Macros[]; Zone.ActionMacroId, ZoneAction.RunMacro.

## [0.32.0] — Session 26 (cont.) — Hotkey Conflict Prevention

### Added
- Hotkeys editor refuses duplicate bindings at save (normalized compare across
  all named hotkeys + optional toggles + built-in Ctrl+Alt+P; warning names
  both colliding features; dialog stays open). Toggle rows carry labels for
  the message. Roadmap: standing one-combo-one-feature rule binding the future
  Action Library + macro hotkeys to the same central check.

## [0.31.0] — Session 26 — Per-Zone Actions + Stealth Follow-Ups

### Added
- **Per-zone actions**: ZoneAction RunCommand/KillProcess/ToggleWindow (int
  enum, append-safe) + Zone.ActionCommand/ActionProcess; VM execution engine
  (ShellExecute non-elevated; protected-process kill refusal; toggle via
  IsIconic/ShowWindowAsync/SetForegroundWindow — added to Win32); "Set Action"
  context submenu showing params verbatim; ClampZones caps (500/100) + enum
  range check; SECURITY.md "Per-Zone Actions" section; Test Mode suppression
  inherited.
- **Stealth follow-ups**: HotkeyStealth (bind/unregister wired), auto-stealth
  process watcher (App, 5 s; auto-releases only self-engaged stealth),
  HideOverlaysInStealth (default true; OverlayService reacts to
  StealthMode/flag), Settings "Stealth" section.
- Roadmap: AHK importer marked OPTIONAL.

### Schema
- v16 → **v17**: HotkeyStealth, StealthWatchedProcesses, HideOverlaysInStealth.

## [0.30.0] — Session 25 — Visual Cohesion Pass + Portable Mode

### Added
- Design tokens (BgBand/WarnAmber/RowDangerTint/RowAccentTint/Radius*) +
  straggler conversion (MainWindow, ZoneListWindow, HotkeyDialog).
- FocusRing style on all interactive controls; uniform disabled states (9
  styles); 150 ms open motion on DarkChromeWindow (close motion deferred).
- **Accent picker** (6 accents; startup-applied, restart to change) — schema
  v15 → **v16** (AccentIndex).
- **Win10 acrylic fallback** for the Mica toggle (AccentPolicy blur-behind).
- **Portable mode**: `OnTop.portable` marker or `--portable` → data in
  `<exe>\Data` (SettingsService.IsPortableMode).
- Overshadowed-control audit (new dim: startup-update toggle while URL empty);
  own-window hook-guard audit (pid-level confirmed correct, closed); logging
  second pass verified. Roadmap: pre-macro final QA pass entry added.

## [0.29.0] — Session 24 — Magnet v6 Root Cause, Per-Monitor Overlays, Tray Persistence + More

### Fixed
- **Magnet v6** — DragDelta-relative-to-thumb feedback loop eliminated via
  absolute cursor sampling against the (newly named) SplitHost grid.
- **Overlay geometry on mixed-DPI multi-monitor** — one overlay window PER
  MONITOR (PerMonitorV2 per-window DPI), OnDpiChanged re-flow, topology-change
  rebuild; OverlayService Distribute() splits zones by screen.

### Added
- **Tray menu persistence** (Settings toggle; StaysOpenOnClick + 0.6 s
  leave-buffer via PreviewMouseMove timestamp; live profile checkmarks).
- **Zone drag-preview overlay** (live while dragging, 3 s linger; canvas
  ZonePreviewRequested/ZoneDragCompleted → VM events → OverlayService).
- **Welcome tour** + AddStarterZones (fresh installs only; migration marks
  existing configs complete).
- **Data safety**: log-tail.txt in bundles (LogService.TailText), pre-import
  auto-backup, backup pruning (keep 10).
- DataGrid focus-loss commit (GridEdit.AttachFocusCommit, both lists);
  startup-shortcut self-repair (explicit + logged); hook latency EMA warn;
  per-zone Color submenu (ZonePalette).

### Schema
- v14 → **v15**: TrayMenuStaysOpen, FirstRunCompleted (existing → true).

## [0.28.0] — Session 23 — Crash Reports, Updater Stage One, Test Mode + 4 Small

### Added
- **Crash-report prompt** (once/session, comment → diag.txt, Config\Crashes;
  fatal = auto-bundle). Shared `Services/DiagnosticsService` (viewer export
  delegates to it).
- **Updater stage one** — `Services/UpdateService` + 3-way
  `Views/UpdatePromptDialog`; Settings → Updates (URL empty = dormant); HTTPS
  both directions, 64 KB cap, 10 s timeout, downgrade refusal, Updates-category
  logging; browser handoff only. SECURITY.md network rule amended (stage two
  still gated on signing). `installer/UPDATES.md` documents the manifest.
- **Test Mode** — session-only VM toggle; HIT/MISS feedback, actions and stats
  untouched.
- Tray **paused badge** (drawn amber dot; pause event centralized in the
  DetectionPaused setter). Map **edge/corner snapping** (Shift bypasses).
  Slider **value bubbles** (AutoToolTip). Uninstaller **DelTree path guard**.
- `portable/` — self-contained single-exe build script + trade-off doc; both
  release artifacts now labeled top-level and independently buildable.

### Schema
- v13 → **v14**: UpdateManifestUrl, UpdateCheckOnStartup, IgnoredUpdateVersion.

## [0.27.0] — Session 22 — Profile-Delete Fix, Magnet v5, Taskbar Semantics v2, UI Un-Clipping

### Fixed
- **Delete-active-profile no-op** — stale reference after SyncActiveProfile's
  record replacement; now name-based RemoveAll post-switch.
- **Magnet v5** — flat hold widened to ±2.5% (feelable mid-drag), curve
  continuous at both well edges, release-settle only from inside the hold.
- **APP section + log-viewer category clipping** — two-row APP layout with
  wrap-row margins; category strip rehosted (nested WrapPanel never wraps).
- **Map zone scale** — 9 px minimum visual half-extent so boxes never hide
  under the center dot at multi-monitor zoom.

### Changed
- **Taskbar semantics** — window always in taskbar while open; setting is now
  **MinimizeToTray** (migrated from old ShowInTaskbar=false); the runtime
  style-flip machinery is deleted entirely.
- Sub-GUIs now ShowInTaskbar=true (taskbar hover navigation).

### Added
- Workflow: mandatory **UI LAYOUT REVIEW** pre-ship step + extension-method
  using-check. Roadmap: crash/issue report prompt (queued).

### Schema
- v12 → **v13**: MinimizeToTray (+ ShowInTaskbar=false migration).

## [0.26.0] — Session 21 (cont.) — Installer Package + Activity Feed

### Added
- **Installer package** — `installer/OnTop.iss` (Inno Setup 6) + BUILD.md:
  wizard with shortcut/startup/location options, run-on-finish, .NET 8 Desktop
  Runtime auto-bootstrap, per-user no-UAC default, data-preserving uninstall.
  Ships ready-to-compile (I build it with Inno Setup; test checklist included).
- **Recent-activity feed** — Statistics window, last 25 of a 300-entry rolling
  feed (triggers/pins/unpins/auto-pins/peeks/profile switches, timestamped),
  persisted in stats.json, validated, cleared by Reset.

### Docs
- Zip contract now includes `installer/`.

## [0.25.0] — Session 21 — Roadmap Sweep: Display Awareness, Resume Robustness, Multi-Select, Diagnostics & Security

### Added
- **Monitor-configuration awareness** — health + map refresh on display changes;
  opt-in auto-disable of off-screen zones (Settings → Displays) with toast + log.
- **Sleep/resume + session-unlock re-assert** — hook, hotkeys, pinned-window
  topmost, GUI topmost; SystemEvents subscribed/unsubscribed properly.
- **Multi-select bulk zone ops** — Delete + right-click Remove/Set Group/Enable/
  Disable "Selected (n)", each ONE undo step (VM RemoveZones/SetGroupFor/
  SetEnabledFor; selection-preserving context menu).
- **Diagnostics bundle export** — Logs toolbar; zip of settings/stats/current
  log/diag.txt (LogService.CurrentLogFile exposed).
- **Elevated-launch warning** (toast + log) and **import hardening** (4 MB gate +
  shared ClampZones for config validate AND zone imports).

### Schema
- v11 → **v12**: AutoDisableOffscreenZones (default false).

## [0.24.0] — Session 20 (cont. 4) — STATISTICS

### Added
- **Statistics window** (APP → Stats...): session uptime/triggers/pins; lifetime
  launches, runtime, triggers (+stealth), pins/unpins, rule pins, peeks, profile
  switches; top-5 most-triggered zones (Id-tracked, deletion-aware); confirmed
  Reset (counters only). `Models/AppStats`, `Services/StatsService` (stats.json:
  size-gated, validated, atomic; 60s dirty-flush; runtime folded on exit),
  `Views/StatisticsWindow` ("stats" placement). No hotkey by decision.

### Schema
- Settings schema unchanged (stats persist in their own stats.json).

## [0.23.0] — Session 20 (cont. 3) — Taskbar Fix, Magnet v4, Logging Audit & PROFILES

### Fixed
- **Taskbar toggle broke the GUI** — ShowInTaskbar now code-managed via a safe
  hide→set→show cycle (+ Mica/Topmost re-assert); live XAML binding removed.
- **Split magnet v4** — stateless gravity-well curve (continuous everywhere, exact
  hold ±1.2%, attraction to ±7%); replaces the glitching lock/unlock state machine.

### Added
- **PROFILES** — named zone workspaces (zones/exceptions/triggers/sliders); combo +
  manager window + tray submenu + optional cycle hotkey; loss-proof switching with
  deep clones; first run wraps the workspace as "Default". `Models/Profile`,
  `Views/ProfilesWindow`, VM engine, "profiles" placement.
- **Logging audit (first pass complete)** — zero silent catches (log or justified),
  `SettingsService.AttachLogger`, Config category; KnownCategories extended
  (Zones/Sliders/Pins/Rules/Config/Profiles/Updates).

### Schema
- v10 → **v11**: Profiles + ActiveProfile + HotkeyProfiles (no-op migration).

## [0.22.0] — Session 20 (cont. 2) — Feedback Round: Magnet v3, Auto-Pin Rename, GUI Cohesion, Taskbar & Hotkey Editor

### Fixed
- **Split magnet v3** — unlock continues from center (no release-edge hop), re-arm
  hysteresis prevents instant re-grab, pixel-snapped widths + skipped no-op writes
  kill the shimmer; bands ±3% / ±6%.
- **Hotkey editor clipping** — label column 182→200px, labels wrap, window 520→560.

### Changed
- **"Rules" → "Auto-Pin"** in every user-facing string (button, window, tray,
  hotkey row, Help). Code identifiers unchanged.
- Hotkey editor reorganized: **REQUIRED / OPTIONAL — ACTIONS / OPTIONAL — TOGGLES**.

### Added
- Main-GUI cohesion: **Peek**, **Legend**, and **Paused** (toggle) in the APP section.
- **Taskbar icon** (Settings toggle, default on); minimize-to-tray only when off.
- Standing requirements: Help/tooltips updated same-session; every feature decides
  its hotkey story (required/optional); main-GUI cohesion rule.

### Schema
- v9 → **v10**: ShowInTaskbar (default true).

## [0.21.0] — Session 20 (cont.) — Zone Peek Hotkey + Rules Hotkey

### Added
- **Peek At All Zones** — optional hotkey flashes every zone (incl. hidden-dot and
  disabled) on the desktop overlay at full opacity for ~1.5s (`OverlayService.Peek`).
  Press-to-peek by design: a hold would need a keyboard hook (refused per SECURITY.md).
- **Window Rules hotkey + tray item** (parity with Pinned Windows).

### Schema
- v8 → **v9**: HotkeyPeek + HotkeyRules (no-op migration).

### Docs
- ROADMAP: 🔺 MAJOR online update checker/updater added (3-way prompt UX,
  Settings check button, security-first requirements, ordered after code
  signing; every update-path event logged under an "Updates" category);
  SECURITY.md "no network" rule gains the matching planned-exception.
- ROADMAP: 🔺 HIGH logging completeness audit added (sweep all silent catches +
  every command path; per-category filter checkboxes in the Log Viewer).

## [0.20.0] — Session 20 — Security-First Pass, Config Trust Fixes, Window Rules & Feedback Batch

### Security (new top priority — see SECURITY.md)
- `SECURITY.md` added (threat model, standing rules, per-session checklist);
  workflow + master prompt updated. app.manifest pins `asInvoker` + PerMonitorV2.
- Config hardening: 4 MB parse gate, full value validation/clamps on load.
- Window titles sanitized (control chars stripped, length capped) — log-forging fix.
- Mouse-hook callback exception-guarded; own-window immunity in all foreign-window paths.

### Fixed
- **Config prune too aggressive** — newer-schema configs now load best-effort
  instead of resetting to defaults (root cause of placement loss across versions).
- **Column widths persist** (main/list/pins/rules grids; new `Helpers/GridColumns`,
  `AppSettings.ColumnWidths`); pins grid gets sane initial widths.
- **Split magnet** — plain Thumb + lock/hysteresis/re-base state machine (was
  GridSplitter fighting the handler at center).
- Pinned viewer: "Other" label for external pins; "Unpin All (OnTop Pins)" rename;
  viewer re-asserts topmost after bulk ops.
- Pin/unpin/unpin-all toasts added (never existed).

### Added
- **Window Rules (auto-pin)** — APP → Rules...; process/title contains-pattern
  rules, master switch off by default, 3s scanner, hand-unpin wins, rule pins are
  OnTop pins. New `Models/WindowRule`, `Services/WindowRuleService`,
  `Views/WindowRulesWindow`.
- **Config conflict prompts** — plain-language Continue/Cancel at startup
  (`Models/ConfigConflict`, wired in App.OnStartup).
- Placement memory for Help / Auto-Close / Exceptions / Overlay Legend.

### Schema
- v7 → **v8**: ColumnWidths + WindowRules + WindowRulesEnabled (no-op migration).

## [0.19.0] — Session 19 — Pinned Windows Viewer, Master Prompt & Split-Magnet Fix

### Added
- **Pinned Windows viewer** — all topmost windows with OnTop-pin badges; per-row
  Unpin, Unpin All (Ours), Refresh; APP button + tray item + optional hotkey; pin
  tracking in the VM; window-enumeration Win32 helpers.
- **MASTER_PROMPT.md** — copy-paste new-session bootstrap; now part of the zip contract.

### Fixed
- **Split magnet acts during the drag** (cumulative-distance hold at center) instead
  of only on release.

### Schema
- v6 → **v7**: `HotkeyPins` + "pins" placement key (no-op migration).

## [0.18.0] — Session 18 — Log-Driven Fixes, Split Divider, Map Context Menu & Zone Search

### Fixed
- **30s "Setting changed: AutoCloseTriggers" spam** — auto-draft only reassigns lists
  when contents changed; bulk-sync list props excluded from the settings log; logged
  lists print contents.
- **Log Follow** — scroll deferred past layout so it actually tracks the newest entry;
  re-check jumps to end; log toolbar tooltips added.

### Added
- **Map/list split divider** — drag to resize either pane; light snap back to the
  default ratio when released near it; MinWidths guard both panes.
- **Blank-map context menu** — one-click Show Monitor N / Show All (primary unhide
  path), Fit / Reset View, Flash All Zones; embedded + pop-out maps.
- **Zone list search box** — live name/group filter, composes with the group dropdown.
- **Log Viewer copy** — multi-select + Ctrl+C / right-click Copy Selected / Copy All.

### Notes
- No schema change.

## [0.17.3] — Build Fix

### Fixed
- `RecomputeZoneHealth` called `Math.Round` on `Zone.X`/`Y`, which are already `int`
  (CS0121 ambiguous-overload). Rounding removed — the ints are used directly.

## [0.17.2] — Build Fix

### Fixed
- Removed a duplicate `ZoneMapCanvas.ResetView` (0.17.1 didn't compile — CS0111).
  The pre-existing `ResetView` (used by the map's "Reset Zoom / Pan" context item and
  the S17 Fit button) is kept.

## [0.17.1] — Session 17 (cont.) — Adaptive Monitor Header + Docs Audit

### Changed
- Monitor column header adapts: **"Monitor"** normally, **"Mon"** when narrowed
  (`Helpers/WidthLabelConverter`). Both lists.

### Docs
- Documentation audit: Feature Inventory brought current through S17; corrected
  Not Yet Built (dropped shipped items, surfaced the Macro end goal); fixed the
  Session Workflow zip command (CHANGELOG + excludes); expanded the Architecture Map.

## [0.17.0] — Session 17 — Map Fit / Flash-All Buttons + Monitor Column

### Added
- **Fit / Reset View** button on the map toolbar (`ZoneMapCanvas.ResetView`).
- **Flash All Zones** button — pulses every zone on the map at once
  (`ZoneMapCanvas.FlashAllZones`).
- **"Mon" column** in both zone lists — 1-based monitor number, live-updating.

### Notes
- No schema change (monitor column is computed).

## [0.16.0] — Session 16 — Test-Feedback Fixes + Nudge / Duplicate-to-Monitor / Copy Details

### Fixed
- **Startup status toast** — summarizes enabled/total zones + paused/stealth state on launch.
- **Locate is a single click** — leftmost list cell is clickable on every row (dim ◎ /
  amber ⚠ when flagged); click flashes the zone. Double-click still works.
- **Off-screen detection uses real monitor rects** — catches zones stranded in gaps
  between monitors (was virtual-desktop bounding box).
- **Re-Enable Mouse Hook is a bindable hotkey** — was hardcoded/hidden; now an editor
  row, still defaulting to Ctrl+Alt+R.

### Added
- **Ctrl+Arrow nudge** for the selected zone (Shift = 10px), undoable.
- **Duplicate To Monitor** — copy a zone to the same relative spot on another monitor
  (multi-monitor only).
- **Copy Details** — zone specs to the clipboard from the context menu.

### Schema
- v5 → **v6**: bindable `HotkeyRehook`; no-op migration (defaults to Ctrl+Alt+R).

## [0.15.0] — Session 15 — Reorder, Locate, Health Badges, Row Accent & CLI Flags

### Added
- **Zone priority reorder** — Move Up / Move Down in the zone context menu (undoable);
  detection is first-match by list order.
- **Locate** — double-click a list row flashes the zone on every open map.
- **Zone health badges** — ⚠ in the list for off-screen / no-hit-area / duplicate-name
  zones, with a tooltip; recomputed live. Both lists.
- **Selected-row accent bar** — blue left bar on the selected DataGrid row.
- **Command-line flags** — `--paused` (start paused) and `--minimized` (start in tray).

### Notes
- No schema change.

## [0.14.0] — Session 14 — Tray Quick Record, Override Cues & List Shortcuts

### Added
- **Tray Quick Record** — capture a zone in the current mode from the tray without
  opening the window (last remaining v9.5-parity tray action).
- **Action-chip override cue** — when Zones Trigger Stealth is on, the per-zone
  Action chips (main + pop-out list) dim and disable, with an explanatory tooltip.
- **Delete / F2 in the zone list** — Delete removes the selected zone (undoable),
  F2 renames it; both ignored while editing a cell. Shared `Views/ZoneListKeys`;
  `CanUserDeleteRows` disabled so deletes go through the undo-aware path.

### Notes
- No schema change.

## [0.13.2] — Session 13 (cont.) — Follow-Up Fixes

### Fixed
- **Tray context menu now dismisses** on selection/click-away — force the main
  window to the foreground before opening (Win32 KB135788: a menu won't
  light-dismiss unless its owner window is foreground; the tray icon is WinForms).
- **Map double-click zone creation now repaints immediately** — added an explicit
  `InvalidateVisual` after the create (Record-made zones repainted via the window
  re-showing; a map double-click keeps the window visible).

## [0.13.1] — Session 13 (cont.) — Follow-Up Fixes

### Fixed
- **Window size persistence** — capture size from `ActualWidth/ActualHeight`
  (custom chrome leaves `Width/Height` at XAML defaults on drag-resize), so the
  real size survives a restart.
- **Grid click-out** — clicking the empty area of the list (not just outside it)
  now commits an in-progress edit.
- **Tray submenus** — renamed the sub-popup template part to `PART_Popup` so
  WPF's MenuItem manages open/close; submenus no longer stay stuck open.

### Changed
- Renamed the **"Dot" list column to "Overlay"** (main + pop-out) and updated Help.

## [0.13.0] — Session 13 — Bug-Fix Pass + Config Relocation

### Fixed
- **Window size/position now persists** — tracked live on resize/move and written
  immediately on close (new `SettingsService.SaveNoBackup`), not only at app exit;
  "Save and Close" captures the current size.
- **Grid click-out commit** — now coordinate-based, so clicking outside the grid
  reliably commits an edit (was misfiring under mouse capture).
- **Tray submenus** ("Pause For", "Zones") now open — the themed MenuItem template
  had no sub-popup; rewrote it with submenu support.
- **Overlay opacity** now applies to the overlay content root (Window.Opacity
  wasn't repainting the click-through layer), so the slider has visible effect.
- **Zone list right-click** — list rows now open the same context menu as the map.

### Changed
- **Config moved to `%APPDATA%\OnTop`** (`config\`, `logs\`, `assets\`) from
  beside-the-exe, with one-time migration of an existing beside-exe config and
  pruning of newer-than-supported configs. Legacy location and shipped-icon
  fallback remain honored (documented as must-maintain in PROJECT_SCOPE).

### Notes
- No schema change (still v5).

## [0.12.0] — Session 12 — Grid-Edit Commit, Timed Pause & Per-Zone Tray Toggles

### Added
- **Click-out commits a zone-list edit** — clicking anywhere outside the grid
  commits an in-progress cell edit (main list and pop-out list). New reusable
  `Helpers/GridEdit.cs`.
- **Timed pause** in the tray — Pause For 15 min / 30 min / 1 hour / 2 hours,
  auto-resumes with a toast; the Resume item shows remaining time; resuming early
  (tray, hotkey, or manual) cancels the timer.
- **Per-zone tray toggles** — a Zones (N) submenu flips any single zone on/off
  (✔ marks enabled), alongside Enable/Disable All.
- Help gained a System Tray section.

## [0.11.0] — Session 11 — Distinguishable Pop-Out Icons & Window Placement Memory

### Added
- **Window size/position memory** for the main window and all three pop-outs
  (map, list, logs) — restored on open, clamped onto a visible monitor so a
  window saved on a since-removed display can't open off-screen. Maximized state
  preserved. New reusable `Helpers/WindowPlacement.cs`; new `WindowRect` DTO +
  `AppSettings.WindowPlacements`. Placements update in memory on close and flush
  to disk once at app exit (avoids churning the .bak backup chain).

### Changed
- **Pop-out buttons are now visually distinct** — a folded-map icon and a
  bulleted-list icon, each with a pop-out arrow, replacing the two identical
  Segoe MDL2 glyphs.
- `App.OnExit` now flushes real settings on normal exit.
- Schema bumped to v5 (forward-only, no-op — empty placements dict by default).

## [0.10.0] — Session 10 — Zone Actions Reorg & Remaining Action Hotkeys

### Added
- **Quick Record** hotkey (optional) — record a zone in the current mode without
  opening the main window; auto-names it and only restores the window if it was
  already open.
- **View Logs** hotkey (optional) — open the live Log Viewer from anywhere.
- **Soft Restart** hotkey (optional) — save and relaunch; the new instance waits
  for the single-instance mutex to free (via a `--restart` arg) for a clean handoff.
- All three sit in the Hotkeys dialog's Actions section and share the 30s
  bind-retry loop.

### Changed
- **Zone Actions panel regrouped** from three arbitrary rows into four labeled
  subsections — RECORD & EDIT (mode selector beside Record, then Duplicate /
  Remove), ZONE DATA (Import / Export), CONFIGURE (Hotkeys / Exceptions /
  Auto-Close / Settings), APP (Logs / Help). No behavior change.
- Recording refactored into one `StartRecording(quick)` method behind both the
  Record button and Quick Record.
- Schema bumped to v4 (forward-only, no-op — new hotkeys default unbound).

## [0.9.0] — Session 9 — Recording Modes, Draw Zones & Per-Toggle Hotkeys

### Added
- **Freeform Draw zones** (v9.5 parity): sketch any shape; detection is a
  real point-in-polygon ray cast, not a box. Self-intersection lockout, live
  polygon rendering on the map / overlays / legend, bounding-box resize handles
  that scale the whole shape from a drag-start snapshot.
- **Drag Box recording**: drag a rectangle, zone sized 1:1 (Tolerance and
  Variation forced to 0).
- **Recording-mode selector** next to Record (Point / Drag Box / Freeform Draw),
  persisted as `AppSettings.RecordMode`, with live 1 / 2 / 3 / Tab switching and
  a mode legend during capture.
- **Redraw Shape…** context-menu entry (Draw zones only) — re-sketch a polygon
  in place; name / group / color / action / enabled all survive.
- **Zone Type column** in both zone lists (Point / Box / Draw).
- **Optional hotkey for every main-GUI toggle** (Desktop Only, Debug Overlay,
  Apply To All, Override Exceptions, Confirm Before Exit, Lock Overlays, Keep
  GUIs On Top, Stealth, Zones Trigger Stealth) — all default-unbound, recorded
  in a new Toggles section of the Hotkeys dialog; a press flips the setting and
  toasts the new state. Shares the existing 30-second bind-retry loop.
- **App version stamp** — `<Version>` in the csproj, read at runtime and shown
  in the main-window subtitle and the Help header (no hard-coded string).
- **CHANGELOG.md** (this file) — versioned fast-scan index.

### Changed
- Sliders no longer touch Draw zones (their detection is the polygon, not the
  Size/Tolerance/Variation box); schema bumped to v3 (forward-only, no rewrite).

## [0.8.0] — Session 8 — Continuity Doc, Themed Tray & Main-GUI Fixes

### Added
- **PROJECT_SCOPE.md** cross-session contract; shipped in every zip.
- Themed WPF tray menu (with Re-Enable Mouse Hook + live stealth toggle),
  Ctrl+Alt+R re-hook hotkey, map zoom/pan, monitor hide/unhide, expanded Help.

### Changed
- Split view is now vertical (map left, list right).

### Fixed
- Dead caption glyph buttons (implicit-TextBlock FontFamily leak) and close /
  minimize wiring; minimize now hides to tray. Tray icon loads the shipped
  `thumbtack.ico`. NRE in the MainWindow constructor (VM read before DataContext).

## [0.7.0] — Session 7 — Zone-Triggered Stealth

### Added
- Per-zone **Action** (Exit / Stealth) with an Action chip column and map entry,
  plus the global **Zones Trigger Stealth** override. Stealth triggers skip
  Confirm Before Exit.

### Changed
- Dark scrollbars and dark tooltips replace the OS-light ones.

## [0.6.0] — Session 6 — Stealth Mode + Stability

### Added
- **Stealth Mode** — fully removes the global mouse hook (anti-cheat safe),
  manual toggle in the panel + tray, persisted, watchdog-aware.
- Backup auto-fallback on corrupt settings, 30s hotkey retry loop, atomic
  settings writes, startup grace period, safety backup before restore.

### Changed
- Hotkey dialog labels binds as (Optional) / (Required).

## [0.5.0] — Session 5 — Feedback Pass + Live Log Viewer

### Added
- Live **Log Viewer** (categories / verbose / search / follow), capture-record
  with naming, Map/List/Split view modes with synced pop-outs, map context menu,
  transfer chevrons, GUIs-on-top toggle, installed tray icon.

*(v9.5 source confirmed lost here — everything below was reconstructed.)*

## [0.4.0] — Session 4 — v9.5 Parity, Phase 1

### Added
- Slider lock, Settings dialog, timestamped backups + restore, two-zone toasts,
  record-time naming prompt, pre-build taskkill.

## [0.1.0 – 0.3.0] — Sessions 1–3 — Foundation

### Added
- AHK→WPF migration; single-instance mutex, crash handlers, hook watchdog,
  schema migrations; custom dark chrome; zone minimap; desktop overlays with
  resize waterfall + click-through lock + legend; groups, exceptions GUI,
  auto-close engine, import/export, start-with-Windows, undo/redo.

### Fixed
- Build hardening: `Nullable=enable`, never enable ImplicitUsings, conditional
  app icon.

---

[0.45.3]: https://github.com/Godswrath88/OnTop/releases/tag/v0.45.3
[0.45.2]: docs/history/WHATS_NEW.md
[0.45.1]: docs/history/WHATS_NEW.md
[0.45.0]: docs/history/WHATS_NEW.md
[0.44.2]: docs/history/WHATS_NEW.md
[0.44.1]: docs/history/WHATS_NEW.md
[0.44.0]: docs/history/WHATS_NEW.md
[0.43.0]: docs/history/WHATS_NEW.md
[0.42.1]: docs/history/WHATS_NEW.md
[0.42.0]: docs/history/WHATS_NEW.md
[0.41.0]: docs/history/WHATS_NEW.md
[0.40.0]: docs/history/WHATS_NEW.md
[0.39.0]: docs/history/WHATS_NEW.md
[0.38.0]: docs/history/WHATS_NEW.md
[0.37.0]: docs/history/WHATS_NEW.md
[0.36.1]: docs/history/WHATS_NEW.md
[0.36.0]: docs/history/WHATS_NEW.md
[0.35.0]: docs/history/WHATS_NEW.md
[0.34.0]: docs/history/WHATS_NEW.md
[0.33.8]: docs/history/WHATS_NEW.md
[0.33.7]: docs/history/WHATS_NEW.md
[0.33.6]: docs/history/WHATS_NEW.md
[0.33.5]: docs/history/WHATS_NEW.md
[0.33.4]: docs/history/WHATS_NEW.md
[0.33.3]: docs/history/WHATS_NEW.md
[0.33.2]: docs/history/WHATS_NEW.md
[0.33.1]: docs/history/WHATS_NEW.md
[0.33.0]: docs/history/WHATS_NEW.md
[0.32.0]: docs/history/WHATS_NEW.md
[0.31.0]: docs/history/WHATS_NEW.md
[0.30.0]: docs/history/WHATS_NEW.md
[0.29.0]: docs/history/WHATS_NEW.md
[0.28.0]: docs/history/WHATS_NEW.md
[0.27.0]: docs/history/WHATS_NEW.md
[0.26.0]: docs/history/WHATS_NEW.md
[0.25.0]: docs/history/WHATS_NEW.md
[0.24.0]: docs/history/WHATS_NEW.md
[0.23.0]: docs/history/WHATS_NEW.md
[0.22.0]: docs/history/WHATS_NEW.md
[0.21.0]: docs/history/WHATS_NEW.md
[0.20.0]: docs/history/WHATS_NEW.md
[0.19.0]: docs/history/WHATS_NEW.md
[0.18.0]: docs/history/WHATS_NEW.md
[0.17.3]: docs/history/WHATS_NEW.md
[0.17.2]: docs/history/WHATS_NEW.md
[0.17.1]: docs/history/WHATS_NEW.md
[0.17.0]: docs/history/WHATS_NEW.md
[0.16.0]: docs/history/WHATS_NEW.md
[0.15.0]: docs/history/WHATS_NEW.md
[0.14.0]: docs/history/WHATS_NEW.md
[0.13.2]: docs/history/WHATS_NEW.md
[0.13.1]: docs/history/WHATS_NEW.md
[0.13.0]: docs/history/WHATS_NEW.md
[0.12.0]: docs/history/WHATS_NEW.md
[0.11.0]: docs/history/WHATS_NEW.md
[0.10.0]: docs/history/WHATS_NEW.md
[0.9.0]: docs/history/WHATS_NEW.md
[0.8.0]: docs/history/WHATS_NEW.md
[0.7.0]: docs/history/WHATS_NEW.md
[0.6.0]: docs/history/WHATS_NEW.md
[0.5.0]: docs/history/WHATS_NEW.md
[0.4.0]: docs/history/WHATS_NEW.md
