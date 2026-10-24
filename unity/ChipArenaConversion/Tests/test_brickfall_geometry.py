"""Collision-data checks, independent of the draft painter. No Unity/PhysX claims."""
import itertools,json,math,unittest
from pathlib import Path
R=Path(__file__).resolve().parents[1]
M=json.loads((R/'Assets/Conversion/Resources/UnityMaps.json').read_text())['maps'][0]
B=M['blocks'];S=M['segments'];radius=.42

def bounds(b):
 p=b['pos'];d=b['size'];return [(p[k]-d[k]/2,p[k]+d[k]/2) for k in 'xyz']
def intersects(a,b,box,padding=0):
 lo=0;hi=1
 for k,(mn,mx) in zip('xyz',bounds(box)):
  d=b[k]-a[k];mn-=padding;mx+=padding
  if abs(d)<1e-8:
   if not mn<a[k]<mx:return False
  else:
   u=(mn-a[k])/d;v=(mx-a[k])/d;lo=max(lo,min(u,v));hi=min(hi,max(u,v))
   if lo>=hi:return False
 return True

def visible(a,b,eye):return not any(intersects(dict(x=a[0],y=eye,z=a[1]),dict(x=b[0],y=eye,z=b[1]),c) for c in B)
def dist_rect(p,v):return math.hypot(max(abs(p['x']-v['center']['x'])-v['size']['x']/2,0),max(abs(p['z']-v['center']['z'])-v['size']['z']/2,0))
class BrickfallGeometry(unittest.TestCase):
 def test_spawn_safety_and_los(self):
  f=M['spawns']['ffa'];a,b=[t['points'] for t in M['spawns']['teams']]
  pairs=list(itertools.combinations(f,2))+list(itertools.product(a,b));bad=[]
  for i,(s,t) in enumerate(pairs):
   for dx,dz,ex,ez in itertools.product((-1,0,1),repeat=4):
    for eye in (1.2,1.6,1.9):
     if visible((s['pos']['x']+dx,s['pos']['z']+dz),(t['pos']['x']+ex,t['pos']['z']+ez),eye):bad.append(i);break
    if i in bad:break
  self.assertEqual(bad,[],f'exposed pair indexes {bad}')
  allpoints=f+a+b
  nearest=min((dist_rect(p['pos'],v),s['id']) for p in allpoints for s in S for v in s['sweeps'])
  self.assertGreaterEqual(nearest[0],3,nearest)
  print('Brickfall collision LOS: 28 FFA + 16 team pairs x 243 variants; nearest sweep',nearest)
 def test_hotel_openings(self):
  n=[(-7,-5.4),(.2,2.2),(3.5,7)];s=[(-7,-3.5),(.2,2.2),(5.4,7)];samples=lambda a:[a[0]+(a[1]-a[0])*(i+.5)/9 for i in range(9)]
  openings=[(i,j) for i,x in enumerate(n) for j,z in enumerate(s) for a in samples(x) for b in samples(z) if visible((a,6.4),(b,-6.4),1.6)]
  self.assertEqual(len(openings),81);self.assertTrue(all(x==(1,1) for x in openings))
  self.assertFalse(any(visible((-8.4,a),(8.4,b),1.6) for a in samples((1,3)) for b in samples((-3,-1))))
 def test_crush_volumes_clear_of_unrelated_props(self):
  bad=[]
  for s in S:
   own=set(s['blocks'])|{p.get('member') for p in s['supports']};c=s['crush']
   for b in B:
    if b['role']=='static' or b['id'] in own:continue
    if all(abs(b['pos'][k]-c['center'][k])<(b['size'][k]+c['size'][k])/2-.01 for k in 'xyz'):bad.append((s['id'],b['id']))
  self.assertEqual(bad,[])
 def test_authored_navigation_clearance(self):
  nodes={n['id']:n for n in M['nav']['nodes']};bad=[]
  for l in M['nav']['links']:
   if l.get('kind','walk')=='stair' or l.get('after'):continue
   a=nodes[l['a']]['pos'];b=nodes[l['b']]['pos']
   for c in B:
    # <=0.3 m lips are small steps; full-height capsule clearance otherwise.
    if c['pos']['y']+c['size']['y']/2<=min(a['y'],b['y'])-.85+.3:continue
    expanded=dict(c,size={k:c['size'][k]+(1.7 if k=='y' else .84) for k in 'xyz'})
    if intersects(a,b,expanded):bad.append((l['id'],c['id']));break
  self.assertEqual(bad,[])
  adj={n:[] for n in nodes}
  for l in M['nav']['links']:
   if not l.get('after'):adj[l['a']].append(l['b']);adj[l['b']].append(l['a'])
  seen={'stair-0-0'};queue=list(seen)
  for n in queue:
   for nxt in adj[n]:
    if nxt not in seen:seen.add(nxt);queue.append(nxt)
  self.assertTrue(all(f'stair-{f}-12' in seen for f in range(3)))
  self.assertTrue(all(f'bridge-{town}-nav-1' in seen for town in ('ashby','bray')))
  print('Brickfall nav: capsule clearance on walk links; hotel floors and both bridges reachable in authored graph')
 def test_ground_connectivity_1024(self):
  # Capsule-width raster at 1 m resolution. Low <=1.2 m cover is hop-traversable;
  # upper-floor interiors are not ground cells. Checks spawn components in every state.
  coords=[(x,z) for z in range(-25,26) for x in range(-29,30)];index={p:i for i,p in enumerate(coords)}
  adj=[[index[q] for q in ((x-1,z),(x+1,z),(x,z-1),(x,z+1)) if q in index] for x,z in coords]
  def mask(boxes):
   result=set()
   for b in boxes:
    (x0,x1),(y0,y1),(z0,z1)=bounds(b)
    if y0>=1.7 or y1<=1.2:continue
    for z in range(max(-25,math.ceil(z0-radius)),min(25,math.floor(z1+radius))+1):
     for x in range(max(-29,math.ceil(x0-radius)),min(29,math.floor(x1+radius))+1):result.add(index[x,z])
   return result
  fixed=mask([b for b in B if not b.get('segment') and b['role']!='structural']);intact=[];rubble=[]
  for s in S:
   intact.append(mask([b for b in B if b.get('segment')==s['id'] or b['id'] in [p.get('member') for p in s['supports']]]));
   heights={}
   for b in s['rubble']:
    (x0,x1),(y0,y1),(z0,z1)=bounds(b)
    for z in range(max(-25,math.ceil(z0-radius)),min(25,math.floor(z1+radius))+1):
     for x in range(max(-29,math.ceil(x0-radius)),min(29,math.floor(x1+radius))+1):heights[index[x,z]]=max(heights.get(index[x,z],0),y1)
   rubble.append(heights)
  points=M['spawns']['ffa']+sum([t['points'] for t in M['spawns']['teams']],[])
  starts=[index[round(p['pos']['x']),round(p['pos']['z'])] for p in points];bad=[]
  for state in range(1024):
   blocked=set(fixed);heights={}
   for i in range(10):
    if state>>i&1:
     for cell,h in rubble[i].items():heights[cell]=max(heights.get(cell,0),h)
    else:blocked.update(intact[i])
   seen={starts[0]};queue=[starts[0]]
   for x in queue:
    for y in adj[x]:
     if y not in blocked and y not in seen and abs(heights.get(x,0)-heights.get(y,0))<=1.20001:seen.add(y);queue.append(y)
   if any(s not in seen or s in blocked for s in starts):bad.append(state)
   # High isolated chimney drums are cover tops, not ground-route destinations.
   missing=[i for i in range(len(coords)) if i not in blocked and i not in seen and heights.get(i,0)<=1.2]
   if missing:bad.append(('disconnected',state,[coords[i] for i in missing]))
  self.assertEqual(bad,[])
  print('Brickfall: all 1024 rubble combinations connect every walkable ground cell and all 16 authored spawn entries on capsule-width ground/rubble raster')
if __name__=='__main__':unittest.main()
