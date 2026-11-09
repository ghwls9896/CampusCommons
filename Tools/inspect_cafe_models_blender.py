import bpy
import json
from pathlib import Path

root = Path.cwd() / 'Assets' / 'UserModels_CafeUpdate'
models = {
    'door': root / 'door-wooden-door-metal/source/door wooden.fbx',
    'grinder': root / 'canarian-cafe-coffee-grinder/source/grinder-for export.fbx',
    'grey-carpet': root / 'grey-carpet/source/extracted/model.fbx',
    'round-carpet': root / 'round-carpet/source/extracted/round-carpet.obj',
    'chandelier': root / 'victorian-chandelier/source/lamparaTecho.fbx',
}
report = {}
for name, path in models.items():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    if path.suffix.lower() == '.fbx':
        bpy.ops.import_scene.fbx(filepath=str(path))
    else:
        bpy.ops.wm.obj_import(filepath=str(path))
    bpy.context.view_layer.update()
    objects = []
    for obj in bpy.data.objects:
        if obj.type != 'MESH':
            continue
        objects.append({
            'name': obj.name,
            'dimensions': [round(x, 4) for x in obj.dimensions],
            'location': [round(x, 4) for x in obj.location],
            'vertices': len(obj.data.vertices),
            'materials': [s.material.name if s.material else None for s in obj.material_slots],
        })
    report[name] = objects
Path('Tools/cafe-model-inspection.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print('CAFE_MODEL_INSPECTION_OK')
