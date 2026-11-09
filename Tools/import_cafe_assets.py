from pathlib import Path
import shutil
import zipfile

source = Path(r'C:\Users\ghwls\Downloads')
destination = Path('Assets/UserModels_CafeUpdate')
names = ('door-wooden-door-metal', 'canarian-cafe-coffee-grinder', 'grey-carpet', 'round-carpet', 'victorian-chandelier')
if destination.exists():
    raise SystemExit(f'Import destination already exists; leaving it intact: {destination}')
destination.mkdir(parents=True)
for name in names:
    shutil.copytree(source / name, destination / name)
    src = destination / name / 'source'
    for archive in src.glob('*.zip'):
        target = src / 'extracted'
        target.mkdir(exist_ok=True)
        with zipfile.ZipFile(archive) as zf:
            for entry in zf.infolist():
                path = (target / entry.filename).resolve()
                if not path.is_relative_to(target.resolve()):
                    raise ValueError(f'Unsafe archive member: {entry.filename}')
            zf.extractall(target)
print('Imported all five supplied models to', destination)
