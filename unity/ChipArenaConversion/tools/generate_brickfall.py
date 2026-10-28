"""Deterministic simulation-only Brickfall grey-box. Never called by export_source.mjs."""
import importlib.util,json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('draft',ROOT/'docs/arenas/scripts/brickfall_m1.py')
draft=importlib.util.module_from_spec(spec);spec.loader.exec_module(draft)
blocks=[];segments=[];pilot=[];nodes=[];links=[]
def v(x=0,y=0,z=0):return dict(x=round(x,6),y=round(y,6),z=round(z,6))
def q(axis=None,angle=0):
    a=math.radians(angle/2);axis=axis or (0,1,0)
    return dict(x=axis[0]*math.sin(a),y=axis[1]*math.sin(a),z=axis[2]*math.sin(a),w=math.cos(a))
def box(id,x0,x1,z0,z1,y0,y1,role='static',material='stone',segment=None,hp=None,rotation=None,target=None):
    b=dict(id=id,material=material,pos=v((x0+x1)/2,(y0+y1)/2,(z0+z1)/2),size=v(x1-x0,y1-y0,z1-z0),rotation=rotation or q(),role=role,mass=26,segment=segment,hp=hp or (120 if role=='masonry' else 34 if role=='cover' else 0))
    if role in ('structural','masonry','carried','rubble'):b['mass']=round((x1-x0)*(y1-y0)*(z1-z0)*{'brick':1900,'stone':2400,'metal':7800}.get(material,650),4)
    (blocks if target is None else target).append(b);return b
def seg(id,mode,supports,crush,telegraph=1.2,duration=1.2,direction=(0,0,0)):
    s=dict(id=id,building=id.split('-')[0],mode=mode,supports=supports,failBelow=2 if mode in ('peel','tear-hinge') else 1,telegraph=telegraph,duration=duration,maxAccel=6.9,mass=35 if mode=='pancake' else 12,material='brick' if mode!='tear-hinge' else 'metal',direction=v(*direction),blocks=[],poses=[],rubble=[],nav=dict(remove=[],add=[]),crush=dict(center=v(*crush[:3]),size=v(*crush[3:]),damage=45,push=6,cap=60,capHpFraction=.6),sweeps=[])
    segments.append(s);return s
for sx in [-1,1]:
 for sz in [-1,1]:
    tag=('n' if sz>0 else 's')+('w' if sx<0 else 'e');id='hotel-'+tag
    s=seg(id,'pancake',[dict(member='hotel-pier-'+tag,w=1)],(sx*5.2,5.9,sz*3,5.6,11.8,6),direction=(sx,0,sz))
    s['sweeps']=[dict(center=v(sx*6.6,5.9,sz*4.4),size=v(8.4,11.8,8.8))]
for name,sign in [('ashby',1),('bray',-1)]:
    s=seg('bridge-'+name,'tear-hinge',[dict(member='bridge-'+name+'-bearing',w=1),dict(segment='hotel-'+('nw' if sign==1 else 'se'),w=1)],(-7*sign,4,9.1*sign,2,8,4.8),.8,.7,(0,0,-sign))
    s['sweeps']=[dict(center=v(-7*sign,4,9*sign),size=v(2,8,6))]
    s=seg('facade-'+name,'peel',[dict(member=name+'-pier-'+str(i),w=1) for i in range(1,4)],(-15.5*sign,4,23.5*sign,19,8,5),.9,1.24,(0,0,sign))
    s['sweeps']=[dict(center=v(-15.5*sign,4,23.5*sign),size=v(19,8.4,5))]
    s=seg('stack-'+('n' if sign==1 else 's'),'topple',[dict(member='stack-'+('n' if sign==1 else 's')+'-base',w=1)],(10*sign,8,18*sign,3,16,16),1,.6,(0,0,-sign))
    s['sweeps']=[dict(center=v(10*sign,8,18*sign),size=v(3,16,16))]
