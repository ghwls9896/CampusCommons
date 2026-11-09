import bpy
from pathlib import Path
from mathutils import Vector

root = Path(__file__).parent / 'DownloadedCafeModels'
for name in ('CoffeeCart_01', 'tea_set_01', 'standing_chalkboard_01'):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(root / name / (name + '_1k.fbx')))
    bpy.context.view_layer.update()
    print('MODEL', name)
    for obj in bpy.data.objects:
        if obj.type != 'MESH':
            continue
        points = [obj.matrix_world @ Vector(v) for v in obj.bound_box]
        lo = [min(p[i] for p in points) for i in range(3)]
        hi = [max(p[i] for p in points) for i in range(3)]
        print('MESH', obj.name, 'min', [round(x, 3) for x in lo],
              'max', [round(x, 3) for x in hi],
              'faces', len(obj.data.polygons),
              'materials', [m.name for m in obj.data.materials if m])
