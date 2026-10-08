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
        models=resource('SourceModels')
        cls.models={m['id']:m for m in models['models']}
        cls.textures=models['textures']
    def test_procedural_textures_contain_one_complete_rgba_base_level(self):
        # The Windows failure came from treating these base-only exports as a full mip chain.
        self.assertEqual(len(self.textures),3)
        for i,texture in enumerate(self.textures):
            with self.subTest(texture=i):
                self.assertGreater(texture['width'],0)
                self.assertGreater(texture['height'],0)
                self.assertEqual(texture['channels'],4)
                pixels=base64.b64decode(texture['pixels'],validate=True)
                self.assertEqual(len(pixels),texture['width']*texture['height']*4)
                self.assertEqual(len(texture['repeat']),2)
                self.assertTrue(all(math.isfinite(v) and v>0 for v in texture['repeat']))
        for model in self.models.values():
            for section in model['sections']:
                self.assertTrue(-1<=section['texture']<len(self.textures),model['id'])
    def test_complete_geometry_and_tracks(self):
        self.assertEqual(len(self.clips),31)
        self.assertEqual(len(self.models),133)
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
    def test_wounds_bind_to_exported_pilot_parts(self):
        data=json.loads((ROOT/'Assets/Conversion/Resources/WoundParts.json').read_text())
        self.assertEqual(len(data['parts']),31)
        names={s['part'] for s in self.models['pilot']['sections']}
        for p in data['parts']:
            self.assertIn(p['name'],names)
            self.assertTrue(0<=p['bone']<22)
            self.assertEqual(len(p['matrix']),16)
            self.assertGreater(len(p['solids']),0)
            for solid in p['solids']:
                for axis in 'xyz':self.assertLess(solid['min'][axis],solid['max'][axis])
    def test_scenery_motion_and_machine_geometry(self):
        data=json.loads((ROOT/'Assets/Conversion/Resources/SceneryMotion.json').read_text())
        self.assertEqual(len(data['maps']),14)
        for m in data['maps']:
            self.assertIn('map-'+m['id'],self.models)
            self.assertIn(len(m['snow']),(0,480))
            self.assertEqual(len(m['smoke'])%4,0)
            self.assertTrue(all(math.isfinite(v) for v in m['smoke']))
            for seed in m['snow']:self.assertGreater(seed['speed'],0)
        for name in ('scenery-snow','scenery-water','summon-toy','summon-zombie','pve-pickup','machine-moving-chip','machine-moving-med','machine-moving-recruit'):
            self.assertTrue(self.models[name]['sections'])
    def test_slender_hierarchy_and_source_image(self):
        import hashlib
        data=json.loads((ROOT/'Assets/Conversion/Resources/SlenderRig.json').read_text())
        nodes=data['nodes'];self.assertEqual(len(nodes),49)
        for i,n in enumerate(nodes):
            self.assertTrue(-1<=n['parent']<i)
            self.assertEqual(len(n['rotation']),4)
            self.assertAlmostEqual(sum(v*v for v in n['rotation']),1,places=5)
        for s in self.models['slender']['sections']:self.assertTrue(0<=s['bone']<len(nodes))
        b=(ROOT/'Assets/Conversion/Resources/SlenderScare.jpg').read_bytes()
        self.assertEqual(hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest(),'39813e99bae17cec879ecff572a0a06980361c44')
if __name__=='__main__':unittest.main()