S={s['id']:s for s in segments}
box('frontage-floor',-32,32,-28,28,-.5,0,material='foundation')
# Real frontage openings/notches and walk-in shop alcoves, as opposed to draft paint.
for sign in [-1,1]:
    cuts=sorted({-32,32,8.5,11.5,*[n for a in (-27,-16,1,18) for n in (a,a+2)]})
    for i,(a,b) in enumerate(zip(cuts,cuts[1:])):
        if a>=8.5 and b<=11.5:continue
        alcove=any(a>=x and b<=x+2 for x in (-27,-16,1,18));z0=27.5 if alcove else 26
        box(f'frontage-{sign}-{i}',*sorted((sign*a,sign*b)),*sorted((sign*z0,sign*28)),0,14)
        if alcove:box(f'frontage-{sign}-lintel-{i}',*sorted((sign*a,sign*b)),*sorted((sign*26,sign*27.5)),2.5,14)
    box(f'frontage-side-{sign}',*sorted((sign*30,sign*32)),-28,28,0,14)
    pilot += [dict(id=f'frontage-top-{sign}',center=v(0,0,sign*27),size=v(64,0,2),bottom=13.9),dict(id=f'frontage-side-top-{sign}',center=v(sign*31),size=v(2,0,56),bottom=13.9)]
# Draft horizontal footprint preserves all cover/ground opening LOS blockers.
for name,x0,x1,z0,z1,top,ch,los in draft.els:
    mirror=name.endswith('~m');stem=name.removesuffix('~m')
    if ch in '#KCP':continue
    if stem.startswith('hotel-'):
        if stem.startswith('hotel-seam'):
            box('hotel-core-'+name,x0,x1,z0,z1,0,3.3);continue
        # Never attach a panel crossing the wing seam to the other quadrant.
        xs=sorted(set([x0,x1]+[x for x in [-2.4,0,2.4] if x0<x<x1])) if x1-x0>1 else [x0,x1]
        zs=sorted(set([z0,z1]+([0] if z0<0<z1 else []))) if z1-z0>1 else [z0,z1]
        for a,b in zip(xs,xs[1:]):
         for c,d in zip(zs,zs[1:]):
          cx=(a+b)/2;cz=(c+d)/2;tag=('n' if cz>=0 else 's')+('w' if cx<0 else 'e');sid='hotel-'+tag
          for floor,(lo,hi) in enumerate([(0,3.36),(3.6,6.96),(7.2,10.56),(10.8,11.8)]):
            if abs(cx)<2.4:
                # Core landing end door: N at F1/roof, S at F2. Ground entry is from lobby west.
                if (cz>0 and floor in (1,3)) or (cz<0 and floor==2):continue
                box(f'hotel-core-end-{name}-{a}-{c}-{floor}',a,b,c,d,lo,hi)
            else:box(f'{sid}-{name}-{a}-{c}-{floor}',a,b,c,d,lo,hi,'masonry','brick',sid)
        continue
    if stem.startswith('ashby-'):
        town='bray' if mirror else 'ashby'
        if 'lane' in stem:
            # Structural band at street, continuous upper facade with open windows.
            for f,(lo,hi) in enumerate([(0,3.36),(3.6,6.96),(7.2,8.2)]):box(town+'-lane-'+stem+'-'+str(f),x0,x1,z0,z1,lo,hi,'masonry','brick','facade-'+town)
        else:box(town+'-'+stem,x0,x1,z0,z1,0,top)
        continue
    role='cover' if ch in 'os' else 'static'
    prefix='market-' if stem.startswith(('kiosk','storeroom','market')) else 'shop-' if stem=='kessels' else 'yard-' if stem.startswith(('dock','truck')) else 'cover-'
    # Closed shop collision stops at roof; add parapet separately.
    box(prefix+name,x0,x1,z0,z1,0,3.6 if stem=='kessels' else top,role,'wood' if role=='cover' else 'stone')
    if stem=='kessels':
        for i,(a,b,c,d) in enumerate([(x0,x1,z0,z0+.2),(x0,x1,z1-.2,z1),(x0,x0+.2,z0,z1),(x1-.2,x1,z0,z1)]):box('shop-parapet-'+name+str(i),a,b,c,d,3.6,4.4)
