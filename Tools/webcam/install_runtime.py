"""Install only QEV-0.8B fast in a persistent per-user Python 3.12 environment."""
import argparse
from pathlib import Path
import shutil
import subprocess
import sys
import venv

APP_FILES = ('live.py', 'qev_backend.py', 'desktop_capture.py', 'screen_study.py',
             'index.html', 'app.js', 'style.css')


def install_app(destination):
    source = Path(__file__).resolve().parent
    destination.mkdir(parents=True, exist_ok=True)
    if sys.platform == 'darwin':
        helper = destination / 'study-window'
        temporary = helper.with_suffix('.building')
        try:
            subprocess.run(['xcrun', 'swiftc', '-O', '-target', 'arm64-apple-macosx15.0',
                            str(source / 'study_window.swift'), '-o', str(temporary)], check=True)
            temporary.replace(helper)
        finally:
            temporary.unlink(missing_ok=True)
    for name in APP_FILES:
        shutil.copy2(source / name, destination / name)

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime-dir',type=Path,default=Path.home()/'.campuscommons'/'qev')
    parser.add_argument('--app-only', action='store_true', help='Refresh app files and the macOS helper without downloading the model')
    parser.add_argument('--app-dir', type=Path, help='Stage only client app files for an offline distribution build')
    args=parser.parse_args()
    if args.app_dir:
        install_app(args.app_dir)
        return
    if sys.version_info[:2] != (3,12):
        parser.error('Use Python 3.12')
    root=args.runtime_dir.expanduser().resolve();source=Path(__file__).resolve().parent
    if args.app_only:
        install_app(root / 'app')
        return
    venv.EnvBuilder(with_pip=True).create(root/'.venv')
    python=root/'.venv'/('Scripts/python.exe' if sys.platform=='win32' else 'bin/python')
    subprocess.run([str(python),'-m','pip','install','-r',str(source/'requirements-qev.txt')],check=True)
    install_app(root / 'app')
    subprocess.run([str(python),str(source/'setup_qev.py'),'--model-dir',str(root/'model'),
                    '--cache-dir',str(root/'hf-cache'/'hub')],check=True)
    print('QEV fast installed:',root)

if __name__=='__main__':main()
