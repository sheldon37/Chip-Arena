"""Trusted local cloth code is optional, bounded, integrity checked and excluded from source packages."""
import json
import sys
from pathlib import Path
import unittest
import zipfile
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
import test_visual_body_assets as visual
from test_visual_body_assets import meta
import local_cloth
import install_local_assets as installer
import pack_visual_body as packer

class ClothTests(unittest.TestCase):
    setUp = visual.VisualBodyTests.setUp
    tearDown = visual.VisualBodyTests.tearDown
    install = visual.VisualBodyTests.install
    quiet = visual.VisualBodyTests.quiet
    def cloth(self):
        for folder in ('MagicaCloth2','MagicaCloth2/Scripts','ChipArenaLocalTools','ChipArenaLocalTools/Runtime'):
            self.bake.write(self.bake.local/(folder+'.meta'),meta(self.bake.next(),True))
        self.bake.asset('MagicaCloth2/Scripts/Cloth.cs','// owner licensed plugin placeholder')
        self.bake.asset('MagicaCloth2/Scripts/MagicaClothV2.asmdef','{"name":"MagicaClothV2"}')
        self.bake.asset(local_cloth.BRIDGE,'// regenerated bridge placeholder')
        self.bake.asset('MagicaCloth2/Example (Can be deleted)/WindDemo.cs','// must never ship')

    def test_cloth_opt_in_roundtrip_and_hash_rejection(self):
        self.cloth();out=self.tmp/'cloth.zip'
        packer.pack(self.base,out,self.dev,with_cloth=True)
        with zipfile.ZipFile(out) as z:
            self.assertIn(local_cloth.MANIFEST,z.namelist());self.assertIn(local_cloth.BRIDGE,z.namelist())
            self.assertFalse(any('WindDemo' in n for n in z.namelist()))
        first=out.read_bytes();packer.pack(self.base,out,self.dev,with_cloth=True);self.assertEqual(first,out.read_bytes())
        code,text=self.install(out);self.assertEqual(code,0,text)
        self.assertEqual((self.project/'Assets/LocalAssets'/local_cloth.BRIDGE).read_text(),'// regenerated bridge placeholder')
        extracted=self.tmp/'extract';installer.safe_extract(out,extracted)
        (extracted/local_cloth.BRIDGE).write_text('tampered')
        with self.assertRaisesRegex(installer.Problem,'hash mismatch'):local_cloth.verify(extracted)

    def test_absent_and_partial_cloth(self):
        self.assertEqual(local_cloth.verify(self.bake.local),{})
        with self.assertRaisesRegex(installer.Problem,'requires local'):packer.pack(self.base,self.tmp/'missing.zip',self.dev,with_cloth=True)

    def test_require_cloth_rejects_fallback_before_install(self):
        code,text=self.install(self.base,'--require-cloth')
        self.assertEqual(code,1,text);self.assertIn('cloth runtime required',text)
        self.assertFalse((self.project/'Assets/LocalAssets/Resources/EffectsAudio').exists())
        self.cloth();out=self.tmp/'cloth.zip';packer.pack(self.base,out,self.dev,with_cloth=True)
        code,text=self.install(out,'--check','--require-cloth')
        self.assertEqual(code,0,text)
        self.assertFalse((self.project/'Assets/LocalAssets/Resources/EffectsAudio').exists())
        code,text=self.install(out,'--require-cloth');self.assertEqual(code,0,text)
        self.assertTrue((self.project/'Assets/LocalAssets'/local_cloth.BRIDGE).is_file())

    def test_narrow_code_paths(self):
        for path in ('../evil.cs','/evil.cs','MagicaCloth2/../evil.cs','MagicaCloth2//Cloth.cs','MagicaCloth2/Example (Can be deleted)/Demo.cs','ChipArenaLocalTools/Editor/Tool.cs','ChipArenaLocalTools/Runtime/Other.cs','MagicaCloth2/evil.dll','MagicaCloth2/evil.exe'):
            self.assertFalse(local_cloth.allowed(path),path)
        self.assertTrue(local_cloth.allowed(local_cloth.BRIDGE))

    def test_cloth_manifest_missing_meta_and_unsafe_paths(self):
        self.cloth();files=local_cloth.collect(self.bake.local)
        listing=self.bake.local/local_cloth.MANIFEST
        for key in ('MagicaCloth2/Scripts.meta',local_cloth.BRIDGE+'.meta'):
            broken=dict(files);del broken[key];listing.write_text(json.dumps({'schema':1,'files':broken}))
            with self.assertRaisesRegex(installer.Problem,'meta'):local_cloth.verify(self.bake.local)
        files['../outside.cs']='a'*64;listing.write_text(json.dumps({'schema':1,'files':files}))
        with self.assertRaisesRegex(installer.Problem,'unsafe'):local_cloth.verify(self.bake.local)

if __name__=='__main__':unittest.main()
