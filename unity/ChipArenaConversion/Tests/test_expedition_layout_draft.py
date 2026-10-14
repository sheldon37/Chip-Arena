"""The Expedition layout drafts (docs/arenas/scripts/expedition_layout.py with expedition_city.py and expedition_ward_zero.py)
and their fixtures (Tests/expedition-city-draft.json, expedition-ward-zero-draft.json, expedition-layout-draft.json, read by
ExpeditionLayoutChecks.cs) agree, the generators are byte-identical across runs and their own checks pass (floor reached
from the sanctuary with the final gate shut and open, the plaza only through the gate volume, every record on walkable
floor with pilot or Hulk clearance, no spawn in or near the sanctuary, Siren's entries unblocked by the instance overlay);
the MapLooks.json expedition-city entry is the generator's look; the manifest keeps the contract's shape and its block
hashes match the fixtures' maps. Static analysis only: no Unity/PhysX claims. The script tests skip without docs/."""
import hashlib,importlib.util,json,subprocess,sys,tempfile,unittest
from pathlib import Path
R=Path(__file__).resolve().parents[1];D=R.parents[1]/'docs/arenas/scripts';SCRIPT=D/'expedition_layout.py'
F={k:R/'Tests'/n for k,n in (('city','expedition-city-draft.json'),('ward','expedition-ward-zero-draft.json'),('json','expedition-layout-draft.json'))}
REGEN='regenerate: python3 docs/arenas/scripts/expedition_layout.py --fixtures (and bump REVISION there when a record or hash changes)'
PINNED=('exp-layout-1','670dc3f307deb0f8967a1549066665b26862bbab63a961ba745a7437d4f30745','5063452a19a77b30b8bf3a30bc990119b932798fa7daabf023bf7340276efb90')
def load(k):return json.loads(F[k].read_text())
def _i(x,k):return str(int(round(float(x)*k)))
def block_hash(m):
 L=[]
 for b in m['blocks']:
  p,s,r=b['pos'],b['size'],b['rotation'];L.append('|'.join(['B',b['id'],b['material'],b['role'],_i(b['hp'],1e4)]+[_i(p[k],1e4) for k in 'xyz']+[_i(s[k],1e4) for k in 'xyz']+[_i(r[k],1e6) for k in 'xyzw']))
 for r in m.get('ramps') or []:L.append('|'.join(['R',r['id'],r['kind']]+[_i(r['low'][k],1e4) for k in 'xyz']+[_i(r['high'][k],1e4) for k in 'xyz']+[_i(r['width'],1e4),_i(r['bottom'],1e4),','.join(r['blocks'])]))
 for c in (m.get('navHints') or {}).get('collisionOff') or []:L.append('C|'+c)
 b=m['bounds'];L.append('|'.join(['K']+[_i(b[k],1e4) for k in ('minX','maxX','minZ','maxZ','ceiling','killY')]))
 return hashlib.sha256(''.join(l+'\n' for l in L).encode()).hexdigest()

@unittest.skipUnless(SCRIPT.exists(),'the docs are not in a packaged source tree')
class ExpeditionLayoutScripts(unittest.TestCase):
 def test_fixtures_are_the_scripts_output(self):
  with tempfile.TemporaryDirectory() as d:
   outs=[]
   for k in range(2):
    p={n:Path(d)/f'{n}{k}.json' for n in F};run=subprocess.run([sys.executable,str(SCRIPT)]+[a for n in F for a in (f'--{n}',str(p[n]))],capture_output=True,text=True)
    self.assertEqual(run.returncode,0,run.stdout[-3000:]+run.stderr[-3000:]);outs.append({n:p[n].read_bytes() for n in F})
   self.assertEqual(outs[0],outs[1],'the generators are deterministic')
   for n in F:self.assertEqual(json.loads(outs[0][n]),load(n),REGEN)
  for line in ('city checks: pass','instance checks: pass','manifest checks: pass','reached from the sanctuary slots (100.0%)','gate open: final plaza','reached (100.0%); whole town',
               "inside the gate volume's width",'spawns inside the sanctuary volume: 0','allow lists within the entry rules (Hulk: breach or Hulk drop; drops: brute; doors and vents: shambler, runner, spitter, brute): True',
               'reached from the entry slots (100.0%)','inherited entries (PveContent.json, 53 by e): exits on reached floor at their own level: 53','Siren blocks copied unchanged: True',
               'breach panels of the 8 breach entries present and unchanged: True','blockHash per area equals its generated map: True','door pairs (town door, instance exit) per instance: exp-ward-zero 1/1'):
   self.assertIn(line,run.stdout)
  self.assertRegex(run.stdout,r'gate shut: final plaza \d+ standable cells, 0 reached')
  self.assertNotIn('BAD ',run.stdout);self.assertNotIn('UNREACHED',run.stdout)
 def test_look_is_the_generators(self):
  spec=importlib.util.spec_from_file_location('expedition_city',D/'expedition_city.py');city=importlib.util.module_from_spec(spec);sys.path.insert(0,str(D))
  try:spec.loader.exec_module(city)
  finally:sys.path.remove(str(D))
  text=(R/'Assets/Conversion/Resources/MapLooks.json').read_text();looks=json.loads(text)['maps']
  self.assertIn(city.look_text(),text,'write it: python3 docs/arenas/scripts/expedition_city.py --maplooks')
  self.assertEqual(looks[-1],city.look())

