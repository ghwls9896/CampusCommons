"""Generate the Grand Library model, game-room models and a Unity-importable FBX.

Run with Blender 4.4: blender -b --python Tools/build_grand_library.py
The model is original, assembled from explicit editable meshes, and uses generated textures.
"""
import bpy, math, os, random, mathutils
import numpy as np
from mathutils import Vector

random.seed(58412)
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSET=os.path.join(ROOT,'Assets','GrandLibrary')
MODEL=os.path.join(ASSET,'Models')
TEX=os.path.join(ASSET,'Textures')
OUT=os.path.join(ROOT,'Design','GrandLibrary')
for p in (MODEL,TEX,OUT): os.makedirs(p,exist_ok=True)

# Colors are chosen to remain warm in both Unity and Blender.
COLORS={
 'walnut':'#39261C','wood':'#68412A','wood_light':'#AD7746','wood_dark':'#251810','floor':'#B08A64',
 'brass':'#B99259','bronze':'#6D5136','ivory':'#D8C8A8','stone':'#A39178',
 'leather':'#654B3A','leather_light':'#9A7555','cushion':'#B7A188','rug':'#8D7860',
 'plant':'#36573A','leaf_light':'#627C4A','soil':'#3D3022','pot':'#73624C',
 'glass':'#9DAFA2','black':'#171B1B','ink':'#263137','chalk':'#E8E0C7',
 'school_floor':'#B2A17F','school_wall':'#D6CBB1','greenboard':'#274A3C',
 'blue':'#425C74','red':'#8A3E32','gold':'#CEAE73','violet':'#765780',
 'lamp':'#FFE1A1','lightline':'#FFD08A',
 'book01':'#56372C','book02':'#70553E','book03':'#705B43','book04':'#314639',
 'book05':'#353C44','book06':'#9A7A4D','book07':'#7C4230','book08':'#796F5A',
 'book09':'#59505B','book10':'#A69270',
 'portal_mafia':'#B45DF3','portal_quiz':'#5FD1E0','portal_escape':'#EE9A50',
}
def rgb(hexval): return tuple(int(hexval[i:i+2],16)/255 for i in (1,3,5))

# Two tiled, original procedural image textures, delivered with the FBX.
N=512
rng=np.random.default_rng(12)
y,x=np.mgrid[0:N,0:N].astype(np.float32)
wood_grain=(np.sin(x*.20+3*np.sin(y*.013)+.7*np.sin(x*.041+y*.018))*.11
            +np.sin(x*.67+np.sin(y*.016)*4)*.025+rng.normal(0,.016,(N,N)))
wood_grain+=np.sin(x*.027+y*.008)*.055
def save_texture(name,base,signal):
    arr=np.empty((N,N,4),dtype=np.float32)
    for i,v in enumerate(base): arr[:,:,i]=np.clip(v*(.90+signal),0,1)
    arr[:,:,3]=1
    img=bpy.data.images.new(name,width=N,height=N,alpha=True)
    img.pixels.foreach_set(arr.ravel()); img.filepath_raw=os.path.join(TEX,name+'.png'); img.file_format='PNG'; img.save(); return img
wood_img=save_texture('walnut_grain',(0.55,.34,.19),wood_grain)
plank_grid=((x%128)<2)|((y%42)<2)
planks=np.sin(x*.13+y*.006)*.05+np.sin(y*.07)*.026+rng.normal(0,.012,(N,N))
planks[plank_grid]-=.2
floor_img=save_texture('floor_planks',(.59,.40,.24),planks)

bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for c in list(bpy.data.collections):
    if c.name!='Collection': bpy.data.collections.remove(c)
collections={}
for g in ['Library','Mafia','Quiz','Escape']:
    c=bpy.data.collections.new(g); bpy.context.scene.collection.children.link(c); collections[g]=c