# Piers placed exactly as the draft.
for sx in [-1,1]:
 for sz in [-1,1]:
    tag=('n' if sz>0 else 's')+('w' if sx<0 else 'e');sid='hotel-'+tag
    box('hotel-pier-'+tag,sx*8-.6,sx*8+.6,sz*6-.6,sz*6+.6,0,3.6,'structural','brick',hp=140)
    for i,y in enumerate([3.6,7.2,10.8]):box(sid+'-slab-'+str(i),*sorted((sx*2.4,sx*8)),*sorted((0,sz*6)),y-.24,y,'carried','stone',sid)
# Hotel core walls: openings on north/south ends; east arcade is clear to 3.36 m.
for floor in range(4):
    lo=floor*3.6;hi=min(lo+3.36,11.8)
    for side,(xa,xb) in enumerate([(-2.4,-2.2),(2.2,2.4)]):
        if floor==0 and side==0:parts=[(-3.8,5.8)] # Ground entry bends around the west core wall.
        elif floor in (1,3):parts=[(-5.8,4.2)]
        elif floor==2:parts=[(-4.2,5.8)]
        else:parts=[(-5.8,5.8)]
        for i,(c,d) in enumerate(parts):box(f'hotel-core-side-{floor}-{side}-{i}',xa,xb,c,d,lo,hi)
box('hotel-core-divider',0,.2,-5.8,5.8,0,3.36)
# Scissor flights, thin tread undersides; ground arcade has 3.36 m clear headroom.
for floor in range(3):
    west=floor%2==0;xa,xb=(-2.15,-.05) if west else (.2,2.2)
    for i in range(24):
        z=-4.8+i*.4 if west else 4.4-i*.4;y=floor*3.6+(i+1)*.15
        box(f'hotel-stair-{floor}-{i}',xa,xb,z,z+.42,0 if floor==0 else y-.22,y)
    landz=4.8 if west else -5.8
    box(f'hotel-stair-landing-{floor}',-2.4,2.4,landz,landz+1,3.6*(floor+1)-.24,3.6*(floor+1))
# Roof stair house (open door at north), inaccessible top.
box('hotel-stairhouse-west',-2.4,-2.2,2.8,4.2,10.8,13.4)
box('hotel-stairhouse-east',2.2,2.4,2.8,4.2,10.8,13.4)
box('hotel-stairhouse-roof',-2.4,2.4,2.8,5.8,13.2,13.4)
pilot.append(dict(id='hotel-stairhouse-top',center=v(0,0,4.3),size=v(4.8,0,3),bottom=13.3))
for town,sign in [('ashby',1),('bray',-1)]:
    def B(id,a,b,c,d,lo,hi,*args,**kwargs):return box(id,*sorted((sign*a,sign*b)),*sorted((sign*c,sign*d)),lo,hi,*args,**kwargs)
    for i,x in enumerate([-24.5,-15.5,-6.5]):B(town+'-pier-'+str(i+1),x-.5,x+.5,20,21,0,3.6,'structural','brick',hp=100)
    # Rear piers are static M1; tenement floors are deferred.
    for i,x in enumerate([-24.5,-6.5]):B(town+'-rear-pier-'+str(i),x-.5,x+.5,12,13,0,3.6)
    B(town+'-core-wall',-20,-19.8,12.3,16.7,0,7.2)
    for y in [3.6,7.2]:
        B(town+'-floor-lane-'+str(y),-25,-6,16.7,21,y-.24,y)
        B(town+'-floor-east-'+str(y),-9.6,-6,12,16.7,y-.24,y)
        B(town+'-floor-west-'+str(y),-25,-20,12,16.7,y-.24,y)
    # Two opposed walking flights in the 10.4 x 4.4 core.
    for f in range(2):
        for i in range(24):
            x=-19.8+i*.4 if f==0 else -10.2-i*.4;y=f*3.6+(i+1)*.15;z=12.5 if f==0 else 14.7
            B(f'{town}-stair-{f}-{i}',x,x+.42,z,z+1.8,y-.22,y)
    B(town+'-stair-landing-0',-10.6,-9.6,12.5,16.7,3.36,3.6)
    B(town+'-stair-landing-1',-20,-19,12.5,16.7,6.96,7.2)
    sid='bridge-'+town
    B(sid+'-bearing',-8,-6,11.5,12.5,6.4,7.2,'structural','metal',hp=60)
    B(sid+'-deck',-8,-6,6,12,6.96,7.2,'carried','metal',sid)
    for x in [-8,-6.15]:B(sid+'-rail-'+str(x),x,x+.15,6,12,7.2,8.3,'carried','metal',sid)
    sid='stack-'+('n' if sign==1 else 's')
    B(sid+'-base',8.8,11.2,25.6,28,0,2,'structural','brick',hp=180)
    for i in range(4):B(sid+'-drum-'+str(i),8.8,11.2,25.6,28,2+i*3.5,5.5+i*3.5,'carried','brick',sid)
    pilot.append(dict(id=sid+'-top',center=v(sign*10,0,sign*26.8),size=v(2.4,0,2.4),bottom=15.9))
    # Shop ramps: 8 m run, 3.6 rise (24.2 degrees). Collision stairs, each .15 riser.
    for i in range(24):B('shop-ramp-'+town+'-'+str(i),-2+i/3,-2+(i+1)/3,19.5,21,0,(i+1)*.15)
    for i in range(8):B('yard-dock-ramp-'+town+'-'+str(i),-25+i*.325,-25+(i+1)*.325,-25.5,-23,0,1.2-i*.15)
