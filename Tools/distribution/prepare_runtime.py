"""Build-time only: produce an offline, relocatable Python/QEV package on its target OS.
Players never run this script or need Python, uv, Git or pip installed.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import subprocess
import zipfile

REPO = Path(__file__).resolve().parents[2]
SOURCE = REPO / 'Tools/webcam'
PYTHON_VERSION = '3.12.11'
MACOS_MINIMUM = (15, 0)
BACKBONE = '4a245fab7757f2d0f82abb4df69fa294847c6f66'
MODEL = 'ken-jo/QEV-0.8B'
APP_FILES = ('live.py','qev_backend.py','index.html','app.js','style.css')


def platform_id(system=None, machine=None):
    pair = (system or platform.system(), (machine or platform.machine()).lower())
    if pair == ('Darwin','arm64'): return 'macos-arm64'
    if pair in [('Windows','amd64'),('Windows','x86_64')]: return 'windows-x64'
    raise ValueError('Supported builders: Apple Silicon macOS or x64 Windows. Intel Mac/Windows ARM are not verified.')


def clean_env(original=None):
    env = dict(os.environ if original is None else original)
    for key in ('PYTHONPATH','PYTHONHOME','VIRTUAL_ENV','CONDA_PREFIX'): env.pop(key,None)
    env.update(PYTHONNOUSERSITE='1',PYTHONUTF8='1',PYTHONDONTWRITEBYTECODE='1')
    return env


def sha256(path):
    h=hashlib.sha256()
    with Path(path).open('rb') as f:
        for data in iter(lambda:f.read(1024*1024),b''): h.update(data)
    return h.hexdigest()


def python_relative(target):
    return 'python/python.exe' if target=='windows-x64' else 'python/bin/python3.12'


def validate_macos_wheels(root):
    for wheel in (Path(root)/'python').rglob('*.dist-info/WHEEL'):
        for major,minor in re.findall(r'macosx_(\d+)_(\d+)_',wheel.read_text(encoding='utf-8')):
            if (int(major),int(minor)) > MACOS_MINIMUM:
                raise ValueError('Native dependency requires newer macOS: '+wheel.parent.name)


def archive_payload(root, output, target):
    root,output=Path(root),Path(output)
    if target == "macos-arm64": validate_macos_wheels(root)
    for name in (python_relative(target),'app/live.py','model/manifest.json'):
        if not (root/name).is_file(): raise ValueError('Missing payload file: '+name)
    files=sorted(p for p in root.rglob('*') if p.is_file())
    if any(p.is_symlink() for p in files): raise ValueError('Payload must not contain symlinks')
    paths=[p.relative_to(root).as_posix() for p in files]
    aliases=set()
    for path in paths:
        # Check directory prefixes too: macOS/Windows commonly use case-insensitive disks.
        parts=path.split('/')
        for i in range(1,len(parts)+1):
            prefix='/'.join(parts[:i]);aliases.add(prefix)
    if len({p.casefold() for p in aliases})!=len(aliases): raise ValueError('Case-colliding payload paths')
    output.mkdir(parents=True,exist_ok=True)
    temp=output/'payload.zip.partial'
    executable=[p.relative_to(root).as_posix() for p in files if p.stat().st_mode & 0o111]
    with zipfile.ZipFile(temp,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=1,allowZip64=True) as archive:
        for p in files: archive.write(p,p.relative_to(root).as_posix())
    final=output/'payload.zip';os.replace(temp,final)
    manifest={'schema':1,'platform':target,'archive':'payload.zip','sha256':sha256(final),
              'archiveBytes':final.stat().st_size,'unpackedBytes':sum(p.stat().st_size for p in files),
              'python':python_relative(target),'executablePaths':executable,
              'minimumOS':'.'.join(map(str,MACOS_MINIMUM)) if target=='macos-arm64' else '10.0'}
    manifest_tmp=output/'manifest.json.partial';manifest_tmp.write_text(json.dumps(manifest,indent=2)+'\n')
    os.replace(manifest_tmp,output/'manifest.json')
    return manifest


def copy_models(source,root):
    data=json.loads((source/'model/manifest.json').read_text())
    if data['backbone']['model_id']!=MODEL or data['backbone']['revision']!=BACKBONE:
        raise ValueError('Unexpected cached backbone revision')
    shutil.copytree(source/'model',root/'model',ignore=shutil.ignore_patterns('.cache'),dirs_exist_ok=True,symlinks=False)
    # Keep only the pinned snapshot, materializing symlinks. No absolute cache links or duplicate blobs.
    rel=Path('models--ken-jo--QEV-0.8B')/'snapshots'/BACKBONE
    src=source/'hf-cache/hub'/rel
    if not (src/'model.safetensors').is_file(): raise ValueError('Pinned model cache missing')
    shutil.copytree(src,root/'hf-cache/hub'/rel,symlinks=False,dirs_exist_ok=True)


def run(args,env=None,cwd=None):
    subprocess.run([str(a) for a in args],check=True,env=env or clean_env(),cwd=cwd)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work-dir',type=Path,default=REPO/'.distribution-work')
    parser.add_argument('--output',type=Path,default=REPO/'Assets/StreamingAssets/StudyRuntime')
    parser.add_argument('--model-source',type=Path,help='Existing QEV model/cache to reuse; no user images are copied')
    parser.add_argument('--python-home',type=Path,help='Existing uv-managed Python3.12.11 distribution to copy')
    parser.add_argument('--reuse-prepared',action='store_true',help='Reuse a prepared payload, then revalidate/relocate it')
    args=parser.parse_args();target=platform_id();work=args.work_dir.resolve()/target
    work.mkdir(parents=True,exist_ok=True);root=work/'payload'
    uv=shutil.which('uv')
    if not uv: raise SystemExit('Build machine requires uv (official Astral package). Players do not.')
    if not args.reuse_prepared:
        if root.exists(): shutil.rmtree(root)
        managed=args.python_home
        if managed is None:
            managed_dir=work/'managed-python'
            triple='macos-aarch64-none' if target=='macos-arm64' else 'windows-x86_64-none'
            run([uv,'python','install','cpython-'+PYTHON_VERSION+'-'+triple,'--install-dir',managed_dir,'--no-bin','--no-registry'])
            managed=managed_dir/('cpython-'+PYTHON_VERSION+'-'+triple)
        if not managed.is_dir(): raise SystemExit('Managed Python directory missing')
        shutil.copytree(managed,root/'python',symlinks=False)
        # This is our private build copy, not uv's managed/system interpreter.
        for marker in (root/'python').rglob('EXTERNALLY-MANAGED'): marker.unlink()
        python=root/python_relative(target)
        run([python,'-I','-c',"import sys;assert sys.version_info[:3]==(3,12,11)"])
        run([uv,'pip','install','--python',python,'-r',SOURCE/'requirements-qev.txt'])
        (root/'app').mkdir()
        for name in APP_FILES: shutil.copy2(SOURCE/name,root/'app'/name)
        for name in ['QEV-NOTICE.txt','QEV-LICENSE.txt']: shutil.copy2(SOURCE/name,root/name)
        if args.model_source: copy_models(args.model_source.resolve(),root)
        else:
            run([python,SOURCE/'setup_qev.py','--model-dir',root/'model','--cache-dir',work/'download-cache'])
            rel=Path('models--ken-jo--QEV-0.8B')/'snapshots'/BACKBONE
            shutil.copytree(work/'download-cache'/rel,root/'hf-cache/hub'/rel,symlinks=False)
        # Record exact native dependency provenance without including credentials.
        result=subprocess.check_output([uv,'pip','freeze','--python',str(python)],env=clean_env(),text=True)
        (root/'DEPENDENCIES.txt').write_text(result)
    # Rename the whole payload before launching it: catches build-machine absolute paths.
    relocated=work/'relocated runtime 한글'
    if relocated.exists(): raise SystemExit('Previous interrupted relocation exists; inspect work directory before retrying')
    root.rename(relocated)
    try:
        env=clean_env();env.update(HF_HUB_OFFLINE='1',TRANSFORMERS_OFFLINE='1',PYTORCH_ENABLE_MPS_FALLBACK='1')
        run([relocated/python_relative(target),'-s',Path(__file__).with_name('smoke_runtime.py'),relocated],env=env)
    finally: relocated.rename(root)
    manifest=archive_payload(root,args.output.resolve(),target)
    print('OFFLINE_RUNTIME_PACKAGE_READY',json.dumps({k:v for k,v in manifest.items() if k!='executablePaths'}),flush=True)

if __name__=='__main__':main()
