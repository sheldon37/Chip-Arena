#!/usr/bin/env python3
"""Integrate Claude's frozen stage layouts, adding capsule-clear, supported navigation.
No changes to authored geometry, identifiers, materials or hazard timing; the M1 maps' pilot columns are filtered (open_tops), the four new
stages' kept only at the map's edges and, for bots, at lethal drops (open_stage).
"""
import importlib.util,json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
# The 2026-11-11 rebuilds: the stage's new draft (its M1 script stays as history).
REBUILT={'skyframe':'m2'}
def draft(name,fn):
    spec=importlib.util.spec_from_file_location(name,ROOT/f'docs/arenas/scripts/{name}_{REBUILT.get(name,"m1")}.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return getattr(m,fn)()['maps'][0]
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
    nodes=[];grid={};B=m['bounds'];step=.5 if m['id']=='rig' else 1
    def samples(lo,hi):return [i*step for i in range(math.ceil((lo+1)/step),math.floor(hi/step))]
    for x in samples(B['minX'],B['maxX']):
        for z in samples(B['minZ'],B['maxZ']):
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
        if h['kind'] in ('maglev','grid','burner','gas','shutter'):return [(h['center'],h['size'])]
        if h['kind']=='crane':return [(z['center'],z['size']) for z in h['zones']]
        a,b=h['a'],h['b'];s=h['size']
        if h['kind']=='vent':return [(v(a['x'],a['y']+s['y']/2,a['z']),s)]
        return [(v(*[(a[k]+b[k])/2 for k in 'xyz']),v(*[abs(a[k]-b[k])+s[k] for k in 'xyz']))]
    links=[]
    for a in nodes:
        x,z=a['pos']['x'],a['pos']['z']
        for dx,dz in [(step,0),(0,step)]:
            for b in grid.get((x+dx,z+dz),[]):
                if not path(a,b):continue
                hazards=[h['id'] for h in m['hazards'] if any(crosses(a['pos'],b['pos'],c,s) for c,s in regions(h))]
                links.append(dict(id=a['id']+'>'+b['id'],a=a['id'],b=b['id'],kind='stair' if abs(a['pos']['y']-b['pos']['y'])>.05 else 'walk',requires=[],after=[],hazards=hazards))
    # Mark existing alcove nodes explicitly for evacuation/holding; no added geometry.
    for n in nodes:
        p=n['pos'];n['safe']=m['id']=='cybercity' and p['y']<0 and abs(p['z'])>=3.5
    m['nav']=dict(nodes=nodes,links=links)
    print(m['id'],len(m['blocks']),'blocks',len(nodes),'nodes',len(links),'links',sum(bool(l['hazards']) for l in links),'gated')
# Owner playtest (2026-11-01): the drafts' columns over low tops and rails read as invisible walls. A column stays only where
# a side opens onto a lethal drop (bots vault what their obstacle ray hits) or where no ordinary jump from beside it reaches
# it: standing body .9 + jump apex 8.5^2/(2*23) + the .85 head test in NativeMapRules.ExcludeTops. No fall damage exists.
# Sides past the bounds count: only bots, abilities and teleports are clamped to them (NativeMapRules.Clamp), not walking pilots.
REACH=3.4
def column_sides(m):
    """Each pilot column's id -> (lethal: a side opens onto a drop with no floor under it, rise: how far above each floor beside it
    its bottom stands)."""
    boxes=[bounds(b) for b in m['blocks']];out={}
    def floor(x,z,below):
        tops=[b[1][1] for b in boxes if b[0][0]-.001<=x<=b[0][1]+.001 and b[2][0]-.001<=z<=b[2][1]+.001 and b[1][1]<=below+.05]
        return max(tops) if tops else None
    for p in m['pilotBlocks']:
        cx,cz,sx,sz,bottom=p['center']['x'],p['center']['z'],p['size']['x'],p['size']['z'],p['bottom'];lethal=False;rise=[]
        for nx,nz in ((1,0),(-1,0),(0,1),(0,-1)):
            for k in range(7):
                t=(k/6-.5)*.9;x=cx+nx*(sx/2+.6)+(0 if nx else t*sx);z=cz+nz*(sz/2+.6)+(0 if nz else t*sz)
                f=floor(x,z,bottom)
                if f is None:lethal=True
                else:rise.append(bottom-f)
        out[p['id']]=(lethal,rise)
    return out
def open_tops(m):
    sides=column_sides(m);kept=[]
    for p in m['pilotBlocks']:
        lethal,rise=sides[p['id']]
        if lethal or not rise or min(rise)>REACH:kept.append(p)
    print(m['id'],'pilot columns',len(m['pilotBlocks']),'->',len(kept));m['pilotBlocks']=kept
# Owner (2026-11-10), on the four new stages: "remove the barriers that allow us to jump over things ... On the bridge, we can let
# people get knocked off the map. Its a instant death when falling off". There a column stays for everyone only where it keeps
# pilots inside the map (EDGES: the outer walls' caps, the club's crowd barrier and its mezzanines' rails over the crowd pit, the
# Sanctum's vault) or where the art hangs too close over a top to stand on it (OVERHANG: the club's lighting towers under their truss and
# its DJ cube under its rig, the Causeway's spires and gables under their floating crown stones). A column at a lethal drop (the Causeway's rails, parapets and crown edges) stops only bots (`bots`: they vault
# whatever their obstacle ray hits; NativeMapRules.ExcludeTops), so a player can jump over or be knocked over it and fall. Every
# other column over a top goes: a double jump (daggers, bow, chips) reaches the 3.9 m ovens, altars and gates, and a pilot may
# stand on anything they can reach.
EDGES={'club':('club-top-wall','club-top-barrier','club-top-rail-mezz'),'emporium':('emporium-top-wall',),'sanctum':('sanctum-top-vault',),'causeway':(),'skyframe':()}
OVERHANG={'club':('club-top-tower','club-top-cube'),'causeway':('causeway-spire-','causeway-gable-'),'skyframe':('skyframe-tree-',)}
def open_stage(m):
    sides=column_sides(m);keep=EDGES[m['id']]+OVERHANG.get(m['id'],());kept=[];bots=0
    for p in m['pilotBlocks']:
        if keep and p['id'].startswith(keep):kept.append(p)
        elif sides[p['id']][0]:kept.append(dict(p,bots=True));bots+=1
    print(m['id'],'pilot columns',len(m['pilotBlocks']),'->',len(kept),'(bots only:',str(bots)+')');m['pilotBlocks']=kept
path=ROOT/'unity/ChipArenaConversion/Assets/Conversion/Resources/UnityMaps.json'
content=json.loads(path.read_text())
for name,fn,song in [('factory','unity_json','factory-anvil-loop'),('cybercity','draft','cybercity-neon-riot')]+[(name,'unity_json','factory-anvil-loop' if name in ('harbor','rig','training') else 'cybercity-neon-riot') for name in ('breakwater','harbor','skyframe','rig','polar','training')]+[('club','unity_json','cybercity-neon-riot')]+[('emporium','unity_json','cybercity-neon-riot')]+[('causeway','unity_json','skyframe-rising-motif')]+[('sanctum','unity_json','moonlit-wilderness')]:
    m=draft(name,fn);m.pop('killVolumes',None);m['music']=song;m['rotation']=name!='training'
    if name in EDGES:open_stage(m)
    elif name not in ('factory','cybercity'):open_tops(m)
    navigation(m)
    content['maps']=[old for old in content['maps'] if old['id']!=name]+[m]
# LF on every platform: the lock pins the exact bytes.
path.write_bytes((json.dumps(content,separators=(',',':'))+'\n').encode())

# Source packaging pins generated map content as well as large media assets.
import hashlib
lockpath=ROOT/'unity/ChipArenaConversion/tools/asset-lock.json'
lock=json.loads(lockpath.read_text());data=path.read_bytes();rel='Assets/Conversion/Resources/UnityMaps.json'
lock['assets']=[a for a in lock['assets'] if a['path']!=rel]+[dict(path=rel,bytes=len(data),gitBlob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest(),source='Generated by tools/generate_stage_hazards.py from Claude stage drafts and existing Brickfall')]
lockpath.write_bytes((json.dumps(lock,indent=2)+'\n').encode())