materials={}
for name,col in COLORS.items():
    m=bpy.data.materials.new(name); m.diffuse_color=(*rgb(col),1); m.use_nodes=True
    nodes=m.node_tree.nodes; bs=nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*rgb(col),1)
    bs.inputs['Roughness'].default_value=.56 if name in ('brass','bronze') else .78
    bs.inputs['Metallic'].default_value=.55 if name in ('brass','bronze') else 0
    if name in ('lamp','lightline') or name.startswith('portal_'):
        bs.inputs['Emission Color'].default_value=(*rgb(col),1)
        bs.inputs['Emission Strength'].default_value=1.8 if name.startswith('portal_') else .7
    if name in ('wood','wood_light','wood_dark','walnut'):
        tex=nodes.new('ShaderNodeTexImage'); tex.image=wood_img
        # Material tint through multiply produces variation between boards and shelves.
        mix=nodes.new('ShaderNodeMixRGB'); mix.blend_type='MULTIPLY'; mix.inputs[0].default_value=.60
        mix.inputs[2].default_value=(*rgb(col),1)
        m.node_tree.links.new(tex.outputs['Color'],mix.inputs[1]); m.node_tree.links.new(mix.outputs[0],bs.inputs['Base Color'])
    if name=='floor':
        tex=nodes.new('ShaderNodeTexImage'); tex.image=floor_img
        m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
    materials[name]=m

class Batch:
    def __init__(self): self.verts=[]; self.faces=[]; self.portal_uv={}
    def add(self,verts,faces,uvs=None):
        o=len(self.verts); self.verts.extend(verts); self.faces.extend([tuple(o+i for i in f) for f in faces])
        if uvs:
            for i,uv in enumerate(uvs): self.portal_uv[o+i]=uv
batches={}
def batch(group,mat,collision=False):
    key=(group,mat,collision)
    if key not in batches: batches[key]=Batch()
    return batches[key]
def box(group,mat,x,y,z,w,h,d,yaw=0,collision=False):
    # Coordinates here use Unity convention (X right, Y up, Z forward).
    c,s=math.cos(yaw),math.sin(yaw); verts=[]
    for yy in (-h/2,h/2):
        for dz,dx in [(-d/2,-w/2),(-d/2,w/2),(d/2,w/2),(d/2,-w/2)]:
            X=x+c*dx+s*dz; Z=z-s*dx+c*dz
            verts.append((X,-Z,y+yy))
    # Blender's (x, -z, y) coordinate conversion reverses handedness.
    # Reverse the cube faces so the floor top and room walls face outward.
    faces=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
    batch(group,mat,collision).add(verts,[tuple(reversed(f)) for f in faces])
def sector(group,mat,x,z,r0,r1,a0,a1,y0,y1,steps=10,collision=False):
    # Sector angles measured from +Z toward +X.
    verts=[]
    for y in (y0,y1):
        for r in (r0,r1):
            for j in range(steps+1):
                a=a0+(a1-a0)*j/steps; verts.append((x+math.sin(a)*r,-(z+math.cos(a)*r),y))
    L=steps+1; faces=[]
    for j in range(steps):
        i=j
        faces.extend([(i,i+1,L+i+1,L+i),(2*L+i,3*L+i,3*L+i+1,2*L+i+1),
          (i,2*L+i,2*L+i+1,i+1),(L+i,L+i+1,3*L+i+1,3*L+i)])
    faces.extend([(0,L,3*L,2*L),(steps,2*L+steps,3*L+steps,L+steps)])
    batch(group,mat,collision).add(verts,faces)
def ring(group,mat,x,z,r0,r1,y0,y1,collision=False,segments=96):
    sector(group,mat,x,z,r0,r1,0,2*math.pi,y0,y1,segments,collision)
def cyl(group,mat,x,y,z,r,h,collision=False,segments=16):
    ring(group,mat,x,z,0,r,y-h/2,y+h/2,collision,segments)
