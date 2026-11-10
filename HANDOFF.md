# OnTop — Handoff Context (read me first in a new chat)

**This file is a temporary bootstrap for a fresh conversation. It is safe to
discard/overwrite once the new chat is rolling.** It summarizes where things
stand so a new session can continue without re-reading everything.

---

## S28 (2026-09-16): staged "finish everything" session — see SESSION_STATE.md
The owner asked to complete all remaining roadmap items in staged, individually
revertable deliveries. `SESSION_STATE.md` is the stage ledger (which zips exist,
how to resume, how to roll back). `OnTopWPF_v0.33.8_baseline.zip` is the
untouched pre-session rollback point.

## What OnTop is
A Windows WPF (.NET 8) always-on-top pinning + zone-exit/stealth tray utility,
rewrite of a lost AutoHotkey version. Single non-elevated process. Built by the
user (Godswrath88) on Windows between chat turns; the assistant's container CANNOT
compile WPF — verification is STRUCTURAL only (xmllint, brace/paren balance,
grep cross-refs, dup-member scans).

## THE #1 THING RIGHT NOW: an unconfirmed build
- Current version **0.43.0** (S28 Stage J layout round), schema **28**, 85 source files. 0.42.1 compiled clean; 0.43.0 is untested. NOTHING from 0.37 onward has been functionally tested — TESTING.md is the outstanding list; owner starts the sweep on 0.43.0. Stages A–I delivered; owner has confirmed only 0.36.1. Next: owner build/test of everything, then Stage J (QA) → 1.0.0. Stages A–G all delivered; next is owner testing of D–G + installer/updater/portable, then Stage H (QA) and 1.0.0. Build confirmed clean at 0.36.1; installer/updater/portable still UNTESTED by owner (don't assume). NEW: one NuGet package (ProtectedData) — build needs restore. See SESSION_STATE.md for the stage ledger.
- The last CODE change was the **v0.33.2 duplicate-definition fix** (removed a
  duplicate `HotkeyStealth`/`_hotkeyStealth` in AppSettings.cs that caused
  CS0102). Versions **0.33.3 → 0.33.6 are ALL DOCS-ONLY** (security roadmap
  work) — no code changed since 0.33.2.
- **v0.36.1 is the first build feedback on the 0.33+ line** — it proved 0.33.2
  never compiled (undeclared `MacroSvc` / `_sync`, shadowed `idx`). All fixed in
  0.36.1; the next build report tells us if the line is finally clean.
- (Historical) Godswrath88 had NOT confirmed 0.33.2 compiles clean. First priority in the
  new chat: get that build confirmation. Paste-build-errors-verbatim loop
  continues. Two build errors have already been fixed reactively this session
  (the MC3088 XAML trigger-placement error in 0.33.1, and the CS0102 duplicate
  in 0.33.2).

## The zip is the ONLY backup
- Deliver `/mnt/user-data/outputs/OnTopWPF_updated.zip` every session via
  present_files.
- On container reset, RESTORE FROM THE ZIP — never rebuild from memory. (This
  happened twice this session; restored cleanly both times.)
- Zip command:
  `zip -rq OnTopWPF_updated.zip README.md WHATS_NEW.md ROADMAP.md PROJECT_SCOPE.md CHANGELOG.md MASTER_PROMPT.md SECURITY.md OnTop.sln OnTop/ installer/ portable/ -x '*/bin/*' '*/obj/*' '*/publish/*' '*/Output/*' '*/out/*'`

## NEW HARD RULE (S27) — document every change, verify every edit
- **Every delivery, including trivial and docs-only, needs CHANGELOG (+ link
  ref) + WHATS_NEW + PROJECT_SCOPE entries.** Before zipping, verify csproj,
  installer .iss, CHANGELOG head, and PROJECT_SCOPE "current:" all name the SAME
  version. Origin: docs-only 0.33.3–0.33.6 skipped CHANGELOG and it fell four
  versions behind unnoticed.
- **Verify the ACTUAL text you inserted, never a proxy.** Origin: those same
  versions' PROJECT_SCOPE lines silently failed to apply — each append was
  anchored to text from the previous (failed) append, and the check printed the
  version-bump count instead of the inserted text, hiding the whole chain. Both
  gaps were backfilled in 0.33.7. Never anchor an edit to a prior unverified edit.

## Standing workflow contract (from PROJECT_SCOPE.md — read it in full)
- Read PROJECT_SCOPE / ROADMAP / WHATS_NEW before coding.
- SECURITY IS PRIORITY #1 — per-session security checklist is mandatory.
- Update ALL docs every session; bump version in OnTop/OnTop.csproj (3 fields) +
  installer/OnTop.iss + CHANGELOG + WHATS_NEW + PROJECT_SCOPE once per delivery.
- Bump schema in SettingsService.cs (CurrentSchemaVersion) with a documented
  migration when settings shape changes.
- No silent catches. Help + tooltips updated same session as a feature. Every
  feature decides its hotkey story. Main-GUI cohesion. UI-layout review +
  extension-method-using check before ship. Give the FULL TESTING CHECKLIST at
  the end of every response until Godswrath88 builds.
- KEY BUILD GOTCHAS learned: `<Style.Triggers>` must come AFTER all `<Setter>`s
  (never between); when adding a settings field, grep AppSettings for a
  pre-existing definition FIRST (duplicate-def trap); extension methods need
  their namespace `using`; `Owner=` on a never-shown window throws.

## What shipped this session (S25–S27), all delivered
- **v0.30.0** — visual cohesion pass (design tokens, focus rings, disabled
  states, dialog open-motion, accent picker, Win10 acrylic fallback) + portable
  mode + audits closed.
- **v0.31.0** — per-zone actions (Run Command / Kill Process w/ protected-name
  refusal / Toggle Window; verbatim display, log+toast, Test-Mode-suppressed) +
  stealth follow-ups (toggle hotkey, auto-stealth watcher, overlay hide).
- **v0.32.0** — hotkey conflict prevention (duplicate bindings refused at save;
  "one combo, one feature").
- **v0.33.0 → 0.33.1** — **THE MACRO SUBSYSTEM CORE** (the North Star): full
  record/playback (mouse+wheel+keys, real timing, layout-adaptive via 0..65535
  virtual-desktop normalization), SendInput playback with drift-free pacing,
  speed/repeat, per-macro hotkeys, zone Run-Macro action, master toggle (ships
  OFF/dark), anti-cheat guard (watched-process refuse + mid-run abort),
  Ctrl+Alt+X panic, SECURITY.md "Macro Module". 0.33.1 hotfixed the MC3088
  build error + a macro wheel-playback bug (spurious cursor move). Also the
  welcome-tour re-show checkbox + soft Restart button (Settings → Maintenance).
- **v0.33.2** — CS0102 duplicate-definition fix (the current code baseline).
- **v0.33.3–0.33.8** — DOCS-ONLY security architecture (see below), the
  document-every-change rule, and the future-capability safety gate.

## Security work DOCUMENTED this session but NOT yet built (all on ROADMAP)
1. **Window Placement & Launcher** — start/move programs to set monitor
   positions on startup/hotkey/zone (Godswrath88's headset-app use case). With
   **privilege compartmentalization**: single launcher choke-point, config-in-
   not-call-in (no runtime Launch(anyString) API a compromised feature can
   invoke), move≠launch least-privilege split, reuse guarded ShellExecute.
2. **STANDING DESIGN LAW** — every non-core feature must be fully disableable
   (only core pinning always-on); for anti-cheat compat AND user autonomy.
3. **Config & Trusted-Data Integrity** — nothing dangerous auto-executes on
   load; validate-in; re-assert trust at load; no integrity theatre.
4. **Shared-config import quarantine** — powerful content from an EXPLICIT
   outside import lands inert until the user reviews a verbatim summary and opts
   each in. NOT encryption (rejected for tamper-proofing: a hostile author signs
   with their own key). **Provenance exemption**: the app's OWN settings +
   backups are trusted → crash/corruption fallback loads fully active, seamless,
   NO re-approval (recovery must never punish the user).
5. **Optional at-rest confidentiality** (NOTE: export is a SEPARATE deliberate
   path — encryption must never block sharing; profiles/layouts/hotkeys/settings
   are never encrypted, macros are excluded from exports by default behind an
   opt-in + literal-keystroke warning) — sensitive data (window titles, paths,
   and esp. MACRO recordings = raw keystrokes/possible passwords) can OPTIONALLY
   be encrypted for privacy via Windows DPAPI (per-user, login-tied, no stored
   key). Encryption = YES for privacy, NO for authority — never conflate.

## Before adding ANY new capability (scripting, launch, file, network)
Read SECURITY.md **"Future Capability Expansion — the safety gate"** first. In
short: bounded VERB LIST, never a general-purpose script engine with ambient
authority; capabilities granted per-macro and never self-granted by imported
content; **NO install / download-then-execute, ever**; reuse the single launcher
choke-point; nothing auto-runs on load or import. This gate is what makes the
single-process decision safe as the app grows.

## Decided AGAINST (don't re-propose without new information)
- **Multi-process / broker architecture** — analyzed S27, recommended against:
  same-user/same-integrity processes aren't isolated (needs
  low-integrity/AppContainer + broker), OnTop has no OS-level privileges to
  compartmentalize, no plugin/script/network attack surface exists, and a
  separate input-synthesis helper resembles cheat-tool architecture (worse for
  anti-cheat — the owner's priority). REVISIT ONLY IF plugins/scripting,
  untrusted binary parsing, or a network service is added. Re-analyzed S27
  against a possible SCRIPTING future and the decision held, CONDITIONAL on the
  safety gate above (rules 1–6). Concrete revisit triggers: a non-strippable
  engine is embedded, third-party/community content runs by default, or
  untrusted binary parsing / a network service appears. If revisited, the
  correct form is a low-integrity/AppContainer child + broker exposing only the
  vetted verb list — naive same-integrity process splitting is NOT isolation.
- **Signing/encrypting config for tamper-proofing** — rejected; a hostile author
  signs with their own key. Content validation + quarantine is the real defense.

## Other open roadmap (post-core)
- Config integrity hardening pass (updater-host re-validation on load,
  macro-event range validation, inert-load regression test, powerful-field
  change notice) — folds in items 3–5 above.
- Action Library decoupling (named reusable actions, hotkey- OR zone-assignable,
  independently toggleable) — the same architecture macros use; run commands
  become keybindable without a zone.
- Final visual/UX QA pass (now "post-core"; pinned before heavy macro reliance).
- Macro polish: humanizing jitter, condition loops, lighter capture modes,
  relative replay, per-profile policies.
- Possible "Features" panel + one-click "Minimal Mode".
- Updater stage two (gated on code signing — Godswrath88 won't pay; GitHub + HTTPS +
  SHA-256 is the accepted stack; Azure Trusted Signing ~$10/mo or free SignPath
  noted). GitHub approved as host; no repo exists yet.
- AHK importer — OPTIONAL, Godswrath88 may skip; needs the original .ahk file.
- Localization scaffold — deferred.

## What Godswrath88 has confirmed working (don't re-litigate)
Active-profile delete, log-viewer clipping fix, taskbar hover, Test Mode, tray
status dot. Everything from v0.30+ is UNTESTED by him so far (visual cohesion,
per-zone actions, stealth follow-ups, the entire macro subsystem, maintenance
QoL). Crash prompt, updater, installer, portable build: all untested.

## The current outstanding TESTING CHECKLIST
The full v0.32→v0.33.x checklist was given in-chat (hotkey conflicts; all macro
items incl. wheel/layout-adaptation/anti-cheat/panic/persistence; maintenance
QoL; per-zone actions; stealth; visual cohesion; core fixes; environment-gated
installer/updater/crash/portable). Re-issue it until he builds. TOP of the list:
**confirm 0.33.2 compiles clean.**
