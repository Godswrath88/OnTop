# OnTop — Security Posture

**Security is priority #1 — above features, above convenience.** OnTop installs a
global input hook, manipulates other applications' windows, and could one day ship
to a large user base. That combination means a vulnerability here could be abused to
harm the user or piggyback on OnTop to attack the system. Every change is reviewed
against this document; anything that can't be made safe is not shipped.

This file is the contract. It is READ and RE-CHECKED every session (see the
per-session Security Review Checklist at the bottom), and updated whenever the
attack surface changes.

---

## Threat model (what we defend against)

1. **Config tampering / malicious config files.** `settings.json` lives in
   `%APPDATA%\OnTop` — a location other user-mode programs can write. A hostile or
   corrupted config must never let an attacker execute code, escape the config
   directory, exhaust memory, or crash OnTop into an exploitable state.
2. **Privilege abuse / unwanted elevation.** OnTop must run with the *least*
   privilege that works. It must never silently elevate, never relaunch itself
   elevated, and never perform an action on behalf of an unelevated caller that
   requires elevation.
3. **Foreign-window / foreign-process abuse.** OnTop enumerates and re-orders other
   applications' windows (topmost). It must never read or write another process's
   memory, never inject code or DLLs, and must treat every foreign HWND as
   untrusted and possibly-stale.
4. **Input-hook abuse.** The low-level mouse hook sees system-wide input. It must
   only ever *observe* for zone detection — never log keystrokes, never persist
   input, never expose it. Stealth unhook must always be reachable.
5. **Code/command injection from data.** No value that originates from a file, a
   window title, a process name, or any other untrusted source is ever passed to a
   shell, `Process.Start`, a format string as a format, SQL, or a code path that
   can execute it.
6. **Instance hijacking.** A second process must not be able to impersonate or
   drive the running instance.
7. **Supply chain.** Minimal dependencies; no network calls; no telemetry.

Out of scope (documented, not defended): an attacker who is already an
Administrator on the machine, or who can modify the OnTop binary itself — at that
point the machine is already compromised.

---

## Standing security rules (never regress)

### Privilege & elevation
- **Never auto-elevate. Never relaunch elevated. Never add a UAC manifest that
  requests `requireAdministrator` or `highestAvailable`.** OnTop's manifest
  requests `asInvoker` only.
- If OnTop *happens* to be run elevated by the user, it must not use that elevation
  to do anything it wouldn't do unelevated, and should behave identically.
- No feature may depend on elevation. If a future feature seems to need it, it is
  redesigned or dropped, not elevated.

### Config & file I/O (`Services/SettingsService.cs`)
- **Size cap**: refuse to parse any config/import file larger than a hard limit
  (currently 4 MB) — reject before deserializing, to bound memory.
- **Strict deserialization**: reflection-based JSON that *ignores unknown fields*
  (forward-compatible) and validates/clamps every value on load (numeric ranges,
  string lengths, list counts). Never trust a value from disk.
- **Path safety**: all paths are built from `Environment.GetFolderPath(ApplicationData)`
  and fixed subfolder names — never from config content. Never open a path derived
  from untrusted input. Reject reparse points/junctions on our own directories.
- **Atomic writes**: write to a temp file in the same directory, then move into
  place, so a crash mid-write can't corrupt or truncate the live config.
- **Fail safe, not silent**: a corrupt/newer/oversized config falls back to a clean
  default AND surfaces a plain-language prompt (Continue with defaults / Cancel) —
  the user is never silently reset, and a good file is never overwritten by defaults
  without consent.
- **No downgrade clobber**: an older build must not overwrite a config written by a
  newer build. Newer configs are loaded best-effort (unknown fields ignored), not
  discarded.

### Foreign windows / Win32 (`Helpers/Win32.cs`, pins + rules)
- Treat every foreign HWND as untrusted: re-validate with `IsWindow` immediately
  before any `SetWindowPos`; tolerate the window vanishing mid-operation.
- **Only ever call `SetWindowPos` (Z-order) on foreign windows.** Never
  `WriteProcessMemory`, `ReadProcessMemory`, `CreateRemoteThread`, DLL injection,
  or `SendMessage`/`PostMessage` with attacker-influenced payloads.
- Never act on OnTop's own windows destructively (unpin/close) via foreign-window
  code paths — our GUI windows are explicitly excluded.

### Input hooks (rule REWRITTEN S28 Stage H — the real rule is LIFECYCLE, not "never")
- The zone mouse hook observes button-downs for zone detection ONLY. It never
  captures keystrokes, never writes input to disk, never transmits it.