def ellipsoid(group,mat,x,y,z,rx,ry,rz,yaw=0,segments=12,rings=7):
    verts=[]; faces=[]; c,s=math.cos(yaw),math.sin(yaw)
    for i in range(rings+1):
        phi=math.pi*i/rings
        for j in range(segments):
            a=2*math.pi*j/segments; dx=rx*math.sin(phi)*math.cos(a); dz=rz*math.sin(phi)*math.sin(a)
            verts.append((x+c*dx+s*dz,-(z-s*dx+c*dz),y+ry*math.cos(phi)))
    for i in range(rings):
        for j in range(segments):
            a=i*segments+j; b=i*segments+(j+1)%segments; faces.append((a,b,b+segments,a+segments))
    batch(group,mat).add(verts,faces)
def softbox(group,mat,x,y,z,w,h,d,yaw=0,collision=False):
    # A gently rounded superellipsoid: real curved upholstery, not a flat cube.
    steps,rings=24,12; verts=[]; faces=[]; c,s=math.cos(yaw),math.sin(yaw)
    def sp(v,p): return math.copysign(abs(v)**p,v) if abs(v)>1e-9 else 0
    for i in range(rings+1):
        lat=-math.pi/2+math.pi*i/rings
        for j in range(steps):
            a=math.tau*j/steps
            dx=w*.5*sp(math.cos(lat),.38)*sp(math.cos(a),.38)
            dz=d*.5*sp(math.cos(lat),.38)*sp(math.sin(a),.38)
            dy=h*.5*sp(math.sin(lat),.38)
            verts.append((x+c*dx+s*dz,-(z-s*dx+c*dz),y+dy))
    for i in range(rings):
        for j in range(steps):
            n=(j+1)%steps; faces.append((i*steps+j,(i+1)*steps+j,(i+1)*steps+n,i*steps+n))
    batch(group,mat,collision).add(verts,faces)
def rod(group,mat,p1,p2,r,collision=False,sides=8):
    # p1/p2 in Unity coordinates; create a tube in Blender coordinates.
    a=Vector((p1[0],-p1[2],p1[1])); b=Vector((p2[0],-p2[2],p2[1])); axis=(b-a).normalized()
    u=axis.cross(Vector((0,0,1)))
    if u.length<.001: u=axis.cross(Vector((1,0,0)))
    u.normalize(); v=axis.cross(u).normalized(); verts=[]
    for p in (a,b):
        for i in range(sides):
            ang=2*math.pi*i/sides; verts.append(tuple(p+r*(u*math.cos(ang)+v*math.sin(ang))))
    faces=[]
    for i in range(sides): j=(i+1)%sides; faces.append((i,j,j+sides,i+sides))
    faces.extend([tuple(reversed(range(sides))),tuple(range(sides,sides*2))])
    batch(group,mat,collision).add(verts,faces)
def at_radius(r,a): return math.sin(a)*r,math.cos(a)*r
def label(text,group,x,y,z,size=.5,reverse=False):
    # Front-facing vertical text; double-sided and facing the room's entrance (-Z).
    curve=bpy.data.curves.new('sign_'+text,'FONT'); curve.body=text; curve.size=size; curve.align_x='CENTER'; curve.align_y='CENTER'; curve.extrude=.004
    ob=bpy.data.objects.new('SIGN_'+text,curve); collections[group].objects.link(ob); ob.location=(x,-z,y); ob.rotation_euler=(math.pi/2,0,math.pi if reverse else 0); ob.scale.x=-1
    curve.materials.append(materials['chalk'])
    # Convert text to mesh so FBX export and Unity keep it.
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active=ob; bpy.ops.object.convert(target='MESH'); ob.select_set(False)
    ob.name='SIGN_'+group+'_'+text

