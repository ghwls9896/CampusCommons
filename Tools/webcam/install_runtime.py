"""Install only QEV-0.8B fast in a persistent per-user Python 3.12 environment."""
import argparse
from pathlib import Path
import shutil
import subprocess
import sys
import venv

APP_FILES = ('live.py', 'qev_backend.py', 'index.html', 'app.js', 'style.css')

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime-dir',type=Path,default=Path.home()/'.campuscommons'/'qev')
    args=parser.parse_args()
    if sys.version_info[:2] != (3,12):
        parser.error('Use Python 3.12')
    root=args.runtime_dir.expanduser().resolve();source=Path(__file__).resolve().parent
    venv.EnvBuilder(with_pip=True).create(root/'.venv')
    python=root/'.venv'/('Scripts/python.exe' if sys.platform=='win32' else 'bin/python')
    subprocess.run([str(python),'-m','pip','install','-r',str(source/'requirements-qev.txt')],check=True)
    (root/'app').mkdir(exist_ok=True)
    for name in APP_FILES:shutil.copy2(source/name,root/'app'/name)
    subprocess.run([str(python),str(source/'setup_qev.py'),'--model-dir',str(root/'model'),
                    '--cache-dir',str(root/'hf-cache'/'hub')],check=True)
    print('QEV fast installed:',root)

if __name__=='__main__':main()
