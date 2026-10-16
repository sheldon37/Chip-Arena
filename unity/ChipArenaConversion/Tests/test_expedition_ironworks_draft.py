"""The City Ironworks blockout draft (docs/arenas/scripts/expedition_ironworks.py; design docs/EXPEDITION-CITY-IRONWORKS.md) and its
fixtures (Tests/expedition-ironworks-draft.json, expedition-ironworks-layout-draft.json, expedition-ironworks-look-draft.json, read by
ExpeditionIronworksDraftChecks.cs) agree; the generator is byte-identical across runs and its own checks pass (both floors reached from
the entry slots, records with pilot or Hulk clearance, every spawn exit routed back with its capsule, no overlapping blocks, the
converter shutter sealing its opening without stranding a boss point or a spawn, the Rigger's hang points, the hazards' Wave 1 and
boss schedule, the town door's returns Hulk-clear in the live town). The importer (tools/generate_expedition.py) resolves the draft
manifest's maps by its draft table and builds the Ironworks navigation, yet imports only the manifest it names, which stays the live
one. NOT LIVE: running it leaves every file under Assets/Conversion/Resources byte-identical, the live manifest stays exp-layout-3
without the foundry, and nothing under Assets/ names the drafts. Static analysis only: no Unity/PhysX claims. The script tests skip
without docs/."""
import hashlib,importlib.util,json,subprocess,sys,tempfile,unittest
from pathlib import Path
R=Path(__file__).resolve().parents[1];D=R.parents[1]/'docs/arenas/scripts';SCRIPT=D/'expedition_ironworks.py'
F={k:R/'Tests'/n for k,n in (('map','expedition-ironworks-draft.json'),('layout','expedition-ironworks-layout-draft.json'),('look','expedition-ironworks-look-draft.json'))}
RES=R/'Assets/Conversion/Resources'
REGEN='regenerate: python3 docs/arenas/scripts/expedition_ironworks.py --fixtures'
def tree(p):return {str(f.relative_to(p)):hashlib.sha256(f.read_bytes()).hexdigest() for f in sorted(p.rglob('*')) if f.is_file()}

@unittest.skipUnless(SCRIPT.exists(),'the docs are not in a packaged source tree')
class ExpeditionIronworksScript(unittest.TestCase):
 def test_fixtures_are_the_scripts_output_and_resources_untouched(self):
  before=tree(RES)
  with tempfile.TemporaryDirectory() as d:
   outs=[]
   for k in range(2):
    p={n:Path(d)/f'{n}{k}.json' for n in F};run=subprocess.run([sys.executable,str(SCRIPT)]+[a for n in F for a in (f'--{n}',str(p[n]))],capture_output=True,text=True)
    self.assertEqual(run.returncode,0,run.stdout[-4000:]+run.stderr[-3000:]);outs.append({n:p[n].read_bytes() for n in F})
   self.assertEqual(outs[0],outs[1],'the generator is deterministic')
   for n in F:self.assertEqual(outs[0][n],F[n].read_bytes(),REGEN)
  self.assertEqual(tree(RES),before,'the generator writes nothing under Assets/Conversion/Resources')
  for line in ('instance checks: pass','town door checks: pass','draft manifest checks: pass','reached from the entry slots (100.0%)',
               'solid blocks overlapping another by more than 1 cm on every axis: 0','pilot exclusion columns under a deck: 0',
               'allow lists within the entry rules (Hulk: breach or Hulk drop; drops: brute; doors and vents: shambler, runner, spitter, brute): True',
               'Hulk-sized and pilot routes from entry 0 to every entry, boss point and the approach: 12 of 12','records within 2 m (spawn exits 0.5 m) of a proposed hazard volume: 0',
               "Ward Zero's area unchanged True, every other town record unchanged True, top level unchanged True",'returns Hulk-clear: 10 of 10','spawn exits within 4 m of a boss point: none',
               'NOT LIVE: Resources/ExpeditionLayout.json stays exp-layout-3',
               'converter shutter exp-foundry:gate:converter (opens boss): 8 x 5 x 0.6 m in wall-w\'s west opening, piers either side and the lintel over it, inside the bounds, overlapping no block: True',
               'shutter shut: boss points reached by a pilot and a Hulk 3 of 3','no way through the opening: True','one runner live; the ladle pour off in the boss encounter: True',
               'the second chip machine upstairs on the walkway: True','one instance gate opening with its boss (exp-foundry:gate:converter)'):
   self.assertIn(line,run.stdout)
  self.assertRegex(run.stdout,r'routed to entry 0 with their own capsule \(Hulk where allowed\): (\d+) of \1\b')
  self.assertRegex(run.stdout,r'spawn exits routed with their capsule (\d+) of \1\b')
  self.assertRegex(run.stdout,r'Rigger hang points: (\d+) \((\d+) under crane girders.*: \1 of \1\b')
  self.assertRegex(run.stdout,r'direct \d+\.\d m .*, north loop \d+\.\d m .*, south loop \d+\.\d m')
  self.assertNotIn('BAD ',run.stdout);self.assertNotIn('UNREACHED',run.stdout)

