using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OnTop.Models;

namespace OnTop.Views;

/// <summary>
/// Scrollable, styled, read-only help panel (TODO #8) — documents the current
/// keybinds, the detection formula, sliders, the zone map, overlays, groups,
/// exceptions, auto-close, and where data lives on disk.
/// </summary>
public class HelpWindow : Window
{
    private static readonly Brush Body = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xD6));
    private static readonly Brush Head = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6));
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0x3A, 0x8B, 0xFF));

    private readonly AppSettings _cfg;

    public HelpWindow(AppSettings cfg)
    {
        _cfg = cfg;
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "OnTop — Help";
        Width = 660; Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = true;    // sub-GUIs surface in taskbar hover (S22)
        SourceInitialized += (_, _) => Helpers.WindowPlacement.Restore(this, "help", _cfg);
        Closing += (_, _) => Helpers.WindowPlacement.Save(this, "help", _cfg);

        var sp = new StackPanel { Margin = new Thickness(22, 16, 22, 22) };

        sp.Children.Add(new TextBlock
        {
            Text = Helpers.AppInfo.TitleWithVersion,
            FontSize = 20, FontWeight = FontWeights.Bold,
            Foreground = Head, Margin = new Thickness(0, 0, 0, 2)
        });
        sp.Children.Add(new TextBlock
        {
            Text = "Always-on-top pinning + zone exit / stealth for Windows.",
            FontSize = 12, Foreground = Body, Margin = new Thickness(0, 0, 0, 6)
        });

        H(sp, "Hotkeys");
        P(sp, $"Always On Top (focused window):  {Hk(cfg.HotkeyToggle)}");
        P(sp, $"Exit OnTop Cleanly:  {Hk(cfg.HotkeyExit)}");
        P(sp, $"Toggle Settings Window:  {Hk(cfg.HotkeyConfig)}");
        P(sp, $"Kill Immediately (No Cleanup):  {Hk(cfg.HotkeyKill)}  —  the EMERGENCY STOP. Exit and Kill are watched by a separate thread inside OnTop, so they work even if the rest of the app is frozen: Kill ends OnTop instantly; Exit tries a clean shutdown first and force-stops after 3 seconds if the app does not respond. Kill stays armed everywhere — including inside the hotkey recorder. A frozen tray icon may linger until you hover it.");
        P(sp, "Pause/Resume Zone Detection:  Ctrl+Alt+P (Fixed)");
        P(sp, "Leave Stealth / Re-Hook Mouse:  Ctrl+Alt+R (Fixed)");
        P(sp, $"Macros Window:  {Hk(cfg.HotkeyMacros)} (Optional)");
        P(sp, $"Record / Stop Macro:  {Hk(cfg.HotkeyMacroRecord)} (Only While Macros Are Enabled)");
        P(sp, $"Apply All Placements:  {Hk(cfg.HotkeyPlacement)} (Only While Window Placement Is Enabled)");
        P(sp, $"Pin Under Cursor: {Hk(cfg.HotkeyPinUnderCursor)}  ·  Opacity +/−: {Hk(cfg.HotkeyPinOpacityUp)} / {Hk(cfg.HotkeyPinOpacityDown)}  ·  Click-Through: {Hk(cfg.HotkeyPinClickThrough)}  ·  Cycle Corner: {Hk(cfg.HotkeyPinCycleCorner)}");
        P(sp, $"Cheat Sheet: {Hk(cfg.HotkeyCheatSheet)}  ·  Command Palette: {Hk(cfg.HotkeyPalette)}");
        P(sp, $"Timed Pin: {Hk(cfg.HotkeyPinTimed)}  ·  Send To Next Monitor: {Hk(cfg.HotkeySendToMonitor)}  ·  Pause/Resume Macro: {Hk(cfg.HotkeyMacroPause)}");
        P(sp, "Quick Record / View Logs / Soft Restart:  Optional, Unbound By Default — Set Them In Hotkeys…. Quick Record Captures A Zone In The Current Mode Without Opening The Window (Auto-Named); Soft Restart Saves And Relaunches OnTop.");
        P(sp, "In The Settings Window:  Ctrl+Z Undo, Ctrl+Y Redo, Ctrl+Enter Save and Close.");
        P(sp, "In The Zone List:  Delete Removes The Selected Zone (Undoable), F2 Renames It.");
        P(sp, "All Hotkeys Except Pause And Re-Hook Are Recordable In Hotkeys… — Press Esc Or Clear To Unbind One. While any hotkey recorder is open (Hotkeys…, a macro's Hotkey…, a library action's Hotkey…) EVERY OnTop hotkey is paused so the keys you press are captured, not executed; a combination another feature already uses is refused the moment you press it.");
        P(sp, "The Hotkeys… Dialog Also Has An Optional, Default-Unbound Hotkey For Every Main-GUI Toggle (Desktop Only, Debug Overlay, Apply To All, Override Exceptions, Confirm Before Exit, Lock Overlays, Keep GUIs On Top, Stealth, Zones Trigger Stealth). Pressing One Flips That Setting And Toasts The New State.");

        H(sp, "Zone Detection");
        P(sp, "Double left click inside an enabled zone on the desktop to exit OnTop. Each zone's detection box is computed from three values that all combine:");
        Mono(sp, "half = (Zone Size ÷ 2) + Tolerance + Variation");
        P(sp, "A click matches when |clickX − zoneX| ≤ halfX and |clickY − zoneY| ≤ halfY. Lower ALL three sliders to make a zone smaller. Desktop Only restricts matching to clicks on the desktop itself.");

        H(sp, "Recording Modes");
        P(sp, "The dropdown next to Record picks how a zone is captured — and you can switch live during capture with 1 (Point), 2 (Box), or 3 (Draw), or Tab to cycle:");
        P(sp, "Point Click — the classic flow: the window hides and your next click places a zone, sized by the sliders.");
        P(sp, "Drag Box — drag a rectangle and the zone is sized 1:1 to it, with Tolerance and Variation set to 0 so nothing pads it. Great for matching an exact on-screen region.");
        P(sp, "Freeform Draw — hold the mouse button and sketch any shape; release to close it. Detection is the real polygon (a point-in-shape test), not a box, so the sliders don't apply. Shapes that cross themselves are rejected — just draw again. Right-click a Draw zone on the map and choose Redraw Shape… to re-sketch it in place. The Type column in the list shows each zone's mode.");

        H(sp, "Sliders");
        P(sp, "Tolerance (0–300) and Variation (0–255) pad the box equally on both axes; Zone Size (0–400) is the core width/height. With Apply To All Zones on, slider moves push to every zone; otherwise only the selected zone. Selecting a zone loads its values back into the sliders.");

        H(sp, "Zone Map");
        P(sp, "The map shows every monitor and zone to scale. Drag a zone to move it, click to select it, double-click empty space to create a zone at that spot. HOLD THE MIDDLE MOUSE BUTTON and drag to scroll (pan) the map; the wheel zooms at the cursor; right-click a monitor to reset the view. Colors match the grid swatch, the overlays, and the legend — click a swatch in the grid to cycle a zone's color.");
        P(sp, "Right-click a zone for its menu; right-click a monitor for hide / show-hidden / reset-view. Map / List / Split at the top switch views, and the pop-out buttons open the map or list as their own resizable windows — selection stays synced across all of them.");

        H(sp, "Overlays");
        P(sp, "Turn on a zone's Overlay column to show it on the desktop: a draggable dot, the true detection box, and corner handles. Dragging a corner resizes via the waterfall — Zone Size shrinks first, then Tolerance, then Variation. Lock Overlays (Click-Through) makes them purely visual so desktop double-clicks pass through; the floating legend lists active overlays and holds the lock and opacity controls.");

        H(sp, "Settings Toggles");
        P(sp, "Desktop Only — zone clicks only register on the desktop itself, not over app windows (OnTop's own zone overlays are looked THROUGH, so they never block a desktop click; another program under the overlay still does). In Test Mode a gated click says which window was in the way. Debug Overlay — draws live detection boxes for troubleshooting. Apply To All Zones — slider changes hit every zone at once. Override Exceptions — ignore the exceptions list entirely. Start With Windows — auto-launch at sign-in. Confirm Before Exit — ask before an Exit-action zone closes OnTop (Stealth zones skip it). Lock Overlays (Click-Through) — makes desktop overlays purely visual so clicks pass through to the desktop. Keep GUIs On Top — pins OnTop's own windows above others. Stealth Mode / Zones Trigger Stealth — see the Stealth Mode section.");

        H(sp, "Sliders");
        P(sp, "Tolerance (0–300) and Variation (0–255) both pad a zone's hit area on every side; Zone Size (0–400) is the core width and height. The full formula is half = (Zone Size ÷ 2) + Tolerance + Variation, applied per axis. Selecting a zone loads its values into the sliders; with Apply To All Zones on, moving a slider changes every zone. The ADJUSTMENT lock above the sliders freezes them so you can't nudge a zone by accident.");


        H(sp, "Overlays and Legend");
        P(sp, "Turn on a zone's Overlay column to paint it on the desktop: a draggable dot, the true detection box, and corner resize handles (which shrink Zone Size, then Tolerance, then Variation). Lock Overlays makes them click-through. The floating legend lists active overlays and holds the opacity slider and lock toggle.");

        H(sp, "Stealth Mode");
        P(sp, "Stealth Mode removes the global mouse hook entirely — nothing stays installed for anti-cheat software to flag — at the cost of zone detection while it's on. Toggle it manually from the settings panel or the tray, or let a zone trigger it: each zone's Action chip picks Exit (close OnTop) or Stealth (unhook and stay in the tray), and the Zones Trigger Stealth toggle overrides every zone to Stealth. Hotkeys keep working in stealth — they don't use the hook — so return via the tray, the settings panel, or Ctrl+Alt+R. Pause (Ctrl+Alt+P) is different: it keeps the hook installed and just ignores clicks.");

        H(sp, "Groups");
        P(sp, "Each zone has a Group (editable in the grid, or via Set Group for the selected zone). The Group dropdown filters the grid, and Enable/Disable Group flips every zone in the filtered group. The map always shows all zones.");

        H(sp, "Exceptions");
        P(sp, "Processes in the Exceptions list ignore the Always On Top and/or Exit hotkeys while focused — each blockable independently. Override Exceptions in settings bypasses the whole list. Import/export uses pipe-delimited text: name|blockToggle|blockExit.");

        H(sp, "Auto-Close");
        P(sp, "When enabled, OnTop exits itself if any trigger process is running — checked at startup and then on the poll interval (or only at startup with Check On Start Only). Import/export uses one process name per line.");

        H(sp, "Macros");
        W(sp, "Macros record every keystroke literally — including anything you type into a password box — and store it in your config. Playback sends synthetic input that anti-cheat systems can detect and may ban accounts for. Use them for single-player, accessibility, and productivity; keep them BLOCKED on the profiles you use for online games.");
        P(sp, $"RECORDING IS HOTKEY-DRIVEN: press {Hk(cfg.HotkeyMacroRecord)} anywhere to start recording (auto-named), press it again to stop and save — no clicking in a window that would end up inside the recording. A small always-on-top status strip (bottom-right, draggable) appears while the Macros window is open and whenever a macro records, counts down, or plays, then hides a few seconds later; its × (or the optional 'Macro Status Strip' hotkey) hides it until the next recording. The start/stop chord (Ctrl+Alt+M) is stripped from every recording. Double-click any macro to open the STEP EDITOR: every click, key, and delay in a searchable list — delete steps, change delays, or strip mouse movement.");
        P(sp, "The Macros window (APP > Macros..., the tray, or its optional hotkey) records EVERYTHING you do — mouse movement, clicks, wheel, keys, with real timing — and plays it back, adapted to your current monitor layout. It ships behind a master toggle (off by default): while off, nothing binds, records, or plays. Each macro gets an optional hotkey (press = play, press again = stop; duplicates across all features are refused), a speed multiplier, a repeat count (0 = loop until stopped), and a Jitter (±N px humanizing wobble on mouse events). Ctrl+Alt+X is the always-available panic stop during playback. Zones can trigger macros via Set Action > Run Macro.");
        W(sp, "Honest limits of the guard: Stealth removes the hook and Kill removes the process — both stop the evidence from that moment on. Neither erases what an anti-cheat may already have recorded (kernel anti-cheat can log hook installs and injected input across the whole session and ship it to the vendor; bans are decided later from that history), and neither makes synthesized input retroactively un-synthesized. The watcher polls every few seconds, so a game that scans during its own startup can see OnTop before OnTop sees the game. The guard reduces risk; it cannot eliminate it. Pinning-only OnTop (Minimal Mode) is as benign as any 'always on top' utility.");
        P(sp, "THREE-LAYER ANTI-CHEAT GUARD: (1) the whole module is optional and dark by default; (2) playback REFUSES to start and ABORTS mid-run while any Stealth-watched process OR (with Guard: Known Anti-Cheat on) any known anti-cheat service — EasyAntiCheat, BattlEye, Vanguard, FACEIT, PunkBuster, GameGuard, XIGNCODE and more — is running; (3) the KILL SWITCH then turns the master toggle OFF and unbinds every macro hotkey until you re-enable it by hand. Each layer has its own visible switch. Per-profile policy: tick 'Deny In Profile' on a game profile and nothing binds, plays, or records while it's active. The tray icon shows a red dot while recording and a blue dot while playing; the status bar says so too.");
        P(sp, "Repeat-until-key: give a looping macro a Stop Key (F8, Escape, Space…) and pressing it ends the loop — polled, no hook. Ctrl+Alt+X always works too.");
        P(sp, "Playback extras: a 3-2-1 COUNTDOWN on the status strip (Countdown button, 0–5 s) so your hands are off the keyboard; THEN PLAY chains another macro when this one finishes on its own (up to 5, never itself); RELATIVE replay follows the window that was in front when you started recording — if it has moved, every mouse position shifts with it; and the step editor can INSERT WAIT FOR WINDOW — a step that pauses until a window with a given title appears (or gives up after a timeout). Waits are the only step you can add: they do nothing but wait.");
        P(sp, "Capture: 'Record Mouse Path' off = actions-only (clicks, wheel, keys — clicks still land where recorded), much smaller. Recordings capture LITERAL keystrokes, passwords included, and live in your local config; the first Record shows a blunt warning once. Export writes all macros to a JSON file (warned: plain-text keystrokes); Import brings macros in DISABLED with no hotkey until you tick each one in the Import Review. Recording hooks exist ONLY while recording.");

        H(sp, "Zone Actions");
        W(sp, "Run Command starts whatever is in the box exactly as if you typed it into Win+R, and Kill Process ends every process with that name. Only YOU put those values there — but check them after importing anyone else's zones (imports arrive with these actions switched off for exactly this reason).");
        P(sp, "AFTER ACTION (right-click a zone): for Run Command / Kill / Toggle Window / Macro / Placement / Library zones, choose what happens once the action fires — Nothing (default), then Stealth, or then Exit. The global 'Zones Trigger Stealth' switch only turns EXIT zones into Stealth; it never swallows an advanced action.");
        P(sp, "Right-click a zone > Set Action: Exit (classic), Stealth (unhook), Run Command (anything you'd type into Win+R), Kill Process (by name — system-critical processes are refused), or Toggle Window (minimize/restore an app's window). The current command or process is shown right in the menu, and every run is logged and toasted — nothing happens silently. The grid's action chip still quick-flips Exit/Stealth; clicking it on an advanced action returns the zone to Exit.");

        H(sp, "Stealth");
        W(sp, "The stealth auto-watcher checks running process names every few seconds while a watched list is set. It reads names only, never memory — but it is a background scanner, and it can be switched off in Settings → Features.");
        P(sp, "Stealth unhooks the mouse so nothing is left for anti-cheats to see — from a zone action, the tray, the GUI, or the new Toggle-Stealth hotkey. Settings > Stealth can list processes that engage stealth AUTOMATICALLY while they run (released when they exit; a manual stealth is never auto-released), and hide the zone overlays while stealthed (on by default — they're visible windows).");

        H(sp, "Imports, Restores & Config Safety");
        P(sp, "Files you IMPORT (zones, macros) or RESTORE from anywhere outside OnTop's own config folder are treated as untrusted: every Run Command / Kill Process / Toggle Window / Run Macro action, updater URL, and macro hotkey arrives INERT, and an Import Review window lists each one verbatim — tick only what you recognize to re-enable it. OnTop's own settings and backups are trusted, so crash recovery and restores from the Backups folder are seamless and never re-prompted. A pre-import backup is always written first. On every start OnTop also re-checks powerful fields (zone commands, macro bindings, updater URL, watched list) and warns once if they changed outside the app. Nothing in a config ever executes just by being loaded — actions fire only on your gesture.");

        H(sp, "Performance & Low Impact Mode");
        P(sp, "OnTop is idle almost all the time: the mouse hook only wakes for clicks, and the only recurring work is a few background timers (auto-pin scan, stealth watcher, a stats flush every minute). Settings → Performance → Low Impact Mode lowers the process priority to Below Normal and slows those timers (auto-pin 3 s → 10 s, watcher 5 s → 15 s) so OnTop always yields to a game. The mouse hook is deliberately NOT slowed — Windows silently removes hooks that respond late.");
        P(sp, "Why there is no 'avoid the game's cores' option: pinning OnTop to particular CPU cores would not help. Games don't pick a fixed core, Windows moves threads around constantly, and a pinned process can only run on its pinned cores even when they are the busy ones. For a process that spends 99.9% of its time asleep, Below Normal priority is the tool that actually works — the scheduler gives every core to the game first.");

        H(sp, "Features & Minimal Mode");
        P(sp, "Settings > Features lists every optional feature's off-switch in one place — Macros, Auto-Pin Rules, Auto-Close, Update Check, Statistics, Zone Actions, Stealth Auto-Watcher, Display Auto-Disable. Only always-on-top pinning and zone detection are always on; everything else is genuinely inert when off (no hooks, no timers, no hotkeys, no network). 'Minimal Mode' flips them all off in one click (confirmed, reversible per switch) — handy for anti-cheat sensitive games.");
        P(sp, "Privacy: the Macros window's 'Encrypt At Rest (DPAPI)' stores recordings encrypted with Windows DPAPI, tied to your Windows login — no password, no stored key; a copied or synced config exposes nothing. Zones, profiles, hotkeys and settings are never encrypted, and Export always writes plain JSON, so sharing is never blocked. A recording sealed under another Windows account loads empty and disabled with a warning.");

        H(sp, "Maintenance");
        P(sp, "Settings > Maintenance: re-show the welcome tour on the next launch (checkbox), and Restart OnTop — saves everything, closes, and relaunches (same as the restart hotkey).");

        H(sp, "Appearance & Accent");
        P(sp, "Settings > Appearance: pick an Accent Color (applies after restart), toggle Mica (on Windows 10 you get an acrylic blur instead), and every control now shows a visible focus ring when navigating by keyboard.");

        H(sp, "Portable Mode");
        P(sp, "Place an empty file named OnTop.portable next to OnTop.exe (or launch with --portable) and ALL data lives in a Data folder beside the exe — true run-from-USB. Without the marker, data lives in %APPDATA%\\OnTop as usual.");

        H(sp, "Tray Menu");
        P(sp, "Settings > Appearance > \"Tray Menu Stays Open After Clicking\" keeps the tray menu up after each selection so you can toggle several things in one visit — it closes on its own shortly after your cursor leaves it (Esc or clicking elsewhere closes it instantly).");

        H(sp, "Overlay & Displays");
        P(sp, "Overlays are drawn with one window per monitor, so mixed display-scaling setups render exactly to scale. Dragging a zone on any map shows its overlay live while you drag, lingering about three seconds after release. Zone colors: right-click a zone > Color (or click its swatch to cycle).");

        H(sp, "Test Mode");
        P(sp, "The Test switch (APP section) checks zone placement safely: zone gestures report HIT (toast + the zone flashes on the map) or MISS (with coordinates) but never execute their action, and test clicks never touch your statistics. Session-only — it always starts off.");

        H(sp, "Updates (Check & Notify)");
        W(sp, "This is the only feature that ever touches the network, and only when you have entered an https manifest URL. It downloads a tiny text manifest, compares versions, and at most opens your browser — it never downloads or runs a program. Off by default because the URL is empty.");
        P(sp, "Settings > Updates can check a release manifest you host (HTTPS) and tell you when a newer version exists — Open Download Page (your browser), Ignore This Version (quiet until the next release), or Cancel (asks again next startup). The app itself never downloads or runs anything, the feature is fully dormant until you enter a URL, and failures never nag. See installer\\UPDATES.md for the manifest format.");

        H(sp, "Crash Reports");
        P(sp, "If OnTop hits an unexpected error, it offers (once per session) to save a diagnostics bundle with your description of what happened — written to Config\\Crashes, never transmitted anywhere. Fatal crashes auto-save a bundle on the way down.");

        H(sp, "Multi-Select & Bulk Actions");
        P(sp, "Both zone lists support multi-select (Ctrl/Shift-click). On the map, dragging a zone snaps to monitor edges and corners — hold Shift to bypass. Delete removes every selected zone as one undoable action, and right-clicking inside the selection offers Remove / Set Group / Enable / Disable for the whole selection at once.");

        H(sp, "Displays & System Events");
        P(sp, "OnTop follows your hardware: when monitors are added or removed the map and zone health refresh automatically, and Settings > Displays can auto-disable zones left with no monitor under them (a toast lists what was disabled). After sleep/resume or locking/unlocking Windows, the mouse hook, hotkeys, and every pinned window's always-on-top are re-asserted automatically.");

        H(sp, "Diagnostics Bundle");
        P(sp, "Logs > Export Diagnostics... saves a single zip (settings, stats, the current log, and system info) — everything a bug report needs. Nothing is ever sent anywhere; you choose the file and where it goes.");

        H(sp, "Statistics");
        P(sp, "Statistics (APP section) shows this session's and lifetime usage: launches, total runtime, zone triggers (with your most-triggered zones), pins/unpins, auto-pins, peeks, and profile switches. Counters live in stats.json beside your settings, never leave the machine, and Reset wipes only the counters — settings, zones, and profiles are untouched.");

        H(sp, "Profiles");
        P(sp, "Profiles (APP section, tray submenu, or the optional Cycle Profiles hotkey) are named zone workspaces: each holds its own zones, process exceptions, auto-close triggers, and default sliders — ideal for keeping a per-game setup separate from a desktop setup. Switching saves the current workspace into its profile first, so nothing is lost. App settings, hotkeys, and Auto-Pin rules stay global.");

        H(sp, "Auto-Pin");
        W(sp, "Auto-Pin scans the titles of every visible window every few seconds while it's on. It only ever changes window z-order and never reads window contents, but a background scanner is exactly the kind of thing some anti-cheat software dislikes — leave it off on gaming profiles if in doubt. It is off by default.");
        P(sp, "Auto-Pin (APP section) pins windows automatically: any visible window whose process name or title contains your pattern is pinned within a few seconds while the master switch is on. Rule pins count as OnTop pins — the Pinned Windows viewer badges them and Unpin All releases them. Unpinning a rule-pinned window by hand wins: it won't be re-pinned until it's closed and reopened. The feature is off by default.");

        H(sp, "Pin Scenes, Pin Memory, Timed Pin, Send To Monitor");
        P(sp, "Pinned Windows → Save Scene… snapshots every window OnTop pinned (position, size, opacity, click-through) under a name; Apply Scene… restores it — also as a Library action and in the palette. Pin Memory (Settings → Features) remembers how each program was last pinned and reapplies it next time. Two more optional hotkeys: pin the focused window for N seconds (Timed Pin, 30 s by default — a timer, no keyboard hook), and Send To Next Monitor (keeps its relative position and size). The tray menu offers quick opacity for the last window you pinned.");

        H(sp, "Zone Cooldown, Schedule & Hit Flash");
        P(sp, "Right-click a zone → Cooldown… ignores repeat triggers for N seconds after a hit (a safety net for Kill / Run Command zones). Schedule… limits a zone to a time window on chosen days. Zone Hit Flash (Settings → Features) pulses the overlay wherever a real gesture landed. Two more gestures: Shift + double-click and Alt + double-click.");

        H(sp, "Idle Rule, Schedules, Profile Diff");
        P(sp, "Idle Rule (Settings → Features): after N minutes without input, switch to a profile (reverts when you're back), unpin everything, or enter Stealth — it reads Windows' last-input time, no hook. Schedules…: time-of-day profiles on chosen days; process rules take priority. Every profile switch now logs and toasts a short diff (+/− zones, macro policy change).");

        H(sp, "Macro Dry Run, Pause, Notes");
        P(sp, "Dry Run (Macros window) plays a macro with the status strip narrating every step but sends NO input — check a chain before trusting it. An optional Pause/Resume hotkey freezes playback mid-run (the timeline re-bases; Ctrl+Alt+X still stops). The step editor gained a Note column and a Hide Mouse Moves view toggle.");

        H(sp, "Config History, Toast History, Themes");
        P(sp, "Settings → Data → Config History… lists the rolling .bak files and timestamped backups with dates and sizes; Restore uses the normal trusted own-path restore. Toast History… shows everything that flashed this session (DND-suppressed ones dimmed). Settings → Appearance → Theme: Dark (designed), High Contrast, or Light — applied live.");

        H(sp, "Hold Chords: Hold-To-Peek, Hold-To-Pin");
        W(sp, "These need a keyboard hook (a normal hotkey can't see when you let go). Like the macro recorder, that hook exists ONLY while the feature is on and a chord is set, and is removed in Stealth and on exit. It compares keys against two chords and stores nothing.");
        P(sp, "Settings → Features → Hold Chords, then set the chords in Hotkeys…: hold one to see every zone overlay at full opacity and release to hide; hold the other to keep the focused window on top and release to let it go. Releasing any modifier also ends the hold.");

        H(sp, "Pin Extras: Opacity, Click-Through, Corners");
        W(sp, "Click-through makes a window ignore your clicks — including its own close button. The same hotkey over it, its Click-Through button in Pinned Windows, or unpinning it, always reverts. OnTop never applies it to its own windows.");
        P(sp, "Click-through windows get a thin ORANGE FRAME so their state is visible (Pinned Windows → 'Orange Frame On Click-Through' switches it off), and the State column shows each row's opacity and click-through.");
        P(sp, "Optional hotkeys (Hotkeys…): Pin Window Under Cursor (for windows that won't take focus), Focused Window Opacity + / − (10% steps, down to 20% — keep a guide or chat visible over a game), Click-Through Under Cursor (clicks pass to what's beneath — overlay maps, timers, notes), and Cycle Focused Window Corner (hop between the four corners of its monitor, picture-in-picture style). The Pinned Windows viewer has − / + / Click-Through buttons per row.");

        H(sp, "Automation: Profile Rules, Display Profiles, Do Not Disturb");
        W(sp, "Profile Rules is a background process-name scanner (names only, never memory), shared with the stealth watcher. Off by default.");
        P(sp, "Settings → Features → Profile Rules…: 'while valorant runs, use profile Game' — and back when it exits. Put your Block-Macros game profiles on autopilot. Display Profiles: Settings → Displays → Bind This Layout binds the active profile to the current monitor arrangement; docking/undocking then switches automatically. Do Not Disturb quiets routine toasts while a watched or known anti-cheat process is running (errors still show).");

        H(sp, "Zone Gestures");
        P(sp, "Right-click a zone → Trigger Gesture: Double Left-Click (classic), Middle Click, Triple Left-Click, Ctrl + Double Left-Click, or Double Right-Click. The hook already sees all of them; a zone only fires on its own gesture, so overlapping zones with different gestures can coexist.");

        H(sp, "Discoverability: Cheat Sheet & Command Palette");
        P(sp, "Two optional hotkeys: the Cheat Sheet shows every current binding on one card (press again, Esc, or click to hide); the Command Palette opens a type-to-filter list of everything OnTop can do right now — built-ins, windows, profile switches, zone toggles, enabled library actions and macros — Enter runs it. Statistics gained bars under the most-triggered zones and Export CSV.");

        H(sp, "Action Library");
        W(sp, "A library action can run a command, kill a process, or play a macro from a plain hotkey — the same guarded verbs zones already have, just easier to reach. Imported libraries arrive with those actions disabled and unbound until you review them.");
        P(sp, "APP → Actions… holds named actions: Run Command, Kill Process, Toggle Window, Play Macro, Apply Placement, Switch Profile, Pause/Resume, Peek, Toggle Stealth, Pin Under Cursor. Give one a hotkey (one combo, one feature — conflicts are refused), point zones at it (Set Action → Library Action), or Run it from the window. Every run is logged; launches and kills use the same single choke-points as zone actions. Off by default in Settings → Features.");

        H(sp, "Window Placement & Launcher");
        W(sp, "The launch half starts whatever program path a rule names, as you. It is off twice by default — a global 'Allow Launching' switch AND a per-rule Launch box — moving never launches, every launch is logged and toasted, and launch paths from imported configs arrive blanked until you review them.");
        P(sp, "Add Rule… / Edit Selected… (or double-click / right-click a row) opens the full editor with a running-process picker, .exe browser, monitor, position, a DELAY (seconds to wait for programs still starting up), and the launch half. Every grid column has a hover description. If the log says 'access denied', the program runs as administrator — OnTop is non-elevated by design and cannot move it.");
        P(sp, "APP → Placement… keeps rules that put a program's window on a chosen monitor at a chosen position and size (or maximized). 'Capture From Focused Window' fills a rule from wherever the window is right now. Apply a rule from the button, all of them from the optional Apply All Placements hotkey, automatically a few seconds after OnTop starts ('On Start'), or from a zone (Set Action → Apply Placement). If no matching window exists and launching is allowed for that rule, OnTop starts the program and places its window once it appears (within ten seconds). The feature is off by default in Settings → Features.");

        H(sp, "Pinned Windows");
        P(sp, "The Pinned Windows viewer (APP section, tray menu, or its optional hotkey) lists every window currently marked always-on-top — with an OnTop badge on the ones this app pinned this run. Unpin any row individually, or Unpin All (Ours) to release everything OnTop pinned. Windows pinned by other tools appear too and can be unpinned the same way.");

        H(sp, "System Tray");
        P(sp, "Right-click the tray icon for quick controls: toggle the window, Quick Record (capture a zone in the current mode without opening the window), pause/resume detection, and Pause For (15 min / 30 min / 1 hour / 2 hours) which auto-resumes and shows the remaining time on the Resume item — resuming early cancels it. Enable/Disable All Zones, plus a Zones submenu to flip any single zone on or off (a ✔ marks enabled ones). Stealth and Re-Enable Mouse Hook live here too. The tray tooltip shows enabled-zone count and current status.");

        H(sp, "Saving and Safety");
        P(sp, "Save and Close writes settings.json with three rolling backups (.bak1–.bak3). While the window is open, a draft is written every 30 seconds for crash recovery. Undo/redo holds the last 50 states. Config and logs live in %APPDATA%\\OnTop (config\\ and logs\\) so they survive app updates and work when OnTop is installed to a read-only location; a config left beside the exe by an older version is migrated here automatically on first run.");

        H(sp, "About & License");
        P(sp, Helpers.AppInfo.TitleWithVersion + " — Copyright (C) 2026 Godswrath88. Free software under the GNU General Public License, version 3 or (at your option) any later version. This program comes with ABSOLUTELY NO WARRANTY. The full license text is in the LICENSE file shipped with the source; the program is an entirely AI-developed project, directed and tested by its author.");

        BuildToc(sp, 2);   // S28: after title + subtitle

        var scroll = new ScrollViewer
        {
            Content = sp,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Content = scroll;
        Helpers.EditorFooter.Wrap(this, scroll, onSave: null, onCancel: null, saveText: "Close");
    }

    private static string Hk(string key) => string.IsNullOrWhiteSpace(key) ? "Unbound" : key;

    // S28: every header is registered so the table of contents can jump to it.
    private readonly System.Collections.Generic.List<(string Title, TextBlock Anchor)> _toc = new();

    /// <summary>S28: amber caution paragraph for features with a security or
    /// comfort trade-off (warnings live where the feature is explained).</summary>
    private void W(Panel parent, string text)
    {
        parent.Children.Add(new TextBlock
        {
            Text = "⚠ " + text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.FindResource("WarnAmberBrush"),
            Margin = new Thickness(0, 2, 0, 4),
        });
    }

    /// <summary>S28: hyperlinked table of contents — a tidy wrap of pill links
    /// that scroll to each section. Inserted right under the subtitle.</summary>
    private void BuildToc(StackPanel sp, int insertAt)
    {
        var box = new Border
        {
            Background = (Brush)Application.Current.FindResource("BgBandBrush"),
            CornerRadius = (CornerRadius)Application.Current.FindResource("RadiusMedium"),
            Padding = new Thickness(12, 10, 12, 6), Margin = new Thickness(0, 8, 0, 6),
        };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = "CONTENTS", FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = Body, Opacity = 0.7, Margin = new Thickness(0, 0, 0, 6) });
        var wrap = new WrapPanel();
        foreach (var (title, anchor) in _toc)
        {
            // S28 Stage J (from testing: "hyperlinks need better shown separation"): each
            // entry is a bordered chip — background, accent border, padding, gap —
            // that highlights on hover. Reads as a row of buttons, not run-on text.
            var target = anchor;
            var chip = new Border
            {
                Background = (Brush)Application.Current.FindResource("BgPanelBrush"),
                BorderBrush = Accent, BorderThickness = new Thickness(1), Opacity = 0.9,
                CornerRadius = new CornerRadius(11), Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(0, 0, 8, 8), Cursor = System.Windows.Input.Cursors.Hand,
                Child = new TextBlock { Text = title, FontSize = 12, Foreground = Accent },
            };
            chip.MouseEnter += (_, _) => { chip.Background = Accent; ((TextBlock)chip.Child).Foreground = (Brush)Application.Current.FindResource("BgDarkBrush"); };
            chip.MouseLeave += (_, _) => { chip.Background = (Brush)Application.Current.FindResource("BgPanelBrush"); ((TextBlock)chip.Child).Foreground = Accent; };
            chip.MouseLeftButtonUp += (_, _) => target.BringIntoView();
            wrap.Children.Add(chip);
        }
        stack.Children.Add(wrap);
        box.Child = stack;
        sp.Children.Insert(insertAt, box);
    }

    private void H(Panel parent, string text)
    {
        var head = new TextBlock
        {
            Text = text, FontSize = 16, FontWeight = FontWeights.SemiBold,
            Foreground = Head, Margin = new Thickness(0, parent.Children.Count == 0 ? 0 : 16, 0, 4)
        };
        _toc.Add((text, head));
        parent.Children.Add(head);
        parent.Children.Add(new Border
        {
            Height = 2, Width = 34, Background = Accent,
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 6),
            CornerRadius = new CornerRadius(1)
        });
    }

    private void P(Panel parent, string text)
    {
        parent.Children.Add(new TextBlock
        {
            Text = text, FontSize = 13, Foreground = Body,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2)
        });
    }

    private void Mono(Panel parent, string text)
    {
        parent.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x1E)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 4, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = text, FontFamily = new FontFamily("Consolas"),
                FontSize = 13, Foreground = Head
            }
        });
    }
}
