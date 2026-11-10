# OnTop — Consolidated Test Pass: v0.37.0 → v0.43.0 (Stages D–J)

> **Status 2026-09-20:** the owner has compiled 0.42.1 clean but has NOT functionally tested anything since the v0.36.1 feedback round. Every item below is outstanding.

> Standing document (S28). Update it whenever a stage adds testable behavior; the owner works it top-down after a build and reports by item number.

Work top-down. Tick what passes; for anything else write **what you did → what happened → what you expected** and paste log lines (View Logs, filter the category shown). Build result comes first — nothing below matters until it compiles.

Legend: **[!]** = a failure here would affect many other items — report it first.

---

## 0. Build & migration (v0.43.0)

- [ ] **[!] Compiles clean** on Windows (NuGet restore succeeded — `System.Security.Cryptography.ProtectedData` is the only package). Paste every error verbatim.
- [ ] First launch: settings migrate 21 → 28 silently; no "config conflict" prompt; no "powerful fields changed" toast on the **second** launch.
- [ ] Existing zones, profiles, hotkeys, macros all present. Zones fire on classic double-click (gesture 0 default).
- [ ] Hotkeys… shows **Ctrl+Alt+M** on "Record / Stop Macro" (new default binding).
- [ ] `settings.json` (installed mode): macros now stored as `EncryptedEvents` base64, no plain `Events`; macros still play after restart. Portable mode: plain `Events` (encryption defaults off there).
- [ ] Help opens; CONTENTS pill box under the subtitle; clicking a pill scrolls to that section; amber ⚠ paragraphs present under Macros / Zone Actions / Auto-Pin / Stealth / Updates.

## 1. Stage D — v0.37.0 (feedback round)

- [ ] **[!] Accent**: Settings → Appearance → pick an accent → whole app re-tints **immediately** (no restart).
- [ ] Macros on → **status strip** appears bottom-right; drag it; position persists after restart.
- [ ] **Ctrl+Alt+M** anywhere → strip turns red marquee with elapsed time + step count; press again → green SAVED for ~3 s → READY. The stop press is NOT in the recording (check step count / editor).
- [ ] Main window has its own **MACROS** band (Macros…, ● Record Macro, ■ Stop). ■ Stop halts a playing macro. Macros window has ■ Panic Stop (Ctrl+Alt+X) in its header, enabled only while playing.
- [ ] Macros window opens large enough (nothing clipped), **remembers size/position**, stays on top with GUIs-on-top.
- [ ] **Step editor**: double-click a macro (or Steps…/right-click) → grid of steps; search "key"; edit a Delay → later times shift; Delete Selected; Remove Mouse Moves (folds delays forward); Save → row shows fewer steps; Cancel changes nothing.
- [ ] **Known Anti-Cheat…** lists services with "(running now)" markers; untick PnkBstrA (or whatever runs) → playback no longer refused. Log: "Known anti-cheat guard exclusions: …".
- [ ] **Kill switch**: start a looping macro, then launch a watched process → master toggle visibly turns OFF; toast says tick "Macros Enabled" to re-arm; ticking it re-arms without cycling. A refusal at start (watched process already running) does NOT trip the switch — just a toast naming the process.
- [ ] **Overlays above OnTop GUIs**: open Macros window over a zone → Peek / map ping → overlays draw above it.
- [ ] Settings → Performance → **Low Impact Mode** → Task Manager shows Below Normal; log line "Low Impact Mode ON…"; off → Normal.
- [ ] Wording: Speed shows/enters as **%**; "Wobble" in px; "Block Macros In Profile X"; repeat "once / 3× / until stopped".
- [ ] Welcome tour (Settings → Maintenance → re-show): lists current hotkeys incl. Ctrl+Alt+M; "Get Started" and "Add 4 Starter Zones" open the main window.

## 2. Stage E — v0.38.0 (Window Placement & Launcher)

- [ ] Settings → Features → **Window Placement** on. APP → Placement… opens (remembers size).
- [ ] **Capture From Focused Window** (3 s) → new rule with process, monitor, X/Y/W/H. Move the window → Apply Selected → it returns. Tick Max → maximizes on that monitor.
- [ ] **On Start**: tick, restart OnTop → rule applies ~4 s after launch.
- [ ] Apply All hotkey (bind it) → applies every enabled rule; hotkey is inert while the feature is off.
- [ ] Zone → Set Action → **Apply Placement** → triggering the zone places the window.
- [ ] **Launch** (H removed the global gate): rule with a Launch Path and Launch **unticked** → no matching window → toast "No Matching Window", nothing launches. Tick Launch → launches, window placed within 10 s. Log has "LAUNCH (placement '…')". Test Mode suppresses the launch with a toast.
- [ ] **Zone.Clone fix**: Duplicate a zone that has Run Command → duplicate keeps the command. Save profile → switch away → back → command intact.
- [ ] Import a zones file / restore from an outside path containing a launch path → Import Review lists "Placement Launch" verbatim, arrives blanked.

