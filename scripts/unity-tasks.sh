#!/usr/bin/env bash
# Headless Unity tasks for the UN SDG Discovery Hall project.
#
# Usage:
#   ./scripts/unity-tasks.sh setup        # URP + media download + build scene + XR config
#   ./scripts/unity-tasks.sh build-quest  # Android APK -> Builds/SDGDiscoveryHall.apk
#   ./scripts/unity-tasks.sh build-webgl  # WebGL player -> Builds/WebGL/
#
# Unity is located via $UNITY_PATH, or auto-detected from the Unity Hub
# default install locations for the project's editor version.
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT_ROOT/ProjectSettings/ProjectVersion.txt")"

find_unity() {
  if [[ -n "${UNITY_PATH:-}" ]]; then
    echo "$UNITY_PATH"
    return
  fi
  local candidates=(
    "/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"
    "$HOME/Unity/Hub/Editor/$VERSION/Editor/Unity"
    "/opt/unityhub/Editor/$VERSION/Editor/Unity"
    "/c/Program Files/Unity/Hub/Editor/$VERSION/Editor/Unity.exe"
    "C:/Program Files/Unity/Hub/Editor/$VERSION/Editor/Unity.exe"
  )
  for c in "${candidates[@]}"; do
    if [[ -x "$c" || -f "$c" ]]; then
      echo "$c"
      return
    fi
  done
  echo "ERROR: Unity $VERSION not found. Set UNITY_PATH to your Unity executable." >&2
  exit 1
}

run_unity() {
  local method="$1"
  local unity
  unity="$(find_unity)"
  echo ">> Unity: $unity"
  echo ">> Method: $method  (log streaming below; first run imports packages and can take a while)"
  "$unity" -batchmode -nographics -quit \
    -projectPath "$PROJECT_ROOT" \
    -executeMethod "$method" \
    -logFile -
}

case "${1:-}" in
  setup)        run_unity NSFGrant.EditorTools.CiTools.SetupProject ;;
  build-quest)  run_unity NSFGrant.EditorTools.CiTools.BuildQuest ;;
  build-webgl)  run_unity NSFGrant.EditorTools.CiTools.BuildWebGL ;;
  *)
    echo "Usage: $0 {setup|build-quest|build-webgl}" >&2
    exit 64
    ;;
esac
