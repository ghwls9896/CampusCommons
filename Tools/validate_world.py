"""Pure Python data/navigation checks. Can also run inside Blender with --python."""
import json, math, os
from collections import deque
root=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
with open(os.path.join(root,'Assets','Campus','Resources','campus.json'),encoding='utf-8') as f:
    data=json.load(f)
palette={m['name'] for m in data['materials']}
assert len(data['seats'])==43, 'Expected 21 lounge + 12 quiz + 10 mafia seats'
counts={g:sum(s['group']==g for s in data['seats']) for g in ['Lounge','Quiz','Mafia']}
assert counts=={'Lounge':21,'Quiz':12,'Mafia':10}, counts
for item in data['shapes']:
    assert item['material'] in palette
    assert all(math.isfinite(v) for v in item['position']+item['scale'])
    assert all(v>0 for v in item['scale'])

# Player-sized 2D collision navigation at 0.25m resolution. Floor/stage surfaces
# within the character's 0.3m step height are traversable; overhead lintels ignored.
step=.25; radius=.31
xmin,zmin=-17.5,-13.5
nx,nz=141,173
blocked=set()
def cell(x,z): return (round((x-xmin)/step),round((z-zmin)/step))
def point(i,j): return xmin+i*step,zmin+j*step
for item in data['shapes']:
    if not item['solid']: continue
    x,y,z=item['position']; w,h,d=item['scale']
    if y+h/2<=.3 or y-h/2>=1.8: continue
    a=math.radians(item['yaw']); c,s=math.cos(a),math.sin(a)
    extent=max(w,d)/2*1.42+radius
    lo=cell(x-extent,z-extent); hi=cell(x+extent,z+extent)
    for i in range(max(0,lo[0]-1),min(nx,hi[0]+2)):
        for j in range(max(0,lo[1]-1),min(nz,hi[1]+2)):
            px,pz=point(i,j); dx,dz=px-x,pz-z
            if item['kind']=='Cylinder': hit=dx*dx+dz*dz<(w/2+radius)**2
            else:
                lx,lz=c*dx-s*dz,s*dx+c*dz
                qx,qz=max(abs(lx)-w/2,0),max(abs(lz)-d/2,0)
                hit=qx*qx+qz*qz<radius*radius
            if hit: blocked.add((i,j))
start=cell(0,-6)
assert start not in blocked, 'Spawn obstructed'
reachable={start}; queue=deque([start])
while queue:
    i,j=queue.popleft()
    for di,dj in [(1,0),(-1,0),(0,1),(0,-1)]:
        p=(i+di,j+dj)
        if 0<=p[0]<nx and 0<=p[1]<nz and p not in blocked and p not in reachable:
            reachable.add(p); queue.append(p)
for x,z in [(-9,14),(9,14),(-9,16),(9,16),(0,-6)]:
    assert cell(x,z) in reachable, 'Door or spawn inaccessible: '+str((x,z))
for seat in data['seats']:
    x,y,z=seat['position']; center=cell(x,z)
    accessible=False
    for di in range(-6,7):
        for dj in range(-6,7):
            q=(center[0]+di,center[1]+dj)
            if q not in reachable: continue
            px,pz=point(*q)
            if (px-x)**2+(pz-z)**2+(.6-y)**2<1.6**2: accessible=True
    assert accessible, 'Seat interaction cannot be reached: '+seat['name']
report={'result':'PASS','shapes':len(data['shapes']),'seats':counts,
        'reachable_navigation_cells':len(reachable),'grid_spacing_metres':step,
        'checks':['finite positive geometry','material references','seat counts','spawn clearance',
                  'both doorway routes','all seats reachable within interaction range'],
        'limitation':'Geometry checks only. Unity compilation, Play Mode and multiplayer are not tested.'}
with open(os.path.join(root,'Design','validation.json'),'w',encoding='utf-8') as f:
    json.dump(report,f,indent=2)
print(json.dumps(report,indent=2))
