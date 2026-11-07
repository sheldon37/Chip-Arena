#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The block effect (owner 2026-10-11 "add a new block effect"; docs/MELEE-ABILITIES.md "The new block effect"):
    // Core/NativeBlockFx.cs (rules, shapes, timing, the CPU mesh), Runtime/NativeBlockFxPresentation.cs and
    // Resources/NativeBlockFx.shader. Unity-only (the Electron client had no block effect), so no parity fixture: these pin the
    // rules. Checked: forms and colours per weapon, the guard window on host and guest clocks, the clash/deflect heuristic and
    // its Detail hook, the flash, crack, hold, ring and hitch curves (the hitch a 50 ms kick, gated by the settings scale),
    // deterministic sparks, the mesh for every form (twin daggers, the hex dome, the first-person arch, low tier), the clash
    // frame and deflect direction, the shader's GLSL-safe shared code and state, the runtime hooks (host, guests, killcam and
    // spectators through Render; the view camera on layer 10; no Time.timeScale), the removed cube bursts, the build gate and
    // the metas. Rendering itself (D3D11, the look) is not run here.
    internal static class BlockFxChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Block fx: "+what);count++;}
        private static string Code(string text)=>string.Join("\n",text.Split('\n').Select(l=>{int c=l.IndexOf("//",StringComparison.Ordinal);return c<0?l:l.Substring(0,c);}));
        private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
        public static void Run(string root,ConversionContent data)
        {
            Forms(data);Timing();Classify();Sparks();Mesh();
            string R(string p)=>File.ReadAllText(Path.Combine(root,p));
            Shader(R("Assets/ChipArena/Resources/NativeBlockFx.shader"));
            Runtime(Code(R("Assets/ChipArena/Runtime/NativeBlockFxPresentation.cs")),Code(R("Assets/ChipArena/Runtime/ArenaPresentation.cs")),Code(R("Assets/ChipArena/Runtime/ConversionGame.cs")),Code(R("Assets/ChipArena/Runtime/NativeBlastPresentation.cs")),Code(R("Assets/ChipArena/Core/NativeBlockFx.cs")));
            Check(R("Assets/ChipArena/Editor/NativeBuildChecks.cs").Contains("Resources.Load<Shader>(\"NativeBlockFx\")"),"the shader is build-checked");
            foreach(var file in new[]{"Runtime/NativeBlockFxPresentation.cs","Core/NativeBlockFx.cs","Resources/NativeBlockFx.shader"})
            {
                string meta=Path.Combine(root,"Assets/ChipArena",file+".meta");Check(File.Exists(meta),"meta: "+file);
                var guid=Regex.Match(File.ReadAllText(meta),@"^guid: ([0-9a-f]{32})$",RegexOptions.Multiline);Check(guid.Success,"meta guid: "+file);
                int owners=Directory.GetFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories).Count(m=>File.ReadAllText(m).Contains("guid: "+guid.Groups[1].Value));
                Check(owners==1,"unique guid: "+file);
            }
            Console.WriteLine("PASS Block fx ("+count+" checks: weapon forms and neon colours, guard window on host and guest clocks, clash/deflect heuristic with the guard/clash/deflect Detail hook, flash/crack/hold/ring/hitch curves, deterministic sparks, guard/twin/hex/first-person arch/low-tier meshes, clash frame and deflect streak, GLSL-safe shader, Render/view-camera/hitch hooks, no timeScale, old block bursts removed, build gate, metas)");
        }
        private static void Forms(ConversionContent data)
        {
            Check(NativeBlockFx.FormOf("sword",0,false)==NativeGuardForm.Arc&&NativeBlockFx.FormOf("greatsword",0,false)==NativeGuardForm.Tall&&NativeBlockFx.FormOf("daggers",0,false)==NativeGuardForm.Twin&&NativeBlockFx.FormOf("shield",0,false)==NativeGuardForm.Hex,"sword arc, greatsword tall arc, daggers twin arcs, shield hex dome");
            foreach(var spec in new[]{"pistol","rifle","fire","leet","emperor","ansatsuken","siren"})Check(NativeBlockFx.FormOf(spec,0,false)==NativeGuardForm.Ward,"every other class guards with the ward: "+spec);
            Check(NativeBlockFx.FormOf(null,1,false)==NativeGuardForm.Twin&&NativeBlockFx.FormOf(null,2,false)==NativeGuardForm.Tall&&NativeBlockFx.FormOf(null,3,false)==NativeGuardForm.Hex&&NativeBlockFx.FormOf(null,0,false)==NativeGuardForm.Arc&&NativeBlockFx.FormOf(null,2,true)==NativeGuardForm.Ward,"the prototype lab keys off its visual set; its guns ward");
            var arc=NativeBlockFx.Shape(NativeGuardForm.Arc);var tall=NativeBlockFx.Shape(NativeGuardForm.Tall);var twin=NativeBlockFx.Shape(NativeGuardForm.Twin);var hex=NativeBlockFx.Shape(NativeGuardForm.Hex);var ward=NativeBlockFx.Shape(NativeGuardForm.Ward);
            Check(arc.Height<arc.Radius&&arc.Height<=.4f,"the sword's arc is slim");
            Check(tall.Height>2*arc.Height&&tall.Span>arc.Span&&tall.Radius>arc.Radius,"the greatsword's arc is tall and wide");
            Check(twin.Radius<arc.Radius&&twin.Span<arc.Span&&twin.TwinYaw>0&&twin.Spread>0,"the daggers' arcs are small and turned outward");
            Check(hex.Radius>=tall.Radius&&hex.Height>1,"the shield's hex dome is the largest ward");
            foreach(var s in new[]{arc,tall,twin,hex,ward})Check(s.Columns>=2&&s.Rows>=1&&s.Span>0&&s.Span<Math.PI&&s.Radius>0&&s.Forward>0&&s.RingMax>0&&s.Taper>=1&&s.Anchor>=0&&s.Anchor<=1,"shape sane: "+s.Form);
            Check(NativeBlockFx.ViewScale(NativeGuardForm.Tall)<NativeBlockFx.ViewScale(NativeGuardForm.Arc)&&NativeBlockFx.ViewScale(NativeGuardForm.Hex)<NativeBlockFx.ViewScale(NativeGuardForm.Arc)&&NativeBlockFx.ViewScale(NativeGuardForm.Arc)<.5f,"the first-person guard is a small arch; the big forms shrink further");
            foreach(var spec in new[]{"sword","daggers","shield","greatsword","emperor","ansatsuken","siren"})Check(NativeBlockFx.DefaultColor(spec,0)==(int)data.Weapon(spec).color,"the fallback colour is the content weapon colour: "+spec);
            foreach(var w in data.weapons)
            {
                var c=NativeBlockFx.GuardColor((int)w.color);var src=NativeBlockFx.Linear((int)w.color);float m=Math.Max(c.X,Math.Max(c.Y,c.Z));
                Check(Math.Abs(m-1)<1e-5f&&c.X>=0&&c.Y>=0&&c.Z>=0,"the guard colour's brightest channel is 1: "+w.id);
                int Dom(Vector3 v)=>v.X>=v.Y&&v.X>=v.Z?0:v.Y>=v.Z?1:2;Check(Dom(c)==Dom(src),"the guard keeps the weapon's dominant hue: "+w.id);
                float Sat(Vector3 v){float hi=Math.Max(v.X,Math.Max(v.Y,v.Z)),lo=Math.Min(v.X,Math.Min(v.Y,v.Z));return hi>0?(hi-lo)/hi:0;}Check(Sat(c)>=Sat(src)-1e-5f,"the guard is at least as saturated as the pastel weapon colour: "+w.id);
            }
            Check(NativeBlockFx.GuardColor(0x808080)==Vector3.One,"a grey weapon guards in white");
        }
        private static void Timing()
        {
            // Host: the authoritative BlockAt; guests and the killcam: BlockUntil minus the window length.
            Check(NativeBlockFx.OpenAt(10,10.3f,.3f)==10,"the host opens at BlockAt");
            Check(Math.Abs(NativeBlockFx.OpenAt(-100,10.3f,.3f)-10)<1e-5f&&Math.Abs(NativeBlockFx.OpenAt(0,10.45f,.45f)-10)<1e-5f,"guests (no blockAt) open at BlockUntil - BlockDuration(stats)");
            Check(Math.Abs(NativeBlockFx.OpenAt(5,10.3f,.3f)-10)<1e-5f&&Math.Abs(NativeBlockFx.OpenAt(11,10.3f,.3f)-10)<1e-5f,"a stale or future BlockAt is not this window's");
            Check(NativeBlockFx.GuardAlpha(9.99f,10,10.3f)==0&&NativeBlockFx.GuardAlpha(10.3f,10,10.3f)==0&&NativeBlockFx.GuardAlpha(11,10,10.3f)==0&&NativeBlockFx.GuardAlpha(float.NaN,10,10.3f)==0,"no guard outside the window");
            Check(NativeBlockFx.GuardAlpha(10.15f,10,10.3f)==1&&NativeBlockFx.GuardAlpha(10.02f,10,10.3f)>0&&NativeBlockFx.GuardAlpha(10.27f,10,10.3f)<1,"full in the window, a quick fade in and a fade out before the end");
            float prev=-1;for(int i=0;i<=40;i++){float w=NativeBlockFx.Wipe(10+i*.0025f,10);Check(w>=prev&&w>=0&&w<=1,"the guard unfolds monotonically");prev=w;}
            Check(NativeBlockFx.Wipe(9.9f,10)==0&&NativeBlockFx.Wipe(10+NativeBlockFx.OpenSeconds,10)==1,"the unfold takes OpenSeconds");
            Check(NativeBlockFx.Flare(0)==1&&NativeBlockFx.Flare(-.01f)==0&&NativeBlockFx.Flare(NativeBlockFx.FlareSeconds)==0&&NativeBlockFx.Flare(.03f)<1&&NativeBlockFx.FlareSeconds<=.1f,"a short white flash");
            Check(NativeBlockFx.CrackFade(0)==1&&NativeBlockFx.CrackFade(NativeBlockFx.CrackSeconds)==0&&NativeBlockFx.CrackReach(1)==.25f&&NativeBlockFx.CrackReach(1-.05f/NativeBlockFx.CrackSeconds)==1,"the cracks run out in 50 ms and fade");
            Check(NativeBlockFx.Hold(0)==1&&NativeBlockFx.Hold(NativeBlockFx.HoldSeconds)==0&&NativeBlockFx.HoldSeconds>=NativeBlockFx.CrackSeconds,"a late clash keeps the guard visible while it cracks");
            Check(Math.Abs(NativeBlockFx.RingSeconds-.18f)<1e-6f&&NativeBlockFx.RingAlpha(1)==0&&NativeBlockFx.RingAlpha(-.1f)==0&&NativeBlockFx.RingAlpha(.1f)>0,"the ring lives .18 s");
            Check(Math.Abs(NativeBlockFx.RingRadius(1,.8f)-.8f)<1e-5f&&NativeBlockFx.RingRadius(0,.8f)<.2f&&NativeBlockFx.RingRadius(.5f,.8f)>NativeBlockFx.RingRadius(.25f,.8f)&&NativeBlockFx.RingWidth(1,.8f)<NativeBlockFx.RingWidth(0,.8f),"the ring expands to its size and thins");
            // The hitch: a kick of at most 50 ms, nothing with camera shake off, bounded and finite.
            Check(Math.Abs(NativeBlockFx.HitchSeconds-.05f)<1e-6f,"the hitch is 50 ms");
            Check(!NativeBlockFx.Hitch(.02f,0,0,out _)&&!NativeBlockFx.Hitch(NativeBlockFx.HitchSeconds,0,1,out _)&&!NativeBlockFx.Hitch(-.001f,0,1,out _)&&!NativeBlockFx.Hitch(float.NaN,0,1,out _),"no hitch with camera shake off, before the clash or after 50 ms");
            float peak=0;for(int i=0;i<50;i++){if(!NativeBlockFx.Hitch(i*.001f,1,1,out var k))continue;Check(float.IsFinite(k.Pitch)&&float.IsFinite(k.Roll)&&float.IsFinite(k.Back),"a finite kick");Check(Math.Abs(k.Pitch)<=NativeBlockFx.HitchPitch+1e-5f&&Math.Abs(k.Roll)<=NativeBlockFx.HitchRoll+1e-5f&&k.Back<=NativeBlockFx.HitchBack+1e-6f,"a bounded kick");peak=Math.Max(peak,Math.Abs(k.Pitch));}
            Check(peak>NativeBlockFx.HitchPitch*.9f&&NativeBlockFx.HitchPitch<=1,"the kick peaks (under a degree)");
            NativeBlockFx.Hitch(.01f,1,.4f,out var reduced);NativeBlockFx.Hitch(.01f,1,1,out var full);Check(Math.Abs(reduced.Pitch-full.Pitch*.4f)<1e-5f,"Reduced effects scales the kick");
        }
        private static void Classify()
        {
            Check(NativeBlockFx.Classify(null,true,true,2,2.5f)==NativeBlockHit.Clash&&NativeBlockFx.Classify(null,true,true,3.6f,2.5f)==NativeBlockHit.Clash,"a melee attacker within reach (+slack) clashes");
            Check(NativeBlockFx.Classify(null,true,true,9,2.5f)==NativeBlockHit.Deflect,"a melee attacker out of reach (a thrown dagger or shield) deflects");
            Check(NativeBlockFx.Classify(null,true,false,1,0)==NativeBlockHit.Deflect,"a ranged or magic attacker deflects, even point blank");
            Check(NativeBlockFx.Classify(null,false,false,0,0)==NativeBlockHit.Clash,"an attacker without a seat (a PvE add's swipe) clashes");
            Check(NativeBlockFx.Classify("clash",true,false,30,0)==NativeBlockHit.Clash&&NativeBlockFx.Classify("deflect",true,true,1,2.5f)==NativeBlockHit.Deflect,"the future Block Detail overrides the guess");
            Check(NativeBlockFx.Notice("guard")&&!NativeBlockFx.Notice("clash")&&!NativeBlockFx.Notice(null)&&!NativeBlockFx.FromDetail("guard",out _)&&!NativeBlockFx.FromDetail("ansatsuken",out _),"guard is the window-open notice, other Details are ignored");
            Check(Math.Abs(NativeBlockFx.Reach(2.5f,1.2f)-3)<1e-5f&&NativeBlockFx.Reach(2.5f,0)==2.5f&&NativeBlockFx.Reach(2.5f,float.PositiveInfinity)==2.5f,"reach is range x the reach stat");
        }
        private static void Sparks()
        {
            var f=new NativeClashFrame{Base=new Vector3(.3f,1.2f,.3f),Tip=new Vector3(-.5f,1.4f,.4f),Contact=new Vector3(0,1.3f,.4f),Toward=Vector3.UnitZ,Up=Vector3.UnitY,Scale=1,Ring=.8f};
            for(int i=0;i<NativeBlockFx.SparksFull;i++)
            {
                Check(NativeBlockFx.Spark(f,7,i,.05f,out var a,out var va,out float ua)&&NativeBlockFx.Spark(f,7,i,.05f,out var b,out var vb,out float ub)&&a==b&&va==vb&&ua==ub,"sparks are deterministic per seed");
                Check(Finite(a)&&Finite(va)&&ua>0&&ua<1,"a live spark is finite");
                Check(!NativeBlockFx.Spark(f,7,i,NativeBlockFx.SparkLifeMax,out _,out _,out _)&&!NativeBlockFx.Spark(f,7,i,-.01f,out _,out _,out _),"every spark burns out by SparkLifeMax");
                NativeBlockFx.Spark(f,7,i,0,out var start,out var v0,out _);Check((start-f.Base).Length()<=(f.Tip-f.Base).Length()+1e-4f&&Vector3.Dot(v0,Vector3.UnitY)>0,"sparks leave the blade upward");
                NativeBlockFx.Spark(f,7,i,.12f,out _,out var later,out _);Check(later.Y<v0.Y,"gravity pulls them down");
            }
            NativeBlockFx.Spark(f,7,0,.05f,out var s7,out _,out _);NativeBlockFx.Spark(f,8,0,.05f,out var s8,out _,out _);Check(s7!=s8,"another event throws other sparks");
            Check(NativeBlockFx.SparkCount(1,false)==NativeBlockFx.SparksFull&&NativeBlockFx.SparkCount(.3f,false)<NativeBlockFx.SparksFull&&NativeBlockFx.SparkCount(1,true)<NativeBlockFx.SparksFull&&NativeBlockFx.SparkCount(0,true)==NativeBlockFx.SparksMin,"Reduced effects and the low tier thin the spray, never below SparksMin");
            Check(Math.Abs(NativeBlockFx.ContactOnBlade(Vector3.Zero,Vector3.UnitX,new Vector3(5,0,0),Vector3.UnitZ)-.85f)<1e-5f&&Math.Abs(NativeBlockFx.ContactOnBlade(Vector3.Zero,Vector3.UnitX,new Vector3(.5f,0,0),Vector3.UnitZ)-.5f)<1e-5f,"the contact sits on the blade, off its ends");
        }
        private static NativeGuardPose Pose(string spec,bool view=false)
        {
            var p=new NativeGuardPose{Chest=new Vector3(0,1.15f,0),Forward=Vector3.UnitZ,Up=Vector3.UnitY,Scale=1,HasBlade=spec!="pistol"};
            if(spec=="daggers"){p.Base0=new Vector3(.26f,1.18f,.3f);p.Tip0=new Vector3(.04f,1.52f,.42f);p.Base1=new Vector3(-.26f,1.18f,.3f);p.Tip1=new Vector3(-.04f,1.52f,.42f);p.Twin=true;}
            else{p.Base0=new Vector3(.28f,1.22f,.32f);p.Tip0=new Vector3(-.55f,1.42f,.42f);}
            if(view){p.View=true;p.Scale=NativeBlockFx.FirstPersonScale;p.Chest=new Vector3(0,1.19f,.12f);}
            return p;
        }
        private static void Mesh()
        {
            var m=new NativeBlockMesh();var color=NativeBlockFx.GuardColor(0xb6dfd7);
            Check(Marshal.SizeOf<NativeBlockVertex>()==19*4,"the vertex is 19 floats (position, colour, three float4 texcoords)");
            // The window: drawn while open, nothing after it without a hit, held through a late clash.
            Check(m.GuardFor(Pose("sword"),NativeGuardForm.Arc,color,10.15f,10,10.3f,false,NativeBlockHit.Clash,0,Vector3.UnitZ,1,1,false)&&m.VertexCount>0,"an open window draws the guard");
            int arc=m.VertexCount;foreach(var v in m.Vertices.Take(arc))Check(float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z)&&v.Mode==NativeBlockFx.ModeArc&&Math.Abs(v.U)<=1&&Math.Abs(v.V)<=1&&v.Alpha==1,"guard vertices are finite arc vertices in -1..1");
            m.Clear();Check(!m.GuardFor(Pose("sword"),NativeGuardForm.Arc,color,10.5f,10,10.3f,false,NativeBlockHit.Clash,0,Vector3.UnitZ,1,1,false)&&m.VertexCount==0,"no guard after the window");
            Check(m.GuardFor(Pose("sword"),NativeGuardForm.Arc,color,10.35f,10,10.3f,true,NativeBlockHit.Clash,.05f,Vector3.UnitZ,1,1,false)&&m.Vertices[0].Flare>0&&m.Vertices[0].Crack>0,"a clash flashes and cracks the guard, held past the window");
            m.Clear();m.GuardFor(Pose("sword"),NativeGuardForm.Arc,color,10.1f,10,10.3f,true,NativeBlockHit.Deflect,.01f,Vector3.UnitZ,1,1,false);Check(m.Vertices[0].Crack==0&&m.Vertices[0].Flare>0&&m.Vertices[0].Flare<NativeBlockFx.Flare(.01f),"a deflect flashes the guard less and never cracks it");
            m.Clear();m.GuardFor(Pose("sword"),NativeGuardForm.Arc,color,10.15f,10,10.3f,false,NativeBlockHit.Clash,0,Vector3.UnitZ,1,1,true);Check(m.VertexCount<arc,"the low tier halves the guard's segments");
            m.Clear();m.GuardFor(Pose("daggers"),NativeGuardForm.Twin,color,10.15f,10,10.3f,false,NativeBlockHit.Clash,0,Vector3.UnitZ,1,1,false);var twin=NativeBlockFx.Shape(NativeGuardForm.Twin);
            Check(m.VertexCount==2*(twin.Columns+1)*(twin.Rows+1),"the daggers draw twin arcs");
            Check(NativeBlockFx.Frame(Pose("daggers"),twin,0,out var right)&&NativeBlockFx.Frame(Pose("daggers"),twin,1,out var left)&&right.Centre.X>0&&left.Centre.X<0&&right.Forward.X>0&&left.Forward.X<0,"one dagger arc per hand, each turned outward");
            Check(!NativeBlockFx.Frame(Pose("sword"),NativeBlockFx.Shape(NativeGuardForm.Arc),1,out _),"one arc for the sword");
            m.Clear();m.GuardFor(Pose("shield"),NativeGuardForm.Hex,color,10.15f,10,10.3f,false,NativeBlockHit.Clash,0,Vector3.UnitZ,1,1,false);Check(m.VertexCount>0&&m.Vertices[0].Mode==NativeBlockFx.ModeHex,"the shield draws the hex dome");
            // In front of the weapon: deeper than the blade middle along the facing.
            var shape=NativeBlockFx.Shape(NativeGuardForm.Arc);NativeBlockFx.Frame(Pose("sword"),shape,0,out var g);var p=Pose("sword");
            Check(g.Centre.Z>(p.Base0.Z+p.Tip0.Z)*.5f+.1f&&g.Centre.Z<1.2f&&Math.Abs(g.Centre.Y-1.25f)<.25f,"the guard stands in front of the weapon at chest height ("+g.Centre+")");
            Check(Math.Abs(g.Tilt)>0&&Math.Abs(g.Tilt)<=shape.Tilt,"the arc rolls with the blade, within its limit");
            Check(NativeBlockFx.Frame(Pose("pistol"),NativeBlockFx.Shape(NativeGuardForm.Ward),0,out var ward)&&ward.Centre.Z>.3f&&ward.Tilt==0,"a bladeless pilot wards in front of the chest");
            // The first-person arch leans up from the eye; its clash ring is smaller.
            NativeBlockFx.ViewAxes(Vector3.UnitZ,Vector3.UnitY,out var vf,out var vu);Check(vf.Y>0&&vf.Z>0&&Math.Abs(Vector3.Dot(vf,vu))<1e-5f&&vu.Z<0&&Vector3.Cross(vu,vf).X>.99f,"the view arch bends in the screen plane, leaning away");
            Check(NativeBlockFx.Frame(Pose("sword",true),shape,0,out var arch)&&Math.Abs(arch.Forward.Y-vf.Y)<1e-5f&&arch.Scale==NativeBlockFx.FirstPersonScale,"the first-person guard is the arch at view scale");
            var cf=NativeBlockFx.ClashFrame(Pose("sword"),shape,new Vector3(0,.865f,0),Vector3.UnitZ,Vector3.UnitY);var cv=NativeBlockFx.ClashFrame(Pose("sword",true),shape,new Vector3(0,1.19f,.12f),Vector3.UnitZ,Vector3.UnitY);
            Check(cf.Ring==shape.RingMax&&Math.Abs(cv.Ring-shape.RingMax*NativeBlockFx.ViewRing)<1e-5f,"the first-person ring is ViewRing of the third-person one");
            Check(cf.Contact.Z>Math.Min(cf.Base.Z,cf.Tip.Z)&&cf.Toward==Vector3.UnitZ&&cf.Base==p.Base0,"the clash sprays from the blade toward the attacker");
            var dc=NativeBlockFx.ClashFrame(Pose("daggers"),twin,new Vector3(0,.865f,0),Vector3.Normalize(new Vector3(-1,0,1)),Vector3.UnitY);Check(dc.Base==Pose("daggers").Base1,"a dagger clash sprays from the blade on the attacker's side");
            // The clash and the deflect: a ring only within RingSeconds, the streak along the reflected direction, all finite.
            var eye=new NativeBlockEye{Position=new Vector3(2,1.8f,4),Up=Vector3.UnitY};
            m.Clear();m.Clash(cf,color,.06f,3,1,false,true,eye,1);int rings=0;for(int i=0;i<m.VertexCount;i++){if(m.Vertices[i].Mode==NativeBlockFx.ModeRing)rings++;Check(float.IsFinite(m.Vertices[i].X)&&float.IsFinite(m.Vertices[i].Y)&&float.IsFinite(m.Vertices[i].Z),"clash vertices are finite");}
            Check(rings==4&&m.IndexCount%6==0,"one upright ring quad");
            var ring=m.Vertices.Where(v=>v.Mode==NativeBlockFx.ModeRing).ToArray();Check(Math.Abs(ring[0].Z-ring[2].Z)<1e-4f&&Math.Abs(ring[0].Y-ring[2].Y)>.2f,"the ring stands upright, facing the attacker (never a floor disc)");
            m.Clear();m.Clash(cf,color,NativeBlockFx.RingSeconds+.01f,3,1,false,true,eye,1);Check(m.Vertices.Take(m.VertexCount).All(v=>v.Mode!=NativeBlockFx.ModeRing)&&m.VertexCount>0,"the ring is gone after RingSeconds; sparks remain");
            m.Clear();m.Clash(cf,color,NativeBlockFx.RecordSeconds,3,1,false,true,eye,1);Check(m.VertexCount==0,"nothing is left when the record expires");
            m.Clear();m.Clash(cf,color,.06f,3,.3f,false,true,eye,1);int reduced=m.VertexCount;m.Clear();m.Clash(cf,color,.06f,3,1,false,true,eye,1);Check(reduced<m.VertexCount,"Reduced effects draws fewer sparks");
            var dir=Vector3.Normalize(new Vector3(.2f,.1f,1));m.Clear();m.Deflect(new Vector3(0,1.25f,.3f),dir,Vector3.UnitY,color,.08f,5,1,false,eye,1);
            var streak=m.Vertices.Take(4).ToArray();Check(streak.All(v=>v.Mode==NativeBlockFx.ModeStreak),"the deflect streak first");
            var tail=new Vector3((streak[0].X+streak[1].X)/2,(streak[0].Y+streak[1].Y)/2,(streak[0].Z+streak[1].Z)/2);var head=new Vector3((streak[2].X+streak[3].X)/2,(streak[2].Y+streak[3].Y)/2,(streak[2].Z+streak[3].Z)/2);
            Check(Vector3.Dot(Vector3.Normalize(head-tail),dir)>.99f&&(head-new Vector3(0,1.25f,.3f)).Length()>1,"the deflect streak runs along the reflected direction");
            Check(m.Vertices.Take(m.VertexCount).Any(v=>v.Mode==NativeBlockFx.ModeGlow),"with a ping");
            // Warm buffers never shrink or reallocate between frames.
            var verts=m.Vertices;m.Clear();m.Clash(cf,color,.03f,3,1,false,true,eye,1);m.Clear();m.Clash(cf,color,.03f,3,1,false,true,eye,1);Check(ReferenceEquals(verts,m.Vertices)||m.Vertices.Length>=verts.Length,"buffers only grow");
        }
        private static void Shader(string text)
        {
            string code=Code(text);
            Check(code.Contains("Shader \"ChipArena/NativeBlockFx\"")&&code.Contains("\"Queue\"=\"Transparent+6\"")&&code.Contains("Blend One OneMinusSrcAlpha")&&code.Contains("ZWrite Off")&&code.Contains("Cull Off")&&code.Contains("ZTest LEqual"),"premultiplied, no depth writes, both faces, after the effects billboards");
            Check(code.Contains("#pragma multi_compile_fog")&&code.Contains("#pragma target 3.5")&&Regex.Matches(code,@"CGPROGRAM").Count==1,"one SM3.5 pass with fog");
            var blocks=Regex.Matches(text,@"BLOCK FX SHARED BEGIN[^\n]*\n(.*?)// ---- BLOCK FX SHARED END",RegexOptions.Singleline);Check(blocks.Count==1,"one SHARED block");
            string shared=Code(blocks[0].Groups[1].Value);
            foreach(var f in new[]{"float BlockFxHash(","float2 BlockFxHex(","float BlockFxCracks(","float4 BlockFxGuard(","float4 BlockFxRing(","float4 BlockFxStreak(","float4 BlockFxGlow(","float4 BlockFxShade("})Check(shared.Contains(f),"shared "+f);
            foreach(var token in new[]{"tex2D","sampler","int ","uint","half","static","bool","mul(","float2x2","float3x3","float4x4","#","for(","while(","fwidth","ddx","ddy","atan2","fmod","out ","line="})Check(!shared.Contains(token),"shared code stays GLSL-compatible: no "+token.Trim());
            Check(!Regex.IsMatch(shared,@"(?<![\w.])\d+(?![\w.])"),"shared code uses float literals only");
            Check(!Regex.IsMatch(shared,@"\d[eE][+-]?\d"),"shared code has no exponent literals");
            Check(shared.Contains("float rr=(0.25+0.75*saturate((1.0-crack)*3.6))*0.9;")&&Math.Abs(NativeBlockFx.CrackSeconds/.05f-3.6f)<1e-4f,"the shader's crack reach is NativeBlockFx.CrackReach");
            Check(shared.Contains("if(mode<1.5)return BlockFxGuard(")&&shared.Contains("mode>0.5?1.0:0.0")&&shared.Contains("if(mode<2.5)return BlockFxRing(")&&shared.Contains("if(mode<3.5)return BlockFxStreak(")&&NativeBlockFx.ModeArc==0&&NativeBlockFx.ModeHex==1&&NativeBlockFx.ModeRing==2&&NativeBlockFx.ModeStreak==3&&NativeBlockFx.ModeGlow==4,"the shader's modes are NativeBlockFx's");
            Check(code.Contains("float2 aa=float2(max(fwidth(i.uv.x),fwidth(i.uv.y)),fwidth(i.extra.w));")&&code.Contains("UNITY_APPLY_FOG_COLOR(i.fogCoord,c,float4(0,0,0,0));"),"derivatives before any branch; additive light fogs toward black");
            Check(code.Contains("float3 vertex:POSITION;")&&code.Contains("float4 color:COLOR;")&&code.Contains("float4 uv:TEXCOORD0;")&&code.Contains("float4 data:TEXCOORD1;")&&code.Contains("float4 extra:TEXCOORD2;"),"the vertex layout matches NativeBlockVertex");
        }
        private static void Runtime(string fx,string presentation,string game,string blasts,string core)
        {
            Check(presentation.Contains("RenderBlockFx(s,renderTime);")&&fx.Contains("private void RenderBlockFx(Snapshot s,float time)"),"Render draws the effect (host, guests, spectators and the killcam replay all render through it)");
            Check(fx.Contains("NativeBlockFx.OpenAt(a.BlockAt,a.BlockUntil,BlockWindow(a))")&&fx.Contains("NativeMeleeRules.BlockDuration(stats)"),"the guard follows the replicated block window and its stats-derived length");
            Check(fx.Contains("blockDraw.Submit(blockWorld,worldCamera,0,false)")&&fx.Contains("blockDraw.Submit(blockView,ViewCamera,10,true)")&&fx.Contains("bool viewOk=FirstPerson&&ViewCamera!=null&&viewRig!=null&&!ScopeActive;"),"the match camera on layer 0, the first-person view camera on layer 10");
            Check(fx.Contains("bool view=FirstPerson&&a.Id==LocalId;"),"the local first-person pilot's guard is the view arch, never the hidden body's");
            Check(fx.Contains("Mathf.Clamp01(WeaponEffectsQuality)")&&fx.Contains("bool low=LowGraphics;")&&fx.Contains("blockDraw.Low=low;"),"Reduced effects and the low graphics tier");
            Check(fx.Contains("NativeBlockFx.Classify(e.Detail,")&&fx.Contains("NativeBlockFx.Notice(e.Detail)"),"the Block Detail hook");
            Check(fx.Contains("SourceWeaponBone(rig,a,time,first,left?CapturedRig.LHand:CapturedRig.RHand,WeaponReach(rig,a,time))"),"the guard reads the blade the renderer draws, reach stretch included");
            foreach(var src in new[]{fx,core})Check(!src.Contains("timeScale")&&!src.Contains("fixedDeltaTime")&&!src.Contains("Physics."),"presentation only: no time scale, physics or simulation access");
            Check(!core.Contains("UnityEngine"),"the rules are pure C#");
            Check(game.Contains("if(!menu&&Spectator==null&&!cinematic.Directing&&prefs.cameraShake)presentation.BlockHitch(cameraView.transform,frame,arena.ViewPilot.Seat.id,renderTime,prefs.reducedEffects?.4f:1);"),"the hitch: the blocker's own live view, camera shake on, reduced with Reduced effects");
            Check(fx.Contains("view.rotation*=Quaternion.Euler(k.Pitch,0,k.Roll);view.position-=view.forward*k.Back;"),"the hitch kicks the camera transform only");
            Check(blasts.Contains("if(e.Kind==EventKind.Block)continue;")&&!NativeEventFeedback.HitBurst(EventKind.Block)&&!presentation.Contains("EventKind.Block?new Color(.3f,.9f,1)"),"the cyan cube/voxel block bursts are gone");
            Check(NativeImpactCues.Plan(new NativeImpactEvent{Kind=EventKind.Block,Position=new V3(0,1,0)},new NativeImpactCommand[NativeImpactCues.MaxCommands])==1,"(the source parity plan itself is unchanged)");
        }
    }
}
#endif
