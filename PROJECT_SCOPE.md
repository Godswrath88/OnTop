# OnTop — Project Scope & Continuity Document

> **READ THIS FIRST. MAINTENANCE IS MANDATORY.**
> This file exists because the v9.5 codebase was permanently lost when the
> session that held it ended and its source was never preserved. Months of
> features had to be reconstructed from fragments. Never again.
>
> **Contract for every development session on this project:**
> 1. Read this file, `ROADMAP.md`, and `WHATS_NEW.md` before writing code
>    (skim `CHANGELOG.md` for the 10-second version history).
> 2. Update all docs EVERY session — and update THIS file EARLY in a session
>    if scope/conventions change, not at the end (sessions can be cut off).
> 3. Every delivered zip must contain: `LICENSE`, `PROJECT_SCOPE.md`, `README.md`,
>    `WHATS_NEW.md`, `ROADMAP.md`, `CHANGELOG.md`, `MASTER_PROMPT.md`,
>    `SECURITY.md`, `HANDOFF.md`, `SESSION_STATE.md` (stage checkpoint ledger —
>    resume/rollback contract, S28), `OnTop.sln`, and the complete `OnTop/` source tree. The zip IS the backup;
>    there is no other copy.
>    Quick self-check before shipping (from the repo root):
>    `for f in LICENSE PROJECT_SCOPE.md README.md WHATS_NEW.md ROADMAP.md CHANGELOG.md MASTER_PROMPT.md SECURITY.md HANDOFF.md SESSION_STATE.md docs/testing/CHECKLIST.md docs/TROUBLESHOOTING.md OnTop.sln OnTop/OnTop.csproj; do [ -e "$f" ] || echo "MISSING $f"; done`
>    and confirm the source count hasn't shrunk (`find OnTop -name '*.cs' -o -name '*.xaml' | wc -l`).
> 4. If the working environment resets, restore from the last delivered zip
>    (`unzip OnTopWPF_updated.zip`) — never rebuild from memory.
> 5. Keep every delivered zip until the next one builds clean.
>
> **Doc roles (don't duplicate):** `WHATS_NEW.md` = full per-session narrative
> (the why + gotchas). `CHANGELOG.md` = terse versioned index (the what + when).
> `ROADMAP.md` = what's still pending. `PROJECT_SCOPE.md` (this) = conventions +
> architecture + the continuity contract. `README.md` = user-facing overview.
> `MASTER_PROMPT.md` = the copy-paste prompt I use to bootstrap a NEW
> development session (read order + workflow summary) — update it whenever the
> workflow, doc set, or standing rules change. `HANDOFF.md` = session-level bootstrap summary.
> `SESSION_STATE.md` = stage-level checkpoint ledger (S28): which staged zips
> exist, how to resume, how to roll back — updated before AND after each stage. `SECURITY.md` = the security contract (threat
> model, standing rules, per-session checklist) — read every session; security
> outranks every other requirement.

---


## ⭐ DOCUMENT EVERY CHANGE — NO EXCEPTIONS FOR "SMALL" ONES (standing rule since S27)
Every delivered version gets a CHANGELOG entry, a WHATS_NEW entry, and a
PROJECT_SCOPE line — **including docs-only, one-line, and trivial changes.**
This rule exists because it was already violated: versions 0.33.3 → 0.33.6 were
docs-only, each got WHATS_NEW + PROJECT_SCOPE coverage but NOT a CHANGELOG
entry, so the CHANGELOG silently fell four versions behind and the gap was only
caught during a later audit. A version that is "too small to document" is
exactly the version that goes missing.
Concretely, per delivery — no matter how small:
- bump OnTop/OnTop.csproj (Version/AssemblyVersion/FileVersion) AND
  installer/OnTop.iss MyAppVersion,
- add a CHANGELOG entry (with its link reference) even if the body is one line,
- add a WHATS_NEW entry,
- add the PROJECT_SCOPE session line (what changed and WHY, plus any lesson),
- if a build gotcha was learned, add it to the build-gotchas list the same turn.
Verify before zipping: csproj, .iss, CHANGELOG head, WHATS_NEW head, and the
PROJECT_SCOPE "current:" line must all name the SAME version. A mismatch means
the delivery is incomplete, not that the docs are "close enough."


## What OnTop Is

A Windows system-tray utility (C# WPF, .NET 8, MVVM) originally migrated from
a 5400-line AutoHotkey v2 script. Its two jobs:

1. **Always-on-top pinning** — a global hotkey pins/unpins the focused window.
2. **Zone exit / stealth** — a low-level global mouse hook watches for a double
   left click inside user-defined desktop "zones"; a matched zone either exits
   the app or drops it into Stealth Mode (see below).

Detection math (do not change without explicit request):
`half = (ZoneSize/2) + Tolerance + Variation` per axis; a click matches when
`|clickX−zoneX| ≤ halfX && |clickY−zoneY| ≤ halfY`. Detection is FIRST-MATCH
in zone list order. All zone coordinates are physical pixels.

## My Standing Requirements (never regress these)

- **No personal identifiers, ever (S28, v0.44.2).** Docs, comments, metadata
  and build output carry only the public handle `Godswrath88` — never my real
  name, Windows username, e-mail or machine name. Release builds use the
  csproj `PathMap`. Docs and comments are written in first person (as me) or
  neutral imperative; `README.md` is the one place that explains the project
  is AI-developed; `MASTER_PROMPT.md` is the one document written as a prompt.
- **The stop must never depend on what it stops (S28 L).** Exit and Kill are
  delivered on the e-stop thread (`EmergencyStopService`), not the WPF window.
  Never register them through `HotkeyService`; never add a UI-thread wait to the
  Kill path; anything new that can block the UI (foreign-process queries, big
  scans) runs off-thread with a timing log line.
- **License is GPL-3.0-or-later.** `LICENSE` is verbatim FSF text — never edit
  it; keep it in every zip; don't add a license-acceptance page to the
  installer (the GPL does not require acceptance to install or use).

- **SECURITY IS PRIORITY #1 — above features, above convenience.** Treat OnTop as
  a massively-used consumer program: it must never be a vehicle for code
  injection, privilege abuse, or any piggyback attack. `SECURITY.md` is the
  contract — read it every session, run its per-session Security Review Checklist
  after any code change, and update it when the attack surface changes. Headlines:
  least-privilege always (`asInvoker`, never elevate), config hardening (size cap,
  validation clamps, atomic writes, fail-safe-not-silent, no downgrade clobber),
  foreign windows get Z-order changes ONLY (never memory/injection), the hook only
  observes, and no untrusted data ever reaches an execution path.
- **Help + tooltips keep pace with the code (high priority).** Every new feature
  or behavior change updates the Help window section and hover tooltips in the
  SAME session it ships — an undocumented feature is an unfinished feature.
- **Every feature decides its hotkey story when it ships.** Ask: does this need a
  keybind? If yes: REQUIRED (rebindable, never unbindable — e.g. Kill) or
  OPTIONAL (default unbound)? The hotkey editor is organized into REQUIRED /
  OPTIONAL — ACTIONS / OPTIONAL — TOGGLES sections (S20 cont.); new entries go in
  the right section, never in a flat list.
- **No silent catches — ever.** Every `catch` either logs (DEBUG at minimum,
  through the main LogService) or carries a written justification comment for
  true silence (the logger's own failures, crash-path best-effort saves,
  pre-logger startup races). Verified zero bare `catch { }` as of v0.23.0 —
  keep it that way (part of the logging audit).
- **Main-GUI cohesion**: features reachable only by hotkey or tray feel bolted-on
  — when a feature makes sense as a button/toggle on the main window, give it
  one (S20 cont. added Peek / Legend / Paused for exactly this reason).
- **Dark, cohesive, professional UI everywhere** — every window, dialog,
  tooltip, scrollbar, context menu, and the tray menu must match the custom
  dark theme (navy `#1A1A28` base, accent `#3A8BFF`). No stock-light anything.
- **Saved confirmation lives in the status bubble with a timestamp — never a
  toast.** (Explicit v9.5 feedback.)
- **Two-zone toasts**: routine → top-right, critical/Error → screen center.
- **Hotkeys are optional and clearable** (press-to-capture recorder; Esc/Clear
  = unbound) — except Kill, which is required and falls back to Ctrl+Alt+Q.
  Pause is fixed Ctrl+Alt+P. New toggles should get optional hotkeys
  (default unbound) when sensible.
- **Record flow**: Record hides the GUI → full-screen capture overlay → click
  places the zone → naming prompt (Topmost InputDialog). Never
  record-at-button-position.
- **Stealth Mode**: removes the mouse hook entirely (anti-cheat safe) — not
  just ignoring clicks. Zones can trigger it (per-zone Action = Exit|Stealth,
  plus the global "Zones Trigger Stealth" override). Per-zone stealth is
  impossible by nature (the hook is process-global). Must always be exitable
  via tray, GUI, and (when bound) hotkey.
- **GUIs on top** is a user toggle (`Cfg.GuiAlwaysOnTop`, default on) bound to
  every non-modal window's Topmost.
- **Data lives in the user profile** at `%APPDATA%\OnTop` (`config\`, `logs\`,
  `assets\`), as of S13/v0.13.0 — moved off "beside the exe" so config survives
  updates and works when installed to a read-only location (Program Files). 3
  rotating backups (`.bak1–3`) + 30s crash-recovery drafts + atomic writes +
  auto-fallback to backups when settings.json is corrupt + `SaveNoBackup` (atomic
  write, no rotation) for high-frequency saves like window placement.

  ⚠️ **Standard / fallback directories that MUST be maintained** (do not remove
  in future refactors — losing them silently breaks upgrades or the tray icon):
  - **Legacy config location** — the old *beside-the-exe* path
    (`AppContext.BaseDirectory[\OnTop]\config`). `SettingsService.MigrateLegacyConfig()`
    probes it once on first run and copies `settings.json` + `.bak1–3` + a user
    icon into `%APPDATA%\OnTop`. Keep probing it until we're certain no user still
    has config there; deleting this path orphans existing users' zones/settings.
  - **Shipped icon fallback** — `AppContext.BaseDirectory\Assets\thumbtack.ico`.
    Icon resolution is user icon (`%APPDATA%\OnTop\assets\thumbtack.ico`) first,
    this shipped default second. The fallback must always remain a valid path even
    with no user override (it's also referenced in the csproj as
    `Condition="Exists('Assets\thumbtack.ico')"` — see the build-rules note).
  - **`%APPDATA%\OnTop` subfolders** (`config`, `logs`, `assets`) are created on
    startup; keep creating all three.
  - **Pruning** — a config whose `SchemaVersion` is newer than we understand is
    skipped (falls through to defaults, file left intact as a future `.bak`), so a
    downgrade can't corrupt behavior.
- **New zones join the active group filter.**
- **Confirm-before-exit** applies to Exit triggers only, never Stealth.
- Windows are hide-to-tray (main window Close/minimize must never kill the app).
- **Shippable installer**: the project must eventually produce a
  single shareable `.exe` installer with a desktop/Start-Menu shortcut choice,
  changeable install location, automatic .NET 8 runtime bootstrapping (end user
  installs nothing), and a highly visible uninstaller at the top of the install
  folder. I may build it locally first. Keep the installer script (Inno
  `.iss` preferred) in the repo. Full spec in ROADMAP.md.
- **Versioning**: the WPF rewrite has its own version line in `OnTop.csproj`
  (`<Version>`), read at runtime via `Helpers/AppInfo.cs` and shown in the
  main-window subtitle + Help header — this is distinct from the lost AutoHotkey
  **v9.5**. Bump `<Version>` (and `AssemblyVersion`/`FileVersion`) once per
  session and add the matching `CHANGELOG.md` entry. 1.0.0 is reserved for when
  the installer ships and v9.5 parity is complete (current: v0.45.3 — Draw/Drag-
  Box zones, all toggle + action hotkeys, window placement memory done; profiles
  / window-rules still pending). Don't hard-code the version string anywhere else
  — `AppInfo` reads it from the assembly.

## Build Rules (hard-won — violating these breaks the build)

- **One NuGet dependency** (S28): `System.Security.Cryptography.ProtectedData`
  — the DPAPI wrapper for optional macro encryption. Build needs NuGet restore
  (automatic online). Don't add packages casually — each is a SECURITY.md item.
- `net8.0-windows`, `UseWPF` + `UseWindowsForms`, `Nullable=enable`,
  `LangVersion=latest`. **NEVER enable ImplicitUsings** — it imports
  System.Drawing + WinForms unaliased and collides with WPF types (CS0104).
- WinForms is imported ONLY as `using Forms = System.Windows.Forms;`;
  System.Drawing (in App) as `using Drawing = System.Drawing;`.
- App icon references are `Condition="Exists('Assets\thumbtack.ico')"` — build
  must succeed without the icon. The icon is also copied to the output
  `Assets\` folder for the tray to load at runtime.
- Pre-build `taskkill` target kills a running OnTop.exe (file-lock fix).
- The editing environment CANNOT compile WPF. Verification = xmllint on all
  XAML + brace-balance on all .cs + grep cross-reference of every handler,
  binding, and new symbol. I compile on Windows and paste errors verbatim;
  fixes come from those.
- **DynamicResource for every brush (S28 K).** WPF FREEZES a brush the moment a
  Style referencing it is sealed (first use), so "mutate the shared brush" is a
  no-op and replacing the resource reaches only DynamicResource consumers.
  Rule: every `*Brush` reference in XAML is `{DynamicResource …}` (Freezable
  children — Pen, GradientStop, Storyboard… — excepted); live theme/accent
  changes REPLACE the Application-level resource (`ThemeOptions.SetToken`).
- **DataGrid over a SHARED collection must commit before the window closes
  (S28 K).** Otherwise the collection's default view stays in an EditItem
  transaction and the next window to bind it throws "'DeferRefresh' is not
  allowed…" — call `GridEdit.EnsureCommitted` before binding and on Closing.
- **Re-asserting HWND_TOPMOST on an already-topmost window is a no-op (S28 K).**
  Toggle NOTOPMOST → TOPMOST to move it to the top of the band.
- **`WindowFromPoint` returns OUR overlay windows (S28 K).** Any "what's under
  the cursor" logic must use `Win32.TopWindowAtPoint` with the overlay skip.
- **Any capture UI suspends global hotkeys (S28 K)** — `VM.SuspendGlobalHotkeys`
  / `ResumeGlobalHotkeys` around it, or the keys execute instead of record.
- **UI GROUPING REVIEW (standing, S28 Stage J).** Every session
  that adds a control decides WHICH card it belongs to (zone tool / groups /
  macros / app / a settings section) and puts it there — never "the nearest
  WrapPanel". A card that grows past ~2 wrapped rows gets split. Any window
  whose `*` row can be starved by `Auto` rows gets a `MinHeight` floor. New
  sub-features get a labeled sub-band or their own card. "Looks congested" is
  a defect, fixed in the same session as the feature that caused it.
- **Namespace gotcha:** `UniformGrid`, `Popup`, `Thumb`, `Track`, `ToggleButton`,
  `StatusBar`, `RepeatButton` live in `System.Windows.Controls.Primitives` — a
  code-built window that uses one needs that using (bit us in v0.42.1).
- **Brace-balance does NOT catch an UNDECLARED member either (CS0103/CS1061).**
  A ctor line `Foo = new FooService(this);` compiles in nobody's head — grep the
  SAME file for `Foo {` / `Foo;` and confirm the field/property exists. Also grep
  every `lock (x)` for a declared `x`. Bit us in v0.36.1: `MacroSvc` (assigned
  since v0.33.0, never declared) and `LogService._sync`. And inside a method,
  never reuse a name a loop/lambda already declared (CS0136 `idx`).
- **Brace-balance does NOT catch a duplicate method (CS0111) or type-level
  mistakes (e.g. CS0121).** Before ADDING a method (especially a `public` one) to an
  existing class, grep the file for the name first — a same-signature method already
  present compiles fine locally but errors on Windows. (Bit us once:
  `ZoneMapCanvas.ResetView` already existed when a second copy was added.) Likewise
  check a property's declared type before calling math/conversion helpers on it —
  `Math.Round` on an `int` property is a CS0121 ambiguous-overload error (bit us on
  `Zone.X`/`Y`, which are `int`, not double).
- Beware WPF implicit-TextBlock-style leakage into ContentPresenter-generated
  text (it broke MDL2 glyph buttons once). Prefer explicit TextBlocks with
  local FontFamily for glyphs, and keep the implicit TextBlock style free of
  FontFamily.
- **Window placement must be captured from `ActualWidth`/`ActualHeight`, never
  `Width`/`Height`.** With the custom `WindowChrome`, a drag-resize updates the
  actual rendered size but leaves the `Width`/`Height` properties at their XAML
  values, so reading `Width`/`Height` persists the default size forever (looked
  like "size won't save" for two sessions). Use `RestoreBounds` when
  maximized/minimized. See `Helpers/WindowPlacement.Save`.
- **A themed `MenuItem` ControlTemplate MUST name its submenu `Popup`
  `x:Name="PART_Popup"`.** WPF's `MenuItem` finds and manages the submenu popup
  by that exact template-part name (open/close, capture, dismiss chain). Any
  other name and submenus render but won't participate in dismissal — they open
  and then stay stuck on screen. The template also needs the `HasItems` trigger
  to reveal the expand arrow and `IsOpen="{TemplateBinding IsSubmenuOpen}"`.
- **The tray `ContextMenu` must foreground the owner window before opening** or it
  won't light-dismiss. `ShowTrayMenu` calls `SetForegroundWindow` on the main
  window's HWND before `IsOpen = true` (Win32 KB135788). The tray icon is a
  WinForms `NotifyIcon`, so nothing WPF is foreground on right-click; without the
  foreground call the menu stays open no matter what. (Submenu popup + top-level
  dismissal are two separate fixes — both are needed.)

## Architecture Map (file → responsibility)

- `App.xaml.cs` — single-instance mutex, crash handlers + crash log, low-level
  mouse hook + watchdog + startup grace, stealth hook lifecycle, tray icon/menu,
  service wiring.
- `Models/` — `Zone` (+`ZoneAction`), `AppSettings` (all persisted settings),
  `ObservableObject` (+RelayCommand).
- `Services/` — `SettingsService` (load/save/migrate/backups/import-export),
  `LogService` (categories, DEBUG/verbose, live tail), `HotkeyService`
  (RegisterHotKey), `ToastService` (two placements), `OverlayService` (desktop
  zone overlays + legend), `AutoCloseService` (process-trigger polling).
- `ViewModels/MainViewModel.cs` — everything user-facing: zones CRUD, sliders,
  groups, undo/redo, hotkey binding + retry, detection handling, backups,
  settings-change logging, auto-draft.
- `Views/` — MainWindow (Map/List/Split + pop-outs), MapWindow, ZoneListWindow,
  LogViewerWindow, SettingsDialog, HotkeyDialog, ExceptionsWindow,
  AutoCloseWindow, HelpWindow, InputDialog, ConfirmDialog, ZoneCaptureOverlay,
  ZoneOverlayWindow + OverlayLegendWindow, ZoneContextMenuFactory.
- `Controls/ZoneMapCanvas.cs` — monitor minimap (physical px), drag/select/
  create/context, zoom + pan, Locate flash (single + flash-all), `ResetView`.
- `Helpers/` — Win32 (DWM/hooks/interop), ChromeWindowBehavior, ThemeOptions,
  StartupShortcut, ZonePalette, `AppInfo` (assembly version), `WindowPlacement`
  (per-window size/pos, ActualWidth-based), `GridEdit` (click-out commit),
  converters (`ZoneColor`, `LogLevelBrush`, `WidthLabel`).
- `Views/ZoneListKeys.cs` — shared Delete / F2-rename / Ctrl+Arrow-nudge handler
  for both zone-list grids.
- `Views/PinnedWindowsWindow.cs` — Pinned Windows viewer (code-built window;
  `Win32.GetTopmostWindows` enumeration, `MainViewModel.PinnedByUs` badges,
  Unpin / UnpinAllOurs, "pins" placement key).
- `Views/WindowRulesWindow.cs` + `Services/WindowRuleService.cs` +
  `Models/WindowRule.cs` — Auto-Pin (window rules): editable rule grid, master
  toggle (off by default), 3s scanner over `Win32.GetVisibleWindows`.
- `Models/Profile.cs` + `Views/ProfilesWindow.cs` — Profiles (named zone
  workspaces): deep-clone snapshots synced from the live top-level cfg fields;
  VM engine in MainViewModel ("Profiles" region).
- `Models/AppStats.cs` + `Services/StatsService.cs` + `Views/StatisticsWindow.cs`
  — usage statistics (own stats.json store, increments at action sites).
- App "System events" region — resume/session-unlock re-assert + display-change
  routing (static SystemEvents, unsubscribed in OnExit).
- `Helpers/GridColumns.cs` — DataGrid column-width persistence
  (`AppSettings.ColumnWidths`, keys "grid/index").
- `Models/ConfigConflict.cs` — plain-language config conflict info for the
  startup Continue/Cancel prompt.
- `Models/QuarantineItem.cs` + `Views/ImportReviewWindow.cs` — S28 import
  quarantine: powerful content from OUTSIDE files lands inert; per-item verbatim
  opt-in. `SettingsService.IsOwnPath` decides provenance (own config dir = trusted).
- `SECURITY.md` (repo root) + `app.manifest` — the security contract and the
  least-privilege manifest.
- `Themes/DarkTheme.xaml` — ALL shared styles. New colors belong here as
  named resources (token pass in progress).

## Feature Inventory (shipped — details in WHATS_NEW.md)

Hardening (mutex, crash handlers, watchdog, schema migrations, backup
fallback, hotkey retry, atomic writes, grace period) · custom dark chrome ·
zone map with modes + pop-outs (synced selection) · desktop overlays with
resize waterfall + click-through lock + opacity + legend · **three recording
modes (Point / Drag Box / Freeform Draw) with live mode swap + naming** ·
**freeform Draw zones (ray-cast polygon detection, self-intersection lockout,
Redraw Shape, polygon render on map/overlays, snapshot scaling)** · groups
(filter/bulk/assign) · exceptions GUI · auto-close GUI+engine · import/export
(zones/exceptions/auto-close) · backups + restore (safety snapshot, undoable) ·
live Log Viewer (categories/verbose/search/follow) · hotkey recorder
(optional/required) · **optional default-unbound hotkey per main-GUI toggle** ·
**optional action hotkeys (Quick Record / View Logs / Soft Restart)** · Stealth
Mode (manual + zone-triggered + global override) · two-zone toasts · Settings
dialog · **grouped Zone Actions panel (RECORD & EDIT / ZONE DATA / CONFIGURE /
APP)** · Help · start-with-Windows · multi-monitor warnings · undo/redo · slider
lock · dark scrollbars/tooltips/menus · **runtime version stamp** · **window
size/position memory (main + pop-outs, off-screen-clamped)** · **click-out grid
commit** · **tray timed pause + per-zone toggle submenu** · config relocated to
`%APPDATA%\OnTop` with one-time legacy migration + newer-schema pruning ·
themed-submenu + detached-tray-menu dismissal fixes · **tray Quick Record** ·
per-zone Action chip dims when the global "Zones Trigger Stealth" override is on ·
**Delete / F2-rename / Ctrl+Arrow-nudge in the zone list** · **zone priority
reorder (Move Up/Down — first-match order = priority)** · **Locate (click the
◎/⚠ cell or double-click a row → pulse the zone on every open map)** · **zone
health badges (off-screen per-monitor / no-hit-area / duplicate-name → ⚠ +
tooltip, live)** · selected-row accent bar · **`--paused` / `--minimized` CLI
flags** · **startup status toast (zone count + paused/stealth)** · **bindable
Re-Enable Mouse Hook hotkey** · **Duplicate zone To Monitor** · **Copy Details
to clipboard** · **map Fit/Reset-View + Flash-All-Zones buttons** · **Monitor
column (adaptive "Monitor"→"Mon" header)** · **map/list split divider (drag +
in-drag center magnet)** · **blank-map context menu (one-click unhide / fit /
flash-all)** · **zone list search box** · **Log Viewer copy + working Follow** ·
**Pinned Windows viewer (topmost enumeration, OnTop-pin badges, per-row Unpin,
Unpin All (OnTop Pins), optional hotkey, pin/unpin toasts, own-GUI immunity)** ·
**Window Rules auto-pin (off by default)** · **column-width persistence (all
grids)** · **config conflict prompts + best-effort newer-config load** ·
**security hardening pass (SECURITY.md, asInvoker manifest, title sanitization,
hook guard, size-gated config)** · **taskbar icon setting** · **main-GUI Peek/
Legend/Paused controls + profile combo** · **PROFILES (named zone workspaces,
loss-proof switching, manager/tray/hotkey)** · **zero-silent-catch logging
(Config/Profiles categories, extended filter checkboxes)** · **STATISTICS
(session + lifetime counters, top zones, own store, confirmed reset)** ·
**display awareness (opt-in off-screen auto-disable)** · **resume/unlock
re-assert** · **multi-select bulk zone ops (single-undo)** · **diagnostics
bundle export** · **elevated-launch warning** · **hardened JSON import**.

## Not Yet Built

See `ROADMAP.md` (kept current every session). Headline pending: Pinned Windows
viewer (+ its hotkey), profiles, window rules, design-token pass, the **AHK
importer**, and the shippable installer. Already shipped (do NOT list these as
pending): Draw/Drag-Box zones, all per-toggle + action hotkeys, the Quick Record /
View Logs / Soft Restart / Re-Enable-Hook hotkeys, window size/position memory, and
the `%APPDATA%` config relocation + one-time legacy migration.

**End-goal capstone:** the roadmap's north star is a modular, optional **Macro
Subsystem** (full-fidelity record/playback + a three-layer anti-cheat guard: optional
module → silent unhook → detection-triggered kill switch). It lands LAST, behind a
master toggle, after the on-top side is stable. Full spec in ROADMAP's "🎯 End Goal"
section — read it before starting any macro work.

## Session Workflow

1. `unzip` the latest delivered zip → working tree; read the three docs.
2. Build features with verbatim-anchor edits; verify (xmllint + braces +
   cross-refs) before shipping.
3. Update `WHATS_NEW.md` (prepend session), `ROADMAP.md` (tick/add), and this
   file (only when scope/conventions change).
4. UI LAYOUT REVIEW (standing since S22) — after ANY UI element addition or
   change, sweep the touched windows for layout hazards before shipping:
   fixed pixel widths/heights that new content can outgrow, WrapPanels nested
   inside WrapPanels (the inner one never wraps — proven clipping bug), items
   without bottom margins in wrapping containers (wrapped rows touch), window
   Min/Default sizes vs new content, and long labels vs fixed columns. Also:
   any NEW extension-method call (LINQ-style helpers like GetValueOrDefault,
   CreateEntryFromFile) → confirm its namespace is imported — grep can't catch
   missing usings and they cost a build round-trip (S21 lesson).
5. Run the SECURITY.md per-session Security Review Checklist against everything
   changed this session (file I/O, foreign HWNDs, dependencies, manifest, data →
   execution paths). Security outranks shipping.
6. Repackage (the zip is the ONLY backup — ship it EVERY session):
   `zip -rq OnTopWPF_updated.zip LICENSE README.md WHATS_NEW.md ROADMAP.md PROJECT_SCOPE.md
   CHANGELOG.md MASTER_PROMPT.md SECURITY.md HANDOFF.md SESSION_STATE.md docs/ .github/ .gitignore .gitattributes .editorconfig OnTop.sln OnTop/ installer/
   portable/ -x '*/bin/*' '*/obj/*' '*/publish/*' '*/Output/*' '*/out/*'` — must
   contain all 5 docs + `MASTER_PROMPT.md` (the new-session bootstrap prompt; update
   it whenever the workflow/doc set changes) + `OnTop.sln` + the full `OnTop/`
   tree. Pre-ship self-check: required files exist and the source count hasn't
   shrunk.
5. Expect compile errors to come back pasted verbatim — fix, repackage.

## Session History (one line each)

- S1–S3: foundation hardening, chrome, minimap, overlays, sub-GUIs, groups,
  import/export, startup, help. Build fixes: Nullable, no-ImplicitUsings,
  conditional icon.
- S4: v9.5 parity phase 1 (slider lock, settings dialog, backups, two-zone
  toasts, naming prompt, taskkill) — rebuilt from recovered session records after
  the v9.5 source was confirmed lost.
- S5: 14-item feedback pass (caption CommandTarget attempt, capture-record,
  view modes + pop-outs, map context menu, live Log Viewer, GUIs-on-top,
  transfer chevrons, icon installed).
- S6: Stealth Mode (manual) + stability (backup fallback, hotkey retry,
  atomic writes, grace period) + labeled hotkeys.
- S7: zone-triggered stealth (per-zone Action + global override) + dark
  scrollbars/tooltips.
- S8: this document; caption-button + glyph root fix; tray icon fix + themed
  tray; vertical split; groups/tools relayout; minimize-to-tray; toggle
  hotkeys; map zoom/pan; monitor hide/unhide; help + tooltip pass.
- S9 (v0.9.0): recording modes (Point / Drag Box / Freeform Draw) + freeform
  Draw zones (ray-cast detection, self-intersection lockout, Redraw Shape,
  polygon render + snapshot scaling, Type column); optional default-unbound
  hotkey for all nine main-GUI toggles; runtime version stamp (AppInfo); new
  CHANGELOG.md; schema v3 (forward-only).
- S10 (v0.10.0): regrouped the Zone Actions panel into four labeled subsections
  (RECORD & EDIT / ZONE DATA / CONFIGURE / APP); added the last three
  v9.5-parity action hotkeys — Quick Record (auto-named, no window), View Logs,
  Soft Restart (save + relaunch via a `--restart` mutex-wait handoff);
  StartRecording refactor; schema v4 (forward-only).
- S11 (v0.11.0): distinct vector pop-out icons (folded-map vs bulleted-list, each
  with a pop-out arrow) replacing two identical glyphs; window size/position
  memory for the main window + all three pop-outs, off-screen-clamped, flushed at
  exit (`Helpers/WindowPlacement`, `AppSettings.WindowPlacements`); schema v5
  (forward-only).
- S12 (v0.12.0): click-out commits a zone-list edit (main + pop-out, reusable
  `Helpers/GridEdit`); tray timed pause (Pause For 15/30/60/120 min, auto-resume,
  remaining-time hint, cancel-on-early-resume) and per-zone enable/disable
  submenu; Help System Tray section. No schema change.
- S13 (v0.13.0): bug-fix pass on the S11–S12 work + config relocation. Fixed:
  window size/position now persists (live-tracked on resize + `SaveNoBackup` on
  close, not just at exit); grid click-out commit made coordinate-based (was
  misfiring under mouse capture); the themed **MenuItem template gained submenu
  support** (root cause of the tray "Pause For"/"Zones" submenus doing nothing —
  it had no sub-popup); overlay opacity now applied to the overlay's content root
  (Window.Opacity wasn't repainting the click-through layer); zone list rows got
  the same right-click menu the map has. Config moved to `%APPDATA%\OnTop` with
  one-time legacy migration + newer-schema pruning (see the maintain-these
  directories note above). No schema change (still v5).
  - **v0.13.1 follow-up**: fixed window-size capture (`ActualWidth/ActualHeight`,
    not `Width/Height` — see build rules), grid click-out now also fires on the
    list's empty area, tray submenu popup renamed to `PART_Popup` so it dismisses,
    and the "Dot" list column renamed to "Overlay".
- S14 (v0.14.0): tray Quick Record (last v9.5-parity tray action); per-zone Action
  chips dim/disable in both lists when Zones-Trigger-Stealth overrides them (with
  tooltip, matching Slider Lock); Delete/F2 in the zone list (remove/rename,
  undoable, ignored while editing) via shared `Views/ZoneListKeys`. No schema change.
- S15 (v0.15.0): five self-contained additions — zone priority reorder (Move Up/Down
  in the context menu; detection is first-match by list order; undoable); Locate
  (double-click a list row pulses the zone on every open map via VM
  `FlashZoneRequested` → `ZoneMapCanvas.FlashZone`); zone health badges (⚠ column
  bound to `Zone.HealthWarning`, recomputed by the VM on zone add/remove/edit for
  off-screen / no-hit-area / duplicate-name); selected-row accent bar; and
  `--paused` / `--minimized` CLI flags (parsed via `HasArg` in `App.OnStartup`).
  No schema change.
  - **S15 planning addendum (no code):** designated the **Macro Subsystem** as the
    roadmap's end-goal capstone — a modular, optional record/playback module with a
    three-layer anti-cheat guard (optional module → silent unhook → detection-
    triggered kill switch that fully disables macros on watched processes/windows/
    zones), full-fidelity "session" recording (all input incl. mouse movement, with
    real-time delays preserved), per-game policy with fail-safe defaults, and blunt
    anti-cheat warnings. Lands last, behind a master toggle. Full spec in ROADMAP
    "🎯 End Goal" section.
- S16 (v0.16.0): test-feedback fixes + additions. Fixes: startup status toast
  (App.OnStartup — zone count + paused/stealth); Locate is now a single click via a
  clickable leftmost cell that shows a dim ◎ (locate) or amber ⚠ (health warning),
  `LocateCell_Click` in both list code-behinds; off-screen health now tests each
  `System.Windows.Forms.Screen` rectangle instead of the virtual-desktop bounding box
  (catches monitor gaps — note the map clamps drags, so off-screen is mainly reachable
  via coord edits/unplugged monitors); Re-Enable Mouse Hook is now the bindable
  `HotkeyRehook` (was hardcoded Ctrl+Alt+R) — editor row + `RegisterIfSet`, defaults to
  Ctrl+Alt+R. Additions: Ctrl+Arrow nudge for the selected zone (`NudgeSelectedZone`,
  Shift=10px, in `ZoneListKeys`); Duplicate To Monitor context submenu
  (`DuplicateZoneToMonitor` — same relative position on the target screen);
  Copy Details (zone specs → clipboard). **Schema v5 → v6** (bindable `HotkeyRehook`,
  no-op migration — defaults to Ctrl+Alt+R so existing configs are unchanged).
- S17 (v0.17.0): three small map/list additions. `ZoneMapCanvas.ResetView` (zoom=1,
  pan=0) behind a "Fit / Reset View" toolbar button; `ZoneMapCanvas.FlashAllZones`
  (flash reworked with a shared `StartFlash` + `_flashAll` flag so it pulses every zone
  together) behind a "Flash All Zones" toolbar button — both operate on the embedded
  `ZoneMap` canvas via `FitMap_Click` / `FlashAll_Click`. Added a "Mon" column to both
  lists bound to new `Zone.MonitorLabel` (`Monitor + 1`; the `Monitor` setter now also
  notifies `MonitorLabel` so it updates live on cross-monitor drags). No schema change.
  Note: export/import zones (settings APP section) and the window status bar (`StatusText`
  + zone count, Row 5) already existed — confirmed before adding.
  - **v0.17.1**: adaptive Monitor column header ("Monitor"→"Mon" when narrowed) via new
    `Helpers/WidthLabelConverter` bound to header `ActualWidth`. Plus a documentation
    audit (no code): brought the Feature Inventory current through S17, corrected Not Yet
    Built, fixed the Session Workflow zip command, and expanded the Architecture Map.
- S18 (v0.18.0): log-file-driven fixes + additions. Fixed the 30s
  "Setting changed: AutoCloseTriggers" spam (auto-draft `SaveDraftNow` now
  SequenceEqual-guards the list reassign; bulk-sync list props excluded from the
  settings-changed logger; logged lists print contents). Fixed log Follow (scroll was
  synchronous inside CollectionChanged → deferred via Dispatcher at Background priority
  in `ScrollToNewest`; Follow re-check jumps to end; log toolbar tooltips added).
  Added: map/list `GridSplitter` (`MapListSplitter`, in the 10px `SplitGap` column;
  `SplitDragCompleted` eases back to the default 4.5/5.5 ratio when released within
  ±6% — timer-driven ease, no DP tricks; MinWidths 150/190); blank-map right-click menu
  via new canvas event `MapBlankContextRequested` (one-click Show Monitor N / Show All —
  the primary unhide path now, `ShowMapBlankMenu` in MainWindow + inline in MapWindow —
  plus Fit/Flash All); zone list search box (`MainViewModel.ZoneSearch` composes with
  the group filter inside `ApplyGroupFilter`; map unaffected since the canvas reads the
  collection directly, not the view); Log Viewer copy (multi-select, Ctrl+C /
  context menu, file-format output). No schema change.
- S19 (v0.19.0): Pinned Windows viewer (first dense roadmap item): Win32 gains
  `EnumWindows`/`IsWindowVisible`/`IsWindow`/`GetWindowText(Length)`/`GetWindowTitle`/
  `SetTopmost`/`GetTopmostWindows`; VM tracks `PinnedByUs` (HashSet, updated in
  `OnHotkeyToggle`) with `Unpin`/`UnpinAllOurs`; new `Views/PinnedWindowsWindow.cs`
  (badge column, per-row Unpin, Unpin All (Ours), Refresh, excludes our GUI, prunes
  dead handles, "pins" placement); entry points: APP "Pinned..." button, tray item,
  VM event `PinsRequested` + new bindable `HotkeyPins` (editor row). **Schema v6 → v7**
  (HotkeyPins + pins placement, no-op migration). Also: `MASTER_PROMPT.md` created
  (new-session bootstrap prompt; added to the zip contract, self-checks, and doc roles —
  keep it updated when the workflow changes), and the split magnet reworked to act
  DURING the drag (cumulative-distance hold at center via `SplitDragStarted`/
  `SplitDragDelta`; release-ease kept) because snap-on-release wasn't feelable.
- S20 (v0.20.0): SECURITY formalized as priority #1 (`SECURITY.md`: threat model,
  standing rules, per-session checklist; workflow step added; `app.manifest` pins
  asInvoker + PerMonitorV2; config 4 MB parse gate + `Validate()` clamps; window
  titles sanitized in `Win32.GetWindowTitle` — log forging; hook callback
  exception-guarded; `Win32.IsOwnWindow` immunity in toggle/unpin/enumeration).
  Config-trust fixes: newer-schema configs now LOAD BEST-EFFORT instead of reset
  (root cause of cross-version placement loss — an older build no longer clobbers
  a newer config); plain-language startup prompt on conflicts
  (`Models/ConfigConflict` + ConfirmDialog in App.OnStartup, Continue/Cancel).
  Column-width persistence: `AppSettings.ColumnWidths` + `Helpers/GridColumns`,
  wired for main ("main"), pop-out list ("list"), pins ("pins"), rules ("rules");
  main saves on hide/close AND App.OnExit via `MainWindow.SaveGridLayout()`.
  Pinned viewer: "Other" label, "Unpin All (OnTop Pins)", `ReassertTopmost()`
  after ops; pin/unpin/unpin-all toasts added (none existed). Split magnet
  rebuilt: plain Thumb (GridSplitter's class-handler resize fought ours — the
  center jitter), lock at ±2%, release past ±5.5% with RE-BASING so travel
  continues from the release edge (no cursor jump). NEW Window Rules (auto-pin):
  `Models/WindowRule`, `Services/WindowRuleService` (3s scan of
  `Win32.GetVisibleWindows`, contains-match on process/title, hand-unpin wins via
  a handled-set, security-first), `Views/WindowRulesWindow` (editable grid,
  master toggle bound to `WindowRulesEnabled` — OFF by default), APP "Rules..."
  button, Help section. Placement added: help/autoclose/exceptions/legend
  (legend keeps bottom-right default until moved). **Schema v7 → v8**
  (ColumnWidths, WindowRules, WindowRulesEnabled; no-op).
  - **v0.21.0**: post-crash integrity audit (clean — BindHotkeys flagged by the
    crude dup-scan is two legal overloads, and the Win32 tuple-returning helpers
    are regex false-negatives; both verified single). Added `OverlayService.Peek`
    (all zones at full opacity ~1.5s then `Refresh()`; press-to-peek — a hold
    needs a keyboard hook, refused per SECURITY.md) behind new bindable
    `HotkeyPeek`, and `HotkeyRules` + tray item for the rules manager
    (`MainWindow.ShowWindowRules` made public). VM events
    `PeekRequested`/`RulesRequested`. **Schema v8 → v9** (both hotkeys, no-op).
  - **v0.22.0** (feedback round): split magnet v3 (unlock re-bases to CENTER —
    v2 hopped to the release edge; `_magnetArmed` re-arm hysteresis; pixel-snapped
    star widths + no-op write skip — the shimmer was sub-pixel relayout; bands
    ±3%/±6%; stale v0.18 comment block removed). "Rules"→"Auto-Pin" in all
    user-facing strings (identifiers unchanged). GUI cohesion: Peek/Legend
    buttons + Paused ToggleSwitch in APP (VM `RequestPeek`/`RequestLegend` +
    `LegendRequested` event; Paused binds `DetectionPaused`). Taskbar icon:
    `Cfg.ShowInTaskbar` (default TRUE) bound on MainWindow; minimize hides to
    tray ONLY when off. Hotkey editor: label col 200px + wrapping (clipping
    fix), reorganized REQUIRED / OPTIONAL—ACTIONS / OPTIONAL—TOGGLES. Three new
    standing requirements (help/tooltip upkeep, hotkey-story decision,
    GUI cohesion). **Schema v9 → v10** (ShowInTaskbar).
  - **v0.23.0**: taskbar toggle fix (XAML binding removed — live EX-style churn
    under WindowChrome/Mica broke the GUI; now code-managed `ApplyTaskbarSetting`
    hide→set→show + `Win32.ApplyBackdrop` + Topmost re-assert). Split magnet v4:
    STATELESS gravity-well curve in `SplitDragDelta` (dead ±1.2% exact hold,
    quadratic well to ±7%, continuous everywhere — v2/v3 state machines produced
    per-swipe glitches; jumps now impossible by construction). Logging audit
    FIRST PASS: zero bare catches (log or justified),
    `SettingsService.AttachLogger` (+ Config category; VM attaches right after
    LogService creation), `LogService.KnownCategories` extended. PROFILES:
    `Models/Profile` (deep-Clone snapshots), `AppSettings.Profiles`/
    `ActiveProfile`/`HotkeyProfiles`, VM engine (`CaptureLiveWorkspace`/
    `SyncActiveProfile` in SaveAll/`SwitchProfile`/`Create`/`Rename`/`Delete`/
    cycle hotkey; live state STAYS in top-level cfg fields — profiles are
    synced snapshots, zero disruption to existing code), `Views/ProfilesWindow`
    ("profiles" placement), APP combo (`SyncProfileCombo` guard flag) +
    Profiles... button, tray submenu, editor row, Help section, Validate()
    clamps. **Schema v10 → v11**.
  - **v0.24.0**: STATISTICS. `Models/AppStats` + `Services/StatsService` (OWN
    file `stats.json` in ConfigDir — settings schema untouched; size-gated read,
    Validate clamps, atomic writes, 60s dirty-flush timer, Dispose flush;
    `AccumulateRuntimeAndSave` from App.OnExit BEFORE Hotkeys.Dispose). VM owns
    `Stats` (created right after LogService; increments at the zone-match site
    in HandleDoubleClick, pin/unpin/unpin-all, both peek paths, SwitchProfile,
    rule pins in WindowRuleService, BumpLaunch in App.OnStartup).
    `Views/StatisticsWindow` ("stats" placement, top-5 per-zone by Id with
    deleted-zone awareness, confirmed Reset preserving FirstRun). APP "Stats..."
    button + Help section. Hotkey decision: NONE (documented).
- S21 (v0.25.0) — roadmap sweep, six items: display-configuration awareness
  (App `SubscribeSystemEvents`/`UnsubscribeSystemEvents` — SystemEvents are
  STATIC, must unsubscribe on exit; `DisplaySettingsChanged` → VM
  `OnDisplaysChanged`: health recompute, `DisplaysChanged` event repaints both
  maps, opt-in `AutoDisableOffscreenZones` disables zones off every monitor with
  toast+log; **schema v11 → v12**; Settings "Displays" section). Sleep/resume +
  session-unlock re-assert (`ReassertAfterSystemEvent`: reinstall hook unless
  stealth, `BindHotkeys()`, re-topmost PinnedByUs w/ dead-handle prune, GUI
  topmost; PowerModeChanged.Resume + SessionSwitchReason.SessionUnlock,
  dispatcher-marshalled). Multi-select bulk ops (VM `RemoveZones`/`SetGroupFor`/
  `SetEnabledFor`, single PushUndo each; `ZoneListKeys.Handle` gained optional
  DataGrid param — BOTH call sites updated; `ZoneContextMenuFactory.ShowForRow`
  preserves multi-selection and prepends a bulk section). Diagnostics bundle
  (Log Viewer "Export Diagnostics..." → zip of settings/stats/current log/
  diag.txt; `LogService.CurrentLogFile` exposed). Elevated-launch warning
  (WindowsPrincipal check in OnStartup — warn, never block). Import hardening
  (4 MB gate + `ClampZones` shared by Validate AND ImportZonesFile — dedup so
  rules can't drift). Hotkey decisions: NONE for all six (automatic or
  context-menu; documented). Help sections added (multi-select, displays &
  system events, diagnostics).
  - **v0.26.0**: INSTALLER PACKAGE — `installer/OnTop.iss` (Inno Setup 6:
    wizard w/ desktop+startmenu+startup tasks, location choice, run-on-finish,
    `dotnet --list-runtimes` detection + HTTPS aka.ms runtime bootstrap,
    PrivilegesRequired=lowest per-user no-UAC default matching asInvoker,
    uninstall prompts before DelTree of %APPDATA%\OnTop) + `installer/BUILD.md`
    (publish + compile steps, first-build test checklist). Ships
    READY-TO-COMPILE — the editing environment can't run Inno Setup; I compile.
    Activity feed: `StatEvent` + `AppStats.RecentEvents` (300 cap, validated),
    `StatsService.AddEvent` wired into every Bump (pins carry process names,
    profile switches carry names), StatisticsWindow "RECENT ACTIVITY (LAST 25)"
    section. Zip contract now includes `installer/`.
- S22 (v0.27.0) — feedback round from my testing: profile-delete bug FIXED (SwitchProfile's
  SyncActiveProfile REPLACES the doomed record, so Remove(p) held a stale
  reference and silently no-opped → now name-based RemoveAll after the switch).
  Magnet v5 (dead ±2.5% real flat hold mid-drag — v4's ±1.2% was imperceptible,
  reading as "release-only"; curve |o|·(|o|−dead)/(band−dead), continuous at
  both edges; release-settle only from inside the hold = zero-pixel normalize).
  Map visual floor (to-scale boxes at 3-monitor zoom were 2–3 px, hidden UNDER
  the fixed dot → min 9 px half-extent, marker-not-measurement below that).
  Taskbar semantics v13: window ALWAYS in taskbar while open; setting is now
  **MinimizeToTray** (migration: old ShowInTaskbar=false → MinimizeToTray=true;
  ApplyTaskbarSetting churn machinery DELETED — root simplification, schema
  v12 → v13). APP section reorganized into two labeled wrap rows w/ bottom
  margins (single overloaded panel clipped on wrap). Log viewer category
  clipping fixed (WrapPanel-in-WrapPanel never wraps → DockPanel host).
  Sub-GUIs (list/map/logs/pins/rules/profiles/stats/help/autoclose/exceptions)
  now ShowInTaskbar=true for hover navigation. Workflow gains the UI LAYOUT
  REVIEW pre-ship step + extension-method using check.
- S23 (v0.28.0) — 3 major + 4 small items I asked for: CRASH REPORTS
  (`Services/DiagnosticsService` shared writer — viewer export delegates;
  `App.OfferCrashReport` once/session via InputDialog comment →
  `WriteCrashBundle` to ConfigDir\crashes; fatal handler auto-bundles).
  UPDATER STAGE ONE (`Services/UpdateService.CheckAsync(vm, manual)` —
  dormant while `UpdateManifestUrl` empty; https enforced on manifest AND
  release URLs; 64 KB cap, 10 s timeout, `Version` compare w/ downgrade
  refusal, `IgnoredUpdateVersion` semantics, browser handoff via
  ShellExecute; `Views/UpdatePromptDialog` 3-way; Settings "Updates" section
  w/ URL TextBox + startup toggle + Check Now; App delayed startup check;
  SECURITY.md network rule AMENDED — stage two still signing-gated;
  `installer/UPDATES.md` manifest doc; **schema v13 → v14**). TEST MODE
  (VM.TestMode session-only; hit → toast+RequestFlashZone+log, action AND
  stats skipped; miss toast at loop end; APP "Test" ToggleSwitch). Smalls:
  tray paused badge (`PausedVariant` drawn amber dot, `UpdateTrayIcon`;
  DetectionPaused setter now the SINGLE PauseStateChanged raiser); map
  edge/corner snapping in OnMouseMove (12 map-px / _scale threshold, Shift
  bypass); slider AutoToolTip bubbles; .iss DelTree path guard. INFRA:
  `portable/` (publish-portable.cmd self-contained single-exe + PORTABLE.md
  trade-off table) — installer/ and portable/ independently buildable, VS Code
  build untouched. Zip contract adds `portable/`.
- S24 (v0.29.0) — fix round + 3L/4N. MAGNET v6 ROOT CAUSE: Thumb DragDelta is
  thumb-relative → magnet-held thumb = cursor divergence feedback (poisoned v2–
  v5); now absolute `Mouse.GetPosition(SplitHost)` sampling (grid named
  SplitHost). PER-MONITOR OVERLAYS: `ZoneOverlayWindow(targetDevice)` covers one
  screen (re-resolved by DeviceName; Hide if unplugged) + `OnDpiChanged` reflow;
  OverlayService `_overlays2` list, `EnsureWindows` screen-signature rebuild,
  `Distribute(zones, clickThrough, opacity)` splits by anchor containment (index
  pairing = build order), Refresh/Peek/Preview all route through it; display
  change → rebuild+Refresh. TRAY PERSISTENCE (`ApplyTrayPersistence`:
  StaysOpenOnClick recursive, PreviewMouseMove timestamp + 200 ms timer closing
  after 600 ms un-hovered; routed events traverse submenu popups so the
  timestamp covers them; live ● updates in Profiles submenu; schema **v14 →
  v15** w/ FirstRunCompleted migration existing→true). DRAG-PREVIEW (canvas
  `ZonePreviewRequested` at first move + existing `ZoneDragCompleted` → VM
  `ZonePreviewStarted/Ended` → OverlayService full-opacity include + 3 s
  linger; zone INPC gives live motion free). WELCOME tour
  (`Views/WelcomeWindow`, `VM.AddStarterZones` — `AddZoneAt` gained
  `pushUndo:false` for single-undo batches; NO Owner set — main may be
  unshown). DATA SAFETY: `LogService.TailText` → bundle log-tail.txt;
  pre-import `WriteBackup`; `WriteBackup` prunes OnTop_Backup_* to newest 10.
  N: `GridEdit.AttachFocusCommit` both lists; startup-shortcut self-repair
  made explicit (Apply rewrites .lnk every launch); hook latency EMA (warn
  once > 3 ms); zone context-menu Color submenu via `ZonePalette`.
- S25 (v0.30.0) — visual cohesion + portable. THEME: new tokens
  (BgBand/WarnAmber/RowDangerTint/RowAccentTint + RadiusSmall/Medium),
  straggler conversion (MainWindow bands/warn/tints, ZoneListWindow,
  HotkeyDialog FindResource); `FocusRing` style + FocusVisualStyle on 7
  interactive styles; uniform IsEnabled=False → Opacity 0.5 on 9 styles
  (python block-insertion into Style.Triggers, new section when absent);
  DarkChromeWindow 150 ms Loaded fade+scale (EventTrigger storyboard;
  RenderTransformOrigin 0.5 + ScaleTransform setters; CLOSE motion deferred).
  ACCENT: `ThemeOptions.Accents` (6) + `ApplyAccent(index)` swapping
  Application resources BEFORE any window (StaticResource resolves at element
  creation → restart-to-apply semantics; Settings combo + toast; **schema v15
  → v16** AccentIndex, clamped in Validate). WIN10 ACRYLIC:
  `Win32.ApplyBackdrop` branches on Build ≥ 22621 (Mica) else
  SetWindowCompositionAttribute AccentPolicy state 4, tint 0xCC281A1A
  (AABBGGRR). PORTABLE MODE: `OnTop.portable` marker or `--portable` →
  AppDir = <exeDir>\Data; `IsPortableMode` exposed; PORTABLE.md + Help
  documented. AUDITS closed w/ findings: overshadowed controls (startup-update
  toggle dims while URL empty — live TextChanged sync), own-window hook guard
  (pid-level correct; capture overlay uses WPF events not the hook), logging
  second pass (14 categories, 93 categorized sites verified). ROADMAP gains
  pre-macro "Final visual/UX QA pass" entry.
- S26 (v0.31.0) — per-zone actions + stealth follow-ups. ACTIONS: ZoneAction
  += RunCommand=2/KillProcess=3/ToggleWindow=4 (INT-serialized, append-safe;
  Validate range-checks); Zone.ActionCommand(≤500)/ActionProcess(≤100) clamped
  in shared ClampZones; VM `RunZoneCommand` (ShellExecute UseShellExecute,
  non-elevated, log+toast EVERY run) / `KillZoneProcess`
  (`ProtectedProcesses` name blocklist refusal, kills all matches w/ count) /
  `ToggleZoneWindow` (IsIconic → SW_RESTORE+SetForegroundWindow else
  SW_MINIMIZE — all three added to Win32 public); trigger switch returns
  before ConfirmBeforeExit; chip quick-flip resets advanced→Exit (documented);
  "Set Action" context submenu shows command/process VERBATIM in headers;
  SECURITY.md new section "Per-Zone Actions" (data→execution rules). STEALTH:
  HotkeyStealth (field=-1, RegisterIfSet, Unregister, editor row);
  App `StartStealthWatcher` 5 s DispatcherTimer — engages on any watched name
  (comma/semicolon, .exe stripped), `_autoStealthEngaged` gate so manual
  stealth never auto-releases; `HideOverlaysInStealth` default TRUE —
  OverlayService OnCfgChanged StealthMode/flag branch hides all or Refresh;
  Settings "Stealth" section (watch TextBox + toggle + hotkey note).
  **Schema v16 → v17.** AHK importer marked OPTIONAL in roadmap.
- S26 cont. (v0.32.0) — hotkey conflict prevention: HotkeyDialog `FindConflict`
  (all named boxes + labeled `_toggleBoxes` tuples + reserved built-in
  Ctrl+Alt+P; normalized trim/space/case compare — capture box writes
  canonical combos), save gate keeps dialog open + toast names BOTH features.
  Roadmap standing rule: Action Library/macro hotkeys must reuse the check
  (extract to shared service at the second assignment surface). Docs-only
  sibling: Action Library decoupling entry (named reusable actions, hotkey- OR
  zone-assignable, independently toggleable, migration + SECURITY parity).
- S27 (v0.33.0) — MACRO SUBSYSTEM CORE (the North Star). Models/Macro (compact
  1-letter-JSON MacroEvent; coords normalized 0..65535 of virtual desktop AT
  RECORD TIME → layout-adaptive replay) + Services/MacroService: recording
  installs WH_KEYBOARD_LL + WH_MOUSE_LL (WH_MOUSE_LL2 const) ONLY between
  Start/StopRecording (AbortRecordingHooks always removes them first on stop/
  dispose; procs held vs GC); Record() normalizes via GetSystemMetrics
  SM_*VIRTUALSCREEN; StopRecording trims trailing 300ms (the stop gesture).
  Playback: Task with absolute Stopwatch pacing (no drift accumulation),
  Math.Clamp speed 0.1–10 / repeat 0–1000 (0=∞), Emit() via SendInput
  (mouse ABSOLUTE|VIRTUALDESK re-projection, wheel mouseData, key vk up/down);
  ANTI-CHEAT FindRunningWatched() refuses start + aborts ~every 64 events;
  Ctrl+Alt+X panic Hotkeys.Register only during playback (Unregister in
  finally). Views/MacrosWindow (master toggle → BindHotkeys; record/stop/
  discard; per-row enable/play-stop/hotkey/speed/repeat/rename/delete;
  FindMacroConflict across all app hotkeys+other macros+built-ins).
  Win32 += SendInput/INPUT/InputUnion/MOUSEINPUT/KEYBDINPUT/KBDLLHOOKSTRUCT/
  GetSystemMetrics/public HookProc+SetHook+Unhook+CallNextHookEx+GetModuleHandle.
  VM: MacroSvc field (ctor after Stats; Dispose first), _macroHkIds bound in
  BindHotkeys when MacrosEnabled (play/stop toggle), RunMacro trigger case,
  public RequestRestart(). Zone.ActionMacroId + ZoneAction.RunMacro=5 (clamp
  0..5, id≤40). HotkeyDialog conflict list += Ctrl+Alt+X + every macro.
  Set Action → Run Macro submenu (lists macros, checks current). Settings
  "Maintenance": welcome-tour-re-show checkbox (sets FirstRunCompleted=false)
  + Restart OnTop button (confirm → RequestRestart). SECURITY.md "Macro Module"
  section. **Schema v17 → v18** (MacrosEnabled default OFF ships dark; Macros[]).
  Caps: 50 macros / 20k events / name 100 / hotkey 60, all in Validate.
  HOTFIX v0.33.1: (1) DarkTheme.xaml MC3088 — S25 open-motion Style.Triggers was
  mid-Setters in DarkChromeWindow → moved to style end (XAML: setters grouped,
  triggers after). LESSON for the workflow's UI review: injecting a
  Style.Triggers block must land AFTER all <Setter>s, never between them.
  (2) Macro Emit wheel no longer ORs MOUSEEVENTF_MOVE — wheel scrolls at current
  pos, buttons still absolute-anchor.
- S27 cont. (v0.33.2) — HOTFIX CS0102: HotkeyStealth + _hotkeyStealth defined
  TWICE in AppSettings (pre-existing pair + an S26 duplicate) → removed the S26
  duplicate, kept the one grouped with the other hotkey props. Duplicate-member
  sweep of AppSettings + large service/VM files came back clean (BindHotkeys()
  / BindHotkeys(bool) is a legit overload). LESSON: when a session adds a
  settings field, grep AppSettings for a pre-existing definition FIRST.
- S27 cont. (v0.33.3, docs) — WINDOW MOVER PRIVILEGE COMPARTMENTALIZATION
  (my request). OnTop = one non-elevated process → no OS sandbox between
  features → defense is architectural. New SECURITY.md "Internal Capability
  Isolation" standing rule: single choke-point per capability;
  config-in-not-call-in (no runtime Launch(anyString)/SendInput(anyBuffer) API a
  compromised feature can invoke — powerful ops read only vetted config);
  move≠launch least-privilege split (launch sub-gated, off by default; move-only
  can't escalate); reuse the guarded ShellExecute path; always log+toast; inert
  when disabled. BUILD GUIDANCE: extend RunZoneCommand's single launcher, never
  add a second one. ROADMAP += Window Placement & Launcher feature + ⭐ STANDING
  DESIGN LAW (every non-core feature fully disableable — anti-cheat + user
  autonomy; mirrored in SECURITY.md).
- S27 cont. (v0.33.4, docs) — CONFIG & TRUSTED-DATA INTEGRITY standing rule.
  Threat model: config lives in user %APPDATA% → the real risk is
  config-as-injection-vector / untrusted imports / malformed data, NOT a local
  attacker (who would already own the account). Invariants (several already
  enforced by the 4MB gate + ClampZones + corrupt-fallthrough + backups + schema
  prompt): nothing dangerous auto-executes on load (THE key invariant — powerful
  ops fire only on an explicit gesture); validate-on-the-way-in; re-assert trust
  at load; powerful fields visible; imports untrusted; NO integrity theatre (no
  local-config signing — a local attacker could re-sign). ROADMAP += "Config
  integrity hardening pass" (updater-host re-validation on load, macro-event
  range validation, explicit inert-load regression test, powerful-field-change
  notice).
- S27 cont. (v0.33.5, docs) — SHARED-CONFIG SAFETY (my question: "can we encrypt a
  shared config so it can't carry dangerous commands?"). FINDING:
  encryption/signing REJECTED for tamper-proofing — the recipient must read it
  (no secrecy from them) and a hostile author signs with their own key (proves
  nothing); the danger is CONTENT, not authority. SOLUTION: QUARANTINE ON
  IMPORT — powerful content from outside lands inert (launch/kill/run-macro
  actions → safe default, updater URL blanked, macros imported but not
  hotkey-bound) until the user reviews a verbatim import summary and opts each
  in; safe by construction (inert + visible + reversible via pre-import backup);
  the user is the trust decision.
- S27 cont. (v0.33.6, docs) — TWO CORRECTIONS from my review. (1) PROVENANCE:
  quarantine applies ONLY to explicit outside imports; the app's OWN settings +
  backups are TRUSTED → crash/corruption fallback loads fully active and
  seamless, never re-prompting (recovery must not punish the user for a bug).
  Size + clamp validation still applies to both. (2) CONFIDENTIALITY is real and
  distinct from tamper-proofing: config holds sensitive data — window titles,
  paths, and especially MACRO recordings = raw keystrokes / possible passwords.
  Guidance: minimize stored data + macro UI warns it captures literal keys;
  whole-file confidentiality → BitLocker/EFS; OPTIONAL encryption of only the
  high-risk subset via Windows DPAPI (ProtectedData, per-user, login-tied, no
  stored key, no password theatre). Encryption = YES for privacy, NO for
  authority — never conflate.
- S27 cont. 5 (v0.33.7, docs) — (1) NEW STANDING RULE "DOCUMENT EVERY CHANGE"
  (my request), added to this file + MASTER_PROMPT: every delivery, including
  trivial/docs-only, needs CHANGELOG (+ link ref) + WHATS_NEW + PROJECT_SCOPE,
  and a pre-zip check that csproj / .iss / CHANGELOG head / WHATS_NEW head /
  "current:" all name the same version. WHY: docs-only 0.33.3–0.33.6 skipped
  CHANGELOG (backfilled this turn) AND their PROJECT_SCOPE session lines had
  silently failed to apply — each append was anchored to text from the previous
  (failed) append, and verification printed the version-bump count instead of
  the appended text, masking the whole chain. LESSON: verify the ACTUAL string
  you inserted, never a proxy; and never chain an anchor to a prior unverified
  edit. (2) EXPORT/SHARING CORRECTION: at-rest DPAPI encryption must never block
  sharing — on-disk format ≠ shared format; profiles/layouts/groups/hotkeys/
  settings are never encrypted; EXPORT is a separate deliberate path (decrypt
  locally → portable artifact) with macros excluded by default behind an opt-in
  + literal-keystroke warning; the recipient side still quarantines.
  (3) MULTI-PROCESS ARCHITECTURE analyzed, NOT added pending my decision:
  same-user/same-integrity processes are not isolated (would need
  low-integrity/AppContainer + a broker); OnTop has no OS-level privileges to
  compartmentalize (launch/move/SendInput are ordinary user operations); there
  is no plugin/script/network/binary-parse attack surface; costs = IPC,
  lifecycle, hook ownership, installer/updater complexity; and a separate
  input-synthesis helper resembles cheat-tool architecture (worse for
  anti-cheat, my priority). REVISIT IF plugins/scripting, untrusted
  binary parsing, or a network service is ever added.
- S27 cont. 6 (v0.33.8, docs) — FUTURE CAPABILITY SAFETY GATE (my question: "what
  about future macro scripting/launch/install as attack vectors?"). New
  SECURITY.md standing rule governing ALL future capability growth. Key
  reasoning: the trust boundary is the SOURCE of instructions, not the feature —
  a user-written script is ordinary intent (process boundary protects nothing);
  imported/community scripts are the real new boundary. Rules: (1) bounded VERB
  LIST, never a general-purpose engine with ambient authority — the single
  highest-value decision, worth more than any post-hoc sandbox; (2) if an engine
  is ever embedded it must be strippable (no io/os/process/net/ffi/dynamic
  import; host functions only; wall-clock + instruction budget) — non-strippable
  engines are disqualified; (3) capabilities granted per-macro, off by default,
  verbatim in UI, imported content can NEVER self-grant (quarantine + per-item
  opt-in); (4) NO install capability EVER — never download-then-execute (the
  malware pattern); at most launch a user-chosen local path via the single
  existing launcher; (5) reuse existing choke-points + log/toast + Test Mode
  suppression + anti-cheat refusal/abort + panic stop + inert-when-disabled;
  (6) nothing auto-runs on load/import. MULTI-PROCESS remains OUT — the S27
  analysis survives a scripting future while rules 1-6 hold; REVISIT TRIGGERS:
  non-strippable engine embedded, third-party content running by default, or
  untrusted binary parsing / network service. If revisited, correct form is a
  low-integrity/AppContainer child + broker exposing only the vetted verb list
  (and building that verb list is most of the benefit anyway, per rule 1);
  naive same-integrity process splitting is NOT security. ROADMAP macro End Goal
  remainder bound to the gate.
- S28 Stage A (v0.34.0) — STAGED "FINISH EVERYTHING" SESSION. Rollback net
  first: baseline zip `OnTopWPF_v0.33.8_baseline.zip` + `SESSION_STATE.md` stage
  ledger (resume/rollback contract; MASTER_PROMPT read-order item 0; zip
  contract). CODE: config integrity hardening — `SettingsService.IsLoading`
  inert-load guard on all 5 choke-points; shared `ClampMacros` (event ranges);
  `IsValidUpdateUrl` at load; `LoadWarnings` surfaced by VM; `PowerfulFieldsStamp`
  (SHA-256, schema **18 → 19**, no-op); quarantine (`Models/QuarantineItem`,
  `Views/ImportReviewWindow`, `QuarantineZones/Settings/Macros`, `IsOwnPath`
  provenance) on zone import + outside restores; `ReadSettingsFile` gained the
  size gate + Validate (was skipping both — real gap). `ImportMacrosFile` /
  `ExportMacrosFile` service-side. LESSONS: theme `WarnAmber`/`BgBand` are
  Colors — use the `*Brush` keys from code; CHANGELOG prepends had piled 27
  stray `---` (collapsed) — anchor prepends to the entry heading, not to `---`.
- S28 Stage B (v0.35.0) — MACRO SUBSYSTEM COMPLETION. MacroService: static
  `KnownAntiCheat` list + `MacroGuardKnownAntiCheat`; `TripKillSwitch`
  (guard layer 3: MacrosEnabled=false + BindHotkeys + save + log + center toast,
  behind `MacroKillSwitch`); `ActiveProfileDenies()` (Profile.MacroPolicy
  Allow=0/Deny=1 — refuse Play/StartRecording, VM BindHotkeys skips macro
  binds, SwitchProfile stops playback; **SyncActiveProfile carries MacroPolicy
  across its record replacement**); `Emit(ev, jx, jy, rng)` jitter
  (`Macro.Jitter` 0–20 px → normalized units once per run); actions-only capture
  (`MacroRecordPath` false drops K==0 at stop). App: `BadgeVariant(icon,color)`
  generalizes PausedVariant → `_iconRecording` red / `_iconPlaying` blue,
  priority recording>playing>paused; MacroSvc.StateChanged drives tray + status
  bar; tray "Macros (hk)" item. VM: `HotkeyMacros`/`_hkMacrosId`/
  `MacrosRequested`/`ActiveProfileMacroPolicy`. MainWindow `ShowMacros()`
  public. HotkeyDialog `_tbMacros` row + conflict entry. MacrosWindow: banner,
  options WrapPanel (4 toggles + Export/Import), first-record confirm
  (`MacroWarningAcknowledged`), Jitter button, guard-active status line.
  **Schema 19 → 20** (no-op). Hotkey story: OPTIONAL Macros Window.
- S28 Stage C (v0.36.0) — FEATURES PANEL + MINIMAL MODE + DPAPI. AppSettings
  `StatsEnabled`/`ZoneActionsEnabled`/`StealthWatcherEnabled` (default true) +
  `EncryptMacros` (false); `Macro.EncryptedEvents`. StatsService `Enabled`
  gate on every Bump (VM syncs from cfg at startup + OnCfgPropertyChanged);
  zone trigger refuses advanced actions when off (no fall-through to Exit);
  App watcher tick checks the switch. `MainViewModel.ApplyMinimalMode`.
  SettingsDialog "Features" GroupBox (WrapPanel, bottom margins) + Minimal
  Mode button; whole dialog now inside a work-area-capped ScrollViewer.
  SettingsService: all 4 serialize sites → `SerializeSettings` (seal Events →
  `EncryptedEvents` via `ProtectedData.Protect`, CurrentUser, fixed entropy;
  live objects restored in finally); `UnsealMacros` in Validate before
  ClampMacros (fail → empty+disabled+unbound+LoadWarning). **NEW NUGET DEP**
  `System.Security.Cryptography.ProtectedData` 8.0.0 — first Windows build
  needs restore online. **Schema 20 → 21** (no-op). Hotkey story: NONE
  (settings-panel features). Deferred: close motion.
- S28 HOTFIX (v0.36.1) — first confirmed-build feedback on the 0.33+ line:
  `MainViewModel.MacroSvc` property NEVER DECLARED (v0.33.0 assigned it in the
  ctor only) → ~30 CS0103/CS1061; `LogService._sync` undeclared lock field
  (v0.29.0); `ZoneContextMenuFactory` loop `idx` shadowed method `idx` (v0.29.0)
  → `colorIdx`; CS8602 in HotkeyDialog. LESSON (added to build rules): grep the
  same file for a declaration after any ctor `X = new` assignment — brace/grep
  cross-ref can't see an undeclared member. Confirms v0.33.2 never compiled.
- S28 Stage D (v0.37.0) — FEEDBACK ROUND from my testing (build confirmed clean at
  0.36.1; v0.30–0.36 checklists passed). New: `HotkeyMacroRecord` (default
  BOUND Ctrl+Alt+M) + `MacroService.ToggleRecording`; `Views/MacroStatusWindow`
  (WS_EX_NOACTIVATE|TOOLWINDOW, 200 ms poll of `RecordingEventCount`/
  `RecordingElapsedMs`/`LastSaved`, placement "macrostatus", App
  `SyncMacroStatusWindow` on MacrosEnabled/ShowMacroStatusBar);
  `Views/MacroEditorWindow` (StepRow view over cloned events, delays re-timed
  via Renumber, editor can only remove/retime); `Views/AntiCheatListWindow` +
  `MacroGuardIgnored`; `LowImpactMode` (App `ApplyLowImpact`: BelowNormal +
  intervals; `WindowRuleService.ApplyLowImpact`); `Helpers/WarnTip`; Help TOC
  (`BuildToc`, `W()` amber paragraphs); main-window MACROS band; Welcome
  hotkeys + `VM.RequestMainWindow`. FIXES: accent (mutate shared brush — live),
  kill-switch UI sync (`_master`/`_syncing`), refuse-at-start no longer trips
  the kill switch, `OverlayService.BringOverlaysToFront` on peek/preview,
  MacrosWindow topmost binding + placement. Wording pass (Block Macros In
  Profile / speed % / Wobble / steps). **Schema 21 → 22.** DECISION: no CPU
  affinity option (documented in Help — priority is the correct tool).
  LESSON (tooling): `grep -c … && python` short-circuits on zero matches —
  never chain edits behind a grep exit code.
- S28 Stage E (v0.38.0) — WINDOW PLACEMENT & LAUNCHER. `Models/PlacementRule`
  (Clone), `Services/PlacementService` (FindWindow over GetVisibleWindows w/
  IsOwnWindow skip; ApplyAsync: move via SetWindowPos NOZORDER|NOACTIVATE
  relative to Screen.AllScreens[Monitor], Maximize path restores→moves→SW_MAXIMIZE;
  launch ≤10 s wait ×250 ms; CaptureForeground), `Views/PlacementWindow`
  (placement "placement"; MonitorOneBased converter). VM: `Placements`
  ObservableCollection synced like WindowRules (load + SaveAll), `PlacementSvc`,
  `_hkPlacementId` registered only while PlacementEnabled (rebind on cfg
  change), **`LaunchViaShell` = THE launcher choke-point** (RunZoneCommand
  delegates). Zone: `ApplyPlacement=6`, `ActionPlacementId`, **Clone() now
  copies action payloads (bug since v0.31.0)**. Quarantine blanks launch paths +
  forces PlacementLaunchEnabled=false; stamp includes "P|" lines. Win32 +=
  GetWindowRect/GetAncestor/Layered attrs/SWP_NOZORDER/SW_MAXIMIZE/RECT. Hotkey
  story: OPTIONAL "Apply All Placements". **Schema 22 → 23.** AHK importer
  SCRAPPED (entry kept).
- S28 Stage F (v0.39.0) — ACTION LIBRARY. `Models/LibraryAction`+`LibraryKind`
  (INT enum, 10 kinds, `IsPowerful`, `Summary`), `Views/ActionLibraryWindow`
  (placement "library"; PickFrom ListBox dialog), VM `RunLibraryAction` (switch
  → existing verbs; Kill/Toggle reuse the zone helpers via a throwaway Zone
  carrying the payload), `TogglePin(IntPtr)` extracted from OnHotkeyToggle,
  `TogglePinUnderCursor`, `_libraryHkIds` bound only while ActionLibraryEnabled,
  **`FindHotkeyConflict` = the shared conflict service** (MacrosWindow
  delegates; HotkeyDialog lists library hotkeys). Zone `Library=7` +
  `ActionLibraryId` (Clone, clamps, quarantine, stamp "L|"). Features switch,
  Minimal Mode, Help. Hotkey story: per-action OPTIONAL. **Schema 23 → 24.**
- S28 Stage G (v0.40.0) — SIXTEEN ADDITIONS. Pin extras in VM (`RootOf` via
  GA_ROOT; `SetPinOpacity` toggles WS_EX_LAYERED + LWA_ALPHA, 100% strips
  layering unless click-through; `_clickThrough` set + `RevertClickThrough` on
  Unpin/toggle-off; `ToggleClickThroughUnderCursor` checks the tracked set
  FIRST because WindowFromPoint skips WS_EX_TRANSPARENT windows; `CycleCorner`
  via Screen.FromPoint WorkingArea). Gesture pipeline: `OnMouseButton(btn,x,y)`
  per-button 500 ms/10 px counting, VK_CONTROL via new `Win32.GetAsyncKeyState`,
  `HandleGesture(x,y,g)` filters `z.Gesture == gesture` — OnLeftClick kept as
  alias; App hook forwards WM_L/M/RBUTTONDOWN. `TickProfileRules` runs at the
  TOP of the stealth watcher tick (before its empty-list early return).
  `DisplaySignature` mirrors OverlayService's sig format. DND =
  `ToastService.SuppressRoutine` (never Error). MacroService: countdown loop
  before playback (StateChanged each second), K=10 wait polls
  GetVisibleWindows every 100 ms and RE-BASES `start` so later steps aren't
  late, chaining in the finally on natural finish (`depth` param, ≤5), relative
  replay computes ox/oy once per run from AnchorLeft/Top vs current rect —
  `Emit(ev, jx, jy, rng, ox, oy)`. ClampMacros: K≤10; K=10 requires S, D
  100..120000. New windows: CheatSheet (UniformGrid 2 cols, closes on
  Deactivated), CommandPalette (only ENABLED features contribute), ProfileRules
  (DataGridComboBoxColumn over ProfileNames). Stats bars via Border width ∝
  count. Hotkey story: seven OPTIONAL hotkeys. **Schema 24 → 25.**
- S28 Stage H (v0.41.0) — SECURITY SCOPE RECALIBRATION (my question: "too strict?").
  Keyboard-hook rule → LIFECYCLE (armed-only); `Services/HoldChordService`
  (Sync() installs/removes WH_KEYBOARD_LL from cfg + StealthMode; chords parsed
  by `HotkeyService.ParseHotkey`; modifiers verified via GetAsyncKeyState; any
  modifier key-up ends a hold; hold-pin only pins a window that wasn't already
  topmost so release can't unpin a real pin). VM `PeekHoldRequested` → App →
  `OverlayService.PeekHold`. Global launch gate REMOVED (`PlacementLaunchEnabled`
  deleted; old field ignored by System.Text.Json). DPAPI default ON: field
  default true + schema-26 migration sets true; `Load()` flips false in
  portable mode for fresh/migrated configs only (`_freshConfig`). `Macro.StopKey`
  polled in the play loop. SECURITY.md "What Stealth and Kill can and cannot
  do" = reference wording; Help ⚠ mirrors it. **Schema 25 → 26.** Hotkey
  story: two OPTIONAL hold chords. Intent recorded: keep the verbose paper
  trail so full context can be rebuilt from the docs alone.
- S28 Stage I (v0.42.0) — THE TWENTY. Models: PinScene/PinSceneItem/
  PinMemoryEntry/ProfileSchedule (Start/End minutes, Days bitmask Mon=bit0,
  StartText/EndText/DaysText string views for DataGrid, IsActive handles
  midnight wrap); Zone += CooldownSec/SchedStart/SchedEnd/SchedDays
  (+IsScheduledNow, Clone); MacroEvent.L; LibraryKind.ApplyScene=10 (reuses
  PlacementId for the scene id). VM: `LastPinned`, RememberPin/ApplyPinMemory
  hooked into TogglePin, SaveScene/ApplyScene (match process+title, fallback
  process), PinTimed (per-hwnd DispatcherTimer → Unpin), SendToNextMonitor
  (relative fractions of WorkingArea), `_zoneLastFire` cooldown + schedule +
  hit-flash gates at the TOP of the zone match, profile diff in SwitchProfile
  (`LastProfileDiff`), TickIdleAndSchedules (idle: `_idleFired` edge; schedules
  yield to process rules via `_ruleProfileActive`). MacroService DryRun (skips
  Emit, sets DryStep, StateChanged every 4 steps) + `_pauseFlag` loop with
  `start` re-base. ToastService.History (300, lock). ThemeOptions.ApplyTheme
  mutates the 9 token brushes (same trick as accent). Windows: ConfigHistory,
  ToastHistory, ProfileSchedules. LESSON: light theme over a dark-designed app
  is best-effort — any hard-coded brushes outside the token set will not
  follow. **Schema 26 → 27.** Hotkey story: three OPTIONAL (timed pin, send to
  monitor, macro pause).
- S28 HOTFIX (v0.42.1) — first build of 0.37–0.42: ONE error, CS0246
  `UniformGrid` → needs `using System.Windows.Controls.Primitives`. LESSON (build
  rule): `UniformGrid`, `Popup`, `Thumb`, `Track`, `ToggleButton`, `StatusBar`
  are all in `Controls.Primitives` — add the using whenever code-built UI
  reaches for them. `TESTING.md` added (consolidated D–I sweep) and listed in
  the zip contract.
- S28 Stage J (v0.43.0) — LAYOUT ROUND (my notes after compiling 0.42.1,
  before any functional test). MainWindow Row 3 → 3 columns (4*/3*/4*): Zone
  Actions (first wrap + ZONE DATA + CONFIGURE) | StackPanel{Zone Groups,
  **Macros** GroupBox} | **App** GroupBox (former APP band incl. ProfileCombo);
  Zones GroupBox MinHeight=300. Help TOC → Border chips (hover-invert);
  duplicate "Zone Map" H() merged. MMB-scroll hint in canvas empty text, map
  ToolTip, welcome, Help. `HotkeyRetrySeconds` (5–600, live via
  OnCfgPropertyChanged → timer.Interval). **Schema 27 → 28.** STANDING RULE
  added to the build rules: UI grouping review.
- S28 Stage K (v0.44.0) — FIRST REAL TEST ROUND (3 logs + marked checklist).
  ROOT CAUSES: (1) Desktop Only gate hit our own overlay via WindowFromPoint →
  `Win32.TopWindowAtPoint(pt, skip)` z-order walk skipping `IsOverlayHwnd`;
  (2) ZonesTriggerStealth override → Exit-only + `Zone.AfterAction`
  (`RunAfterAction` after every advanced branch); (3) accent: StaticResource
  brushes FREEZE when a Style seals → ALL `*Brush` refs → DynamicResource
  (script skipped Freezable elements) + `ThemeOptions.SetToken` replaces with
  frozen brushes; (4) overlay z-order: NOTOPMOST→TOPMOST toggle +
  `EnsureFrontTimer` (400 ms, only when fg changed to an own window) + call
  after Refresh; (5) `TrimChordEdges` (orphan ups at start / orphan downs at
  end, auto-repeat safe); (6) DataGrid EditItem crash → `GridEdit.EnsureCommitted`;
  (7) SetWindowPos result checked (SetLastError) — error 5 = elevated target;
  candidate ranking; (8) `SuspendGlobalHotkeys`/`Resume` (BindHotkeys early
  return; HoldChords honors) around HotkeyDialog + new `HotkeyCaptureDialog`;
  live conflict revert in HotkeyDialog (`_allBoxes`, Tag = last good).
  UX: `Helpers/EditorFooter.Wrap/HideCloseButton` on every sub-GUI (JSON
  snapshot → revert); `PlacementRuleDialog` + `ProcessPickerDialog`;
  PlacementRule.DelaySec/LaunchWaitSec; strip lifecycle (`MacrosWindowOpen`,
  `MacroStatusSyncRequested/ToggleRequested`, 4 s linger, `HotkeyMacroStatus`);
  `ClickThroughBorderWindow` + `ClickThroughBorder`; Pinned State column.
  **Schema 28 → 29.** TESTING.md rewritten (legend). LESSONS → build rules.
- S28 (v0.44.1, repo-only) — GitHub readiness: `.gitignore` (excludes real
  config/stats/backups — they can hold macro keystrokes), `.gitattributes`,
  `.editorconfig`, README rewritten. LICENSE deferred to 0.44.2 (I wanted to
  compare license types first). Zip contract += these three dotfiles.
- S28 (v0.44.2, docs + metadata) — LICENSE = GPL-3.0-or-later (verbatim FSF
  text, never edit); README "An AI-developed project" disclosure + License
  section; Help "About & License"; csproj Authors/Copyright/license + Release-only
  `PathMap` (no local build path in release binaries); installer Publisher =
  Godswrath88 + AppCopyright. Every doc and code comment rewritten in first
  person / neutral voice; real name and personal setup details removed;
  HANDOFF.md rewritten. Zip contract += `LICENSE`. Standing rule: never write my
  Windows username, e-mail or machine name into any file. Schema stays 29.
- S28 Stage L pt.1 (v0.45.0) — EMERGENCY STOP (from testing: Pinned Windows
  froze the app and the Exit hotkey with it). `Services/EmergencyStopService`:
  dedicated thread + own message loop owns Exit/Kill (RegisterHotKey with NULL
  hwnd → WM_HOTKEY to that thread); Kill = log + TerminateProcess; Exit = UI
  path with 3 s escalation; 1 s dispatcher watchdog logs stalls ≥ 4 s. VM
  BindHotkeys routes exit/kill through `EStop.Rebind` (Kill armed even while
  suspended). Pinned Windows scan → Task.Run + dispatcher apply + timing logs;
  `Win32.GetProcessName` → OpenProcess/QueryFullProcessImageName;
  RegisterHotKey SetLastError. Tray "Kill Now". Schema stays 29. RULE: any
  future stop/exit path must not depend on the UI thread being alive.
- S28 (v0.45.1, docs-only) — SECURITY.md "Reporting a vulnerability" section
  pointing to GitHub private vulnerability reporting (now enabled on the public
  repo). No code change. Schema stays 29. RULE: never record a known, unfixed
  weakness in a public file; keep it in a private advisory until the fix ships.
- S28 (v0.45.2, docs + GitHub templates only) — TESTING.md →
  `docs/testing/CHECKLIST.md` (by feature, stable IDs, smoke test, all marks
  kept); `docs/TROUBLESHOOTING.md` (user-facing); `.github/ISSUE_TEMPLATE/`
  (Bug report, Test pass, config); labels retest/needs-log/blocker/test-pass;
  open retest items = issues #1–#14. 0.45.1 compiled clean on the Windows
  machine. Schema stays 29. RULE: failures and retests live in issues; never
  paste a whole log into a public issue (window titles are private).
- S28 (v0.45.3, hotfix) — `EditorFooter.Wrap` detaches the window's content
  before re-parenting it (every footer window crashed on open since 0.44.0);
  the diagnostics zip closes each entry before creating the next. Schema
  stays 29. RULE: a helper that re-parents an element must detach it first.
  "Do this first" messages from clicks in OnTop's own windows use
  `Views/NoticePopup` (fade in/out, never takes focus, duration =
  `NoticeSeconds` 1–60, `HintPopupsEnabled` off = toast instead). RULE: a
  scrolling window keeps its Save/Close buttons docked outside the
  ScrollViewer. RULE: hotkey and background paths use
  toasts, not hint popups. RULE: "do this first" hints never need a click to
  get out of. The user-facing name of `Zone.ShowDot` is "Overlay".
