#!/bin/zsh
set -eu
cd -- "${0:A:h}/../.."
QEV_WORK_DIR="${QEV_WORK_DIR:-/private/tmp/campus-qev}"
export PYTORCH_ENABLE_MPS_FALLBACK=1
exec "$QEV_WORK_DIR/.venv/bin/python" Tools/webcam/live.py \
  --model-dir "$QEV_WORK_DIR/model" --cache-dir "$QEV_WORK_DIR/hf-cache/hub" --port 8770
