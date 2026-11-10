# OnTop — TESTING.md (standing checklist)

> **Standing document.** Every stage that adds testable behavior appends to it. I work it top-down after a build and report **by item number**. Marks are typed inside the brackets; notes go on the line below the item, prefixed `>`.

## Legend (type one character inside the brackets)

| Mark | Meaning | When to use it |
|---|---|---|
| `[ ]` | not tested yet | default |
| `[.]` | pass | it did what the item says |
| `[x]` | **fail** | it didn't — add a `>` note: what you did → what happened → what you expected (+ log lines) |
| `[~]` | partial | works, but with a problem worth a `>` note (cosmetic, wording, edge case) |
| `[n]` | not applicable / skipped on purpose | you chose not to test it this round |
| `[b]` | blocked | couldn't test because another item failed — name it in a `>` note |
| `[?]` | unclear | you didn't know how to test it — say what was confusing |

**`[!]` in the item text** = a failure here cascades into many other items; report those first.
**`⟳ retest`** = this was reported failing and has since been fixed; test it again from scratch.
**"How:"** lines explain the steps for items that aren't obvious.

**Log filtering:** View Logs → tick the category shown in brackets (e.g. `[Detection]`). Turn on **Verbose Logging** (Settings → Logging) for the test session — from v0.44.0 it logs *why* a gesture was ignored, placement candidates, overlay re-asserts and hotkey bind errors.

---

## 0. Build & migration (v0.44.0)

- [ ] **[!] Compiles clean** on Windows (NuGet restore succeeded — `System.Security.Cryptography.ProtectedData` is the only package). Paste every error verbatim.
- [.] First launch: settings migrate silently (now → schema 29); no "config conflict" prompt; no "powerful fields changed" toast on the **second** launch.
- [.] Existing zones, profiles, hotkeys, macros all present.
- [.] Hotkeys… shows **Ctrl+Alt+M** on "Record / Stop Macro".
- [ ] `settings.json` (installed mode): macros stored as `EncryptedEvents` base64, no plain `Events`; macros still play after restart. Portable mode: plain `Events`.
  > 09-24: installed mode not tested yet.
- [.] Help opens; CONTENTS chips; amber ⚠ paragraphs present.

## 1. Stage D — v0.37.0 (feedback round)

- [ ] **[!] ⟳ retest — Accent**: Settings → Appearance → pick an accent → whole app re-tints **immediately**. Also try Theme (High Contrast / Light / Dark) — same mechanism.
  > 09-24: "accent doesn't change colors of anything". Root cause: frozen StaticResource brushes; all brush refs are DynamicResource now.
- [ ] ⟳ retest — **Status strip lifecycle**: turn Macros on with the Macros window CLOSED → NO strip. Open the Macros window → strip appears. Close it → strip gone. Press Ctrl+Alt+M with the window closed → strip appears (RECORDING), stays ~4 s after stop, hides. Press × on the strip → hidden until the next recording. Bind "Show / Hide Macro Status Strip" in Hotkeys… → toggles it; the strip shows that hotkey.
  > 09-24: strip shouldn't be up permanently; needs a close keybind; should close with the sub-GUI; popped on script start.
- [ ] ⟳ retest — **Ctrl+Alt+M** → red marquee + elapsed + step count; again → SAVED → READY. Open the step editor: the recording must NOT start with Ctrl/Alt/M key-ups or end with their key-downs.
  > 09-24: the start chord was still in the recording. Fixed (TrimChordEdges).
- [.] Main window MACROS card (Macros…, ● Record, ■ Stop); ■ Panic Stop in the Macros window.
- [.] Macros window opens large enough, remembers size/position, stays on top.
- [.] Step editor: search / delay edit / Delete Selected / Remove Mouse Moves / Save / Cancel.
- [.] Known Anti-Cheat… list with "(running now)".
- [n] Kill switch (mid-run watched-process appearance → master toggle OFF; refusal at start does NOT trip it).
  How: start a looping macro (Repeat 0), then launch a program whose name is in Settings → Stealth watched list (e.g. add `notepad` to the list first). Expect: playback stops, "Macros Enabled" turns off, toast says to re-arm.
- [ ] ⟳ retest — **Overlays above OnTop GUIs**: open the Macros window over a zone → Peek / map ping (◎ in the list) → overlays draw ABOVE the window, and stay above after the ping ends. Click into the main window → overlays still above within half a second.
  > 09-24: overlays buried under the main GUI after a ping and didn't come back. Fixed (NOTOPMOST→TOPMOST toggle + re-assert on our-window focus).