- **Keyboard hooks are allowed only inside an opt-in feature's ARMED window**:
  installed when the feature is on and actually needed (macro recording between
  Start/Stop; hold chords while the switch is on AND a chord is set), removed
  the instant that stops being true, removed by Stealth, removed on exit and
  dispose. A keyboard hook must never persist as a background default. The
  S20 refusal of a keyboard hook for press-to-peek was over-broad — the macro
  recorder already followed this lifecycle rule and it holds up; hold-to-peek /
  hold-to-pin now use it too (`Services/HoldChordService`).
- A keyboard hook compares keys against configured chords (or records into the
  macro buffer while recording). It never logs, stores, or forwards keystrokes
  otherwise.
- Stealth (full unhook of EVERYTHING — mouse, recorder, hold chords) must always
  be reachable by hotkey and tray, even if the GUI is unreachable.

### What Stealth and Kill can and cannot do (honesty rule, S28 Stage H)
- Stealth removes every hook; Kill removes the process. Both stop the evidence
  FROM THAT MOMENT ON. Neither is retroactive: kernel anti-cheat can log hook
  installs/unhooks, process launches, module names and injected-input flags
  across the whole session and send them to the vendor; bans are decided later
  from that history. The stealth watcher polls every 5–15 s, so a game that
  scans during its own startup can see OnTop before OnTop sees the game.
- `SendInput` output carries the injected flag any low-level hook can see —
  synthesized input cannot be made retroactively un-synthesized. The macro
  guard reduces the odds; it cannot make a ban impossible.
- Pinning-only OnTop (Minimal Mode) does what every "always on top" tool does
  (SetWindowPos) and is as benign as it gets. The risk is macros, not pinning.
- Therefore every warning about anti-cheat stays BLUNT and no UI text may claim
  safety. This paragraph is the reference wording.

### Data → execution boundary
- No untrusted string is ever used as a format string, shell command, path,
  process name to launch, or code to evaluate.
- Any future "run command / run macro" action is OFF by default, per-item opt-in,
  clearly warned, and never populated from an imported/shared config without an
  explicit confirmation step.

### Instance & dependencies
- Single-instance via a named mutex; a second instance defers to the first and does
  not drive it.
- No network I/O, no telemetry, no auto-update download-and-run. Dependencies kept
  minimal and pinned.
- **Amended exception — STAGE ONE in effect (v0.28.0):** the app may perform
  exactly one network operation: an anonymous HTTPS GET of the release manifest
  (Services/UpdateService), and ONLY when the user has configured a manifest URL
  (empty = fully dormant, the default). Constraints enforced in code: HTTPS
  scheme required on the manifest AND release URLs, 64 KB response cap, 10 s
  timeout, strict JSON, downgrade/equal refusal, never blocks or nags on
  failure, every event logged under "Updates" — and the app itself downloads
  and executes NOTHING (the "Open Download Page" choice is a browser handoff).
  **Stage two** (in-app download + SHA-256 + Authenticode verification + run)
  remains prohibited until code signing ships, per the original constraints
  below, which continue to bind it: signed manifest, hash + signature checks
  before execution, user-visible install only, off-switch. Audit any change to
  the update path against this paragraph.

---

## Per-Zone Actions (the data→execution boundary, S26)
Zones can now run a command, kill a process, or toggle a window. This is
user-authored configuration acting as user intent — like a shortcut — but it IS
config-driven execution, so these rules bind every current and future action:
- **User-authored only.** Actions execute exactly what the user typed into the
  zone's own settings; nothing is fetched, templated, or expanded by us. The
  shell (ShellExecute, non-elevated, current user) does only what Win+R would.
- **Nothing hidden.** The full command/process is shown VERBATIM in the Set
  Action menu; every execution is logged (Zones category, full command) AND
  toasted. There is no silent path.
- **Caps + clamps.** ActionCommand ≤ 500 chars, ActionProcess ≤ 100, enum
  range-checked — enforced in the same shared ClampZones as every import/load.
- **Protected-process refusal.** Kill refuses explorer/winlogon/csrss/svchost/
  lsass/services/system/smss/wininit/dwm by name, with a logged warning.
- **Test Mode suppresses actions** — placement can always be verified safely.
- Auto-Stealth's watcher only READS the process list (names) — it never touches
  other processes.

