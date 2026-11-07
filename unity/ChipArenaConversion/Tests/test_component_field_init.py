"""Unity native objects must not be created while a MonoBehaviour is constructed.

Unity rejects native construction (MaterialPropertyBlock, Mesh, Material, textures, Shader.Find, Time/Screen reads, ...) in a
MonoBehaviour's constructor or instance field initializers. ArenaPresentation is one component split across many partial
files, so one such initializer in any part stops the whole component's construction and leaves later fields null (the
owner's Windows log, 2026-10-12: NativeCyberCityPresentation.metroOne, fixed by Astra in 82b837e). This text scan follows
field initializers through the managed helpers they construct. It is a static approximation, not a Unity run.
"""
import os,re,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]/'Assets/ChipArena'
NATIVE={'MaterialPropertyBlock','Mesh','Material','Texture2D','Texture3D','RenderTexture','ComputeBuffer','GraphicsBuffer',
        'GameObject','CommandBuffer','Cubemap','Sprite','AudioClip','PhysicMaterial','Shader'}
CALLS=re.compile(r'(?<![\w.])(?:Shader\.Find|Resources\.Load\w*|GameObject\.Find\w*|Camera\.main|Screen\.\w+|Time\.\w+|Application\.\w+|QualitySettings\.\w+|SystemInfo\.\w+)')

def types():
 out={}
 for path in ROOT.rglob('*.cs'):
  if 'Editor' in path.parts:continue
  s=path.read_text()
  for m in re.finditer(r'\b(?:class|struct)\s+(\w+)([^{;]*)\{',s):
   i=m.end();depth=1
   while depth and i<len(s):
    depth+={'{':1,'}':-1}.get(s[i],0);i+=1
   out.setdefault(m.group(1),[]).append((path,s[m.end():i-1],m.group(2)))
 return out
def top(body):
 out=[];depth=0
 for c in body:
  if c=='{':depth+=1
  elif c=='}':depth-=1
  elif depth==0:out.append(c)
 return ''.join(out)
def initializers(body):
 for stmt in top(body).split(';'):
  if '=>' in stmt or re.search(r'\b(const|static)\b',stmt) or '=' not in stmt:continue
  rhs=stmt.split('=',1)[1]
  for m in re.finditer(r'\bnew\s+([A-Z]\w*)\s*[({]',stmt):yield m.group(1),None,stmt.strip()
  for m in CALLS.finditer(rhs):yield None,m.group(0),stmt.strip()
class ComponentFieldInit(unittest.TestCase):
 def test_no_native_construction_in_component_initializers(self):
  T=types();memo={}
  def allocates(name,seen=()):
   if name in NATIVE:return name
   if name in memo:return memo[name]
   if name in seen or name not in T:return None
   memo[name]=None
   for _,body,_ in T[name]:
    for t,call,stmt in initializers(body):
     r=call or allocates(t,seen+(name,))
     if r:memo[name]=name+' -> '+r;return memo[name]
    for m in re.finditer(r'\b'+name+r'\s*\([^)]*\)\s*(?::\s*(?:base|this)\([^)]*\))?\s*\{',body):
     i=m.end();depth=1
     while depth and i<len(body):
      depth+={'{':1,'}':-1}.get(body[i],0);i+=1
     ctor=body[m.end():i]
     hit=CALLS.search(ctor)
     if hit:memo[name]=name+'() -> '+hit.group(0);return memo[name]
     for n in re.finditer(r'\bnew\s+([A-Z]\w*)\s*[({]',ctor):
      r=allocates(n.group(1),seen+(name,))
      if r:memo[name]=name+'() -> '+r;return memo[name]
   return None
  components=[n for n,defs in T.items() if any(re.search(r':\s*(MonoBehaviour|ScriptableObject)\b',h) for _,_,h in defs)]
  self.assertIn('ArenaPresentation',components)
  bad=[]
  for name in components:
   for path,body,_ in T[name]:
    for t,call,stmt in initializers(body):
     r=call or allocates(t)
     if r:bad.append(f'{path.relative_to(ROOT)}: {name}: {stmt[:120]} ({r})')
  self.assertEqual(bad,[],'create these in Awake/Start or lazily on first use:\n'+'\n'.join(bad))
 def test_scan_catches_the_known_case(self):
  body='private readonly MaterialPropertyBlock metroOne=new MaterialPropertyBlock();private Foo f=new Foo();'
  self.assertEqual([t for t,_,_ in initializers(body)],['MaterialPropertyBlock','Foo'])
  self.assertEqual([c for _,c,_ in initializers('private float t=Time.time;private int k=NativeDamageScreen.Slots;')],['Time.time'])

if __name__=='__main__':unittest.main()
