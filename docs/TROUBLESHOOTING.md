# OnTop — Troubleshooting

Help inside OnTop (main window → Help) documents every feature. This page
covers what to do when something goes wrong.

## OnTop is frozen or won't close

The emergency stop runs on its own thread, so these hotkeys work even when the
rest of OnTop is not responding:

| Hotkey (default) | What it does |
|---|---|
| **Ctrl+Alt+Q**: Kill | Ends OnTop instantly. No cleanup: pinned windows stay pinned. |
| **F10**: Exit | Clean shutdown. If OnTop doesn't respond within 3 s, it falls back to Kill. |

If you changed them, they're listed in Hotkeys…. While the UI still responds,
tray icon → **Kill Now — No Cleanup** also works. Task Manager → OnTop → End
task is the last resort.

After a Kill, the tray icon may stay until you hover over it. That's a Windows
quirk, not a running process.

Then report the freeze (see [Reporting a problem](#reporting-a-problem)). The
log written just before it matters: look for `UI thread unresponsive for N s`.

## Where things are

| | Installed / built from source | Portable |
|---|---|---|
| Settings | `%APPDATA%\OnTop\config\settings.json` | `<exe folder>\Data\config\settings.json` |
| Automatic backups | `settings.json.bak1`–`.bak3` beside it, plus `config\backups\` | same, under `Data\` |
| Logs | `%APPDATA%\OnTop\logs\OnTop_<date-time>.log` | `<exe folder>\Data\logs\` |

Portable mode is on when an `OnTop.portable` file sits beside `OnTop.exe` (or
OnTop is started with `--portable`).

**View Logs** in OnTop opens the log with category filters. For a problem,
turn on **Settings → Logging → Verbose Logging** first: it records *why* things
were ignored (gesture gating, hotkey bind errors, placement decisions).

## Settings

**A prompt about a corrupt or newer config at startup.** OnTop never overwrites
a settings file it couldn't read. If `settings.json` is damaged, it tries the
`.bak` copies in order. If the file came from a newer OnTop version, it loads
what it understands and doesn't save over the file until you act.

**Restore an earlier configuration:** Config History… lists the automatic
backups; pick one and Restore.

**Start completely fresh:** exit OnTop, rename `%APPDATA%\OnTop\config` (for
example to `config.old`), and start OnTop again. Nothing is deleted, and you
can copy files back from the renamed folder.

**Turn everything optional off at once:** Settings → Features → **Minimal
Mode**. This leaves plain window pinning.

## Common problems

**A window won't pin, move, or change opacity; the log says "access denied".**
That program runs as administrator. OnTop deliberately runs without admin
rights, and Windows doesn't let a normal program change an elevated program's
windows. This often applies to hardware tools (GPU tuners, RGB software) and
some launchers. There is no workaround short of running both at the same level,
and OnTop isn't designed to run elevated.

**A hotkey does nothing, or the log shows `[Hotkey]` with code 1409.** Another
program already registered that combo. Pick a different one in Hotkeys…, or
close the other program. Settings → Performance → Hotkey Re-Bind Retry keeps
retrying in case the other program lets go.

**Double-clicking a zone does nothing.** With **Desktop Only** on, zones fire
only when the desktop is under the cursor, not a program window. With Verbose
Logging on, every ignored double-click logs its reason (`gated by Desktop Only
— under cursor: '<title>'`, `no enabled zone … contains it`, `in cooldown`,
`outside its schedule`). Test Mode shows the reason as a toast.

**Zone actions after importing someone else's zones do nothing.** Imported Run
Command / Kill Process / Toggle Window / Macro actions arrive switched off and
are listed for review. That's deliberate: check what they run, then enable them.

**Games with anti-cheat.** Macros refuse to run while a known anti-cheat
process is running, and **Stealth** removes OnTop's input hooks entirely. Read
Help → the macro section's limits before using macros anywhere near an online
game: OnTop reduces the risk but can't make synthesized input undetectable.

## Reporting a problem

- **Security problem:** don't open a public issue. Use the repo's private
  **Security → Report a vulnerability** (see [SECURITY.md](../SECURITY.md)).
- **Anything else:** open an issue with the **Bug report** template. Include
  the version (main-window subtitle or Help → About & License) and the steps.

**Logs are private.** They contain your window titles and process names. Paste
only the lines around the problem into an issue, never a whole log file.