## Config & Trusted-Data Integrity (standing rule, S27)
Config lives in the user's own %APPDATA% (or the portable Data folder), so the
threat model is precise: an attacker with arbitrary write access to that folder
already owns more than OnTop. The real risks are (a) a manipulated config acting
as an INJECTION VECTOR into OnTop's powerful features, (b) untrusted data
crossing the trust boundary (imports, shared profiles/configs), and (c)
malformed data causing crashes or unbounded resource use. Rules:

- **Nothing dangerous auto-executes on load.** Loading or importing a config
  NEVER launches a program, plays a macro, kills a process, moves a window, or
  makes a network call. Powerful actions fire ONLY on an explicit user gesture
  (a zone trigger, a hotkey, a button) — parsing config is inert. A config
  therefore cannot be an attack the moment it is read.
- **Validate on the way IN, every load and import.** Already enforced and to be
  kept comprehensive: size gates (4 MB), the shared `ClampZones` (string caps,
  collection caps, enum-range checks), corrupt→next-candidate fallthrough,
  rotating backups, and the newer-schema conflict prompt. Any new powerful field
  joins this validation the session it is added — clamp/range-check it in
  Validate, never trust the raw value.
- **Re-assert trust constraints at load, not just at use.** Fields that gate a
  privileged capability are re-checked when read, not only when exercised:
  the updater manifest URL must be https (empty = dormant); watched-process and
  action strings are clamped; macro events are count- and range-capped. A
  tampered value is neutralized on load, so a later "use" can't be tricked.
- **Surface changes to powerful fields.** Because actions/launches/macros are
  shown verbatim in their editors and log+toast on every run, a config that was
  altered to point a zone at malware is VISIBLE the moment it fires (and
  inspectable in the editor beforehand) — never silent. Keep it that way for
  every new powerful field.
- **Treat imports as untrusted.** Imported zone files / shared configs get the
  same validation as loaded config PLUS the pre-import auto-backup, so a hostile
  import is bounded and reversible. Never widen import to accept a field that
  bypasses Validate.
- **No integrity theatre.** We do NOT sign or checksum the local config to
  "prove" it is untampered — a local attacker could re-sign it, so that adds
  complexity without real protection. The defense is the inertness + validation
  above, which holds regardless of how the file got there.

### Provenance: trusted (own) vs untrusted (imported) — they are NOT treated alike
A hard line separates OnTop's OWN data from data crossing in from outside, so
safety measures never punish the user for the app's own recovery:
- **TRUSTED — loaded fully active, never quarantined:** the app's own
  settings.json, its rotating backups, and the pre-import auto-backups it wrote.
  A crash/corruption **fallback to a backup is a trusted recovery** — it loads
  seamlessly with every action/macro/setting intact. The user is NOT re-prompted
  to re-approve their own workspace; that would punish them for a bug. (This is
  the answer to "wouldn't quarantine be annoying after a crash?" — yes, which is
  why recovery from own data is explicitly exempt.)
- **UNTRUSTED — quarantined on import:** only a file the user EXPLICITLY imports
  or a config obtained from elsewhere. This is the sole case the quarantine
  below applies to.
