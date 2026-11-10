# OnTop — Roadmap

Everything approved and outstanding, in one place. Shipped work is tracked in
`WHATS_NEW.md`; conventions and the continuity contract live in `PROJECT_SCOPE.md`
— update all three every session.

**North star:** the long-term **end goal** is a modular **Macro Subsystem**
(full-fidelity record / playback with a hard anti-cheat guard) folded into OnTop —
see the **End Goal** section at the bottom of this file. Near-term queued work
(v9.5 parity, the installer, the viewers) ships first; the macro engine lands last,
as its own module, so it can never destabilize the on-top core.

---


## 🎨 ROUND 3 — after the current test rounds pass (approved list, S28 2026-09-24)

> **Gate:** nothing here starts until I have built and tested 0.44.x per
> docs/testing/CHECKLIST.md (plus installer/updater/portable when I get to them) and Stage L
> has fixed what that turns up. I approved the list; ordering is mine to change.
> Suggested staging: **L1** = design system + Professional mode toggle + main
> window/settings (the big visual jump) · **L2** = grids/overlays/toasts/tray ·
> **L3** = motion + tour + empty states · then B–E.

### A. Appearance: "Professional" mode (the current UI becomes "Minimalist")
A second appearance mode, switchable in Settings → Appearance, forced off in Low
Impact Mode (it costs GPU). Pure WPF — vectors, gradients, blur, shadows,
animation — no image assets, no new dependencies.
1. Fluent-style layered surfaces — elevated cards with soft shadows, subtle inner borders, surface/raised/overlay tints, Mica/acrylic behind every chrome window on Win11 (extend the existing acrylic helper to every sub-GUI).
2. Typography scale — display/title/body/caption ramp, Segoe UI Variable where available, tighter header tracking, tabular numerals in grids.
3. Iconography everywhere — Segoe Fluent Icons glyphs on every button/menu item, consistent 16/20 px, accent-tinted on hover.
4. Motion system — 150–250 ms eased transitions on window open/close (the deferred close motion), expand/collapse, toggles, list row insert/remove, toast slide+fade. All off in Low Impact / reduced-motion.
5. Live accent-derived palette — hover/pressed/focus computed from the accent; accent glow on focused inputs; gradient accent on primary buttons.
6. Hero header on the main window — app name + status pill (Active / Paused / Stealth / Recording) + monitor thumbnails, replacing the plain status line.
7. Zone map polish — anti-aliased outlines with gradient fills, pill-badge labels, hover glow, monitor bezels as rounded frames with a screen-glass gradient, faint grid.
8. Overlay polish — soft outer glow, animated pulse on hit-flash, matching numbered badges.
9. Card-based Settings — each section a card with icon + description; left rail with jump-links (like the Help TOC).
10. Data grids — zebra striping, row hover elevation, sortable headers with glyphs, inline status chips (Enabled / Off-screen / Cooldown).
11. Toasts — glass cards with an icon strip, progress bar for timed ones, stacked with offset shadows.
12. Tray icon set — crisp multi-resolution icon; state variants as real glyphs (recording ring, playing triangle, paused bars, stealth eye) instead of a corner dot.
13. Welcome tour as a stepper — paged cards, progress dots, XAML-vector illustrations.
14. Empty states — every list (zones, macros, rules, scenes) gets an illustrated empty card with a primary action.
15. Micro-interactions — toggle thumb slide, checkbox check-draw, button press scale.

### B. Pinning & windows
16. Pin all windows of a process (+ "pin new windows of X automatically" as a rule flag).
17. Pinned Windows search/filter + group-by-process.
18. Scene auto-apply when a bound display layout appears.
19. "Pin stack" hotkey — cycle focus through OnTop-pinned windows.
20. Snap-to-edge nudging — arrow hotkeys move the focused pinned window 10 px (Shift = 1 px).

### C. Tooling & trust
21. Diagnostics card — what's running right now: hooks installed (mouse / recorder / hold), timers + intervals, priority, guard processes seen, feature switch states — one copyable screen.
22. Startup self-check log line — every feature switch state, so every log opens with the app's posture.
23. Presets — export/import the whole feature configuration as a named preset (Streaming, Gaming, Work), quarantined like everything else.
24. Settings search — type to filter every setting across sections.
25. Log viewer filters — category chips, level, time range; "copy last 50 lines".
26. Health badges in the tray tooltip (hook latency, last guard hit).

### D. Macros (still inside the SECURITY.md safety gate)
27. Macro templates — "click here, wait, type this" skeletons built in the editor without recording.
28. Macro groups/folders in the Macros window.
29. Per-macro "allowed profiles" whitelist (finer than the per-profile block).
30. Step editor — drag to reorder; multi-select delay scaling ("make these 20% faster").

### E. Accessibility
31. Reduced-motion switch (also implied by Low Impact); larger UI density mode (deferred since S25); full keyboard navigation of the zone grid; screen-reader names on every control.

## 🔺 Config integrity hardening pass (S27)
The load/import path is already well-defended (size gates, shared ClampZones,
corrupt-fallthrough, backups, schema-conflict prompt, https-only updater). A
focused pass to close the remaining gaps against a config-as-attack-vector,
per the new SECURITY.md "Config & Trusted-Data Integrity" rule:
- ✅ **Updater URL host re-validation on load** — DONE S28 (`IsValidUpdateUrl` in Validate; blanked + warned). Original: beyond the https check, optional
  allow-listing / clear surfacing so a swapped manifest host can't quietly
  redirect update checks (the check-only stage already executes nothing, but
  the URL should be visible + validated on load).
- ✅ **Macro-event range validation on load** — DONE S28 (`ClampMacros`, shared with import). Original: currently macros are count-capped;
  also range-check event kinds/coords/vk codes in Validate so a hand-edited
  macro can't feed malformed input to SendInput.