def plant(group,x,z,size=1,large=False,base=0):
    cyl(group,'pot',x,base+.35*size,z,.48*size,.7*size,True,16)
    cyl(group,'soil',x,base+.71*size,z,.40*size,.05*size,False,12)
    if large:
        rod(group,'wood_dark',(x,base+.7*size,z),(x,base+2.7*size,z),.09*size)
        for i in range(20):
            a=random.random()*math.tau; r=random.uniform(.4,1.5)*size; xx=x+math.sin(a)*r; zz=z+math.cos(a)*r; yy=base+random.uniform(2.1,3.8)*size
            rod(group,'wood_dark',(x,base+1.3*size,z),(xx,yy,zz),.025*size)
            for j in range(6):
                la=random.random()*math.tau; lr=random.uniform(.05,.38)*size
                ellipsoid(group,random.choice(['plant','leaf_light']),xx+math.sin(la)*lr,yy+random.uniform(-.25,.25)*size,
                          zz+math.cos(la)*lr,.16*size,.23*size,.09*size,yaw=la,segments=8,rings=5)
    else:
        for i in range(10):
            a=i*math.tau/10; r=random.uniform(.15,.45)*size; xx=x+math.sin(a)*r; zz=z+math.cos(a)*r; yy=base+random.uniform(1.05,1.7)*size
            rod(group,'plant',(x,base+.7*size,z),(xx,yy,zz),.018*size)
            ellipsoid(group,random.choice(['plant','leaf_light']),xx,yy,zz,.24*size,.42*size,.13*size,yaw=a,segments=12,rings=7)
def sofa(group,x,z,yaw=0,color='leather',width=3.2):
    def b(mat,dx,y,dz,w,h,d,collision=False):
        c,s=math.cos(yaw),math.sin(yaw); box(group,mat,x+c*dx+s*dz,y,z-s*dx+c*dz,w,h,d,yaw,collision)
    def soft(mat,dx,y,dz,w,h,d,collision=False):
        c,s=math.cos(yaw),math.sin(yaw); softbox(group,mat,x+c*dx+s*dz,y,z-s*dx+c*dz,w,h,d,yaw,collision)
    b('wood_dark',0,.25,0,width,.3,1.35,True)
    soft(color,0,.56,-.1,width,.55,1.3,True)
    soft(color,0,1.0,-.55,width,.85,.42,True)
    for dx in (-width/2+.13,width/2-.13): soft(color,dx,.82,0,.36,.78,1.4,True)
    for dx in (-width*.3,0,width*.3):
        soft('cushion',dx,.97,-.38,width*.27,.48,.25)
        soft('leather_light' if color=='leather' else 'cushion',dx,.86,.06,width*.28,.18,.92)
    for dx in (-width*.39,width*.39):
        for dz in (-.5,.5): b('wood_dark',dx,.12,dz,.12,.24,.12)
def chair(group,x,z,yaw=0,color='leather',seat_callback=None):
    def b(mat,dx,y,dz,w,h,d,collision=False):
        c,s=math.cos(yaw),math.sin(yaw); box(group,mat,x+c*dx+s*dz,y,z-s*dx+c*dz,w,h,d,yaw,collision)
    b('wood_dark',0,.43,0,.85,.12,.8,True)
    softbox(group,color,x,.56,z,.86,.22,.82,yaw,True)
    c,s=math.cos(yaw),math.sin(yaw)
    softbox(group,color,x-s*.34,1.02,z-c*.34,.85,.85,.24,yaw,True)
    for dx in (-.3,.3):
        for dz in (-.28,.28): b('wood_dark',dx,.2,dz,.09,.42,.09)
    if seat_callback: seat_callback(group,x,.69,z,math.degrees(yaw))
def table(group,x,z,r=1.0,h=.55):
    cyl(group,'wood',x,h,z,r,.15,True,32)
    cyl(group,'wood_dark',x,h/2,z,r*.32,h,True,24)
    ring(group,'brass',x,z,r*.94,r,h-.08,h-.04,False,32)
def desk(group,x,z,w=2,d=.8,h=.75):
    box(group,'wood_light',x,h,z,w,.12,d,0,True)
    for dx in (-w*.42,w*.42):
        for dz in (-d*.38,d*.38): box(group,'wood_dark',x+dx,h/2,z+dz,.1,h,.1,0,True)
