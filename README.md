# OnTop — C# WPF (.NET 8)

Windows desktop utility: Always-On-Top toggle, exit-zone detection, system tray.
Migrated from a 5400-line AutoHotkey v2 script.

## Build & Run
Requires .NET 8 SDK (free, dotnet.microsoft.com). No Visual Studio needed.
```
cd OnTop
dotnet build
dotnet run
```
Publish single exe:
```
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```
Place your `thumbtack.ico` in `OnTop/Assets/` before building (see Assets/README.txt).

## Default Hotkeys
| Key | Action |
|---|---|
| F1 | Toggle Always On Top (focused window) |
| F10 | Exit cleanly |
| Ctrl+Alt+R | Toggle settings window |
| Ctrl+Alt+Q | Kill immediately (emergency, no cleanup) |
| Ctrl+Alt+P | Pause/resume zone detection |

First four are customizable via the Hotkeys dialog.

## Status
Core architecture complete and audited. Since then: hardening (single-instance
mutex, global exception handlers, mouse-hook watchdog, settings schema
versioning), visual identity (custom dark title bar, animated toggle switches,
toast notifications), the zone editor minimap, and the full zone overlay system
(dots, detection boxes, waterfall corner resize, legend, click-through lock).
The full original TODO list is complete: Exceptions and Auto-Close sub-GUIs,
zone import/export, groups, startup shortcut, multi-monitor warnings, and the
help window — plus a press-to-capture hotkey recorder, status bar, empty
states, swatch color cycling, and overlay opacity. See WHATS_NEW.md for
details. Remaining roadmap: profiles, per-zone actions, window rules,
statistics, theme variants, onboarding, update checker, and the deeper visual
overhaul. Companion spec: OnTop_Feature_Blueprint.docx. START HERE: PROJECT_SCOPE.md is the cross-chat continuity contract — read and maintain it every session. Outstanding work lives in ROADMAP.md; shipped work in WHATS_NEW.md (full narrative) and CHANGELOG.md (terse versioned index).
