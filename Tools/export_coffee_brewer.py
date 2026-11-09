import bpy
from mathutils import Vector
from pathlib import Path

root = Path(__file__).parent / 'DownloadedCafeModels' / 'CoffeeCart_01'
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
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
parts = []
for part in bpy.context.selected_objects:
    if part.type != 'MESH':
        continue
    pts = [part.matrix_world @ Vector(v) for v in part.bound_box]
    if min(v.z for v in pts) >= 1.035 and sum(v.x for v in pts) / len(pts) < .05:
        parts.append(part)
bpy.ops.object.select_all(action='DESELECT')
for part in parts:
    part.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
obj = bpy.context.object
obj.name = 'Coffee brewer and carafes'
bpy.context.view_layer.update()
pts = [obj.matrix_world @ Vector(v) for v in obj.bound_box]
cx = (min(p.x for p in pts) + max(p.x for p in pts)) * .5
cy = (min(p.y for p in pts) + max(p.y for p in pts)) * .5
z = min(p.z for p in pts)
obj.location -= Vector((cx, cy, z))
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
out = root / 'CoffeeBrewer_1k.fbx'
bpy.ops.export_scene.fbx(filepath=str(out), use_selection=True,
    object_types={'MESH'}, add_leaf_bones=False, axis_forward='-Z', axis_up='Y')
print('EXPORTED', out, 'parts', len(parts), 'bounds', obj.dimensions[:])
