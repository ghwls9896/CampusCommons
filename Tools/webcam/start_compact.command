#!/bin/zsh
set -eu
cd -- "${0:A:h}/../.."
QEV_COMPACT_DIR="${QEV_COMPACT_DIR:-/private/tmp/campus-qev-compact}"
export PYTORCH_ENABLE_MPS_FALLBACK=1
exec "$QEV_COMPACT_DIR/.venv/bin/python" Tools/webcam/live.py \
  --model-dir "$QEV_COMPACT_DIR/model" --cache-dir "$QEV_COMPACT_DIR/hf-cache" \
  --variant compact --port 8770
