#!/bin/bash
set -eu
UNITY_MONO='/Applications/Unity/Hub/Editor/6000.4.5f1/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge'
"$UNITY_MONO/bin/mono" "$UNITY_MONO/lib/mono/4.5/mcs.exe" -out:/private/tmp/cc-study-core.exe Assets/Webcam/Runtime/StudyTracker.cs Tools/study/StudyTests.cs
"$UNITY_MONO/bin/mono" /private/tmp/cc-study-core.exe
