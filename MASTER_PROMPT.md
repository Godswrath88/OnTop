# OnTop — Master Prompt (copy-paste this to start a new development session)

> **How I use this file:** I upload `OnTopWPF_updated.zip` to a new development
> session, then paste the prompt between the lines below as the first message.
> If the session can extract the zip itself, that's all it needs; otherwise I
> extract and upload the repo folder. (This is the one document that is written
> as a prompt on purpose — it is the bootstrap for the next session.)
>
> **Keep this file current:** this prompt must be UPDATED whenever the workflow,
> file layout, doc set, or standing rules change — updating it is part of the
> standard session workflow (see PROJECT_SCOPE.md → Session Workflow). Whoever
> changes how sessions work changes this file in the same session.

---

I'm continuing development of **OnTop**, my C# WPF (.NET 8) Windows utility
(always-on-top pinning + zone-exit/stealth tray app). The attached zip is the
complete project: source, solution, and all continuity documentation. This is a
long-running multi-session project — the zip is the ONLY backup, and the docs
inside it are the contract between sessions.

**Before writing any code, read these files from the zip root, in this order:**

0. **`SESSION_STATE.md`** (if present) — the stage-level checkpoint ledger: which
   staged deliveries exist, which zip to restore to resume or to ROLL BACK, and
   the plan for any unfinished stage. Then `HANDOFF.md` (session-level summary).
1. **`PROJECT_SCOPE.md`** — the continuity contract. What the app is, my standing
   requirements (never regress these), the hard-won build rules (violating them
   breaks the build — including the verification gotchas), the config/directory
   contract, the architecture map, the feature inventory, the session workflow,
   and the one-line session history.
2. **`ROADMAP.md`** — everything approved and outstanding, what's shipped (✅),
   what's pending, and the 🎯 End Goal section (the Macro Subsystem capstone —
   read it before any macro-related work).
3. **`WHATS_NEW.md`** — newest-first session narratives; read at least the top
   2–3 entries so you know exactly where the last session left off and what I
   still need to verify on Windows.
4. **`CHANGELOG.md`** — terse version index (skim; useful for version numbering).
5. **`SECURITY.md`** — the security contract: threat model, standing rules, and
   the per-session Security Review Checklist. **Security is priority #1** — run
   the checklist after any code change.
6. **`README.md`** — user-facing overview (skim). `LICENSE` is GPL-3.0 — never
   edit it, and keep it in every zip.
7. **`docs/testing/CHECKLIST.md`** — the standing test checklist, by feature,
   with my marks; open failures and "retest" items are GitHub issues (#N links).
   `docs/TROUBLESHOOTING.md` is the user-facing guide.

**Then follow the Session Workflow in PROJECT_SCOPE.md.** The short version:
- Your environment cannot compile WPF — verify structurally (xmllint every .xaml,
  brace/paren balance every .cs, grep cross-reference every new symbol, and grep
  for existing definitions BEFORE adding any method). I build on Windows and
  paste errors verbatim.
- Bump the csproj version once per session (+ CHANGELOG entry); bump the settings
  schema (with a documented migration) only when the stored shape changes.
- Update ALL docs every session — PROJECT_SCOPE (history + version note),
  ROADMAP (tick shipped items), WHATS_NEW (session entry), CHANGELOG — and this
  MASTER_PROMPT.md if the workflow itself changed.
- Multi-stage work: every stage is its own complete delivery (version + docs +
  zip); update SESSION_STATE.md before and after each stage so a cut-off session
  can be resumed or rolled back from the last delivered zip.
- Write docs and code comments in first person (as me, the author) or neutral
  imperative — never as "the owner asked". Never write my Windows username,
  e-mail or machine name into any file; the public handle is Godswrath88.
- Re-zip everything (LICENSE + all docs incl. MASTER_PROMPT.md + SECURITY.md +
  SESSION_STATE.md + HANDOFF.md + docs/ + .github/ + the dotfiles + OnTop.sln + full
  OnTop/ tree + installer/ + portable/, excluding bin/obj/publish/Output/out) and
  deliver the zip EVERY session, with a short "when you build, test X" list.
- Installer, updater and portable build are untested until I say otherwise —
  never assume they work.

Tell me what state the project is in per the docs, then we'll pick this session's
work (I may have build feedback from the last zip to address first).

---

*(End of prompt. I replace the last paragraph with my actual request when I
already know what I want this session — e.g. paste build errors, or name the
features to add.)*

**DOCUMENT EVERY CHANGE, INCLUDING TRIVIAL AND DOCS-ONLY ONES.** Every delivery
gets a CHANGELOG entry (+ link ref), a WHATS_NEW entry, and a PROJECT_SCOPE
line — no "too small to bother." Before zipping, verify csproj, installer .iss,
CHANGELOG head, WHATS_NEW head, and PROJECT_SCOPE "current:" all name the same
version. (This rule exists because docs-only v0.33.3–0.33.6 skipped CHANGELOG
and it fell four versions behind unnoticed.)
