---
name: ontop-test-round
description: Use when the user wants to test an OnTop build ("let's test", "test round", "run the checklist", "retest #N"), or has typed results into the Round sheet at the top of docs/testing/CHECKLIST.md: walk the checklist with them one item at a time in chat, read the logs yourself for each result, and do all the writing (marks, dated notes, issue drafts, the Test pass summary). The user only does the steps and says what happened.
---

# OnTop — guided test round

The failure this prevents: the tester transcribing things a session can read
for itself (build number, dates, log lines, issue links), and a single mark
standing in for six separate claims.

The user's part is: do the steps, say `p` or say what happened. Everything
else is this skill's job.

## 1. Pin the build
- Ask nothing that can be read. Newest log in `%APPDATA%\OnTop\logs\`
  (`OnTop started` lines), `Directory.Build.props`, `git log -1`,
  `git status`. Run from Visual Studio = the current checkout.
- State it in one line: "Testing 0.45.5 (abc1234), log OnTop_….log". If the
  running build isn't the one the queue was written for, say so and rebuild the
  queue.
- Remind once: run with **Ctrl+F5** (no debugger) and turn on **Verbose
  Logging**. Under the debugger an exception pauses every thread, which looks
  like a freeze.

## 2. Build the queue
Order, skipping anything already passed on this build:
1. Smoke test (the list in CHECKLIST.md).
2. `[!]` items that are unmarked or `⟳ retest` (a failure there blocks others).
3. Every `⟳ retest` item, grouped by window so the user isn't jumping around.
4. Items added or changed since the last build the user tested
   (`git diff <last-tested-tag>..HEAD -- docs/testing/CHECKLIST.md`).
5. Then, only if the user wants to keep going, unmarked items by section.

Never queue section X (installer, portable, updater) unless the user says they
are testing those. Say how many items and roughly how long, and let the user
cut it down ("just the retests").

Write the queue into the **Round sheet** at the top of CHECKLIST.md (replace
the previous one): one line per item, `ID — five-word title — (#issue)`, with
nothing after the dash at the end. It is the fallback for testing without a
session; in a live round the chat replaces it.

## 3. Walk it, one item per message
- Give the ID, a plain title, and the steps as a short numbered list (three or
  four steps; rewrite the checklist's dense sentence, don't paste it). End with
  what to look for. For an item with lettered sub-checks, give one sub-check at
  a time, or two when they share the same steps.
- Note the clock time when the item is handed over; that bounds the log window.
- Accept anything as an answer:
  - `p`, "pass", "yes", "works" → pass.
  - `s` / "skip" → `[n]`; "can't, X is broken" → `[b]` naming X.
  - "how?" or confusion → explain once more differently; if still unclear,
    `[?]` and fix the item's wording afterwards.
  - Any description of something wrong → fail or partial. Don't make the user
    choose between `[x]` and `[~]`: it works but something is off = `[~]`;
    the item's claim is false = `[x]`. Say which was recorded.
- After EVERY answer, pass or fail, read the log lines since the item started
  (and any new crash bundle in `%APPDATA%\OnTop\config\crashes\`). A pass with
  an `[ERROR]` in its window, or with the expected log line missing, is not a
  pass yet: say what the log shows and ask one specific question.
- On a failure, ask at most one follow-up, and only if the log can't answer it
  and it changes the fix. Then move on; diagnosis happens after the round
  (`ontop-feedback-intake`), not in the middle of it.
- If a `[!]` item fails, drop the queue items that depend on it (`[b]`) rather
  than making the user test them.
- Keep each message short. No recap of earlier items.

## 4. Record as it goes
After each answer, edit `docs/testing/CHECKLIST.md` straight away (a round can
end at any moment):
- The mark on the item or sub-check, and a dated note in the user's words:
  `  > MM-DD: vX.Y.Z, <what they said>` (add the one log line that matters,
  with its category, when there is one).
- Never upgrade a mark. What the user didn't say they saw is untested. A parent
  item's mark follows its sub-checks: all pass → `[.]`; any fail → `[x]`;
  otherwise `[~]` or `[ ]`. Never change a mark the user typed themselves
  without telling them in that message.
- A `⟳ retest` that passes: remove the `⟳ retest` text, keep the `(#N)` until
  the issue is closed.
- Log lines quoted into the checklist or an issue must not carry window titles
  or the names of the user's own apps: generalize them ("a browser window").

## 5. Close the round
Show one summary table: ID, result, one line. Then list the outward steps and
do them only after the user says go (issues are public):
- Passed retest → comment on the issue with the build and close it.
- Failed retest → comment on the issue: build, what happened, the relevant log
  lines only.
- New failure → a Bug report issue (labels `bug`, plus `blocker` / `needs-log`
  as fits); add `(#N)` to the checklist item.
- A **Test pass** issue for the build: smoke results, retests, issues filed.
- Commit the checklist on a branch (`test/round-vX.Y.Z`) and open a PR; no
  version bump for marks alone.

Never tell the user a build is Stable or suggest marking it; only they grant
that. Failures found here go to `ontop-feedback-intake` before any fix.

## Results typed into the Round sheet instead
If the user filled in the Round sheet on their own: treat each line as an
answer in step 3 (timestamps come from the log, by matching what the item
does), record as in step 4, ask about lines that are ambiguous in ONE batch,
then close as in step 5 and clear the sheet.
