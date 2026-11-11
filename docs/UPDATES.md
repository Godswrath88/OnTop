# OnTop — Release Manifest (stage-one update checker)

The in-app update checker (Settings → Updates) fetches ONE tiny JSON file over
HTTPS, compares versions, and — with your permission — opens the release page
in the browser. The app never downloads or executes anything itself (stage two
is gated on code signing; see SECURITY.md).

## Manifest format
```json
{
  "version": "0.29.0",
  "url": "https://github.com/<you>/<repo>/releases/tag/v0.29.0",
  "notes": "One-line summary shown in the prompt."
}
```
Both URLs must be **https**. Version must parse as `major.minor.patch`.

## GitHub setup (the zero-cost path)
1. Create a repo (private works for the manifest only if the raw URL is public —
   simplest is a public repo). Enable 2FA on the account: **the account is the
   root of trust** for the whole update chain.
2. Commit the manifest as e.g. `update.json` on the default branch.
3. In OnTop: Settings → Updates → Release Manifest URL =
   `https://raw.githubusercontent.com/<you>/<repo>/main/update.json`
4. Each release: upload `OnTopSetup-x.y.z.exe` (and/or the portable exe) to a
   GitHub Release, then edit `update.json`'s version/url. Done — running copies
   see it on their next startup check.

Leave the URL field EMPTY in the app to keep the feature fully dormant.
