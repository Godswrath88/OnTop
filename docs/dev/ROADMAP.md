# OnTop — Roadmap

Everything approved and still outstanding, in one place. Shipped work is in
`CHANGELOG.md` (the long-form spec of everything shipped before v0.45.4 is in
the roadmap at tag `v0.45.4`); conventions and the architecture live in
`PROJECT_SCOPE.md` (this folder). Remove an item here in the same PR that ships it.

**North star:** the modular **Macro Subsystem** (record / playback with a hard
anti-cheat guard). Its core shipped in v0.33.0; what remains is in the
**End Goal** section at the bottom of this file.

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

## ⭐ Standing rules for every feature (mine)

- **Every non-core feature must be fully disableable (S27).** The ONLY thing
  that is always on is the core pinning engine (always-on-top + the zone mouse
  hook). Every other feature MUST ship with its own enable/disable toggle,
  default to a safe state (no hooks, no hotkeys, no network, executes nothing),
  and be genuinely inert code when disabled, not merely hidden UI. Reasons:
  anti-cheat compatibility (strip OnTop down to just pinning) and user autonomy
  (users decide their own attack surface). A new feature without a real
  off-switch is incomplete; it joins Settings → Features and Minimal Mode.
- **One combo, one feature (S26).** Every hotkey surface validates through the
  shared conflict check (`FindHotkeyConflict`) at assignment time. Windows
  silently fails a second `RegisterHotKey`, so refusing the duplicate is the
  only reliable guard. Conflicts with other apps keep logging and joining the
  30 s retry list.

## Smaller open items (left over from shipped features)

- Light and high-contrast theme variants (the accent picker shipped; density is Round 3 #31).
- Per-zone color: a cycle option next to the swatch picker.
- Profiles: auto-switch the profile per monitor layout (compare Round 3 #18).
- Command-line flag `--profile <name>` (`--paused`, `--minimized`, `--portable` exist).
- Crash/issue prompt: deep-link to the repo's new-issue page; exercise
  config-corruption recovery deliberately.
- Localization scaffold.
- Pinned Windows viewer: recent hotkey activity feed (candidate for the Statistics page).
- Final visual/UX fresh-eyes pass before 1.0: narrow widths, focus/disabled
  coverage, token use, tooltip/Help coverage, a click-through of every window.

## Installer / Uninstaller (high priority for sharing)

> ✅ SCRIPT SHIPPED (S21 cont., v0.26.0): `installer/OnTop.iss` + BUILD.md cover
> every requirement below; since v0.45.4 CI builds the installer from each tag.
> Remaining to fully tick: I run the installer through BUILD.md's test
> checklist, then code signing (below) is the next distribution step.

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

- **Code-sign the exe and installer** — removes SmartScreen warnings;
  the single most professional distribution step (doubly important now that
  the installer is meant to be shared with other people)
- Privacy note in Help: the hook reads mouse position/clicks only — no
  keylogging, nothing leaves the machine
- ⏸ **SHELVED (2026-11-10): elevated "window mover" helper so Placement can
  move administrator windows.** Evaluated and parked. A tiny separate helper,
  started through the admin prompt only when a move hits an admin window,
  would work (for one startup rule, about one prompt per OnTop launch). Risks
  that parked it: (1) with the per-user install the helper sits in a
  user-writable folder, so any program could swap the exe and get full admin
  the next time the (unsigned, "Unknown publisher") prompt is accepted. The
  no-prompt scheduled-task variant is worse. A safe version needs code
  signing + a per-machine install under Program Files, and no portable mode.
  (2) A non-elevated caller driving an elevated helper is a confused deputy:
  any desktop program could move/hide admin windows (click-tricking).
  (3) Every helper bug is an admin bug. (4) A routine admin prompt trains
  click-through. (5) Anti-cheat visibility. It would also need SECURITY.md's
  "no feature may depend on elevation" rule changed. Revisit only after
  code signing lands. What shipped instead (0.45.3): a clear notice with
  Turn Rule Off / Keep Rule + "Don't ask again for this placement".

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
