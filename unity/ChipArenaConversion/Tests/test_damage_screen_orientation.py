"""The damage screen must not turn the frame upside down (one candidate for the owner's Windows D3D11 report "camera seems
to flip when taking damage"; a knockdown or stagger switching the view mode in ConversionGame.UpdateCamera is the other).

The first version copied BuiltinRenderTextureType.CameraTarget into a temporary from a command buffer and blitted it back;
on D3D that copy of the back buffer or an anti-aliased target lands top-down without a negative texel height, so the whole
frame was drawn upside down while the effect was up. It now draws as an OnRenderImage image effect through Graphics.Blit,
which orients its single source itself. These static checks keep that path and the shader's orientation contract; they
cannot run D3D11 (no rendered acceptance is claimed here).
"""
import re, unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
RUNTIME=ROOT/'Assets/ChipArena/Runtime'
def code(path):
    """C# source without // comments (the headers may name the old path)."""
    return '\n'.join(line.split('//',1)[0] for line in path.read_text().splitlines())
def ternary(expr,names):
    """`flip>0.0?A:B` (A and B plain arithmetic over `names`) as a Python function of (flip, *names)."""
    m=re.fullmatch(r'flip>0\.0\?([^?:]+):([^?:]+)',expr.strip());assert m,expr
    def py(e):
        for src,dst in names:e=e.replace(src,dst)
        assert re.fullmatch(r'[0-9a-z.+\- ]+',e),e
        return e
    args=','.join(['flip']+[dst for _,dst in names])
    return eval(f'lambda {args}:({py(m.group(1))}) if flip>0.0 else ({py(m.group(2))})',{'__builtins__':{}})
class DamageScreenOrientation(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.pass_cs=code(RUNTIME/'NativeDamageScreenPresentation.cs')
        cls.image_cs=code(RUNTIME/'NativeDamageScreenImage.cs')
        cls.shader=(ROOT/'Assets/ChipArena/Resources/NativeDamageScreen.shader').read_text()
        cls.frag=cls.shader[cls.shader.index('float4 frag(v2f_img i):SV_Target'):cls.shader.index('ENDCG')]
        cls.shared=cls.shader[cls.shader.index('// BEGIN SHARED'):cls.shader.index('// END SHARED')]
    def test_no_camera_target_round_trip(self):
        for token in ('BuiltinRenderTextureType','CameraTarget','CommandBuffer','AddCommandBuffer','CameraEvent','GetTemporaryRT','.Blit('):
            with self.subTest(token=token):self.assertFalse(token in self.pass_cs,'the pass must not copy the camera target and blit it back')
    def test_draws_through_on_render_image_and_graphics_blit(self):
        c=self.image_cs
        self.assertRegex(c,r'public sealed class NativeDamageScreenImage:MonoBehaviour')
        m=re.search(r'void OnRenderImage\(RenderTexture (\w+),RenderTexture (\w+)\)\{(.*?)\}\n',c,re.S);self.assertIsNotNone(m,'OnRenderImage(source, destination)')
        src,dst,body=m.groups()
        self.assertIn(f'Graphics.Blit({src},{dst},Material)',body);self.assertIn(f'Graphics.Blit({src},{dst})',body,'pass-through without a material')
        self.assertNotIn('CommandBuffer',c);self.assertNotIn('BuiltinRenderTextureType',c)
        meta=(RUNTIME/'NativeDamageScreenImage.cs.meta').read_text();guid=re.search(r'^guid: ([0-9a-f]{32})$',meta,re.M)
        self.assertIsNotNone(guid,'a .meta with a 32-hex guid')
        owners=[p for p in (ROOT/'Assets').rglob('*.meta') if f'guid: {guid.group(1)}' in p.read_text(errors='ignore')]
        self.assertEqual(len(owners),1,f'guid reused: {owners}')
    def test_pass_enables_the_effect_only_while_active(self):
        c=self.pass_cs
        self.assertIn('public static void Frame(NativeDamageScreen s,bool active,',c);self.assertIn('public static void Release(bool all)',c)
        self.assertRegex(c,r'Frame\(NativeDamageScreen s,bool active,[^)]*\)\s*\{\s*if\(!active\|\|s==null\)\{Detach\(\);return;\}')
        self.assertIn('AddComponent<NativeDamageScreenImage>()',c);self.assertIn('image.enabled=true',c)
        detach=re.search(r'void Detach\(\)\{(.*?)\}\n',c,re.S).group(1);self.assertIn('image.enabled=false',detach)
        release=re.search(r'void Release\(bool all\)\s*\{(.*?)\n        \}',c,re.S).group(1);self.assertIn('Detach();',release);self.assertRegex(release,r'Drop\(added\[i\]\)')
        game=code(RUNTIME/'ConversionGame.cs');self.assertIn('NativeDamageScreenPass.Release(true)',game)
    def test_shader_keeps_the_texel_size_guard(self):
        f=self.frag
        self.assertIn('#pragma vertex vert_img',self.shader);self.assertIn('float flip=1.0;',f)
        guard=re.search(r'#if UNITY_UV_STARTS_AT_TOP\s*(.*?)\s*#endif',f,re.S);self.assertIsNotNone(guard)
        self.assertEqual(guard.group(1).strip(),'if(_MainTex_TexelSize.y<0.0)flip=-1.0;')
        self.assertEqual(f.count('flip='),2,'flip is set only by the initialiser and the guard')
    def test_scene_is_sampled_at_the_blit_uv(self):
        overlay=ternary(re.search(r'float2 uv=float2\(i\.uv\.x,([^;]+)\);',self.frag).group(1),[('i.uv.y','iy')])
        self.assertRegex(self.frag,r'DamageScreen\(uv,flip,');self.assertIn('float3 SceneTap(float2 uv){return saturate(tex2Dlod(_MainTex,float4(uv,0.0,0.0)).rgb);}',self.shader)
        tap=ternary(re.search(r'float3 Scene\(float2 uv,float flip\)\{return SceneTap\(float2\(uv\.x,([^;]+)\)\);\}',self.shared).group(1),[('uv.y','uy')])
        for flip in (1.0,-1.0):
            for iy in (0.0,0.125,0.5,0.8,1.0):
                with self.subTest(flip=flip,iy=iy):
                    uy=overlay(flip,iy);self.assertAlmostEqual(tap(flip,uy),iy,9,'the scene tap must land on i.uv')
                    self.assertAlmostEqual(uy,iy if flip>0 else 1.0-iy,9,'the overlay is laid out with y up')
        calls=re.findall(r'\bScene\(([^;]*?)\)(?=[.;)])',self.shared.split('float3 Scene(',1)[1].split('\n',1)[1])
        self.assertTrue(calls);self.assertTrue(all(c.endswith(',flip') for c in calls),calls)
if __name__=='__main__':
    unittest.main()