# Capsule-clear bridge portals on the second floor; ground LOS stays authored.
for b in blocks:
    if b['id'] in ('ashby-ashby-south4','bray-ashby-south4'):
        b['pos']['y']=3.48;b['size']['y']=6.96
    if b['id'] in ('hotel-nw-hotel-n0--8-5.85-2','hotel-se-hotel-s6-7--6.15-2'):
        b['pos']['x']=math.copysign(7.8,b['pos']['x']);b['size']['x']=.4;b['mass']*=.4
# Segment local authoring and permanent rubble. Fall-sweep definitions are used by both sim and checks.
for s in segments:
    sid=s['id'];owned=[b for b in blocks if b.get('segment')==sid];s['blocks']=[b['id'] for b in owned];s['mass']=round(sum(b['mass'] for b in owned)/1000,4)
    for b in owned:
        p=b['pos'];floor=max(0,int((p['y']-.1)/3.6));pivot=v(p['x'],max(0,floor*3.6),p['z']);axis=v(1);angle=0;drop=max(0,p['y']-.7);delay=0
        if s['mode']=='pancake':
            if b['role']=='carried':delay={2:0,1:.25,0:.45}.get(floor,0)
            else:
                # Storey-height panel hinges at its floor, then drops independently.
                sx=-1 if '-w-' in b['id'] or abs(p['x'])>7.5 and p['x']<0 else 1
                if abs(p['z'])>5:axis=v(1);angle=math.copysign(35,p['z'])
                else:axis=v(0,0,1);angle=-math.copysign(35,p['x'])
                drop=max(0,floor*3.6);delay=(2-min(2,floor))*.12
        elif s['mode']=='tear-hinge':pivot=v(p['x'],7.2,12 if 'ashby' in sid else -12);angle=-55 if 'ashby' in sid else 55;drop=3;delay=0
        elif s['mode']=='peel':angle=45 if 'ashby' in sid else -45;drop=max(0,floor*3.6);delay=(2-min(2,floor))*.12
        else:pivot=v(p['x'],0,p['z']);angle=-90 if sid=='stack-n' else 90;drop=0
        s['poses'].append(dict(block=b['id'],pivot=pivot,axis=axis,angle=angle,drop=drop,delay=delay))
    rubble=s['rubble']
    if s['mode']=='pancake':
        sx=-1 if sid.endswith(('nw','sw')) else 1;sz=1 if sid.endswith(('nw','ne')) else -1
        # .2 m steps along a <=22-degree ramp; apron and seam remain open.
        for i in range(14):
            x0=2.4+i*.5;x1=x0+.5;h=max(.6,3.4-i*.2)
            box(f'rubble-{sid}-{i}',*sorted((sx*x0,sx*x1)),*sorted((sz*2.6,sz*(3.4 if sid=='hotel-sw' and i<4 else 5.2))),0,h,'rubble','stone',target=rubble)
        box(f'rubble-{sid}-apron',*sorted((sx*2.4,sx*9.4)),*sorted((sz*.8,sz*2.6)),0,.4,'rubble','stone',target=rubble)
    elif s['mode']=='tear-hinge':
        sign=1 if sid.endswith('ashby') else -1
        box('rubble-'+sid,-8,-6,8,9.8,0,1.2,'rubble','metal',target=rubble) if sign==1 else box('rubble-'+sid,6,8,-9.8,-8,0,1.2,'rubble','metal',target=rubble)
    elif s['mode']=='peel':
        sign=1 if sid.endswith('ashby') else -1
        for i in range(16):
            h=.45 if i in (3,4,10,11) else 1.2
            box(f'rubble-{sid}-{i}',*sorted((sign*(-23+i),sign*(-22+i))),*sorted((sign*21,sign*23.8)),0,h,'rubble','brick',target=rubble)
    else:
        sign=1 if sid=='stack-n' else -1
        for i,z in enumerate([26.8,23.5,20,17,14,11]):
            h=2 if i==0 else 2.4 if i==1 else 1.3
            box(f'rubble-{sid}-{i}',10*sign-1.2,10*sign+1.2,z*sign-.8,z*sign+.8,0,h,'rubble','brick',target=rubble)