- [n] Low Impact Mode → Task Manager shows Below Normal; log line.
- [.] Wording: speed %, Wobble px, "Block Macros In Profile X", repeat once/N×/until stopped.
- [.] Welcome tour: current hotkeys; Get Started / Add 4 Starter Zones open the main window.
- [ ] **Countdown field** (Macros window, "Countdown before play"): set 0 → playback starts instantly; set 3 → strip counts 3-2-1.

## 2. Stage E — v0.38.0 (Window Placement & Launcher)

- [.] Features → Window Placement on; APP → Placement… opens.
- [ ] ⟳ retest — **Capture From Focused Window** (3 s) → rule appears. **Move the window, then Apply Selected → it returns.** Tick Max → maximizes there.
  How: use a NORMAL program (Notepad, Explorer) for this test. Hardware tools such as GPU tuning utilities run as administrator — a non-elevated OnTop **cannot** move them (Windows forbids it); the log/toast now says "access denied — runs as administrator" instead of "applied".
  > 09-24: placement didn't move the window; the log claimed "applied". Root cause: result never checked + elevated target.
- [ ] ⟳ retest — **Edit a rule by hand**: double-click a row (or Edit Selected… / right-click → Edit Rule…) → full editor with Pick… (running processes / Browse .exe), monitor, X/Y/W/H, Maximize, On Start, **Delay**, Launch path, Launch, wait. Save → grid updates. Hover a column header → description.
  > 09-24: no way to adjust a rule except capture; no column descriptions; needs a delay for slow-starting apps.
