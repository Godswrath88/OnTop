# OnTop — Portable Build

`publish-portable.cmd` produces a single `out\OnTop.exe` that runs from any
folder with **no install step at all** — copy it anywhere (or a USB stick) and
double-click. Configuration, zones, profiles, stats, and logs live in
`%APPDATA%\OnTop`, identical to the installed version, so the two can even be
used interchangeably on the same machine.

## Portable vs Installer — pick per situation
| | Portable exe | Installer (`installer\`) |
|---|---|---|
| Install step | none | wizard (shortcuts, startup, uninstall) |
| Size | ~150–180 MB (runtime baked in) | few MB + .NET 8 fetched only if missing |
| Works offline / no prerequisites | ✅ always | needs .NET 8 (auto-fetched once) |
| .NET security patches | ❌ frozen until you rebuild | ✅ serviced by Windows Update |
| Updater handoff target (future) | no | yes |
| Uninstall entry / shortcut hygiene | none (just delete the exe) | ✅ |

**True portable data (v0.30.0):** drop an empty file named `OnTop.portable`
next to the exe (or launch with `--portable`) and all config/logs/stats live in
a `Data` folder beside it — nothing touches `%APPDATA%`. Without the marker the
exe behaves like the installed version.

Both are built from the same source at the same version; build either, both, or
neither per release — nothing here affects opening/compiling the solution in
VS Code.

> Note: single-file WPF extracts native libraries to a temp folder on first
> launch (normal .NET behavior); first start is slightly slower and some AV
> heuristics give large unsigned single-exes extra scrutiny.
