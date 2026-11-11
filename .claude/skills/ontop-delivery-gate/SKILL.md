---
name: ontop-delivery-gate
description: Use before pushing a branch / opening or updating any OnTop pull request: the mechanical pre-PR gate (build + tests, version coherence, edits landed, encoding, identity scan, docs per change, claims audit) ending in a scorecard. A check without pasted output is a failed check; a failure blocks the PR.
---

# OnTop — delivery gate (before every PR)

Rules:
1. Every check is a COMMAND; paste its output (trimmed, not paraphrased). A
   check described without output FAILED.
2. A failing row blocks the PR. Fix it, then re-run the WHOLE gate.
3. Never weaken a check to make it pass; account for every line it printed.
4. Verify the ACTUAL inserted text (`grep -n` the new line), never a proxy.
5. Order: threat review (when it applies) → fixes → independent review →
   fixes → second pass on the fix delta (when non-trivial) → this gate, last.
6. Temporary files go in the session scratchpad, never the repo or /tmp.

## A. Build and tests
```
dotnet build OnTop.sln -c Release -nologo -v q -p:TreatWarningsAsErrors=true
dotnet test OnTop.sln -c Release --no-build -nologo
powershell -NoProfile -ExecutionPolicy Bypass -File tools/check-version.ps1
```
0 warnings, 0 errors, all tests pass, version OK. If the installer script,
`Directory.Build.props` or `tools/` changed, also run
`tools/build-installer.ps1 -OutDir <scratchpad>`.

## B. Edits landed
For each change: `grep -n '<unique fragment>' <file>`. For each new symbol:
its definition and at least one use. Removed symbols: no references left. For
every new call into another class, OPEN it and confirm the member and
signature (plausible-but-missing members are the most common AI failure).
New catches: `git diff main...HEAD | grep '^+.*catch'`; each logs or carries a
`// justified-silent:` reason.

## C. Encoding and identity
```
# double-encoded UTF-8 introduced by this branch (perl/sed mishaps):
for f in $(git diff --name-only main...HEAD); do [ -f "$f" ] && c=$(grep -c $'\xc3\xa2\xc2\x80\|\xc3\x83\xc2' "$f"); m=$(git show main:"$f" 2>/dev/null | grep -c $'\xc3\xa2\xc2\x80\|\xc3\x83\xc2'); [ "${c:-0}" != "${m:-0}" ] && echo "GARBLED $f"; done
# personal identifiers: private patterns live in .git/info/identity-patterns
# (inside .git, so they can never be committed or uploaded):
git diff main...HEAD | grep '^+' | grep -i -E -f .git/info/identity-patterns
git log main..HEAD --format='%an <%ae>' | sort -u
```
Encoding and identity scans must print NOTHING; authors must be the public
handle with the noreply address. The private patterns file (real name, personal
e-mail, Windows account path; one regex per line) lives only in `.git/info/`;
never copy it anywhere or echo its contents. The local pre-commit and pre-push
hooks enforce the same patterns. Logs or test notes copied into docs or issues
must not carry window titles or names of the user's own apps.

## D. Docs per change
- `Directory.Build.props` version bumped when the PR ships code or
  user-visible docs; `CHANGELOG.md` entry for that version + link ref.
- Stored settings shape or a default changed → `CurrentSchemaVersion` bumped
  with a migration block (`git diff main...HEAD -- OnTop/Models/AppSettings.cs
  OnTop/Models/*.cs` — any new persisted property counts).
- `docs/testing/CHECKLIST.md`: items for everything the user must verify;
  `⟳ retest` on touched items; the riskiest item named first in the PR.
- User-visible feature → Help text and tooltips updated (grep the old wording).
- Requirement/rule/architecture changed → `docs/dev/PROJECT_SCOPE.md`;
  roadmap item shipped → `docs/dev/ROADMAP.md`.
- Security-relevant code → SECURITY.md checklist run; notes updated.

## E. Claims audit
Every factual claim in the CHANGELOG entry, PR description, Help and
SECURITY.md text changed: give the file:line that makes it true. Absolute
words ("every", "never", "no longer", "cannot") need proof for the whole
quantifier or get softened. Re-run after review fixes.

## F. Scorecard (last thing before `git push` / `gh pr create`)
```
A build/tests     ✔/✘  <0 warn/0 err; n/n tests; version x.y.z OK>
B edits landed    ✔/✘  <n greps; n members opened; removed: 0 refs; catches n, all justified>
C encoding/ident  ✔/✘  <garble: none; identity: none; authors: handle/noreply>
D docs            ✔/✘  <CHANGELOG x.y.z; schema N (+migration / unchanged); checklist IDs; Help>
E claims          ✔/✘  <n claims with file:line; n softened>
R independent review  ran (passes) / not required (why) / SKIPPED-BECAUSE: …
T threat review       ran (roles) / not required / OPEN FINDINGS: n (where)
CI                    pending → link; report the result when it finishes
COULD NOT VERIFY HERE: runtime behavior of <claims> → checklist <IDs>; test FIRST: <ID>
```
An empty row means the gate didn't pass. Never claim the installer, updater
or portable build works; after merge the user tags, and `release.yml` builds
the downloads.