- [ ] ⟳ retest — **On Start** (+ Delay 5): restart OnTop → log "waiting 5s first" then applied ~9 s after launch.
- [n] Apply All hotkey; inert while the feature is off.
- [ ] ⟳ retest — Zone → Set Action → **Apply Placement** → triggering the zone places the window and does **NOT** enter Stealth (unless the zone's After Action says so).
  > 09-24: zone applied placement but ALSO went into stealth (global "Zones Trigger Stealth" was on). Fixed: the override only affects Exit zones; use right-click → After Action for advanced zones.
- [n] Launch: rule with a Launch Path, Launch unticked → "No Matching Window", nothing launches; ticked → launches, placed within the wait. Test Mode suppresses.
- [n] Zone.Clone fix: Duplicate a Run Command zone → duplicate keeps the command.
- [n] Import/restore with a launch path → Import Review lists "Placement Launch", arrives blanked.
- [ ] ⟳ retest — **Reopen Placement… after closing it** (edit a cell first, then close while the cell is still in edit mode) → no crash.
  > 09-24: crashed with "'DeferRefresh' is not allowed during an AddNew or EditItem transaction". Fixed (commit on close + before bind; Auto-Pin too).
- [ ] **Placement window footer**: change something → Cancel (Revert) → change is gone. Change → Save & Close → kept. No ✕ in the title bar.

## 3. Stage F — v0.39.0 (Action Library)

- [ ] ⟳ retest — Features → Action Library on → Actions… → New → Run Command → Payload `notepad` → **Run** → Notepad opens; log `[Zones] Library action 'X' (library window): Run: notepad`.
  > 09-24: run command didn't work from the library or a zone. The ZONE path was the detection bug (section 4 [!]); the library's Run button was never pressed per the log — please press Run in the window this time.
- [ ] ⟳ retest — Hotkey… (now a **capture dialog**: press keys) → assign → press it → runs. Press a combo already used by a macro or app hotkey → refused **in the dialog** with both names; nothing executes while the dialog is open.
  > 09-24: duplicates were ignored and the original hotkey executed. Fixed (global hotkeys suspended during capture; live refusal).
- [n] Each kind from Run: Kill (protected refused), Toggle Window, Play Macro, Apply Placement, Switch Profile, Pause/Resume, Peek, Toggle Stealth, Pin Under Cursor, Apply Pin Scene.
- [.] Zone → Set Action → Library Action → triggers it; delete the action → zone falls back to Exit.
- [n] Feature off → hotkeys dead; zone toasts "Action Library Is Off".
- [ ] ⟳ retest — Hotkeys… dialog: press a combo that a library action already uses → refused the moment you press it (toast names both), box reverts.
- [ ] **Library footer**: Cancel (Revert) undoes a new action; Save & Close keeps.

## 4. Stage G — v0.40.0 (sixteen additions)

- [ ] **[!] ⟳ retest — Zone detection at all**: with Desktop Only ON and overlays visible (any zone with its dot on), double-click inside a zone on the DESKTOP → fires. Double-click inside the zone with a program window under it → does NOT fire; verbose log says `gated by Desktop Only — under cursor: '<title>'`. In Test Mode the miss toasts "Not On Desktop (<process>)".
  > 09-24: "double clicks aren't detecting". Root cause: Desktop Only saw OnTop's own overlay as the window under the cursor. Fixed (looks through overlays; still blocks on a real window).
- [ ] **Gestures**: Middle Click zone fires only on middle; Triple Left on the third click; Ctrl+double only with Ctrl; Double Right on right; Shift+double / Alt+double. A classic zone ignores those.
  How: right-click a zone → Trigger Gesture. Verbose log line `Gesture <name> at (x,y)` appears for every dispatch.
- [.] Pinned Windows −/+ opacity; Click-Through; revert.
- [ ] ⟳ retest — **Click-through feedback**: Click-Through on a row → an **orange frame** appears around that window and the row's State column reads e.g. `100% · CLICK-THROUGH`; off → frame gone. "Orange Frame On Click-Through" switch turns frames off/on for all.
- [n] Hotkeys: Pin Under Cursor, Opacity +/−, Click-Through Under Cursor, Cycle Corner.
- [n] Profile Rules… → rule for a running process → switches; revert when gone.
  How: Settings → Features → Profile Rules on → Profile Rules… → Add → Process `notepad`, Profile = a second profile you created. Open Notepad → within 5 s the profile switches (toast with diff). Close Notepad → reverts.
- [n] Display Profiles: Bind This Layout → change monitor config → switches.
- [ ] Do Not Disturb + watched process running → routine toasts silent; Errors show; Toast History shows `[DND]`.
- [ ] Macro Countdown 3 → "STARTING IN 3…"; **Then Play…** → second macro after a natural finish; **Relative** → clicks follow a moved anchor window.
  How (Relative): record a macro that clicks inside Notepad; set Relative ON on the row; move Notepad; play → the clicks land in the moved window.
- [ ] Editor → Insert Wait For Window ("Calculator", 10 s) → playback pauses until Calculator opens; timeout stops with a toast.
- [ ] Cheat Sheet hotkey → card; Command Palette hotkey → type "peek" → Enter.
- [ ] Statistics → bars; Export CSV….

## 5. Stage H — v0.41.0 (scope recalibration + hold chords)

- [ ] Features → Hold Chords on; Hotkeys… Hold To Peek → overlays while held; releasing Ctrl alone ends it. Log `Hold-chord keyboard hook INSTALLED`.
- [ ] Hold To Pin → topmost while held, released after; an already-pinned window untouched.
- [ ] Stealth → `hook REMOVED (disarmed)`; leave Stealth → back.
- [ ] Hold Chords off → hook removed; Exit → removal logged.
- [ ] Looping macro + Stop Key F8 → F8 stops it.
- [ ] Placement: no "Allow Launching" switch; per-rule Launch alone gates.

## 6. Stage I — v0.42.0 (the twenty)

- [ ] Pin two windows → Save Scene… → Unpin All → Apply Scene… → both back. Palette lists the scene; Library kind Apply Pin Scene.
- [ ] Pin Memory on → 60% opacity → unpin → pin again → 60% + position.
- [ ] Timed Pin hotkey → released after N s (toast).
- [ ] Send To Next Monitor → same relative spot/size.
- [ ] Tray → Last Pinned Opacity → 60%.
- [ ] Zone Cooldown 5 → second hit within 5 s ignored (`in cooldown`); Schedule outside now → `outside its schedule`; Zone Hit Flash pulses.
- [ ] Dry Run → strip narrates, nothing sent; Pause/Resume hotkey freezes/resumes.
- [ ] Editor Note column; Hide Mouse Moves.
- [ ] Profile switch toast shows the diff.
- [ ] Idle Rule 1 min → fires; activity → reverts.
- [ ] Schedules… window covering now → switches; process rule beats it.
- [ ] Theme: High Contrast / Light / Dark — live. List what looks wrong in Light.
- [ ] Config History… lists backups; Restore has no review. Toast History… lists toasts.
- [ ] Welcome tour "Everything Optional Is Off" step.
- [ ] Minimal Mode turns every switch off.

## 6b. Stage J — v0.43.0 (layout round)

- [ ] Action row = three cards; nothing clipped; Zones row ≥ 300 px.
- [ ] Every former button still exists (Macros card / App card).
- [ ] Help CONTENTS chips; one "Zone Map" section mentioning middle-mouse scroll.
- [ ] Map hint / tooltip / tour mention MMB scroll; MMB actually pans.
- [ ] Settings → Performance → Hotkey Re-Bind Retry 5 → retries every ~5 s in the log.

## 6c. Stage K — v0.44.0 (this round's fixes and the footer contract)

- [ ] **After Action**: right-click an advanced zone → After Action → "Then Enter Stealth" → trigger → action runs, THEN stealth. "Nothing" → just the action. With "Zones Trigger Stealth" ON, an Exit zone goes stealth but an advanced zone still runs its action.
- [ ] **Every sub-GUI** (Macros, Actions, Placement, Auto-Pin, Profile Rules, Schedules, Known Anti-Cheat, Pinned, Stats, Profiles, Help, Config History, Toast History, step editor) has a footer button and **no title-bar ✕**. Editors: Cancel (Revert) undoes what you changed since opening; Save & Close keeps. Settings: Cancel (Revert) restores every setting to what it was when opened (try flipping Low Impact and a theme, then Revert).
- [ ] **Auto-Pin pickers**: Add From Focused Window (3 s) / Pick Running… (list + Browse .exe) create rules with the right process name.
- [ ] **Verbose logging** now shows: `Gesture … at (x,y)`, gating reasons, `no enabled zone … contains it`, `Placement … candidate:` lines, `Overlays re-asserted`, `Global hotkeys SUSPENDED/RESUMED`.
- [ ] Hotkey bind failure at startup logs a Win32 code (1409 = another program holds it) — check the `[Hotkey]` lines if F1/F10 fail to bind.

## 6d. v0.44.2 (license + metadata — no behavior change)

- [ ] Build restores and compiles with the new csproj properties (Authors/Copyright/PathMap). A Release build's crash bundle must show `/_/OnTop/...` paths, never a real folder.
- [ ] Help → last TOC chip "About & License" jumps to the new section; text shows the version, `Godswrath88`, the GPL notice and the no-warranty line.
- [ ] Exe → Properties → Details shows Company/Copyright as `Godswrath88` / GPL-3.0-or-later.

## 6e. v0.45.0 — Emergency stop (Stage L part 1)

- [ ] **Kill while healthy**: press the Kill combo → OnTop is gone instantly; the log's last line is `KILL — Kill hotkey: terminating OnTop now …`.
- [ ] **Exit while healthy**: Exit combo → normal clean exit (confirm dialog if enabled; `Exit hotkey pressed (e-stop thread) — handing to the UI thread` then `OnTop exited`).
- [ ] **Kill while FROZEN** (the real test): reproduce the freeze (open Pinned Windows, or whatever froze it) → wait for the UI to stop responding → Kill combo → process ends. Log shows `UI thread unresponsive for N s` before the KILL line.
- [ ] **Exit while FROZEN**: same, press Exit → after ~3 s the process ends; log shows `UI thread did not respond to Exit within 3 s — escalating`.
- [ ] **Pinned Windows opens without freezing**; with verbose logging on, `[Pins] Pinned Windows: scan started` / `scan done — N row(s) in X ms` appear. If it still freezes: attach the log — the watchdog line and the last `[Pins]` line are what pinpoint it.
- [ ] Hotkeys… → record a new combo for something else while Kill is armed: recording works; pressing the CURRENT Kill combo in the recorder stops OnTop (documented, intended).
- [ ] Tray → **Kill Now — No Cleanup** ends the process.
- [ ] Startup log line `Hotkeys: … (exit/kill on the e-stop thread)` has no `KILL NOT BOUND` suffix.

## 7. Regression spot-checks

- [ ] ⟳ retest — Classic Exit + Stealth zones fire (this was the detection bug). Test Mode: hit toast + flash; miss toast with the reason.
- [.] Ctrl+Alt+P; Ctrl+Alt+X; stealth toggle hotkey; auto-stealth watcher.
- [n] Auto-Pin rules scan (3 s / 10 s Low Impact).
- [n] Profiles create/switch/delete; Block Macros binds/unbinds.
- [n] Import zones with Run Command → review; restore from Backups → no review.
- [n] Hook latency sane; no "hook dropped" warnings.
- [.] Crash report prompt / draft recovery.

## 8. Environment-gated (I say explicitly when I test these)

- [n] Installer (`installer/OnTop.iss`).
- [n] Portable publish (`portable/publish-portable.cmd`).
- [n] Updater (dormant with empty URL; test manifest → notify → browser only).

---

**Report format:** item number, the mark, one line, log lines with the category in brackets. Batch cosmetic notes at the end. Attach the session log(s) from `%APPDATA%\OnTop\logs` — the three logs attached in the first round were what made the root causes findable.
