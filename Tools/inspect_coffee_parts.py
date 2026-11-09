import bpy
from pathlib import Path
from mathutils import Vector
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
root = Path(__file__).parent / 'DownloadedCafeModels' / 'CoffeeCart_01'
bpy.ops.import_scene.fbx(filepath=str(root / 'CoffeeCart_01_1k.fbx'))
obj = bpy.data.objects['CoffeeCart_01_props']
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.separate(type='LOOSE')
bpy.ops.object.mode_set(mode='OBJECT')
bpy.context.view_layer.update()
parts = sorted((o for o in bpy.context.selected_objects if o.type == 'MESH'), key=lambda o: len(o.data.polygons), reverse=True)
print('PARTS', len(parts))
for part in parts[:35]:
    pts = [part.matrix_world @ Vector(v) for v in part.bound_box]
    lo = [min(p[i] for p in pts) for i in range(3)]
    hi = [max(p[i] for p in pts) for i in range(3)]
    print('PART', part.name, 'faces', len(part.data.polygons), 'min', [round(x, 2) for x in lo], 'max', [round(x, 2) for x in hi])