- ✅ **Explicit "inert load" test** — DONE S28 as a runtime tripwire (`SettingsService.IsLoading` + refusal at every choke-point). Original: a standing test that loading a config with a
  RunCommand/RunMacro/launch field does NOT execute it on load (only on
  trigger); guards the most important invariant against regressions.
- ✅ **Powerful-field change visibility** — DONE S28 (`PowerfulFieldsStamp`, schema 19). Original: consider a one-time notice when a
  powerful field (action command, updater URL, launch path) differs from the
  last-seen value, so a silent external edit is surfaced on next start.
- ✅ **Shared-config import quarantine (S27)** — DONE S28 (`ImportReviewWindow`, provenance via `IsOwnPath`). Original: applies ONLY to
  files the user EXPLICITLY imports, never to the app's own settings/backups: a
  crash/corruption fallback to an own backup loads fully active and seamless (no
  re-approval — recovery must never punish the user). For a genuine outside
  import, powerful content lands inert: launch/kill/
  run-macro zone actions reset to a safe default, updater URL blanked, macros
  imported but not hotkey-bound — until the user reviews an import summary
  (every command/macro/URL/path shown verbatim) and opts each back in
  explicitly. Makes a hostile shared config safe by construction; NO
  encryption/signing (rejected — see SECURITY.md: a hostile author just signs
  with their own key; validate content, don't trust authority). Confidentiality
  of on-disk config is left to Windows EFS/BitLocker; the app rolls no crypto
  as the PRIMARY defense.
- ✅ **Optional at-rest confidentiality for sensitive subset (S27)** — DONE S28 (`EncryptMacros`, DPAPI, schema 21). Original:
  — separate from tamper-proofing (which stays rejected). The config can hold
  privacy-sensitive data — window titles, paths, and especially MACRO
  recordings (raw keystrokes, potentially passwords). Plan: (1) minimize what's
  stored + make the macro UI clear it captures literal keystrokes; (2)
  optionally encrypt just the high-risk subset (macro event streams / command
  strings) with **Windows DPAPI (ProtectedData, per-user scope)** — decryption
  tied to the Windows login, so a copied/synced/leaked file exposes nothing, no
  app-stored key, no password prompt; (3) EXPORT is a separate deliberate path that
  decrypts locally and writes a portable artifact, so sharing/publishing is
  NEVER blocked — profiles/layouts/hotkeys/settings are never encrypted at all;
  macros are excluded from exports by default with an explicit opt-in + a
  "this captures literal keystrokes" warning. YES for privacy, still NO for
  authority/tamper-proofing.
This is hardening, not a rewrite — the invariants already largely hold; the pass
makes them explicit, tested, and complete.

## ⭐ STANDING DESIGN LAW — Every Non-Core Feature Must Be Fully Disableable (my rule, S27)
The ONLY thing that is always on is the core OnTop pinning engine (always-on-top
+ the zone mouse hook that powers it). **Every other feature** — macros (already
dark-by-default), the updater, auto-pin rules, stealth automation, statistics,
per-zone actions, the future Action Library, window placement/launcher, and
anything added later — MUST:
  - ship with its own explicit enable/disable toggle,
  - default to a safe state (off, or inert) where it installs no hooks, binds no
    hotkeys, makes no network calls, and executes nothing, and
  - when disabled, be genuinely inert code — not merely hidden UI.
Two reasons, both first-class:
  1. **Anti-cheat / compatibility** — a user must be able to strip OnTop down to
     just pinning so nothing it does can trip a game's anti-cheat.
  2. **User autonomy & security** — users decide their own attack surface; every
     capability is theirs to switch off, and the app must respect that fully.
This binds every existing and future feature. Reviews check it: a new feature
without a real off-switch is incomplete. ✅ DONE S28 (v0.36.0): Settings →
Features panel lists every toggle (new switches added for Statistics, Zone
Actions, Stealth Auto-Watcher) and a "Minimal Mode" button disables everything
but the core in one click.

## Queued — v9.5 Parity (Phases 2–4)

- ✅ **Draw zones** — SHIPPED (S9, v0.9.0): freeform polygon recording with
  full-screen capture overlay, ray-cast point-in-polygon detection, bounding-box
  scaling handles with drag-start snapshot, self-intersection lockout, Zone Type
  column, and a Redraw Shape context-menu entry.
- ✅ **Drag Box recording mode** — SHIPPED (S9): drag a rectangle, zone sized 1:1
  (Tolerance and Variation forced to 0).
- ✅ **Recording mode setting** (Point / Drag Box / Draw) + live legend mode swap
  during capture — SHIPPED (S9), persisted as `RecordMode`, 1/2/3/Tab to switch.
- ✅ **Pinned Windows viewer** — SHIPPED (S19, v0.19.0): every topmost window on
  the system with an OnTop badge on our pins, per-row Unpin, Unpin All (Ours),
  Refresh, optional hotkey. Still open from the original idea: recent hotkey
  activity feed (candidate for the Statistics page).
- ✅ **Extra action hotkeys** — SHIPPED (S10, v0.10.0): Quick Record (record in
  the current mode without opening the window, auto-named), View Logs (open the
  Log Viewer), and Soft Restart (save + relaunch, with a `--restart` mutex-wait
  handoff). All optional/clearable in the Hotkeys dialog's Actions section. (The
  nine main-GUI *toggle* hotkeys shipped in S9; a Pinned Windows hotkey ships
  with that viewer.)
- **%APPDATA%\OnTop is now the config home** (S13, v0.13.0) — config/logs moved
  there, with one-time migration of a beside-exe config from an older build.
  Still open: importing the *original v9.5 AutoHotkey* data that lived in
  %APPDATA% (see AHK importer below).
