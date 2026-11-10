# Building the OnTop Installer

The installer is an [Inno Setup](https://jrsoftware.org/isinfo.php) script
(`OnTop.iss`) that produces a single `OnTopSetup-<version>.exe` you can hand to
anyone. It fulfills the roadmap spec: wizard UI, Desktop / Start Menu shortcut
checkboxes, install-location choice, optional launch-at-startup, "Run OnTop
now" on finish, automatic .NET 8 Desktop Runtime download+install when
missing, and an uninstaller that asks before touching your data.

## One-time setup
1. Install **Inno Setup 6.2 or newer** (free): https://jrsoftware.org/isdl.php

## Build steps (every release)
From a terminal in this `installer\` folder:

```
dotnet publish ..\OnTop\OnTop.csproj -c Release -r win-x64 --self-contained false -o publish
```

Then open `OnTop.iss` in the Inno Setup Compiler and press **Compile**
(or from a terminal: `iscc OnTop.iss`). The result lands in
`installer\Output\OnTopSetup-<version>.exe`.

> Keep `#define MyAppVersion` in `OnTop.iss` in sync with the version in
> `OnTop.csproj` each release (the session workflow's version bump covers both).

## Choices baked in (and why)
- **Per-user install by default, no UAC** (`PrivilegesRequired=lowest`) — this
  matches the app's own `asInvoker` manifest and SECURITY.md's least-privilege
  rule. The wizard still lets a user pick an all-users install (normal UAC
  prompt appears in that case).
- **Framework-dependent publish** (`--self-contained false`) keeps the download
  small; the script detects a missing .NET 8 Desktop Runtime by running
  `dotnet --list-runtimes` and, if absent, downloads Microsoft's runtime
  installer over HTTPS (fixed `aka.ms` link) and runs it silently. Offline →
  clear instructions instead. If you'd rather ship a fully self-contained
  build (bigger, zero prerequisites), publish with `--self-contained true` and
  delete the whole `[Code]` .NET section from the script.
- **Uninstall asks before deleting `%APPDATA%\OnTop`** — settings, zones,
  profiles, stats, and logs survive a reinstall unless the user says otherwise.
- The **startup checkbox** creates the same Startup-folder shortcut the app's
  own "start with Windows" toggle manages, so the two stay consistent.

## Testing checklist (first build)
1. Run the setup on a machine (or VM) **without** .NET 8 → runtime downloads
   and installs silently, then OnTop installs and launches.
2. Re-run setup → upgrade-in-place, settings intact.
3. Toggle each wizard checkbox combination once (desktop / start menu /
   startup).
4. Uninstall → answer **No** to the data prompt → `%APPDATA%\OnTop` survives;
   reinstall → zones/profiles all present. Uninstall again with **Yes** →
   folder gone.

## Relationship to the updater (roadmap)
This installer is the delivery vehicle the future online updater hands off to:
check → prompt → download `OnTopSetup-x.y.z.exe` → verify → run this wizard.
Code-signing the setup exe (roadmap: Security/Distribution) removes SmartScreen
warnings and is the hard prerequisite for the updater's Authenticode check.
