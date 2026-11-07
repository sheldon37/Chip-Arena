"""The Factory draft (docs/arenas/scripts/factory_m1.py) and its fixture for the presentation checks (Tests/factory-draft.json,
read by FactoryChecks.cs) agree, and the draft's own checks pass (jittered spawn LOS, clearances). No Unity/PhysX claims."""
import json,subprocess,sys,tempfile,unittest
from pathlib import Path
R=Path(__file__).resolve().parents[1];SCRIPT=R.parents[1]/'docs/arenas/scripts/factory_m1.py';FIXTURE=R/'Tests/factory-draft.json'

@unittest.skipUnless(SCRIPT.exists(),'the docs are not in a packaged source tree')
class FactoryDraft(unittest.TestCase):
 def test_fixture_is_the_scripts_output(self):
  with tempfile.TemporaryDirectory() as d:
   out=Path(d)/'draft.json';run=subprocess.run([sys.executable,str(SCRIPT),'--json',str(out)],capture_output=True,text=True)
   self.assertEqual(run.returncode,0,run.stdout[-2000:]+run.stderr[-2000:])
   self.assertIn('FFA: 28 pairs, 28 fully blocked',run.stdout);self.assertIn('Teams A-B: 16 pairs, 16 fully blocked',run.stdout)
   self.assertEqual(json.loads(out.read_text()),json.loads(FIXTURE.read_text()),'regenerate: python3 docs/arenas/scripts/factory_m1.py --json unity/ChipArenaConversion/Tests/factory-draft.json')
 def test_shape(self):
  m=json.loads(FIXTURE.read_text())['maps'][0];ids=[b['id'] for b in m['blocks']]
  self.assertEqual(m['id'],'factory');self.assertEqual(len(ids),len(set(ids)));self.assertTrue(all(i.startswith('factory-') for i in ids))
  self.assertEqual(len(m['spawns']['ffa']),8);self.assertEqual([len(t['points']) for t in m['spawns']['teams']],[4,4])
  self.assertEqual(sorted(h['kind'] for h in m['hazards']),sorted(['saw-slot']*2+['saw-rail']*2+['crane']+['press']*2+['vent']*4))
  for b in m['blocks']:
   for k in 'xyz':self.assertGreater(b['size'][k],0,b['id'])
   self.assertLessEqual(abs(b['pos']['x'])+b['size']['x']/2,40.001,b['id']);self.assertLessEqual(abs(b['pos']['z'])+b['size']['z']/2,32.001,b['id'])
if __name__=='__main__':unittest.main()
