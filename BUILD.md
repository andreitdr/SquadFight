# Building SquadFight

Unity 6000.6.7f1 with Android + iOS modules. Close the Editor before command-line builds.

| Command | Output |
|---|---|
| `./build.sh android-dev` | `Builds/Android/SquadFight-<version>-<build>-dev.apk` — development build (debug console, FPS overlay, desync checks) |
| `./build.sh android-release` | `Builds/Android/SquadFight-<version>-<build>.aab` — release App Bundle for Google Play |
| `./build.sh android-apk` | `Builds/Android/SquadFight-<version>-<build>.apk` — signed release APK to install directly on phones |
| `./build.sh android-release-all` | both of the above with the same build number (what CI runs on a release) |
| `./build.sh ios` | `Builds/iOS/` Xcode project (open on a Mac, sign, archive) |
| `./build.sh ios-sim` | `Builds/iOS-Simulator/` dev Xcode project for the iOS Simulator (arm64); then `xcodebuild -project Builds/iOS-Simulator/Unity-iPhone.xcodeproj -scheme Unity-iPhone -sdk iphonesimulator -configuration Debug CODE_SIGNING_ALLOWED=NO build` |
| `./build.sh tests` / `validate` / `setup` | tests, config validation, player settings + scene/theme/audio regeneration |

Windows: `build.ps1 <same target>`.

## Versioning
- **Version name** = `VERSION.txt` (never name it `VERSION`: it breaks IL2CPP builds on macOS) (semantic, e.g. `1.0.0`). Bump it for each store release.
- **Build number** (Android `versionCode`, iOS build) = `BUILD_NUMBER` env var if set (CI), else `yyMMddHH` UTC.

## Android signing (release only, never commit keys)
Create an upload key once:
```bash
keytool -genkeypair -v -keystore squadfight-upload.keystore -alias upload -keyalg RSA -keysize 2048 -validity 10000
```
Then, before `./build.sh android-release`:
```bash
export ANDROID_KEYSTORE_PATH=/secure/path/squadfight-upload.keystore
export ANDROID_KEYSTORE_PASS=...
export ANDROID_KEYALIAS_NAME=upload
export ANDROID_KEYALIAS_PASS=...
```
CI uses `ANDROID_KEYSTORE_BASE64` (base64 of the keystore) plus the same three secrets. Enable Play App Signing in the Play Console.
Keep the keystore and passwords backed up: every APK of the game must be signed with the same key, or phones refuse to
install the update over the old version (players would have to uninstall and lose their save).

## Releasing a new version (GitHub Actions)
**Rule: pushing to `main` never builds an APK** — it only runs the tests. An Android build (signed **APK** + **AAB**,
attached to a GitHub Release named after the tag) happens only when you push a version tag:

| Tag | Build | GitHub Release |
|---|---|---|
| `v1.1.0` | release | normal release, marked **Latest** |
| `v1.1.0-beta.1` (also `-alpha.N`, `-rc.N`) | pre-release | marked **Pre-release**, never "Latest" |
| anything else (`v1.1`, `v1.1.0-test`, …) | none (the job stops with an error) | — |

Both channels are release-signed with the same upload key, so a tester can install a pre-release and later update to
the release without uninstalling. The tag must equal `VERSION.txt` with a `v` in front, or the job stops.

Pre-release (testers):
```bash
echo 1.1.0-beta.1 > VERSION.txt
git commit -am "pre-release: 1.1.0-beta.1"
git tag v1.1.0-beta.1
git push origin main v1.1.0-beta.1
```
Release:
```bash
echo 1.1.0 > VERSION.txt
git commit -am "release: 1.1.0"
git tag v1.1.0
git push origin main v1.1.0
```
The Android version name shows the suffix (`1.1.0-beta.1`); iOS builds drop it (`1.1.0`) because iOS accepts only digits. Creating the release from the GitHub web UI with a new `v…` tag works
too; the files are added to that release. Download the `.apk` from the release page and open it on the phone
(allow "install unknown apps" once).

One-time repository setup (Settings → Secrets and variables → Actions):
| Secret | Value |
|---|---|
| `UNITY_LICENSE` | contents of your `Unity_lic.ulf` (Personal: see game.ci "Activation") |
| `UNITY_EMAIL`, `UNITY_PASSWORD` | your Unity account |
| `ANDROID_KEYSTORE_BASE64` | `base64 -i squadfight-upload.keystore \| pbcopy` (macOS) |
| `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME`, `ANDROID_KEYALIAS_PASS` | the keystore password, alias (`upload`) and key password |

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
