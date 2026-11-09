"""Run with Blender --background --python Tools/build_world.py. No external assets."""
import bpy, math, json, os
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, 'Assets', 'Campus', 'Resources', 'campus.json')
PALETTE = {
    'cream': '#EEE8D9', 'wall': '#F6F1E6', 'oak': '#B88D60',
    'dark': '#293C3A', 'sage': '#8BA991', 'mint': '#C5D7C4',
    'purple': '#A29BC4', 'lavender': '#DCD8ED', 'coral': '#D99379',
    'gold': '#E8BE70', 'white': '#FFFFFF', 'leaf': '#4C795B',
    'soil': '#554638', 'screen': '#243E49', 'rug': '#DBCEB8'
}
items, seats, labels = [], [], []
def shape(name, pos, scale, mat, kind='Cube', yaw=0, solid=True, group='Lounge'):
    items.append(dict(name=name, position=list(pos), scale=list(scale), material=mat,
                      kind=kind, yaw=yaw, solid=solid, group=group))
def box(n,x,y,z,w,h,d,m,**kw): shape(n,(x,y,z),(w,h,d),m,**kw)
def cyl(n,x,y,z,r,h,m,**kw): shape(n,(x,y,z),(r*2,h,r*2),m,'Cylinder',**kw)
def text(n, s, x,y,z, size=.5, yaw=0, mat='dark', group='Lounge'):
    labels.append(dict(name=n,text=s,position=[x,y,z],size=size,yaw=yaw,material=mat,group=group))
def rotate(x,z,yaw):
    a=math.radians(yaw)
    return x*math.cos(a)+z*math.sin(a),-x*math.sin(a)+z*math.cos(a)
def chair(n,x,z,yaw=0,mat='sage',group='Lounge'):
    def part(label,dx,y,dz,w,h,d,m):
        rx,rz=rotate(dx,dz,yaw)
        box(n+' '+label,x+rx,y,z+rz,w,h,d,m,yaw=yaw,group=group)
    part('seat',0,.52,0,.76,.18,.76,mat)
    part('back',0,.96,-.32,.76,.8,.14,mat)
    for dx in [-.27,.27]:
        for dz in [-.27,.27]: part('leg',dx,.24,dz,.065,.48,.065,'oak')
    seats.append(dict(name=n,position=[x,.62,z],yaw=yaw,group=group))
def sofa(n,x,z,yaw=0,mat='sage'):
    def p(label,dx,y,dz,w,h,d,m):
        rx,rz=rotate(dx,dz,yaw)
        box(n+' '+label,x+rx,y,z+rz,w,h,d,m,yaw=yaw)
    p('base',0,.35,0,3.5,.45,1.2,'oak')
    p('back',0,.95,-.5,3.5,1.0,.25,mat)
    for dx in [-1.1,0,1.1]:
        p('cushion',dx,.65,.04,1.04,.28,.95,mat)
        rx,rz=rotate(dx,.05,yaw)
        seats.append(dict(name=n+' seat',position=[x+rx,.81,z+rz],yaw=yaw,group='Lounge'))
    for dx in [-1.7,1.7]: p('arm',dx,.8,0,.22,.7,1.2,mat)
def table(n,x,z,w=2,d=1,h=.75,group='Lounge'):
    box(n+' top',x,h,z,w,.12,d,'oak',group=group)
    for dx in [-w*.38,w*.38]:
        for dz in [-d*.32,d*.32]: box(n+' leg',x+dx,h/2,z+dz,.09,h,.09,'dark',group=group)
def plant(n,x,z,group='Lounge'):
    cyl(n+' pot',x,.35,z,.38,.7,'cream',group=group)
    cyl(n+' stem',x,1,z,.055,1.4,'oak',group=group)
    for dx,y,dz in [(-.28,1.4,0),(.23,1.65,.12),(0,1.95,-.1)]:
        shape(n+' leaves',(x+dx,y,z+dz),(.75,.8,.65),'leaf','Sphere',solid=False,group=group)
def wall(n,x,z,w,d,group='Lounge'):
    box(n,x,1.7,z,w,3.4,d,'wall',group=group)
    box(n+' skirting',x,.12,z,w,.24,d+.04,'oak',group=group)

