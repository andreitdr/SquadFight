#!/usr/bin/env bash
# Local Android emulator kept inside the project (AndroidEmulator/, gitignored). Usage:
#   tools/android-emulator.sh setup            # download emulator + system image, create the AVD (once, ~2 GB)
#   tools/android-emulator.sh start            # boot the emulator (window); waits until Android is ready
#   tools/android-emulator.sh install [apk]    # install + launch an APK (default: newest in Builds/Android)
#   tools/android-emulator.sh stop
# Uses the JDK and sdkmanager bundled with the Unity Android module; UNITY_ANDROID overrides its path.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
HOME_DIR="$ROOT/AndroidEmulator"
SDK="$HOME_DIR/sdk"
export ANDROID_AVD_HOME="$HOME_DIR/avd"
export ANDROID_SDK_ROOT="$SDK" ANDROID_HOME="$SDK"
AVD="SquadFight"
API="${EMULATOR_API:-35}"
ARCH="$([[ "$(uname -m)" == arm64 ]] && echo arm64-v8a || echo x86_64)"
IMAGE="system-images;android-$API;google_apis;$ARCH"
PACKAGE="$(sed -n 's/^    Android: \(.*\..*\)$/\1/p' "$ROOT/ProjectSettings/ProjectSettings.asset" | head -1)"

VERSION="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt")"
UNITY_ANDROID="${UNITY_ANDROID:-}"
if [[ -z "$UNITY_ANDROID" ]]; then
  for c in "/Applications/Unity/Hub/Editor/$VERSION/PlaybackEngines/AndroidPlayer" \
           "/Volumes/SSD/Apps/UnityEditors/$VERSION/PlaybackEngines/AndroidPlayer" \
           "$HOME/Unity/Hub/Editor/$VERSION/Editor/Data/PlaybackEngines/AndroidPlayer"; do
    [[ -d "$c" ]] && UNITY_ANDROID="$c" && break
  done
fi
[[ -d "$UNITY_ANDROID" ]] || { echo "Unity Android module not found; set UNITY_ANDROID" >&2; exit 1; }
export JAVA_HOME="$UNITY_ANDROID/OpenJDK"
SDKMANAGER="$UNITY_ANDROID/SDK/cmdline-tools/16.0/bin/sdkmanager"
# avdmanager finds system images relative to its own install, so use the copy installed into our SDK.
AVDMANAGER="$SDK/cmdline-tools/latest/bin/avdmanager"
ADB="$SDK/platform-tools/adb"
EMULATOR="$SDK/emulator/emulator"

wait_boot() {
  "$ADB" wait-for-device
  until [[ "$("$ADB" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" == 1 ]]; do sleep 2; done
}

case "${1:-}" in
  setup)
    mkdir -p "$SDK" "$ANDROID_AVD_HOME"
    "$SDKMANAGER" --sdk_root="$SDK" --install "cmdline-tools;latest" "platform-tools" "emulator" "platforms;android-$API" "$IMAGE"
    if ! "$EMULATOR" -list-avds 2>/dev/null | grep -qx "$AVD"; then
      echo no | "$AVDMANAGER" create avd -n "$AVD" -k "$IMAGE" -d pixel_7
      # Hardware keyboard + a bit more RAM for IL2CPP builds.
      cfg="$ANDROID_AVD_HOME/$AVD.avd/config.ini"
      sed -i '' -e 's/^hw.keyboard=.*/hw.keyboard=yes/' -e 's/^hw.ramSize=.*/hw.ramSize=4096/' "$cfg"
      grep -q '^hw.keyboard=' "$cfg" || echo "hw.keyboard=yes" >> "$cfg"
    fi
    echo "AVD '$AVD' ready ($IMAGE). Start it with: tools/android-emulator.sh start" ;;
  start)
    [[ -x "$EMULATOR" ]] || { echo "run '$0 setup' first" >&2; exit 1; }
    nohup "$EMULATOR" -avd "$AVD" -no-snapshot-save -gpu host > "$HOME_DIR/emulator.log" 2>&1 &
    echo "Booting $AVD (log: AndroidEmulator/emulator.log)…"
    wait_boot
    echo "Emulator ready." ;;
  install)
    apk="${2:-$(ls -t "$ROOT"/Builds/Android/*.apk 2>/dev/null | head -1)}"
    [[ -f "$apk" ]] || { echo "no APK found; build one with ./build.sh android-dev" >&2; exit 1; }
    wait_boot
    "$ADB" install -r "$apk"
    "$ADB" shell monkey -p "$PACKAGE" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
    echo "Installed and launched $(basename "$apk") ($PACKAGE)" ;;
  stop) "$ADB" emu kill ;;
  *) echo "usage: $0 setup|start|install [apk]|stop" >&2; exit 2 ;;
esac