@unittest.skipUnless(SCRIPT.exists(),'the importer needs docs/arenas/scripts')
class ExpeditionIronworksImporter(unittest.TestCase):
 def test_importer_resolves_the_draft_but_imports_the_live_manifest(self):
  spec=importlib.util.spec_from_file_location('exp_import_ironworks',R/'tools/generate_expedition.py');gx=importlib.util.module_from_spec(spec);spec.loader.exec_module(gx)
  m=json.loads(F['layout'].read_text())
  for a in m['areas']:
   name,prefix,scale=gx.MAP_DRAFTS[a['map']];d=json.loads((R/'Tests'/f'expedition-{name}-draft.json').read_text())['maps'][0]
   self.assertEqual(d['id'],a['map']);self.assertEqual(gx.block_hash(d),a['blockHash'],a['map'])
  self.assertEqual(gx.MAP_DRAFTS['expedition-ironworks'],('ironworks','ei',2))
  d=json.loads(F['map'].read_text())['maps'][0];nav=gx.navigation(d)
  self.assertGreater(len(nav['nodes']),20000);self.assertGreater(len(nav['links']),len(nav['nodes']))
  self.assertTrue(any(n['layer']==1 for n in nav['nodes']));self.assertEqual(sum(1 for l in nav['links'] if l['kind']=='drop'),len(d['navHints']['drops']))
  self.assertTrue(all(n['id'].startswith('ei-') for n in nav['nodes']))
  imported=json.loads((R/'Tests/expedition-layout-draft.json').read_text())
  self.assertTrue(all(a['map']!='expedition-ironworks' for a in imported['areas']));self.assertNotIn('exp-foundry',imported['instances'])

class ExpeditionIronworksFixtures(unittest.TestCase):
 def test_not_live(self):
  live=json.loads((RES/'ExpeditionLayout.json').read_text());self.assertEqual(live['revision'],'exp-layout-3');self.assertEqual(live['instances'],['exp-ward-zero'])
  self.assertTrue(all(a['map'] not in ('expedition-ironworks','expedition-foundry') for a in live['areas']))
  self.assertNotIn('"expedition-ironworks"',(RES/'UnityMaps.json').read_text());self.assertNotIn('"expedition-foundry"',(RES/'UnityMaps.json').read_text());self.assertNotIn('"ironworks"',(RES/'MapLooks.json').read_text())
  names=('ironworks-draft','ironworks-layout-draft','ironworks-look-draft','exp-layout-3-ironworks-draft')
  for f in (R/'Assets').rglob('*'):
   if f.is_file() and f.suffix in ('.cs','.json','.shader','.cginc','.asset','.unity','.prefab','.mat','.txt','.md') and '/LocalAssets/' not in str(f):
    t=f.read_text(errors='ignore');self.assertFalse(any(n in t for n in names),f'{f} names the Ironworks draft')
 def test_draft_manifest_is_live_plus_foundry(self):
  live=json.loads((RES/'ExpeditionLayout.json').read_text());m=json.loads(F['layout'].read_text())
  self.assertEqual(m['revision'],'exp-layout-3-ironworks-draft');self.assertEqual(m['instances'],live['instances']+['exp-foundry'])
  town=m['areas'][0];door=[d for d in town['doors'] if d['anchor']=='exp-city:door:exp-foundry']
  self.assertEqual(len(door),1);self.assertEqual(len(door[0]['returns']),8)
  town['anchors']=[a for a in town['anchors'] if a['id']!='exp-city:door:exp-foundry'];town['doors']=[d for d in town['doors'] if d['anchor']!='exp-city:door:exp-foundry']
  f=[a for a in m['areas'] if a['id']=='exp-foundry'];self.assertEqual(len(f),1);f=f[0]
  m['areas']=[a for a in m['areas'] if a['id']!='exp-foundry'];m['instances']=live['instances'];m['revision']=live['revision']
  self.assertEqual(m,live,'the draft is the live manifest plus the foundry only: '+REGEN)
  mp=json.loads(F['map'].read_text())['maps'][0];self.assertEqual(mp['id'],f['map']);self.assertFalse(mp['rotation'])
  self.assertTrue(all(set(s['allow'])<={'shambler','runner','spitter','brute','hulk'} for s in f['spawns']))
  self.assertEqual(mp['id'],'expedition-ironworks')
  self.assertEqual(f['ironworks']['kinds'],['cinderling','stoker','slagback','ladleman','rigger'])
  self.assertTrue(all(h['status']=='proposal' and h['id'].startswith('expedition-ironworks-') and h['schedule']['wave1'] in ('on','half','off') for h in f['ironworks']['hazards']))
  hz={h['id']:h for h in f['ironworks']['hazards']};self.assertEqual(hz['expedition-ironworks-ladle-crane']['schedule']['boss'],'off')
  self.assertEqual([g['id'] for g in f['gates']],['exp-foundry:gate:converter']);self.assertEqual(f['gates'][0]['opens'],'boss')
  self.assertEqual(f['ironworks']['arena']['shutter'],'exp-foundry:gate:converter')
  rig=[s for s in f['ironworks']['spawns'] if 'rigger' in s['propose']];self.assertGreaterEqual(len(rig),6)
  self.assertTrue(all('hang' in s and f['spawns'][s['spawn']]['kind']=='vent' and f['spawns'][s['spawn']]['surface']=='ceiling' for s in rig))
  self.assertEqual([x['level'] for x in f['ironworks']['machines'] if x['kind']=='chip'],[0,1])
  self.assertEqual(f['ironworks']['presentation']['seams'],[{'kind':'brute','table':'NativeHordeKinds.Seam','default':0xFF6A2C,'tint':0xFFD27A,'scope':'exp-foundry'}])
 def test_look_shape(self):
  s=json.loads(F['look'].read_text());self.assertEqual(s['schema'],1);l=s['maps'][0]
  self.assertEqual((l['id'],l['look'],l['sky']['variant']),('expedition-ironworks','ironworks','none'));self.assertLessEqual(len(l['lights']),8)

if __name__=='__main__':unittest.main()