class ExpeditionLayoutFixtures(unittest.TestCase):
 def test_manifest_shape(self):
  m=load('json');self.assertEqual(list(m),['schema','revision','world','instances','areas'])
  self.assertEqual((m['schema'],m['world'],m['instances']),(1,'exp-city',['exp-ward-zero']));self.assertEqual([a['id'] for a in m['areas']],['exp-city','exp-ward-zero'])
  w,i=m['areas']
  self.assertEqual(list(w),['id','role','map','blockHash','anchors','doors','interactions','sanctuary','gates','spawns','boss'])
  self.assertEqual(list(i),['id','role','map','blockHash','anchors','doors','interactions','spawns','boss'])
  self.assertEqual((w['role'],w['map'],i['role'],i['map'],i['spawns']),('world','expedition-city','instance','expedition-ward-zero','inherit:siren'))
  self.assertEqual((m['revision'],w['blockHash'],i['blockHash']),PINNED,'a generated record or map changed: '+REGEN+', then update PINNED here')
  for a in m['areas']:
   for x in a['anchors']:
    self.assertEqual(list(x),['id','kind','building','x','y','z','yaw']);self.assertIn(x['kind'],('door','machine','chest'));self.assertTrue(x['id'].startswith(a['id']+':'));self.assertTrue(0<=x['yaw']<360)
    self.assertEqual(x['kind']=='door',bool(x['building']))
   for d in a['doors']:
    self.assertEqual(list(d),['anchor','interact','closure','approach','returns' if a['role']=='world' else 'entries'])
    self.assertEqual(len(d['returns' if a['role']=='world' else 'entries']),8)
    for k in ('interact','closure'):self.assertEqual(list(d[k]),['x','y','z','sx','sy','sz','yaw'])
   for x in a['interactions']:self.assertEqual(list(x),['anchor','x','y','z','range'])
  self.assertEqual([x['id'] for x in w['anchors']],['exp-city:door:exp-ward-zero','exp-city:machine:0','exp-city:cache:0','exp-city:cache:1','exp-city:cache:2'])
  self.assertEqual([x['id'] for x in i['anchors']],['exp-ward-zero:door:exit','exp-ward-zero:cache:0','exp-ward-zero:cache:1'])
  self.assertEqual([x['kind'] for x in w['anchors']+i['anchors'] if 'cache' in x['id']],['chest']*5)
  self.assertEqual(len(w['sanctuary']['volumes']),1);self.assertEqual(len(w['sanctuary']['slots']),8);self.assertEqual(w['gates'],[dict(id='exp-city:gate:final',opens='final',closure=w['gates'][0]['closure'])])
  g=w['gates'][0]['closure'];self.assertEqual((g['sx'],g['sy'],g['sz']),(12,6,0.6))
  sp=w['spawns'];self.assertEqual([e['e'] for e in sp],list(range(len(sp))));self.assertEqual(len({e['id'] for e in sp}),len(sp))
  adds={a['id'] for a in json.loads((R/'Assets/Conversion/Resources/PveContent.json').read_text())['adds']}
  for e in sp:
   self.assertIn(e['kind'],('door','vent','drop','breach'));self.assertTrue(e['allow'] and set(e['allow'])<=adds,e['id']);self.assertEqual((e['level'],e['song'],e['pit'],e['exit']['y']),(0,-1,False,0),e['id'])
   self.assertEqual('hulk' in e['allow'],e['kind']=='breach' or e['kind']=='drop' and e['hulk'],e['id'])
 def test_maps_and_hashes(self):
  m=load('json');town=load('city')['maps'][0];inst=load('ward')['maps'][0]
  self.assertEqual(block_hash(town),m['areas'][0]['blockHash']);self.assertEqual(block_hash(inst),m['areas'][1]['blockHash'])
  siren=next(x for x in json.loads((R/'Assets/Conversion/Resources/UnityMaps.json').read_text())['maps'] if x['id']=='siren')
  self.assertEqual(inst['blocks'][:len(siren['blocks'])],siren['blocks'],'Siren changed: '+REGEN);self.assertEqual((inst['ramps'],inst['navHints']['collisionOff']),(siren['ramps'],siren['navHints']['collisionOff']))
  self.assertTrue(all(b['id'].startswith('expedition-ward-zero-') for b in inst['blocks'][len(siren['blocks']):]));self.assertEqual(inst['hazards'],[])
  for mp,prefix in ((town,'expedition-city-'),(inst,None)):
   ids=[b['id'] for b in mp['blocks']];self.assertEqual(len(ids),len(set(ids)));self.assertFalse(mp['rotation'])
   self.assertEqual(len(mp['spawns']['ffa']),8);self.assertEqual([len(t['points']) for t in mp['spawns']['teams']],[4,4])
   if prefix:self.assertTrue(all(i.startswith(prefix) for i in ids))
  self.assertEqual(town['bounds'],dict(minX=-60,maxX=60,minZ=-60,maxZ=60,ceiling=40,killY=-8));self.assertEqual(town['look'],'expedition-city')
  self.assertLessEqual(len(town['blocks']),400);self.assertTrue({b['material'] for b in town['blocks']}<={'stone','brick','metal','wood','foundation'})
  for b in town['blocks']:
   for k in 'xyz':self.assertGreater(b['size'][k],0,b['id'])
   self.assertLessEqual(abs(b['pos']['x'])+b['size']['x']/2,62.001,b['id']);self.assertLessEqual(abs(b['pos']['z'])+b['size']['z']/2,62.001,b['id'])
 def test_look_entry(self):
  looks=json.loads((R/'Assets/Conversion/Resources/MapLooks.json').read_text())['maps'];mine=[l for l in looks if l['id']=='expedition-city']
  self.assertEqual(len(mine),1);self.assertEqual(looks[-1]['id'],'expedition-city');self.assertEqual(sum(1 for l in looks if l['look']=='expedition-city'),1)
  l=mine[0];self.assertEqual((l['sky']['variant'],l['fog']['mode']),('dusk','linear'));self.assertLessEqual(len(l['lights']),8)
if __name__=='__main__':unittest.main()
