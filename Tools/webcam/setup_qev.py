"""Download the pinned native QEV checkpoint once; no camera access."""
import argparse
import json
from pathlib import Path

from qev_backend import MODEL_ID, MODEL_REVISION


def main():
    from huggingface_hub import snapshot_download
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model-dir', type=Path, required=True)
    parser.add_argument('--cache-dir', type=Path, required=True)
    args = parser.parse_args()
    snapshot_download(MODEL_ID, revision=MODEL_REVISION, local_dir=args.model_dir,
                      allow_patterns=['manifest.json', 'head.safetensors', 'LICENSE', 'README.md'])
    manifest = json.loads((args.model_dir / 'manifest.json').read_text())
    backbone = manifest['backbone']
    if (backbone['model_id'] != MODEL_ID or
            backbone['revision'] != '4a245fab7757f2d0f82abb4df69fa294847c6f66' or
            manifest['default_mode'] != 'accurate'):
        raise ValueError('Unexpected native QEV checkpoint identity.')
    snapshot_download(MODEL_ID, revision=backbone['revision'], cache_dir=args.cache_dir,
                      allow_patterns=['*.json', '*.jinja', 'model.safetensors'])
    print('QEV-0.8B accurate is cached. live.py can now run offline.')


if __name__ == '__main__':
    main()
