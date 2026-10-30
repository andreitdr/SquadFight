# Building SquadFight

Unity 6000.6.7f1 with Android + iOS modules. Close the Editor before command-line builds.

| Command | Output |
|---|---|
| `./build.sh android-dev` | `Builds/Android/SquadFight-<version>-<build>-dev.apk` — development build (debug console, FPS overlay, desync checks) |
| `./build.sh android-release` | `Builds/Android/SquadFight-<version>-<build>.aab` — release App Bundle for Google Play |
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

## What every build does
- Config validation runs before the build and blocks it on errors.
- IL2CPP, ARM64 only, managed stripping Medium (release) / Low (dev); `Assets/_Game/link.xml` preserves save types.
- **Offline guarantee:** INTERNET and ACCESS_NETWORK_STATE are removed from the merged Android manifest (`AndroidManifestPostProcessor`).
- Development builds (`BuildOptions.Development`) compile the debug console, FPS overlay and battle desync checks; release builds strip them (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`).
- `Builds/size-report.md` lists the largest assets.

## iOS
Automatic signing is enabled; open `Builds/iOS/Unity-iPhone.xcodeproj`, choose your team, Archive → TestFlight.
No privacy-sensitive APIs are used (no tracking, no network), so no extra Info.plist usage strings are required.

## Backups (decision D7)
Android Auto Backup and iCloud device backup are left **on**: the save (`save.json`/`save.bak` in persistentDataPath)
is restored after a reinstall/new phone. Saves from a newer app version are never overwritten (read-only mode).
