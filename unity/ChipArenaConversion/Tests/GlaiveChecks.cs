using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Glaive (docs/GLAIVE.md): its content rows and access, the replicated record (valid, refused when broken, deep-copied), the disc's
    /// paths (the flick's loop out and back, the halo's circle, a flight carried on), the keyed moves (every fist in reach, the ring's edge
    /// leading every cut, the cuts running into each other, right to left then left to right, the whirl's whole turn), the model, the
    /// effects (whole, deterministic, thinner on the low tier), the sounds, and the wiring in the Runtime (read as text).
    /// </summary>
    internal static class GlaiveChecks
    {
        private static int count;private static void Check(bool ok,string what){if(!ok)throw new Exception("Glaive: "+what);count++;}
        private sealed class Recorder:ITechniqueFxSink
        {
            public int Draws;public bool Broken;
            private void V(Vector3 v){if(!MathUtil.Finite(v.X)||!MathUtil.Finite(v.Y)||!MathUtil.Finite(v.Z))Broken=true;Draws++;}
            public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){V(at);V(color);}
            public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){V(head);}
            public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){V(at);}
            public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){V(at);}
            public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){V(tail);V(head);}
            public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){V(a);V(b);V(c);V(d);}
            public void Fire(Vector3 at,Vector3 tint,float opacity,float size,float u,float seed,float floor){V(at);}
            public void Water(Vector3 at,Vector3 lit,int shade,float opacity,float size,Vector3 segment,float u,float seed,float floor){V(at);}
            public void Drop(Vector3 at,Vector3 segment,Vector3 color,float opacity,float size,float u,float seed){V(at);}
            public void Tongue(bool view,Vector3 a,Vector3 b,Vector3 color,float width){V(a);V(b);}
        }
        private sealed class Scene:IKitFxScene
        {
            public readonly List<Actor> List=new List<Actor>();public IReadOnlyList<Actor> Actors=>List;public Vector3 Feet(Actor a)=>new Vector3(a.Position.x,a.Position.y,a.Position.z);
            public bool Chest(Actor a,out Vector3 chest){chest=Feet(a)+Vector3.UnitY*1.2f;return true;}
            public bool Blade(Actor a,bool left,out Vector3 root,out Vector3 tip){root=tip=default;return false;}
            public int Viewer=>0;public bool ViewBlade(bool left,out Vector3 root,out Vector3 tip){root=tip=default;return false;}
            public int WeaponColor(Actor a)=>0x6fe4ff;public int Shots=>0;public bool Shot(int index,out Vector3 at,out Vector3 velocity,out string fx,out int owner){at=velocity=default;fx=null;owner=0;return false;}
        }
        public static void Run(string root)
        {
            count=0;var data=System.Text.Json.JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new System.Text.Json.JsonSerializerOptions{IncludeFields=true});data.Validate();
            // ---- the rows and access ----
            var row=data.Weapon(NativeGlaive.Spec);Check(row!=null&&row.devOnly&&row.@class=="melee"&&row.name=="Glaive","a DEV melee row");
            Check(data.abilities.Count(a=>a.spec==NativeGlaive.Spec)==2&&data.Ability(NativeGlaive.Spec,"q")?.id==NativeGlaive.Throw&&data.Ability(NativeGlaive.Spec,"e")?.id==NativeGlaive.Halo,"its Q (the throw) and E (the halo)");
            Check(NativeLoadoutText.Role(row).StartsWith("DEV")&&NativeRangedRules.KeyboardQ(data,NativeGlaive.Spec,true)&&!NativeAccess.Guest.Allows("weapon:"+NativeGlaive.Spec,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),"its loadout line, Q on the keyboard reaches it, a guest cannot take it");
            // ---- the record ----
            var k=new NativeGlaiveKit{Ready=new float[NativeGlaive.Slots],CastKind=NativeGlaive.Throw,CastAt=10,CastRelease=10.14f,CastUntil=10.4f,Disc=NativeGlaive.Out,DiscAt=10.14f,DiscPos=new V3(0,1.2f,1),DiscVel=new V3(0,0,30),DiscSampleAt=10.2f,Hits=new[]{new NativeGlaiveHit{Id=1,Kind=NativeGlaive.HitThrow,At=10.3f,Pos=new V3(0,1,6),Dir=new V3(0,0,1)}}};
            Check(k.Valid(),"a record in flight is valid");var copy=k.ViewCopy();copy.Hits[0].At=99;copy.Ready[0]=5;Check(k.Hits[0].At==10.3f&&k.Ready[0]==0,"its copy is deep");
            foreach(var broken in new Func<NativeGlaiveKit,NativeGlaiveKit>[]{x=>{x.Disc=9;return x;},x=>{x.Bounces=9;return x;},x=>{x.CastStep=7;return x;},x=>{x.DiscPos=new V3(float.NaN,0,0);return x;},x=>{x.Hits=new[]{new NativeGlaiveHit{Kind="nope",Pos=default,Dir=default}};return x;},x=>{x.CastKind=new string('x',40);return x;}})
                Check(!broken(k.ViewCopy()).Valid(),"a broken record is refused");
            Check(NativeGlaive.Throw.Length<=24&&NativeGlaive.Halo.Length<=24&&NativeGlaive.Skim.Length<=24&&NativeGlaive.Burst.Length<=24,"its kinds fit the wire");
            // ---- the disc's paths ----
            var from=new Vector3(0,1.2f,0);var dir=Vector3.UnitZ;
            var mid=NativeGlaive.FlickAt(from,dir,.5f);Check(Math.Abs(mid.Z-NativeGlaive.FlickReach)<.05f,"the flick goes out its reach");
            Check(NativeGlaive.FlickAt(from,dir,.25f).X>.5f&&NativeGlaive.FlickAt(from,dir,.75f).X<-.5f,"out on the right, back on the left");
            Check(Vector3.Distance(NativeGlaive.FlickAt(from,dir,1),from)<.01f,"and back to the hand");
            var centre=new Vector3(0,.865f,0);var o1=NativeGlaive.OrbitAt(centre,dir,0,1);var o2=NativeGlaive.OrbitAt(centre,dir,0,1.1f);
            Check(Math.Abs(new Vector2(o1.X,o1.Z).Length()-NativeGlaive.HaloRadius)<.01f&&Vector3.Distance(o1,o2)>.5f,"the halo circles at its radius");
            Check(NativeGlaive.DiscAt(k,10.25f,centre,out var flying)&&Math.Abs(flying.Z-(1+30*.05f))<.01f,"a flight is carried on from its sample");
            k.Disc=NativeGlaive.Hand;Check(!NativeGlaive.DiscAt(k,11,centre,out _)&&NativeGlaive.InHand(k,11)==1,"in the hand it is not out");k.Disc=NativeGlaive.Out;Check(NativeGlaive.InHand(k,11)==0,"out, the hand is empty");
            k.Disc=NativeGlaive.Hand;k.Reformed=true;k.DiscAt=20;Check(NativeGlaive.InHand(k,20.06f)<.6f&&NativeGlaive.InHand(k,20.2f)==1,"re-formed, it grows back in");
            // ---- the keyed moves ----
            foreach(NativeGlaiveMotion.Move m in Enum.GetValues(typeof(NativeGlaiveMotion.Move)))
            {
                if(m==NativeGlaiveMotion.Move.None)continue;var keys=NativeGlaiveMotion.KeyTimes(m);float worst=0,fastest=0;Vector3? last=null;const float dt=1/240f;
                for(float t=keys[0];t<=keys[keys.Length-1]+1e-4f;t+=dt)
                {
                    var p=NativeGlaiveMotion.At(m,t);var d=NativeDogMallet.HandleAt(p);var f=NativeDogMallet.FaceAt(p);
                    Check(Math.Abs(d.Length()-1)<1e-3f&&Math.Abs(f.Length()-1)<1e-3f&&Math.Abs(Vector3.Dot(d,f))<2e-3f&&p.Free>.99f,m+": the ring's way and edge are square, the left hand its own, at "+t.ToString("0.000"));
                    worst=Math.Max(worst,(NativeDogMallet.GripAt(p)-NativeDogMallet.ShoulderAt(p,true)).Length());var ring=NativeGlaiveMotion.RingAt(p);
                    if(last.HasValue)fastest=Math.Max(fastest,(ring-last.Value).Length()/dt);last=ring;Check(ring.Y>.15f,m+": the ring never goes into the floor");
                }
                Check(worst<=NativeDogMallet.Reach*1.05f,m+": the fist stays in its arm's reach (worst "+worst.ToString("0.00")+" m)");
                Check(fastest<=75,m+": the ring never jumps (a cut at most 75 m/s, a pop is far more; fastest "+fastest.ToString("0.0")+" m/s)");
            }
            {
                float Side(NativeGlaiveMotion.Move m,float t)=>NativeGlaiveMotion.RingAt(NativeGlaiveMotion.At(m,t)).X;
                Check(Side(NativeGlaiveMotion.Move.Cut1,-.06f)>.3f&&Side(NativeGlaiveMotion.Move.Cut1,.1f)<-.3f,"the first cut runs from the right to the left");
                Check(Side(NativeGlaiveMotion.Move.Cut2,-.06f)<-.3f&&Side(NativeGlaiveMotion.Move.Cut2,.1f)>.3f,"the second from the left to the right");
                bool Same(DogMalletPose x,DogMalletPose y)=>(NativeDogMallet.GripAt(x)-NativeDogMallet.GripAt(y)).Length()<.03f&&Vector3.Dot(NativeDogMallet.HandleAt(x),NativeDogMallet.HandleAt(y))>.995f;
                Check(Same(NativeGlaiveMotion.At(NativeGlaiveMotion.Move.Cut1,.25f),NativeGlaiveMotion.At(NativeGlaiveMotion.Move.Cut2,-.15f))&&Same(NativeGlaiveMotion.At(NativeGlaiveMotion.Move.Cut2,.25f),NativeGlaiveMotion.At(NativeGlaiveMotion.Move.Whirl,-.2f))&&Same(NativeGlaiveMotion.At(NativeGlaiveMotion.Move.Whirl,.36f),NativeGlaiveMotion.At(NativeGlaiveMotion.Move.Flick,-.16f)),"each cut starts where the last ended (the combo flows)");
                var w0=NativeGlaiveMotion.At(NativeGlaiveMotion.Move.Whirl,-.06f);var w1=NativeGlaiveMotion.At(NativeGlaiveMotion.Move.Whirl,.36f);Check(w1.Spin-w0.Spin>350,"the whirl turns the whole body once");
                foreach(var m in new[]{NativeGlaiveMotion.Move.Cut1,NativeGlaiveMotion.Move.Cut2,NativeGlaiveMotion.Move.Skim})
                {
                    float hit=m==NativeGlaiveMotion.Move.Skim?.18f:0,h=.004f;var a=NativeGlaiveMotion.RingAt(NativeGlaiveMotion.At(m,hit-h));var b=NativeGlaiveMotion.RingAt(NativeGlaiveMotion.At(m,hit-h*.01f));var p=NativeGlaiveMotion.At(m,hit-h*.5f);
                    var v=b-a;var d=NativeDogMallet.HandleAt(p);var across=v-d*Vector3.Dot(v,d);Check(across.Length()>1e-5f&&Math.Abs(Vector3.Dot(Vector3.Normalize(across),NativeDogMallet.FaceAt(p)))>.95f&&p.Smear>.2f,m+": at the hit the ring's edge leads, smeared");
                }
            }
            {
                var kit=new NativeGlaiveKit{Ready=new float[2],CastKind=NativeGlaive.Swing,CastAt=30,CastRelease=30.15f,CastUntil=30.4f,CastStep=1};
                Check(NativeGlaiveMotion.Sample(kit,30.15f,out var p,out var mv,out _)&&mv==NativeGlaiveMotion.Move.Cut2&&Math.Abs(p.Weight-1)<1e-4f,"a cut's release is its hit key at full weight");
                kit.CastStep=3;kit.CastRelease=30.16f;kit.CastUntil=30.7f;Check(NativeGlaiveMotion.Sample(kit,30.6f,out _,out var fl,out _)&&fl==NativeGlaiveMotion.Move.Flick,"the flick runs on to its catch");
                var caught=new NativeGlaiveKit{Ready=new float[2],Disc=NativeGlaive.Hand,DiscAt=40};Check(NativeGlaiveMotion.Sample(caught,40.1f,out _,out var cm,out _)&&cm==NativeGlaiveMotion.Move.Catch&&!NativeGlaiveMotion.Sample(caught,41,out _,out _,out _),"a catch recoils, then lets the clip be");
            }
            // ---- the model ----
            Check(NativeWeaponModels.Designed(NativeGlaive.Spec),"the ring is a designed model");
            for(int tier=0;tier<3;tier++){var mesh=NativeWeaponModels.Build(NativeGlaive.Spec,tier);Check(mesh!=null&&mesh.Triangles>100&&mesh.Triangles<=NativeWeaponModels.Budget[tier],"tier "+tier+" builds inside its budget ("+(mesh?.Triangles??0)+")");}
            // ---- the effects ----
            {
                var scene=new Scene();var a=new Actor{Id=3,SourceSpec=NativeGlaive.Spec,Position=new V3(0,0,0),Yaw=0};scene.List.Add(a);var fx=new NativeTechniqueFx();
                a.Glaive=new NativeGlaiveKit{Ready=new float[2],CastKind=NativeGlaive.Swing,CastAt=50,CastRelease=50.15f,CastUntil=50.4f,CastStep=0};
                var cut=new Recorder();fx.DrawGlaives(cut,scene,50.14f,1,false);var calm=new Recorder();fx.DrawGlaives(calm,scene,50.01f,1,false);Check(cut.Draws>calm.Draws+20&&!cut.Broken,"a cut draws its crescent along the edge ("+cut.Draws+" against "+calm.Draws+")");
                a.Glaive=k.ViewCopy();a.Glaive.Disc=NativeGlaive.Out;a.Glaive.DiscAt=10.14f;a.Glaive.Reformed=false;var fly=new Recorder();fx.DrawGlaives(fly,scene,10.25f,1,false);var flyLow=new Recorder();fx.DrawGlaives(flyLow,scene,10.25f,.3f,true);Check(fly.Draws>20&&flyLow.Draws<fly.Draws&&!fly.Broken,"the flying ring glows and trails its wake, thinner on the low tier");
                a.Glaive=new NativeGlaiveKit{Ready=new float[2],Disc=NativeGlaive.Gone,DiscAt=60,Hits=new[]{new NativeGlaiveHit{Id=2,Kind=NativeGlaive.HitBurst,At=60,Pos=new V3(0,1,4),Dir=new V3(0,1,0)}}};
                var burst=new Recorder();fx.DrawGlaives(burst,scene,60.1f,1,false);Check(burst.Draws>80&&!burst.Broken,"the burst: flash, shockwaves, shards, sparks, dust, the ring re-forming ("+burst.Draws+")");
                var again=new Recorder();fx.DrawGlaives(again,scene,60.1f,1,false);Check(again.Draws==burst.Draws,"deterministic");
                foreach(var kind in new[]{NativeGlaive.HitCut,NativeGlaive.HitWhirl,NativeGlaive.HitFlick,NativeGlaive.HitThrow,NativeGlaive.HitRicochet,NativeGlaive.HitReturn,NativeGlaive.HitHalo,NativeGlaive.HitCatch,NativeGlaive.HitSkim})
                {a.Glaive=new NativeGlaiveKit{Ready=new float[2],Hits=new[]{new NativeGlaiveHit{Id=3,Kind=kind,At=70,Pos=new V3(0,1,2),Dir=new V3(0,0,1)}}};var r=new Recorder();fx.DrawGlaives(r,scene,70.05f,1,false);Check(r.Draws>3&&!r.Broken,kind+" draws");}
            }
            // ---- the sounds ----
            foreach(var id in new[]{"ring-cut","ring-slice","ring-throw","ring-ping","ring-hum","ring-burst","ring-catch","ring-flick"}){var smp=NativeToonSynth.Samples(id);Check(NativeToonSynth.Known(id)&&smp.Length>1000&&smp.All(MathUtil.Finite)&&smp.Max(Math.Abs)<=NativeToonSynth.Peak+1e-3f,id+" is synthesized");}
            // ---- the wiring (the Runtime is read as text: CoreTests compiles Core only) ----
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            var arena=R("NativeArena.cs");Check(arena.Contains("StepGlaiveWorld();")&&arena.Contains("StepGlaiveInput(p);")&&arena.Contains("GlaiveMotion(p,ref velocity);")&&arena.Contains("a.Glaive=p.Seat.spec==NativeGlaive.Spec?p.Glaive.ViewCopy():null;"),"the rules are stepped, moved and snapshotted");
            Check(R("NativeFrameCodec.cs").Contains("a.HasGlaive=a.Glaive!=null;")&&R("NativeNetworkFrame.cs").Contains("a.Glaive!=null&&!a.Glaive.Valid()")&&R("NativeNetworkFrame.cs").Contains("p.Glaive=p.Pose.Glaive.ViewCopy();"),"the record crosses the wire, is checked and mirrored");
            Check(R("NativeMelee.cs").Contains("DogBusy(p)||GlaiveBusy(p)")&&R("NativeGuard.cs").Contains("!GlaiveBusy(p)")&&R("NativeBots.cs").Contains("BotGlaive(p,ref input,target,distance,visible)"),"a committed move holds the guard and the roll; bots use it");
            Check(R("NativeVisualMotionPresentation.cs").Contains("a.SourceSpec==NativeGlaive.Spec")&&R("NativeDogMalletPresentation.cs").Contains("NativeGlaiveMotion.Sample(")&&R("NativeSourcePresentation.cs").Contains("GlaiveShown(a,time)")&&R("ArenaPresentation.cs").Contains("RenderGlaive(s,renderTime);")&&R("NativeTechniquePresentation.cs").Contains("GlaiveFx(now);")&&R("NativeFightHud.cs").Contains("GlaiveCards(p);")&&R("NativeAudio.cs").Contains("GlaiveMoments(p,pos,t,mine);"),"its moves, ring, effects, cards and sound are hooked");
            foreach(var file in new[]{"NativeGlaivePresentation.cs"}){var text=R(file);Check(!text.Contains("Damage(")&&!text.Contains("Knockback")&&!text.Contains("SendInput"),file+": presentation only");}
            Console.WriteLine("PASS glaive: "+count+" checks (the rows, the record, the disc's paths, the keyed moves, the model, the effects, the sounds, the wiring)");
        }
    }
}
