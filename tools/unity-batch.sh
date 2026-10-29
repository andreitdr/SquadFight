#!/usr/bin/env bash
# Developer helper: compile / run a method / run tests in Unity batch mode and summarise results.
#   tools/unity-batch.sh compile
#   tools/unity-batch.sh exec Namespace.Class.Method
#   tools/unity-batch.sh test EditMode|PlayMode [filter]
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt")"
UNITY="${UNITY:-/Volumes/SSD/Apps/UnityEditors/$VERSION/Unity.app/Contents/MacOS/Unity}"
mkdir -p "$ROOT/Logs" "$ROOT/TestResults"
LOG="$ROOT/Logs/batch-$1.log"
summarize() {
  grep -E "error CS|warning CS" "$LOG" | grep -v "Library/PackageCache" | sort -u | head -50
  grep -E "Scripts have compiler errors|executeMethod.*(failed|could not)|Exception" "$LOG" | head -20
}
case "$1" in
  compile) "$UNITY" -batchmode -nographics -quit -projectPath "$ROOT" -logFile "$LOG"; code=$?; summarize ;;
  exec)    "$UNITY" -batchmode -nographics -quit -projectPath "$ROOT" -logFile "$LOG" -executeMethod "$2"; code=$?; summarize ;;
  test)
    RES="$ROOT/TestResults/$2.xml"; rm -f "$RES"
    extra=(); [[ -n "${3:-}" ]] && extra=(-testFilter "$3")
    "$UNITY" -batchmode -nographics -projectPath "$ROOT" -logFile "$LOG" -runTests -testPlatform "$2" -testResults "$RES" ${extra[@]+"${extra[@]}"}; code=$?
    summarize
    if [[ -f "$RES" ]]; then
      python3 - "$RES" <<'PY'
import sys, xml.etree.ElementTree as ET
r = ET.parse(sys.argv[1]).getroot()
print("RESULT", r.get("result"), "total", r.get("total"), "passed", r.get("passed"), "failed", r.get("failed"), "skipped", r.get("skipped"))
for tc in r.iter("test-case"):
    if tc.get("result") == "Failed":
        msg = tc.find("failure/message")
        print("FAIL", tc.get("fullname"), "::", (msg.text or "").strip()[:400] if msg is not None else "")
PY
    else echo "no results file"; fi ;;
esac
echo "exit=$code"
exit $code
