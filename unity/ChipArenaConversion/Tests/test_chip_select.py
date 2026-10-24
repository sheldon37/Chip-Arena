"""PvE chip selection wiring (Core/NativeChipSelect.cs, Runtime/NativeChipSelectPresentation.cs and NativeChipSelectHud.cs,
Resources/NativeChipSelect.shader; docs/PVE-CHIP-SELECT.md). The managed checks (Tests/ChipSelectChecks.cs) cover the motion,
the hologram and the tag in Core; this text scan covers what they cannot: the replicated open pick the look reads, the hooks
in the shared presentation files, the shader's kinds and the palette. A static approximation, not a Unity run."""
import re,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]/'Assets/ChipArena'
def read(p):return (ROOT/p).read_text()

class ChipSelect(unittest.TestCase):
 def test_replicated_open_pick(self):
  frame=read('Runtime/NativeNetworkFrame.cs')
  self.assertIn('public int open;',frame,'every PvE row frame carries its open pick id')
  self.assertIn('open=r.Open?.Id??0',frame,'the host writes each row\'s open pick')
  self.assertIn('if(pf.id==rf.open)r.Open=pick;',frame,'guests rebuild the open pick of every row')
  pres=read('Runtime/NativeChipSelectPresentation.cs')
  self.assertRegex(pres,r'NativeChipSelect\.Selecting\(true,row\.Open\?\.Id\?\?0,p\.Hp>0,p\.Seat\.connected,v\.Holding,!string\.IsNullOrEmpty\(v\.Cinematic\),v\.Stage=="ended"\)')
  self.assertNotIn('ChipPickOpen',pres,'no proposed field: the replicated open pick is used')

 def test_presentation_only(self):
  for p in ['Core/NativeChipSelect.cs','Runtime/NativeChipSelectPresentation.cs','Runtime/NativeChipSelectHud.cs']:
   s=read(p)
   for banned in ['OpenPvePick','ClosePvePick','ChoosePve','ChoosePveChip','PveOpen(','NetworkFrame(','ApplyFrame','SaveAccount','prefs.Save']:
    self.assertNotIn(banned,s,p+' must not touch the pick flow, the network or saves ('+banned+')')
  self.assertNotRegex(read('Runtime/NativeChipSelectPresentation.cs'),r'row\.Open\s*=','the look never writes the row')

 def test_hooks(self):
  arena=read('Runtime/ArenaPresentation.cs')
  self.assertIn('CyberVictimPose(rig,a,time); ChipSelectPose(rig,a,time);',arena,'the pose layer after the route and the other overlays')
  self.assertIn('RenderChipSelect(s);',arena,'the world holograms after the rigs are posed')
  view=read('Runtime/NativeViewPresentation.cs')
  sway,hook,draw=view.index('ApplyViewSway(shown,time,ads);'),view.index('ChipSelectView(viewRig,source,time);'),view.index('SourceCharacter(viewRig,shown,time,true)')
  self.assertTrue(sway<hook<draw,'the first-person wrist after the view rig is placed, before it is drawn')
  self.assertIn('ChipSelectTags(scale,height);',read('Runtime/NativeCombatHud.cs'),'the tag with the overhead bars')
  self.assertIn('"Preview Chip Selecting"',read('Runtime/NativeSandbox.cs'),'the Dev Sandbox preview')
  hud=read('Runtime/NativeChipSelectHud.cs')
  self.assertIn('FirstPersonView&&p==arena.ViewPilot',hud,'never my own tag in first person')
  self.assertIn('physics.Raycast(eye',hud,'occluded like the overhead bars')

 def test_shader_kinds_and_palette(self):
  core=read('Core/NativeChipSelect.cs');shader=read('Resources/NativeChipSelect.shader')
  kinds=dict(re.findall(r'Kind(Frame|Card|Beam|Mote|Ring|Line)=(\d+)',core))
  self.assertEqual(kinds,{'Frame':'0','Card':'1','Beam':'2','Mote':'3','Ring':'4','Line':'5'})
  for t in ['kind<.5','kind<1.5','kind<2.5','kind<3.5','kind<4.5']:self.assertIn(t,shader)
  self.assertIn('float4 _ChipHolo;',shader);self.assertIn('Shader.PropertyToID("_ChipHolo")',read('Runtime/NativeChipSelectPresentation.cs'))
  self.assertIn('Shader "ChipArena/NativeChipSelect"',shader);self.assertIn('Resources.Load<Shader>("NativeChipSelect")',read('Runtime/NativeChipSelectPresentation.cs'))
  for k in ['Blend One One','ZWrite Off','Cull Off','multi_compile_fog','UNITY_COLORSPACE_GAMMA']:self.assertIn(k,shader)
  theme=dict(re.findall(r'(\w+)Hex=0x([0-9a-f]{6})',read('Runtime/NativeCyberTheme.cs')))
  holo=dict(re.findall(r'(Cyan|CyanSoft|Danger|Ink)=0x([0-9a-f]{6})',core))
  for name in ['Cyan','CyanSoft','Danger','Ink']:self.assertEqual(holo[name],theme[name],name+' matches the cyber palette')
  self.assertTrue((ROOT/'Resources/NativeChipSelect.shader.meta').exists())

if __name__=='__main__':unittest.main()