# Connected single-level footprint. Shared wall has two 3m openings.
box('Lounge floor',0,-.16,0,36,.32,28,'cream')
wall('West wall',-18,0,.25,28)
wall('East wall',18,0,.25,28)
wall('South wall',0,-14,36,.25)
for a,b in [(-18,-10.5),(-7.5,7.5),(10.5,18)]: wall('Shared wall', (a+b)/2,14,b-a,.25)
for x,group,accent,floor in [(-9,'Quiz','sage','mint'),(9,'Mafia','purple','lavender')]:
    box(group+' floor',x,-.16,22,16,.32,16,floor,group=group)
    wall(group+' west',x-8,22,.25,16,group)
    wall(group+' east',x+8,22,.25,16,group)
    wall(group+' north',x,30,16,.25,group)
    box(group+' lintel',x,3.08,14,3,.64,.3,accent,group=group)
    for dx in [-1.62,1.62]: box(group+' doorway',x+dx,1.4,14,.24,2.8,.4,accent,group=group)
    box(group+' threshold',x,.015,14,3,.03,1.8,accent,solid=False,group=group)
    text(group+' entrance',group.upper()+' ROOM',x,3.05,13.75,.36,group=group)
    plant(group+' plant',x+6.5,28.4,group)
    plant(group+' plant',x-6.5,28.4,group)

# Lounge: three seating clusters, study bar, bookshelves, welcome desk.
text('Lounge title','CAMPUS COMMONS',0,2.75,13.8,.7)
text('Lounge sub','MEET  /  LEARN  /  PLAY',0,2.03,13.78,.25)
for x,z,mat in [(-10,-5,'sage'),(10,-5,'coral'),(-10,6,'purple')]:
    box('Conversation rug',x,.015,z,7.5,.03,6,'rug',solid=False)
    sofa('Social sofa',x,z-1.8,0,mat)
    chair('Lounge armchair',x-2.4,z+1.3,145,mat)
    chair('Lounge armchair',x+2.4,z+1.3,215,mat)
    table('Coffee table',x,z,2.3,1.25,.48)
    cyl('Table planter',x,.68,z,.15,.28,'cream',solid=False)
    shape('Small plant',(x,.96,z),(.45,.4,.45),'leaf','Sphere',solid=False)
    plant('Cluster plant',x+3.15,z-2.2)
table('Study table',10,6,6.5,1.6)
for x in [7.8,10,12.2]:
    chair('Study chair',x,4.65,0,'sage')
    chair('Study chair',x,7.35,180,'sage')
    box('Desk notebook',x,.83,6,.65,.035,.42,'lavender',solid=False)
for x in [-15,15]:
    box('Bookcase back',x,1.25,12.9,3.8,2.5,.22,'oak')
    for y in [.2,1.0,1.8,2.6]: box('Book shelf',x,y,12.5,3.8,.1,.8,'oak')
    for dx in range(11):
        for y in [.59,1.39,2.19]: box('Book',x-1.5+dx*.29,y,12.5,.19,.65,.42,['sage','coral','purple','cream'][dx%4])
table('Welcome desk',0,-10,4,1.1,1.0)
text('Welcome sign','WELCOME',0,1.04,-10.57,.35,yaw=180)
box('Welcome mat',0,.018,-11.8,5,.036,2,'sage',solid=False)
for x,z in [(-16,-11),(16,-11),(-16,9),(16,9)]: plant('Tall plant',x,z)
# Decorative ceiling-height beams keep the open roof view readable.
for x in [-17.6,17.6]: box('Timber beam',x,3.5,0,.25,.25,28,'oak',solid=False)
for z in [-10,-2,6]:
    for x in [-17.8,17.8]:
        box('Window frame',x,1.95,z,.3,1.7,4.5,'oak')
        box('Frosted window',x+(.17 if x<0 else -.17),1.95,z,.04,1.45,4.25,'mint',solid=False)

# Quiz: twelve seats with clear center aisle and presentation area.
box('Quiz stage',-9,.12,27.6,11,.24,3,'oak',group='Quiz')
box('Quiz display frame',-9,2.1,29.68,10,2.5,.24,'dark',group='Quiz')
box('Quiz display',-9,2.1,29.52,9.65,2.18,.04,'screen',solid=False,group='Quiz')
text('Quiz screen heading','QUIZ ROOM',-9,2.45,29.46,.65,mat='white',group='Quiz')
text('Quiz screen sub','READY TO THINK TOGETHER?',-9,1.65,29.44,.24,mat='mint',group='Quiz')
for row,z in enumerate([18,20.6,23.2]):
    for col,x in enumerate([-14,-11.6,-6.4,-4]):
        table('Quiz desk',x,z+.55,1.5,.7,group='Quiz')
        chair('Quiz seat %02d'%(row*4+col+1),x,z-.35,0,'sage','Quiz')
box('Quiz aisle',-9,.012,20,1.5,.024,10,'cream',solid=False,group='Quiz')
# Mafia: ten seats around the central discussion table.
cyl('Mafia rug',9,.012,23,5.6,.024,'purple',solid=False,group='Mafia')
cyl('Round discussion tabletop',9,.8,23,3.05,.16,'oak',group='Mafia')
cyl('Round table pedestal',9,.37,23,.65,.74,'dark',group='Mafia')
for i in range(10):
    a=2*math.pi*i/10
    x,z=9+4.25*math.sin(a),23+4.25*math.cos(a)
    chair('Mafia seat %02d'%(i+1),x,z,180+math.degrees(a),'purple','Mafia')
    cyl('Player marker',9+2.5*math.sin(a),.91,23+2.5*math.cos(a),.17,.05,'gold',solid=False,group='Mafia')
