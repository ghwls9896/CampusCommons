"""Convert the school FBX supplied by the user into Unity-ready grouped meshes.

The input folder is read only. Faces and original image textures are retained;
the many source objects are grouped by material and collision purpose.
"""
import bpy, os, json, shutil, mathutils

ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE=r'C:\Users\ghwls\Downloads\baldi'
SOURCE_FBX=os.path.join(SOURCE,'source','School.fbx')
SOURCE_TEX=os.path.join(SOURCE,'textures')
DEST=os.path.join(ROOT,'Assets','GrandLibrary','UserSchool')
MODELS=os.path.join(DEST,'Models'); TEXTURES=os.path.join(DEST,'Textures')
REPORT=os.path.join(DEST,'SchoolMaterials.json')
for directory in (MODELS,TEXTURES): os.makedirs(directory,exist_ok=True)
if not os.path.exists(SOURCE_FBX): raise RuntimeError('User-supplied School.fbx was not found')
texture_names={f.lower():f for f in os.listdir(SOURCE_TEX)}
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=SOURCE_FBX,use_image_search=False)
groups={}; original_mats={}; omitted=0
for obj in list(bpy.context.scene.objects):
    if obj.type!='MESH': continue
    n=obj.name.lower()
    # These are static, closed, two-sided source door planes. Removing them
    # leaves the source doorway openings traversable in this prototype.
    if n.startswith('_doorin') or n.startswith('_doorout'):
        bpy.data.objects.remove(obj,do_unlink=True); omitted+=1; continue
    if len(obj.material_slots)!=1 or not obj.material_slots[0].material:
        raise RuntimeError('Unexpected material layout on '+obj.name)
    mat=obj.material_slots[0].material
    name=mat.name
    original_mats[name]=mat
    solid=any(keyword in n for keyword in ('floor','wall','ceiling','desk','chair','locker','fence','grass'))
    groups.setdefault((name,solid),[]).append(obj)

entries=[]; outputs=[]
# Source's hallway doors are approximately 10 units tall: 0.25 gives a 2.5m opening.
# X is reflected for Unity's FBX importer; world Z is centered around the map.
normalise=mathutils.Matrix.Translation((0,-41.25,0)) @ mathutils.Matrix.Diagonal((-.25,-.25,.25,1))
for i,((matname,solid),objects) in enumerate(sorted(groups.items(),key=lambda item:(item[0][0],item[0][1]))):
    # The source FBX contains some unusual custom data; rebuilding the mesh
    # explicitly avoids depending on Blender's object-join custom-data path.
    vertices=[]; faces=[]; uvs=[]
    for source in objects:
        mesh=source.data; transform=normalise @ source.matrix_world
        offset=len(vertices); flip=transform.determinant()<0
        vertices.extend([tuple(transform @ v.co) for v in mesh.vertices])
        active_uv=mesh.uv_layers.active.data if mesh.uv_layers.active else None
        for poly in mesh.polygons:
            loop_ids=list(poly.loop_indices)
            if flip: loop_ids.reverse()
            faces.append(tuple(offset+mesh.loops[j].vertex_index for j in loop_ids))
            if active_uv: uvs.extend([tuple(active_uv[j].uv) for j in loop_ids])
            else: uvs.extend([(0,0)]*len(loop_ids))
    mesh=bpy.data.meshes.new('school_mesh_%03d'%i)
    mesh.from_pydata(vertices,[],faces); mesh.update()
    if len(mesh.loops)!=len(uvs): raise RuntimeError('UV length mismatch in '+matname)
    uv=mesh.uv_layers.new(name='UVMap')
    for n,p in enumerate(uvs): uv.data[n].uv=p
    obj=bpy.data.objects.new('SCHOOL_%03d_%s'%(i,'SOLID' if solid else 'DETAIL'),mesh)
    bpy.context.scene.collection.objects.link(obj)
    texfile=''; mat=original_mats[matname]
    if mat.use_nodes:
        for node in mat.node_tree.nodes:
            if node.type!='TEX_IMAGE' or not node.image: continue
            basename=os.path.basename(node.image.filepath)
            matched=texture_names.get(basename.lower())
            if matched:
                texfile=matched
                shutil.copy2(os.path.join(SOURCE_TEX,matched),os.path.join(TEXTURES,matched))
                break
    color=list(mat.diffuse_color)
    obj.data.materials.append(mat)
    entries.append(dict(index=i,sourceMaterial=matname,texture=texfile,color=color,solid=solid,objects=len(objects)))
    outputs.append(obj)
    print('SCHOOL_BATCH',i,matname,'solid='+str(solid),'objects='+str(len(objects)),'texture='+texfile,flush=True)
with open(REPORT,'w',encoding='utf-8') as f: json.dump(dict(entries=entries,omittedStaticDoorFaces=omitted,scale=.25),f,indent=2)
bpy.ops.object.select_all(action='DESELECT')
for obj in outputs: obj.select_set(True)
tmp=os.path.join(ROOT,'Design','GrandLibrary','UserSchool-export-temp.fbx')
bpy.ops.export_scene.fbx(filepath=tmp,use_selection=True,object_types={'MESH'},
                         axis_forward='-Z',axis_up='Y',bake_space_transform=False,
                         add_leaf_bones=False,use_mesh_modifiers=True,path_mode='AUTO')
os.replace(tmp,os.path.join(MODELS,'BaldiSchool.fbx'))
print('USER_SCHOOL_BUILD_OK batches=%d omitted_closed_door_faces=%d'%(len(outputs),omitted))
