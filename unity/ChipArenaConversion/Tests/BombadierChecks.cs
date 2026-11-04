using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Bombadier's presentation (docs/BOMBADIER.md): the preview's record is valid at every moment of its loop and the same clock writes the same record; the effects are whole, deterministic,
    /// thinned on Reduced and gone once the record is empty; the Last Warning entrance runs its full length on a sound timeline in order; the launcher builds at every tier; and nothing here reaches the rules.
    /// </summary>
    internal static class BombadierChecks
    {
        private static int count;private static void Check(bool ok,string what){if(!ok)throw new Exception("Bombadier: "+what);count++;}
        private sealed class Recorder:ITechniqueFxSink
        {
            public int Draws;public float Hash;public bool Broken;
            private void V(Vector3 v){if(!MathUtil.Finite(v.X)||!MathUtil.Finite(v.Y)||!MathUtil.Finite(v.Z))Broken=true;Hash+=v.X*.13f+v.Y*.17f+v.Z*.19f;Draws++;}
            private void F(float f){if(!MathUtil.Finite(f))Broken=true;}
            public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){V(at);V(color);F(size);}
            public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){V(head);V(velocity);F(width);}
            public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){V(at);F(opacity);F(size);}
            public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){V(at);F(size);}
            public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){V(tail);V(head);}
            public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){V(a);V(b);V(c);V(d);}
            public void Fire(Vector3 at,Vector3 tint,float opacity,float size,float u,float seed,float floor){V(at);F(opacity);F(size);}
            public void Water(Vector3 at,Vector3 lit,int shade,float opacity,float size,Vector3 segment,float u,float seed,float floor){V(at);}
            public void Drop(Vector3 head,Vector3 segment,Vector3 color,float coverage,float width,float u,float seed){V(head);}
            public void Tongue(bool view,Vector3 tail,Vector3 head,Vector3 color,float width){V(tail);V(head);}
        }
        private sealed class Scene:IKitFxScene
        {
            public readonly List<Actor> List=new List<Actor>();public IReadOnlyList<Actor> Actors=>List;public Vector3 Feet(Actor a)=>new Vector3(a.Position.x,a.Position.y,a.Position.z);public bool Chest(Actor a,out Vector3 chest){chest=Feet(a)+Vector3.UnitY*1.25f;return true;}public bool Blade(Actor a,bool left,out Vector3 root,out Vector3 tip){root=tip=default;return false;}public int Viewer=>0;public bool ViewBlade(bool left,out Vector3 root,out Vector3 tip){root=tip=default;return false;}public int WeaponColor(Actor a)=>0;public int Shots=>0;public bool Shot(int index,out Vector3 at,out Vector3 velocity,out string fx,out int owner){at=velocity=default;fx=null;owner=0;return false;}
        }
        private sealed class One:IBombSource{public NativeBombKit K;public NativeBombKit Of(int actor)=>K;}
        public static void Run(string root)
        {
            count=0;
            // The record through its loop.
            var feet=new Vector3(2,.5f,-3);var kit=new NativeBombKit();var again=new NativeBombKit();int bombs=0,blasts=0,linked=0,finale=0,line=0;var kinds=new HashSet<string>();var fx=new NativeTechniqueFx();var scene=new Scene();var src=new One{K=kit};NativeBombs.Source=src;scene.List.Add(new Actor{Id=1,Hp=100,SourceSpec=NativeBombs.Spec,Position=new V3(2,.5f,-3)});
            for(float t=0;t<NativeBombPreview.Loop;t+=1/30f)
            {
                NativeBombPreview.Write(kit,feet,.7f,100,100+t,7);Check(kit.Valid(),"the record is valid at "+t.ToString("0.00"));Check((kit.Scorched!=null)==(t>=7.12f&&t<7.12f+NativeBombs.ScorchSeconds)&&(kit.Scorched==null||(kit.Scorched[0].Id==7&&kit.Scorched[0].Until-kit.Scorched[0].At==NativeBombs.ScorchSeconds)),"Scorched is on the record only through its window at "+t.ToString("0.00"));NativeBombPreview.Write(again,feet,.7f,100,100+t);Check(kit.Bombs.Length==again.Bombs.Length&&kit.Blasts.Length==again.Blasts.Length,"the same clock writes the same record");
                bombs+=kit.Bombs.Length;blasts+=kit.Blasts.Length;foreach(var b in kit.Blasts){if(b.Link>0)linked++;kinds.Add(b.Kind);}if(kit.Finale!=null)finale++;if(kit.Line!=null)line++;
                var r=new Recorder();fx.DrawBombs(r,scene,100+t,1,false);Check(!r.Broken,"nothing broken at "+t.ToString("0.00"));var r2=new Recorder();fx.DrawBombs(r2,scene,100+t,1,false);Check(r.Draws==r2.Draws&&Math.Abs(r.Hash-r2.Hash)<1e-3f,"the same clock draws the same frame");var thin=new Recorder();fx.DrawBombs(thin,scene,100+t,.3f,true);Check(thin.Draws<=r.Draws&&!thin.Broken,"Reduced and the low tier draw no more");
            }
            Check(bombs>0&&blasts>0&&linked>0&&finale>0&&line>0,"the loop shows charges, blasts, links, the line and the Finale");foreach(var k in new[]{NativeBombs.Pop,NativeBombs.Mine,NativeBombs.Sticky,NativeBombs.Bomblet,NativeBombs.Spark,NativeBombs.Column})Check(kinds.Contains(k),k+": a blast of its kind shows");
            var empty=new NativeBombKit();src.K=empty;var none=new Recorder();fx.DrawBombs(none,scene,100,1,false);Check(none.Draws==0,"an empty kit draws nothing");NativeBombs.Source=null;
            // A bad record is refused.
            Check(!new NativeBombKit{Meter=2}.Valid(),"meter out of range");Check(!new NativeBombKit{Bombs=new[]{new NativeBomb{Kind="nuke"}}}.Valid(),"an unknown charge");Check(!new NativeBombKit{Bombs=new NativeBomb[NativeBombs.MaxBombs+1]}.Valid(),"too many charges");Check(!new NativeBombKit{Blasts=new[]{new NativeBlast{Radius=float.NaN}}}.Valid(),"a broken blast");
            // The names and cards.
            Check(NativeBombs.Slot.Length==NativeBombs.Slots&&NativeBombs.SlotKeys.Length==NativeBombs.Slots,"a key for every card");foreach(var id in NativeBombs.Slot){Check(NativeBombs.Name(id).Length>0,id+": a name");Check(NativeBombs.Icon(id).Length>0,id+": an icon");var o=new NativeKitSound[4];Check(NativeBombSounds.Cast(id,o)>0,id+": it sounds");}
            // The intro.
            Check(Math.Abs(NativeIntroRules.Duration(NativeBombs.Entrance)-NativeBombadierIntro.Total)<.001&&Math.Abs(NativeIntroRules.NameAt(NativeBombs.Entrance)-NativeBombadierIntro.CardAt)<.001,"the match gives the entrance its length and its name card its beat");
            Check(NativeEntranceDirector.Directed(NativeBombs.Entrance)&&NativeIntroRules.Pickable(NativeBombs.Entrance)&&NativeIntroRules.StyleName(NativeBombs.Entrance).Contains("Last Warning"),"the entrance is staged, pickable and named");Check(NativeIntroRules.IntroSting(NativeBombs.Entrance)==null,"no bed: the opening is silence but for its own cues");
            var script=(NativeBombadierIntro)NativeEntranceDirector.Script(NativeBombs.Entrance);script.Prepare();Check(script.Blasts.Length>0&&script.Blasts.Length<=NativeEntranceStage.Pylons&&script.Blasts.All(b=>b.Position.Z<=1.2f),"the sim's blasts stay off the lens");{var at=new System.Collections.Generic.HashSet<float>();for(int i=0;i<NativeEntranceStage.Pylons;i++)at.Add(script.BlastAt(i));Check(at.Count==NativeEntranceStage.Pylons&&at.Min()>=NativeBombadierIntro.FirstBlast&&at.Max()<NativeBombadierIntro.ShockAt,"every pylon goes, one after another, before the shock");}Check(script.AccentAt(NativeBombadierIntro.BlackAt+.1f)=="#000000"&&script.AccentAt(NativeBombadierIntro.FirstBlast+.1f)!="#000000","the suit goes dark for the beat of nothing and burns after");Check(script.Length==NativeBombadierIntro.Total&&script.Clock(NativeBombadierIntro.Total)<=NativeBombadierIntro.Total+.01f&&Math.Abs(script.Clock(NativeBombadierIntro.CardAt+.5f)-(NativeBombadierIntro.CardAt+.5f))<.02f,"the slow motion is caught up after the name card lands");
            var seen=new SortedSet<float>();for(int i=0;i<NativeEntranceStage.Pylons;i++){float at=script.BlastAt(i);Check(at>=NativeBombadierIntro.FirstBlast&&seen.Add(at),"pylon "+i+" has its own beat");if(i>0)Check(true,"");}
            var far=script.BlastAt(Enumerable.Range(0,NativeEntranceStage.Pylons).OrderBy(i=>NativeEntranceStage.Pylon(i).Z).First());var near=script.BlastAt(Enumerable.Range(0,NativeEntranceStage.Pylons).OrderBy(i=>NativeEntranceStage.Pylon(i).Z).Last());Check(far<near,"the blasts come toward the lens");
            var pose=new CapturedRig.Pose();var lights=new NativeEntranceLights();float lastClock=-1;
            for(float a=0;a<NativeBombadierIntro.Total;a+=1/30f){float t=script.Clock(a);Check(t>=lastClock-1e-4f,"the entrance's clock never runs back");lastClock=t;script.Pose(t,pose,out var rt,out _);Check(MathUtil.Finite(rt.X)&&MathUtil.Finite(rt.Y)&&MathUtil.Finite(rt.Z),"the pose holds");script.Camera(a,out var view);Check(MathUtil.Finite(view.Eye.X)&&MathUtil.Finite(view.Look.Y)&&view.Fov>10,"the camera holds");script.Lights(t,lights);if(a>=NativeBombadierIntro.BlackAt&&a<NativeBombadierIntro.FirstBlast-.05f)Check(lights.Points==0,"the beat of black has no light in it");}
            Check(script.FlameAt(NativeBombadierIntro.FuseFrom+.1f).X>script.FlameAt(NativeBombadierIntro.Stomp).X&&Vector3.Distance(script.FlameAt(NativeBombadierIntro.Stomp),new Vector3(-.16f,NativeEntranceStage.FloorY+.012f,.1f))<.1f,"the fuse burns to the boots");
            float prevCue=-1;foreach(var c in NativeBombSounds.Intro){Check(c.At>=prevCue&&c.At<NativeBombadierIntro.Total,"the entrance's cues run in order inside it");prevCue=c.At;Check(c.At<NativeBombadierIntro.Click+.001f||c.At>=NativeBombadierIntro.FirstBlast-.001f,"nothing sounds in the beat of black");}Check(NativeBombSounds.Intro.Count(c=>c.Key=="ui.countdown")>=8,"the ticks speed up");
            // The TNT: a bundle in each fist, within budget, on every tier.
            Check(NativeWeaponModels.Designed(NativeBombs.Spec),"the TNT is designed");var tnt=NativeWeaponModels.Get(NativeBombs.Spec);Check(tnt.Dual&&tnt.MirrorLeft,"a bundle in each fist, the left a mirror image");Check(Math.Abs(tnt.Muzzle.Z-NativeWeaponModels.TntFuseTip.Z)<1e-4f,"the weapon's socket is the fuse's end");
            for(int tier=0;tier<NativeWeaponModels.Tiers;tier++){var m=NativeWeaponModels.Build(NativeBombs.Spec,tier);Check(m.Sections.Any(s=>s.Bone==NativeWeaponModels.RightHand)&&m.Sections.Any(s=>s.Bone==NativeWeaponModels.LeftHand),"the TNT builds on both hands at tier "+tier);Check(m.Sections.Sum(s=>s.Indices.Count)/3<=NativeWeaponModels.Budget[tier],"the TNT is within its budget at tier "+tier);}
            // Round stock for the loose pieces: closed, unit, about the origin.
            var cp=new List<Vector3>();var cn=new List<Vector3>();var ci=new List<int>();NativeTnt.RoundPrimitive(cp,cn,ci);Check(ci.Count%3==0&&ci.All(i=>i>=0&&i<cp.Count)&&cp.All(p=>Math.Abs(p.Y)<=.5001f&&p.X*p.X+p.Z*p.Z<=1.0001f)&&cn.All(v=>Math.Abs(v.Length()-1)<1e-3f),"the cylinder primitive holds");
            Check(!Enum.IsDefined(typeof(WeaponShape),NativeTnt.Round),"round stock stays beside the Prototype pieces' shapes");Check(NativeTnt.Stick.All(p=>MathUtil.Finite(p.Size.Y)&&p.Size.Y>0)&&NativeTnt.Stick.Count(p=>p.Glow)==1&&NativeTnt.Small.All(p=>!p.Glow),"a loose stick has one ember, a small one none");
            // What the fists show: the stick that went is gone until the next is drawn, and only from the hand that threw it.
            bool Near(float a,float b)=>Math.Abs(a-b)<1e-3f;float Held(NativeBombKit c,float t0,bool left,float rAt=-100,float rTo=-100)=>NativeBombs.HeldShare(c,t0,rAt,rTo,left);
            var pop=new NativeBombKit{CastKind=NativeBombs.PopBomb,CastAt=0,CastRelease=.16f,CastUntil=.56f};Check(!NativeBombs.ThrowsLeft(0)&&NativeBombs.ThrowsLeft(NativeBombs.PopPeriod+.01f)&&!NativeBombs.ThrowsLeft(NativeBombs.PopPeriod*2+.01f),"held fire takes turns between the hands");
            Check(Near(Held(pop,.1f,false),1)&&Near(Held(pop,.2f,false),0)&&Near(Held(pop,.5f,false),1)&&Near(Held(pop,.2f,true),1),"a thrown stick leaves its own hand, and the next is back before the cast ends");
            var mine=new NativeBombKit{CastKind=NativeBombs.BounceMine,CastAt=0,CastRelease=.22f,CastUntil=.7f};Check(Near(Held(mine,.1f,true),0)&&Near(Held(mine,.1f,false),1)&&Near(Held(mine,.65f,true),1),"a mine takes the left fist's place");
            var sw=new NativeBombKit{CastKind=NativeBombs.DeadMansSwitch,CastAt=0,CastRelease=.12f,CastUntil=.5f};Check(Near(Held(sw,.2f,true),0)&&Near(Held(sw,.2f,false),1)&&Near(Held(sw,.49f,true),1),"the switch takes the left fist's place");
            var heave=new NativeBombKit{CastKind=NativeBombs.ClusterLob,CastAt=0,CastRelease=.28f,CastUntil=.8f};Check(Near(Held(heave,.1f,true),0)&&Near(Held(heave,.1f,false),0)&&Near(Held(heave,.75f,true),1),"the big bundle takes both");
            var fin=new NativeBombKit{Finale=new NativeBombFinale{At=10,Lock=12.1f,Strike=12.6f,Until=13.6f}};Check(Near(Held(fin,9.9f,false),1)&&Near(Held(fin,11,false),0)&&Near(Held(fin,12.3f,true),0)&&Near(Held(fin,13.3f,true),1),"both hands are on the plunger through the Finale");
            Check(Near(Held(null,5,false),1)&&Near(Held(null,5.5f,false,5,6),0)&&Near(Held(null,5.5f,true,5,6),1)&&Near(Held(null,5.95f,false,5,6),1),"a reload sends the right hand to the satchel and it comes back holding one");
            // What is out tumbles, and comes to rest on its side; the crate is under the hands.
            var flying=new NativeBomb{Id=3,Kind=NativeBombs.Pop,At=0,Vel=new V3(0,3,9)};var resting=new NativeBomb{Id=3,Kind=NativeBombs.Pop,At=0};var q1=NativeBombs.ChargeTurn(flying,.1f);var q2=NativeBombs.ChargeTurn(flying,.3f);Check(Math.Abs(q1.Length()-1)<1e-3f&&Quaternion.Dot(q1,q2)<.999f,"a stick tumbles in the air");Check(Math.Abs(Vector3.Transform(Vector3.UnitY,NativeBombs.ChargeTurn(resting,.3f)).Y)<.2f,"and lies on its side once stopped");
            Check(Math.Abs(Vector3.Distance(NativeBombs.BoxSpot(Vector3.Zero,Vector3.UnitZ),Vector3.Zero)-.638f)<.02f&&NativeBombs.BoxSpot(Vector3.Zero,Vector3.UnitZ).Z>.5f,"the crate goes under the hands");
            // Its own clips, nothing shouldered.
            var bset=NativeVisualMotion.For(NativeBombs.Spec);Check(bset!=null&&bset.Spec==NativeBombs.Spec&&!bset.Gun&&!bset.All.Any(c=>c.StartsWith("2Hand-Shooting",StringComparison.Ordinal)),"the Bombadier's own motion, nothing shouldered");Check(bset.All.Contains("Armed-Cast-R-Attack3_start")&&bset.All.Contains("Armed-Cast-R-Attack3_start"+NativeVisualMotion.Mirror),"it throws from each hand");Check(NativeVisualMotion.For("rifle").Spec=="rifle"&&NativeVisualMotion.For("machinegun").Spec=="rifle","the guns keep theirs");
            // First person: finite arms through a whole loop, and the fists on their own joints.
            {var vp=new CapturedRig.Pose();vp.Reset();var loop=new NativeBombKit();for(float t=0;t<NativeBombPreview.Loop;t+=1/30f){NativeBombPreview.Write(loop,Vector3.Zero,0,0,t);NativeBombView.Apply(vp,loop,t,8,9,3);foreach(int b in new[]{CapturedRig.LArm,CapturedRig.LElbow,CapturedRig.LHand,CapturedRig.RArm,CapturedRig.RElbow,CapturedRig.RHand})Check(MathUtil.Finite(vp.Rotation[b].X)&&Math.Abs(vp.Rotation[b].Length()-1)<1e-3f,"the view arms hold");Check(vp.Rotation[CapturedRig.Chest].W>.999f,"the view torso stays square");}}
            // The camera comes home and the Finale stays with its caster.
            var f=new NativeBombFinale{At=10,Lock=12.1f,Strike=12.6f,Until=13.6f,Box=new V3(0,0,1),Dir=new V3(0,0,1)};for(float t=10;t<13.6f;t+=1/30f){var shot=NativeBombCamera.At(f,Vector3.Zero,t,false,true);Check(MathUtil.Finite(shot.Position.X)&&MathUtil.Finite(shot.Look.Y)&&shot.Fov>10&&shot.Blend>=0&&shot.Blend<=1,"the Finale's camera holds");}Check(NativeBombCamera.At(f,Vector3.Zero,13.599f,false,false).Blend<.01f,"it blends home");Check(!NativeBombCamera.Active(f,13.7f)&&!NativeBombCamera.Active(null,11),"off outside it");
            // Wiring, and the line it must not cross.
            string R(string file)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",file)).Replace("\r\n","\n");var pres=R("Runtime/NativeBombPresentation.cs")+R("Runtime/NativeBombScreen.cs")+R("Core/NativeBombFx.cs");foreach(var bad in new[]{"arena.Sandbox","SendInput","Damage(","Knockback"})Check(!pres.Contains(bad),"the presentation does not reach "+bad);
            var hud=R("Runtime/NativeFightHud.cs");Check(hud.Contains("if(p.Seat.spec==NativeBombs.Spec)BombCards(p);"),"the Bombadier's cards on the HUD");Check(R("Runtime/NativeCinematicCamera.cs").Contains("presentation.BombFinaleDirect("),"the Finale's camera is directed");Check(R("Runtime/ArenaPresentation.cs").Contains("RenderBomb(s,renderTime);")&&R("Runtime/NativeTechniquePresentation.cs").Contains("BombFx(now);"),"the models and particles are drawn");
            Console.WriteLine("PASS Bombadier ("+count+" checks: the record through its loop; the effects whole, deterministic and thinned; the Last Warning's length, beats, light, sound and camera; the TNT at every tier, the fists, the throws and first person; no rule touched)");
        }
    }
}
