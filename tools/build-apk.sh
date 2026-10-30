#!/usr/bin/env bash
# Local signed release APK (no CI; the owner uploads APKs to GitHub Releases by hand). Usage:
#   tools/build-apk.sh setup    # once: create the upload keystore and save its password in the macOS Keychain (prompts you)
#   tools/build-apk.sh          # build Builds/Android/SquadFight-<version>-<build>.apk, signed with that keystore
# The keystore lives outside the repo (~/.squadfight, override with SQUADFIGHT_KEYSTORE); the password only ever lives
# in the Keychain (service "squadfight-android-keystore"). Back up both: every APK must be signed with the same key,
# or phones refuse to install the update over the previous version. Close the Unity Editor before building.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
KEYSTORE="${SQUADFIGHT_KEYSTORE:-$HOME/.squadfight/squadfight-upload.keystore}"
ALIAS="upload"
SERVICE="squadfight-android-keystore"

VERSION="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt")"
UNITY_ANDROID="${UNITY_ANDROID:-}"
if [[ -z "$UNITY_ANDROID" ]]; then
  for c in "/Applications/Unity/Hub/Editor/$VERSION/PlaybackEngines/AndroidPlayer" \
           "/Volumes/SSD/Apps/UnityEditors/$VERSION/PlaybackEngines/AndroidPlayer" \
           "$HOME/Unity/Hub/Editor/$VERSION/Editor/Data/PlaybackEngines/AndroidPlayer"; do
    [[ -d "$c" ]] && UNITY_ANDROID="$c" && break
  done
fi
KEYTOOL="$UNITY_ANDROID/OpenJDK/bin/keytool"

case "${1:-build}" in
  setup)
    [[ -x "$KEYTOOL" ]] || { echo "Unity Android module (OpenJDK keytool) not found; set UNITY_ANDROID" >&2; exit 1; }
    if [[ -f "$KEYSTORE" ]]; then
      echo "Keystore already exists: $KEYSTORE (not overwriting)."
    else
      mkdir -p "$(dirname "$KEYSTORE")" && chmod 700 "$(dirname "$KEYSTORE")"
      echo "Creating the upload keystore. Choose a password and keep it safe (it is never written to the repo)."
      "$KEYTOOL" -genkeypair -v -storetype PKCS12 -keystore "$KEYSTORE" -alias "$ALIAS" \
        -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=SquadFight"
      chmod 600 "$KEYSTORE"
    fi
    echo "Saving the keystore password in the macOS Keychain (service \"$SERVICE\"). Type the same password:"
    security add-generic-password -U -a "$USER" -s "$SERVICE" -w
    echo "Done. Back up $KEYSTORE and its password somewhere safe."
    ;;
  build)
    [[ -f "$KEYSTORE" ]] || { echo "No keystore at $KEYSTORE. Run: tools/build-apk.sh setup" >&2; exit 1; }
    PASS="$(security find-generic-password -a "$USER" -s "$SERVICE" -w 2>/dev/null)" \
      || { echo "Keystore password not in the Keychain. Run: tools/build-apk.sh setup" >&2; exit 1; }
    # PKCS12 keystores use one password for the store and the key.
    ANDROID_KEYSTORE_PATH="$KEYSTORE" ANDROID_KEYSTORE_PASS="$PASS" ANDROID_KEYALIAS_NAME="$ALIAS" ANDROID_KEYALIAS_PASS="$PASS" \
      "$ROOT/build.sh" android-apk
    ls -t "$ROOT"/Builds/Android/*.apk | head -1
    ;;
  *) echo "usage: $0 [setup|build]" >&2; exit 2 ;;
esac
