#!/usr/bin/env bash
# All tests, locally (there is no GitHub CI): pure C# tests, content validation + progression simulator,
# then Unity EditMode + PlayMode. Run before every push to main. Close the Unity Editor first.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
dotnet test DotnetTests/GameTests --nologo -v quiet
dotnet run -c Release --project DotnetTests/BalanceCli -- validate
dotnet run -c Release --project DotnetTests/BalanceCli -- progression 1,2,3 > /dev/null
./build.sh tests
for f in TestResults/editmode.xml TestResults/playmode.xml; do
  line="$(grep -o '<test-run[^>]*' "$f")"
  result="$(sed -n 's/.*result="\([^"]*\)".*/\1/p' <<< "$line")"
  echo "$f: $(grep -oE '(total|passed|failed|skipped)="[0-9]+"' <<< "$line" | tr '\n' ' ')"
  [[ "$result" == Passed* ]] || { echo "Unity tests failed: $f" >&2; exit 1; }
done
echo "All tests passed."
