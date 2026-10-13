"""The Ward Zero revamp draft (docs/arenas/scripts/wardzero_m1.py) and its fixture for the presentation checks
(Tests/wardzero-draft.json, read by WardZeroChecks.cs) agree, the generator is byte-identical across runs, and its own
checks pass (both floors reached, every route on its own, every entry on walkable floor, jittered spawn LOS). Static
analysis only: no Unity/PhysX claims."""
import json,subprocess,sys,tempfile,unittest
from pathlib import Path
R=Path(__file__).resolve().parents[1];SCRIPT=R.parents[1]/'docs/arenas/scripts/wardzero_m1.py';FIXTURE=R/'Tests/wardzero-draft.json'

@unittest.skipUnless(SCRIPT.exists(),'the docs are not in a packaged source tree')
class WardZeroDraft(unittest.TestCase):
 def test_fixture_is_the_scripts_output(self):
  with tempfile.TemporaryDirectory() as d:
   outs=[]
   for k in range(2):
    out=Path(d)/f'draft{k}.json';run=subprocess.run([sys.executable,str(SCRIPT),'--json',str(out)],capture_output=True,text=True)
    self.assertEqual(run.returncode,0,run.stdout[-2000:]+run.stderr[-2000:]);outs.append(out.read_bytes())
   self.assertEqual(outs[0],outs[1],'the generator is deterministic')
   for line in ('reached from the spawns (100.0%)','FFA: 28 pairs, 28 fully blocked','Teams A-B: 16 pairs, 16 fully blocked','entry exits: 53 points on reached floor at their own level: 53','ids unique: True','ids clear of the current Ward Zero blocks: True'):
    self.assertIn(line,run.stdout)
   self.assertEqual(run.stdout.count('foot to top on its own: yes'),8)
   self.assertNotIn('foot to top on its own: no',run.stdout)
   self.assertEqual(json.loads(outs[0]),json.loads(FIXTURE.read_text()),'regenerate: python3 docs/arenas/scripts/wardzero_m1.py --json unity/ChipArenaConversion/Tests/wardzero-draft.json')
 def test_shape(self):
  d=json.loads(FIXTURE.read_text());m=d['maps'][0];ids=[b['id'] for b in m['blocks']]
  self.assertEqual((m['id'],m['name'],m['look']),('siren','Ward Zero','quarantine'));self.assertEqual(len(ids),len(set(ids)));self.assertTrue(all(i.startswith('siren-') for i in ids))
  self.assertEqual(len(m['spawns']['ffa']),8);self.assertEqual([len(t['points']) for t in m['spawns']['teams']],[4,4])
  self.assertEqual(sorted(h['kind'] for h in m['hazards']),sorted(['burner']*4+['gas']*2+['grid']*2+['shutter']*4))
  for b in m['blocks']:
   for k in 'xyz':self.assertGreater(b['size'][k],0,b['id'])
   self.assertLessEqual(abs(b['pos']['x'])+b['size']['x']/2,40.001,b['id']);self.assertLessEqual(abs(b['pos']['z'])+b['size']['z']/2,32.001,b['id'])
  e=d['pve']['entries'];self.assertEqual([x['e'] for x in e],list(range(len(e))));self.assertEqual(len({x['id'] for x in e}),len(e))
  self.assertEqual(sorted(x['song'] for x in e[:8]),[0,0,0,0,1,1,1,1]);self.assertTrue(all(x['pit'] and x['kind']=='vent' and x['surface']=='floor' for x in e[:8]))
  breach=[x for x in e if x['kind']=='breach'];self.assertEqual(len(breach),8)
  for x in breach:
   blk=next(b for b in m['blocks'] if b['id']==x['block']);self.assertEqual(blk['pos'],x['center']);self.assertEqual(blk['size'],x['size'])
   self.assertGreaterEqual(x['size']['y'],3.4);self.assertGreaterEqual(max(x['size']['x'],x['size']['z']),3.2);self.assertEqual(abs(x['normal']['x'])+abs(x['normal']['z']),1)
  hulk=[x for x in e if x['kind']=='drop' and x.get('hulk')];self.assertEqual(len(hulk),1);self.assertGreaterEqual(hulk[0]['clear'],4.5)
  self.assertEqual({x['level'] for x in e},{0,1});self.assertEqual(len(m['ramps']),8)
  for r in m['ramps']:
   self.assertEqual((r['low']['y'],r['high']['y']),(0,6.0));self.assertTrue(set(r['blocks'])<=set(m['navHints']['collisionOff']))
if __name__=='__main__':unittest.main()