def lamp(group,x,z,base=.6):
    cyl(group,'brass',x,base+.05,z,.18,.1,False)
    rod(group,'brass',(x,base+.1,z),(x,base+.63,z),.035)
    # A low-poly pleated linen shade keeps the warm reading-lamp silhouette.
    cyl(group,'lamp',x,base+.68,z,.27,.34,False,16)
    ring(group,'brass',x,z,.24,.28,base+.5,base+.54,False,16)
def bookshelves(floor_y):
    count=40; width=math.tau*19/count*.92
    for i in range(count):
        a=math.tau*i/count; x,z=at_radius(19.1,a); yaw=a
        # Long side is local X (tangent); local Z points radially outward.
        box('Library','wood_dark',x,floor_y+2.05,z,width,4.0,.42,yaw,True)
        for j in range(5):
            box('Library','wood_light',x,floor_y+.15+j*.76,z-.01,width,.10,.66,yaw)
            if j<4:
                for b in range(10):
                    dx=-width*.43+b*width*.095+random.uniform(-.02,.02)
                    ht=random.uniform(.4,.64); thick=random.uniform(.12,.20)
                    xx=x-math.sin(a)*.34+math.cos(yaw)*dx; zz=z-math.cos(a)*.34-math.sin(yaw)*dx
                    box('Library','book%02d'%random.randint(1,10),xx,floor_y+.23+j*.76+ht/2,zz,thick,ht,.23,yaw)
                    if b%3==0: box('Library','gold',xx-math.sin(a)*.12,floor_y+.30+j*.76+ht*.45,zz-math.cos(a)*.12,thick*.8,.018,.018,yaw)
        box('Library','wood_light',x,floor_y+4.1,z,width,.18,.7,yaw)
        if i%4==0:
            px,pz=at_radius(18.7,a); box('Library','walnut',px,floor_y+2.1,pz,.28,4.2,.9,yaw,True)
            box('Library','brass',px,floor_y+3.9,pz,.36,.08,.94,yaw)

# GRAND LIBRARY. Three independent, inhabitable levels inside a circular shell.
cyl('Library','floor',0,-.19,0,20.4,.38,True,128)
for r in [18.5,18.9,19.3,19.7]: ring('Library','wood_light',0,0,r,r+.035,.01,.025,False,128)
for level in [0,4.8,9.6]: bookshelves(level)
for lvl in [4.8,9.6]:
    ring('Library','wood',0,0,14.9,20.0,lvl-.28,lvl,True,128)
    ring('Library','wood_light',0,0,14.82,15.03,lvl-.22,lvl+.03,False,128)
    ring('Library','lightline',0,0,14.91,15.01,lvl-.22,lvl-.15,False,128)
    ring('Library','walnut',0,0,19.55,19.9,lvl-.42,lvl-.24,False,128)
    # Space in the balcony handrail at the stair landing, and at its continuation.
    gaps=[math.radians(60)] if lvl==4.8 else [math.radians(270)]
    for i in range(96):
        a=(i+.5)*math.tau/96
        if any(abs((a-g+math.pi)%math.tau-math.pi)<math.radians(13) for g in gaps): continue
        a0=i*math.tau/96; a1=(i+1)*math.tau/96
        sector('Library','brass',0,0,14.84,14.9,a0,a1,lvl+1.04,lvl+1.10,1,True)
        for h in (.38,.68): sector('Library','bronze',0,0,14.87,14.9,a0,a1,lvl+h,lvl+h+.027,1)
        if i%3==0:
            x,z=at_radius(14.87,a); rod('Library','brass',(x,lvl,z),(x,lvl+1.08,z),.035)
    # Small living planters follow the balconies while keeping the landings clear.
    for i in range(12):
        a=(i+.35)*math.tau/12
        if any(abs((a-g+math.pi)%math.tau-math.pi)<math.radians(20) for g in gaps): continue
        x,z=at_radius(15.75,a); plant('Library',x,z,.52,False,lvl)
