#!/bin/zsh
set -eu
QEV_WORK_DIR="${CAMPUS_QEV_HOME:-$HOME/.campuscommons/qev}"
export PYTORCH_ENABLE_MPS_FALLBACK=1
exec "$QEV_WORK_DIR/.venv/bin/python" "$QEV_WORK_DIR/app/live.py" \
  --model-dir "$QEV_WORK_DIR/model" --cache-dir "$QEV_WORK_DIR/hf-cache/hub" --port 8770
