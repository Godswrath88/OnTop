# Git hooks (privacy guard)

These hooks stop a personal identifier from ever reaching a commit or a push:

- **pre-commit:** refuses a commit if `git config user.email` isn't a GitHub
  noreply address, or if an added line matches a private pattern.
- **pre-push:** re-checks every commit being pushed: diffs, messages, and
  author/committer identities.

The hooks hold no personal data. The patterns live in
`.git/info/identity-patterns` (one extended regex per line: real name, personal
e-mail, Windows account path). `.git/` is never uploaded, so the patterns
never leave the machine.

Set up once per clone:

```
git config core.hooksPath tools/git-hooks
git config user.name Godswrath88
git config user.email <id>+Godswrath88@users.noreply.github.com
# then write .git/info/identity-patterns
```

CI runs `tools/check-privacy.ps1` as a public, pattern-free backstop (real
user-profile paths, real e-mail addresses, non-noreply commit authors).