- ✅ **Own-window hook guard** — CLOSED (S25 audit): pid-level guard is the correct scope; the capture overlay uses WPF events, not the hook — handle matching adds nothing.

## Visual / Cohesion

- ✅ **Design-token pass** — DONE (S25, v0.30.0): stragglers converted to new
  tokens (BgBand/WarnAmber/RowDangerTint/RowAccentTint/Radius*); remaining code
  literals are deliberate alpha-derivations. Original goal: every color, corner radius, and spacing value into
  named theme resources; code-built windows consume the same tokens
- ✅ **Focus rings + disabled states** — SHIPPED (S25, v0.30.0): shared FocusRing on all interactive styles; uniform 50% disabled dim added to the 9 styles lacking one.
- ✅ **Open motion** — SHIPPED (S25, v0.30.0): 150 ms fade+scale on every chrome window. CLOSE motion deferred (needs Closing interception in code).
- ✅ **Tray paused badge** — SHIPPED (S23, v0.28.0): drawn amber corner dot on the tray icon while paused.
- ✅ **Accent picker** — SHIPPED (S25, v0.30.0): 6 accents, startup-applied. Light/high-contrast variants + density REMAIN (deliberately deferred for a personal tool).
- ✅ **Win10 acrylic fallback** — SHIPPED (S25, v0.30.0): AccentPolicy blur-behind below Win11 22H2.
- ✅ **Slider value bubble** while dragging — SHIPPED (S23, v0.28.0). · pulsing overlay dots · panel drop shadows ·
  Fluent icons on buttons (distinct vector pop-out icons S11; DataGrid selected-row
  accent bar shipped S14... ✅ S15)
- ✅ **Per-zone color picker** — SHIPPED (S24, v0.29.0): right-click → Color submenu with swatches (cycle remains).

## UX

- ✅ **Regroup the Zone Actions panel** — SHIPPED (S10, v0.10.0): the buttons
  were a jumbled three-row mix of unrelated actions; now four labeled
  subsections (RECORD & EDIT, ZONE DATA, CONFIGURE, APP) group related controls,
  with the record-mode selector placed beside Record. Behavior unchanged.
- ✅ **Persist main-window size and position** — SHIPPED (S11, v0.11.0): the main
  window and all three pop-outs (map/list/logs) save size+position and restore on
  open, clamped onto a visible monitor (a window saved on a removed display can't
  strand off-screen); maximized state preserved. Reusable `Helpers/WindowPlacement`;
  placements flush to disk once at app exit to avoid backup-chain churn.
- ✅ **Click-out-anywhere to commit a grid edit** — SHIPPED (S12, v0.12.0):
  clicking outside the zone grid commits an in-progress cell edit, in both the
  main list and the pop-out list (reusable `Helpers/GridEdit`).
- ✅ **Delete removes / F2 renames the selected zone** — SHIPPED (S14, v0.14.0),
  shared by the main and pop-out lists; ignored while editing a cell.
- ✅ **DataGrid keyboard-focus escape** — SHIPPED (S24, v0.29.0): commit on
  keyboard-focus leaving the grid (both lists).
- ✅ **Gray out zone Actions when overridden** — SHIPPED (S14, v0.14.0): with the
  global "Zones Trigger Stealth" toggle on, the per-zone Action chips (Exit/
  Stealth) in both lists dim and disable, with a tooltip, matching how sliders dim
  under Slider Lock.
- ✅ **Overshadowed-controls audit** — DONE (S25, v0.30.0): one new find (the
  startup-update toggle dims while the manifest URL is empty); sliders + action
  chips were already cued. Original: apply the same dim/disable cue
  wherever a global toggle supersedes a per-item control (Action chips and Slider
  Lock done; sweep the rest for consistency).

- ✅ **Optional hotkey for every toggle** — SHIPPED (S9, v0.9.0): a bindable,
  default-unbound hotkey slot per main-GUI toggle (Desktop Only, Debug Overlay,
  Apply To All, Override Exceptions, Confirm Before Exit, Lock Overlays, Keep
  GUIs On Top, Stealth, Zones Trigger Stealth) in a new Toggles section of the
  Hotkeys dialog, alongside the existing action hotkeys. A press flips the
  setting and toasts the new state; conflicts share the 30s retry loop.

- ✅ **Corner/edge snapping** on map drags — SHIPPED (S23, v0.28.0); Shift bypasses; zoom-independent threshold.
  ✅ **Arrow-key nudge** for the selected zone SHIPPED (S16, v0.16.0): Ctrl+Arrow =
  1px, Ctrl+Shift+Arrow = 10px (Ctrl-modified so plain arrows stay list nav).
- ✅ **Duplicate a zone to another monitor** SHIPPED (S16, v0.16.0): context-menu
  submenu, copies to the same relative position on the chosen monitor.
- ✅ Bindable **Re-Enable Mouse Hook** hotkey SHIPPED (S16, v0.16.0): was hardcoded
  Ctrl+Alt+R, now an editor row (still defaults to Ctrl+Alt+R).
- ✅ **Locate** — SHIPPED (S15, v0.15.0): double-click a list row pulses that zone
  on every open map (embedded + pop-out).
- ✅ Delete removes / F2 renames the selected zone (S14). ✅ Search box over the grid SHIPPED (S18, v0.18.0)
- ✅ **Multi-select** in the zone lists — SHIPPED (S21, v0.25.0): bulk remove
  (Delete), set-group, enable/disable via selection-preserving context menu;
  every bulk op is one undo step.
- ✅ **Zone priority reorder** — SHIPPED (S15, v0.15.0): Move Up/Down in the zone
  context menu (undoable); detection is first-match by list order.