# Curved rising staircase: two flights, 27 distinct real mesh treads per flight.
for flight in range(2):
    a0=math.radians(-150+210*flight); rise=4.8/27
    for i in range(27):
        aa=a0+math.radians(210)*i/27; ab=a0+math.radians(210)*(i+1)/27
        top=flight*4.8+(i+1)*rise
        sector('Library','wood_light',0,0,11.8,15.35,aa,ab,top-.17,top,4,True)
        sector('Library','brass',0,0,11.8,15.35,ab-.007,ab,top-.01,top+.008,1)
        ax,az=at_radius(11.83,(aa+ab)/2)
        rod('Library','brass',(ax,top,az),(ax,top+1.02,az),.035)
        bx,bz=at_radius(11.83,ab)
        rod('Library','brass',(ax,top+1.02,az),(bx,top+1.02+rise/2,bz),.045)

# Framed open oculus and a luminous high roof, plus ribs and hanging pendants.
ring('Library','wood_dark',0,0,15,20.2,14.05,14.45,False,128)
ring('Library','brass',0,0,14.9,15.1,14.0,14.18,False,128)
for i in range(24):
    a=i*math.tau/24; x1,z1=at_radius(15,a); x2,z2=at_radius(20,a)
    rod('Library','wood_light',(x1,14.22,z1),(x2,14.22,z2),.1)
for i in range(12):
    a=i*math.tau/12; x1,z1=at_radius(8,a); x2,z2=at_radius(15,a)
    rod('Library','bronze',(x1,15.15,z1),(x2,14.22,z2),.055)
ring('Library','bronze',0,0,7.92,8.08,15.08,15.2,False,96)
for x,z in [(-11,-5),(11,-5),(-11,8),(11,8)]:
    rod('Library','brass',(x,13.9,z),(x,11.3,z),.035)
    ellipsoid('Library','lamp',x,11.15,z,.35,.3,.35,segments=12,rings=8)

# Distinct social islands leave a central route to the three portals.
for x,z in [(-9,-5),(9,-5),(-9,3),(9,3)]:
    # Furniture is installed as separately editable realistic prefabs in Unity.
    cyl('Library','rug',x,.018,z,2.85,.035,False,48)
for x,z in [(-14,-7),(12,-13),(-15,5),(14,11),(-3,-13),(4,-13)]: plant('Library',x,z,1.1,True)
for x,z in [(-12,11),(12,11)]: plant('Library',x,z,1.1)
desk('Library',0,-13,5,1.3,.95); lamp('Library',-1.5,-13,1.02); lamp('Library',1.5,-13,1.02)

# Modelled oval portals. Their visual surface and trigger behavior are wired in Unity.
def portal(group,x,z,material,caption,yaw=0):
    cyl(group,'stone',x,.06,z,2.0,.12,True,32)
    c,s=math.cos(yaw),math.sin(yaw)
    def point(dx,y,dz=0): return (x+c*dx+s*dz,y,z-s*dx+c*dz)
    for layer in range(2):
        for i in range(48):
            a0=i*math.tau/48; a1=(i+1)*math.tau/48
            rr=1.45 if layer==0 else 1.28
            p1=point(math.sin(a0)*rr,2.12+math.cos(a0)*rr*1.36)
            p2=point(math.sin(a1)*rr,2.12+math.cos(a1)*rr*1.36)
            rod(group,'brass' if layer==0 else material,p1,p2,.095 if layer==0 else .05)
    verts=[(x,-z,2.12)]; uv=[(.5,.5)]
    for i in range(49):
        a=math.tau*i/48; px,py,pz=point(1.2*math.sin(a),2.12+1.64*math.cos(a)); verts.append((px,-pz,py))
        uv.append((.5+.5*math.sin(a),.5+.5*math.cos(a)))
    batch(group,material).add(verts,[(0,i+1,i+2) for i in range(48)],uv)
    box(group,'wood_dark',x,4.45,z,3.35,.43,.20,yaw)
    if group!='Library': label(caption,group,x,4.44,z+.13,.28,True)
