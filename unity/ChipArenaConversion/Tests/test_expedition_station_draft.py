"""The Central Station design draft (docs/EXPEDITION-CENTRAL-STATION.md): Tests/expedition-central-station-draft.json is the
output of docs/arenas/scripts/expedition_central_station.py (byte for byte), its own checks pass (the three floor levels
reached from the entries, records and spawn exits on reached floor with their clearance, Hulk routes, no overlapping
colliders, the hub door record on Station Road), and it is NOT LIVE: running the generator leaves every live resource
byte-identical, and the live manifest, maps and looks hold no station. ExpeditionStationDraftChecks.cs runs Astra's parser
and geometry gate on it. Static analysis only: no Unity/PhysX claims. The script test skips without docs/."""
import hashlib,json,subprocess,sys,tempfile,unittest
from pathlib import Path
R=Path(__file__).resolve().parents[1];SCRIPT=R.parents[1]/'docs/arenas/scripts/expedition_central_station.py'
DRAFT=R/'Tests/expedition-central-station-draft.json';RES=R/'Assets/Conversion/Resources'
LIVE=('UnityMaps.json','ExpeditionLayout.json','MapLooks.json','PveContent.json','HazardKit.json','GameContent.json')
REGEN='regenerate: python3 docs/arenas/scripts/expedition_central_station.py --fixture'
def digest():return {n:hashlib.sha256((RES/n).read_bytes()).hexdigest() for n in LIVE}

class StationDraftFixture(unittest.TestCase):
 def test_draft_is_not_live(self):
  d=json.loads(DRAFT.read_text());self.assertTrue(d['draft']);self.assertEqual(d['revision'],'exp-transit-draft-1')
  self.assertEqual([m['id'] for m in d['maps']],['expedition-transit']);self.assertEqual(d['layout']['area']['id'],'exp-transit')
  live=json.loads((RES/'ExpeditionLayout.json').read_text());self.assertNotIn('exp-transit',live['instances'])
  self.assertNotIn('exp-transit',[a['id'] for a in live['areas']])
  self.assertIn('exp-transit',[r['building'] for a in live['areas'] if a['id']=='exp-city' for r in a['hub']['reserved']])
  self.assertNotIn('expedition-transit',[m['id'] for m in json.loads((RES/'UnityMaps.json').read_text())['maps']])
  self.assertNotIn('central-station',[m['look'] for m in json.loads((RES/'MapLooks.json').read_text())['maps']])
  for e in d['layout']['area']['spawns']:
   self.assertTrue(set(e['allow'])<={'shambler','runner','spitter','brute','hulk'},e['id'])
   self.assertTrue(set(e.get('station',[]))<={'linesman','conductor','undertow','pileup'},e['id'])

@unittest.skipUnless(SCRIPT.exists(),'the docs are not in a packaged source tree')
class StationDraftScript(unittest.TestCase):
 def test_fixture_is_the_scripts_output_and_live_files_untouched(self):
  before=digest()
  with tempfile.TemporaryDirectory() as t:
   out=Path(t)/'station.json';run=subprocess.run([sys.executable,str(SCRIPT),'--json',str(out)],capture_output=True,text=True)
   self.assertEqual(run.returncode,0,run.stdout[-4000:]+run.stderr[-3000:])
   self.assertEqual(out.read_bytes(),DRAFT.read_bytes(),REGEN)
  self.assertEqual(digest(),before,'the generator never writes a live resource')
  for line in ('station checks: pass','town door checks: pass','colliding blocks overlapping (more than 1 cm in every axis): 0',
               'Hulk-sized and pilot routes from entry 0 to every entry, boss point and the approach: 12 of 12',
               'allow lists within the entry rules (Hulk: breach or Hulk drop; drops: brute; doors and vents: shambler, runner, spitter, brute): True',
               'breach panels: 3, static, solid mass behind each: True','every block listed: True'):
   self.assertIn(line,run.stdout)
  self.assertEqual(run.stdout.count('reached from the entry slots (100.0%)'),3,'ground, upper and track bed all reached')
  self.assertRegex(run.stdout,r'spawn exits: (\d+) .* with their clear radius: \1\n')
  self.assertRegex(run.stdout,r'spawn exits routed to entry 0 for the pilot probe: (\d+) of \1')
  self.assertNotIn('BAD ',run.stdout);self.assertNotIn('UNREACHED',run.stdout)

if __name__=='__main__':unittest.main()
