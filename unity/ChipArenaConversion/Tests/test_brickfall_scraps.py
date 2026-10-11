"""Brickfall's paper scraps must not slide through the arena's cover (ARENA_PROPOSAL.md §2.1 rule 1, §3.3, §3.10).

NativeBrickfallAtmosphere.Scrap rides each scrap along one of six fixed east-west street runs at .04-3 m, so a run that
crosses a kiosk, a stall, a dock or a wall shows paper skating through it. The runs are chosen from the §3.3 draft blockout
(docs/arenas/scripts/brickfall_m1.py, which generates that map), frozen with its elements and painted ramps into
Tests/look-references.json by tools/export_look_references.py; this checks every run's sweep (x0 - .25 .. x1 + .25 along,
the centre line ± (half width + .35) across) keeps 0.4 m from every solid in that blockout, the loading-dock ramps included.
The real map's props also fade a scrap out at runtime (NativeBrickfallCover); that part is a managed check
(Tests/BrickfallSceneryChecks.cs). When the blockout changes, re-export the fixture and re-pick the runs from its free cells.
Reads only files under unity/ChipArenaConversion (the native CI checks out only that folder), so every check always runs.
"""
import json, re, unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
ATMOSPHERE=ROOT/'Assets/ChipArena/Core/NativeBrickfallAtmosphere.cs'
REFERENCES=ROOT/'Tests/look-references.json'
ALONG,ACROSS,CLEAR=.25,.35,.4
def array(source,name):
    m=re.search(name+r'=\{([^}]*)\}',source);assert m,name
    return [float(v) for v in m.group(1).split(',')]
def blockout():
    # The draft's elements (mirrors included, as the script appends them) and its loading-dock ramps ('/' painted areas),
    # each ramp with its point-symmetric mirror.
    draft=json.loads(REFERENCES.read_text(encoding='utf-8'))['draft']
    solids=[(e[0],e[1],e[2],e[3],e[4]) for e in draft['elements']]
    for x0,x1,z0,z1,ch in draft['paint']:
        if ch=='/':solids+=[('ramp',x0,x1,z0,z1),('ramp~m',-x1,-x0,-z1,-z0)]
    return solids
class BrickfallScraps(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        src=ATMOSPHERE.read_text(encoding='utf-8')
        cls.runs=list(zip(array(src,'LaneX0'),array(src,'LaneX1'),array(src,'LaneZ'),array(src,'LaneHalf')))
        cls.solids=blockout()
    def test_six_point_symmetric_runs(self):
        self.assertEqual(len(self.runs),6)
        for k in range(0,6,2):
            (a0,a1,az,ah),(b0,b1,bz,bh)=self.runs[k],self.runs[k+1]
            with self.subTest(pair=k):self.assertEqual((a0,a1,az,ah),(-b1,-b0,-bz,bh),'runs come in point-symmetric pairs like the map')
    def test_runs_stay_in_the_play_space(self):
        for x0,x1,z,h in self.runs:
            with self.subTest(run=(x0,x1,z,h)):
                self.assertGreaterEqual(x0-ALONG,-30);self.assertLessEqual(x1+ALONG,30);self.assertLessEqual(abs(z)+h+ACROSS,26);self.assertGreater(x1-x0,12)
    def test_runs_clear_the_draft_cover(self):
        self.assertGreater(len(self.solids),100,'the frozen blockout still lists its solids')
        self.assertEqual(sum(1 for s in self.solids if s[0].startswith('ramp')),4,'both loading-dock ramps and their mirrors')
        for x0,x1,z,h in self.runs:
            sx0,sx1,sz0,sz1=x0-ALONG-CLEAR,x1+ALONG+CLEAR,z-h-ACROSS-CLEAR,z+h+ACROSS+CLEAR
            hits=[name for name,a,b,c,d in self.solids if a<sx1 and b>sx0 and c<sz1 and d>sz0]
            with self.subTest(run=(x0,x1,z,h)):self.assertEqual(hits,[],'a scrap run crosses §3.3 cover')
if __name__=='__main__':
    unittest.main()