## 3. Stage F — v0.39.0 (Action Library)

- [ ] Features → **Action Library** on. APP → Actions… → New Action → kind Run Command → Payload (e.g. `notepad`) → Run → Notepad opens, log "Library action 'X' (library window): Run: notepad".
- [ ] Hotkey… assign → press it → runs. Assign a combo already used by a macro or app hotkey → **refused**, message names both.
- [ ] Kinds: Kill Process (protected names refused), Toggle Window, Play Macro (picker), Apply Placement (picker), Switch Profile (picker), Pause/Resume, Peek, Toggle Stealth, Pin Under Cursor — each does its thing from Run.
- [ ] Zone → Set Action → **Library Action** → triggers it. Delete the action → that zone falls back to Exit.
- [ ] Turn the feature off → library hotkeys dead, zone toasts "Action Library Is Off".
- [ ] Hotkeys… dialog: assigning an app hotkey that collides with a library hotkey is refused.

## 4. Stage G — v0.40.0 (sixteen additions)

- [ ] **[!] Gestures**: set a zone to Middle Click → fires only on middle; Triple Left → fires on the third click, not the second; Ctrl+double → only with Ctrl; Double Right → right button. A classic zone still ignores those. Log category "Detection".
- [ ] **Pinned Windows** → −/+ changes the window's opacity in 10% steps (floor 20%); **Click-Through** → clicks pass through; button/hotkey again or **Unpin** reverts it (window is clickable again).
- [ ] Hotkeys: Pin Under Cursor (hover a window that has no focus), Opacity +/−, Click-Through Under Cursor, Cycle Corner (TL→TR→BR→BL, size kept).
- [ ] **Profile Rules…** (Features → Profile Rules on): rule "notepad → profile X" → open Notepad → profile switches (toast shows the diff); close → reverts if "Revert When Gone".
- [ ] **Display Profiles**: Features on → Settings → Displays → Bind This Layout → change monitor config (unplug/duplicate) → profile switches on the bound layout.
- [ ] **Do Not Disturb** on + a watched process running → routine toasts silent, Error toasts still show; Toast History shows them as [DND].
- [ ] Macro **Countdown** 3 → strip shows "STARTING IN 3…2…1"; **Then Play…** → second macro follows after a natural finish (not after Ctrl+Alt+X); **Relative** on → move the anchor window → clicks follow it; Relative on a macro with no anchor → warning toast.
- [ ] Editor → **Insert Wait For Window** ("Calculator", 10 s) → playback pauses on that step until Calculator opens; times out → macro stops with a toast.
- [ ] **Cheat Sheet** hotkey → card of every binding; Esc/click/hotkey hides. **Command Palette** hotkey → type "peek" → Enter → peeks; only enabled features are listed.
- [ ] Statistics → bars under top zones; **Export CSV…** → opens in Excel.

## 5. Stage H — v0.41.0 (scope recalibration + hold chords)

- [ ] Features → **Hold Chords** on; Hotkeys… set Hold To Peek (e.g. Ctrl+Alt+Space) → overlays show while held, hide on release; releasing **Ctrl alone** also ends it. Log: "Hold-chord keyboard hook INSTALLED".
- [ ] Hold To Pin → focused window on top while held, released after; a window that was **already pinned** is untouched by release.
- [ ] Enter **Stealth** → log "Hold-chord keyboard hook REMOVED (disarmed)"; chords dead; leave Stealth → back.
- [ ] Turn Hold Chords off → hook removed (log). Exit OnTop → log shows removal.
- [ ] Looping macro with **Stop Key** F8 → F8 stops it.
- [ ] Placement window: no "Allow Launching" switch; per-rule Launch alone gates launching (see 2).

## 6. Stage I — v0.42.0 (the twenty)

