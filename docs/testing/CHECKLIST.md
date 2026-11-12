# OnTop — Test Checklist

> **Standing document, organized by feature.** Each check has a stable ID
> (`Z1`, `M4`, …) that never changes, so a report like "Z1 [x], M2 [.]" stays
> valid as the list grows. When a feature changes, edit its checks in place.
> Git history records earlier marks, so there are no per-version sections.
>
> **Failures go to GitHub Issues**, one per failure (use the **Bug report**
> template). An item that links `#N` has an open issue. Close the issue when the
> retest passes; if it still fails, comment on it.
>
> **A test round** = a **Test pass** issue (template) for one build. It holds
> the smoke test below plus any items you choose. Marks for the full list can
> also be typed here and committed.
>
> **The easy way: a guided round.** In a Claude Code session, say "let's test".
> It hands over one item at a time as a few short steps; answer `p` or say what
> happened in plain words. It reads the logs, writes the marks and dated notes
> here, and drafts the issues (the `ontop-test-round` skill). Testing alone:
> fill in the **Round sheet** below and it gets transcribed next session.

## Round sheet (the inbox; rewritten for each build)

Type after the dash: `p` for a pass, `s` to skip, or what happened in your own
words. No brackets, no dates, no log lines: those get filled in from the logs
when the sheet is transcribed onto the items below, which stay the record.
An ID with lettered parts (A3a, A3b, …) can take one answer for the lot
(`p`) or one per letter (`a p, b p, c revert didn't undo the theme`).

**Build: 0.45.5** · run with Ctrl+F5 (no debugger) · Verbose Logging on