portal('Library',14, -5.5,'portal_mafia','MAFIA',math.pi/2)
portal('Library',14, 0,'portal_quiz','QUIZ  1 VS 1',math.pi/2)
portal('Library',14, 5.5,'portal_escape','ESCAPE ROOM',math.pi/2)

# Separate game rooms at distant coordinates; walking through a portal moves the player.
def room(group,cx,w,d,wall,accent):
    box(group,wall,cx,-.18,0,w,.36,d,0,True)
    box(group,wall,cx,2.35,d/2,w,4.7,.3,0,True)
    box(group,wall,cx-w/2,2.35,0,.3,4.7,d,0,True)
    box(group,wall,cx+w/2,2.35,0,.3,4.7,d,0,True)
    box(group,wall,cx,2.35,-d/2,w,4.7,.3,0,True)
    # Game rooms are enclosed; warm luminous strips make the ceiling readable.
    box(group,wall,cx,4.8,0,w,.2,d)
    for dx in [-w*.28,0,w*.28]:
        box(group,'lightline',cx+dx,4.67,1,.13,.04,d*.50)
    for xx in (-w/2+.4,w/2-.4): box(group,accent,cx+xx,2.3,0,.3,4.6,d-.5)
    box(group,accent,cx,.12,d/2-.2,w,.24,.15)
    portal(group,cx,-d/2+.9,'portal_quiz','LOUNGE')
room('Mafia',100,19,18,'wood_dark','walnut')
box('Mafia','wood',100,2.1,8.78,17,3.8,.12)
for dx in range(-7,8,2):
    box('Mafia','brass',100+dx,2.2,8.68,.04,4,.04)
for x in (93,107):
    plant('Mafia',x,7.1,.9)
    for y in (1,2,3): box('Mafia','lightline',x,y,8.55,1.3,.04,.08)
cyl('Mafia','rug',100,.02,1,5.4,.04,False,64)
table('Mafia',100,1,3.1,.82)
for i in range(10):
    a=math.tau*i/10; x=100+math.sin(a)*4.3; z=1+math.cos(a)*4.3
    # Chairs are realistic separate prefabs installed by the Unity scene builder.
    cyl('Mafia','gold',100+math.sin(a)*2.4,.92,1+math.cos(a)*2.4,.17,.03)
label('MAFIA  |  DISCUSS AND DISCOVER','Mafia',100,3.4,8.62,.4)
for x,z in [(96,-5.7),(104,-5.7)]: lamp('Mafia',x,z,.15)

room('Quiz',200,15,18,'ivory','wood_light')
box('Quiz','ink',200,2.65,8.78,10,2.9,.18,0,True)
box('Quiz','blue',200,2.65,8.66,9.5,2.4,.04)
label('QUIZ  1  VS  1','Quiz',200,2.95,8.58,.7)
label('READY  /  THINK  /  ANSWER','Quiz',200,2.12,8.57,.25)
for dx in [-2.8,2.8]:
    # The Unity builder supplies a realistic desk and chair for each player.
    box('Quiz','black',200+dx,.91,1.8,.5,.1,.35)
    cyl('Quiz','gold' if dx<0 else 'blue',200+dx,1.0,1.8,.13,.1)
for x in [194,206]: plant('Quiz',x,6.8,1.05)

# Escape Room uses the separately imported user-provided Baldi school model.

