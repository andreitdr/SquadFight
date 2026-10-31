# Project: Offline Hero Collector (Unity / C#, mobile)

This project is built autonomously. **At the start of every session:**
1. Read `AUTOPILOT.md` and follow it exactly.
2. Read `PROGRESS.md` and resume at the first unfinished step.
3. Do not wait for instructions; continue the loop until step 18 is DONE.

Only ask the owner what AUTOPILOT.md says to ask. Specs live in `plan/`.

## Release rules (owner decision, 2026-10-31 — always follow)
- **Before every push to `main`, run all tests locally** with `tools/test-all.sh` (Unity Editor closed) and push only if they pass.
- **Build a signed release APK only when the owner asks for one** (not after every push to `main`): `tools/build-apk.sh` (Unity Editor must be closed), then tell the owner the APK path. If the keystore is missing, ask the owner to run `tools/build-apk.sh setup` in their own terminal.
- **No GitHub Actions / CI at all**: tests, builds and releases all happen on the owner's Mac. Do not add anything under `.github/workflows`.
- The owner uploads APKs to GitHub Releases by hand. Releases are **GitHub only**: nothing goes to Google Play, the App Store or any other store.
- **Never commit, print or ask for the keystore password in chat.** It lives only in the macOS Keychain (service `squadfight-android-keystore`); the keystore lives outside the repo (`~/.squadfight/`). The owner types the password only into their own terminal (`tools/build-apk.sh setup`).
- Create or push version tags only when the owner explicitly asks. Plain `git push origin main` (no `--tags` / `--follow-tags`).

## Branch workflow (owner decision, 2026-10-31 — always follow)
- **Every new task gets its own branch**, created from the branch you are on when the task appears: `git switch -c task/<short-slug>` (e.g. `task/ipad-layout`, `task/step-14-shop`). A task is any owner request, a new autopilot step, or a REWORK.
- Do all work and commits for that task on its branch only.
- When the task is done and verified, **try to merge it back** into the branch it came from: `git switch <base>` then `git merge --no-ff task/<slug>`.
  - Merge conflicts: resolve them, re-run the affected tests, then commit the merge.
  - If a conflict cannot be resolved safely: `git merge --abort`, leave the task branch as it is, and ask the owner.
  - After a successful merge, delete the task branch with `git branch -d task/<slug>` (never `-D`).
- Task branches stay local; do not push them. If the base is `main`, the release rules above still apply to the merged result (tests before the push).