# Widen telegraph/hazard sweeps to contain every authored corner pose, including
# the chimney's initial rear edge and bridge rails. Data checks independently
# sample the C# pose function against these conservative boxes.
def smooth(t):
    t=max(0,min(1,t));return t*t*(3-2*t)
def rotate(vv,axis,degrees):
    a=math.radians(degrees);c=math.cos(a);ss=math.sin(a);dot=sum(vv[i]*axis[i] for i in range(3))
    cross=[axis[1]*vv[2]-axis[2]*vv[1],axis[2]*vv[0]-axis[0]*vv[2],axis[0]*vv[1]-axis[1]*vv[0]]
    return [vv[i]*c+cross[i]*ss+axis[i]*dot*(1-c) for i in range(3)]
for segdef in segments:
    sweep=segdef['sweeps'][0];lo=[sweep['center'][k]-sweep['size'][k]/2 for k in 'xyz'];hi=[sweep['center'][k]+sweep['size'][k]/2 for k in 'xyz']
    for pose in segdef['poses']:
        b=next(b for b in blocks if b['id']==pose['block']);axis=[pose['axis'][k] for k in 'xyz'];pivot=[pose['pivot'][k] for k in 'xyz']
        duration=math.sqrt(6*pose['drop']/6.9) if segdef['mode']=='pancake' and pose['angle']==0 else max(.01,segdef['duration']-pose['delay'])
        for tick in range(241):
            time=-segdef['telegraph']+(segdef['telegraph']+pose['delay']+duration)*tick/240
            age=max(0,time-pose['delay']);u=smooth(age/max(.01,duration));angle=pose['angle']*u;drop=pose['drop']*u
            if segdef['mode']=='tear-hinge':angle=pose['angle']*smooth(age/.7);drop=pose['drop']*smooth((age-.3)/.4)
            if segdef['mode']=='topple':
                lean=math.copysign(8,pose['angle']);angle=lean*smooth((time+segdef['telegraph'])/segdef['telegraph']) if time<0 else lean+(pose['angle']-lean)*u
            for corner in range(8):
                point=[b['pos'][k]+b['size'][k]/2*(1 if corner>>i&1 else -1)-pivot[i] for i,k in enumerate('xyz')];point=rotate(point,axis,angle)
                point=[point[i]+pivot[i]-(drop if i==1 else 0) for i in range(3)]
                for i in range(3):lo[i]=min(lo[i],point[i]-.02);hi[i]=max(hi[i],point[i]+.02)
    sweep['center']=v(*[(a+b)/2 for a,b in zip(lo,hi)]);sweep['size']=v(*[b-a for a,b in zip(lo,hi)])
