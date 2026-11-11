# What's New

## Session 28 — v0.45.3 — Hotfix: sub-windows crashed on open

**What was reported:** opening Placement "glitched everything out". The whole
PC and the cursor lagged, and neither F10 nor Ctrl+Alt+Q stopped OnTop; it
had to be stopped from Visual Studio. Run again without the debugger, each
sub-window button showed "Unexpected Error — Details Were Logged", and the
fourth one crashed OnTop.

**What it actually was:**
- Every window with the Save & Close / Close footer (added in 0.44.0) threw
  on open. Each window sets `Content = root` and then calls
  `EditorFooter.Wrap(this, root, …)`, which added `root` to a new panel while
  the window still owned it. WPF refuses that ("already the logical child of
  another element"). The stack traces pointed to `EditorFooter.cs:35` from
  Placement, Action Library, Auto-Pin and Pinned Windows. Wrap now detaches
  the content first; one fix covers all 13 windows.
- **The lag and the dead hotkeys came from the debugger, not OnTop.** Under
  Visual Studio, the exception paused every thread in the process, including
  the e-stop thread. OnTop's low-level mouse/keyboard hooks stopped
  answering, so Windows waited on them for every input event. Without the
  debugger, OnTop's own handler recovered from the first three errors; the
  fourth was fatal by design.
- **The crash-report prompt failed too:** the diagnostics zip kept `diag.txt`
  open while creating the next entry, which `ZipArchive` doesn't allow. Fixed.
  The bundles written during this test are probably incomplete. The same zip
  code (also used by Log Viewer → Export) also failed when replacing an
  existing file, could fail if a log line was being written at that moment,
  and two crash bundles in the same second collided. All fixed. Export now
  only writes `.zip` files and replaces the target only once the new zip is
  complete, so a mistyped file name can't wipe out a real file.
- **#14 (Pinned Windows freeze, reported on 0.44.x)** is very likely this same
  bug: Pinned Windows throws the same exception. A retest will confirm it.

**Confirmed in the same session:** Kill while healthy (E1) works; the log ends
with the `KILL —` line. With the fix, every sub-window opens.

**Also in this build, from the same test:**
- "You need to do X first" messages were easy to miss: small grey text in the
  main window's status bar (Set Group with no zone selected) or a toast in the
  corner. They're now a hint card that fades in over the window you clicked
  in (maximized windows included), stays a few seconds, and fades out.
  There's nothing to click. It never takes focus, not even when you click it
  to dismiss it early, so you can keep typing; hovering keeps it up. **Settings → Appearance → Hint Popups** turns it on or off
  (off shows the hint as a toast instead). Next to it, you type how many
  seconds it stays up: digits only, 1–60, default 4. A 0, a blank or anything
  out of range snaps back to the last good value. A Preview button shows a
  sample. Remove and Duplicate with no zone
  selected used to do nothing at all; they now say why. Same for Record Macro
  while macros are off, and the Action Library picker when there's nothing to
  pick. Hints only come from clicks in OnTop's own windows; anything a hotkey
  or a background check triggers stays a toast. With Hint Popups off, the
  toast is a Warning, so Do Not Disturb hides it while a watched game runs.
- The "No Overlays Active" toast still called the Overlay column "Dot". Fixed.
  "Drag dots to move" (map hint, overlay legend) is left alone: it describes
  the dot you actually drag, not the option.
- **Settings' Save & Close and Cancel (Revert) stay on screen.** They used to
  sit at the very end of the scrolling page. Now they're docked below the
  scroll area with a thin divider, so they're always visible, and scrolling
  to the end brings the last section right down to them. When Settings opens,
  it's also fitted to the monitor it lands on, so a shorter second screen
  can't push the buttons below the edge. The other sub-windows and Hotkeys
  already kept their buttons outside the scroll area.
- **You hear about it when a blocked hotkey frees up.** Windows won't say
  which program holds a hotkey, and finding out would mean injecting code
  into other programs, which OnTop never does. Instead, OnTop now tells you
  the moment it gets each hotkey back: "Ctrl+Alt+N (quick record) Is Now
  Active — The Other App Released It". To find the culprit, close a suspect
  app and wait one retry (30 s by default; Settings → Performance). Before
  this, the only note came once every conflict had cleared, and it didn't
  say which one. Retry ticks no longer write to the log at normal levels
  (the hotkey summary, "failed to bind" and macro/library warnings are DEBUG
  on retries). Two older hotkey bugs surfaced in review and are fixed: the
  Stealth hotkey was registered twice, so anyone with a Stealth combo had a
  hotkey that looked permanently blocked; and hotkey ids climbed on every
  retry until, after about a day of a persistent conflict, they'd run out of
  Windows' range. A combo OnTop can't even read now gets one "isn't a valid
  key combination" toast instead of being retried forever as "in use". The
  "is now active" note covers the main hotkeys and the toggle hotkeys; macro
  and library hotkeys only log.
- **Placement asks what to do about administrator windows.** OnTop can't
  move a window that belongs to a program running as administrator (GPU
  Tweak III here), and it won't run as administrator to get around that.
  Before, a startup rule like that failed with an error every launch. Now the
  first failure in a session opens a small window explaining why, with
  **Turn This Rule Off** or **Keep Rule**, plus **Don't ask again for this
  placement**, so keeping the rule doesn't mean a question every launch. It
  doesn't take focus. While Do Not Disturb is holding notifications (a
  watched game is running) it's only a toast, and the question waits for a
  later failure. Closing it without answering saves nothing. Several at once
  step down the screen instead of stacking. A rule set to "don't ask" only
  notes the failure in the log. The rule editor has a matching switch, **Ask
  if it runs as admin**, to turn the question back on, even in the same
  session. I also looked hard at a separate administrator "window
  mover" helper and shelved it: with a per-user install, any program could
  swap that helper and get full admin the next time the prompt is accepted.
  The full reasoning is in ROADMAP under Security / Distribution.
- **OnTop has its own icon.** A generic red push-pin, drawn from basic shapes
  by a script in `tools/make_icon.ps1`, so it's original and ships under
  OnTop's own license; no icon pack or third-party art. It has the standard
  sizes from 16 px (tray) up to 256 px (Explorer); in-between sizes are
  scaled from the nearest one. The old `thumbtack.ico` had no recorded source, a single
  256 px image and no small sizes, and it was stored in a format older
  loaders couldn't read. Users can still drop their own `thumbtack.ico` into
  the `assets` folder to change the tray icon.

**Settings schema 29 → 30.** The three new fields (Hint Popups on/off and
seconds, and the per-rule "don't ask again") get a no-op migration. The bump
matters on the way back: an older OnTop now sees this config as newer and
won't save over it and drop those fields. Rolling back to 0.45.2 or 0.43.0
shows the "newer config" notice; that's expected.

**Independent review.** Before delivery, a separate reviewer that hadn't seen
any of this reasoning went over the whole change. It built clean (0 errors, 0
warnings) and disproved several of my claims; everything above reflects the
fixes. A second fresh reviewer then checked just the fixes and caught one
that was too clever: handing out the lowest free hotkey id let a stale id
unregister someone else's hotkey (worst case: the macro panic key during
playback, while the Hotkeys editor was open). Replaced with ids that climb and
wrap, plus clearing every id when its hotkey is released. It also caught
Export truncating a file before the zip was complete, Esc in the admin
question counting as an answer, hint placement on mixed-scaling monitors,
and Help overclaiming which hotkeys get a "now active" note. All fixed.
Not fixed, on purpose:
- Revert in the Placement window restores the rules as they were when it
  opened, so it also undoes an admin-window answer given while it was open.
  That's what Revert means; the answer is still applied correctly by rule id.
- On a PC where Do Not Disturb is on AND a watched process (e.g. a resident
  anti-cheat service) is always running, the admin-window question can never
  appear, and each startup shows an Error toast. Way out: switch off "Ask if
  it runs as admin" in that rule's editor, or turn the rule off.
- The tray icon size is read once at startup; after a DPI change it may look
  slightly soft until OnTop restarts.
- If another app grabs and releases the same combo repeatedly, each release
  gets its own "is now active" note.
- The admin-window questions always appear on the primary monitor.
- Settings on a screen shorter than about 340 px can still push its buttons
  off the bottom; moving Settings to another monitor after it opens doesn't
  refit it.
- Exit and Kill (e-stop thread) still report an unreadable combo as "in use"
  and retry it; they're set from Hotkeys…, which only records valid combos.

**Still open:** a real UI freeze with the e-stop thread alive (E3/E4) hasn't
been tested; this "freeze" was the debugger.

## Session 28 — v0.45.2 — Testing moves to GitHub

The project now lives in a git repo instead of passed-around zips, so testing
moves there too. TESTING.md was organized by stage (D, E, F … 6e), which mixed
history with the checklist and buried each feature's checks across several sections.

- **`docs/testing/CHECKLIST.md`** replaces it, organized by **feature**
  (Zones, Pinning, Macros, Placement, …). Each check has an ID that never
  changes (Z1, M2), so reports stay valid as the list grows. A smoke-test section at
  the top is the five-minute pass for every build. Every existing mark and note
  came along.
- **Failures are GitHub issues now.** The 13 open retest items and the
  still-unexplained Pinned Windows freeze are issues #1–#14. Labels: `retest`
  (fixed, waiting for a confirming test), `blocker` (a failure cascades),
  `needs-log`. Checklist items link their issue.
- **Issue templates:** a Bug report form that asks for version, area, steps
  and only the relevant log lines (logs contain window titles, so nothing is pasted
  whole into a public issue), and a Test pass form with the smoke test as
  checkboxes. Blank issues are off; security reports are pointed to private
  reporting.
- **`docs/TROUBLESHOOTING.md`** for anyone who downloads OnTop: what to do when
  it freezes, where the settings and logs are, how to recover or reset settings,
  why some windows can't be pinned, hotkey conflicts, and zone gating.
- 0.45.1 was compiled clean (0 errors, 0 warnings) in a development session
  on the Windows machine. That's the first build check that wasn't run by hand.

No code or behavior change. Schema stays 29.

## Session 28 — v0.45.1 — Vulnerability reporting (docs only)

The project now lives in a public GitHub repo with private vulnerability
reporting turned on, but nothing told people to use it, so a security problem
could have been posted as a public issue that anyone could read before it was fixed.

- **`SECURITY.md` now starts with "Reporting a vulnerability":** Security tab →
  **Report a vulnerability**, plus what to include and what counts as in scope.
  The button only shows for visitors; as the owner, I see the incoming reports
  list instead.
- No code or behavior change. Schema stays 29. v0.45.0 is still not compiled,
  so this build's status is the same as 0.45.0's.

## Session 28 (Stage L, part 1) — v0.45.0 — The Emergency Stop

Clicking the Pinned Windows button froze OnTop — and the Exit hotkey did nothing,
because every hotkey was delivered to the same frozen thread. That second part is
the real bug: a stop function that depends on the thing it is supposed to stop is
not a stop function.

- **Exit and Kill now have their own thread.** `Services/EmergencyStopService`
  runs a tiny Win32 message loop on a thread that does nothing else; it
  registers the two combos with a NULL window, so Windows posts their WM_HOTKEY
  to *that* queue — the WPF dispatcher can be dead and they still arrive.
  - **Kill**: writes one log line (synchronously, so it is on disk), then
    `TerminateProcess`. Not `Environment.Exit` — that runs exit handlers on a
    runtime that may be wedged. The OS drops hooks and hotkeys when the process
    dies. Pinned windows stay pinned, exactly like Kill always did.
  - **Exit**: hands the normal clean shutdown to the UI thread (exception-
    process check, confirm-before-exit, settings save all still apply). If the
    UI thread has not even *started* that handler within 3 s, it escalates to
    the Kill path. Slow is fine; hung is not.
  - Kill stays armed while a hotkey recorder is open (Exit is released so it
    can be re-recorded). Pressing the Kill combo inside the recorder stops
    OnTop — the editor says so.
- **A watchdog that only writes.** The same service pings the dispatcher every
  second. Four seconds of silence → one WARN line with the stall length; recovery
  → one more. It never acts. The point is the timestamp next to whatever the log
  shows right before a freeze.
- **The freeze itself.** I could not reproduce it from the code — the scan
  (`EnumWindows` + titles + process names) uses calls Windows documents as
  non-blocking for foreign windows. So the scan now runs on a pool thread with
  a "Scanning…" placeholder and DEBUG timing lines, a WARN if it passes 3 s,
  and the process-name lookup no longer enumerates every process per window.
  Whatever it was, it can no longer hold the UI thread, and the next log will
  say where the time went. If it freezes again, the watchdog line + the
  preceding DEBUG lines are what I need.
