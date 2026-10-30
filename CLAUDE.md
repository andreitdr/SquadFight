# Project: Offline Hero Collector (Unity / C#, mobile)

This project is built autonomously. **At the start of every session:**
1. Read `AUTOPILOT.md` and follow it exactly.
2. Read `PROGRESS.md` and resume at the first unfinished step.
3. Do not wait for instructions; continue the loop until step 18 is DONE.

Only ask the owner what AUTOPILOT.md says to ask. Specs live in `plan/`.

## Release rules (owner decision, 2026-10-31 — always follow)
- **Nothing is built or released when pushing to `main`, unless the owner explicitly asks for a release or pre-release.** Pushes to `main` only run tests. Do not change CI to build or release on push; CI is read-only except the tag-gated release job.
- Releases and pre-releases are **GitHub only** (signed APK on a GitHub Release). Nothing goes to Google Play, the App Store or any other store; CI builds no AAB.
- Android builds happen **only** for version tags (see `BUILD.md` → "Releasing a new version"):
  - `vX.Y.Z` → **release** (GitHub Release marked Latest)
  - `vX.Y.Z-beta.N` (or `-alpha.N`, `-rc.N`) → **pre-release** (GitHub Release marked Pre-release)
- The version tag is the explicit release signal. Create and push one only when the owner explicitly asks for a release / pre-release, never as part of a normal push. The tag must equal `VERSION.txt` (bump it and commit first). Plain `git push origin main` never pushes tags (do not use `--tags` / `--follow-tags`).