# Minimal ground grid authored around static and intact obstacles. Re-evaluate links on collapse.
def clear(x,z):
    for b in blocks:
        p=b['pos'];d=b['size']
        if p['y']-d['y']/2<1.7 and p['y']+d['y']/2>.01 and abs(x-p['x'])<d['x']/2+.46 and abs(z-p['z'])<d['z']/2+.46:return False
    return True
for x in range(-29,30):
 for z in range(-25,26):
    if clear(x,z):nodes.append(dict(id=f'g{x},{z}',pos=v(x,.9,z),layer=0))
N={n['id']:n for n in nodes}
for n in nodes:
    x=int(n['pos']['x']);z=int(n['pos']['z'])
    for dx,dz in [(1,0),(0,1)]:
        to=f'g{x+dx},{z+dz}'
        if to in N:links.append(dict(id=n['id']+'>'+to,a=n['id'],b=to,kind='walk',requires=[],after=[]))
# Full scissor stair sequence, including walkable landings; connect ground start to nearest node.
path=[]
for f in range(3):
    west=f%2==0;xc=-1.1 if west else 1.2
    for i in range(13):
        z=-4.9+i*.8 if west else 4.9-i*.8;y=f*3.6+i*.3+.9
        id=f'stair-{f}-{i}';nodes.append(dict(id=id,pos=v(xc,y,z),layer=f+1));path.append(id)
for a,b in zip(path,path[1:]):links.append(dict(id=a+'>'+b,a=a,b=b,kind='stair',requires=[],after=[]))
first=next(n for n in nodes if n['id']==path[0]);nearest=min((n for n in nodes if n['layer']==0),key=lambda n:sum((n['pos'][k]-first['pos'][k])**2 for k in 'xyz'))
links.append(dict(id='stair-entry',a=nearest['id'],b=path[0],kind='stair',requires=[],after=[]))
def node(id,x,y,z,layer):
    nodes.append(dict(id=id,pos=v(x,y,z),layer=layer));return id
def link(a,b,kind='walk',requires=None,after=None):
    id=a+'>'+b;links.append(dict(id=id,a=a,b=b,kind=kind,requires=requires or [],after=after or []));return id
# Leave the core through each floor's real side opening; direct diagonals hit its wall.
for floor in range(3):
    door_z=5.1 if floor%2==0 else -5.1;y=4.5+floor*3.6
    for sx in [-1,1]:
        wing=('n' if door_z>0 else 's')+('w' if sx<0 else 'e');entry='hotel-'+wing
        inside=node(f'hotel-door-{floor}-{sx}',sx*1.2,y,door_z,floor+1)
        outside=node(f'hotel-exit-{floor}-{sx}',sx*3,y,door_z,floor+1)
        link(f'stair-{floor}-12',inside,'stair');link(inside,outside,'stair',requires=[entry])
        for sz in [-1,1]:
            sid='hotel-'+('n' if sz>0 else 's')+('w' if sx<0 else 'e')
            room=node(sid+'-nav-'+str(floor),sx*5.2,y,sz*3,floor+1)
            corner=node(sid+'-corner-'+str(floor),sx*5.2,y,door_z,floor+1)
            link(outside,corner,requires=[entry,sid]);link(corner,room,requires=[entry,sid])
