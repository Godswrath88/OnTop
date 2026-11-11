# OnTop — Project Scope (design contract)

What OnTop is, the requirements that must never regress, the hard-won build
rules, and the architecture map. The development workflow (branches, PRs,
versions, releases, testing) is in [`CLAUDE.md`](../../CLAUDE.md) at the repo
root. Security rules are in [`SECURITY.md`](../../SECURITY.md) and outrank
everything here. The per-session history up to v0.45.3 is archived in
[`docs/history/SESSION_HISTORY.md`](../history/SESSION_HISTORY.md).

Update this file in the same PR whenever a requirement, convention or the
architecture changes. It is a description of how things ARE, not a log.

---

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
  is AI-developed; `CLAUDE.md` is the one document written as instructions to
  the AI assistant.
- **The stop must never depend on what it stops (S28 L).** Exit and Kill are
  delivered on the e-stop thread (`EmergencyStopService`), not the WPF window.
  Never register them through `HotkeyService`; never add a UI-thread wait to the
  Kill path; anything new that can block the UI (foreign-process queries, big
  scans) runs off-thread with a timing log line.
- **License is GPL-3.0-or-later.** `LICENSE` is verbatim FSF text — never edit
  it; keep it at the repo root and in every release; don't add a
  license-acceptance page to the
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
- Builds are checked: `dotnet build OnTop/OnTop.csproj` runs locally (the
  development PC has the .NET SDK) and in CI on every PR. Treat a warning as a
  bug. Runtime behavior is still only confirmed by my own testing.
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

## Feature Inventory (shipped — details in CHANGELOG.md and docs/history/WHATS_NEW.md)

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

## Rules learned the hard way (collected from the session history)

Each of these came from a real bug or near-miss; the archived history has the
story behind it.

- **Stop paths:** no stop/exit path may depend on the UI thread being alive
  (Exit/Kill live on the e-stop thread; Kill = log line → TerminateProcess).
- **Security disclosure:** never record a known, unfixed weakness in a public
  file; keep it in a private advisory until the fix ships.
- **Testing records:** failures and retests live in GitHub issues; never paste
  a whole log into a public issue (window titles are private).
- **WPF re-parenting:** a helper that re-parents an element must detach it
  from its current parent first.
- **Settings schema:** every new settings field bumps
  `SettingsService.CurrentSchemaVersion` with a documented (even no-op)
  migration. Defaults fill in either way, but without the bump an older build
  saves over the config and drops the field.
- **Hotkey ids:** `HotkeyService` ids climb and wrap at 0xBFFF, skipping live
  ones; never hand out the lowest free id (a stale id or a queued WM_HOTKEY
  would reach the new owner). Id fields are reset to -2 when released.
  `Register` returns -1 = in use (retried), -3 = invalid combo (one toast per
  real bind, never retried).
- **Logging:** retry ticks log at DEBUG only.
- **File writes:** writers that replace a user-visible file build a temp file
  and move it into place.
- **Layout:** a scrolling window keeps its Save/Close buttons docked outside
  the ScrollViewer.
- **Hints vs toasts:** "do this first" messages from clicks in OnTop's own
  windows use `Views/NoticePopup` (fades in and out, never takes focus,
  `NoticeSeconds` 1–60, `HintPopupsEnabled` off = toast). Hotkey and
  background paths use toasts. A hint never needs a click to get out of.
- **Wording:** the user-facing name of `Zone.ShowDot` is "Overlay".
- **UI layout review** after any UI change: fixed widths/heights new content
  can outgrow, WrapPanels nested in WrapPanels (the inner one never wraps),
  items without bottom margins in wrapping containers, window Min/Default
  sizes vs new content, long labels vs fixed columns. Any new extension-method
  call → confirm its namespace is imported.
