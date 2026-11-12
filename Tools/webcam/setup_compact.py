"""Download the pinned QEV-0.5B bundle; no camera access."""
import argparse
from pathlib import Path

MODEL_ID = 'ken-jo/QEV-0.5B'
MODEL_REVISION = '0d99aa34b354a81a4b963cb3059b75466174fd54'
SOURCE_REVISION = 'e091f7ae8c6f064860d59be560faa27f0387ff35'

def main():
    from huggingface_hub import snapshot_download
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model-dir', type=Path, required=True)
    args = parser.parse_args()
    snapshot_download(MODEL_ID, revision=MODEL_REVISION, local_dir=args.model_dir)
    print('QEV-0.5B Compact cached. The native loader verifies bundle hashes at startup.')

if __name__ == '__main__':
    main()
