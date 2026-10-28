#!/usr/bin/env python3
"""Integrate Claude's frozen stage layouts, adding capsule-clear, supported navigation.
No changes to authored geometry, identifiers, materials or hazard timing.
"""
import importlib.util,json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
def draft(name,fn):
    spec=importlib.util.spec_from_file_location(name,ROOT/f'docs/arenas/scripts/{name}_m1.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return getattr(m,fn)()['maps'][0]
def v(x,y,z):return dict(x=round(x,4),y=round(y,4),z=round(z,4))
def bounds(b):return tuple((b['pos'][k]-b['size'][k]/2,b['pos'][k]+b['size'][k]/2) for k in 'xyz')
def navigation(m):
    boxes=[bounds(b) for b in m['blocks']];bins={}
    for box in boxes:
        for x in range(math.floor(box[0][0]-.5),math.ceil(box[0][1]+.5)+1):
            for z in range(math.floor(box[2][0]-.5),math.ceil(box[2][1]+.5)+1):bins.setdefault((x,z),[]).append(box)
    def near(x,z):return bins.get((math.floor(x),math.floor(z)),[])
    def tops(x,z):return sorted(set(round(b[1][1],4) for b in near(x,z) if b[0][0]-.001<=x<=b[0][1]+.001 and b[2][0]-.001<=z<=b[2][1]+.001))
    def clear(x,y,z):
        for p in m['pilotBlocks']:
            if y+1.7>=p['bottom'] and abs(x-p['center']['x'])<p['size']['x']/2+.43 and abs(z-p['center']['z'])<p['size']['z']/2+.43:return False
        # Stair risers up to .25m can be stepped; use the actual supported top at each sample.
        for b in near(x,z):
            if b[1][1]<=y+.01 or b[1][0]>=y+1.75:continue
            if b[0][0]-.43<x<b[0][1]+.43 and b[2][0]-.43<z<b[2][1]+.43:
                if b[1][1]>y+.65:return False
        return True
    nodes=[];grid={};B=m['bounds']
    for x in range(math.ceil(B['minX']+1),math.floor(B['maxX'])):
        for z in range(math.ceil(B['minZ']+1),math.floor(B['maxZ'])):
            for y in tops(x,z):
                if clear(x,y,z):
                    n=dict(id=f'{m["id"]}-nav-{x},{z},{y:g}',pos=v(x,y+.9,z),layer=round(y*10));nodes.append(n);grid.setdefault((x,z),[]).append(n)
    def path(a,b):
        pa,pb=a['pos'],b['pos'];dy=pb['y']-pa['y']
        if abs(dy)>1.01:return False
        prev=pa['y']-.9
        for i in range(1,7):
            u=i/6;x=pa['x']+(pb['x']-pa['x'])*u;z=pa['z']+(pb['z']-pa['z'])*u;expected=pa['y']-.9+dy*u
            candidates=[y for y in tops(x,z) if abs(y-expected)<.35]
            if not candidates:return False
            y=min(candidates,key=lambda y:abs(y-expected))
            if abs(y-prev)>.321 or not clear(x,y,z):return False
            prev=y
        return True
    def crosses(a,b,c,s,margin=.5):
        lo,hi=0.,1.
        for k in 'xyz':
            low=c[k]-s[k]/2-margin;high=c[k]+s[k]/2+margin;delta=b[k]-a[k]
            if abs(delta)<1e-9:
                if not low<=a[k]<=high:return False
            else:
                aa,bb=sorted(((low-a[k])/delta,(high-a[k])/delta));lo=max(lo,aa);hi=min(hi,bb)
                if lo>hi:return False
        return True
    def regions(h):
        if h['kind'] in ('maglev','grid'):return [(h['center'],h['size'])]
        if h['kind']=='crane':return [(z['center'],z['size']) for z in h['zones']]
        a,b=h['a'],h['b'];s=h['size']
        if h['kind']=='vent':return [(v(a['x'],a['y']+s['y']/2,a['z']),s)]
        return [(v(*[(a[k]+b[k])/2 for k in 'xyz']),v(*[abs(a[k]-b[k])+s[k] for k in 'xyz']))]
    links=[]
    for a in nodes:
        x,z=a['pos']['x'],a['pos']['z']
        for dx,dz in [(1,0),(0,1)]:
            for b in grid.get((x+dx,z+dz),[]):
                if not path(a,b):continue
                hazards=[h['id'] for h in m['hazards'] if any(crosses(a['pos'],b['pos'],c,s) for c,s in regions(h))]
                links.append(dict(id=a['id']+'>'+b['id'],a=a['id'],b=b['id'],kind='stair' if abs(a['pos']['y']-b['pos']['y'])>.05 else 'walk',requires=[],after=[],hazards=hazards))
    # Mark existing alcove nodes explicitly for evacuation/holding; no added geometry.
    for n in nodes:
        p=n['pos'];n['safe']=m['id']=='cybercity' and p['y']<0 and abs(p['z'])>=3.5
    m['nav']=dict(nodes=nodes,links=links)
    print(m['id'],len(m['blocks']),'blocks',len(nodes),'nodes',len(links),'links',sum(bool(l['hazards']) for l in links),'gated')
path=ROOT/'unity/ChipArenaConversion/Assets/Conversion/Resources/UnityMaps.json'
content=json.loads(path.read_text())
for name,fn,song in [('factory','unity_json','factory-anvil-loop'),('cybercity','draft','cybercity-neon-riot')]:
    m=draft(name,fn);m['music']=song;m['rotation']=True;navigation(m)
    content['maps']=[old for old in content['maps'] if old['id']!=name]+[m]
path.write_text(json.dumps(content,separators=(',',':'))+'\n')

# Source packaging pins generated map content as well as large media assets.
import hashlib
lockpath=ROOT/'unity/ChipArenaConversion/tools/asset-lock.json'
lock=json.loads(lockpath.read_text());data=path.read_bytes();rel='Assets/Conversion/Resources/UnityMaps.json'
lock['assets']=[a for a in lock['assets'] if a['path']!=rel]+[dict(path=rel,bytes=len(data),gitBlob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest(),source='Generated by tools/generate_stage_hazards.py from Claude stage drafts and existing Brickfall')]
lockpath.write_text(json.dumps(lock,indent=2)+'\n')
