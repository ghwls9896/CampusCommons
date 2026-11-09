import bpy, os, json
from mathutils import Vector
root=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
base=os.path.join(root,'Assets','GrandLibrary','Realism')
ids=['sofa_02','mid_century_lounge_chair','dining_chair_02','round_wooden_table_01','wooden_table_02','wooden_bookshelf_worn','modern_ceiling_lamp_01','desk_lamp_arm_01']
result=[]
for name in ids:
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=os.path.join(base,name,name+'.fbx'),use_image_search=False)
    lo=Vector((float('inf'),)*3); hi=Vector((float('-inf'),)*3); objects=[]
    for ob in bpy.context.scene.objects:
        if ob.type!='MESH': continue
        for point in ob.bound_box:
            p=ob.matrix_world @ Vector(point)
            for i in range(3): lo[i]=min(lo[i],p[i]); hi[i]=max(hi[i],p[i])
        objects.append({'name':ob.name,'vertices':len(ob.data.vertices),'materials':[s.material.name if s.material else '' for s in ob.material_slots]})
    result.append({'id':name,'min':list(lo),'max':list(hi),'size':list(hi-lo),'objects':objects})
    print('MODEL',name,'size',list(hi-lo),'meshes',len(objects))
out=os.path.join(root,'Design','GrandLibrary','realism-asset-inspection.json')
with open(out,'w',encoding='utf-8') as f: json.dump(result,f,indent=2)