- ✅ **Peek** — SHIPPED (S20 cont., v0.21.0): bindable hotkey flashes ALL zone
  overlays on the desktop for ~1.5s (plus the S17 map Flash-All button).
  Deliberately press-to-peek, not hold: key-up detection needs a keyboard hook,
  which SECURITY.md forbids (mouse-observation only).
- ✅ Tray: timed pause ("Pause For" 15/30/60/120 min, auto-resume), per-zone
  enable/disable submenu (S12, v0.12.0), and Quick Record (S14, v0.14.0) —
  SHIPPED.
- ✅ **Zone health badges** — SHIPPED (S15, v0.15.0): ⚠ in the list for off-screen,
  no-hit-area (all-zero size), or duplicate-name zones, with a tooltip; live.
- ✅ **Test mode** — SHIPPED (S23, v0.28.0): APP toggle; HIT toast + map flash /
  MISS with coordinates; actions suppressed, stats untouched, session-only.
- ✅ **Onboarding tour + starter zones** — SHIPPED (S24, v0.29.0); fresh installs only.

## Features

- ✅ **Profiles** — SHIPPED (S20 cont. 3, v0.23.0): named zone workspaces
  (zones/exceptions/triggers/sliders), loss-proof deep-clone switching, combo +
  manager + tray submenu + optional cycle hotkey. ✅ Monitor-configuration
  awareness SHIPPED too (S21, v0.25.0): health/map refresh on display changes +
  opt-in auto-disable of off-screen zones. (Auto-SWITCHING profiles per monitor
  layout remains a future idea.)
- ✅ **Window rules** — SHIPPED (S20, v0.20.0): auto always-on-top for windows
  matching process/title contains-patterns; master switch (off by default),
  per-rule enable, 3s scanner, hand-unpin wins, rule pins = OnTop pins
- ✅ **Per-zone actions** — SHIPPED (S26, v0.31.0): Run Command / Kill Process
  (protected-name refusal) / Toggle Window via right-click → Set Action; shown
  verbatim, logged + toasted every run, Test-Mode-suppressed, clamped
  (SECURITY.md "Per-Zone Actions"). REMAINING: **run a macro** (End Goal),
  multi-click patterns (double vs triple), and the **Action Library
  decoupling** below (S26).

- ✅ DONE S28 Stage E (v0.38.0): `Models/PlacementRule`, `Services/PlacementService`, `Views/PlacementWindow`, single launcher choke-point `MainViewModel.LaunchViaShell`, double-gated launch, zone action ApplyPlacement, hotkey, startup apply, quarantine of launch paths. Original spec: **🔺 Window Placement & Launcher — start and/or move windows to set positions
  (S27)**
  Move (or launch-then-move) specific programs to specific monitor positions on
  startup or via hotkey/zone — for apps that don't remember their own placement
  (e.g. a headset utility that should live in a corner of the second monitor).
  - **Placement rules**: match a window by process name and/or title, then set
    its target — monitor + position/size (drag-to-set on the zone map, or
    numeric), and optionally always-on-top + which profile it belongs to.
  - **Launch-then-move**: if the target program isn't running, optionally start
    it first (user-authored path/command — same SECURITY.md "Per-Zone Actions"
    rules: verbatim, logged, non-elevated), wait for its main window to appear
    (bounded poll), then move it.
  - **Triggers**: run all rules on OnTop startup (opt-in per rule), on a hotkey
    ("apply my layout"), and assignable as a zone/Action-Library action so one
    click arranges a whole workspace. Integrates with the Action Library above
    (a placement is just another action type) and with Profiles (per-profile
    layouts).
  - **Mechanism**: SetWindowPos/MoveWindow via the existing Win32 layer
    (already have ShowWindowAsync/SetForegroundWindow); DPI-aware coordinates
    reusing the overlay's virtual-desktop mapping so multi-monitor/mixed-DPI
    placement is exact. No new privileged surface — it moves windows the user
    could move by hand.
  - **SECURITY — privilege compartmentalization (S27).** The
    launch-then-move path is the most powerful thing OnTop can do (start a
    user-named program). OnTop is ONE non-elevated process, so OS-level the
    capability is inherently shared — the defense is therefore architectural,
    aimed at making the capability hard to reach, tightly constrained, and
    always visible. Design requirements for this feature:
    - **Single choke-point.** All process-launching lives in ONE internal
      method (extend the existing per-zone `RunZoneCommand` path — do NOT add a
      second launcher). One audited door, not many.
    - **Config-only inputs, never runtime/among-feature calls.** The launcher
      accepts a target ONLY from the user's saved placement rules (validated,
      clamped, shown verbatim). It exposes NO public "launch(arbitrary
      string)" API another feature could call at runtime — no feature can hand
      it a command; it reads only vetted config. This is the core of "other
      compromised features can't use it."
    - **Allow-list, not free-form, for auto-run-on-startup.** A rule that runs
      automatically at startup must point at an explicit user-saved path/rule;
      startup never executes anything assembled at runtime.
    - **Move ≠ launch — split the privileges.** Pure window MOVING
      (SetWindowPos on an already-running window) is low-risk and can be freely
      assignable. LAUNCHING is separately gated: its own sub-toggle (off by
      default), and a rule can move-only without ever gaining launch rights.
      A move-only rule can never escalate to launch.
    - **Reuse, don't re-implement, the guarded primitives.** Window moving uses
      the existing Win32 layer; launching reuses the existing ShellExecute
      (non-elevated, current user) path with its clamps, verbatim display,
      per-run log+toast, and Test-Mode suppression already in SECURITY.md's
      "Per-Zone Actions." No new privileged surface is introduced.
    - **Everything logged + toasted.** Every launch and every move logs (with
      the target) and toasts — no silent path, so a misuse is immediately
      visible.
    - **Master-toggle inert.** With the feature disabled it registers nothing,
      polls nothing, launches nothing (per the standing design law).
  - **Toggleable** like every non-core feature (see the master principle below):
    ships behind its own enable switch, off by default.