# Tenement switchback stairs and roof galleries link to the two suspended bridges.
for town,sign in [('ashby',1),('bray',-1)]:
    route=[]
    for floor in range(2):
        for i in range(13):
            x=-19.6+i*.8 if floor==0 else -10.1-i*.8;z=13.4 if floor==0 else 15.6
            route.append(node(f'{town}-stair-nav-{floor}-{i}',sign*x,floor*3.6+i*.3+.9,sign*z,floor+1))
    for a,b in zip(route,route[1:]):link(a,b,'stair')
    # Entry approaches along the rear opening; do not cut through the static core wall.
    start=next(n for n in nodes if n['id']==route[0])
    ground=min((n for n in nodes if n['layer']==0 and sign*n['pos']['z']<12),key=lambda n:sum((n['pos'][k]-start['pos'][k])**2 for k in 'xyz'))
    link(ground['id'],route[0],'stair')
    gallery=node(town+'-gallery',-19.4*sign,8.1,18*sign,2);link(route[-1],gallery)
    east=node(town+'-roof-east',-7*sign,8.1,18*sign,2);link(gallery,east)
    rear=node(town+'-roof-bridge',-7*sign,8.1,12.8*sign,2);link(east,rear)
    bridge='bridge-'+town;hotel='hotel-'+('nw' if sign==1 else 'se');route=[rear]
    for i,z in enumerate((11,9,7,5.1)):route.append(node(bridge+'-nav-'+str(i),-6.9*sign,8.1,z*sign,2))
    route.append(hotel+'-nav-1')
    for a,b in zip(route,route[1:]):link(a,b,requires=[bridge,hotel])
# Authored rubble ramps reconnect the street to the hotel's first-floor landings.
for s in segments:
    if s['mode']!='pancake':continue
    sid=s['id'];sx=-1 if sid.endswith(('nw','sw')) else 1;sz=1 if sid.endswith(('nw','ne')) else -1
    route=[]
    for i in reversed(range(14)):
        id=f'{sid}-rubble-nav-{i}';route.append(id)
        nodes.append(dict(id=id,pos=v(sx*(2.65+i*.5),max(.6,3.4-i*.2)+.9,sz*3),layer=1))
    def rubble_link(a,b,kind='walk'):
        id=a+'>'+b;links.append(dict(id=id,a=a,b=b,kind=kind,requires=[],after=[sid]));s['nav']['add'].append(id)
    for a,b in zip(route,route[1:]):rubble_link(a,b)
    street=min((n for n in nodes if n['layer']==0 and n['pos']['x']*sx>9.6),key=lambda n:(n['pos']['x']-sx*9.65)**2+(n['pos']['z']-sz*3)**2)
    rubble_link(street['id'],route[0],'jump')
    # North core landing is always static; approach along the seam of the remaining floor.
    if sz>0:
        outside=node(sid+'-rubble-door',sx*2.65,4.3,5.1,1)
        rubble_link(route[-1],outside);rubble_link(outside,'hotel-door-0-'+str(sx))
for s in segments:
    s['nav']['remove']=[l['id'] for l in links if s['id'] in l.get('requires',[])]
def spawn(x,z):return dict(pos=v(x,1.1,z),yaw=math.degrees(math.atan2(-x,-z))%360)
m=dict(id='brickfall',name='Brickfall',tag='Condemned brick district. Blow the piers, rewrite the streets.',theme='brickfall',music='brickfall-demolition-row',ambience='brickfall',look='golden-demolition',sky=0x8899aa,floor=0x444444,accent=0xf4b321,rotation=True,bounds=dict(minX=-30,maxX=30,minZ=-26,maxZ=26,ceiling=24,killY=-6),spawns=dict(ffa=[spawn(*p) for p in [(-16,9.5),(16,-9.5),(-27,-21),(27,21),(-4,16),(4,-16),(-27.5,19),(27.5,-19)]],teams=[dict(side=i,points=[spawn(*p) for p in points]) for i,points in enumerate([draft.teamA,draft.teamB])]),blocks=blocks,segments=segments,pilotBlocks=pilot,nav=dict(nodes=nodes,links=links))
out=dict(schema=1,materials=[dict(id='brick',color=10242608,mass=26,durability=120,friction=.85,indestructible=False)],maps=[m])
path=ROOT/'unity/ChipArenaConversion/Assets/Conversion/Resources/UnityMaps.json';path.write_text(json.dumps(out,separators=(',',':'))+'\n')
print(f'Brickfall: {len(blocks)} blocks, {sum(b["role"]!="static" for b in blocks)} props, {len(segments)} segments, {len(nodes)} nav nodes, {len(links)} links')
