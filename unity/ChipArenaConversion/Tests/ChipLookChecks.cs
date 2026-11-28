#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The chips and shots that were built of boxes (owner 2026-11-04; Core/NativeChipLooks.cs): every chip part has a soft look and no box, the tiers thin
    // them, a frame's budget holds any stack; the smooth models are closed, outward-facing and at the source's sizes; the blade is a crescent that leaves
    // no plane edge-on; the source's tables and meshes are untouched, for the parity fixtures. How it looks is reviewed in Unity captures, not here.
    internal static class ChipLookChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Chip looks: "+what);count++;}
        public static void Run(string root)
        {
            count=0;Parts();Tiers();Heavy();Meshes();Blade();Scene();Text(root);
            Console.WriteLine("PASS chip looks ("+count+" checks: no chip part draws a box, Reduced and low thin them, a frame's budget, a heavy round's slug and tracer, the ball, the frag, rocket, mark and ball lightning closed and outward, the blade, the source tables unchanged)");
        }
        private static void Parts()
        {
            var all=new NativeShotModules{Bounces=3,Homing=1,Split=1,Pierces=1,Impulse=2,Shrapnel=1,Velocity=2,Vacuum=1,Meteor=1,LifeSteal=.1,Zigzag=1,Boomerang=1};
            var parts=new List<NativeShotRules.Part>();NativeShotRules.Modules(all,true,true,parts);Check(parts.Count>20,"a full stack has its many parts");
            var seen=new HashSet<NativeChipPart>();
            foreach(var p in parts)
            {
                NativeShotRules.Linear(p.Color,false,out float r,out float g,out float b);var look=NativeChipLooks.Part(p.Shape,p.Anim,(float)p.SX,r,g,b);seen.Add(look);
                if(p.Shape==NativeShotShape.Box)Check(look==NativeChipPart.Streak,"a box part (Split, Velocity, Return to Sender) is a streak");
                if(p.Anim==NativeShotRules.PartAnim.Flame)Check(look==NativeChipPart.Flame,"a flame cone is a flame");
                if(p.Shape==NativeShotShape.Ring)Check(look==NativeChipPart.Ring,"a ring is a ring card");
                Check(NativeChipLooks.Gain(look)>1,"a part has its light");
            }
            Check(seen.Contains(NativeChipPart.Shard)&&seen.Contains(NativeChipPart.Glint)&&seen.Contains(NativeChipPart.Dark),"Tracking and Piercing shards, Shrapnel's and poison's glints, Vacuum's dark core");
            Check(NativeChipLooks.Part(NativeShotShape.Orb,default,.18f,.9f,.1f,.1f)==NativeChipPart.Ball&&NativeChipLooks.Part(NativeShotShape.Orb,default,.18f*NativeTracers.ModuleShare,.9f,.1f,.1f)==NativeChipPart.Glint,"Pong's ball is a ball, a small orb a glint");
            Check(NativeChipLooks.Near(.3f)==0&&NativeChipLooks.Near(5)==1&&NativeChipLooks.Near(80)==0,"a part fades at the eye and far off");
        }
        private static void Tiers()
        {
            int Kept(float q,bool low){int n=0;for(int i=0;i<30;i++)if(NativeChipLooks.Keep(i,q,low))n++;return n;}
            Check(Kept(1,false)==14&&Kept(1,true)==8&&Kept(.3f,false)==3,"the tiers thin a shot's parts");
            Check(NativeChipLooks.Keep(0,.3f,false)&&NativeChipLooks.Keep(0,1,true),"every tier shows a shot's first part");
            Check(NativeChipLooks.Budget(1,false)>NativeChipLooks.Budget(1,true)&&NativeChipLooks.Budget(1,true)>NativeChipLooks.Budget(.3f,false),"each tier's budget is smaller");
            Check(NativeChipLooks.Budget(1,false)<=240,"a frame's parts stay in the hundreds whatever the stack");
            Check(NativeChipLooks.BladeSegments(.3f,false)<NativeChipLooks.BladeSegments(1,true)&&NativeChipLooks.BladeSegments(1,true)<=NativeChipLooks.BladeSegments(1,false),"the blade's quads thin");
            Check(NativeChipLooks.BladeEchoes(.3f,false)==0&&NativeChipLooks.BladeEchoes(1,true)==1&&NativeChipLooks.BladeEchoes(1,false)==2,"its echoes thin");
        }
        private static void Heavy()
        {
            Check(!NativeChipLooks.Heavy(NativeChipLooks.RoundRadius)&&!NativeChipLooks.Heavy(NativeChipLooks.RoundRadius*.7),"a plain round and Needle Storm's are the needle alone");
            Check(NativeChipLooks.Heavy(NativeChipLooks.RoundRadius*1.5)&&NativeChipLooks.Heavy(NativeChipLooks.RoundRadius*1.7),"Lobber's, Going Up's, Firework Factory's and Dense Core's show their size");
            var plain=new NativeTracerStyle{Core=.02f,Sheath=.08f,HeadSize=.07f};var s=plain;NativeChipLooks.HeavyTracer(ref s,NativeChipLooks.RoundRadius);Check(s.Core==plain.Core,"a plain round's tracer is its own");
            s=plain;NativeChipLooks.HeavyTracer(ref s,NativeChipLooks.RoundRadius*4);Check(Math.Abs(s.Core/plain.Core-2)<1e-3&&s.Sheath>plain.Sheath&&s.HeadSize>plain.HeadSize,"a heavy round's tracer widens by the root of its size");
            s=plain;NativeChipLooks.HeavyTracer(ref s,NativeChipLooks.RoundRadius*25);Check(s.Core<=plain.Core*NativeChipLooks.HeavyWiden+1e-4f,"and is held");
        }
        private static void Closed(NativeChipMesh m,string what,float limit,int faces)
        {
            Check(m.Indices.Length%3==0&&m.Paint.Length==m.VertexCount&&m.Normals.Length==m.Positions.Length,what+": its arrays agree");Check(m.Indices.Length/3<=faces,what+": "+m.Indices.Length/3+" faces, at most "+faces);
            Vector3 P(int i)=>new Vector3(m.Positions[i*3],m.Positions[i*3+1],m.Positions[i*3+2]);Vector3 N(int i)=>new Vector3(m.Normals[i*3],m.Normals[i*3+1],m.Normals[i*3+2]);
            for(int i=0;i<m.VertexCount;i++){Check(Math.Abs(N(i).Length()-1)<1e-3,what+": a unit normal");var p=P(i);Check(Math.Abs(p.X)<=limit&&Math.Abs(p.Y)<=limit&&Math.Abs(p.Z)<=limit,what+": within its size");Check(m.Paint[i]>=0&&m.Paint[i]<=1,what+": a shade");}
            for(int t=0;t<m.Indices.Length;t+=3)
            {
                int a=m.Indices[t],b=m.Indices[t+1],c=m.Indices[t+2];var f=Vector3.Cross(P(b)-P(a),P(c)-P(a));if(f.LengthSquared()<1e-12f)continue;
                Check(Vector3.Dot(f,N(a)+N(b)+N(c))>0,what+": a face turned outward, as the source's are");
            }
        }
        private static void Meshes()
        {
            Closed(NativeChipLooks.Ball,"the ball",.51f,500);Closed(NativeChipLooks.BallAway,"the far ball",.51f,120);Closed(NativeChipLooks.BallPlain,"a plain ball",.51f,300);Closed(NativeChipLooks.Slug,"the slug",.51f,160);
            var far=NativeChipLooks.BallAway;var near=NativeChipLooks.Ball;Check(far.VertexCount*3<near.VertexCount,"far off the ball is a third of its faces");
            float R(NativeChipMesh m){float best=0;for(int i=0;i<m.VertexCount;i++){var p=new Vector3(m.Positions[i*3],m.Positions[i*3+1],m.Positions[i*3+2]);best=Math.Max(best,p.Length());}return best;}
            Check(Math.Abs(R(near)-.5f)<.02f&&Math.Abs(R(far)-.5f)<.001f,"the ball fills the source's unit box, so the box's transform makes it the round's radius");
            int dark=0;for(int i=0;i<near.VertexCount;i++)if(near.Paint[i]<.2f)dark++;Check(dark>=30,"its three finger holes");
            Closed(NativeChipLooks.Smooth(NativeShotShape.Frag),"the frag",.15f,220);Closed(NativeChipLooks.Smooth(NativeShotShape.Rocket),"the rocket",.34f,240);Closed(NativeChipLooks.Smooth(NativeShotShape.OrbitalMark),"the mark",.31f,80);Closed(NativeChipLooks.Smooth(NativeShotShape.BallLightning),"ball lightning",.34f,500);
            foreach(var s in new[]{NativeShotShape.TidalWave,NativeShotShape.FlameLance,NativeShotShape.Shackles,NativeShotShape.Fallback,NativeShotShape.FireCone,NativeShotShape.Box})Check(NativeChipLooks.Smooth(s)==null,s+" keeps its source mesh (not box-built, or another drawer's)");
            Check(ReferenceEquals(NativeChipLooks.Smooth(NativeShotShape.Rocket),NativeChipLooks.Smooth(NativeShotShape.Rocket)),"built once");
            Check(NativeChipLooks.HaloShare("frag-grenade")<1&&NativeChipLooks.HaloShare("rocket")<1&&NativeChipLooks.HaloShare("ball-lightning")<NativeChipLooks.HaloShare("rocket")&&NativeChipLooks.HaloShare("flame-lance")==1&&NativeChipLooks.HaloShare(null)==1,"the halo gives way to a smooth body, the others keep theirs");
            Check(NativeChipLooks.Shade(1)>NativeChipLooks.Shade(0)&&NativeChipLooks.Shade(-1)==NativeChipLooks.Shade(0)&&NativeChipLooks.Shade(1)<=1.0001f,"lit from the sky");
            Check(Math.Abs(NativeChipLooks.BallSpin(8,1)-NativeChipLooks.BallSpin(8,0)-NativeChipLooks.BallTurn)<1e-3f,"the ball turns at its own rate");
        }
        private static void Blade()
        {
            var dir=Vector3.Normalize(new Vector3(.3f,.1f,1));NativeChipLooks.BladeFrame(dir,4,out var side,out var lean);
            Check(Math.Abs(side.Length()-1)<1e-3&&Math.Abs(lean.Length()-1)<1e-3&&Math.Abs(Vector3.Dot(side,dir))<1e-3,"its frame is unit and across the flight");
            NativeChipLooks.BladeFrame(dir,5,out var other,out _);Check(Vector3.Dot(side,other)<.999f,"it rolls one way or the other by shot");
            NativeChipLooks.BladeFrame(Vector3.UnitY,4,out var up,out _);Check(up.Length()>.9f,"straight up still has a frame");
            var tipA=NativeChipLooks.BladePoint(0,1,side,lean,dir,1);var tipB=NativeChipLooks.BladePoint(1,1,side,lean,dir,1);var mid=NativeChipLooks.BladePoint(.5f,1,side,lean,dir,1);var inner=NativeChipLooks.BladePoint(.5f,0,side,lean,dir,1);
            float width=(tipA-tipB).Length();Check(width>.8f&&width<1.2f,"about a metre across, as the source's beam ("+width+")");
            Check(Vector3.Dot(mid-tipA,dir)>0&&Vector3.Dot(mid-tipB,dir)>0,"its tips sweep behind its middle");
            Check((mid-inner).Length()>.2f&&(mid-inner).Length()<.45f,"the band has depth");
            Check(Math.Abs(Vector3.Dot(lean,dir))<.9f,"it is flat to nobody: seen from behind and from the side it has width");
            NativeChipLooks.BladeEcho(0,out float back,out float size,out float gain);Check(back==0&&size==1&&gain==1,"the blade itself");
            float last=0,lastGain=1;for(int k=1;k<=2;k++){NativeChipLooks.BladeEcho(k,out back,out size,out gain);Check(back>last&&gain<lastGain&&size<1,"each echo further back, fainter, smaller");last=back;lastGain=gain;}
        }
        private static void Scene()
        {
            // The source's scene keeps its source numbers: a part is only marked.
            var scene=new NativeShotScene();scene.Begin(1,1,1/60d,1);
            var v=new NativeShotView{Kind=NativeShotKind.Bullet,Id=9,Spec="fire",Class="magic",Y=1,VZ=30,Radius=.14,FuseLeft=1e9,Modules=new NativeShotModules{Velocity=1,Bounces=1,Split=1}};scene.Shot(v);
            Check(scene.Draws.Count>1&&scene.Draws[0].Look==NativeShotLook.Source,"a shot's model is the source's");
            int boxes=0;for(int i=1;i<scene.Draws.Count;i++){var d=scene.Draws[i];Check(d.Look==NativeShotLook.Part&&d.PartIndex==i-1,"a chip part is marked with its index");if(d.Shape==NativeShotShape.Box)boxes++;}
            Check(boxes==5,"the table still holds its box parts for the parity fixtures");
            Check(NativeShotShapes.Mesh(NativeShotShape.Rocket).VertexCount==4*24&&NativeShotShapes.Mesh(NativeShotShape.Frag).VertexCount==3*24,"the source's box-built meshes are untouched");
        }
        private static void Text(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/"+f)).Replace("\r\n","\n");
            string shots=R("Runtime/NativeShotPresentation.cs"),fx=R("Runtime/NativeWeaponFxPresentation.cs"),tracer=R("Runtime/NativeTracerPresentation.cs"),glow=R("Runtime/NativeCombatGlow.cs"),shader=R("Resources/NativeWeaponFx.shader"),draw=R("Runtime/NativeWeaponFxDraw.cs");
            Check(shots.Contains("if(ChipFx!=null&&d.Look==NativeShotLook.Part){ChipPart(d);continue;}"),"a part goes to the effects drawer, never the solid layer, when there is one");
            Check(shots.Contains("if(ChipFx!=null&&SmoothModel(d,color))continue;"),"a box-built model takes its smooth mesh");
            Check(shots.Contains("else if(ChipFx!=null&&v.SwordBeam&&scene.Draws.Count>first)HidePackHead(first);"),"the blade's box is hidden");
            Check(shots.Contains("else if(ChipFx!=null&&v.Bowling&&scene.Draws.Count>first)BallLook(first,v,now);"),"the ball replaces the cube");
            Check(shots.Contains("Relook(first,NativeShotLook.Slug)"),"a heavy round keeps its size on a slug");
            Check(fx.Contains("if(shots!=null)shots.ChipFx=draw;"),"the game turns them on");
            Check(tracer.Contains("if(v.SwordBeam||v.Bowling){ChipShotFx(draw,arena,b,v,now,q,low,trails,eye);return;}"),"the blade and the ball draw their own effects");
            Check(tracer.Contains("NativeChipLooks.HeavyTracer(ref e.Style,v.Radius);"),"a heavy round's tracer widens");
            Check(glow.Contains("||v.SwordBeam||v.Bowling);"),"no .7 m halo or old ribbon on the blade or the ball");
            Check(shader.Contains("if(mode<0.5&&w>0.5)shape=w>1.5?FxBlade(uv):FxRing(uv);")&&draw.Contains("RingCard=1,BladeCard=2"),"the shader's cards");
            // `line` and its kin are HLSL keywords (geometry shader primitives): one of them as a variable costs the whole drawer its shader.
            foreach(var word in new[]{"float line","float point","float triangle","float lineadj","float triangleadj"})Check(!shader.Contains(word),"no HLSL keyword as a name: "+word);
            // Cosmetic: nothing here writes the match.
            foreach(var f in new[]{R("Core/NativeChipLooks.cs")})Check(!f.Contains("UnityEngine")&&!f.Contains("NativeArena")&&!f.Contains(".Bullets")&&!f.Contains("Damage"),"the rules know nothing of the match");
            int at=tracer.IndexOf("private void ChipShotFx(",StringComparison.Ordinal),end=tracer.IndexOf("private void RoundBegin(",StringComparison.Ordinal);Check(at>0&&end>at,"the blade's and the ball's effects are found");
            string body=tracer.Substring(at,end-at);Check(!body.Contains("Damage")&&!body.Contains("b.Position=")&&!body.Contains("b.Velocity=")&&!body.Contains("Emit("),"and they read the shot, nothing more");
        }
    }
}
#endif