- ✅ DONE S28 Stage F (v0.39.0): `Models/LibraryAction` (10 kinds), `Views/ActionLibraryWindow`, VM `RunLibraryAction` dispatcher + `FindHotkeyConflict` (the promised shared check — macros window now delegates), `ZoneAction.Library = 7`, quarantine, Features switch. Original: **🔺 Action Library — decouple triggers from actions (S26)**
  Today an action lives INSIDE the zone that fires it, so actions are only
  reachable by triggering a zone — which limits their use. Restructure so both
  halves are first-class and independently usable:
  - **Named, reusable actions** — a small library of user-defined actions
    (Run Command / Kill Process / Toggle Window / later: Run Macro), each with
    a name, its parameters shown verbatim, and its own enable toggle.
  - **Assignable everywhere** — a zone's trigger can point at a library action;
    an OPTIONAL HOTKEY can point at the same (or any other) action directly —
    run commands become keybindable without any zone involved. One action,
    many triggers.
  - **Independently toggleable** — disabling a zone doesn't disable the action
    (a hotkey using it still works); disabling an action neuters it everywhere
    at once, with the zone/hotkey UI dimming to show the override (the standard
    overshadowed-control cue).
  - **Back-compat** — existing per-zone inline actions keep working (migrated
    into the library as auto-named entries, e.g. "Zone <name> Command"), and
    the quick Exit/Stealth chip behavior is unchanged.
  - **Security unchanged** — the SECURITY.md "Per-Zone Actions" rules bind the
    library identically (verbatim display, log+toast every run, protected-name
    refusal, clamps, Test Mode suppression); hotkey-fired actions log the
    hotkey as the source.
  - This is deliberately the SAME architecture the macro End Goal specifies
    ("first-class entities, assignable to a hotkey or a zone trigger") — build
    it here and macros later slot into the library as one more action type.

- **🔺 Hotkey conflict rule — ONE COMBO, ONE FEATURE (my rule, S26)**
  ✅ Editor-level PREVENTION SHIPPED (S26 cont., v0.32.0): the Hotkeys dialog
  refuses to save duplicates (normalized compare across every binding incl.
  optional toggles and the built-in Ctrl+Alt+P), naming both colliding
  features in the warning — duplicates can't exist in the first place.
  STANDING REQUIREMENT for every future hotkey surface: the **Action Library's
  per-action hotkeys and macro hotkeys MUST validate through the same central
  check** (extract FindConflict into a shared service when a second surface
  appears — the dialog is the only assignment path today). Rationale: Windows
  silently fails the second RegisterHotKey, leaving a feature dead with no
  error; assignment-time refusal is the only reliable guard. Registration
  failures that still occur (conflicts with OTHER apps) keep logging + joining
  the 30 s retry list as today.
- ✅ **Statistics page** — SHIPPED (S20 cont. 4, v0.24.0): session + lifetime
  counters, top-triggered zones, own stats.json store, confirmed reset.
  ✅ Trigger history / recent-activity feed SHIPPED too (S21 cont., v0.26.0):
  timestamped rolling feed (300-entry cap, persisted, reset-aware) in the
  Statistics window.
- ❌ **SCRAPPED (my decision, S28) — kept for the record in case I change my mind.** AHK importer (OPTIONAL — only proceeds if the
  original .ahk file ever surfaces)** — migrate the original AutoHotkey OnTop's settings, then
  retire it (also ends the F1/F10 hotkey conflict for good)
- ✅ **Stealth Mode follow-ups** — SHIPPED (S26, v0.31.0): toggle-stealth
  hotkey (optional binding), auto-stealth while watched processes run (5 s
  watcher; auto-releases only what it engaged), and overlay auto-hide while
  stealthed (default on, toggleable). The watcher doubles as the "silent"
  layer of the macro anti-cheat guard (End Goal).
- ✅ Command-line flags — `--paused` + `--minimized` SHIPPED (S15, v0.15.0);
  `--profile <name>` still pending (needs Profiles).
- ✅ **Diagnostics bundle export** — SHIPPED (S21, v0.25.0): Logs toolbar zips
  settings + stats + current log + system info to a user-picked file.
- ✅ **Crash/issue report prompt** — SHIPPED (S23, v0.28.0): once-per-session
  prompt with comment → Config\Crashes bundle; fatal crashes auto-bundle.
  (Original spec below; the GitHub-issue deep link waits for the repo era.)
  Previously: — on unhandled crashes (and
  optionally on ERROR-level events), offer a dialog: "Something went wrong —
  save a diagnostics bundle?" with a free-text "what were you doing?" comment
  box that gets written into the bundle's diag.txt. Purely local (SECURITY.md:
  nothing transmitted) — the user attaches the zip wherever they report the
  issue; once the updater/GitHub era arrives, the prompt can deep-link to the
  repo's new-issue page. Note: config-corruption recovery remains lightly
  tested by hand — exercise it deliberately once update-push testing exists.
- Update checker (HTTPS version compare only, never auto-execute)
- ✅ **Portable mode** — SHIPPED (S25, v0.30.0): OnTop.portable marker / --portable → data beside the exe. Localization scaffold REMAINS (deferred).

## Stability

