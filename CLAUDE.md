# Project: Offline Hero Collector (Unity / C#, mobile)

This project is built autonomously. **At the start of every session:**
1. Read `AUTOPILOT.md` and follow it exactly.
2. Read `PROGRESS.md` and resume at the first unfinished step.
3. Do not wait for instructions; continue the loop until step 18 is DONE.

Only ask the owner what AUTOPILOT.md says to ask. Specs live in `plan/`.

## Release rules (owner decision, 2026-10-31 — always follow)
- **Never trigger an APK build by pushing to `main`.** Pushes to `main` only run tests. Do not change CI to build on push.
- Android builds happen **only** for version tags (see `BUILD.md` → "Releasing a new version"):
  - `vX.Y.Z` → **release** (GitHub Release marked Latest)
  - `vX.Y.Z-beta.N` (or `-alpha.N`, `-rc.N`) → **pre-release** (GitHub Release marked Pre-release)
- The tag must equal `VERSION.txt` (bump it and commit first). Never create or push a version tag unless the owner asks for a release or pre-release.
