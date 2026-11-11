## What and why
<!-- What was reported or wanted, the root cause, what this changes. This text becomes the release notes. -->

## Checklist
- [ ] `Directory.Build.props` version bumped (if this ships code or user-visible docs)
- [ ] `CHANGELOG.md` entry
- [ ] Settings shape changed? → `CurrentSchemaVersion` bumped with a migration block
- [ ] `docs/testing/CHECKLIST.md`: new items / `⟳ retest` on touched items
- [ ] Help text and tooltips updated for user-visible changes
- [ ] `SECURITY.md` checklist run (hooks, hotkeys, Win32, file I/O, config, launch, network)
- [ ] Independent review for risky areas; every finding's disposition below

## Review findings and dispositions
<!-- FIXED / NOT A BUG (file:line + reason) / ACCEPTED RISK (recorded where) -->

## Still unverified
<!-- What only a test on Windows can confirm; which checklist IDs. -->
