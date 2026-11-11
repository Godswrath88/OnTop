# OnTop

A Windows system-tray utility (C# / WPF / .NET 8) that does two things well:

1. **Always-on-top pinning** — a hotkey pins the focused window above everything (with optional opacity, click-through, corner cycling, timed pins, scenes and per-app memory).
2. **Zones** — spots on your desktop that react to a gesture (double-click, middle-click, triple-click…) and run an action: exit OnTop, enter stealth (remove the mouse hook entirely), run a command, place a window, play a macro, switch a profile, and more.

Everything beyond pinning and zones is optional and ships **off**: macros (record/playback with a three-layer anti-cheat guard), auto-pin rules, window placement & launcher, an action library, profile automation (process / schedule / idle / monitor-layout), hold-to-peek / hold-to-pin chords, statistics, and a check-only updater. **Minimal Mode** turns all of it off in one click.

> ⚠ **Honest note on macros and anti-cheat.** Synthetic input can be detected by anti-cheat software and can get accounts banned. OnTop refuses to play macros around watched and known anti-cheat processes and can kill-switch the module, but no guard is perfect and nothing here is retroactive. Use macros for single-player, accessibility and productivity. See `SECURITY.md`.

## Download / which version to use

[![Latest release](https://img.shields.io/github/v/release/Godswrath88/OnTop?label=latest%20release)](https://github.com/Godswrath88/OnTop/releases/latest)
[![Newest pre-release](https://img.shields.io/github/v/release/Godswrath88/OnTop?include_prereleases&label=newest%20pre-release&color=orange)](https://github.com/Godswrath88/OnTop/releases)

Every version tag has a GitHub Release whose first line states its status:

| Status | Meaning |
|---|---|
| ✅ **Stable** | Built clean **and** passed both the smoke test and the full feature checklist in `docs/testing/CHECKLIST.md` on my machine, or I have personally judged it good enough and said so in the release notes. |
| ⚠ **Restore point** | Built clean, but has known issues (linked in the release). Use it only to roll back. |
| 🧪 **Untested** | Built (or not yet built) but not tested. Marked *Pre-release* on GitHub. |

Only Stable and Restore-point releases can be GitHub's **Latest** release, so [releases/latest](https://github.com/Godswrath88/OnTop/releases/latest) never points at an untested build. `main` is usually ahead of it and untested.

**Right now no version is Stable** (pre-1.0, testing in progress). The latest release is **v0.43.0**, a restore point with known issues. **v0.45.3** is the newest version, and it is untested. Releases ship source only; see Build below.

## Build

Requires the free .NET 8 SDK — no Visual Studio needed.

```
cd OnTop
dotnet build
dotnet run
```

The first build restores one NuGet package (`System.Security.Cryptography.ProtectedData`, Microsoft's DPAPI wrapper, used to encrypt macro recordings at rest). OnTop ships its own icon (`OnTop/Assets/thumbtack.ico`, drawn by `tools/make_icon.ps1`); to use a different tray icon, put your own `thumbtack.ico` in the `assets` folder of your data directory.

- **Installer:** `installer/OnTop.iss` (Inno Setup 6) — see `docs/BUILD.md`.
- **Portable:** `portable/publish-portable.cmd` — single self-contained exe, data beside it. See `docs/PORTABLE.md`.

## Data

Config, logs and backups live in `%APPDATA%\OnTop` (or in `Data\` beside the exe in portable mode). Config is validated on load, written atomically with rotating backups, and never executes anything just by being loaded. Anything imported from outside arrives with its powerful content inert until you review it.

## Default hotkeys

| Key | Action |
|---|---|
| F1 | Pin / unpin the focused window |
| F10 | Exit OnTop |
| Ctrl+Alt+R | Show / hide the main window |
| Ctrl+Alt+Q | Kill immediately (no cleanup) |
| Ctrl+Alt+P | Pause / resume zone detection (fixed) |
| Ctrl+Alt+M | Start / stop a macro recording (only while macros are on) |
| Ctrl+Alt+X | Panic-stop macro playback (fixed, only while playing) |

Every other hotkey is optional and unbound until you set it. Hotkeys are recorded by pressing them; a combination another feature already uses is refused.

## Project documents

| File | What it is |
|---|---|
| `CHANGELOG.md` | What changed in each version. |
| `SECURITY.md` | Threat model, standing security rules, how to report a vulnerability. |
| `docs/TROUBLESHOOTING.md` | Frozen app, logs, resetting settings, common problems, reporting a bug. |
| `docs/BUILD.md`, `docs/PORTABLE.md`, `docs/UPDATES.md` | Building the installer, the portable exe, and the update manifest. |
| `docs/testing/CHECKLIST.md` | The standing test checklist, by feature (legend inside). Failures are tracked as GitHub issues. |
| `docs/dev/PROJECT_SCOPE.md` | For contributors: standing requirements, build rules, architecture map. |
| `docs/dev/ROADMAP.md` | Everything approved and outstanding. |
| `CLAUDE.md` | Working notes for the AI assistant that develops OnTop (workflow, hard rules). |
| `docs/history/` | Archived long-form notes from before the project moved to GitHub. |

## Status

Pre-1.0, no Stable release yet (see Download above). Every feature is built; the installer, updater and portable build are written but not yet exercised end-to-end. 1.0.0 ships when they are.

## An AI-developed project

OnTop is a **completely AI-developed project**. Every line of code, every document in this repository and the design decisions behind them were produced by Anthropic's Claude across a long series of development sessions. I (Godswrath88) direct the project: I decide what it does, set the security rules, build each version on Windows, test it, and feed the results back into the next session. Development happens with Claude Code in this repository: every change goes through a branch, a pull request and an automated build-and-test run on GitHub before I merge it. `CLAUDE.md` holds the working notes that carry the context from one session to the next.

The docs and code comments are written in first person as the author's notes. Treat this repository as what it is: a working program built by an AI under human direction, reviewed and tested by a human, and shared so that anyone can use, study, and improve it.

## License

Copyright (C) 2026 Godswrath88.

OnTop is free software: you can redistribute it and/or modify it under the terms of the **GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version** (SPDX: `GPL-3.0-or-later`). It is distributed in the hope that it will be useful, but **WITHOUT ANY WARRANTY**; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the `LICENSE` file for the full text.

In plain terms: use it, share it, change it — but anything built from this code must stay under the same license with its source available. Nobody gets to close it up and sell it as their own.