box('Mafia display panel',9,2.05,29.7,8,2.1,.2,'dark',group='Mafia')
text('Mafia heading','MAFIA CLUB',9,2.35,29.55,.65,mat='white',group='Mafia')
text('Mafia subtitle','DISCUSS  /  DISCOVER  /  DECIDE',9,1.62,29.53,.22,mat='lavender',group='Mafia')
os.makedirs(os.path.dirname(DATA),exist_ok=True)
with open(DATA,'w',encoding='utf-8') as f:
    json.dump(dict(materials=[dict(name=k,hex=v) for k,v in PALETTE.items()],shapes=items,seats=seats,labels=labels),f,ensure_ascii=False,indent=2)

# Blender source mirrors Unity geometry. Unity (x,y,z) -> Blender (x,-z,y).
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
mats={}
for n,h in PALETTE.items():
    rgb=tuple(int(h[i:i+2],16)/255 for i in (1,3,5))
    m=bpy.data.materials.new(n); m.diffuse_color=(*rgb,1); m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*rgb,1); bs.inputs['Roughness'].default_value=.75
    mats[n]=m
groups={}
for g in ['Lounge','Quiz','Mafia']:
    c=bpy.data.collections.new(g); bpy.context.scene.collection.children.link(c); groups[g]=c
for s in items:
    x,y,z=s['position']; w,h,d=s['scale']; kind=s['kind']
    if kind=='Cube':
        bpy.ops.mesh.primitive_cube_add(size=1,location=(x,-z,y)); ob=bpy.context.object; ob.scale=(w,d,h)
    elif kind=='Cylinder':
        bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=w/2,depth=h,location=(x,-z,y)); ob=bpy.context.object
    else:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=1,location=(x,-z,y)); ob=bpy.context.object; ob.scale=(w/2,d/2,h/2)
    ob.name=s['name']; ob.rotation_euler.z=math.radians(s['yaw'])
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if kind=='Cube':
        bevel=ob.modifiers.new('Soft furniture edges','BEVEL'); bevel.width=.035; bevel.segments=2
        normal=ob.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    for c in list(ob.users_collection): c.objects.unlink(ob)
    groups[s['group']].objects.link(ob)
    ob.data.materials.append(mats[s['material']])
for t in labels:
    x,y,z=t['position']
    curve=bpy.data.curves.new(t['name'],'FONT'); curve.body=t['text']; curve.align_x='CENTER'; curve.align_y='CENTER'; curve.size=t['size']; curve.extrude=.001
    ob=bpy.data.objects.new(t['name'],curve); groups[t['group']].objects.link(ob)
    ob.location=(x,-z,y); ob.rotation_euler=(math.pi/2,0,math.radians(t['yaw'])); curve.materials.append(mats[t['material']])
    ob.scale.x=-1  # Keep text readable after the Unity-to-Blender handedness conversion.
scene=bpy.context.scene
scene.render.engine='CYCLES'; scene.cycles.samples=24
scene.cycles.use_denoising=True
scene.world.color=(.65,.65,.65)
scene.view_settings.view_transform='AgX'
for loc,power,size in [((0,-5,25),6500,25),((-15,12,18),4000,18),((15,-28,17),3000,15)]:
    bpy.ops.object.light_add(type='AREA',location=loc); ob=bpy.context.object; ob.data.energy=power; ob.data.shape='DISK'; ob.data.size=size
bpy.ops.object.light_add(type='SUN',location=(0,0,18)); bpy.context.object.data.energy=1.5; bpy.context.object.rotation_euler=(.45,-.5,-.3); bpy.context.object.data.angle=.25
bpy.ops.object.camera_add(location=(48,56,57)); cam=bpy.context.object; cam.rotation_euler=(Vector((0,-7,0))-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.type='ORTHO'; cam.data.ortho_scale=68; scene.camera=cam
scene.render.resolution_x=1600; scene.render.resolution_y=1300; scene.render.resolution_percentage=100
out=os.path.join(ROOT,'Design'); os.makedirs(out,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out,'CampusCommons.blend'))
scene.render.filepath=os.path.join(out,'Campus-overview.png'); bpy.ops.render.render(write_still=True)
cam.location=(20,22,20); cam.rotation_euler=(Vector((0,1,0))-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.ortho_scale=41
scene.render.resolution_x=1600; scene.render.resolution_y=1000; scene.render.filepath=os.path.join(out,'Lounge-detail.png'); bpy.ops.render.render(write_still=True)
print('CAMPUS_BUILD_OK shapes=%d seats=%d'%(len(items),len(seats)))