- **🔺 HIGH — Logging completeness audit (S20). FIRST PASS DONE
  (v0.23.0): zero silent catches (all log or carry written justification),
  SettingsService.AttachLogger + Config category, category checkbox list
  extended. STANDING RULE from here: no new bare `catch { }` ever — log it or
  justify it in a comment. Remaining below for a second pass:**
  - Sweep **every silent catch** (20 bare `catch { }` today: App ×9,
    SettingsService ×6, OverlayService ×2, LogService ×2, VM ×1 — plus
    commented variants) — each either logs (DEBUG at minimum) or carries a
    written justification for true silence (e.g. inside the logger itself).
  - Sweep every command/action path: hotkeys (fired AND failed-to-bind), tray
    items, buttons, context menus, rule scans, pin/unpin, config load/save/
    migrate/conflict, placement + column restore failures, overlay lifecycle,
    watchdog decisions — anything a user or a bug report would ask "did it
    happen?" about must answer from the log.
  - Volume control so the audit doesn't drown the viewer: new **per-category
    filter checkboxes in the Log Viewer** (Settings/Hotkeys/Pins/Rules/Overlay/
    Config/Updates/…), with noisy completeness-logging defaulting to DEBUG so
    the existing Verbose toggle + category boxes keep the default view clean.
  - Acceptance: a session's log tells the full story of what OnTop did and why,
    with zero invisible failures.
- ✅ **Hook + topmost reapply on sleep/resume and session switch** — SHIPPED
  (S21, v0.25.0): PowerModeChanged.Resume + SessionUnlock re-assert hook,
  hotkeys, pinned windows, GUI topmost.
  (PowerModeChanged / SessionSwitched)
- ✅ **Per-monitor overlay windows + DPI-change re-flow** — SHIPPED (S24, v0.29.0): one overlay per monitor (PMv2), OnDpiChanged reposition, topology rebuild.
- ✅ **Startup-shortcut self-repair** — SHIPPED (S24, v0.29.0): re-pointed every launch, logged.
- ✅ **Hook latency instrumentation** — SHIPPED (S24, v0.29.0): EMA + one-time warn at 3 ms.
- ✅ **Crash bundles include the log tail** — SHIPPED (S24, v0.29.0): log-tail.txt (~50 lines).
- ✅ **Pre-import auto-backup + backup pruning (keep 10)** — SHIPPED (S24, v0.29.0).

## Installer / Uninstaller (high priority for sharing)

> ✅ SCRIPT SHIPPED (S21 cont., v0.26.0): `installer/OnTop.iss` + BUILD.md cover
> every requirement below (wizard, shortcuts, location, startup, run-now, .NET 8
> bootstrap, data-preserving uninstall, per-user no-UAC default). Remaining to
> fully tick: I compile with Inno Setup and run BUILD.md's test
> checklist — then code signing (below) is the next distribution step.

Goal: produce a single `.exe` installer I can hand to anyone, that sets
OnTop up with no manual steps and leaves an obvious uninstaller behind. I'm
fine building it on my own machine first (e.g. Inno Setup or WiX), but I want
a fleshed-out, professional experience.

Required installer features:
- **Wizard UI** with these options: create a **Desktop shortcut** (checkbox),
  create a **Start Menu shortcut** (checkbox), and **choose the install
  location** (default `%ProgramFiles%\OnTop`, changeable).
- **Optional**: "Launch OnTop at Windows startup" checkbox (wires the same
  Startup-folder shortcut the app manages), and "Run OnTop now" on finish.
- **Dependency bootstrapping** — detect and install prerequisites automatically
  so the end user does nothing: the **.NET 8 Desktop Runtime** (download +
  silent install if absent), and the VC++ redistributable if ever needed. If a
  network isn't available, fall back to a bundled offline copy or a clear
  message. (Alternatively ship a **self-contained** build so no runtime
  download is needed — decide per target; self-contained is simpler/offline but
  larger. Document the tradeoff.)