- Tray menu gained **Kill Now — No Cleanup** (only useful while the UI still
  answers; the hotkey is the one that works when it doesn't).
- `RegisterHotKey` now has `SetLastError = true` — the "error 1409" reporting
  from Stage K was reading an unrelated error code.

Schema stays 29. Test: TESTING.md section 6e.

## Session 28 — v0.44.2 — License, first person, no names

No code behavior changed. This version makes the repository fit to publish:

- **GPL-3.0-or-later.** `LICENSE` is the verbatim FSF text of GPL version 3
  (copied from a local system copy; hash-verified). I chose the GPL so nobody
  can take this — a mostly AI-written program — close it up and charge for it;
  anything built from it stays open. README has the license section with the
  no-warranty notice, and Help gained an **About & License** entry.
- **The README says what this is.** A completely AI-developed project: every
  line of code and every document was produced by Anthropic's Claude across
  development sessions, with me (Godswrath88) directing, building, testing and
  feeding results back. That statement lives in the README only; the rest of
  the docs and every code comment now read as the author's own notes.
- **First person everywhere.** Roughly 130 places said "owner request",
  "the owner asked", "your request", "the assistant's container", "the next
  chat", "a weaker model" — all rewritten. Code comments that quoted my test
  reports now say "from testing: …". `HANDOFF.md` was stale on top of that and
  is rewritten from scratch. `MASTER_PROMPT.md` is the one exception and says
  so: it is *meant* to be a prompt, so it still addresses the next session.
- **No identifiers.** My real name was in `HANDOFF.md` (seven times) and the
  installer's Publisher; the hardware tool from my setup was named in four
  places. All gone or generalized. Publisher and copyright holder are the
  handle `Godswrath88`. New standing rule: never write my Windows username,
  e-mail or machine name into any file — and `OnTop.csproj` gets a
  **Release-only `PathMap`** so release builds don't embed the local build path
  (crash stack traces showed my Windows user name through it).
- **Metadata.** Authors / Company / Product / Copyright / license expression in
  the csproj (visible in the exe's Details tab), `AppCopyright` in the
  installer, `LICENSE` in the zip contract.

Also filed: the **Round 3** ideas list I approved on 09-24 (31 items — Professional
appearance mode and the rest) now sits at the top of ROADMAP.md with its gate and
suggested staging. It had only existed in a session transcript, which is exactly
the kind of loss these docs exist to prevent.

Schema stays 29. Nothing to retest beyond the Help window's new last section
(and that the build still restores/compiles — the csproj gained properties).

## Session 28 — v0.44.1 — GitHub readiness

Repo hygiene only: `.gitignore` (notably excluding real config files, which can
contain macro keystrokes), `.gitattributes`, `.editorconfig`, and a README that
finally describes the app that exists. No `LICENSE` yet — I wanted to compare
license types first (MIT / GPL-3.0 / none); until one is added the code is all-rights-reserved by
default.

## Session 28 (Stage K, v0.44.0) — My First Real Test Round

Three logs and a marked-up checklist. The big ones were root causes, not
polish:

- **Double-clicks weren't detecting** because Desktop Only asked Windows "what
  window is under the cursor?" and got OnTop's own zone overlay back — it covers
  the whole monitor and is only click-through while Overlays Locked is on. Every
  gesture was silently thrown away as "not the desktop". OnTop now looks
  THROUGH its own overlays to what's really beneath (a real program under the
  overlay still blocks, as I wanted), and a gated click says why in the log
  (verbose) or as a Test-Mode toast.
- **A placement zone went into Stealth** because the global "Zones Trigger
  Stealth" switch replaced *every* action with Stealth. It now only turns Exit
  zones into Stealth. Advanced-action zones get their own **After Action**
  (Nothing / then Stealth / then Exit).
- **Accent never changed anything** — WPF freezes a brush the moment a style
  that uses it is first applied, so the in-place tint was a no-op and replacing
  the resource reached nobody because every reference was StaticResource. All
  125 brush references are DynamicResource now; accent AND the three themes
  apply live.
- **Overlays under the main GUI after a ping** — re-asserting "topmost" on an
  already-topmost window is a no-op on modern Windows. They're now dropped and
  re-raised, re-asserted after every refresh, and re-asserted whenever an OnTop
  window takes focus.
- **Placement said "applied" and moved nothing** — the move result was never
  checked. It is now; error 5 means the target (a GPU tuning utility, in my case) runs as
  administrator and a non-elevated program cannot move it — by design. The log
  and toast say exactly that. Candidates are also ranked instead of first-hit.
- **Placement crashed on reopen** — the grid was mid-edit when the window
  closed; both grids now commit before closing and before binding.
- **The hotkey recorder ran the keys you pressed** — every global hotkey is now
  suspended while any recorder is open; conflicts are refused the instant you
  press them; macro/library hotkeys use a proper capture dialog.
- **The recording contained Ctrl+Alt+M** — start-chord key-ups and stop-chord
  key-downs are stripped.

And the UX contract I asked for: every sub-GUI has **Save & Close** and, where
edits can be undone, **Cancel (Revert)**; title-bar ✕ is gone from them;
Settings has Revert too. Placement has a full rule editor (double-click a row),
a running-process picker and .exe browser (Auto-Pin too), a per-rule delay,
and column hover descriptions. The macro strip only shows while the Macros
window is open or something is recording/playing, has a × and an optional
hotkey, and countdown is a plain field. Click-through windows get an orange
frame and the Pinned list shows each row's state.

`TESTING.md` is now the standing checklist with a typeable legend; my marks
and notes are carried in; everything fixed here is tagged **retest**.

## Session 28 (Stage J, v0.43.0) — Layout Round Before The Sweep

You compiled 0.42.1 and sent layout notes before testing anything, so these go
in first and the sweep starts from here:

- **The action row was starving the Zones row.** It's now three cards — Zone
  Actions (only zone tools), Zone Groups with a new **Macros** card under it,
  and **App** (Logs, Pinned, Auto-Pin, Actions, Placement, Profiles, Stats,
  Help, Peek, Legend, Paused, Test, profile picker). Much shorter, so the map
  and list get their height back (and a 300 px floor).
- **Help contents** are proper chips now — bordered, rounded, spaced, invert on
  hover — instead of links running together. The duplicated "Zone Map"
  section is one section.
- **Hold the middle mouse button to scroll the map** — said on the empty-map
  hint, the map tooltip, the welcome tour, and Help.
- **Hotkey re-bind retry interval** is yours to set (Settings → Performance,
  5–600 s; was hard-wired to 30).
- A standing rule: every session that adds a control decides which card it
  belongs to, splits a card that's grown, and checks that no Auto-sized row is
  squeezing a star-sized one. Grouping is part of the work, not a cleanup.
- Schema 27 → 28, no-op. `TESTING.md` now has a Stage J section; NOTHING from
  0.37 onward has been functionally tested yet — that document is the whole
  outstanding list.

## Session 28 — Hotfix v0.42.1

Your first build of the 0.37–0.42 line came back with exactly one error:
`UniformGrid` (the cheat-sheet's two-column layout) lives in
`System.Windows.Controls.Primitives`, not the main controls namespace. One
`using` fixes it. Six versions, ~70 features, one line — the sweep is on.
Consolidated test document: `TESTING.md` (now in the zip).

## Session 28 (Stage I, v0.42.0) — The Twenty

All twenty from the second ideas list, minus nothing:

- **Pinning**: Save/Apply **Scenes** (every OnTop pin with geometry, opacity,
  click-through — restorable from Pinned Windows, a Library action, or the
  palette); **Pin Memory** reapplies how a program was last pinned; **Timed
  Pin** (pin for N seconds via a timer — no keyboard hook); **Send To Next
  Monitor**; tray quick-opacity for the last pin.
- **Zones**: Shift/Alt double-click gestures; **Cooldown** (a jittery extra
  click can't fire a Kill twice); **Schedule** (time window + days); **Hit
  Flash** outside Test Mode.
- **Macros**: **Dry Run** — plays with the strip narrating each step and sends
  nothing; **Pause/Resume** hotkey; per-step **Notes** and a Hide Mouse Moves
  view in the editor.
- **Profiles**: every switch logs and toasts a **diff** (+/− zones, macro
  policy); **Idle Rule** (after N idle minutes: profile / unpin all / stealth,
  reverting on activity — reads Windows' last-input time, no hook); time-of-day
  **Schedules** (process rules win).
- **Comfort**: tour step for the Features panel; **Config History** browser
  (own-path restore, no quarantine); **Toast History** (DND-suppressed dimmed);
  **Themes** — Dark, High Contrast, Light — applied live by mutating the token
  brushes. Light is a best-effort palette swap over a dark-designed app: expect
  rough edges and note them in TESTING.md.
- Schema 26 → 27, no-op. Ten stages of code now sit on the one confirmed
  build; the next step is a build and test round.

## Session 28 (Stage H, v0.41.0) — Is The Security Scope Too Strict? Two Loosened, One Tightened

You asked whether the security scope was too tight. Mostly no — but:

- **The keyboard-hook rule was inconsistent.** SECURITY.md refused a keyboard
  hook for press-to-peek in S20, yet the macro recorder installs one every time
  you record. The rule that actually held up is *lifecycle*: hook only while a
  feature is armed, gone the moment it isn't, gone in Stealth, gone on exit.
  It's rewritten that way, and it unlocks **Hold Chords**: hold one chord to see
  every overlay, release to hide; hold another to keep the focused window on
  top, let go to release. Off by default; the hook exists only while the
  switch is on and a chord is set.
- **Launching needed three switches.** The global "Allow Launching" gate is
  gone; a rule's own Launch box plus import quarantine already guarantee an
  import can't arrive launching.
- **Tightened: macro recordings are now encrypted by default** (installed
  mode). The most sensitive thing the app stores — literal keystrokes — was the
  least protected by default. DPAPI costs nothing. Portable mode stays off
  because the file travels between logins.
- **The honest answer to "why could a ban still happen after Stealth or
  Kill?"** is now written into SECURITY.md and Help: both stop the evidence
  *from that moment on*. Kernel anti-cheat can already have logged the hook
  install and any injected input and shipped it; bans are decided later from
  that history; the watcher polls every few seconds so a game scanning at its
  own startup can see OnTop first; and synthesized input can't be
  un-synthesized. The guard reduces risk. Pinning-only OnTop is benign.
- Also: **Stop Key** for looping macros (polled, no hook). Schema 25 → 26.
- Paper trail kept deliberately verbose, per my standing intent: full context
  should be rebuildable from the docs alone.

## Session 28 (Stage G, v0.40.0) — The Sixteen Additions

Everything from the ideas list except the three that were advised against:

- **Pinning finally gets its extras.** Opacity +/− hotkeys (10% steps, floor
  20%) so a guide or chat stays visible over a game; **click-through** so clicks
  pass to what's beneath (same hotkey over it, its button in Pinned Windows, or
  unpinning always reverts — OnTop never leaves a trapped window behind);
  **pin the window under the cursor** for windows that won't take focus; and
  **corner cycling** for a picture-in-picture feel. All in Pinned Windows too.
- **Automation.** Profile Rules: "while *valorant* runs, use profile *Game*"
  and back when it exits — your Block-Macros game profiles on autopilot.
  Display Profiles: bind a profile to a monitor layout; dock/undock switches.
  Do Not Disturb quiets routine toasts while a guarded process runs.
- **Zone gestures.** Each zone picks its trigger: double left (classic), middle
  click, triple left, Ctrl+double, or double right. The hook now forwards all
  three buttons; click counting is per button.
- **Macros.** 3-2-1 countdown on the status strip; **Then Play…** chains macros
  (natural finish only, five deep, never itself); **Relative** replay follows
  the window that was in front when you recorded; and the step editor can
  **Insert Wait For Window** — pause until a title appears or give up after a
  timeout. That's the only step you can add: it waits, it does nothing.
- **Discoverability.** A hotkey cheat-sheet card and a type-to-filter command
  palette (built-ins, windows, profiles, zone toggles, enabled actions and
  macros — Enter runs). Statistics grew bars and Export CSV.
- Schema 24 → 25, no-op. Every new feature ships inert or unbound.

## Session 28 (Stage F, v0.39.0) — Action Library

Triggers and actions are now decoupled, the way macros already were:

- **APP → Actions…** holds named actions — Run Command, Kill Process, Toggle
  Window, Play Macro, Apply Placement, Switch Profile, Pause/Resume, Peek,
  Toggle Stealth, Pin Under Cursor. Give one a hotkey, point zones at it
  (Set Action → Library Action), or Run it from the window. A Run Command
  finally works from a plain keybind without needing a zone.
- **No new power.** Every kind calls the exact same guarded verb that already
  existed (the one launcher, the kill blocklist, the macro guard, profile
  switching). The library adds reachability, nothing else — and it ships off.
- **One conflict check for everything.** The S26 rule said to extract the
  hotkey conflict check into a shared service the moment a second assignment
  surface appeared — that moment is now. `FindHotkeyConflict` lives in the VM;
  the Macros window and the Library window both use it; the Hotkeys dialog
  sees library bindings too.
- Imports: powerful library actions arrive disabled and unbound; the feature
  switch never arrives on. Schema 23 → 24, no-op.

## Session 28 (Stage E, v0.38.0) — Window Placement & Launcher

Your headset-app use case, built to the S27 spec:

- **APP → Placement…** holds rules that put a program's window on a chosen
  monitor at a position/size (or maximized). **Capture From Focused Window**
  writes a rule from wherever the window is right now. Apply one rule, all of
  them (optional hotkey), automatically a few seconds after OnTop starts, or
  from a zone (Set Action → Apply Placement).
- **Move ≠ launch, by construction.** A rule always describes where a window
  goes. It may *optionally* name a program to start when nothing matches — and
  that half is inert unless the global **Allow Launching** switch AND the
  rule's own Launch box are both on. Launch paths are shown verbatim, every
  launch is logged and toasted, and imported configs arrive with launch paths
  blanked and the global switch off.
- **One launcher, for everything.** Zone Run Command and placement launch now
  share a single `LaunchViaShell` choke-point (refuses during config load and
  in Test Mode). The number of places the app can start a program did not grow.
- **Bug found on the way:** `Zone.Clone()` was dropping the action payload, so
  profile snapshots and Duplicate lost Run Command / Kill / Macro settings.
  Fixed.
- AHK importer: marked SCRAPPED in the roadmap, entry kept for the record.
- Schema 22 → 23, no-op; the feature ships off (Settings → Features).

## Session 28 (Stage D, v0.37.0) — Your Feedback Round

Fourteen items from my first real test of the 0.33+ line. Headlines:

- **Recording is hotkey-driven now.** Ctrl+Alt+M (new default, changeable in
  Hotkeys…) starts a recording anywhere; press again to stop and save. The
  stop press is trimmed off the tail, so clicking in a window to stop is gone.
  A small **status strip** sits bottom-right (drag it anywhere): gray READY,
  red marquee RECORDING with elapsed time and step count, green SAVED, blue
  PLAYING. It never steals focus. Switch it off in the Macros window.
- **Step editor.** Double-click a macro (or Steps…, or right-click): every
  click, key, scroll and delay in a searchable grid. Edit delays, delete steps,
  strip all mouse movement (clicks keep their spots). Save or Cancel.
- **Known anti-cheat list is per-entry.** Macros → Known Anti-Cheat… shows each
  service (and which are running right now); untick one that never minds macros
  on your setup — e.g. PnkBstrA, which runs permanently on PunkBuster machines.
- **The kill switch confusion.** Two things were wrong: (1) a refusal *at start*
  tripped the kill switch — now only a process that *appears mid-run* does, a
  start refusal just refuses and names the process; (2) the "Macros Enabled"
  toggle wasn't refreshed when the switch turned macros off, so it looked ON
  while it was OFF — now it mirrors the real state and the toast says "tick
  Macros Enabled to re-arm."
- **Accent color works now** — and applies live, no restart. The old code
  replaced the brush resource, but every style had already captured the
  original brush object; mutating that object in place re-tints everything.
- **Overlays above your other OnTop windows** on peek/flash/preview (they were
  losing the z-order fight to newer topmost GUIs). The Macros window also gets
  the GUIs-on-top binding it was missing, remembers its size, and opens large
  enough to fit everything.
- **Performance.** Settings → Performance → Low Impact Mode: Below-Normal
  priority + slower background timers. On core affinity: honestly, no — pinning
  OnTop to "non-game cores" can't help a process that's asleep 99.9% of the
  time, games don't own fixed cores, and a pinned process can only run on its
  pinned cores even when those are the busy ones. Below-Normal priority is what
  actually makes the scheduler give every core to the game first. Written up in
  Help → Performance so the reasoning is on record.
- **Amber ⚠ cautions** on the hover tooltips and Help sections of every feature
  with a security or comfort trade-off — Macros, Auto-Pin, Zone Actions, Stealth
  watcher, Updates. **Help gained a hyperlinked table of contents.**
- **Plain wording:** "Block Macros In Profile X" (was "Deny In Profile"), speed
  as a percentage (100% = as recorded, 200% = twice as fast), "Wobble" in pixels
  with 0 = exact / 3 = subtle / 10 = loose, repeat "once / 3× / until stopped".
- **Layout:** MACROS is its own band on the main window (Macros…, ● Record
  Macro, ■ Stop), and the Macros window has a visible ■ Panic Stop (Ctrl+Alt+X).
- **Welcome tour** lists your current hotkeys (including the new record one)
  and both buttons open the main window.
- Installer / updater / portable: still untested by me — nothing assumed.

Schema 21 → 22 (no-op; note the record hotkey defaults BOUND on purpose).

## Session 28 — Hotfix v0.36.1 (First Real Build Feedback On The 0.33+ Line)

Your first build of Stage C came back with four items, and three of them are
older than this session — proof the v0.33.2 baseline never actually compiled:

- **`MacroSvc` was never declared.** The S27 macro core assigned
  `MacroSvc = new MacroService(this)` in the VM constructor but the property
  itself was missing, so every macro call in App, the VM, the Macros window and
  the new Features panel failed. One `public MacroService MacroSvc` fixes all
  ~30 errors.
- **`LogService._sync`** — the diagnostics tail (S24) locked on a field that
  didn't exist. Declared.
- **`idx` shadowing** in the zone context menu's Color submenu (S24) — the loop
  local collided with a later method-scope `idx`. Renamed `colorIdx`.
- One nullability warning in the hotkey conflict check — silenced properly.

Structural verification can't catch "assigned but never declared" (it's a
symbol error, not a shape error), so a new build-gotcha is recorded: after
adding a `Foo = new ...` line in a constructor, grep the SAME file for `Foo {`
or `Foo;` and confirm the declaration exists. Nothing else changed; the Stage C
feature set is exactly as described below.

## Session 28 (Stage C, v0.36.0) — Features Panel, Minimal Mode, Encrypted Recordings

The Standing Design Law gets its consolidation, and the S27 confidentiality
decision gets built:

- **Settings → Features.** One panel, one switch per optional feature: Macros,
  Auto-Pin Rules, Auto-Close, Update Check At Startup, Statistics, Zone
  Actions, Stealth Auto-Watcher, Display Auto-Disable. Three of those didn't
  have a switch before — Statistics (every counter is now a no-op when off),
  Zone Actions (an advanced action on a zone refuses + toasts instead of
  running; Exit/Stealth are core and unaffected), and the Stealth Auto-Watcher
  (its 5 s timer goes inert). Each is genuinely inert when off, not just hidden.
- **Minimal Mode.** One confirmed click turns all eight off — OnTop becomes
  pinning + zone detection and nothing else. Reversible per switch. Logged as
  an ACTION with the full list.
- **Encrypt At Rest (DPAPI)** in the Macros window, default off. Recordings are
  sealed with Windows DPAPI under YOUR login at every save — no password, no
  stored key; a copied/synced/leaked config exposes nothing. Zones, profiles,
  hotkeys and settings are never touched, and Export still writes plain JSON,
  so sharing is never blocked (the S27 correction, honored). A recording sealed
  under a different Windows account loads empty and disabled with a warning —
  never a crash, never silent.
- **Build note — new NuGet package.** DPAPI on .NET 8 needs
  `System.Security.Cryptography.ProtectedData` (Microsoft, 8.0.0); it's in the
  csproj. Your first build after this needs NuGet restore to succeed (VS Code /
  `dotnet build` does this automatically; if you build offline it will fail
  with NU1101 — restore once online).
- Layout review: the Settings dialog outgrew a 1080p work area, so its content
  now scrolls inside a work-area-capped ScrollViewer.
- Schema 20 → 21, no-op. Deferred: close motion (cosmetic, needs a Closing
  interception that can't be verified without a build).

## Session 28 (Stage B, v0.35.0) — The Macro Subsystem Is Complete

The North Star's remaining items that pass the safety gate are in:

- **The third guard layer — the kill switch.** Until now a guard hit refused or
  aborted playback. Now (with Kill Switch on, the default) it also turns the
  master toggle OFF and unbinds every macro hotkey, and stays off until you
  re-enable it by hand in the Macros window. A center-screen toast and an
  ACTION log line say exactly which process tripped it.
- **A built-in known-anti-cheat list** (EasyAntiCheat, BattlEye, Vanguard,
  FACEIT, PunkBuster, GameGuard, XIGNCODE, HoYoProtect, Ricochet, ...) now feeds
  the guard alongside your Stealth watched list — behind its own switch
  ("Guard: Known Anti-Cheat", default on). A false positive only refuses a macro.
- **Per-profile policy.** Tick "Deny In Profile" on a game profile and, while
  it's active, no macro hotkey binds and nothing plays or records regardless
  of the master toggle; switching to a DENY profile stops any playback. Found
  and fixed a subtlety on the way: `SyncActiveProfile` replaces the profile
  record wholesale, so the policy is explicitly carried across.
- **You can always see it.** The tray icon gets a red dot while recording and a
  blue dot while playing (same badge machinery as the amber paused dot), the
  tray tooltip and the main status bar say RECORDING / PLAYING.
- **Blunt warning, once.** A permanent banner in the Macros window, plus a
  one-time confirm before your first recording: literal keystrokes (passwords
  included) are stored, synthetic input can get accounts banned.
- **Actions-only capture** ("Record Mouse Path" off drops the mouse path;
  clicks still land where recorded), **per-macro Jitter** (±N px humanizing
  wobble, 0–20), and **Export/Import** — export warns it's plain-text
  keystrokes; import arrives disabled and unbound behind the Import Review, and
  any restored hotkey is conflict-checked again before it binds.
- **Hotkey story decided:** an OPTIONAL "Macros Window" hotkey (default
  unbound) in the ACTIONS section, plus a tray item that shows "— Off" while
  the module is dark.
- Schema 19 → 20, no-op migration (every new default is the old behavior).

Deferred by the safety gate (documented, not built): condition-based loops and
any scripting. Relative-replay mode remains optional polish.

## Session 28 (Stage A, v0.34.0) — Config Integrity Hardening Is Now Code + The Stage Ledger

This session is a staged "finish everything" run. Before any code, a rollback
net went in: the untouched pre-session tree is delivered as
`OnTopWPF_v0.33.8_baseline.zip`, and a new **`SESSION_STATE.md`** ledger records
every stage (version, what changed, which zip carries it, how to revert). Every
stage is a complete delivery — a cut-off session can be resumed OR rolled back from
the last zip. MASTER_PROMPT now lists it first in the read order.

Stage A turns the S27 security doctrine into code:

- **Import quarantine, with a review window you actually see.** Zone imports,
  and restores from any file OUTSIDE OnTop's own config folder, now arrive with
  every powerful action neutralized — Run Command / Kill Process / Toggle
  Window / Run Macro reset to Exit, the updater URL blanked, macros disabled and
  unbound. A dark-chrome **Import Review** lists each item *verbatim* with an
  unticked checkbox; only what you tick is re-enabled. The provenance rule from
  S27 is honored: restoring from your own Backups folder (the picker's default)
  is trusted and seamless — no review, no re-approval.
- **Inert-load guard.** A `SettingsService.IsLoading` flag is raised during every
  config read; the five execution choke-points refuse, ERROR-log, and toast if
  they are ever reached during a load. It should never fire — it is the "nothing
  executes on load" invariant as a live tripwire, so a regression is loud.
- **Macro events are range-validated on load and import** (`ClampMacros`): a
  hand-edited macro can't feed out-of-range coordinates, bogus virtual-key codes,
  absurd wheel deltas, or time-travelling timestamps to SendInput. Bad events are
  dropped rather than clamped so a corrupt stream can't be silently reinterpreted.
- **Powerful-field change notice.** Every save stamps a SHA-256 of the fields
  that matter (zone actions/commands/processes, macro bindings, updater URL,
  watched list). If the stamp doesn't match on the next start, you get one
  warning toast + log line — an external edit or sync can't silently change what
  a zone runs. Deliberately NOT a signature (no integrity theatre).
- **Updater URL re-validated at load** (https + parseable host, else blanked +
  warned), and **the restore path was missing both the size gate and Validate()**
  — a real gap found while wiring this, now closed.
- Service-side `ImportMacrosFile` / `ExportMacrosFile` exist now (quarantined,
  size-gated, ClampMacros); their Macros-window buttons ship in Stage B.
- Schema 18 → 19 (`PowerfulFieldsStamp`, no-op migration). Help gained
  "Imports, Restores & Config Safety"; SECURITY.md gained the IMPLEMENTED block.
- Housekeeping: CHANGELOG's head had accumulated 27 stray `---` separators from
  prepends — collapsed to one.

**Build note:** v0.33.2 is still unconfirmed on Windows; this build carries
that risk plus its own. Paste errors verbatim and they get fixed as a hotfix
bump on this stage.



## Session 27 (cont. 6 — docs) — Safety Gate For Future Macro Capabilities

Answering "what if macros later gain scripting/launch/install?" — a new
SECURITY.md standing rule, **"Future Capability Expansion — the safety gate,"**
that any new power must pass through:

- **The trust boundary is the SOURCE of the instructions, not the feature.** A
  script you wrote is ordinary user intent; a script from a shared pack or
  community library is untrusted input, and that is what the gate is for.
- **No general-purpose script engine with ambient authority** — prefer a bounded
  VERB LIST (wait, click, send keys, set window position, run a named approved
  action) over embedding a language that has file/network/process reach by
  default. This one decision protects more than any sandbox added later.
- If an engine is ever embedded, it must be **strippable** (no io/os/process/
  network/ffi/dynamic-import; host functions only; time + instruction budget).
- **Capabilities are granted, never assumed** — powerful verbs off by default,
  per-macro, shown verbatim, and **imported content can never self-grant**.
- **NO install capability, ever** — never download-then-execute (that is the
  malware pattern); at most launch a local path the user chose, via the single
  existing launcher choke-point.
- Reuses every existing guarantee: one choke-point, log + toast per run, Test
  Mode suppression, anti-cheat refusal/abort, panic stop, inert when disabled,
  and **nothing auto-runs on load or import**.
- **Multi-process stays out** — the reasoning survives a scripting future while
  those rules hold. Documented revisit triggers: a non-strippable engine,
  third-party content running by default, or untrusted binary parsing / a
  network service. And if ever revisited, the correct form is a low-integrity /
  AppContainer child + broker exposing only the vetted verb list — not naive
  same-integrity process splitting, which provides no isolation at all.

The macro End Goal's remaining items (condition loops, etc.) are now explicitly
bound to this gate in ROADMAP.md.

## Session 27 (cont. 5 — docs) — Document-Every-Change Rule + Sharing Preserved

- **New standing rule: DOCUMENT EVERY CHANGE, including trivial and docs-only
  ones** (my request). Every delivery now requires a CHANGELOG entry (with its
  link ref), a WHATS_NEW entry, AND a PROJECT_SCOPE line — plus a pre-zip check
  that csproj, installer .iss, CHANGELOG head, WHATS_NEW head, and the
  PROJECT_SCOPE "current:" line all name the same version. This exists because
  it already bit us: docs-only v0.33.3–0.33.6 each got WHATS_NEW and
  PROJECT_SCOPE coverage but skipped CHANGELOG, which silently fell four
  versions behind until an audit caught it. Those four entries have been
  backfilled. Rule mirrored into MASTER_PROMPT.md so it's in the per-session
  brief, not just the contract.
- **Clarified: at-rest encryption will NOT block sharing profiles.** Answering
  my question — profiles, zone layouts, groups, hotkeys, and settings are not
  sensitive and would NEVER be encrypted, so sharing with friends or publishing
  online is unaffected. Only macro keystroke streams would be. Export is
  specified as a separate deliberate path that decrypts locally and writes a
  portable artifact, with macros excluded by default behind an explicit opt-in
  and a "this captures literal keystrokes" warning. The earlier wording (shared
  configs "omit the encrypted subset") would have limited sharing and has been
  corrected in SECURITY.md and ROADMAP.md.

## Session 27 (cont. 4 — docs) — Two Corrections: Recovery Isn't Quarantined + Encryption For Privacy

You caught two real issues with the previous security notes:

- **A crash/corruption fallback must NOT be quarantined** — my correction, and
  it was right: that would be miserable (app recovers from its own backup, then
  disables all your actions and makes you re-approve your own workspace). Fixed with a hard
  **provenance line**: the app's own settings + backups are TRUSTED and load
  fully active (recovery is seamless); ONLY a file the user explicitly imports
  is untrusted and quarantined. Size/clamp validation still applies to both;
  the powerful-field quarantine applies only to real outside imports.
- **Encryption for PRIVACY is legitimate** (distinct from tamper-proofing,
  which stays rejected). The config can hold sensitive data — window titles,
  paths, and especially **macro recordings that capture raw keystrokes, possibly
  passwords**. Corrected guidance: minimize what's stored + warn in the macro UI
  that it captures literal keys; leave whole-file confidentiality to Windows
  BitLocker/EFS; and optionally encrypt just the high-risk subset with **Windows
  DPAPI** (per-user, tied to the Windows login — a copied/synced file exposes
  nothing, no stored key, no password-prompt theatre). Shared exports omit the
  encrypted subset by nature. **Encryption: YES for privacy of sensitive data,
  NO for proving authority — the two must never be conflated.**

Both corrections are in SECURITY.md and ROADMAP.md.

## Session 27 (cont. 3 — docs) — Shared-Config Safety (Quarantine, Not Encryption)

- **The right answer to "can we make a shared config safe?"** (my follow-up question),
  written into the spec. The honest finding: **encryption/signing does NOT solve
  this and is explicitly rejected** — the recipient must read the file (so it
  can't be secret from them), and a hostile author would just sign/encrypt with
  their own key, so a signature proves nothing about safety. The danger is never
  the file's *authority*; it's its *content* (a command pointing at malware, a
  destructive macro, a redirected updater URL).
- **The approach that actually works: quarantine on import.** When a config or
  profile is imported from outside, powerful content lands **inert** — launch/
  kill/run-macro zone actions reset to a safe default, updater URL blanked,
  macros imported but not hotkey-bound — until the user reviews an **import
  summary** (every command, macro, URL, and launch path shown verbatim) and
  opts each back in explicitly. A maliciously crafted shared config is then safe
  *by construction*: it arrives inert, fully visible, and reversible (pre-import
  backup), with the user — not an unverifiable signature — as the trust
  decision.
- Encoded as a SECURITY.md subsection ("Shared / imported configs — validate
  content, don't trust authority") and a concrete ROADMAP deliverable folded
  into the config-integrity hardening pass. On-disk confidentiality, if a user
  wants it, is left to Windows EFS/BitLocker — the app rolls no crypto.

## Session 27 (cont. 2 — docs) — Config & Trusted-Data Integrity

- **Config-as-attack-vector hardening, specified before build** (my request).
  Since configs live in the user's own %APPDATA%, the real threat isn't a
  local attacker editing files (they'd already own the account) — it's a
  manipulated config acting as an INJECTION VECTOR into OnTop's powerful
  features, untrusted imports, or malformed data. New SECURITY.md standing rule
  **"Config & Trusted-Data Integrity"** captures the defenses, several already
  in place:
  - **nothing dangerous auto-executes on load** — parsing config never
    launches, plays, kills, moves, or calls out; powerful actions fire only on
    an explicit user gesture, so a config can't be an attack the moment it's
    read (the single most important invariant);
  - **validate on the way in** — the existing 4 MB gates + shared ClampZones
    (string/collection caps, enum-range checks) + corrupt-fallthrough + backups
    + schema-conflict prompt, extended to every new powerful field;
  - **re-assert trust at load, not just at use** — updater URL https-checked,
    action/watched strings clamped, macro events capped, so a tampered value is
    neutralized on read;
  - **powerful fields are visible** — shown verbatim in editors, log+toast on
    every run, so a config pointed at malware surfaces immediately;
  - **imports treated as untrusted** (same validation + pre-import backup);
  - **no integrity theatre** — we deliberately don't sign/checksum local config
    (a local attacker could re-sign; it adds complexity, not protection).
- **Roadmap: Config integrity hardening pass** — a focused task closing the few
  remaining gaps (updater-host re-validation on load, macro-event range checks,
  an explicit "inert load" regression test, optional powerful-field change
  notice). Hardening, not a rewrite — the invariants largely hold already.

## Session 27 (cont. — docs) — Window-Mover Security Architecture

- **Privilege compartmentalization for the future Window Placement & Launcher**
  (my request). OnTop is one non-elevated process, so features can't be
  OS-sandboxed from each other — the defense is architectural, and it's now
  written into the spec so we build it in from day one, not bolt it on:
  - **one choke-point** for all process-launching (extend the existing audited
    path — never add a second launcher);
  - **config-in, not call-in** — the launcher reads only vetted user rules and
    exposes NO runtime "launch(arbitrary string)" API another (compromised)
    feature could call — this is the direct answer to "can't be called and used
    by other features";
  - **move ≠ launch** — window-moving (low-risk) is split from launching
    (separately sub-gated, off by default), and a move-only rule can't escalate;
  - **reuse the guarded ShellExecute path** (verbatim, non-elevated, logged +
    toasted, Test-Mode-suppressed) rather than a new privileged surface;
  - **inert when disabled.**
  Captured in ROADMAP.md (on the feature entry) and as a new SECURITY.md
  standing rule, **"Internal Capability Isolation,"** that binds every powerful
  capability (launch, input synthesis, window manipulation, network) — the same
  principles macros and per-zone actions already follow.

## Session 27 (hotfix 0.33.2) — Duplicate-Definition Build Fix + 2 Roadmap Items

- **Fixed the CS0102 build error**: `HotkeyStealth` (and its backing field) were
  defined twice in AppSettings — a pre-existing declaration plus a duplicate the
  S26 stealth pass added. Removed the duplicate; the property keeps its place
  with the other hotkeys. Also swept the whole settings class (and the big
  service/VM files) for any other accidental duplicates from the multi-session
  merges — none found.
- **Roadmap — Window Placement & Launcher** (my request): a future feature to
  start and/or move specific programs to set monitor positions on startup or via
  hotkey/zone (e.g. a headset app that should always sit in a corner of your
  second monitor). Match by process/title, target monitor+position, optional
  launch-then-move, hotkey/startup/zone triggers, per-profile layouts — built on
  the existing Win32 layer, toggleable like every non-core feature.
- **Roadmap — Standing Design Law: every non-core feature must be fully
  disableable** (my rule): only the core pinning engine is always on;
  everything else (macros, updater, auto-pin, stealth automation, stats, zone
  actions, future features) must ship with a real off-switch that makes it inert
  — for anti-cheat compatibility AND user autonomy/security. Encoded in both
  ROADMAP.md (as a top-level design law) and SECURITY.md (as a standing rule
  every feature review checks). A future "Features" panel + one-click "Minimal
  Mode" is noted.

---

## Session 27 (hotfix 0.33.1) — Build Fix + Wheel Correctness

- Fixed the **DarkTheme.xaml build error** (MC3088): an animation trigger block
  from the S25 visual pass was sitting between Setters in the main window style —
  XAML requires setters grouped together with triggers after them. Moved; the
  open-motion animation is unchanged.
- Fixed **macro wheel playback** carrying a stray cursor move (post-ship audit of
  the macro core) — scroll now happens at the current position; button clicks
  still land exactly where recorded.

---

## Session 27 — MACRO SUBSYSTEM CORE + Welcome-Tour Re-Show + Soft Restart (0.33.0)

**THE MACRO MODULE (the North Star, core shipped)**
- **APP → Macros...** opens the module. A **master toggle (ships OFF)** gates
  everything: while off, no hooks exist, no hotkeys bind, nothing records or
  plays — the pinning core stays completely clear of it.
- **Full-fidelity recording**: press Record and use your PC normally — mouse
  **movement**, clicks, wheel, and keys are all captured with **real timing**.
  Playback reproduces the whole session at recorded speed. Positions are stored
  as fractions of the virtual desktop, so replay **adapts to your monitor
  layout** at play time. The keyboard+mouse hooks exist **only** between start
  and stop of recording — never otherwise.
- **Playback**: SendInput-based with drift-free absolute pacing, a **speed
  multiplier** (0.1–10×), and **repeat** (N times, or 0 = loop until stopped).
- **Per-macro hotkeys**: optional global hotkey — press to play, press again to
  stop. Assignment goes through the same **one-combo-one-feature** conflict
  check (across every app hotkey, every other macro, and the built-ins).
- **Zones can run macros**: right-click a zone → Set Action → **Run Macro**.
- **Anti-cheat guard (honest by design)**: playback **refuses to start** while
  any Stealth-watched process is running, **aborts mid-run** if one appears,
  and Test Mode suppresses zone-triggered playback. **Ctrl+Alt+X** is an
  always-available panic stop (registered only during playback, and reserved in
  the conflict checker). New SECURITY.md section **"Macro Module"** governs it
  all; caps at 50 macros / 20,000 events.

**QoL**
- **Show Welcome Tour On Next Launch** — Settings → Maintenance checkbox
  re-arms the first-run tour (with its starter-zones offer).
- **Restart OnTop** — Settings → Maintenance button: saves everything, closes,
  and relaunches (the soft-reset I asked for; same path as the restart
  hotkey, now reachable without a binding).

**Notes**
- Schema v17 → **v18** (MacrosEnabled + Macros library; Zone.ActionMacroId +
  ZoneAction.RunMacro). Version → **0.33.0**; installer synced. Remaining macro
  polish (jitter, condition loops, lighter capture modes, relative replay,
  per-profile policies) is queued under the End Goal; the pre-macro QA pass is
  now the **post-core QA pass** — still recommended before heavy reliance.

---

## Session 26 (cont. 2) — Hotkey Conflict Prevention (0.32.0)

- **Duplicates refused at the door (my request)**: the Hotkeys editor now
  checks every binding — all named hotkeys, the optional toggles, and the
  built-in Ctrl+Alt+P pause — with a normalized compare before saving. A
  collision keeps the dialog open and names BOTH features ("...Assigned To
  Both Peek And Toggle Stealth"). Windows silently fails the second
  registration of a duplicate combo, leaving that feature dead with no error —
  so prevention at assignment time is the only reliable guard.
- **Standing roadmap rule**: the Action Library's per-action hotkeys and macro
  hotkeys MUST route through the same central check when they arrive (the
  dialog is the only assignment path today; the check extracts into a shared
  service the moment a second surface appears). OS-level conflicts with OTHER
  apps keep logging + retrying as before.
- Version → **0.32.0** (no schema change).

---

## Session 26 (cont.) — Roadmap: Action Library Decoupling (docs only)

- New 🔺 roadmap entry (my request): **decouple triggers from actions** — a
  named, reusable **Action Library** where run-commands (and kills/toggles,
  later macros) get their own enable toggles and are assignable to zones AND
  directly to optional hotkeys, so actions are usable without any zone at all.
  Zones and actions stay independently toggleable, with the standard dim cue
  when an action override is in effect; existing inline zone actions migrate
  automatically. Deliberately the same architecture the macro End Goal already
  specifies, so macros later slot in as one more action type. No code this
  round — the entry is queued ahead of the pre-macro QA pass.

---

## Session 26 — Per-Zone Actions + Stealth Follow-Ups (0.31.0)

**PER-ZONE ACTIONS (the roadmap's power feature, done security-first)**
- Right-click a zone → **Set Action**: **Exit** (classic), **Stealth**, **Run
  Command…** (anything you'd type into Win+R), **Kill Process…** (by name —
  every instance; protected system processes like explorer/lsass are refused
  with a logged warning), or **Toggle Window…** (minimize ↔ restore+focus an
  app's main window).
- Security posture (new SECURITY.md section "Per-Zone Actions"): user-authored
  only, shown **verbatim** in the menu, every execution **logged with the full
  command AND toasted** — no silent path; ShellExecute non-elevated; strings
  clamped in the same shared validator as imports; **Test Mode suppresses
  actions** so placement is always safely verifiable. Enum is int-serialized —
  append-safe for old configs.
- The grid's action chip still quick-flips Exit/Stealth; clicking it on an
  advanced action returns the zone to Exit (the context menu is the advanced
  editor). New enum names display in the chip automatically.

**STEALTH FOLLOW-UPS (all three roadmap remainders)**
- **Toggle-Stealth hotkey** — optional binding in the Hotkeys editor.
- **Auto-Stealth watcher** — Settings → Stealth → list process names
  (comma/semicolon); while ANY runs, stealth engages automatically and releases
  when they're all gone — but a *manually* enabled stealth is never
  auto-released. 5 s cadence, idle-cheap when the list is empty. (This watcher
  is also the "silent layer" the macro anti-cheat guard will reuse.)
- **Overlay auto-hide while stealthed** (default ON, toggleable) — overlay
  windows are visible surfaces; hiding them completes the disappearance.

**Roadmap**
- **AHK importer marked OPTIONAL** — proceeds only if my original .ahk file
  ever surfaces; no longer blocks anything.

**Notes**
- Schema v16 → **v17** (HotkeyStealth, StealthWatchedProcesses,
  HideOverlaysInStealth; Zone.ActionCommand/ActionProcess). Version →
  **0.31.0**; installer synced. Remaining before the pre-macro QA pass:
  multi-click patterns (noted), my installer/GitHub/signing side — then QA,
  then macros.

---

## Session 25 — Visual Cohesion Pass + Portable Mode (0.30.0)

**The visual cluster (5 roadmap items in one sweep)**
- **Design-token pass**: the theme was already token-based; the stragglers are
  now converted — header/footer bands, the off-screen ⚠ amber, and the grid-row
  danger/accent tints are named theme resources (BgBand, WarnAmber,
  RowDangerTint, RowAccentTint), plus shared RadiusSmall/Medium tokens. The
  remaining literal colors in code are deliberate alpha-derivations (overlay
  fills from the zone palette, capture-overlay accents) and are documented as
  such.
- **Keyboard focus rings**: every interactive control (buttons, toggles,
  combos, sliders, text boxes) shows a consistent rounded accent ring when
  keyboard-focused — one shared FocusRing style.
- **Disabled states**: a uniform dimmed look (50% opacity) added to all nine
  interactive styles that lacked one; only ToggleSwitch had it before.
- **Dialog open motion**: every chrome window opens with a 150 ms fade +
  subtle scale-in (ease-out). Close motion needs code interception and is
  deferred — noted on the roadmap.
- **Accent picker**: Settings → Appearance → Accent Color (Blue, Violet, Teal,
  Green, Amber, Rose). Applied at startup before any window exists, so it
  re-skins everything; changing it prompts a restart note. Schema v15 → **v16**.
- **Windows 10 acrylic fallback**: the Mica toggle now degrades gracefully —
  on builds below Win11 22H2 it applies an acrylic blur-behind with our dark
  tint instead of silently doing nothing.

**Portable mode (roadmap)**
- Drop an empty **`OnTop.portable`** file next to the exe (or launch with
  `--portable`) and ALL data (config/logs/stats) lives in a `Data` folder
  beside the exe — true run-from-USB, pairing perfectly with the portable
  build. No marker = `%APPDATA%` as always. PORTABLE.md updated.

**Audits (both roadmap items closed with findings)**
- **Overshadowed controls**: sliders (Slider Lock) and action chips (global
  Stealth) were already cued; ONE new find — "Check For Updates At Startup" is
  meaningless while the manifest URL is empty and now dims live until a URL is
  entered. Nothing else qualifies.
- **Own-window hook guard**: audited; the process-id-level guard is the correct
  scope. The capture overlay takes clicks through normal WPF events (not the
  hook), so handle-level matching would add complexity for zero gain —
  documented decision, item closed.
- **Logging second pass**: category coverage verified across all 14 registered
  categories (93 categorized call sites + 14 default-General conversational
  ones) — no unlogged state changes found in the S22–S24 features.

**Also**
- ROADMAP gains a **"Final visual/UX QA pass"** entry pinned immediately before
  the macro subsystem (my call — fresh-eyes sweep over everything added
  between now and then).

**Notes**
- Schema v15 → **v16** (AccentIndex). Version → **0.30.0**; installer synced.
  Deferred with notes: light/high-contrast theme variants + density setting,
  dialog close motion, localization scaffold.

---

## Session 24 — Fix Round + 3 Large + 4 Normal (0.29.0)

**FIX — the magnet's recurring glitch, root cause found (v6)**
- `DragDelta` reports movement relative to THE THUMB — and the magnet holds the
  thumb while your cursor keeps moving, so accumulated deltas diverge from real
  cursor travel. That feedback loop poisoned every previous version. v6 ignores
  the deltas entirely and samples the absolute cursor position against the host
  panel every event: the curve now maps true cursor intent, tick for tick.

**FIX/LARGE 1 — per-monitor overlay windows (the map-vs-overlay mismatch I reported)**
- The overlay was ONE window spanning all monitors with ONE DPI scale — with
  mixed Windows display-scaling, geometry was off on non-matching screens.
  Overlays are now **one window per monitor**, each getting its own DPI under
  PerMonitorV2, with live re-flow on DPI change and automatic rebuild when
  monitors change. Map and overlay should now agree everywhere.

**NEW — tray menu persistence (my spec)**
- Settings → Appearance → **"Tray Menu Stays Open After Clicking"**: items no
  longer dismiss the menu; it closes ~0.6 s after your cursor leaves it (a
  forgiving buffer — grazing the edge won't slam it). Esc/outside-click still
  close instantly. Profile checkmarks update live while it's open. Schema v15.

**NEW — live overlay while dragging zones on the map**
- Drag a zone on either map and its overlay appears at full opacity, following
  in real time; release, and it lingers ~3 s before restoring the normal view.

**LARGE 2 — first-run welcome tour + starter zones**
- Fresh installs get a 20-second tour with an optional **"Add 4 Starter Zones"**
  (primary-monitor corners, group Starter, one undo removes all). Existing
  configs migrate as already-completed — you'll never see it.

**LARGE 3 — data-safety pass (three roadmap items)**
- Diagnostics/crash bundles now include **log-tail.txt** (the freshest ~50 log
  lines, even if the on-disk log is locked or huge); **every import writes an
  automatic backup first**; and timestamped backups are **pruned to the newest
  10** (rotation .baks untouched).

**NORMAL ×4** — DataGrid edits **commit when keyboard focus leaves** the grid
(Tab/Enter chains out); the startup shortcut **self-repairs** to the current exe
path every launch (moved installs heal, logged); **hook latency instrumentation**
(EMA of click-handling cost; warns before Windows would evict the hook); and a
proper **per-zone Color submenu** in the zone right-click menu (swatch-cycle
stays too).

**Notes**
- Schema v14 → **v15** (TrayMenuStaysOpen, FirstRunCompleted w/ existing-config
  migration). Version → **0.29.0**; installer script synced.

---

## Session 23 — 3 Major + 4 Small: Crash Reports, Updater Stage One, Test Mode & More (0.28.0)

**MAJOR 1 — Crash/issue report prompt (my roadmap request)**
- After a survivable unexpected error, OnTop offers ONCE per session to save a
  diagnostics bundle with a **"what were you doing?" comment** baked into
  diag.txt — written to `Config\Crashes`, never transmitted. Fatal crashes
  auto-save a bundle on the way down (no prompt possible). The bundle writer is
  now a shared service, so the Logs-window export and the crash path can't drift.

**MAJOR 2 — Updater STAGE ONE: check & notify (dormant by default)**
- Settings → **Updates**: a manifest URL field (EMPTY = the feature is fully
  dormant, zero network — the default), a startup-check toggle, and a "Check For
  Updates Now" button. When a newer version exists you get your exact three-way
  prompt: **Open Download Page** (browser handoff — the app itself downloads and
  executes NOTHING), **Ignore This Version** (quiet until the NEXT release), or
  **Cancel** (asks again next startup). HTTPS enforced both directions, 64 KB
  response cap, 10 s timeout, downgrade refusal, failures never nag, everything
  logged under the Updates category. `installer\UPDATES.md` documents the tiny
  JSON manifest + the free GitHub setup. SECURITY.md's network rule is formally
  amended for exactly this; stage two (in-app download/verify/run) stays gated
  on code signing.

**MAJOR 3 — Test Mode**
- A **Test** switch beside Paused: zone gestures report **HIT** (toast + map
  flash) or **MISS** (with coordinates) but never execute their action — and
  test clicks never pollute your statistics. Session-only, always starts off.

**SMALL ×4**
- **Tray paused badge** — the tray icon gains a drawn amber dot while detection
  is paused (and the pause event is now raised from one place, so the GUI
  toggle, tray, command, and hotkey all keep the badge in sync).
- **Map edge/corner snapping** — dragging a zone snaps its anchor to monitor
  edges (corners when both axes catch); **hold Shift to bypass**. Zoom-independent
  ~12-map-pixel feel.
- **Slider value bubbles** — the three zone sliders show their value in a bubble
  while dragging (ticks the roadmap visual item).
- **Uninstaller safety guard** — the optional data-cleanup DelTree now refuses
  to run unless the resolved path verifiably ends in `\OnTop` (belt-and-
  suspenders on the one destructive line).

**Infrastructure — BOTH release artifacts, labeled, independent (my request)**
- Top-level `installer\` (wizard setup exe) and NEW `portable\`
  (`publish-portable.cmd` → one self-contained `OnTop.exe`, no install step,
  same `%APPDATA%` config). `PORTABLE.md` carries the honest trade-off table
  (size, frozen runtime patching, SmartScreen). Build either, both, or neither
  per release — none of it touches the VS Code build path.

**Notes**
- Schema v13 → **v14** (UpdateManifestUrl / UpdateCheckOnStartup /
  IgnoredUpdateVersion). Version → **0.28.0** (installer script synced). Hotkey
  decisions: Test Mode = GUI toggle only for now (optional binding can come on
  request); updater/crash paths = none (documented).

---

## Session 22 — Feedback Round: Profile-Delete Fix, Magnet v5, Taskbar Semantics v2, UI Un-Clipping (0.27.0)

**Fixed — deleting the active profile actually deletes it now**
- Root cause: switching away first (correct) runs SyncActiveProfile, which
  REPLACES the doomed profile's record with a fresh snapshot object — so the
  delete then removed a stale reference and silently did nothing. Now removal is
  name-based after the switch. (Great catch — the toast was telling the truth
  about the switch and lying about the delete.)

**Fixed — magnet v5: the hold is now feelable mid-drag**
- v4's exact-hold was ±1.2% (~9 px) — too small to notice while dragging, so the
  only visible magnetism was the release settle ("only works on release"). v5:
  the flat hold is ±2.5% (a real ~20 px dead spot mid-drag), the approach curve is
  continuous at BOTH edges (no glitch at the well boundary — v4's curve had a
  slope kink there), and the release-settle only fires from inside the hold, where
  it's a zero-pixel normalize — never a visible jump.

**Changed — taskbar setting is now "Minimize To Tray" (my spec)**
- The main window ALWAYS shows in the taskbar while open — no more runtime style
  flipping at all (the whole fragile hide→set→show machinery is deleted). The
  setting that remains is **Settings → Appearance → "Minimize To Tray"**: off =
  normal taskbar minimize; on = classic hide-to-tray. Migration: if you had the
  old taskbar icon OFF, you get MinimizeToTray ON. Schema v12 → **v13**.

**Fixed — the two clipping reports**
- **APP section**: reorganized into two rows — windows/tools (Logs, Pinned,
  Auto-Pin, Profiles, Stats, Help) and live state (Peek, Legend, Paused,
  Profile picker with a label) — and every item now has a bottom margin so
  wrapped rows never touch or clip.
- **Log viewer categories**: the checkbox strip was a WrapPanel nested inside a
  WrapPanel — the inner one receives unbounded width and never wraps, running
  off the window edge. Rehosted in a DockPanel (width-constrained → wraps
  properly), plus per-checkbox bottom margins.
- **New pre-ship protocol step (my suggestion)**: a UI LAYOUT REVIEW is now a
  mandatory workflow step after any UI change — fixed sizes vs content growth,
  nested wrap panels, missing wrap-row margins, window minimums, long labels —
  plus an extension-method using-check (the S21 build-error lesson).

**New — sub-GUIs in the taskbar**
- All tool windows (list, map, logs, pinned, auto-pin, profiles, stats, help,
  auto-close, exceptions) now appear in the taskbar hover/thumbnails, so every
  open window — and any accidental duplicate — is one hover away. Modals stay
  out.

**Fixed — map zone scale vs overlay**
- The map was drawing zones to true scale — but at fit-3-monitors zoom that's
  2–3 px, hidden UNDER the fixed center dot, which read as "way smaller than the
  overlay". Below ~9 px half-extent the box now draws at a floor that always
  encloses the dot (a position marker at that zoom, not a measurement — zoom in
  for true scale).

**Roadmap (added, not restructured)**
- **Crash/issue report prompt**: on crashes, offer to save a diagnostics bundle
  with a "what were you doing?" comment baked into diag.txt — local-only, and
  later deep-linkable to a GitHub issues page. Corruption-protection testing is
  parked until update-push testing exists (my call, noted there).

**Notes**
- Schema v12 → **v13** (MinimizeToTray + migration). Version → **0.27.0**.
  Installer script version synced.

**Verify on Windows:** delete the active profile → it's GONE from the list and
combo; drag the split slowly — a real flat hold at center you can feel, smooth
escape, nothing on release; minimize with Minimize-To-Tray off → taskbar, on →
tray; APP section and log-viewer categories at various window widths → nothing
clips; hover the taskbar icon with several tool windows open → all thumbnails
there; map at fit-all zoom → every zone box visibly encloses its dot.

---

## Session 21 (cont.) — INSTALLER PACKAGE + Activity Feed (0.26.0)

**NEW — the installer, fully scripted (roadmap HIGH: "for sharing")**
- `installer\OnTop.iss` (Inno Setup 6) + `installer\BUILD.md`. The wizard covers
  the whole roadmap spec: Desktop / Start Menu shortcut checkboxes, install
  location choice, optional **launch at Windows startup** (the same
  Startup-folder shortcut the app's own toggle manages, so they stay in sync),
  **"Run OnTop now"** on finish, and **automatic .NET 8 Desktop Runtime
  bootstrapping** — detected via `dotnet --list-runtimes`; if missing, it's
  downloaded from Microsoft's fixed HTTPS link and installed silently (offline →
  clear instructions instead).
- Security-first, matching the app: **per-user install by default with NO UAC**
  (`PrivilegesRequired=lowest`, mirroring the asInvoker manifest; all-users
  remains available), the runtime download is the only network access, and the
  **uninstaller asks before deleting `%APPDATA%\OnTop`** — your zones, profiles,
  stats, and logs survive reinstalls unless you say otherwise.
- Two commands to build (see BUILD.md): `dotnet publish … -o publish`, then
  compile the .iss. Inno Setup can't run in the editing environment, so this ships as
  READY-TO-COMPILE — the roadmap item ticks fully once my first setup exe
  builds and passes the BUILD.md test checklist.

**NEW — recent-activity feed (the Statistics remainder)**
- The Statistics window gains **RECENT ACTIVITY (LAST 25)**: a timestamped
  rolling feed of zone triggers (with stealth marked), pins (with process name),
  unpins, auto-pins (with process), peeks, and profile switches (with name).
  The feed persists in stats.json (capped at 300 entries, validated like
  everything else) and is wiped by the same confirmed Reset.

**Notes**
- Settings schema unchanged (v12); the feed lives in stats.json. Version →
  **0.26.0**. The zip now contains the `installer\` folder (zip contract
  updated).

**Verify on Windows:** build the installer per BUILD.md and run its testing
checklist (fresh machine/VM without .NET 8 if possible); in the app — do a few
actions, open Stats: the feed lists them newest-first with times; restart → feed
persists; Reset → feed clears too.

---

## Session 21 — Roadmap Sweep: Display Awareness, Resume Robustness, Multi-Select, Diagnostics & Security Ticks (0.25.0)

Six queued roadmap items in one pass.

**New — monitor-configuration awareness (the Profiles remainder)**
- On any display change (dock/undock, resolution, monitor added/removed): zone
  health re-evaluates, the map's monitor outlines repaint (main + pop-out), and —
  if the new **Settings → Displays → "Auto-Disable Zones That Fall Off-Screen"**
  toggle is on (off by default) — zones left with no monitor under them are
  disabled automatically, with a toast listing them and a Zones-category log entry.

**New — sleep/resume + session-switch robustness (Stability item)**
- Waking from sleep and unlocking Windows can silently kill the low-level mouse
  hook, drop hotkey registrations, and shuffle Z-order. OnTop now listens for
  both and re-asserts everything: the hook (unless Stealth removed it), all
  hotkeys, always-on-top for every OnTop-pinned window (dead handles pruned), and
  the GUI's own topmost. Logged under System/Pins. SystemEvents subscriptions are
  properly torn down on exit (they're static — leaking them is a classic bug).

**New — multi-select bulk zone actions (UX item)**
- Both zone lists: Ctrl/Shift-select multiple rows. **Delete** removes them all as
  ONE undoable action; **right-click inside the selection** offers Remove / Set
  Group / Enable / Disable "Selected (n)" — each a single undo step with a toast.
  Right-clicking outside a selection behaves exactly as before.

**New — diagnostics bundle export (Stability item)**
- **Logs → "Export Diagnostics..."**: one SaveFileDialog, one zip — settings.json,
  stats.json, the current log file, and a diag.txt (version, schema, OS, .NET,
  monitor layout, zone/profile counts). Everything a bug report needs; nothing is
  ever transmitted anywhere (you choose the file).

**Security ticks (two roadmap items)**
- **Elevated-launch warning**: running OnTop as administrator now warns (toast +
  log) — a mouse hook doesn't need admin reach, and least privilege is the rule.
- **Import hardening**: zone-JSON imports are size-gated (4 MB, same as config)
  and every imported zone passes the exact clamps the config loader applies —
  now literally the same code (`ClampZones`, shared by Validate and import, so
  the rules can't drift apart).

**Notes**
- Schema v11 → **v12** (AutoDisableOffscreenZones, default off). Version →
  **0.25.0**. Hotkey decisions: none of the six need a binding (all automatic or
  context-menu driven) — documented per the standing rule.

---

## Session 20 (cont. 4) — STATISTICS (0.24.0)

**NEW — Statistics: session + lifetime usage tracking**
- **APP → "Stats..."** opens the counters: this session's uptime / zone triggers /
  pins, and lifetime launches, total runtime (live-updating), zone triggers (with
  stealth broken out), pins/unpins, auto-pins from rules, peeks, profile switches —
  plus your **five most-triggered zones** (tracked by zone Id so renames don't lose
  history; deleted zones still show, marked).
- **Own store, own rules**: counters live in `stats.json` beside the settings with
  the same security discipline (size-gated read, validated values, atomic
  temp-then-move writes) — and a stats **Reset** (confirmed) wipes ONLY counters;
  settings, zones, and profiles are untouched. Nothing identifying is stored and
  nothing ever leaves the machine.
- Counting hooks live at the real action sites: the zone-match path, pin/unpin
  (including Unpin All's count), rule pins, both peek paths, profile switches,
  launch, and runtime folded in on exit. Dirty-flag flushing every 60s keeps disk
  writes minimal.
- **Hotkey decision (per the standing rule): none** — an occasional-glance window
  doesn't earn a binding; the APP button is its home. Documented in the window
  itself and here.

**Notes**
- No settings-schema change (stats have their own file). Version → **0.24.0**.

**Verify on Windows:** open Stats — session uptime ticks on Refresh; trigger a zone,
pin/unpin something, peek, switch profiles → counters move; restart → lifetime
persists and runtime accumulated; Reset → counters wipe, zones/settings untouched;
check stats.json appears in %APPDATA%\OnTop\config.

---

## Session 20 (cont. 3) — Taskbar Fix, Magnet v4, Logging Audit First Pass & PROFILES (0.23.0)

**Fixed — the taskbar toggle no longer breaks the GUI**
- Root cause: `ShowInTaskbar` was XAML-bound, so flipping the setting churned
  extended window styles/owner on a LIVE custom-chrome window (WindowChrome + Mica)
  — that's what broke. It's now code-managed: applied once at load, and live changes
  go through a safe **hide → set → show** cycle with the Mica backdrop re-applied and
  Topmost re-asserted. Logged under Settings.

**Fixed — split magnet v4: a gravity well, not a state machine**
- v3's lock/unlock produced exactly what I saw: one visual glitch per swipe and no
  perceptible hold. v4 replaces all state with a pure position **curve**: response is
  continuous everywhere (jumps are mathematically impossible), flattens to zero slope
  at center — the divider decelerates in and genuinely resists leaving (±1.2% is an
  exact hold) — and rejoins 1:1 tracking at the ±7% band edge. Stateless, so slow
  drags, fast swipes, and direction changes all feel identical.

**Logging completeness audit — first pass DONE (the queued 🔺 HIGH item)**
- **Zero silent catches remain.** All 20+ bare `catch { }` blocks were swept: real
  failures now log (backup-rotation steps → WARN, draft save/delete + overlay
  teardown + auto-draft → DEBUG, exit-save → ERROR), and the genuinely-unloggable
  ones (the logger's own failures, crash-path best-effort saves, pre-logger mutex
  races, the last-resort crash writer) carry written justification comments.
- SettingsService gains `AttachLogger` so config I/O failures finally surface
  (new **Config** category).
- The per-category filter checkboxes I asked for **already existed** in the Log
  Viewer (persisted, write-time gated) — the category list is now extended with
  Zones, Sliders, Pins, Rules, Config, Profiles, and Updates so everything new is
  filterable. Noisy audit logging sits at DEBUG behind the Verbose toggle.

**NEW — PROFILES (the large-scale addition): named zone workspaces**
- A profile holds **zones, process exceptions, auto-close triggers, and the default
  sliders** — keep a per-game setup separate from a desktop setup. App settings,
  hotkeys, and Auto-Pin rules stay global by design (switching changes the
  workspace, not the app).
- **Switching is loss-proof**: the current workspace is saved into its profile
  record first, then the target loads (deep clones both ways, so editing live never
  silently mutates a snapshot). Overlays, map, health badges, and group filter all
  refresh; config persists immediately; toast + Profiles-category log.
- **Four entry points** (cohesion rule): a **profile combo + "Profiles..." button**
  in the APP section, a **tray "Profiles" submenu** (active one checked), the
  **manager window** (Switch / New-copies-current / Rename / Delete, double-click to
  switch, active marked ●, zone counts, "profiles" placement), and an optional
  **Cycle Profiles hotkey**.
- Guard rails: unique names, the last profile can't be deleted, deleting the active
  one switches away first, first run wraps your existing workspace as "Default" —
  nothing changes until you create a second profile. Validation clamps profile
  count/name lengths/nested zones (SECURITY.md discipline). Help section added.

**Notes**
- Schema v10 → **v11** (Profiles, ActiveProfile, HotkeyProfiles; no-op migration —
  the Default profile is created from your live workspace on first run).
  Version → **0.23.0**.

**Verify on Windows:** toggle Show-In-Taskbar off/on repeatedly → GUI survives (brief
hide/show blink is expected); drag the split — decelerates into center, holds, pulls
free with zero jumps at any speed; make a second profile, add zones, switch back and
forth from the combo, tray, manager, and hotkey → each keeps its own zones; delete
the active profile → switches away safely; check the log for Config/Profiles entries
and the new category checkboxes. Paste any build errors verbatim.

---

## Session 20 (cont. 2) — Feedback Round: Magnet v3, Auto-Pin Rename, GUI Cohesion, Taskbar & Hotkey Editor (0.22.0)

**Fixed — split magnet v3 (the remaining stutter had three causes)**
- v2's unlock jumped the divider to the release-band edge (a ~40px hop — the "freak
  out"); v3 re-bases so the divider **continues smoothly from center**, exactly as
  originally specced. A **re-arm** rule stops the instant re-grab when you drift back
  (the magnet only grabs again after the pointer clearly leaves the neighborhood).
  And widths are now **pixel-snapped with no-op writes skipped** — sub-pixel star
  math was re-laying-out the whole panel every mouse message (the shimmer). Bands
  widened per feedback: grab at ±3%, release past ±6%. Also removed a stale
  duplicated comment block left from the v0.18 implementation.

**Renamed — "Rules" → "Auto-Pin" everywhere users see it**
- APP button ("Auto-Pin..."), window title, master toggle ("Enable Auto-Pin"), tray
  item, hotkey editor row, Help section. Code identifiers keep the WindowRules names.

**New — main-GUI cohesion buttons (my request)**
- Hotkey/tray-only features now have visible homes: **Peek** and **Legend** buttons
  plus a **Paused** toggle switch join the APP section (the toggle binds the same
  DetectionPaused state as Ctrl+Alt+P and the tray, so they all stay in sync).

**New — taskbar icon (Settings toggle, default ON)**
- The main window now shows in the taskbar by default; Settings → APPEARANCE →
  "Show Main Window In Taskbar" restores the old tray-only behavior. Minimize
  behaves normally with the icon on, and keeps the hide-to-tray behavior when off
  (without a taskbar button a minimized window would be stranded). Schema v10.

**Fixed + reorganized — hotkey editor**
- Clipping: the label column was 182px and long labels ("Pinned Windows Viewer
  (Optional):") overflowed into the boxes. Column widened to 200px, labels now WRAP
  as a safety net, window widened to 560.
- Reorganized into **REQUIRED / OPTIONAL — ACTIONS / OPTIONAL — TOGGLES** sections
  (my request), dropping the per-row "(Optional)" suffixes that caused the
  overflow in the first place. Kill sits alone under REQUIRED (rebindable, never
  unbindable; fixed Ctrl+Alt+P noted there).

**New standing requirements (PROJECT_SCOPE)**
- **Help + tooltips keep pace with the code** — updated in the same session a
  feature ships (high priority, my rule).
- **Every feature decides its hotkey story when it ships** — needed? required or
  optional? — and lands in the right editor section, never a flat list.
- **Main-GUI cohesion** — hotkey/tray-only features get a main-window control when
  it makes sense.

**Notes**
- Schema v9 → **v10** (ShowInTaskbar, default true). Version → **0.22.0**.

**Verify on Windows:** drag the split slowly through center — solid grab at ~3%,
clean hold, smooth continue past ~6% with NO hop, and no shimmer during free drags;
"Auto-Pin..." button + renamed window/tray/help; Peek + Legend buttons and the Paused
toggle (flip it, then Ctrl+Alt+P — they stay in sync); taskbar icon present, minimize
goes to taskbar, turn the setting off → minimize hides to tray again; hotkey editor —
no clipped labels, three clear sections. Paste any build errors verbatim.

---

## Session 20 (cont.) — Zone Peek Hotkey + Rules Hotkey (0.21.0)

Post-ship audit of the crash-interrupted session came back clean (no duplicate
definitions, all wiring single-sourced), then two additions.

**New — Peek At All Zones (bindable hotkey)**
- Press the new optional **Peek** hotkey and every zone — including disabled ones and
  ones with their overlay dot turned off — flashes onto the desktop overlay at full
  opacity for ~1.5 seconds, then the normal view restores. The "where are my zones?"
  glance, finally on the actual screen instead of just the map.
- Design note: this is **press**-to-peek, not hold-to-peek. Detecting key-release
  would require a keyboard hook, and SECURITY.md limits our input hook to mouse
  observation only — growing the attack surface for a cosmetic nicety is exactly what
  the security contract exists to refuse. (Documented in ROADMAP.)

**New — Window Rules hotkey + tray item**
- The Rules manager gets the same treatment as the Pinned viewer: an optional
  bindable hotkey and a tray menu entry ("Window Rules (hotkey)"), for parity.

**Notes**
- Schema v8 → **v9** (HotkeyPeek, HotkeyRules; both default unbound — no-op
  migration). Version → **0.21.0**.

**Docs addendum (same zip, no code):** the ROADMAP gains a 🔺 MAJOR item — an
**online update checker/updater** (my request): startup + periodic version check,
prompt with Download & Update / Ignore This Version (silenced until the NEXT release) /
Cancel (re-prompts next startup), plus a manual "Check for Updates" button in Settings.
Because an updater is the app's biggest potential attack surface, the entry specs the
security requirements as non-negotiable (signed manifest over HTTPS, SHA-256 +
Authenticode verification before execution, downgrade refusal, never-silent installs,
an off switch, zero telemetry) and hard-orders it AFTER code signing. SECURITY.md's
"no network" rule gains a matching planned-exception note so the two docs can't drift.
Two follow-ups I asked for: the updater entry now requires every update-path
event (checks, the three-way choice, verification pass/fail, swallowed errors) to be
logged through the main LogService under an "Updates" category — reconstructable from
the log alone; and Stability gains a 🔺 HIGH **logging completeness audit**: sweep all
20 current silent `catch { }` blocks and every command path so nothing fails
invisibly, with new per-category filter checkboxes in the Log Viewer (+ DEBUG-level
default for the noisy additions) to keep the default view uncluttered.

**Verify on Windows:** bind Peek in the hotkey editor → press it anywhere → all zones
flash on-screen for ~1.5s and restore (dots that were hidden go back to hidden); bind
the Rules hotkey → opens the manager; tray shows both entries with their bindings.

---

## Session 20 — Security-First Pass, Config Trust Fixes, Window Rules & Feedback Batch

The biggest session yet: security formalized as priority #1, the config-loss root
cause fixed, the full feedback batch, and the next dense feature.

**SECURITY — now the top standing requirement (new `SECURITY.md`)**
- OnTop is treated as a massively-used consumer program: `SECURITY.md` defines the
  threat model, standing rules, and a per-session Security Review Checklist that the
  workflow now requires. Concrete hardening this session: an **app manifest pinning
  `asInvoker`** (OnTop can never silently elevate; also declares PerMonitorV2 DPI),
  **config load hardening** (4 MB size gate before parsing; every value clamped/
  validated on load — opacity, intervals, list sizes, string lengths), **window-title
  sanitization** (titles are attacker-controlled text — control chars stripped and
  length capped before they reach logs/UI, blocking log forging), an **exception
  guard around the mouse-hook callback** (an escaped exception in the system input
  chain could destabilize input for every process), and **own-window immunity** in
  every foreign-window path. Foreign windows only ever get Z-order changes — never
  memory access, never injection; the new rules feature was designed under these
  rules from the start.

**Fixed — the config prune was too aggressive (my diagnosis was right)**
- Old behavior: a config written by a NEWER build was discarded and OnTop fell back
  to defaults — so running an older build after a newer one (old extract folders,
  testing a previous zip) gutted your good config the moment it saved. New behavior:
  newer configs are **loaded best-effort** (the JSON loader ignores unknown fields, so
  zones, hotkeys, placements, and column widths all survive), and the file is NOT
  overwritten until you act. This was the likely cause of "main window placement lost
  when starting a new version."

**New — plain-language config prompts (Continue / Cancel)**
- If the config is corrupt, or came from a newer version, OnTop now explains it in
  plain words at startup — "Your saved settings were created by a newer version…" /
  "OnTop couldn't read your saved settings…" — with **Continue** or **Cancel (Close
  OnTop)** so you can restore a backup first. No more silent resets.

**Fixed — column widths now persist (all grids)**
- Column widths were never saved anywhere — that's why they reset. They now persist
  in the user config (`ColumnWidths`, new `GridColumns` helper) for the main zone
  list, the pop-out list, the Pinned Windows grid, and the new Rules grid — saved on
  close/exit, restored on open. The pins grid also gets sane initial widths +
  MinWidths so it never starts jumbled.

**Pinned Windows viewer — feedback round**
- "Pinned By" now shows **"Other"** for windows pinned outside OnTop (e.g. MSI
  Afterburner) instead of a blank cell.
- **"Unpin All (Ours)" → "Unpin All (OnTop Pins)"**.
- **OnTop's GUI is immune**: the toggle hotkey ignores our own windows, unpin paths
  skip them, enumeration excludes them, and the viewer re-asserts its topmost after
  bulk operations — it can never bury itself again.
- **Pin/unpin toasts**: pinning never had a toast — now "Always On Top ON — process"
  (green) / OFF, per-row unpin, and a count toast on Unpin All. (The toast window was
  already topmost; the calls simply didn't exist.)

**Fixed — split magnet lock/unlock (no more center freak-out)**
- Root cause: GridSplitter applies its own resize before our handler every tick — the
  two fought at the lock point. The divider is now a plain Thumb we drive entirely
  ourselves, with a real state machine: **hard lock** when the pointer's intended
  split enters ±2% of center, **stays locked** until the pull exceeds ±5.5%
  (hysteresis, no chatter), then **re-bases at the release edge** so travel continues
  smoothly from where it let go — no jump to the cursor.

**New — Window Rules (auto-pin) — dense roadmap item #2**
- **APP → "Rules..."**: rules like "process (or title) contains X → pin it
  automatically." Master switch (**off by default** per SECURITY.md), per-rule
  enable, live editing (click-out commits like the zone grids), Add/Remove.
- A 3-second scanner pins matches; rule pins count as OnTop pins (badged in the
  viewer, released by Unpin All). **Hand-unpinning wins**: a window you unpin is not
  re-pinned until it's closed and reopened. Toast + log on each rule pin.

**QOL — window persistence audit**
- Size/position memory added to Help, Auto-Close, Exceptions, and the Overlay Legend
  (legend keeps its classic bottom-right default until you move it), joining main /
  map / list / logs / pins / rules. Modals stay centered by design.

**Notes**
- Schema v7 → **v8** (ColumnWidths, WindowRules, WindowRulesEnabled; no-op
  migration). Version → **0.20.0**. `SECURITY.md` added to the zip contract.

**Verify on Windows:** resize columns in any grid → restart → they hold; run an older
build after this one → your config survives and the newer-config prompt appears; break
settings.json on purpose → plain-language prompt; pin something → toast appears even
over other apps; Pinned viewer shows "Other" for MSI Afterburner; Unpin All can't bury
the viewer; drag the split slowly — solid lock, clean release, no jump; add a rule
(e.g. "notepad"), enable rules, open Notepad → auto-pinned with toast. Paste any build
errors verbatim.

---

## Session 19 — Pinned Windows Viewer, Master Prompt & Split-Magnet Fix

The first of the dense roadmap items, plus workflow tooling and a feel fix.

**New — Pinned Windows viewer (dense roadmap item #1)**
- **APP → "Pinned...", tray → "Pinned Windows", or its new optional hotkey** opens a
  viewer listing every window currently marked always-on-top on the system — process,
  title, and a **"Pinned By: OnTop" badge** on the ones this app pinned this run
  (OnTop now tracks its own pins). Windows pinned before OnTop started, or by other
  tools, appear too (no badge) and can be managed the same way.
- **Per-row Unpin**, **Unpin All (Ours)** (releases everything OnTop pinned), and
  **Refresh** (enumeration is on-demand, not a live watcher — cheap and predictable).
  OnTop's own GUI windows are excluded from the list. Dead-window handles are pruned.
- Remembers its size/position ("pins" placement key). New optional **Pinned Windows
  Viewer hotkey** in the hotkey editor. Help gets a "Pinned Windows" section.

**New — MASTER_PROMPT.md (new-session bootstrap)**
- The zip now ships a copy-paste **master prompt** for starting fresh sessions: which
  docs to read in what order, the workflow summary, and my standing instructions. It's part
  of the continuity contract now — the zip command, self-checks, and doc-role list all
  include it, and the standing rule is to UPDATE it whenever the workflow or doc set
  changes.

**Fixed — the split "grab" is now felt while dragging**
- 0.18.0 snapped on *release*, which reads as nothing happening. The magnet now acts
  **during the drag**: the divider is positioned from cumulative drag distance, and
  while the computed split is within ±4% of the default it pins to exactly center —
  so it visibly sticks, then releases naturally once your total pull crosses the
  window. (Per-delta pinning would have trapped slow drags; cumulative tracking is
  what makes the escape feel right.) The release-ease remains as a finisher.

**Notes**
- Schema v6 → **v7** (`HotkeyPins` + "pins" placement key; no-op migration).
  Version → **0.19.0**.

**Verify on Windows:** pin a window with the toggle hotkey → open Pinned... → it's
listed with the OnTop badge; Unpin from the row; pin two, Unpin All (Ours); a window
made topmost by another tool shows without the badge. Drag the split divider slowly
through center — it should visibly hold, then release. Bind the new Pinned Windows
hotkey and try it. Paste any build errors verbatim.

---

## Session 18 — Log-Driven Fixes, Split Divider, Map Context Menu & Zone Search

Four items from testing feedback (including a user-supplied log file), plus additions.

**Fixed — the 30-second "Setting changed: AutoCloseTriggers" log spam**
- My log file pinpointed it: the crash-recovery auto-draft (every 30s while
  the window is open) reassigned `Cfg.AutoCloseTriggers` with a fresh list each tick —
  a new reference every time, so PropertyChanged fired and the settings logger recorded
  a phantom change. Now the draft only reassigns when contents actually changed, and
  bulk-sync list properties are excluded from the settings-changed log entirely (their
  individual changes are already logged as actions). Bonus: list-typed settings that
  do get logged now print their contents instead of "List`1[System.String]".

**Fixed — log Follow actually follows**
- Classic WPF timing bug: the follow-scroll ran synchronously inside CollectionChanged,
  before the virtualized list generated the new row — so ScrollIntoView silently
  no-oped. The scroll is now deferred until after layout, so Follow tracks the newest
  entry as it arrives. Re-checking Follow jumps to the end immediately, and the whole
  log toolbar now has hover tooltips (Follow had none — fair complaint).

**New — drag the map/list split, with a light snap back to center**
- The gap between the map and the list is now a grab handle: drag left/right to give
  either pane more room (sensible minimum widths on both sides). Release it within a
  small window of the default split and it eases gently back — a soft magnet at
  center; anywhere beyond that it stays where you put it.

**New — right-click the blank gray map space**
- The map's empty space now has a context menu: **Show Monitor N** (one click per
  hidden monitor — replacing the type-the-numbers dialog as the primary unhide path),
  **Show All Hidden Monitors**, plus **Fit / Reset View** and **Flash All Zones**.
  Works on the embedded map and the pop-out. (The old dialog remains reachable from
  the monitor right-click menu as a fallback.)

**New — zone list search box**
- A filter box above the list narrows it live by zone name or group; composes with the
  group dropdown. The map is unaffected (it always draws all zones).

**New — copy from the Log Viewer**
- Select entries (multi-select supported) and Ctrl+C or right-click → Copy Selected /
  Copy All (Filtered) — formatted exactly like the log file, for easy sharing.

**Notes**
- No schema change. Version → **0.18.0**.

**Verify on Windows:** leave the settings window open 60+ seconds → no more
AutoCloseTriggers entries; open View Logs with Follow on → new entries stay in view;
drag the map/list divider (release near the middle → soft snap back); right-click
blank map space → unhide/fit/flash menu; type in the list filter box; select log rows →
Ctrl+C and paste. Paste any build errors verbatim.

---

## Session 17 (cont.) — Build Fix (0.17.3)

- Fixed CS0121 in `RecomputeZoneHealth`: it called `Math.Round` on `Zone.X`/`Y`,
  which are already `int` — no overload takes an int, so the compiler couldn't pick
  between the decimal/double conversions. The rounding was pointless on ints and is
  simply removed. Swept every other `Math.Round` in the codebase — all operate on
  real doubles, so this was the only instance.

---

## Session 17 (cont.) — Build Fix (0.17.2)

- Removed a duplicate `ZoneMapCanvas.ResetView` method that slipped in with the S17
  Fit button — the canvas already had one (wired to the map's "Reset Zoom / Pan"
  right-click item), so 0.17.1 failed to compile (CS0111). One definition now; the
  Fit button and context item both use it. Recorded the lesson in PROJECT_SCOPE
  build rules: brace-balance can't catch duplicate methods, so grep before adding one.

---

## Session 17 (cont.) — Adaptive Monitor Header + Documentation Audit

**Adaptive "Monitor" column header (0.17.1)**
- The zone-list monitor column now reads **"Monitor"** by default and collapses to
  **"Mon"** when the column is dragged too narrow to fit — via a new
  `Helpers/WidthLabelConverter` (ConverterParameter `"56|Mon|Monitor"`) bound to the
  header's `ActualWidth`. Both lists.

**Documentation audit (no code)**
- Full skim of the codebase against the docs so the next session has complete
  context. Fixes to `PROJECT_SCOPE.md`: the **Feature Inventory** was stale (~S12) —
  brought current through S17 (config relocation, tray Quick Record, reorder, Locate,
  health badges, CLI flags, startup toast, bindable re-hook hotkey, nudge, Duplicate
  To Monitor, Copy Details, Fit/Flash-All buttons, Monitor column); **Not Yet Built**
  no longer lists already-shipped items (window-placement memory, `%APPDATA%` importer)
  and now surfaces the Macro Subsystem as the end-goal capstone; the **Session
  Workflow** zip command was missing `CHANGELOG.md` + the `bin/obj` excludes (fixed);
  and the **Architecture Map** now names the notable helpers (`AppInfo`,
  `WindowPlacement`, `GridEdit`, `ZoneListKeys`, `WidthLabel`) and the canvas's
  Locate/flash/ResetView additions.

Version → **0.17.1**. No schema change.

---

## Session 17 — Map Fit / Flash-All Buttons + Monitor Column

Three small, self-contained additions (verified after checking that export/import and
the status bar already existed).

**New — "Fit / Reset View" button on the map**
- The map toolbar gains a viewfinder button that resets zoom and pan back to the
  centered fit-all view — handy after you've zoomed/panned in and want to get back to
  the whole desktop. (`ZoneMapCanvas.ResetView`.)

**New — "Flash All Zones" button on the map**
- A ripple button next to it pulses every zone on the map at once — a quick "where are
  all my zones" peek. Reuses the Locate flash, extended to flash the whole set together.
  (`ZoneMapCanvas.FlashAllZones`.)

**New — "Mon" column in the zone list**
- Both lists now show each zone's 1-based monitor number (matching the "Duplicate To
  Monitor" menu), so on a multi-monitor setup you can see zone distribution at a glance.
  Updates live when a zone is dragged across monitors.

**Notes**
- No schema change (the monitor column is computed, not stored). Version → **0.17.0**.

**Files touched:** `Controls/ZoneMapCanvas.cs` (ResetView, FlashAllZones + flash render),
`Views/MainWindow.xaml(.cs)` (two toolbar buttons + handlers, Mon column),
`Views/ZoneListWindow.xaml` (Mon column), `Models/Zone.cs` (MonitorLabel), `OnTop.csproj`.

**Verify on Windows:** zoom/pan the map, then click the viewfinder button → view resets;
click the ripple button → all zones pulse; check the "Mon" column shows the right monitor,
and drag a zone to another monitor to see it update. Paste any build errors verbatim.

---

## Session 16 — Test-Feedback Fixes + Nudge / Duplicate-to-Monitor / Copy Details

Four fixes from testing the 0.15.0 build, plus three additions.

**Fixed — startup status toast**
- On launch, a toast now summarizes the session: enabled/total zone count, plus
  "detection paused" and "stealth on" when applicable (warning-colored if either is
  active). No more silent starts.

**Fixed — Locate is now a single click, and the warning slot is the click target**
- The leftmost list column is now a clickable cell on *every* row: a dim ◎ normally
  (tooltip "Locate on map"), or the amber ⚠ when the zone has a health issue (tooltip =
  the issue). Clicking either flashes the zone on the map — no more hunting for a
  non-editable spot to double-click. (Double-click a row still works too.)

**Fixed — off-screen detection now uses real monitor rectangles**
- Off-screen was tested against the virtual-desktop bounding box, which misses the
  gaps between mismatched monitors. It now checks each monitor's rectangle, so a zone
  stranded in a gap is flagged. Note: dragging a zone on the map clamps it to the
  desktop bounds, so on a single monitor you can't drag one truly off-screen — this
  mainly catches coordinate edits, gaps, and zones left behind when a monitor is
  unplugged. (Duplicate-name detection was already working.)

**Fixed — Re-Enable Mouse Hook is now a bindable hotkey**
- It was hardcoded to Ctrl+Alt+R and hidden from the hotkey editor. It's now a proper
  bindable row ("Re-Enable Mouse Hook (Optional)"), still defaulting to Ctrl+Alt+R so
  nothing changes unless you rebind it. (Ctrl+Alt+P for Pause stays fixed.)

**New — Ctrl+Arrow nudges the selected zone**
- With a zone selected in the list, Ctrl+Arrow moves its anchor 1px (Ctrl+Shift+Arrow =
  10px), undoable — for precise positioning without dragging. Plain arrows still
  navigate the list.

**New — Duplicate a zone to another monitor**
- On a multi-monitor setup, the zone right-click menu gains "Duplicate To Monitor →"
  with an entry per monitor; it copies the zone to the *same relative position* on the
  chosen monitor. (Hidden on single-monitor setups.)

**New — Copy Details**
- Right-click → "Copy Details" puts a zone's name, position, monitor, kind, action,
  group, tolerance/cap/variation, and enabled/overlay state on the clipboard — handy
  for sharing a config or reporting an issue.

**Notes**
- Schema v5 → **v6** (bindable `HotkeyRehook`; no-op migration — defaults to the old
  Ctrl+Alt+R, so existing configs are unchanged). Version → **0.16.0**.

**Verify on Windows:** relaunch → startup toast shows zone count + mode. Click the
◎/⚠ in the left column of any row → the zone flashes; hover a ⚠ → its issue. Put a zone
in a monitor gap or edit its coords off-screen → ⚠ appears. Open the hotkey editor →
"Re-Enable Mouse Hook" row is there. Select a zone, Ctrl+Arrow nudges it. On two
monitors, right-click → Duplicate To Monitor. Right-click → Copy Details, then paste.
Paste any build errors verbatim.

---

## Session 15 (addendum) — Macro Subsystem Set as Roadmap End Goal (planning, no code)

Captured the long-term direction: my separate macro recorder will be folded
into OnTop as an **optional, modular record/playback module** — the roadmap's end-goal
capstone. Key decisions written into ROADMAP's "🎯 End Goal" section:

- **Full-fidelity "session" recording** — capture all keyboard + mouse input
  *including mouse movement* with real timestamps, so playback reproduces exactly
  what the user did at real-time speed ("a remote user driving the machine"), delays
  preserved.
- **Three-layer anti-cheat guard** so it can always be subverted: (1) optional module
  that installs nothing when off, (2) silent unhook (reuses Stealth), and (3) a
  **detection-triggered kill switch** that *fully disables* macros when a watched
  process / window / zone appears — with graduated response levels.
- **Per-game policy + fail-safe defaults** (competitive/kernel-anti-cheat titles
  default to disabled), a known-risky-process list, and blunt "can get you banned"
  warnings — never marketed as undetectable.
- Lands **last**, behind a master toggle, so it can't destabilize the on-top core.

No version change (planning only).

---

## Session 15 — Reorder, Locate, Health Badges, Row Accent & CLI Flags

Five self-contained additions.

**New — zone priority reorder (Move Up / Move Down)**
- Detection is first-match by list order, so when zones overlap the earlier one
  wins. The zone right-click menu now has **Move Up (Higher Priority)** / **Move
  Down (Lower Priority)** (disabled at the ends) to control that order. Moves are
  relative to the full list — what actually drives detection — and are undoable.

**New — Locate: double-click a list row to flash the zone on the map**
- Double-clicking a zone in the list (main or pop-out) pulses an expanding, fading
  ring on that zone on every open map (embedded and pop-out), so you can find it
  at a glance. Purely visual, ~0.7s, no state change.

**New — zone health badges**
- The list shows a ⚠ next to any zone that's **off-screen** (its anchor isn't on
  any monitor), has **no hit area** (size, tolerance, and variation all 0), or has
  a **duplicate name**. Hover the ⚠ for the specific issue(s). Recomputed live as
  zones are added, removed, moved, renamed, or resized. Shown in both lists.

**New — selected-row accent bar**
- The selected zone row now gets a blue left accent bar, so the current selection
  reads at a glance (a small visual-cohesion win).

**New — command-line flags**
- `--paused` starts with zone detection paused; `--minimized` starts in the tray
  without showing the window (already the default — honored and logged so a
  shortcut can request it explicitly). `--restart` remains the internal soft-restart
  handoff. Handy for tailored shortcuts.

**Notes**
- No schema change. Version → **0.15.0**.

**Files touched:** `Themes/DarkTheme.xaml` (accent bar), `App.xaml.cs` (CLI flags),
`ViewModels/MainViewModel.cs` (reorder, flash event, health recompute),
`Views/ZoneContextMenuFactory.cs` (Move Up/Down), `Controls/ZoneMapCanvas.cs`
(flash), `Views/MainWindow.xaml(.cs)` / `Views/ZoneListWindow.xaml(.cs)` /
`Views/MapWindow.cs` (Locate wiring + badge column), `Models/Zone.cs`
(HealthWarning), `OnTop.csproj`.

**Verify on Windows:** right-click a zone → Move Up/Down changes list order (and
detection priority for overlaps); double-click a list row → the zone flashes on
the map; make a zone off-screen / all-zero-size / same-named as another → a ⚠
appears with a tooltip; the selected row shows a blue left bar; launch with
`--paused` → starts paused (check the log/tray). Paste any build errors verbatim.

**Queued next:** Pinned Windows viewer, profiles, window rules, design-token pass,
AHK importer, and the shippable installer — see ROADMAP.

---

## Session 14 — Tray Quick Record, Override Cues & List Shortcuts

Three self-contained additions.

**New — Quick Record in the tray**
- The tray menu gains **Quick Record** (right under Toggle OnTop, showing its
  hotkey if bound). It captures a zone in the current record mode without opening
  the main window and auto-names it — the same action as the Quick Record hotkey,
  now reachable from the tray. This was the last remaining v9.5-parity tray action.

**New — the Action chip dims when a global toggle overrides it**
- When **Zones Trigger Stealth** is on, every zone is forced to Stealth regardless
  of its own Exit/Stealth setting — which made the per-zone Action chip misleading.
  Now, while that global toggle is on, the Action chip (in both the main and
  pop-out lists) dims and stops responding to clicks, with a tooltip explaining
  it's being driven by the global override. This matches how the sliders already
  dim under Slider Lock — a consistent "a global setting supersedes this" cue.

**New — Delete / F2 shortcuts in the zone list**
- With a zone selected in the list, **Delete** removes it (undoable, through the
  same command as the Remove button) and **F2** opens the rename prompt. Both are
  ignored while you're editing a cell, so they keep their normal in-editor
  behavior. `CanUserDeleteRows` is off so deletion always goes through the
  undo-aware path. Shared by the main and pop-out lists (`Views/ZoneListKeys`).

**Notes**
- No schema change. Version → **0.14.0**.

**Files touched:** `App.xaml.cs` (tray Quick Record), `Views/MainWindow.xaml` +
`Views/ZoneListWindow.xaml` (Action-chip override dim + `CanUserDeleteRows`),
`Views/MainWindow.xaml.cs` + `Views/ZoneListWindow.xaml.cs` (Delete/F2 wiring),
`Views/ZoneListKeys.cs` (new, shared handler), `Views/HelpWindow.cs`,
`OnTop.csproj`.

**Verify on Windows:** tray → Quick Record captures a zone (auto-named, window
stays hidden). Turn on Zones Trigger Stealth → the Action chips dim and won't
flip; turn it off → they're active again. Select a zone in the list and press
Delete (removes, and Ctrl+Z brings it back) / F2 (rename prompt); confirm neither
fires while editing a cell. Build errors get pasted verbatim into the next session.

**Queued next:** Pinned Windows viewer, profiles, window rules, design-token pass,
AHK importer, and the shippable installer — see ROADMAP.

---

## Session 13 (cont.) — 0.13.2 Follow-Up Fixes

- **Tray menu now dismisses.** The submenu fix in 0.13.1 worked, but the top-level
  tray menu still stayed on screen. Root cause is a long-standing Win32 quirk
  (KB135788): a context menu won't close on an outside click unless its owning
  window is the foreground window — and the tray icon is WinForms, so nothing WPF
  is foreground when you right-click it. Now the (hidden) main window is forced to
  the foreground first, so the menu closes on selection or click-away.
- **Zones created by double-clicking the map now appear immediately.** Zones made
  via Record repaint because the window re-shows after capture; a map double-click
  keeps the window visible, so the new zone wasn't being painted until some other
  refresh. Added an explicit repaint after a double-click create.

Version → **0.13.2**. No schema change.

**Files touched:** `App.xaml.cs`, `Controls/ZoneMapCanvas.cs`, `OnTop.csproj`.

**Verify on Windows:** right-click the tray, pick an item (or click elsewhere) →
the whole menu closes. Double-click an empty spot on the map → the zone shows up
right there. If a map-click zone *still* doesn't appear, note whether it shows
in the zone list (and whether it pops in after resizing the window or zooming the
map) — that'll pinpoint it.

---

## Session 13 (cont.) — 0.13.1 Follow-Up Fixes

Four issues from retesting the 0.13.0 build.

- **Window size now actually restores across a restart.** Root cause: placement
  was captured from `Window.Width/Height`, but with the custom window chrome a
  drag-resize updates `ActualWidth/ActualHeight` and leaves `Width/Height` at the
  XAML defaults — so every save wrote the *default* size and restore faithfully
  applied it. Now the size is read from `ActualWidth/ActualHeight` (and
  `RestoreBounds` when maximized/minimized), so the real size persists.
- **Clicking the empty area of the list now commits an edit too.** The commit
  test treated any click inside the grid's bounds as "still editing"; it now
  commits unless the click landed on an actual row, so the empty space below the
  rows works like clicking outside the grid.
- **Tray submenus now close properly.** The themed MenuItem's sub-popup was named
  `subPopup`; WPF's MenuItem manages its submenu by the template-part name
  `PART_Popup`, so without that exact name the submenu opened (via our binding)
  but never joined the menu's dismiss chain and stayed on screen. Renamed to
  `PART_Popup` — submenus now open and close with the rest of the menu.
- **Renamed the confusing "Dot" column to "Overlay"** in both the main and pop-out
  lists (it toggles whether the zone is painted on the desktop overlay). Help
  text updated to match.

Version → **0.13.1**. No schema change.

**Files touched:** `Helpers/WindowPlacement.cs`, `Helpers/GridEdit.cs`,
`Themes/DarkTheme.xaml`, `Views/MainWindow.xaml`, `Views/ZoneListWindow.xaml`,
`Views/HelpWindow.cs`, `OnTop.csproj`.

**Verify on Windows:** resize the main window, exit fully, relaunch → it should
reopen at the saved size (also try Soft Restart). Click the empty part of the list
while editing a cell → commits. Tray → Pause For → the submenu opens *and* the
whole menu closes after you pick or click away. The list column now reads
"Overlay". Paste any build errors verbatim.

---

## Session 13 — Bug-Fix Pass (S11–S12 issues) + Config Relocation

Six issues surfaced by a real build, plus moving config into the user profile.

**Fixed — window size/position now actually persists**
- Placements were only flushed to disk at app *exit*, and "Save and Close" didn't
  capture the live window size — so a resize usually didn't stick. Now the main
  window's size/position is tracked into settings continuously as you resize/move,
  and every window (main + pop-outs) persists immediately on close via a new
  lightweight `SaveNoBackup` write (atomic, but doesn't rotate the `.bak` chain).
  So a resize survives whether you close to tray, hit Save and Close, or exit.

**Fixed — clicking out of a grid edit (e.g. Group) now commits**
- The outside-click detection walked the visual tree, which could misfire under
  mouse capture and think an outside click was inside the grid. It's now
  coordinate-based (pointer position vs the grid's bounds), which is immune to
  that. Clicking any cell, button, or empty space commits the edit.

**Fixed — tray "Pause For" (and "Zones") submenus do nothing**
- Root cause: the app's themed MenuItem template had no submenu popup at all — it
  only rendered the header, so *no* submenu could ever open. Rewrote the template
  to include the expand arrow and a themed sub-popup. Both tray submenus now open
  and work; any future submenus will too.

**Fixed — overlay opacity slider had no visible effect**
- The opacity was applied via `Window.Opacity` on the transparent, click-through
  overlay, which doesn't reliably repaint its child visuals. It's now applied to
  the overlay's content root instead, so the slider visibly fades the dots/boxes.

**Fixed — zone list rows had no right-click menu**
- Only the map had the zone context menu. The list now opens the same menu
  (rename, group, color, action, enable, etc.) on right-click — for the row under
  the cursor, in both the main list and the pop-out.

**Changed — config moved to `%APPDATA%\OnTop`**
- Config and logs were stored beside the .exe. They now live in
  `%APPDATA%\OnTop` (`config\`, `logs\`, `assets\`), so settings survive updates
  and work when OnTop is installed somewhere read-only (Program Files) — this is
  also a prerequisite for the eventual installer. A config left beside the exe by
  an older version is **migrated automatically** on first run (settings + backups
  + any custom icon). A config written by a *newer* build (schema we don't
  understand) is pruned — skipped in favor of defaults, with the file left intact.
- The old beside-exe location and the shipped-icon fallback path are both still
  honored and are documented as must-maintain in PROJECT_SCOPE so they aren't lost
  in future refactors.

**Notes**
- No schema change (still v5). Version → **0.13.0**.

**Files touched:** `Themes/DarkTheme.xaml` (submenu template),
`Helpers/GridEdit.cs` (coordinate-based commit), `Views/MainWindow.xaml(.cs)`,
`Views/ZoneListWindow.xaml.cs`, `Views/MapWindow.cs`,
`Views/LogViewerWindow.xaml.cs` (placement persist + list right-click),
`Views/ZoneContextMenuFactory.cs` (`ShowForRow`), `Views/ZoneOverlayWindow.cs` +
`Services/OverlayService.cs` (content-root opacity), `Services/SettingsService.cs`
(`SaveNoBackup`, `%APPDATA%` relocation + migration + pruning),
`Views/HelpWindow.cs`, `OnTop.csproj`.

**Verify on Windows:** the editing environment can't compile WPF — xmllint + brace/paren balance
+ full symbol cross-refs pass here. Please retest all six: (1) resize the window,
close to tray, reopen → size restored; also try Save and Close then exit/relaunch.
(2) Edit the Group cell, click a button/empty space → value commits. (3) Tray →
Pause For → 15 Minutes → detection pauses and the submenu actually opens; (4) tray
→ Zones → toggle one. (5) With a zone's Dot on, drag Overlay Opacity → the dot
visibly fades. (6) Right-click a row in the list → context menu appears. Also
confirm existing zones/settings **carried over** after the config move (they
should have been migrated from the old folder). Build errors get pasted verbatim into the next session.

**Queued next:** Pinned Windows viewer, profiles, window rules, design-token pass,
and the shippable installer — see ROADMAP.

---

## Session 12 — Grid-Edit Commit, Timed Pause & Per-Zone Tray Toggles

Three self-contained quality-of-life wins.

**New — clicking away commits a zone-list edit**
- Editing a cell in the zone list (Name, X/Y, sliders, Group…) and then clicking
  elsewhere used to leave the typed value uncommitted until you pressed Enter or
  clicked another row — a real papercut. Now a click anywhere outside the grid
  commits the edit. Works in both the main list and the pop-out list.
- New reusable `Helpers/GridEdit.cs`: each window calls it from PreviewMouseDown;
  it commits only when the click lands outside the grid (a click inside is left
  alone), and committing when nothing's being edited is a harmless no-op.

**New — timed pause from the tray**
- The tray menu gains a **Pause For** submenu — 15 min / 30 min / 1 hour / 2 hours.
  Detection pauses immediately and **auto-resumes** when the time is up, with a
  toast. While a timed pause is active, the Resume item shows the remaining time
  ("Resume Detection (Auto In 22 Min)").
- Resuming early by any path — the tray toggle, the Ctrl+Alt+P hotkey, or manually
  — cancels the pending timer so it can't fire later. (A single PropertyChanged
  watch on DetectionPaused handles all resume paths.)

**New — per-zone enable/disable from the tray**
- Alongside Enable/Disable All, a **Zones (N)** submenu lists every zone and flips
  it on or off directly from the tray — a ✔ marks the enabled ones. The menu is
  rebuilt on each open, so the marks and the count are always current.

**Plumbing**
- Named both zone DataGrids (`ZonesGrid`) so the commit helper can reach them.
- `App` gained a `DispatcherTimer` + target-time for the timed pause, cleared on
  resume and on exit.
- Help gained a **System Tray** section documenting these.
- Version → **0.12.0**. No schema change (nothing new persisted).

**Files touched:** `Helpers/GridEdit.cs` (new), `Views/MainWindow.xaml`
(+`.xaml.cs`) and `Views/ZoneListWindow.xaml` (+`.xaml.cs`) (named grid +
PreviewMouseDown commit), `App.xaml.cs` (timed pause, zone submenu, timer
lifecycle), `Views/HelpWindow.cs` (System Tray section), `OnTop.csproj` (version).

**Verify on Windows:** the editing environment can't compile WPF — xmllint + brace/paren
balance + full symbol cross-refs pass here. Watch especially: editing a grid cell
then clicking a button/empty space commits the value (and doesn't interfere with
normal cell-to-cell editing); **Pause For** pauses and then auto-resumes at the
right time, the remaining-time label updates on reopen, and resuming early kills
the timer; and the **Zones** submenu toggles the right zone with correct ✔ marks.
Paste any build errors verbatim.

**Queued next:** Pinned Windows viewer, %APPDATA% importer, profiles, window
rules, design-token pass, and the shippable installer — see ROADMAP.

---

## Session 11 — Distinguishable Pop-Out Icons + Window Size/Position Memory

Two things: the two pop-out buttons are now visually distinct, and every window
remembers where you put it.

**Changed — the pop-out buttons are now tellable apart**
- Both pop-out buttons (top-right of the Zones panel) used the *same* Segoe MDL2
  glyph, so you couldn't tell which popped out the map vs the list. They're now
  two composed vector icons:
  - **Map** → a folded-paper-map shape with a small blue pop-out arrow.
  - **List** → a bulleted-list shape with the same pop-out arrow.
- The shared arrow reads as "opens in its own window"; the different base shapes
  (map vs list) tell them apart at a glance. Drawn from the theme foreground +
  accent so they match the rest of the chrome and scale crisply.

**New — windows remember their size and position**
- The main window and all three pop-outs (map, list, log viewer) now save their
  size and position and restore it next time they open, so you never have to
  re-size or re-place them. Last zone view mode was already saved; this completes
  the picture.
- **Off-screen safe.** A window saved on a monitor you later unplug (or at a
  resolution that shrank) would otherwise open stranded with no reachable title
  bar. Restore clamps the geometry onto the current virtual screen — sizes cap to
  what fits, and the position is nudged so a usable strip of the title bar always
  lands on a visible monitor. Maximized state is preserved (via restore-bounds,
  so the un-maximized size survives too).
- **Persistence is careful about your backups.** `SettingsService.Save` rotates
  the .bak1/2/3 chain on every call, so writing on every window close would churn
  your real backups with placement-only saves. Instead, placements update in
  memory on close/hide and flush to disk **once at app exit** — the 30-second
  autosave still covers mid-session. New reusable `Helpers/WindowPlacement.cs`
  (Restore/Save) does the clamping; each window calls it from
  `OnSourceInitialized` (restore, before it's shown, so no flicker) and
  `OnClosing` (save).

**Plumbing**
- New `WindowRect` DTO + `AppSettings.WindowPlacements` dictionary keyed by a
  stable window name ("main"/"map"/"list"/"logs").
- `App.OnExit` now captures the main window's final placement and does one real
  settings save (also good hygiene — real settings are flushed on normal exit).
- Schema **v5** (forward-only, no-op): older configs load with an empty
  placements dict and just use each window's XAML default until first close.
- Version → **0.11.0**.

**Files touched:** `Views/MainWindow.xaml` (two vector pop-out icons; restore/save
in `MainWindow.xaml.cs`), `Models/AppSettings.cs` (WindowPlacements + WindowRect),
`Helpers/WindowPlacement.cs` (new), `Views/MapWindow.cs`,
`Views/ZoneListWindow.xaml.cs`, `Views/LogViewerWindow.xaml.cs` (restore/save),
`App.xaml.cs` (exit flush), `Services/SettingsService.cs` (schema v5),
`OnTop.csproj` (version).

**Verify on Windows:** the editing environment can't compile WPF — xmllint + brace/paren
balance + full symbol cross-refs pass here. Watch especially: the two pop-out
icons render crisply and are clearly distinct; each window restores its
size/position on reopen; and the **off-screen clamp** works — save a pop-out on a
second monitor, disconnect it, reopen, and confirm the window lands on the
primary display with a grabbable title bar (not off-screen). Paste any build
errors verbatim.

**Queued next:** Pinned Windows viewer, %APPDATA% importer, profiles, window
rules, design-token pass, and the shippable installer — see ROADMAP.

---

## Session 10 — Zone Actions Reorg + Remaining v9.5 Action Hotkeys

Cleaned up the main window's Zone Actions panel (it had become a jumbled row of
unrelated buttons) and finished the last of the v9.5-parity **action** hotkeys.

**Changed — Zone Actions is now grouped by purpose**
- The panel's buttons were spread across three arbitrary rows that mixed zone
  editing, config dialogs, file I/O, and app utilities. They're now four
  labeled subsections, each holding only things that belong together:
  - **RECORD & EDIT** — the record-mode dropdown sits directly beside **Record**
    (so they read as the pair they are), then **Duplicate** and **Remove**.
  - **ZONE DATA** — **Import Zones** / **Export Zones**.
  - **CONFIGURE** — **Hotkeys** / **Exceptions** / **Auto-Close** / **Settings**
    (all the "open a config dialog" buttons in one place).
  - **APP** — **Logs** / **Help**.
- Labels use the same muted small-caps style as the ADJUSTMENT / TOGGLES headers,
  so the grouping is obvious at a glance. No behavior changed — every button does
  exactly what it did, just findable now.

**New — the last three v9.5-parity action hotkeys (all optional, default-unbound)**
- **Quick Record** — records a zone in the current mode *without opening the main
  window*. It auto-names the zone and only restores the window if it was already
  open, so it won't pop up over a game. Great for grabbing a zone mid-session.
- **View Logs** — opens the live Log Viewer from anywhere.
- **Soft Restart** — saves settings and relaunches OnTop. Handled in `App` (which
  owns the single-instance mutex): it flushes settings, starts a fresh copy with
  a `--restart` argument, and shuts down. The relaunched instance **waits up to
  ~3s for the mutex** to free instead of bouncing off single-instance detection,
  so the handoff is clean and nothing is left un-running.
- All three live in the **Actions** section of the Hotkeys… dialog alongside
  Always-On-Top / Exit / Settings / Kill, are recorded/cleared the same way, and
  share the existing 30-second bind-retry loop on conflict.

**Plumbing**
- The record flow was refactored into one `StartRecording(quick)` method backing
  both the Record button (prompts for a name) and Quick Record (auto-named); the
  window exposes `QuickRecord()` and `ShowLogs()` for `App` to call, following the
  established VM-event → View pattern (`RecordRequested`, `LogsRequested`,
  `RestartRequested`, mirroring `ToggleSettingsRequested`).
- Settings **schema bumped to v4** (forward-only, no-op): the three new hotkey
  fields default to unbound, so v3 (and earlier) configs load untouched.
- Version → **0.10.0** (subtitle + Help header update automatically).

**Files touched:** `Models/AppSettings.cs` (3 hotkey fields),
`Services/SettingsService.cs` (schema v4), `ViewModels/MainViewModel.cs`
(register + callbacks + 3 events), `App.xaml.cs` (event subs, `SoftRestart`,
`--restart` mutex wait), `Views/MainWindow.xaml.cs` (StartRecording refactor +
`QuickRecord`/`ShowLogs`, Services using), `Views/MainWindow.xaml` (Zone Actions
reorg), `Views/HotkeyDialog.cs` (3 action rows), `Views/HelpWindow.cs` (hotkey
docs), `OnTop.csproj` (version).

**Verify on Windows:** the editing environment can't compile WPF — xmllint + brace/paren
balance + full symbol cross-refs pass here. Watch especially: the reorganized
Zone Actions panel lays out cleanly at normal and narrow widths; Quick Record
from a hotkey while the window is hidden (auto-names, doesn't pop the window) and
while it's open (restores it); View Logs opens the viewer; and **Soft Restart
actually relaunches** — confirm the new instance comes up and the old one exits
(watch for a double instance or nothing running, which would mean the mutex wait
needs tuning). Paste any build errors verbatim.

**Queued next:** Pinned Windows viewer, %APPDATA% importer, profiles, window
rules, design-token pass, and the shippable installer — see ROADMAP.

---

## Session 9 — Recording Modes, Draw Zones & Per-Toggle Hotkeys

The big v9.5-parity item finally landed: **freeform Draw zones**, alongside
**Drag Box** recording and a **recording-mode selector** — plus an optional
hotkey for every main-GUI toggle, an app version stamp, and a new CHANGELOG.

**New — recording modes (the headline)**
- A **recording-mode dropdown** sits next to Record: **Point Click** (classic —
  hides the window, next click places the zone), **Drag Box** (drag a rectangle,
  zone sized 1:1 with Tolerance/Variation forced to 0), and **Freeform Draw**
  (hold and sketch any shape, release to close it). The choice persists as
  `AppSettings.RecordMode`.
- The capture overlay handles all three and doubles as a live legend: the pill
  shows the current mode and gesture, and **1 / 2 / 3** (or **Tab**) switch modes
  live mid-capture. Esc or right-click cancels, as before.
- **Draw zones detect against the real polygon** — an even-odd ray cast in
  anchor-relative coordinates — not a bounding box, so the sliders don't apply to
  them. Vertices are stored *relative to the anchor* so moving the zone moves the
  whole shape and JSON property order can never desync the two.
- **Self-intersection lockout** (v9.5 behavior): a shape whose outline crosses
  itself is rejected on release (turns red, pill explains) so ray-cast detection
  stays unambiguous — you just draw it again.
- Draw zones render as their true outline everywhere: the **zone map** (a
  `StreamGeometry` with real polygon hit-testing), the **desktop overlays** (a
  `Polygon` beside the box), and they still get **bounding-box resize handles** —
  dragging a corner scales the whole polygon from a **drag-start snapshot** so
  repeated integer scaling can't erode the shape.
- **Redraw Shape…** on the map right-click menu (Draw zones only) re-sketches a
  polygon in place — name, group, color, action, and enabled state all survive.
  Works from the embedded map and the pop-out map (the logic lives in the shared
  `ZoneContextMenuFactory`, and hides whichever window owns the menu during
  capture).
- A **Type** column (Point / Box / Draw) in the main list and the pop-out list.

**New — an optional hotkey for every toggle**
- The Hotkeys… dialog gained a **Toggles** section with a bindable,
  default-unbound hotkey for each of the nine main-GUI toggles: Desktop Only,
  Debug Overlay, Apply To All Zones, Override Exceptions, Confirm Before Exit,
  Lock Overlays, Keep GUIs On Top, Stealth Mode, and Zones Trigger Stealth.
- Pressing a bound toggle hotkey flips that setting live and toasts the new state
  (the GUI is usually hidden when a global hotkey fires, so the toast is the
  feedback). They register through the same path as the action hotkeys, so a
  conflict joins the existing 30-second retry loop and clears itself when the
  other program releases the key. The dialog is now scrollable so it stays a
  sane height with thirteen binds.

**New — version stamp + CHANGELOG**
- The build now carries a real **version** (`<Version>0.9.0</Version>` in the
  csproj). It's read from assembly metadata at runtime (no hard-coded string to
  drift) and shown in the **main-window subtitle** (“v0.9.0” beside the title)
  and the **Help header**. 1.0.0 is reserved for when the installer ships and
  v9.5 parity is complete. New `Helpers/AppInfo.cs` is the single source.
- New **CHANGELOG.md** — a terse, versioned fast-scan index (Added/Changed/Fixed
  per version) that complements this file: CHANGELOG is the headline, WHATS_NEW
  is the full story. Sessions now map to versions (this is 0.9.0).

**Changed**
- Slider pushes now **skip Draw zones** (both Apply-To-All and single-zone) since
  their hit area is the polygon, not the Size/Tolerance/Variation box.
- Settings **schema bumped to v3** — forward-only. Every new field (zone `Kind`
  + `RelPoints`, `RecordMode`, the nine toggle hotkeys) defaults exactly right
  for older files, so v2 configs load untouched.

**Files touched:** `Models/Zone.cs` (Kind, ZonePoint, polygon storage +
ray-cast Contains + snapshot scaling), `Models/AppSettings.cs` (RecordMode + 9
hotkeys), `Services/SettingsService.cs` (schema v3), `ViewModels/MainViewModel.cs`
(AddBoxZone / AddDrawZone / ReplaceDrawPoints, toggle-hotkey binding, slider
guard), `Views/ZoneCaptureOverlay.cs` (rewritten — three modes, live swap,
self-intersection lockout), `Views/ZoneOverlayWindow.cs` (polygon visual),
`Controls/ZoneMapCanvas.cs` (polygon render + hit-test), `Views/MainWindow.xaml`
(+`.xaml.cs`) (mode dropdown, Type column, version subtitle, Record dispatch),
`Views/ZoneListWindow.xaml` (Type column), `Views/ZoneContextMenuFactory.cs`
(Redraw Shape), `Views/HotkeyDialog.cs` (rewritten — Actions + Toggles),
`Views/HelpWindow.cs` (Recording Modes + toggle-hotkey docs + version),
`Helpers/AppInfo.cs` (new), `OnTop.csproj` (version).

**Verify on Windows:** the editing environment can't compile WPF — xmllint + brace/paren
balance + symbol cross-refs all pass here. Watch especially: the Draw capture
gesture and self-intersection rejection, polygon rendering/scaling on both the
map and the overlays, Redraw from the pop-out map, and each of the nine toggle
hotkeys flipping the right setting. Paste any build errors verbatim.

**Queued next:** Pinned Windows viewer, the remaining v9.5-parity hotkeys (Quick
Record, View Logs, Soft Restart), %APPDATA% importer, and the shippable
installer — see ROADMAP.

---

## Session 8 — Continuity Doc + Main-GUI Fixes + Themed Tray

**Hotfix (post-build):** NullReferenceException in the MainWindow constructor —
`ZoneMap.HiddenMonitors = VM.Cfg.HiddenMonitors` ran before App assigned the
DataContext, so `VM` was null. Moved that assignment into a DataContextChanged
handler; all other VM references were already inside deferred handlers.

**Continuity (never lose the project again)**
- New **PROJECT_SCOPE.md** in every zip: the full cross-session contract —
  standing requirements, build rules, architecture map, feature inventory, and
  a mandate for every later session to keep all four docs current and ship them in
  every zip. README and ROADMAP now point at it. Zips now contain
  PROJECT_SCOPE + README + WHATS_NEW + ROADMAP + full source.

**Fixes**
- **Close / minimize buttons** — rewired with direct Click handlers instead of
  SystemCommands RoutedCommands (which depend on command targets/focus a
  tray-shown window lacks). Minimize now hides to tray (no taskbar button
  exists to restore from otherwise). Also fixed the root cause of dead glyph
  buttons: the implicit TextBlock style no longer forces a FontFamily that
  leaked into templated content and clobbered the MDL2 caption glyphs.
- **Split mode is vertical** — map left, list right (was stacked).
- **Tray icon** — now loads from the shipped `Assets\thumbtack.ico` beside the
  exe (previously only a user-folder path was checked, so it fell back to the
  generic Windows icon). The icon is copied to output on build.
- **Groups and Tools relayout** — split into a clean **Zone Actions** box
  (three tidy rows, all tools) and a **Zone Groups** box with FILTER and ASSIGN
  mini-sections and properly separated fields. Every button carries a tooltip.

**New**
- **Themed tray menu** — the stock light WinForms menu is gone; right-clicking
  the tray now opens a WPF ContextMenu in the app's dark theme, including a
  **Re-Enable Mouse Hook** entry and a live-labeled stealth toggle.
- **Re-hook hotkey Ctrl+Alt+R** — leaves Stealth Mode and re-installs the hook
  from anywhere (for when you're out of a game that disallowed it).
- **Map zoom and pan** — mouse wheel zooms (anchored at the cursor), middle-
  click drag pans, and the monitor right-click menu has Reset Zoom / Pan.
- **Monitor hide / unhide** — right-click a monitor on the map to hide it;
  "Show Hidden Monitors..." restores by number (or all). Persisted, view-only.
- **Hover context everywhere** — tooltips on all sliders, toggles, and buttons
  (and they now show on disabled controls, with a longer read time). Help
  window expanded with Settings Toggles, Sliders, Zone Map, and Overlays
  sections.

**Queued next:** an optional (default-unbound) hotkey for every main-GUI
toggle — see ROADMAP. Deferred here to stay within session limits after a large
batch.

---

## Session 7 — Zone-Triggered Stealth + Dark Scrollbars/Tooltips

**Zone-triggered Stealth (the design as originally intended)**
- Session 6's Stealth Mode was manual-only; zones always exited. Now the zone
  itself is the trigger: every zone has an **Action** — Exit (classic) or
  **Stealth** (unhook the mouse and stay alive in the tray instead of closing).
  Use the app completely normally; when a Stealth zone registers a double-click,
  OnTop unhooks on the spot — nothing left for anti-cheat software to flag.
- **Action chip column** in both zone lists (red tint = Exit, blue = Stealth,
  click to flip, undoable), a right-click menu entry on the map, and the
  **"Zones Trigger Stealth (Not Exit)"** global override toggle that forces
  every zone to Stealth regardless of its per-zone setting.
- Stealth triggers skip Confirm Before Exit (nothing is closing — it's
  reversible). Hotkeys don't use the hook, so they keep working: come back via
  the tray, the settings panel, or Ctrl+Alt+R. Existing zones default to Exit,
  so nothing changes until you opt a zone in.

**Cohesion slice**
- **Dark scrollbars** — modern thin thumb-only style (hover/drag states),
  replacing the OS-light bars in the grids, lists, log viewer, and help.
- **Dark tooltips** — themed rounded tooltips replacing the stock light ones.
- Both roadmap items ticked; the full design-token pass is next in that lane.

---

## Session 6 — Stealth Mode + Stability Hardening

**Stealth Mode (anti-cheat safe)**
- New toggle on the main panel and in the tray ("Toggle Stealth Mode (No
  Hook)"). Unlike Pause — which keeps the hook installed and merely ignores
  clicks — Stealth **fully removes the global mouse hook**, so there is nothing
  installed for anti-cheat software to flag. Zone detection is off while it's
  on; flipping it back re-installs the hook live. Persisted across restarts,
  tray tooltip shows "STEALTH (Hook Off)", the watchdog knows not to resurrect
  the hook, and enter/exit both toast + log.
- Per-zone stealth is intentionally *not* a thing: the hook is process-global,
  so only removing it entirely helps with anti-cheats. The per-zone On column
  already covers "don't trigger here." Follow-ups on the roadmap: a stealth
  hotkey, auto-stealth while watched game processes run, overlay auto-hide.

**Stability (the ⚡ roadmap items)**
- **Backup auto-fallback** — a corrupt `settings.json` no longer silently
  resets to defaults: load now walks `.bak1` → `.bak3`, uses the first
  readable one, and toasts + logs exactly what was recovered (or warns loudly
  if truly nothing was readable).
- **Hotkey retry loop** — failed binds (my other OnTop holding F1/F10) now
  retry every 30 seconds silently; the moment the conflicting program closes,
  OnTop grabs the keys and toasts "Hotkeys Bound — The Conflict Cleared."
- **Atomic settings writes** — saves go to a temp file then rename, so a crash
  or power loss mid-write can never truncate `settings.json`.
- **Startup grace period** — zone triggers are ignored for the first 2 seconds
  after launch, so a stray double-click can't insta-exit the app as it starts.
- **Safety backup before restore** — restoring from a backup first snapshots
  the current state as its own timestamped backup.

**Hotkey dialog**
- Binds are now labeled explicitly: Always On Top / Exit / Toggle Settings are
  **(Optional)** — clearable to Unbound — and Kill is **(Required)** with its
  Ctrl+Alt+Q fallback. (The clear/optional behavior itself shipped in Phase 1.)

---

## Session 5 — Feedback Pass (All 14 Items) + Live Log Viewer

*(v9.5 source confirmed gone — everything below rebuilt from recovered records
and my live feedback.)*

**Fixed**
- **X / − caption buttons** — SystemCommands route to the keyboard-focused
  element by default, which a tray-shown window may not have. Both buttons now
  set `CommandTarget` to the window explicitly. Applies to every chrome window.
- **Record flow** — real v9.5 click-to-place: Record hides the window, a
  crosshair capture overlay with a hint pill appears, your next click places
  the zone there (Esc/right-click cancels), *then* the naming prompt shows.
- **Button clipping** — fixed widths removed everywhere (Undo/Redo now
  MinWidth); default button padding tightened; action rows rebalanced into
  cleaner two-row layouts per box.
- **Groups + map capture** — new zones (Record, map double-click) join the
  active group filter, so they always appear in the filtered list.
- **Hotkey bind failures** — clearer message: names the keys and points at the
  other program holding them (my other OnTop) + Rebind hint.
- **Restore confirmation** — new themed `ConfirmDialog` replaces the stock
  Windows MessageBox there *and* for Confirm Before Exit.
- **Your icon** — `ThumbTack.ico` installed as `Assets\thumbtack.ico`; tray,
  title bars, and the startup shortcut pick it up on next build.

**New**
- **Zone view modes** — Map / List / Split segmented toggle (persisted), both
  panes in one Zones section, and **pop-out buttons** that open the map and the
  list as their own resizable windows. Selection is one shared `SelectedZone`,
  so clicking a zone anywhere highlights it everywhere.
- **Right-click menu on map zones** (embedded + pop-out): Rename, Set Group,
  Cycle Color, Enable/Disable, Show/Hide Desktop Overlay, Duplicate, Remove —
  with new dark ContextMenu/MenuItem styles.
- **Live Log Viewer is back** — entries stream in as you use the program, with
  level filter, search, follow-scroll, Clear, and the **category tracking
  checkboxes** (General/System/Hotkey/Detection/Recording/Settings/Overlay,
  persisted; errors always log) plus a **Verbose (DEBUG)** toggle. LogService
  rebuilt with categories, ms timestamps, and a capped in-memory tail.
- **Keep GUIs On Top** — new toggle (default on); the main window, pop-outs,
  and the log viewer bind to it. Dialogs ride above their owner.
- **Settings dialog fleshed out** — grouped sections, overlay opacity slider
  with live %, verbose toggle, Log Viewer access, version footer, 560px wide.

**Polished**
- **Sliders** — full redesign: gradient-filled track, round white thumb with
  hover/drag glow (value bubble ships with the deeper visual pass).
- **Slider lock overlay** — now a soft gradient veil sized to the sliders with
  a proper "Sliders Locked" pill; sliders dim beneath it.
- **Transfer arrows** — the >>/<< text buttons replaced with crisp custom
  chevron graphics in Exceptions and Auto-Close.

---

## Session 4 — v9.5 Parity, Phase 1 (From Recovered Session Records)

The uploaded reference (`OnTopWPF_1.zip`) turned out to be a duplicate download
of the foundation zip, not the v9.5 `OnTopPolished` source — so Phase 1 was
rebuilt from the recovered records of the May sessions. **Still wanted:** the
real v9.5 source folder/zip, for the Phase 2 Draw/Drag-Box port. Search for
`.cs` files containing `SlidersLocked`, or files named `LogViewerWindow` /
`PinnedWindowsWindow` / `InputDialog`.

- **Slider lock** — "ADJUSTMENT" mini-header above the sliders with a lock
  toggle (padlock glyph, accent when locked). Locked = sliders dim to 55% and a
  translucent overlay swallows every click so nothing can be nudged by
  accident. Persisted as `AppSettings.SlidersLocked`, exactly like v9.5.
- **Settings dialog** (`Views/SettingsDialog.cs`, "Settings..." button) — Mica
  backdrop toggle (applies to all open windows live, honored by every future
  window via `ThemeOptions` + `Win32.ApplyBackdrop`), log retention days,
  Backup Now / Restore From Backup, and Open Config / Open Logs folder buttons.
  Theme + density + recording mode join it with later phases.
- **Backup / Restore** — timestamped JSON snapshots of all settings + zones in
  `config\backups\`; restore is confirmed, undoable, rebinds hotkeys, and
  reloads everything in place.
- **Two-zone toasts** — v9.5 layout restored: routine notices top-right,
  critical (Error) dead center. The legend moved to the bottom-right corner to
  clear the toast zone.
- **Saved timestamp in the status bubble** — Save no longer toasts; the status
  bar shows "Saved At h:mm:ss" (this was my explicit v9.5 feedback).
- **Settings-change logging** — every settings flip writes an ACTION line
  ("Setting changed: DesktopOnly = False"), ready for the Log Viewer port.
  OverlayOpacity is exempted to avoid slider-drag spam.
- **Zone naming on record** — the Record button now pops the dark, Topmost
  `InputDialog` (the v9.5 z-order fix) to name the new zone. Map double-clicks
  stay silent for speed.
- **Build QoL** — the v9.5 pre-build `taskkill` target: a running OnTop can
  never file-lock the build again.

---

## Build Fix — Nullable Warnings, Then Namespace Ambiguities

**Round 1 (CS8632, warnings only):** the whole codebase used nullable syntax
(`string?`, `Zone?`, `event Action?`) without the csproj opting into a nullable
context. Added `<Nullable>enable</Nullable>` and `<LangVersion>latest</LangVersion>`.

**Round 2 (CS0104, real errors — a self-inflicted one):** round 1 also added
`<ImplicitUsings>enable</ImplicitUsings>` in round 1, which auto-imports
`System.Drawing` and `System.Windows.Forms` *globally and unaliased*. Those
collide with WPF's `System.Windows.Media`/`.Controls`/`.Shapes` on shared type
names (`Color`, `Pen`, `Brush`, `Point`, `Rectangle`, `TextBox`, `Panel`,
`MouseEventArgs`, `Timer`, `Application`) — 30 ambiguity errors. Fix:
- **Removed `ImplicitUsings`.** The code already has explicit usings on every
  file, and each one imports its WPF namespace while pulling WinForms in only
  through a `Forms =` alias — so with the global imports gone, every ambiguous
  name resolves to the WPF type as intended.
- **`App.xaml.cs`** had one genuine pre-existing collision: it imported
  `System.Drawing` unaliased (for `Icon`/`SystemIcons`) alongside
  `System.Windows`, colliding on `Application`. Changed to `using Drawing =
  System.Drawing;` and qualified the two icon usages.

Final csproj nullable/lang settings only:

    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>

Lesson logged: `ImplicitUsings` is not safe in a mixed WPF + WinForms project.

---

## Session 3 — Original TODO List Complete (#2–#8) + Roadmap Quick-Wins

### Exceptions Sub-GUI (`Views/ExceptionsWindow.xaml`)
Two-panel editor: running processes (live filter, multi-select, double-click to add)
on the left, current exceptions on the right with independent Block Toggle /
Block Exit checkboxes, >> / << transfer buttons, refresh, and pipe-delimited
.txt import/export (`name|blockToggle|blockExit`). All mutation goes through the
VM; changes apply immediately and persist on the main Save.

### Auto-Close Sub-GUI + Engine (`Views/AutoCloseWindow.xaml`, `Services/AutoCloseService.cs`)
Same two-panel layout for trigger processes, plus master enable, Check On Start
Only, and a live poll interval. The engine scans at startup and then on the
interval, reconfigures the moment settings change, and exits OnTop on the first
match. One-name-per-line .txt import/export; `.exe` suffixes are normalized off.

### Zone Import/Export
Save/Open dialogs on the main window. Imports regenerate IDs, skip duplicates
(name+X+Y), assign fresh palette colors, and land as one undo step.

### Zone Groups
Group dropdown filters the DataGrid through its collection view (the map always
shows every zone), Enable/Disable Group bulk-flips the filtered set, and Set
Group For Selected assigns the selected zone (creating groups implicitly).
Editing the Group cell feeds the dropdown live.

### Startup With Windows (`Helpers/StartupShortcut.cs`)
The existing toggle now works: IShellLink COM interop writes/removes
`OnTop.lnk` (with the exe's icon) in the user's Startup folder — no package
references. State is reconciled at startup and toasts on failure.

### Multi-Monitor Awareness
Startup validation warns (toast + log) about zones whose monitor index exceeds
the connected count or whose centers sit outside the virtual screen. Monitor
indices were already being tracked on create/move since session 1.

### Help Window (`Views/HelpWindow.cs`)
Scrollable, dark-chrome, styled help panel replacing the MessageBox — live
keybinds (shows "Unbound"), the detection formula, sliders, map, overlays,
groups, exceptions, auto-close, and data locations.

### Roadmap Quick-Wins Folded In
- **Hotkey recorder** — HotkeyDialog is now press-to-capture: click a box, press
  the combo. Esc/Clear unbinds (hotkeys are now optional; the VM skips empty
  registrations and the tray shows "Unbound"). Kill always falls back to
  Ctrl+Alt+Q so the escape hatch survives.
- **Bottom status bar** — last action on the left, zone count on the right.
- **Empty state** — "No Zones Yet…" hint over the empty DataGrid.
- **Per-zone color cycling** — click a grid swatch to advance the zone through
  the palette (undoable; the full picker remains on the roadmap).
- **Overlay opacity slider** — in the legend, persisted, applies live.
- **Window fade-in** — soft 150ms fade each time settings open from the tray.
- **Dark ComboBox + ListBox styles** added to the theme.

### Still On The Roadmap (next sessions)
Profiles · per-zone actions · window rules · statistics page · theme variants ·
onboarding tour · update checker · test mode · slider redesign with value
bubble · pulsing overlay dots · DataGrid selected accent bar · Mica/acrylic
Win10 fallback + window scale animations + panel shadows · per-zone color
picker UI · portable/installed toggle · localization scaffold.

---

## Session 2 — Overlay System (TODO #1) + Schema Versioning

### Zone Overlay System (`Views/ZoneOverlayWindow.cs`, `Views/OverlayLegendWindow.cs`, `Services/OverlayService.cs`)

- **One transparent, topmost window spanning the virtual screen** draws, for every
  zone with the Dot column checked: a colored draggable dot, the semi-transparent
  detection box (real `HalfX × HalfY`, to scale, in physical pixels), a name +
  live `W×Hpx` label chip, and four corner resize Thumbs.
- **Drag the dot** to move the zone (clamped to the virtual screen). **Drag a
  corner** to resize — `Zone.ResizeTo()` implements the blueprint waterfall: caps
  shrink first; when the binding axis hits zero cap it eats Tolerance, then
  Variation (both shared between axes; the other axis keeps the remainder).
  Each gesture is one undo step, and resizing a selected zone silently reloads
  the sliders so they show the new values.
- **Click-through lock** (`WS_EX_TRANSPARENT`) — new persisted setting
  `OverlaysLocked`, toggleable from the settings panel and the legend. Locked =
  purely visual, desktop double-click detection works *through* the boxes
  (layered windows hit-test per pixel, so unlocked semi-transparent fills would
  otherwise catch clicks — that's edit mode, by design).
- **Legend window** — floating dark-chrome panel: lock toggle, swatch + name +
  size per active overlay. Close hides it; reopen from the tray ("Show Overlay
  Legend") or by reactivating overlays.
- Overlays restore on startup for zones saved with ShowDot, reposition on
  display-settings changes, never steal focus, and hide from Alt+Tab.
- DPI: physical-px zone coords are converted with the process DPI scale (uniform
  under WPF's default system-DPI awareness), so visuals land exactly on the
  detection area. Mixed-DPI multi-monitor setups may show overlay offset on
  secondary displays — flag it when running that configuration.

### Stable Zone Colors

- `Zone.ColorIndex` (persisted) replaces positional palette assignment — zones now
  keep their color across deletes, reorders, and sessions, and the map, overlays,
  legend, and grid all agree. New **swatch column** in the DataGrid
  (`Helpers/ZoneColorConverter.cs`) plus a row hover highlight.
- This is the stepping stone to the approved per-zone color picker.

### Settings Schema Versioning (the hardening item my roadmap added)

- `AppSettings.SchemaVersion` + `SettingsService.CurrentSchemaVersion` with a
  `Migrate()` hook that upgrades old files in place on load. First real migration
  ships with it: v1 → v2 assigns sequential `ColorIndex` to existing zones so
  they keep the colors they were already shown with.

---

## Session 1 — Hardening + Visual Identity + Zone Map

Follows the "Recommended Next Steps" order from the master prompt: steps 1–3 complete.

## 1. Quick Hardening (App.xaml.cs)

- **Single-instance mutex** — second launch signals the first instance (which shows
  the settings window + toast) and exits. Uses `Local\` named Mutex + AutoReset event.
- **Global exception handlers** — `DispatcherUnhandledException` (logs, toasts, and
  recovers up to 3 times per session so one bad binding doesn't kill the tray app),
  `AppDomain.UnhandledException` (saves a recovery draft, logs, lets it die), and
  `TaskScheduler.UnobservedTaskException`. All also append to `OnTop.crash.log` beside
  the exe as a fallback if the log service never came up.
- **Mouse hook watchdog** — Windows silently removes low-level hooks that stall.
  The hook callback now stamps a liveness pulse on *every* mouse message; a 5s
  DispatcherTimer compares cursor movement against the pulse and re-installs the
  hook (with a toast) if it went dead. `SetLastError` added to `SetWindowsHookEx`
  so failures log a real Win32 error code.
- **Startup flash fix** — the old show-then-hide HWND dance is gone
  (`HotkeyService.Initialize` already calls `EnsureHandle()`). This also fixes a
  latent bug: the show/hide tripped `IsVisibleChanged` → `StopAutoDraft()` →
  `DeleteDraft()`, which destroyed the crash-recovery draft at every startup.
- **Minimize-restore fix** — show paths now reset `WindowState.Normal` so a
  minimized settings window (no taskbar button) can always be recovered from the
  tray or hotkey.

## 2. Visual Identity

- **Custom title bar** (`DarkChromeWindow` style, Themes/DarkTheme.xaml) — WindowChrome
  with an integrated dark caption: pin glyph, title, Minimize/Close caption buttons
  (Segoe MDL2 Assets). `Helpers/ChromeWindowBehavior.cs` wires the SystemCommands
  (styles can't add CommandBindings) and applies the DWM dark frame automatically on
  `SourceInitialized`. Close routes through `Window.Close()`, so the main window keeps
  its hide-to-tray behavior. Maximized state pads 7px for the borderless overhang;
  `NoResize` dialogs auto-hide the minimize button. Applied to MainWindow and
  HotkeyDialog.
- **Animated toggle switches** (`ToggleSwitch` CheckBox style) — 150ms eased knob
  slide + track color animation to accent blue. Applied to the six settings toggles;
  DataGrid checkbox columns intentionally keep plain checkboxes.
- **Toast notifications** (`Services/ToastService.cs`) — bottom-right, non-blocking,
  click-to-dismiss, never steals focus (`WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`).
  Info/Success/Warning/Error variants with accent bar + MDL2 icon. Wired into: save
  confirmation, draft recovery warning, hotkey bind failures, watchdog re-installs,
  unexpected-error notices, and second-instance launches. Confirmation dialogs that
  need an answer (Confirm Before Exit) remain MessageBoxes by design.

## 3. Flagship: Zone Editor Canvas (`Controls/ZoneMapCanvas.cs`)

A screen-scale minimap in the main window, between Settings and the Zones grid:

- Draws every monitor (with index + resolution) and every zone — center dot plus the
  real detection box (`HalfX × HalfY`) to scale. Disabled zones render dimmed.
- **Drag a zone** to reposition it — live `(X, Y)` readout while dragging, clamped to
  the virtual screen. One undo step per drag (`ZoneDragStarted` pushes the snapshot
  before the first change).
- **Click** selects (two-way bound to `SelectedZone`, so the DataGrid and sliders
  follow); **double-click empty space** creates a zone at that screen location.
- All zone coordinates stay in physical pixels (`System.Windows.Forms.Screen` bounds,
  matching `GetCursorPos` and the hook) — the canvas only scales for display, so
  there is no DPI coordinate drift.
- `Helpers/ZonePalette.cs` — the 12-color palette, shared by index so the upcoming
  overlay system shows each zone in the same color as the map.
- New VM methods: `AddZoneAt(x, y)` (Record now reuses it), `BeginZoneDrag`,
  `EndZoneDrag`. Zones now track their `Monitor` index on create/move, feeding the
  multi-monitor warnings planned in TODO item 7.

## Small Extras

- `Ctrl+Z` / `Ctrl+Y` InputBindings and a working `Ctrl+Enter` (Save and Close) —
  the buttons already advertised these.
- Status text (zone recorded/moved/saved) now actually displays, under Undo/Redo.
- `Win32.cs`: `WS_EX_TRANSPARENT` / `WS_EX_TOOLWINDOW` / `WS_EX_NOACTIVATE` constants
  (toasts now, overlay click-through lock later).
- Main window is 60px taller (880) to fit the map row.

## Build Notes

- No new packages; WindowChrome/SystemCommands ship in PresentationFramework.
- Authored and reviewed on a non-Windows machine, so it has **not** been compiled —
  run `dotnet build` first and shout if anything trips.
- If a thin light frame edge ever shows on Win10, change `GlassFrameThickness="-1"`
  to `"0"` in the `DarkChromeWindow` style (trades the DWM drop shadow for it).
