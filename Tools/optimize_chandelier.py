import bpy
from pathlib import Path

root = Path.cwd() / 'Assets' / 'UserModels_CafeUpdate' / 'victorian-chandelier' / 'source'
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(root / 'lamparaTecho.fbx'))
meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
for obj in bpy.context.selected_objects:
    obj.select_set(False)
for obj in meshes:
    obj.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.join()
model = bpy.context.view_layer.objects.active
model.name = 'VictorianChandelier_Combined'
bpy.ops.export_scene.fbx(filepath=str(root / 'victorian_chandelier_optimized.fbx'), use_selection=True,
                         object_types={'MESH'}, add_leaf_bones=False, axis_forward='-Z', axis_up='Y',
                         bake_space_transform=False, path_mode='AUTO')
print('Merged', len(meshes), 'meshes into one optimized chandelier')
