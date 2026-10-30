# Project: Offline Hero Collector (Unity / C#, mobile)

This project is built autonomously. **At the start of every session:**
1. Read `AUTOPILOT.md` and follow it exactly.
2. Read `PROGRESS.md` and resume at the first unfinished step.
3. Do not wait for instructions; continue the loop until step 18 is DONE.

Only ask the owner what AUTOPILOT.md says to ask. Specs live in `plan/`.

## Release rules (owner decision, 2026-10-31 — always follow)
- **After every push to `main`, build a new signed release APK locally** with `tools/build-apk.sh` (Unity Editor must be closed) and tell the owner the APK path. If the keystore is missing, ask the owner to run `tools/build-apk.sh setup` in their own terminal.
- **CI never builds Android or iOS and never publishes releases.** Do not add build/release jobs to `.github/workflows`; CI only runs tests.
- The owner uploads APKs to GitHub Releases by hand. Releases are **GitHub only**: nothing goes to Google Play, the App Store or any other store.
- **Never commit, print or ask for the keystore password in chat.** It lives only in the macOS Keychain (service `squadfight-android-keystore`); the keystore lives outside the repo (`~/.squadfight/`). The owner types the password only into their own terminal (`tools/build-apk.sh setup`).
- Create or push version tags only when the owner explicitly asks. Plain `git push origin main` (no `--tags` / `--follow-tags`).
