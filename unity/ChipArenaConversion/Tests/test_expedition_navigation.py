"""Draft navigation profiles do not enable unapproved instance resources."""
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('exp_import', ROOT/'tools/generate_expedition.py')
module = importlib.util.module_from_spec(spec)
HAS_SCRIPTS = (ROOT.parents[1]/'docs/arenas/scripts/expedition_common.py').exists()
if HAS_SCRIPTS: spec.loader.exec_module(module)

@unittest.skipUnless(HAS_SCRIPTS, 'authoring scripts absent from packaged source')
class InstanceNavigation(unittest.TestCase):
    def test_profiles(self):
        for name, prefix in [('expedition-transit', 'et'), ('expedition-ironworks', 'ei'), ('expedition-ward-zero', 'ew')]:
            m = dict(id=name, bounds=dict(minX=-2,maxX=2,minZ=-2,maxZ=2,ceiling=8,killY=-5),
                     blocks=[dict(id='floor',pos=dict(x=0,y=-.5,z=0),size=dict(x=8,y=1,z=8),rotation=dict(x=0,y=0,z=0,w=1))])
            nav=module.navigation(m)
            self.assertEqual(len(nav['nodes']),64)
            self.assertEqual(len(nav['links']),112)
            self.assertTrue(all(n['id'].startswith(prefix+'-') for n in nav['nodes']))
            self.assertTrue(all(n['id'].startswith(prefix+'-') for n in nav['links']))
            self.assertEqual(nav,module.navigation(m))
    def test_unknown_map_refused(self):
        with self.assertRaises(ValueError):
            module.navigation(dict(id='unknown',blocks=[],bounds={}))

if __name__ == '__main__': unittest.main()