# Convert each batch into a real mesh with generated UVs and meaningful grouping.
for (group,mat,solid),data in batches.items():
    name='LIB_%s__%s__%s'%(group,mat,'SOLID' if solid else 'DETAIL')
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(data.verts,[],data.faces); mesh.update()
    if mat in ('leather','leather_light','cushion','plant','leaf_light'):
        for polygon in mesh.polygons: polygon.use_smooth=True
    uv=mesh.uv_layers.new(name='UVMap')
    for poly in mesh.polygons:
        normal=poly.normal
        for li in poly.loop_indices:
            vi=mesh.loops[li].vertex_index
            if vi in data.portal_uv: uv.data[li].uv=data.portal_uv[vi]; continue
            p=mesh.vertices[vi].co
            # World-space planar mapping keeps grain consistent across segments.
            if abs(normal.z)>.55: uv.data[li].uv=(p.x/3,p.y/3)
            elif abs(normal.x)>abs(normal.y): uv.data[li].uv=(p.y/3,p.z/3)
            else: uv.data[li].uv=(p.x/3,p.z/3)
    ob=bpy.data.objects.new(name,mesh); collections[group].objects.link(ob); mesh.materials.append(materials[mat])

# Render lighting is part of the .blend preview. Unity has separately authored lights.
scene=bpy.context.scene
scene.render.engine='CYCLES'; scene.cycles.samples=32; scene.cycles.use_denoising=True
scene.world.use_nodes=True
scene.world.node_tree.nodes.get('Background').inputs['Color'].default_value=(.54,.64,.67,1)
scene.world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.55
scene.view_settings.view_transform='AgX'
def area(loc,power,size,target):
    bpy.ops.object.light_add(type='AREA',location=loc); light=bpy.context.object
    light.data.energy=power; light.data.color=(1,.77,.51); light.data.shape='DISK'; light.data.size=size
    light.rotation_euler=(Vector(target)-light.location).to_track_quat('-Z','Y').to_euler()
area((0,0,14),12000,18,(0,0,0))
for x,z in [(-10,-7),(10,-7),(-10,7),(10,7),(0,0)]: area((x,-z,10),1800,5,(x,-z,0))
for x in [100,200,300]: area((x,0,9),4500,12,(x,0,0))
bpy.ops.object.camera_add(location=(0,13,2.2)); cam=bpy.context.object
cam.rotation_euler=(Vector((0,-4,6.3))-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.lens=18; scene.camera=cam
scene.render.resolution_x=1500; scene.render.resolution_y=1000; scene.render.resolution_percentage=100
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'GrandLibrary.blend'))
if os.environ.get('GRAND_LIBRARY_SKIP_RENDER')!='1':
    scene.render.filepath=os.path.join(OUT,'library-interior-blender.png'); bpy.ops.render.render(write_still=True)

# Export only the four mesh collections (no Blender render cameras/lights).
bpy.ops.object.select_all(action='DESELECT')
for c in collections.values():
    for ob in c.objects: ob.select_set(True)
# Unity's FBX importer changes handedness by reflecting X. Pre-reflect the
# exported copy so world coordinates match the portal/seat locations in Unity.
# The .blend above remains in the natural Blender orientation.
reflect=mathutils.Matrix.Diagonal((-1.0,1.0,1.0,1.0))
for c in collections.values():
    for ob in c.objects:
        transform=reflect @ ob.matrix_world
        if ob.name.startswith('SIGN_'):
            # The text was mirrored for the Blender camera; flip about its own
            # center once more so it reads correctly after Unity's FBX import.
            cx=-ob.matrix_world.translation.x
            transform=mathutils.Matrix.Translation((cx,0,0)) @ reflect @ mathutils.Matrix.Translation((-cx,0,0)) @ transform
        ob.data.transform(transform)
        if transform.determinant()<0: ob.data.flip_normals()
        ob.matrix_world=mathutils.Matrix.Identity(4)
temporary_fbx=os.path.join(OUT,'GrandLibrary-export-temp.fbx')
bpy.ops.export_scene.fbx(filepath=temporary_fbx,use_selection=True,
    object_types={'MESH'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',
    bake_space_transform=False,add_leaf_bones=False,path_mode='AUTO',use_mesh_modifiers=True)
os.replace(temporary_fbx,os.path.join(MODEL,'GrandLibrary.fbx'))
print('GRAND_LIBRARY_BUILD_OK batches=%d vertices=%d'%(len(batches),sum(len(v.verts) for v in batches.values())))