- [ ] Pin two windows → Pinned Windows → **Save Scene…** → Unpin All → **Apply Scene…** → both back, same geometry/opacity/click-through. Palette lists "Apply Pin Scene: …"; Library kind "Apply Pin Scene" works.
- [ ] Features → **Pin Memory** on → set opacity 60% on an app → unpin → pin again → 60% + position restored.
- [ ] **Timed Pin** hotkey → pinned; toast "Timed Pin Released" after N s (Settings default 30).
- [ ] **Send To Next Monitor** → lands on the other monitor at the same relative spot/size.
- [ ] Tray menu → "Last Pinned Opacity (app)" submenu → 60% applies.
- [ ] Zone **Cooldown…** 5 → hit it twice within 5 s → second ignored (log "in cooldown"); **Schedule…** outside the current time → ignored (log "outside its schedule"); **Zone Hit Flash** on → overlay pulses on a real hit.
- [ ] Shift+double and Alt+double gestures fire their zones only with that modifier.
- [ ] Macros → **Dry Run** on → play → strip/status narrate steps, nothing happens; log "MACRO DRY RUN…"; off → normal. **Pause/Resume** hotkey mid-run → freezes (⏸ on the strip), resumes with timing intact.
- [ ] Editor **Note** column editable + searchable; **Hide Mouse Moves** collapses the path (view only).
- [ ] Profile switch toast shows the **diff** (+N/−M zones, macro policy change).
- [ ] Features → **Idle Rule** → Idle Rule… 1 min, action 1 + profile → leave the PC 1 min → switches; touch the mouse → reverts. Actions 2 (unpin all) and 3 (stealth) fire once.
- [ ] Features → **Profile Schedules** → Schedules… add a window covering now → switches; edit it to exclude now → reverts. A process rule beats a schedule.
- [ ] Appearance → **Theme**: High Contrast → immediate; Light → note every element that looks wrong (list them — this is the best-effort one); Dark → back to designed.
- [ ] Settings → Data → **Config History…** lists .bak1–3 + timestamped backups with dates/sizes; Restore → no Import Review (own path); **Toast History…** lists this session's toasts.
- [ ] Welcome tour has the "Everything Optional Is Off Until You Turn It On" step.
- [ ] **Minimal Mode** turns off every switch added since D (incl. Placement, Library, Profile Rules, Display Profiles, Hold Chords, Idle, Schedules, Pin Memory).

## 6b. Stage J — v0.43.0 (layout round)

- [ ] Main window: action row is three cards — Zone Actions | Zone Groups + Macros | App. Nothing clipped at the default 1060×940; at a smaller height the Zones row keeps ≥300 px and the window scrolls/fits rather than crushing the map.
- [ ] Every button that used to be in Zone Actions still exists somewhere (Macros…, ● Record, ■ Stop in the Macros card; Logs/Pinned/Auto-Pin/Actions/Placement/Profiles/Stats/Help/Peek/Legend/Paused/Test + profile picker in App).
- [ ] Help: CONTENTS is a row of bordered chips; hover inverts; click jumps. Only one "Zone Map" section, mentioning middle-mouse scroll.
- [ ] Empty map hint + map tooltip + welcome tour mention holding the middle mouse button to scroll; holding MMB actually pans.
- [ ] Settings → Performance → Hotkey Re-Bind Retry: set 5 → with a hotkey held by another app, log shows retries every ~5 s; set 120 → every ~2 min; invalid input reverts.
- [ ] Zone list / map splitter and Map/List/Split views unchanged.

## 7. Regression spot-checks (things the new stages touched)

- [ ] Classic zone Exit + Stealth actions still work; Test Mode still suppresses actions with hit toast + flash.
- [ ] Ctrl+Alt+P pause; Ctrl+Alt+X panic; stealth toggle hotkey; auto-stealth watcher (5 s / 15 s Low Impact).
- [ ] Auto-Pin rules still scan (3 s / 10 s Low Impact).
- [ ] Profiles: create / switch / delete; per-profile Block Macros still binds/unbinds macro hotkeys.
- [ ] Import zones with Run Command → Import Review → unticked stays Exit; ticked restored. Restore from Backups folder → no review.
- [ ] Hook latency line in the log stays sane with the extra button forwarding (no "hook dropped by Windows" warnings during normal use).
- [ ] Crash report prompt / draft recovery unchanged (kill the process, relaunch).

## 8. Environment-gated (only when you get to them — tell me explicitly)

- [ ] Installer (`installer/OnTop.iss`): wizard, shortcut/location choices, .NET bootstrap, uninstaller.
- [ ] Portable publish (`portable/publish-portable.cmd`): single exe; `Data` beside exe; encryption defaults off; `--portable`.
- [ ] Updater: stays dormant with an empty manifest URL; with a test https manifest, check → notify → opens browser only.

---

Report format that helps most: the item number, ✅/❌, one line of what happened, and the log lines (category in brackets). Cosmetic issues (clipping, wording, Light theme) — batch them at the end.