- **Highly visible uninstaller** placed at the TOP of the install folder
  (e.g. `Uninstall OnTop.exe` right in `%ProgramFiles%\OnTop\`), so opening the
  install location shows it immediately — plus the normal Start Menu and
  Add/Remove Programs entries.
- **Clean uninstall** — remove the exe, shortcuts (desktop/start/startup), and
  offer to delete the `config\`/`logs\` data folder (ask, don't force, so users
  can keep their zones). Kill a running OnTop.exe first (reuse the taskkill
  approach).
- **Per-user vs per-machine** — support both; per-user avoids the admin prompt,
  per-machine needs elevation. Default to per-user to match the app's
  no-admin-needed stance.
- **Code-sign the installer and exe** (see below) so SmartScreen doesn't warn
  recipients — important since this is meant to be shared with others.
- Versioned output name (`OnTop_Setup_x.y.z.exe`) and upgrade-in-place support
  (uninstall old / install new without losing config).

Build notes for whoever implements: an **Inno Setup** script is the lightest
path (has built-in `[Tasks]` for the desktop/startup checkboxes, `[Dirs]`,
custom pages for the runtime download via `DownloadTemporaryFile`, and auto-
generates the uninstaller + Add/Remove entry). WiX/MSI is the alternative if
enterprise deployment is ever needed. Keep the `.iss` (or `.wxs`) script in the
repo so it's version-controlled like everything else.

## Security / Distribution

- ✅ **Warn when launched elevated** — SHIPPED (S21, v0.25.0): toast + log at
  startup when running as administrator.
- ✅ **Strict validation + size caps on imported JSON** — SHIPPED (S21,
  v0.25.0): 4 MB gate + shared ClampZones across config load and imports.
- **Code-sign the exe and installer** — removes SmartScreen warnings;
  the single most professional distribution step (doubly important now that
  the installer is meant to be shared with other people)
- Privacy note in Help: the hook reads mouse position/clicks only — no
  keylogging, nothing leaves the machine

### 🔺 MAJOR — Online update checker + updater (S20)

> ✅ **STAGE ONE SHIPPED** (S23, v0.28.0): check & notify with the exact 3-way
> prompt + Settings button, dormant-by-default, browser handoff only — nothing
> is downloaded or executed by the app (SECURITY.md amended accordingly).
> REMAINING = stage two below (in-app download + SHA-256 + Authenticode + run),
> still hard-ordered AFTER code signing.

Goal: OnTop can detect that a newer release exists and offer it to the user.

**UX contract (as I specified it):**
- On startup (and periodically while running), a lightweight version check; when a
  newer version exists, a prompt with exactly three choices:
  1. **Download & Update** — fetch the new release and hand off to the installer.
  2. **Ignore This Version** — silence the prompt for THIS release only; it
     reappears when the NEXT release ships (persist an `IgnoredUpdateVersion`).
  3. **Cancel** — dismiss for now; prompt again on next startup.
- A manual **"Check for Updates" button in Settings** (APP section), which also
  reports "You're up to date" and clears an ignored version if the user checks
  manually.

**Security requirements (non-negotiable — this feature is the app's biggest
potential attack surface; see SECURITY.md "Supply chain"):**
- The version CHECK is a single HTTPS GET of a small signed manifest (version +
  release notes URL + download URL + SHA-256 + signature). No telemetry: the
  request carries nothing identifying beyond standard HTTP; no machine IDs, no
  usage data, ever.
- HTTPS with certificate validation ON (never disable); pinned host allow-list
  (the release host only); manifest size hard-capped; strict JSON parsing with
  the same validation discipline as the config loader.
- The downloaded installer is verified BEFORE execution: SHA-256 must match the
  manifest AND the file's Authenticode signature must validate (requires the
  code-signing item above to ship FIRST — the updater must not ship before
  signing does).
- **Never silent**: no background install, no auto-run. The user explicitly
  clicks Download & Update; the installer runs visibly, asInvoker (elevation
  only if the installer itself legitimately requires it, via the OS prompt).
- Downgrade protection: refuse any "update" whose version is ≤ the running
  version (blocks rollback attacks serving old vulnerable builds).
- Update checking is a **setting the user can turn off entirely** (default ON
  is acceptable since the check is anonymous, but the off switch is required).
- Failure = silence toward the USER, never toward the LOG: network errors never
  nag, never block startup — but every update-path event IS logged through the
  main LogService under an "Updates" category: checks (start/result/skipped),
  the prompt shown and which of the three choices the user made, ignored-version
  set/cleared, download start/finish, hash + signature verification results
  (pass AND fail), handoff to the installer, and every error including ones
  deliberately swallowed for UX. The updater must be fully reconstructable from
  the log alone.
- When this ships, SECURITY.md's "no network" rule gets amended to "no network
  I/O except the update check, under these exact constraints" + a new checklist
  item auditing the update path.

**Build order:** code signing → manifest format + signing pipeline (server side
is just static file hosting) → check + prompt (Ignore/Cancel semantics) →
verified download + handoff → Settings button. Depends on the installer.

---

## Final visual/UX QA pass (S25 — do IMMEDIATELY before macros)
A fresh-eyes sweep over everything added since the S25 cohesion pass: layout at
narrow widths, focus/disabled coverage on new controls, token usage in new
views, tooltip/Help coverage, motion consistency, and a full click-through of
every window. Cheap insurance before the macro project begins.

## 🎯 End Goal (North Star) — Macro Subsystem: Record / Playback + Anti-Cheat Guard

> ✅ **CORE SHIPPED (S27, v0.33.0)** — full-fidelity session recording (mouse
> path + clicks + wheel + keys, real timestamps, 0..65535 virtual-desktop
> normalization → layout-adaptive replay), SendInput playback with
> drift-free absolute pacing, speed multiplier, repeat/loop-until-stopped,
> per-macro hotkeys (play/stop toggle, conflict-checked), zone Run-Macro
> action, management window, master toggle (ships dark), anti-cheat guard
> (watched-process refusal + mid-run abort), Ctrl+Alt+X panic (playback-only
> registration, reserved), Test-Mode suppression, SECURITY.md "Macro Module"
> contract, caps (50 macros / 20k events).
> **⚠ SAFETY GATE:** any macro capability beyond input replay — condition
> loops, scripting, launching, file/network access — MUST pass SECURITY.md
> "Future Capability Expansion" first: bounded VERB LIST over a scripting
> language (no ambient authority), capabilities granted per-macro and never
> self-granted by imported content, **no install/download-then-execute ever**,
> reuse of the single launcher choke-point, and nothing auto-runs on load or
> import. Multi-process is deliberately NOT the answer (see PROJECT_SCOPE); it
> is revisited only if a non-strippable engine is embedded or third-party
> content runs by default.
> REMAINING from the spec below: humanizing jitter, condition-based loops,
> lighter capture modes (action-only / hotkey-sequence), relative-replay
> option, per-profile macro policies, and the pre-macro QA pass items become
> the post-core polish list.

Fold my separate macro recorder into OnTop as an **optional, self-contained
module**, so one app does always-on-top pinning *and* input macros — with the
pinning core staying lean and hook-free when macros are off. This is the long-term
capstone; it lands **last** and **behind a master toggle**, so it can be shipped
dark and can never destabilize the on-top core. Design pillars: **modular,
fail-safe, and honest about anti-cheat.**

### A. Recording

- **Full-fidelity "session" recording (the primary mode)** — capture *all* keyboard
  and mouse input, **including mouse movement**, with real timestamps. The user tabs
  into whatever they're working with, presses the record hotkey, uses the PC exactly
  as normal, and stops; playback then reproduces exactly what they did, at real-time
  speed — effectively "a remote user driving the machine." This is the target
  fidelity, not a stretch goal.
- **Real-time timing preserved** — inter-event delays are part of the recording, so
  playback runs at the same speed as the original session (delays inserted to match
  real time). Optional speed multiplier and optional humanizing jitter on top.
- **Movement fidelity** — record the full mouse path (sampled at a sensible rate),
  not just clicks; handle absolute vs relative coordinates and multi-monitor / DPI so
  paths land correctly on replay.
- **Edge cases to get right** — held modifiers, key auto-repeat, chorded/simultaneous
  keys, buttons held *during* moves, and the scroll wheel.
✅ - **Lighter capture modes (optional)** — DONE S28 (Record Mouse Path toggle → actions-only). — action-only (clicks + keys, no path) and
  fixed hotkey-sequence macros, for when full fidelity isn't needed.

### B. Playback

- **SendInput-based replay** reproducing the recorded stream with its original timing.
- **Loop options** — once / N times / until stopped / until a condition; speed
  multiplier; optional randomized jitter for less mechanical timing.
- **Resolution / layout awareness** — absolute-position replay must adapt to the
  monitor layout at playback time (or replay relative), so a different setup doesn't
  misfire.
- **Always-available panic stop** — a global hotkey that halts any playback
  instantly, no matter what else is happening.

### C. Macro management

- Macros are **first-class entities alongside Zones** — named, saved, listed, and
  editable, with their own editor.
- **Assignable to a hotkey or a zone trigger** — a zone's Action can become "Run
  Macro X," extending the existing per-zone-action model (run command / kill process
  / toggle window → run macro).
✅ - **Per-profile macro sets** — DONE S28 (policy per profile; the LIBRARY stays global by design). — ties into Profiles, so each game/profile carries its
  own macros *and* its own macro policy (below).

### D. Modularity & the anti-cheat guard (the safety core — must be bullet-proof)

Three escalating layers, so the feature can always be subverted down to "not present":

1. **Whole subsystem is an optional module** — a master toggle that, when off,
   installs/activates *nothing* macro-related. The on-top core runs clean and light,
   with no macro hooks present for anything to flag.
2. **Silent / stealth mode** — reuse the existing Stealth unhook: drop the input
   hooks so nothing is installed for anti-cheat to see; macros go inert while
   stealthed. (This is the existing "unhook if the game hates it," now covering
   macros too.)
3. **Detection-triggered auto-disable — the KILL SWITCH** — a watch
   list of **processes / window titles / designated zones** that, when detected,
   **fully disable the macro subsystem** — not merely unhook, but tear it down: stop
   any running playback, unhook, disable record and play, and (optionally) hide the
   macro UI and unload the module for the session. Reuses the existing
   process-exception / zone-detection machinery.
   - **Graduated response levels**, chosen per rule: (a) unhook only (silent),
     (b) pause macros, (c) full teardown / disable the module, (d) also close the
     macro module entirely for the session.
   - **Match by** process name, window title, or a specific zone being present /
     entered.
   - **Auto re-enable** when the watched process/window closes — **off by default**
     (re-enabling anywhere near anti-cheat should be a deliberate, explicit choice).

### E. Per-game policy & fail-safe defaults

✅ - **Per-game / per-profile macro policy** — DONE S28 (Profile.MacroPolicy, Deny In Profile toggle). — each profile declares its stance:
  *allowed*, *silent-only*, or *fully disabled*. Competitive titles default to
  **disabled**.
- **Fail-safe by default** — when a watched anti-cheat process is present, the safe
  behavior (macros disabled) is the default; enabling macros for such a game requires
  an explicit, per-game opt-in.
✅ - **Known-risky-process list** — DONE S28 (MacroService.KnownAntiCheat + Guard: Known Anti-Cheat switch). — ship a maintained list of kernel-level anti-cheat
  processes (e.g. Vanguard, and EAC / BattlEye in kernel mode) that, by default,
  trigger the kill switch or at least a blocking warning.

### F. Anti-cheat honesty & guardrails (mandatory UX — non-negotiable)

✅ - **Blunt, unavoidable warnings** — DONE S28 (banner + one-time confirm). — synthetic input can be detected and can get
  accounts **banned**, especially under kernel-level anti-cheat. The app must **never**
  imply macros are undetectable; require a one-time acknowledgement before first use
  in a given game.
- **No global "on for everything"** — macros are enabled per game/profile, never
  blanket-on.
✅ - **Always-visible armed / recording / playing indicator** — DONE S28 (tray badge red/blue + tooltip + status bar)., so the user is never
  unsure whether input is being captured or injected.
- **Full audit logging** of every arm / record / play / auto-disable event.
- **Legitimate-use framing** — accessibility, single-player, productivity, and
  non-competitive automation are the intended uses; the guardrails target
  competitive / anti-cheat titles specifically.

### G. Architecture notes (for whoever implements)

- **New Macros module**: a `MacroService` (record/playback engine), a `MacroRecording`
  model (event stream + high-resolution timings), a `Macro` entity (name, events,
  assignment, policy), a Macros list/editor view, and a `MacroGuard` component wired
  to the existing process/zone detection.
- **Reuse existing infrastructure**: extend the low-level hook to also capture
  keyboard + mouse-move for recording; reuse hotkey registration, the Stealth unhook,
  and the process-exception / zone watchers (the kill switch is that same machinery
  pointed at a new response). Keep the recorder and the guard **decoupled** so the
  guard can hard-stop the engine at any instant.
- **Capture** via `WH_KEYBOARD_LL` + `WH_MOUSE_LL` with `QueryPerformanceCounter`
  timestamps; **replay** via `SendInput` with timed waits.
- **Suggested phases**: (1) record/playback engine — full-fidelity session capture +
  timed replay + panic stop; (2) macro management + hotkey/zone assignment + profiles;
  (3) the guard / kill switch + per-game policy + anti-cheat warnings; (4) editor +
  humanization + advanced triggers. Ship the module behind the master toggle so it
  stays dark until the guard is proven solid.
