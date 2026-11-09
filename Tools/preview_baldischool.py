import bpy, os, math
from mathutils import Vector
root=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
source=r'C:\Users\ghwls\Downloads\baldi\source\School.fbx'
textures=r'C:\Users\ghwls\Downloads\baldi\textures'
out=os.path.join(root,'Design','GrandLibrary')
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=source,use_image_search=False)
for m in bpy.data.materials:
    if not m.use_nodes: continue
    for n in m.node_tree.nodes:
        if n.type=='TEX_IMAGE' and n.image:
            path=os.path.join(textures,os.path.basename(n.image.filepath))
            if os.path.exists(path): n.image=bpy.data.images.load(path,check_existing=True)
for ob in bpy.context.scene.objects:
    if ob.type=='MESH' and ('ceiling' in ob.name.lower() or 'roof' in ob.name.lower()): ob.hide_render=True
scene=bpy.context.scene
scene.render.engine='CYCLES'; scene.cycles.samples=8; scene.cycles.use_denoising=True
scene.world.color=(.7,.7,.7)
bpy.ops.object.light_add(type='SUN',location=(0,0,150)); bpy.context.object.rotation_euler=(.15,-.2,.3); bpy.context.object.data.energy=3
bpy.ops.object.camera_add(location=(0,-165,650)); cam=bpy.context.object; cam.rotation_euler=(0,0,0); cam.data.type='ORTHO'; cam.data.ortho_scale=480; scene.camera=cam
scene.render.resolution_x=1100; scene.render.resolution_y=1100; scene.render.resolution_percentage=100
scene.render.filepath=os.path.join(out,'school-top-inspection.png'); bpy.ops.render.render(write_still=True)
print('SCHOOL_PREVIEW_OK')
