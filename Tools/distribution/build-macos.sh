#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/../.."
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.4.5f1/Unity.app/Contents/MacOS/Unity}"
command -v uv >/dev/null || { echo 'Build machine requires uv. Players do not.' >&2; exit 1; }
uv run --no-project --python 3.12.11 python Tools/distribution/prepare_runtime.py "$@"
"$UNITY_EDITOR" -batchmode -quit -projectPath "$PWD" -executeMethod CampusCommons.Webcam.Editor.DistributionBuild.Mac -logFile "$PWD/.distribution-work/unity-macos-build.log"
echo "Ready: $PWD/Builds/CampusCommons-macos-arm64/CampusCommons.app"