All the size/clamp/range validation applies to BOTH (malformed own-data still
shouldn't crash or balloon memory) — but the powerful-field QUARANTINE applies
ONLY to untrusted imports.

### Shared / imported configs — validate content, don't trust authority
A config received from someone else is the one case where extra defense is
genuinely warranted. Encryption/signing does NOT help here and is explicitly
rejected: the recipient must read the file (so it can't be secret from them),
and a hostile author would simply sign/encrypt with their own key — the app
cannot distinguish "trusted friend" from "attacker," so a signature proves
nothing about safety. The danger is never the file's *authority*; it is the
file's *content* (a RunCommand pointing at malware, a destructive macro, a
redirected updater URL). Therefore, when a config or partial config is IMPORTED
or otherwise crosses in from outside (not the app's own saved file):
- **Quarantine powerful fields on import.** Zone actions that launch/kill/run a
  macro, macro definitions, the updater URL, launch paths, and watched-process
  lists do NOT arrive active. They import in a DISABLED/neutralized state
  (e.g. action reset to a safe default, updater URL blanked, macros imported
  but not hotkey-bound) until the user explicitly reviews and re-enables each.
- **Show a review summary.** On import, present exactly what powerful content
  the file contains — every command string, macro (event count/duration), URL,
  and launch path — verbatim, and require an explicit per-item opt-in before any
  of it can fire. Silent activation is never allowed.
- **Everything else already applies:** the same size gates, ClampZones, enum
  range checks, macro caps, and a pre-import auto-backup so the import is
  bounded and reversible.
This makes a shared config safe by CONSTRUCTION: even a maliciously crafted one
lands inert, fully visible, and reversible — the user is the trust decision, not
a signature the app can't verify.

### Confidentiality vs tamper-proofing — a real distinction (privacy)
Encryption was rejected above for TAMPER-PROOFING (a hostile author just signs
with their own key). But CONFIDENTIALITY is a separate, legitimate concern, and
the config genuinely can hold privacy-sensitive data:
- pinned/auto-pin **window titles** (leak which apps, documents, or sites the
  user works with), watched **process names**, **command strings** and launch
  paths (can contain the username or project paths), and especially
- **macro recordings**, which capture raw keystrokes — potentially including
  **typed passwords or private messages**. This is the sharpest privacy risk in
  the whole app.
Guidance:
- **Minimize what is stored.** Prefer not to persist sensitive content at all:
  e.g. sanitize/scrub obvious secrets, and make clear in the macro UI that
  recordings capture literal keystrokes (so users don't record a password field).
  Least-data-at-rest beats encrypting sensitive data we didn't need to keep.
- **Confidentiality is the OS's job by default.** Full-disk/user-profile
  protection (BitLocker, EFS) already covers the on-disk file and is the right
  layer — the app should not roll its own crypto as the primary defense.
- **Optional at-rest encryption is reasonable for the sensitive SUBSET**, if
  added: use the platform key store (Windows DPAPI, `ProtectedData`, per-user
  scope) to encrypt specifically the high-risk parts (macro event streams,
  maybe command strings) — NOT a hand-rolled cipher, NOT a password the app
  stores. DPAPI ties decryption to the user's Windows login, so a copied file is
  useless on another account. This defends confidentiality (a leaked/synced/
  backed-up file doesn't expose keystrokes) WITHOUT the false tamper-proofing
  promise.
- **At-rest encryption must NEVER block sharing — EXPORT is a separate path.**
  DPAPI data is per-user and undecryptable by anyone else, so the ON-DISK format
  must not be the thing that gets shared. Sharing works because export is a
  deliberate, distinct operation: the app decrypts locally (the same Windows
  login can) and writes a plain, portable artifact. Requirements:
  - profiles, zone layouts, groups, hotkeys, and settings are NOT sensitive and
    are NEVER encrypted — sharing them with friends or publishing them online is
    completely unaffected;
  - only macro keystroke streams (and optionally command strings) are ever
    encrypted at rest;
  - exports EXCLUDE macros by default, with an explicit opt-in checkbox and a
    clear warning that recordings capture literal keystrokes and may contain
    passwords or private text — informed consent exactly when it matters;
  - the recipient side is unchanged: an imported file is untrusted and goes
    through the normal quarantine.
  Net: users can freely share and publish profiles/layouts; the only friction is
  a deliberate, warned opt-in for the one category that genuinely deserves it.
- **Never a security theatre password prompt.** No app-managed "config password"
  — it would have to store the key to auto-load, defeating itself.
Net: encryption is a YES for privacy of the sensitive subset (via DPAPI, opt-in,
scoped), and a NO for tamper-proofing/authority. The two must not be conflated.

## Future Capability Expansion — the safety gate (standing rule, S27)
Applies to ANY future capability added to macros or elsewhere: scripting,
conditional logic, launching, installing, file or network access. The rule is
that new power must arrive through this gate, not around it. **The trust
boundary is the SOURCE of the instructions, not the feature's name:** a script
the user wrote themselves is ordinary user intent (same category as a zone Run
Command); a script that arrived from a shared pack, a community library, or a
downloaded profile is untrusted input and is what these rules exist for.

**1. No general-purpose script engine with ambient authority.** Prefer a bounded
VERB LIST over a language — e.g. wait, move mouse, click, send keys, set window
position, run a NAMED pre-approved action. A verb list has nothing to escape to,
because no file/network/process reach exists in the first place. This single
decision provides more protection than any sandbox added afterwards.

**2. If an engine is ever embedded anyway,** it must be one that can be stripped:
remove/deny the standard library's io, os, process, network, ffi/native-interop,
and dynamic require/import; expose ONLY explicit host functions implementing the
verb list; enforce a wall-clock and instruction budget so a hostile script can't
hang or spin the app. An engine that cannot be stripped is disqualified.

**3. Capabilities are granted, never assumed.** Powerful verbs (launch, file
write, network, anything beyond input synthesis + window placement) are OFF by
default, granted per-macro by explicit user action, shown verbatim in the UI,
and **content that arrived from outside can never self-grant** — imported
scripts land inert under the existing quarantine and require per-item opt-in.

**4. NO install capability. Ever.** OnTop must never download-then-execute, nor
run an installer it fetched. The malware pattern is exactly "fetch remote
content, run it." At most, LAUNCH a local path the user explicitly chose,
through the single existing launcher choke-point (Internal Capability Isolation
above) — no second launcher, no runtime-assembled command.

**5. Reuse the existing choke-points and guarantees.** Any new capability routes
through the audited launcher / input path rather than adding a parallel one;
logs and toasts on every execution; obeys Test Mode suppression; obeys the
anti-cheat watched-process refusal and mid-run abort; stops on the panic key;
and is fully inert when its feature toggle is off (Feature Disableability).

**6. Nothing new auto-runs.** The inert-load invariant extends to scripts: no
script executes on load, on import, or on startup unless the user explicitly
configured that specific script to a specific trigger.

**When multi-process WOULD become justified.** Single-process was chosen
deliberately (see PROJECT_SCOPE: same-user/same-integrity processes are not
isolated, OnTop holds no OS-level privilege to compartmentalize, and a separate
input-synthesis helper resembles cheat-tool architecture — worse for anti-cheat).
That reasoning survives a scripting future ONLY while rules 1–6 hold. Revisit if
any of these become true:
  - a non-strippable general-purpose engine is embedded,
  - third-party / community content runs by default rather than by explicit
    per-item opt-in,
  - OnTop begins parsing untrusted binary formats or exposing a network service.
If revisited, the correct form is NOT "two processes" but a **low-integrity or
AppContainer child holding no capabilities, talking to a broker that exposes
only the vetted verb list** — note that building the vetted verb list is most of
the benefit, and it is required by rule 1 regardless. Naive same-integrity
process splitting provides no isolation and should never be treated as security.

## Internal Capability Isolation (standing rule, S27)
OnTop is a single non-elevated process, so features cannot be sandboxed from
each other by the OS — a compromised feature runs with the same rights as any
other. The defense is architectural, and these rules bind every powerful
capability (process launch, input synthesis, window manipulation, network):
- **One choke-point per capability.** Each powerful operation has exactly ONE
  internal implementation (e.g. all process-launching goes through a single
  audited method), so there is one place to reason about and one place to
  guard — never several parallel launchers/senders.
- **Config-in, not call-in.** Powerful operations take their inputs from
  validated user config, NOT from a public runtime API other features can call
  with arbitrary arguments. There is no `Launch(anyString)` or
  `SendInput(anyBuffer)` surface a compromised feature can invoke at runtime;
  the capability reads only vetted, clamped, user-authored data.
- **Least privilege by default.** The lower-risk half of a capability is
  separated from the higher-risk half and gated independently (e.g. moving a
  window vs launching a program) so a feature/rule gains only what it needs and
  cannot escalate.
- **Always visible.** Every powerful operation logs (with its target) and
  toasts; no silent execution path exists, so misuse surfaces immediately.
- **Inert when disabled.** A disabled feature installs/polls/registers nothing
  (ties into Feature Disableability below).
These are the same principles the Macro Module and Per-Zone Actions already
follow; new powerful features must be audited against this list.

## Feature Disableability (standing rule, S27)
The core pinning engine is the only always-on component. Every other feature
must be fully disableable — with a real off-switch that makes it inert (no
hooks, no hotkey bindings, no network, no execution), defaulting to a safe
state. This is both an anti-cheat/compatibility guarantee and a user-autonomy /
user-security guarantee: the user controls their own attack surface and can
reduce OnTop to pure pinning at will. Every feature review verifies its
off-switch actually makes it inert, not merely hidden. Macros already model
this (master toggle, ships dark).

## Macro Module (S27 — the most privileged feature; audit every change here)
Recording captures ALL input; playback synthesizes input. The rules that make
that acceptable, enforced in `Services/MacroService`:
- **Master toggle, OFF by default (ships dark).** While off: no hooks, no
  hotkey bindings, no recording, no playback — the module is inert code.
- **Hooks exist ONLY while recording.** The WH_KEYBOARD_LL + WH_MOUSE_LL pair
  is installed in StartRecording and removed in StopRecording/Dispose — never
  at any other time. The pinning core's own mouse hook is untouched by this.
- **Keystroke content never leaves the user's config.** Recordings are stored
  only in settings.json; the log records event COUNTS and durations, never
  keys. Diagnostics bundles therefore include macro content only because they
  include settings.json — the user's own file, user-sent.
- **Playback is user-initiated only** (button, the macro's hotkey, or a zone
  the user configured) and never elevated; SendInput as the current user.
- **Anti-cheat guard:** playback REFUSES to start while any Stealth watched
  process runs, aborts mid-run if one appears, and Test Mode suppresses
  zone-triggered playback like every action.
- **Panic stops, always:** Ctrl+Alt+X (registered only during playback —
  reserved in the conflict check), the macro's own hotkey re-press, the Stop
  button, master-toggle off, and the kill switch.
- **Caps:** ≤50 macros, ≤20,000 events each, names ≤100, hotkeys validated
  through the one-combo-one-feature check.

## Config Integrity Hardening — IMPLEMENTED (S28, v0.34.0)
The "Config & Trusted-Data Integrity" and "Shared / imported configs" rules
above are now code, not just doctrine:
- **Inert-load guard** — `SettingsService.IsLoading` is TRUE during every
  Load / ReadSettingsFile / ImportZonesFile / ImportMacrosFile. The execution
  choke-points (`MainViewModel.RunZoneCommand` / `KillZoneProcess` /
  `ToggleZoneWindow`, `MacroService.Play` / `StartRecording`) refuse + log an
  ERROR + toast if reached while loading. This is the standing "nothing executes
  on load" regression test in runtime form; it must never fire.
- **Macro event range validation** — `SettingsService.ClampMacros` (shared by
  Validate AND macro import): kind 0..9, coords 0..65535, wheel ±12000, VK
  1..254, non-decreasing T ≤ 24 h; invalid events are DROPPED, not reinterpreted.
- **Updater URL re-validated on load** — non-https / unparseable → blanked +
  warned (`IsValidUpdateUrl`).
- **Restore path closed** — `ReadSettingsFile` now size-gates + Validates (it
  previously skipped both — real gap found and closed S28).
- **Powerful-field change visibility** — `AppSettings.PowerfulFieldsStamp`
  (SHA-256 of zone actions/commands/processes, macro bindings, updater URL,
  watched list) written on every save; mismatch on load → one toast + Warn.
  Explicitly NOT an integrity signature (no integrity theatre).
- **Import quarantine + review** — `QuarantineZones` / `QuarantineSettings` /
  `QuarantineMacros` neutralize powerful content from OUTSIDE files;
  `Views/ImportReviewWindow` shows each item verbatim with an UNCHECKED opt-in.
  Provenance: `IsOwnPath` (under ConfigDir) = trusted (own backups, crash
  recovery — never re-prompted); anything else = outside.

## Macro Module — Stage B additions (S28, v0.35.0)
- **Guard layer 1 widened**: `MacroService.FindRunningWatched` now also checks
  the built-in `KnownAntiCheat` name list (behind `MacroGuardKnownAntiCheat`,
  default ON). A false positive only refuses a macro.
- **Guard layer 3 — kill switch** (`TripKillSwitch`, behind `MacroKillSwitch`,
  default ON): a guard hit at start or mid-run flips `MacrosEnabled` OFF, rebinds
  (unbinding every macro hotkey), saves, ACTION-logs, and toasts at screen
  center. Re-enabling is a deliberate human act in the Macros window.
- **Per-profile policy** (`Profile.MacroPolicy`, DENY = 1): no macro hotkey
  binds, Play/StartRecording refuse, playback stops on switch. Carried across
  `SyncActiveProfile`'s record replacement (would otherwise be lost).
- **Always-visible indicator**: tray badge (red recording / blue playing), tray
  tooltip, status bar — the user is never unaware synthesis is armed/active.
- **Literal-keystroke warning**: persistent banner + a one-time blunt confirm
  before the first recording (`MacroWarningAcknowledged`).
- **Export/Import**: export is a separate deliberate path with a plain-text
  warning; import is size-gated, `ClampMacros`-validated, ids regenerated,
  QUARANTINED (disabled + hotkey blanked) behind the Import Review, and any
  restored hotkey is conflict-checked again before binding.
- **Jitter** is applied only to mouse coordinates at emit time, clamped to the
  normalized space — it cannot change WHAT is sent, only where by ≤ 20 px.
- Nothing here adds a dependency, a network call, or a new execution path; the
  single SendInput site in `Emit` is unchanged in kind.

## Feature Disableability + Confidentiality — IMPLEMENTED (S28, v0.36.0)
- **Features panel** (Settings → Features) exposes one switch per non-core
  feature; new switches `StatsEnabled`, `ZoneActionsEnabled`,
  `StealthWatcherEnabled` gate `StatsService.Bump*`, the advanced-action branch
  of the zone trigger (refuse + toast, never fall through to Exit), and the
  5 s stealth watcher tick. `MainViewModel.ApplyMinimalMode` flips all eight.
- **At-rest confidentiality** (`EncryptMacros`, default OFF): macro event
  streams are sealed with Windows DPAPI (`ProtectedData`, CurrentUser scope,
  fixed app entropy) at every serialize site (`SerializeSettings` — Save,
  SaveNoBackup, SaveDraft, WriteBackup) and unsealed in Validate. Live objects
  are never left sealed. A seal that fails to open leaves the macro EMPTY +
  DISABLED + unbound with a load warning. Zones/profiles/hotkeys/settings are
  never encrypted; `ExportMacrosFile` writes live (plain) events. This is
  PRIVACY, not authority — it proves nothing about who wrote the file.
- **New dependency**: NuGet `System.Security.Cryptography.ProtectedData` 8.0.0
  (Microsoft-published; DPAPI wrapper; no network, no native code of its own).
  Justified: the only supported way to reach DPAPI from .NET 8 without P/Invoke.

## Stage D notes (S28, v0.37.0)
- `MacroEditorWindow` can only DELETE steps or change DELAYS — it cannot create
  a step or change a step's kind/key/position, so the recorded verb set is the
  ceiling (safety-gate rule 1 holds).
- `MacroStatusWindow` installs no hooks; it polls three properties 5×/s.
- Kill switch now fires only on a mid-run APPEARANCE; a refusal at start is
  already safe and no longer disables the module (found in testing: persistent services tripped it).
- `MacroGuardIgnored` weakens guard layer 1 per entry by explicit user choice;
  the window carries an amber warning and logs every change as an ACTION.
- Low Impact Mode lowers priority only (no affinity, no hook changes).
- No new dependencies, network paths, or Process.Start sites.

## Stage E + F notes (S28, v0.38.0–0.39.0)
- **Single launcher choke-point** `MainViewModel.LaunchViaShell` — zone Run
  Command, placement launch, and library RunCommand all route here. It refuses
  during config load and in Test Mode, and ACTION-logs + toasts every call.
  `Process.Start` sites: still 4 (restart, this launcher, open-folder, updater
  browser handoff).
- **Move ≠ launch**: `PlacementService` moves via SetWindowPos/ShowWindowAsync
  only; launch requires `PlacementLaunchEnabled` AND `rule.LaunchEnabled` AND a
  non-empty path. Imports blank paths and force the sub-gate off.
- **Action Library adds no verbs** — `RunLibraryAction` is a switch over
  existing guarded calls. Powerful kinds are quarantined on import; the feature
  switch never arrives on from an import.
- `Zone.Clone()` bug fix means profile snapshots now carry action payloads —
  which is correct, and also why `QuarantineSettings` walks profile zones.

## Stage G notes (S28, v0.40.0)
- Pin extras touch foreign windows via `SetWindowLong` (ex-style bits
  WS_EX_LAYERED / WS_EX_TRANSPARENT), `SetLayeredWindowAttributes` (alpha) and
  `SetWindowPos` (position) — still geometry/style/z-order only; never input,
  never memory; `IsOwnWindow` excluded everywhere. Click-through is tracked and
  reverted on every unpin path so OnTop cannot leave a window unclickable.
- The hook now forwards middle/right button-downs in addition to left — same
  callback, same guard, same immediate `CallNextHookEx`; the extra branch is a
  comparison and a struct read. Hook latency EMA still covers it.
- Wait-for-window is a WAIT (polls titles, never touches the window); it is
  the only insertable step and keeps the verb ceiling (safety-gate rule 1).
  Chaining is depth-capped and only follows a natural finish (panic stop ends
  the chain). Relative replay only offsets coordinates.
- Profile Rules and Display Profiles switch profiles — an internal, already
  guarded operation; both ship off. DND suppresses UI only; the log is complete.
- Cheat sheet and palette expose only enabled features; the palette runs
  through the same VM entry points as everything else.
- No new dependencies, network paths, or Process.Start sites.

## Stage H notes (S28, v0.41.0) — two relaxations, one tightening
- RELAXED: keyboard-hook rule rewritten to lifecycle (above); hold chords added.
- RELAXED: the global "Allow Launching" sub-gate for placements was REMOVED —
  three switches to launch was ceremony; per-rule Launch box + import quarantine
  (which blanks paths and unticks Launch) already guarantee an import can't
  arrive launching. `PlacementLaunchEnabled` is ignored if present in old JSON.
- TIGHTENED: macro recordings are now DPAPI-encrypted at rest BY DEFAULT in
  installed mode (schema 26 migration switches existing configs on); portable
  mode defaults off because the file travels between logins. The most
  sensitive data the app holds was the least protected by default — fixed.
- Repeat-until-key polls `GetAsyncKeyState` (no hook).
- Paper trail: kept deliberately verbose so full context can be rebuilt from
  the docs alone (my standing rule).

## Stage I notes (S28, v0.42.0)
- Idle rule reads `GetLastInputInfo` (a timestamp) — no hook, no keystrokes.
- Timed pin is a DispatcherTimer, deliberately NOT a hold chord (no hook).
- Scenes/pin memory store process names, titles (≤40 chars), rects, opacity —
  in the ordinary config; they are not treated as sensitive. They only ever
  cause SetWindowPos / layered-alpha / ex-style changes on matched windows.
- Dry Run never reaches `Emit`; pause is a flag in the pacing loop.
- Zone cooldown is a safety property: a jittery extra gesture cannot fire Kill
  or Run Command twice within the window.
- Themes mutate brushes only. No new dependencies, network, or Process.Start.

## Stage K notes (S28, v0.44.0)
- `Win32.TopWindowAtPoint` enumerates top-level windows and reads rects/titles/
  cloak state only — same information the Pinned viewer already reads.
- `ClickThroughBorderWindow` is a transparent, click-through, non-activating
  window of our own; it never touches the target beyond `GetWindowRect`.
- `SuspendGlobalHotkeys` UNREGISTERS every hotkey while a capture UI is open
  (including the macro record hotkey and hold chords) and re-registers after —
  a strictly smaller footprint during capture.
- Placement failure on elevated targets is reported, never worked around: OnTop
  stays `asInvoker`. The "access denied" toast is the security posture speaking.
- After Action (then Stealth / then Exit) is an internal state change; the
  global override narrowing (Exit→Stealth only) removes a surprise, not a guard.
- Footers/revert operate on in-memory config snapshots (JSON) — no new file I/O
  paths; the ordinary save path is unchanged.

## Stage L notes (S28, v0.45.0) — the emergency stop
- `Services/EmergencyStopService`: a dedicated thread with its own Win32
  message loop owns the Exit and Kill hotkeys (`RegisterHotKey` with a NULL
  window → WM_HOTKEY goes to that thread's queue). It adds NO hooks and reads
  NO input; it is two hotkey registrations moved off the UI thread plus a
  self-terminate. Nothing in config, imports or the network can reach it.
- Kill = one synchronous log line, then `TerminateProcess` on our own process.
  Not `Environment.Exit` (runs process-exit handlers on a possibly hung
  runtime). The OS releases low-level hooks and hotkeys on process death; no
  cleanup means exactly that — pinned foreign windows stay pinned, as before.
- Exit still runs the normal UI-thread path (exception-process check, confirm,
  settings save). Escalation to the hard stop happens only if the UI thread
  never STARTS the handler within 3 s — a hung app, not a slow dialog.
- The watchdog pings the dispatcher once a second and logs one WARN line when
  the UI thread has been silent ≥ 4 s (and one when it recovers). It never
  acts on its own: a stop is always a physical key press.
- `Win32.GetProcessName` now uses `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)`
  + `QueryFullProcessImageName` — the least privilege that answers the question;
  protected processes refuse it and fall back to the old path. Read-only.
- The Pinned Windows scan runs on a pool thread. `EnumWindows` / titles /
  process names are the same read-only queries as before, just not on the
  thread that must stay responsive.

## Per-session Security Review Checklist (run every session that touches code)

- [ ] Did any change read or write a file? → size cap, path built from fixed roots,
      atomic write, values validated on load, corrupt-file prompt intact.
- [ ] Did any change touch a foreign HWND? → `IsWindow` re-check before use, only
      Z-order calls, our own windows excluded, tolerant of vanished windows.
- [ ] Did any change add a dependency, network call, or `Process.Start`? →
      justify or remove; never launch anything derived from untrusted data.
- [ ] Did any change touch the manifest or startup? → still `asInvoker`, no
      elevation, single-instance intact.
- [ ] Did any change move data toward execution (format strings, eval, shell)? →
      prove the source is trusted or gate it behind explicit opt-in + warning.
- [ ] Update this file if the attack surface changed.
