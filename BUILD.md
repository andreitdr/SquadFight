# Building SquadFight

Unity 6000.6.7f1 with Android + iOS modules. Close the Editor before command-line builds.

| Command | Output |
|---|---|
| `./build.sh android-dev` | `Builds/Android/SquadFight-<version>-<build>-dev.apk` — development build (debug console, FPS overlay, desync checks) |
| `./build.sh android-release` | `Builds/Android/SquadFight-<version>-<build>.aab` — release App Bundle for Google Play |
| `./build.sh android-apk` | `Builds/Android/SquadFight-<version>-<build>.apk` — signed release APK to install directly on phones |
| `./build.sh android-release-all` | both of the above with the same build number |
| `tools/build-apk.sh` | signed release APK using the keystore + Keychain password from `tools/build-apk.sh setup` (run after every push to `main`) |
| `./build.sh ios` | `Builds/iOS/` Xcode project (open on a Mac, sign, archive) |
| `./build.sh ios-sim` | `Builds/iOS-Simulator/` dev Xcode project for the iOS Simulator (arm64); then `xcodebuild -project Builds/iOS-Simulator/Unity-iPhone.xcodeproj -scheme Unity-iPhone -sdk iphonesimulator -configuration Debug CODE_SIGNING_ALLOWED=NO build` |
| `tools/test-all.sh` | every test suite (dotnet + BalanceCli + Unity EditMode/PlayMode); run before pushing |
| `./build.sh tests` / `validate` / `setup` | tests, config validation, player settings + scene/theme/audio regeneration |

Windows: `build.ps1 <same target>`.

## Versioning
- **Version name** = `VERSION.txt` (never name it `VERSION`: it breaks IL2CPP builds on macOS) (semantic, e.g. `1.0.0`). Bump it for each release / pre-release.
- **Build number** (Android `versionCode`, iOS build) = `BUILD_NUMBER` env var if set, else `yyMMddHH` UTC.

## Android signing (release only, never commit keys)
Use `tools/build-apk.sh setup` once (see below): it creates the upload key outside the repo and keeps the password in
the macOS Keychain. To call `./build.sh android-apk` / `android-release` directly instead, export
`ANDROID_KEYSTORE_PATH`, `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME` (`upload`) and `ANDROID_KEYALIAS_PASS`.
Keep the keystore and password backed up: every APK of the game must be signed with the same key, or phones refuse to
install the update over the old version (players would have to uninstall and lose their save).

## Releasing a new version (built on your Mac, uploaded by hand)
**There is no GitHub CI.** Tests, builds and releases all run on the Mac:
```bash
tools/test-all.sh           # before every push to main: pure C# tests, content validation, Unity EditMode + PlayMode
```
After every push to `main`, a signed release APK is built locally:
```bash
tools/build-apk.sh          # → Builds/Android/SquadFight-<version>-<build>.apk
```
You upload that APK to a GitHub Release yourself (Releases → Draft a new release → tag `v<VERSION.txt>`, attach the
`.apk`, tick **Set as a pre-release** for `-alpha1` / `-beta.1` / `-rc.1` versions). Releases are for **GitHub only**:
nothing goes to Google Play, the App Store or any other store. Download the `.apk` from the release page and open it on
the phone (allow "install unknown apps" once).

One-time setup (asks you for a new keystore password in your terminal; nothing is written to the repo):
```bash
tools/build-apk.sh setup
```
It creates `~/.squadfight/squadfight-upload.keystore` (alias `upload`) and stores its password in the macOS Keychain
(service `squadfight-android-keystore`), where `tools/build-apk.sh` reads it. Back up the keystore file and the
password: every APK must be signed with the same key, or phones refuse to install the update over the old version.

Version names: `VERSION.txt` (e.g. `0.0.1-alpha1`). The Android version name keeps the suffix; iOS builds drop it
(`0.0.1`) because iOS accepts only digits.

## What every build does
- Config validation runs before the build and blocks it on errors.
- IL2CPP, ARM64 only, managed stripping Medium (release) / Low (dev); `Assets/_Game/link.xml` preserves save types.
- **Offline guarantee:** INTERNET and ACCESS_NETWORK_STATE are removed from the merged Android manifest (`AndroidManifestPostProcessor`).
- Development builds (`BuildOptions.Development`) compile the debug console, FPS overlay and battle desync checks; release builds strip them (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`).
- `Builds/size-report.md` lists the largest assets.

## Android emulator (local testing)
A project-local emulator lives in `AndroidEmulator/` (gitignored, ~5 GB; uses the JDK/sdkmanager from Unity's Android module):
```bash
tools/android-emulator.sh setup     # once: emulator + Android 15 arm64 image + AVD "SquadFight"
tools/android-emulator.sh start     # boots it and waits until Android is ready
tools/android-emulator.sh install   # installs + launches the newest Builds/Android/*.apk (or pass a path)
tools/android-emulator.sh stop
```
The game is ARM64-only, so the arm64 image is used on Apple Silicon; on Intel hosts the script picks x86_64, which cannot run the game.

## iOS
Automatic signing is enabled; open `Builds/iOS/Unity-iPhone.xcodeproj`, choose your team, Archive → TestFlight.
No privacy-sensitive APIs are used (no tracking, no network), so no extra Info.plist usage strings are required.

## Backups (decision D7)
Android Auto Backup and iCloud device backup are left **on**: the save (`save.json`/`save.bak` in persistentDataPath)
is restored after a reinstall/new phone. Saves from a newer app version are never overwritten (read-only mode).
