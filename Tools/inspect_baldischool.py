"""Read-only inspection of the user-provided FBX for placement and material mapping."""
import bpy, os, json
from mathutils import Vector
root=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
source=r'C:\Users\ghwls\Downloads\baldi\source\School.fbx'
out=os.path.join(root,'Design','GrandLibrary','school-inspection.json')
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=source, use_image_search=False)
objects=[]; lo=Vector((float('inf'),)*3); hi=Vector((float('-inf'),)*3)
for ob in bpy.context.scene.objects:
    if ob.type!='MESH': continue
    corners=[ob.matrix_world @ Vector(c) for c in ob.bound_box]
    for p in corners:
        for i in range(3): lo[i]=min(lo[i],p[i]); hi[i]=max(hi[i],p[i])
    mats=[]
    for slot in ob.material_slots:
        if slot.material:
            m=slot.material
            nodes=[{'node':n.name,'image':n.image.name,'file':n.image.filepath} for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image] if m.use_nodes else []
            mats.append({'name':m.name,'textures':nodes})
    objects.append({'name':ob.name,'location':list(ob.location),'dimensions':list(ob.dimensions),'materials':mats,'vertices':len(ob.data.vertices),'polygons':len(ob.data.polygons)})
summary={'source':source,'object_count':len(objects),'bounds_min':list(lo),'bounds_max':list(hi),'size':list(hi-lo),'objects':objects}
os.makedirs(os.path.dirname(out),exist_ok=True)
with open(out,'w',encoding='utf-8') as f: json.dump(summary,f,ensure_ascii=False,indent=2)
print('SCHOOL_INSPECTION',json.dumps({'object_count':len(objects),'min':list(lo),'max':list(hi),'size':list(hi-lo),'objects':[(o['name'],o['vertices']) for o in objects[:20]]}))
