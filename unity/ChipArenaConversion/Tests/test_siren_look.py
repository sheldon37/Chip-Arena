"""The Siren model bank keeps its animated pieces apart (tools/export_models.mjs) for Core/NativeSirenLook.cs to pose."""
import base64, gzip, json, unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def resource(name):
    envelope=json.loads((ROOT/f'Assets/Conversion/Resources/{name}.json').read_text())
    assert envelope['schema']==1 and envelope['commit']=='05950eaef80ff2483a9c44d898cad98be1a76bbc'
    return json.loads(gzip.decompress(base64.b64decode(envelope['data'])))
class SirenLookChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.siren=next(m for m in resource('SourceModels')['models'] if m['id']=='siren')
    def test_pieces_are_named_sections_in_their_own_frames(self):
        named={s['part']:s for s in self.siren['sections'] if s['part']}
        # The source's pieces, then the Unity finish's unit boxes (tools/siren_design.mjs; Core/NativeSirenDetail.cs places them).
        couture={'siren-couture-plate','siren-couture-deep','siren-couture-sheen','siren-couture-ash','siren-couture-seam'}
        self.assertEqual(set(named),{'siren-eye','siren-teeth-upper','siren-teeth-lower','siren-claws-l','siren-claws-r','siren-flame-box'}|couture)
        for name,s in named.items():
            with self.subTest(piece=name):
                self.assertEqual(s['bone'],-1)
                self.assertEqual(len(s['positions'])%3,0)
        # One unit box serves every tongue step and wisp; the eye is a unit oval (its brow cuts are rebuilt natively).
        box=named['siren-flame-box']['positions'];self.assertEqual(len(box),24*3);self.assertAlmostEqual(max(map(abs,box)),.5,6)
        eye=named['siren-eye']['positions'];self.assertEqual(len(eye),120*3);self.assertAlmostEqual(max(abs(v) for v in eye[0::3]),1,5)
        for name in couture:
            with self.subTest(part=name):
                p=named[name]['positions'];self.assertEqual(len(p),24*3);self.assertAlmostEqual(max(map(abs,p)),.5,6)
                self.assertTrue(all(w==0 for w in named[name]['emission'][3::4]),'couture parts take no body or accent tint')
        # The finer grin (13 upper and 12 lower fangs, 5 triangles each) replaces the source's 11 and 10 single triangles.
        self.assertEqual(len(named['siren-teeth-upper']['indices']),13*5*3);self.assertEqual(len(named['siren-teeth-lower']['indices']),12*5*3)
    def test_static_batches_hold_no_piece(self):
        static=[s for s in self.siren['sections'] if not s['part']]
        # Joint batches plus the shadow pool; the world-space flame pool (420 collapsed cubes) is no longer exported.
        self.assertTrue(all(0<=s['bone']<22 or (s['transparent'] and s['texture']==0) for s in static))
        self.assertFalse(any(len(s['positions'])//3==420*24 for s in static))
        head=[s for s in static if s['bone']==5 and s['ink']]
        self.assertEqual(len(head),1);self.assertLess(max(head[0]['positions'][1::3]),.4,'flame tongues left in the head batch')
    def test_face_tracks_cover_every_siren_clip(self):
        tracks={c['key']:c for c in resource('SirenLook')['clips']}
        motion=[c for c in json.loads((ROOT/'tools/motion-export.json').read_text())['clips'] if c['key'].startswith('siren-')]
        self.assertEqual(sorted(tracks),sorted(c['key'] for c in motion))
        for c in motion:
            with self.subTest(clip=c['key']):
                t=tracks[c['key']];self.assertEqual((t['frames'],t['loop']),(c['frames'],c['loop']));self.assertEqual(len(t['values']),t['frames']*2)
                self.assertTrue(all(-1e-6<=v<=1+1e-6 for v in t['values']))
if __name__=='__main__':
    unittest.main()
