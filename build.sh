#!/usr/bin/env bash
# Command-line builds. Usage:
#   ./build.sh android-dev | android-release | ios | tests | validate | setup
# UNITY env var overrides the editor path. Android signing via env vars (see BUILD.md).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
VERSION="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt")"
UNITY="${UNITY:-}"
if [[ -z "$UNITY" ]]; then
  for c in "/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity" \
           "/Volumes/SSD/Apps/UnityEditors/$VERSION/Unity.app/Contents/MacOS/Unity" \
           "$HOME/Unity/Hub/Editor/$VERSION/Editor/Unity"; do
    [[ -x "$c" ]] && UNITY="$c" && break
  done
fi
[[ -x "$UNITY" ]] || { echo "Unity $VERSION not found; set UNITY=/path/to/Unity" >&2; exit 1; }
mkdir -p "$ROOT/Logs" "$ROOT/TestResults"
run() { "$UNITY" -batchmode -nographics -projectPath "$ROOT" -logFile "$ROOT/Logs/$1.log" "${@:2}"; }
case "${1:-}" in
  setup)           run setup -quit -executeMethod Game.EditorTools.ProjectSetup.RunAllBatch ;;
  validate)        run validate -quit -executeMethod Game.EditorTools.ConfigValidatorMenu.ValidateBatch ;;
  tests)           run tests-edit -runTests -testPlatform EditMode -testResults "$ROOT/TestResults/editmode.xml"
                   run tests-play -runTests -testPlatform PlayMode -testResults "$ROOT/TestResults/playmode.xml" ;;
  android-dev)     run build-android-dev -quit -executeMethod Game.EditorTools.BuildScript.BuildAndroidDev ;;
  android-release) run build-android-release -quit -executeMethod Game.EditorTools.BuildScript.BuildAndroidRelease ;;
  ios)             run build-ios -quit -executeMethod Game.EditorTools.BuildScript.BuildIos ;;
  ios-sim)         run build-ios-sim -quit -executeMethod Game.EditorTools.BuildScript.BuildIosSimulator ;;
  *) echo "usage: $0 setup|validate|tests|android-dev|android-release|ios|ios-sim" >&2; exit 2 ;;
esac