Smoke (stop and report if one fails):
- B1 builds clean —
- B2 launches, settings intact —
- B14 View Logs opens —
- E1 Kill hotkey —
- E2 Exit hotkey —
- Z1 a zone fires on the desktop (#2) —
- P1 pin, opacity, unpin —
- E5 Pinned Windows opens, no freeze (#14) —
- S1 Stealth removes the hooks, and back —
- A1 accent re-tints the app (#1) —

Retests (fixed in code, waiting on you):
- A3 every sub-window: opens, footer, no ✕, Revert —
- Z3 Exit and Stealth zones fire (#2) —
- Z6 overlays stay above OnTop windows (#5) —
- P2 orange frame on click-through (#13) —
- H2 duplicate hotkey refused (#12) —
- M1 macro status strip lifecycle (#3) —
- M2 recording has no Ctrl+Alt+M in it (#4) —
- L2 placement moves the window back (#6) —
- L3 edit a placement rule by hand (#7) —
- L4 placement On Start + Delay (#8) —
- L6 placement zone doesn't enter Stealth (#9) —
- L9 reopen Placement after closing mid-edit (#10) —
- AL1 library Run Command runs (#11) —
- AL2 library hotkey capture, duplicates refused (#12) —
- E3 Kill while frozen, only if something freezes (#14) —

Newer items, never tested:
- A11 "do this first" hint popups —
- A13 hint popup settings —
- A14 Settings footer stays visible —
- A12 overlay legend toast wording —
- B11 thumbtack icon everywhere —
- B12 Export Diagnostics over an existing file —
- B13 terminal build leaves OnTop running —
- H9 freed hotkey re-binds with a toast —
- H10 Stealth hotkey binds once —
- H11 panic key survives the Hotkeys dialog —
- L12 administrator window question —
- L13 administrator question vs Cancel (Revert) —

## Legend (type one character inside the brackets)

| Mark | Meaning | When to use it |
|---|---|---|
| `[ ]` | not tested yet | default |
| `[.]` | pass | it did what the item says |
| `[x]` | **fail** | it didn't. Open an issue (or add a `>` note): what you did → what happened → what you expected (+ log lines) |
| `[~]` | partial | works, but with a problem worth a `>` note (cosmetic, wording, edge case) |
| `[n]` | not applicable / skipped on purpose | you chose not to test it this round |
| `[b]` | blocked | couldn't test because another item failed; name it in a `>` note |
| `[?]` | unclear | you didn't know how to test it; say what was confusing |

- **`[!]`** in the item text (and the `blocker` label on issues) = a failure
  here cascades into many other items. Test and report those first.
- **`⟳ retest`** (and the `retest` label) = reported failing, fixed in code,
  waiting for a confirming test.
- **Lettered sub-checks** (`A3a`, `A3b`, …) split an item that makes several
  claims: mark the letters. The parent's mark is the summary (all pass → `[.]`,
  any fail → `[x]`, otherwise `[~]`).
- **"How:"** lines give the steps for items that aren't obvious.
- **Log filtering:** View Logs → tick the category shown in brackets (e.g.
  `[Detection]`). Turn on **Verbose Logging** (Settings → Logging) for a test
  session: it logs *why* a gesture was ignored, placement candidates, overlay
  re-asserts and hotkey bind errors.
- **Logs are private.** They contain window titles and process names. In an
  issue, paste only the relevant lines, never a whole log file.

---

## Smoke test (every build, ~5 minutes)

Run these first; if any fail, stop and report.

1. **B1**: builds clean.
2. **B2**: launches; settings, zones and hotkeys are intact.
3. **B14**: View Logs opens (the rest of the list leans on it).
4. **E1**, **E2**: Kill and Exit hotkeys work.
5. **Z1**: a zone fires on the desktop.
6. **P1**: pin a window, change its opacity, unpin.
7. **E5**: Pinned Windows opens without freezing.
8. **S1**: Stealth on → hooks removed in the log; off → back.
9. **A1**: an accent change re-tints the app.

---

## B. Build & startup

- [.] **B1 [!] Compiles clean** on Windows (NuGet restore succeeded; `System.Security.Cryptography.ProtectedData` is the only package). Paste every error verbatim.
  > 11-09: v0.45.1, `dotnet build` Debug: 0 errors, 0 warnings (compiled in a development session on the Windows machine).
- [.] **B2** First launch: settings migrate silently (now → schema 30); no "config conflict" prompt; no "powerful fields changed" toast on the **second** launch.
- [.] **B3** Existing zones, profiles, hotkeys, macros all present.
- [.] **B4** Hotkeys… shows **Ctrl+Alt+M** on "Record / Stop Macro".
- [ ] **B5** `settings.json` (installed mode): macros stored as `EncryptedEvents` base64, no plain `Events`; macros still play after restart. Portable mode: plain `Events`.
  > 09-24: installed mode not tested yet.
- [.] **B6** Help opens; CONTENTS chips; amber ⚠ paragraphs present.
- [ ] **B7** A Release build's crash bundle shows `/_/OnTop/...` paths, never a real folder (csproj `PathMap`).
- [ ] **B8** Help → last TOC chip "About & License" jumps to its section; text shows the version, `Godswrath88`, the GPL notice and the no-warranty line.
- [ ] **B9** Exe → Properties → Details shows Company/Copyright as `Godswrath88` / GPL-3.0-or-later.
- [ ] **B10** **Verbose logging** shows: `Gesture … at (x,y)`, gating reasons, `no enabled zone … contains it`, `Placement … candidate:` lines, `Overlays re-asserted`, `Global hotkeys SUSPENDED/RESUMED`.
- [ ] **B11** Icon (0.45.3): the red thumbtack shows on the exe in Explorer, the taskbar, Alt+Tab and the tray, and looks crisp (not blurry) in the tray.
- [ ] **B12** Log Viewer → **Export Diagnostics…** to a file that already exists → confirm Replace → the zip is written (no error) and opens with settings.json, the log, diag.txt and log-tail.txt. Type a bare name (no extension) → saved as .zip.
- [ ] **B14 [!]** View Logs (0.45.5): open the Log Viewer from the main window → it opens and lists log lines with a colored level badge on each row; no crash. Tick a category and Verbose Logging → still fine.
  > 11-10: v0.45.3, crashed on open (`'DynamicResourceExtension' cannot be set on the 'Converter' property`). Found in the crash bundle on 11-12; fixed in 0.45.5.
- [ ] **B13** Build behavior (0.45.4): with OnTop running, `dotnet build` from a terminal leaves it running; starting a build in Visual Studio still closes it first (exe not locked).

## E. Emergency stop & exit

Default combos: **Exit = F10**, **Kill = Ctrl+Alt+Q** (Hotkeys… to change).

- [.] **E1 Kill while healthy**: press the Kill combo → OnTop is gone instantly; the log's last line is `KILL — Kill hotkey: terminating OnTop now …`.
  > 11-10: v0.45.2, pass (log ends with the `KILL —` line).
- [ ] **E2 Exit while healthy**: Exit combo → normal clean exit (confirm dialog if enabled; `Exit hotkey pressed (e-stop thread) — handing to the UI thread` then `OnTop exited`).
- [ ] **E3 Kill while FROZEN** (the real test): reproduce the freeze (open Pinned Windows, or whatever froze it) → wait for the UI to stop responding → Kill combo → process ends. Log shows `UI thread unresponsive for N s` before the KILL line. (#14)
- [ ] **E4 Exit while FROZEN**: same, press Exit → after ~3 s the process ends; log shows `UI thread did not respond to Exit within 3 s — escalating`.
- [ ] **E5 [!] Pinned Windows opens without freezing**; with verbose logging on, `[Pins] Pinned Windows: scan started` / `scan done — N row(s) in X ms` appear. If it still freezes: the watchdog line and the last `[Pins]` line are what pinpoint it. (#14)
- [ ] **E6** Hotkeys… → record a new combo for something else while Kill is armed: recording works; pressing the CURRENT Kill combo in the recorder stops OnTop (documented, intended).
- [ ] **E7** Tray → **Kill Now — No Cleanup** ends the process.
- [ ] **E8** Startup log line `Hotkeys: … (exit/kill on the e-stop thread)` has no `KILL NOT BOUND` suffix.

## A. Appearance, layout & help

- [ ] **A1 [!] ⟳ retest — Accent**: Settings → Appearance → pick an accent → whole app re-tints **immediately**. Also try Theme (High Contrast / Light / Dark), which uses the same mechanism. (#1)
  > 09-24: "accent doesn't change colors of anything".
- [ ] **A2** Theme: High Contrast / Light / Dark switch live. List what looks wrong in Light.
- [~] **A3 [!] ⟳ retest** **Every sub-GUI** (Macros, Actions, Placement, Auto-Pin, Profile Rules, Schedules, Known Anti-Cheat, Pinned, Stats, Profiles, Help, Config History, Toast History, step editor):
  - [ ] **A3a** each one opens.
  - [ ] **A3b** each has a footer button and **no title-bar ✕**.
  - [ ] **A3c** Editors: Cancel (Revert) undoes what you changed since opening; Save & Close keeps.
  - [ ] **A3d** Settings: Cancel (Revert) restores every setting to what it was when opened (try flipping Low Impact and a theme, then Revert).
  > 11-10: v0.45.2, Placement, Action Library, Auto-Pin and Pinned crashed on open (`already the logical child of another element`). Every footer window was affected since 0.44.0; fixed in 0.45.3. Open each one once.
  > 11-10: 0.45.3 fix, every sub-window opens. Footer buttons, missing ✕ and Cancel (Revert) not tested yet.
- [ ] **A11** "Do this first" hint popups (0.45.3):
  - [ ] **A11a** with no zone selected, press **Set Group**, **Remove** and **Duplicate** → each fades in a hint centered on the main window, then fades out by itself (no status-bar-only message, no silent no-op, nothing to click).
  - [ ] **A11b** You can keep typing while it's up. Click dismisses it early; hovering keeps it up.
  - [ ] **A11c** With macros off, **Record** → hint. Action Library → edit an action whose picker has nothing to pick → hint.
  - [ ] **A11d** Does it look professional? Note anything that feels off.
- [ ] **A13** Settings → Appearance → **Hint Popups**:
  - [ ] **A13a** Preview shows a sample; typing 2 vs 10 in **Stay Up For** visibly changes how long it stays.
  - [ ] **A13b** Letters can't be typed; 0, blank or 99 snaps back with a "1–60" toast.
  - [ ] **A13c** Switch **off** → the seconds box greys out and the same hints appear as a toast instead.
  - [ ] **A13d** Both settings survive a restart.
- [ ] **A14** Settings: shrink nothing, just scroll. **Save & Close** and **Cancel (Revert)** stay visible at the bottom the whole time; scrolled to the end, the Data section sits right above them.
- [ ] **A12** Tray → **Show Overlay Legend** with no overlays on → toast says "Overlay Column", not "Dot".
- [ ] **A4** Action row = three cards; nothing clipped; Zones row ≥ 300 px.
- [ ] **A5** Every former button still exists (Macros card / App card).
- [ ] **A6** Help CONTENTS chips; one "Zone Map" section mentioning middle-mouse scroll.
- [ ] **A7** Map hint / tooltip / tour mention MMB scroll; MMB actually pans.
- [.] **A8** Wording: speed %, Wobble px, "Block Macros In Profile X", repeat once/N×/until stopped.
- [.] **A9** Welcome tour: current hotkeys; Get Started / Add 4 Starter Zones open the main window.
- [ ] **A10** Welcome tour "Everything Optional Is Off" step.

## Z. Zones & overlays

- [ ] **Z1 [!] ⟳ retest — Zone detection at all**: with Desktop Only ON and overlays visible (any zone with its dot on). (#2)
  - [ ] **Z1a** double-click inside a zone on the DESKTOP → fires.
  - [ ] **Z1b** double-click inside the zone with a program window under it → does NOT fire; verbose log says `gated by Desktop Only — under cursor: '<title>'`.
  - [ ] **Z1c** In Test Mode the miss toasts "Not On Desktop (<process>)".
  > 09-24: "double clicks aren't detecting".
- [ ] **Z2** **Gestures**: Middle Click zone fires only on middle; Triple Left on the third click; Ctrl+double only with Ctrl; Double Right on right; Shift+double / Alt+double. A classic zone ignores those.
  How: right-click a zone → Trigger Gesture. Verbose log line `Gesture <name> at (x,y)` appears for every dispatch.
- [ ] **Z3 ⟳ retest** — Classic Exit + Stealth zones fire. Test Mode: hit toast + flash; miss toast with the reason. (#2)
- [ ] **Z4** **After Action**: right-click an advanced zone → After Action → "Then Enter Stealth" → trigger → action runs, THEN stealth. "Nothing" → just the action. With "Zones Trigger Stealth" ON, an Exit zone goes stealth but an advanced zone still runs its action.
- [ ] **Z5** Zone Cooldown 5 → second hit within 5 s ignored (`in cooldown`); Schedule outside now → `outside its schedule`; Zone Hit Flash pulses.
- [ ] **Z6 ⟳ retest — Overlays above OnTop GUIs**: open the Macros window over a zone → Peek / map ping (◎ in the list) → overlays draw ABOVE the window, and stay above after the ping ends. Click into the main window → overlays still above within half a second. (#5)
  > 09-24: overlays buried under the main GUI after a ping and didn't come back.
- [n] **Z7** Zone.Clone fix: Duplicate a Run Command zone → duplicate keeps the command.

## P. Pinning

- [.] **P1** Pinned Windows −/+ opacity; Click-Through; revert.
- [ ] **P2 ⟳ retest — Click-through feedback**: Click-Through on a row → an **orange frame** appears around that window and the row's State column reads e.g. `100% · CLICK-THROUGH`; off → frame gone. "Orange Frame On Click-Through" switch turns frames off/on for all. (#13)
- [n] **P3** Hotkeys: Pin Under Cursor, Opacity +/−, Click-Through Under Cursor, Cycle Corner.
- [ ] **P4** Pin two windows → Save Scene… → Unpin All → Apply Scene… → both back. Palette lists the scene; Library kind Apply Pin Scene.
- [ ] **P5** Pin Memory on → 60% opacity → unpin → pin again → 60% + position.
- [ ] **P6** Timed Pin hotkey → released after N s (toast).
- [ ] **P7** Send To Next Monitor → same relative spot/size.
- [ ] **P8** Tray → Last Pinned Opacity → 60%.
- [ ] **P9** **Auto-Pin pickers**: Add From Focused Window (3 s) / Pick Running… (list + Browse .exe) create rules with the right process name.
- [n] **P10** Auto-Pin rules scan (3 s / 10 s Low Impact).

## H. Hotkeys & hold chords

- [.] **H1** Ctrl+Alt+P; Ctrl+Alt+X; stealth toggle hotkey; auto-stealth watcher.
- [ ] **H2 ⟳ retest** — Hotkeys… dialog: press a combo that a library action already uses → refused the moment you press it (toast names both), box reverts. (#12)
- [ ] **H3** Hotkey bind failure at startup logs a Win32 code (1409 = another program holds it); check the `[Hotkey]` lines if F1/F10 fail to bind.
- [ ] **H4** Settings → Performance → Hotkey Re-Bind Retry 5 → with Verbose Logging on, `Still blocked: …` appears every ~5 s (0.45.3: DEBUG, not ERROR).
- [ ] **H9** Hotkey freed (0.45.3): with a combo held by another app (e.g. Ctrl+Alt+N), close that app → within one retry a toast says "Ctrl+Alt+N (quick record) Is Now Active — The Other App Released It", and the hotkey works. Clearing a blocked combo in Hotkeys… instead shows no such note.
- [ ] **H10** Stealth hotkey (0.45.3): set a Stealth combo in Hotkeys… → restart → no "In Use By Another App" warning for it, the log has no `Failed to bind stealth`, and pressing it toggles Stealth once (one toast, not two).
- [ ] **H11** Panic key safety (0.45.3 review): open Hotkeys…, and while it's open trigger a zone set to Run Macro; then close Hotkeys… → Ctrl+Alt+X still stops the macro, and after it ends the Always On Top hotkey still works.
- [ ] **H5** Cheat Sheet hotkey → card; Command Palette hotkey → type "peek" → Enter.
- [ ] **H6** Features → Hold Chords on; Hotkeys… Hold To Peek → overlays while held; releasing Ctrl alone ends it. Log `Hold-chord keyboard hook INSTALLED`.
- [ ] **H7** Hold To Pin → topmost while held, released after; an already-pinned window untouched.
- [ ] **H8** Hold Chords off → hook removed; Exit → removal logged.

## S. Stealth & quiet modes

- [ ] **S1 [!]** Stealth → `hook REMOVED (disarmed)`; leave Stealth → back.
- [ ] **S2** Do Not Disturb + watched process running → routine toasts silent; Errors show; Toast History shows `[DND]`.

## M. Macros

- [ ] **M1 ⟳ retest — Status strip lifecycle**: (#3)
  - [ ] **M1a** turn Macros on with the Macros window CLOSED → NO strip.
  - [ ] **M1b** Open the Macros window → strip appears. Close it → strip gone.
  - [ ] **M1c** Press Ctrl+Alt+M with the window closed → strip appears (RECORDING), stays ~4 s after stop, hides.
  - [ ] **M1d** Press × on the strip → hidden until the next recording.
  - [ ] **M1e** Bind "Show / Hide Macro Status Strip" in Hotkeys… → toggles it; the strip shows that hotkey.
  > 09-24: strip shouldn't be up permanently; needs a close keybind; should close with the sub-GUI; popped on script start.
- [ ] **M2 ⟳ retest — Ctrl+Alt+M** → red marquee + elapsed + step count; again → SAVED → READY. Open the step editor: the recording must NOT start with Ctrl/Alt/M key-ups or end with their key-downs. (#4)
- [.] **M3** Main window MACROS card (Macros…, ● Record, ■ Stop); ■ Panic Stop in the Macros window.
- [.] **M4** Macros window opens large enough, remembers size/position, stays on top.
- [.] **M5** Step editor: search / delay edit / Delete Selected / Remove Mouse Moves / Save / Cancel.
- [.] **M6** Known Anti-Cheat… list with "(running now)".
- [n] **M7** Kill switch (mid-run watched-process appearance → master toggle OFF; refusal at start does NOT trip it).
  How: start a looping macro (Repeat 0), then launch a program whose name is in Settings → Stealth watched list (e.g. add `notepad` to the list first). Expect: playback stops, "Macros Enabled" turns off, toast says to re-arm.
- [ ] **M8** **Countdown** (Macros window, "Countdown before play"): 0 → playback starts instantly; 3 → the strip shows "STARTING IN 3…" and counts 3-2-1.
- [ ] **M9** **Then Play…** → second macro after a natural finish; **Relative** → clicks follow a moved anchor window.
  How (Relative): record a macro that clicks inside Notepad; set Relative ON on the row; move Notepad; play → the clicks land in the moved window.
- [ ] **M10** Editor → Insert Wait For Window ("Calculator", 10 s) → playback pauses until Calculator opens; timeout stops with a toast.
- [ ] **M11** Looping macro + Stop Key F8 → F8 stops it.
- [ ] **M12** Dry Run → strip narrates, nothing sent; Pause/Resume hotkey freezes/resumes.
- [ ] **M13** Editor Note column; Hide Mouse Moves.
- [n] **M14** Profile with Block Macros → macro hotkeys unbind while active, rebind after.

## L. Window placement & launcher

- [.] **L1** Features → Window Placement on; APP → Placement… opens.
- [ ] **L2 ⟳ retest** — Placement capture and apply: (#6)
  - [ ] **L2a** **Capture From Focused Window** (3 s) → rule appears.
  - [ ] **L2b** **Move the window, then Apply Selected → it returns.**
  - [ ] **L2c** Tick Max → maximizes there.
  How: use a NORMAL program (Notepad, Explorer). Hardware tools such as GPU tuning utilities run as administrator, and a non-elevated OnTop **cannot** move them (Windows forbids it); the log/toast says "access denied — runs as administrator".
  > 09-24: placement didn't move the window; the log claimed "applied".
- [ ] **L3 ⟳ retest** — **Edit a rule by hand**: double-click a row (or Edit Selected… / right-click → Edit Rule…) → full editor with Pick… (running processes / Browse .exe), monitor, X/Y/W/H, Maximize, On Start, **Delay**, Launch path, Launch, wait. Save → grid updates. Hover a column header → description. (#7)
- [ ] **L4 ⟳ retest** — **On Start** (+ Delay 5): restart OnTop → log "waiting 5s first" then applied ~9 s after launch. (#8)
- [n] **L5** Apply All hotkey; inert while the feature is off.
- [ ] **L6 ⟳ retest** — Zone → Set Action → **Apply Placement** → triggering the zone places the window and does **NOT** enter Stealth (unless the zone's After Action says so). (#9)
- [n] **L7** Launch: rule with a Launch Path, Launch unticked → "No Matching Window", nothing launches; ticked → launches, placed within the wait. Test Mode suppresses.
- [n] **L8** Import/restore with a launch path → Import Review lists "Placement Launch", arrives blanked.
- [ ] **L9 ⟳ retest** — **Reopen Placement… after closing it** (edit a cell first, then close while the cell is still in edit mode) → no crash. (#10)
- [ ] **L10** **Placement window footer**: change something → Cancel (Revert) → change is gone. Change → Save & Close → kept. No ✕ in the title bar.
- [ ] **L11** Placement has no "Allow Launching" switch; per-rule Launch alone gates.
- [ ] **L12** Admin window (0.45.3; GPU Tweak III works):
  - [ ] **L12a** rule with On Start → restart OnTop with GPU Tweak running → one window "can't move its window … runs as administrator", focus stays where it was.
  - [ ] **L12b** **Keep Rule** with the box unticked → asked again next launch (not again in the same session: Apply Selected → toast only).
  - [ ] **L12c** Tick **Don't ask again** + Keep → next launch only a WARN log line, no window, no toast.
  - [ ] **L12d** Rule editor → **Ask if it runs as admin** on → asks again.
  - [ ] **L12e** **Turn This Rule Off** → the rule's On box clears.
- [ ] **L13** Admin window: with the question open, press **Cancel (Revert)** in the Placement window, then answer **Turn This Rule Off** → the rule really ends up off (grid + after restart). Close the question with ✕ instead → nothing changes and it asks again next launch.

## AL. Action Library

- [ ] **AL1 ⟳ retest** — Features → Action Library on → Actions… → New → Run Command → Payload `notepad` → **Run** → Notepad opens; log `[Zones] Library action 'X' (library window): Run: notepad`. (#11)
  > 09-24: the library's Run button was never pressed per the log; press Run in the window this time.
- [ ] **AL2 ⟳ retest** — Hotkey… (a **capture dialog**: press keys) → assign → press it → runs. Press a combo already used by a macro or app hotkey → refused **in the dialog** with both names; nothing executes while the dialog is open. (#12)
- [n] **AL3** Each kind from Run: Kill (protected refused), Toggle Window, Play Macro, Apply Placement, Switch Profile, Pause/Resume, Peek, Toggle Stealth, Pin Under Cursor, Apply Pin Scene.
- [.] **AL4** Zone → Set Action → Library Action → triggers it; delete the action → zone falls back to Exit.
- [n] **AL5** Feature off → hotkeys dead; zone toasts "Action Library Is Off".
- [ ] **AL6** **Library footer**: Cancel (Revert) undoes a new action; Save & Close keeps.

## R. Profiles & automation

- [n] **R1** Profiles create/switch/delete.
- [n] **R2** Profile Rules… → rule for a running process → switches; reverts when gone.
  How: Settings → Features → Profile Rules on → Profile Rules… → Add → Process `notepad`, Profile = a second profile you created. Open Notepad → within 5 s the profile switches (toast with diff). Close Notepad → reverts.
- [n] **R3** Display Profiles: Bind This Layout → change monitor config → switches.
- [ ] **R4** Profile switch toast shows the diff.
- [ ] **R5** Idle Rule 1 min → fires; activity → reverts.
- [ ] **R6** Schedules… window covering now → switches; a process rule beats it.

## C. Config, data & maintenance

- [n] **C1** Import zones with Run Command → review; restore from Backups → no review.
- [ ] **C2** Config History… lists backups; Restore has no review. Toast History… lists toasts.
- [ ] **C3** Minimal Mode turns every switch off.
- [n] **C4** Low Impact Mode → Task Manager shows Below Normal; log line.
- [ ] **C5** Statistics → bars; Export CSV….
- [.] **C6** Crash report prompt / draft recovery.
- [n] **C7** Hook latency sane; no "hook dropped" warnings.

## X. Environment-gated (I say explicitly when I test these)

- [n] **X1** Installer (`installer/OnTop.iss`). From v0.45.4 the installer on each GitHub Release is built by CI; test that file.
- [n] **X2** Portable: the `OnTop-x.y.z-portable.zip` on the release (or `portable/publish-portable.cmd`). Its SHA-256 matches `SHA256SUMS.txt`.
- [n] **X3** Updater (dormant with empty URL; test manifest → notify → browser only).

---

**Report format:** a guided round or the Round sheet at the top (plain words;
the logs get read for you), or an issue per failure (Bug report template).
Batch cosmetic notes at the end. Logs live in `%APPDATA%\OnTop\logs` (portable:
`<exe folder>\Data\logs`). Attaching whole logs to a chat session is fine; on
GitHub, paste only the relevant lines.
