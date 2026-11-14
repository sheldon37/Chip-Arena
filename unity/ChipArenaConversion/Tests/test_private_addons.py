"""Real pack/extract/install route using an original stand-in, never licensed code."""
import json
import unittest
import zipfile
from test_local_cloth import ClothTests
from test_visual_body_assets import meta
import private_addons as addons
import pack_visual_body as packer
import install_local_assets as installer

class AddonTests(unittest.TestCase):
    setUp = ClothTests.setUp
    tearDown = ClothTests.tearDown
    install = ClothTests.install
    quiet = ClothTests.quiet

    def fixture(self):
        paths=[]
        bridge = """using UnityEngine;
using ChipArena.UnityConversion;
public static class StandInBridge {
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
 static void Register() {
  NativePrivateAddons.MapBuilt += c => Debug.Log("Stand-in built " + c.MapId);
  NativePrivateAddons.MapTearingDown += c => Debug.Log("Stand-in teardown " + c.MapId);
 }
}
"""
        for folder in ('PrivateAddons', 'PrivateAddons/StandIn'):
            rel=folder+'.meta';self.bake.write(self.bake.local/rel,meta(self.bake.next(),True));paths.append(rel)
        for name,content in [('Bridge.cs',bridge),('StandIn.asmdef','{"name":"StandIn"}'),('Vendor.cs','public static class StandInVendor { public const int Version=1; }'),('Light.shader','Shader "StandIn" { SubShader { Pass {} } }'),('Light.cginc','// include'),('Light.hlsl','// include'),('Light.compute','#pragma kernel CSMain\n[numthreads(1,1,1)] void CSMain(uint3 id : SV_DispatchThreadID) {}')]:
            rel=('PrivateAddons/' if name=='Bridge.cs' else 'PrivateAddons/StandIn/')+name;self.bake.asset(rel,content);paths.extend([rel,rel+'.meta'])
        self.bake.asset('PrivateAddons/StandIn/Unused.cs','// never selected')
        self.allow=self.tmp/'allow.json';self.allow.write_text(json.dumps({'schema':1,'files':paths}))
        return paths

    def test_roundtrip_determinism_and_unlisted_exclusion(self):
        paths=self.fixture();out=self.tmp/'addons.zip'
        packer.pack(self.base,out,self.dev,addons_allowlist=self.allow)
        first=out.read_bytes();packer.pack(self.base,out,self.dev,addons_allowlist=self.allow);self.assertEqual(first,out.read_bytes())
        with zipfile.ZipFile(out) as z:
            self.assertIn(addons.MANIFEST,z.namelist());self.assertNotIn('PrivateAddons/StandIn/Unused.cs',z.namelist())
        code,text=self.install(out);self.assertEqual(code,0,text)
        for rel in paths:self.assertEqual((self.bake.local/rel).read_bytes(),(self.project/'Assets/LocalAssets'/rel).read_bytes())

    def test_tamper_meta_budget_and_missing(self):
        paths=self.fixture();files=addons.collect(self.bake.local,self.allow)
        broken=dict(files);del broken['PrivateAddons/Bridge.cs.meta']
        with self.assertRaisesRegex(installer.Problem,'missing meta'):addons.verify_files(self.bake.local,broken)
        original=addons.BUDGET
        try:
            addons.BUDGET=1
            with self.assertRaisesRegex(installer.Problem,'budget'):addons.verify_files(self.bake.local,files)
        finally:addons.BUDGET=original
        (self.bake.local/'PrivateAddons/Bridge.cs').write_text('tampered')
        with self.assertRaisesRegex(installer.Problem,'hash mismatch'):addons.verify_files(self.bake.local,files)

    def test_bad_paths_and_absence(self):
        self.assertEqual(addons.verify(self.bake.local),{})
        for path in ('../x.cs','PrivateAddons/../x.cs','PrivateAddons//x.cs','PrivateAddons/Demo.cs','PrivateAddons/Samples/x.cs','PrivateAddons/x.dll','Other/x.cs','PrivateAddons/x.cs:stream','PrivateAddons/x*.cs'):
            self.assertFalse(addons.allowed(path),path)

    def test_repack_removes_old_listed_code(self):
        paths=self.fixture();first=self.tmp/'first.zip';second=self.tmp/'second.zip'
        packer.pack(self.base,first,self.dev,addons_allowlist=self.allow)
        removed='PrivateAddons/StandIn/Light.compute'
        self.allow.write_text(json.dumps({'schema':1,'files':[p for p in paths if p not in (removed,removed+'.meta')]}))
        packer.pack(first,second,self.dev,addons_allowlist=self.allow)
        with zipfile.ZipFile(second) as z:self.assertNotIn(removed,z.namelist())

if __name__=='__main__':unittest.main()
