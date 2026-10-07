"""Validate exported scene topology and source-specific lifetimes, without claiming rendered acceptance."""
import base64, gzip, json, math, unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def resource(name):
    envelope=json.loads((ROOT/f'Assets/Conversion/Resources/{name}.json').read_text())
    assert envelope['schema']==1 and envelope['commit']=='05950eaef80ff2483a9c44d898cad98be1a76bbc'
    return json.loads(gzip.decompress(base64.b64decode(envelope['data'])))
class CinematicChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.clips={c['key']:c for c in resource('SceneMotion')['clips']}
        cls.models={m['id']:m for m in resource('SourceModels')['models']}
    def test_complete_geometry_and_tracks(self):
        self.assertEqual(len(self.clips),31)
        self.assertEqual(len(self.models),124)
        for key,c in self.clips.items():
            self.assertIn(key,self.models)
            self.assertEqual(len(c['values']),c['frames']*c['tracks']*20,key)
            self.assertTrue(all(math.isfinite(v) for v in c['values']),key)
            self.assertEqual(len(c['environment']),c['frames']*8,key)
            for l in c['lights']:
                self.assertEqual(len(l['values']),c['frames']*11,key)
                self.assertTrue(all(math.isfinite(v) for v in l['values']),key)
            for s in self.models[key]['sections']:
                self.assertTrue(0<=s['bone']<c['tracks'],key)
                self.assertTrue(any(abs(v)>.00001 for v in s['positions']),key+' collapsed mesh')
                self.assertTrue(all(math.isfinite(v) for v in s['positions']+s['normals']),key)
            self.assertIn(len(c['victim']),(0,22),key)
    def test_clown_drop_and_victim(self):
        c=self.clips['death-dev-clown']
        def opacity(track,t):return c['values'][(track*c['frames']+round(t/c['duration']*(c['frames']-1)))*20+16]
        self.assertGreaterEqual(c['dropped'],0)
        self.assertEqual(opacity(c['dropped'],.1),0)
        self.assertGreater(opacity(c['dropped'],.5),0)
        self.assertEqual(len(c['victim']),22)
        for spec in ('pistol','rifle','greatsword','fire','leet'):
            self.assertIn('loose-'+spec,self.models)
    def test_siren_blackout_and_real_stage(self):
        c=self.clips['stage-dev-siren']
        light=lambda f:sum(l['values'][f*11+6] for l in c['lights'])
        self.assertGreater(max(light(f) for f in range(c['frames'])),10)
        self.assertAlmostEqual(light(c['frames']-1),0,places=4)
        self.assertGreater(len(self.models['stage-dev-siren']['sections']),20)
    def test_demon_and_flames_are_animated(self):
        for key in ('death-dev-raging-demon','death-siren-crush','death-siren-dissipate','stage-dev-ansatsuken'):
            c=self.clips[key]
            self.assertGreater(c['tracks'],100,key)
            self.assertNotEqual(c['values'][:c['frames']*20],c['values'][-c['frames']*20:],key)
if __name__=='__main__':unittest.main()
