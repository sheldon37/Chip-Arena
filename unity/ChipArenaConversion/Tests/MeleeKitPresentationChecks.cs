#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The melee Q/E presentation (Claude; Astra's simulation, docs/MELEE-SIMULATION-HANDOFF.md): Core/MeleeKitMotion (pilot
    // poses), Core/NativeMeleeKitFx (effects), Core/NativeMeleeKitHud (Q/E cards), Core/NativeMeleeKitSounds (audio routing),
    // Runtime/NativeMeleeKitPresentation (the arena hooks). Each ability's replicated phase sequence is replayed on the
    // simulation's own timings (attackRate 1, NativeMeleeAbilities): every Kind/Phase owns a real pose (never the T-pose, the
    // guard or the legacy special phrase), the layer is continuous across every replicated phase boundary and within each
    // phase, the effects are a pure function of replicated actors and events (a JSON round trip of the snapshot draws the same
    // frame; nothing in the files reads authority-only state), quality tiers thin them, the HUD cards follow cooldown, recast
    // and stamina, and the audio keys are bank keys with built-in fallbacks. Rendering, Unity and Windows are not run here.
    internal static class MeleeKitPresentationChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Melee kit presentation: "+what);count++;}
        private static string Code(string text)=>string.Join("\n",text.Split('\n').Select(l=>{int c=l.IndexOf("//",StringComparison.Ordinal);return c<0?l:l.Substring(0,c);}));

        // ---- the replicated phase sequences (NativeMeleeAbilities: TryMeleeAbility, StepMeleeAbility, MeleeAbilityMotion,
        // TryMeleeRecast, MeleeRecastStrike, StepMeleeKitWorld) at attackRate 1 ----
        private struct Span{public float From,To;public string Phase;public float At,Release,Until,Landed;public string Special;}
        private sealed class Timeline{public string Kind,Spec;public readonly List<Span> Spans=new List<Span>();}
        private static Timeline Cast(string kind,string spec,string special,float at,float release,params (float to,string phase,float until)[] phases)
        {
            var t=new Timeline{Kind=kind,Spec=spec};float from=at;
            foreach(var p in phases){t.Spans.Add(new Span{From=from,To=p.to,Phase=p.phase,At=at,Release=release,Until=p.until,Landed=-1,Special=special});from=p.to;}
            return t;
        }
        private static Timeline[] Timelines()
        {
            var list=new List<Timeline>();
            // Flash Step: a hit stops the dash (recovery .2 s from the hit) or the full 7 m does.
            list.Add(Cast("flash-step","sword","flash-step",1,1.15f,(1.15f,"windup",1.37f),(1.277f,"active",1.37f),(1.477f,"recovery",1.477f)));
            list.Add(Cast("flash-step","sword","flash-step",1,1.15f,(1.15f,"windup",1.37f),(1.37f,"active",1.37f),(1.57f,"recovery",1.57f)));
            list.Add(Cast("rising-dragon","sword","sword-launch",1,1.26f,(1.26f,"windup",1.76f),(1.76f,"recovery",1.76f)));
            var sky=Cast("rising-dragon","sword","skyfall",1.5f,1.7f,(1.7f,"recast",2.15f),(1.85f,"fall",4.7f),(2.1f,"recovery",2.1f));for(int i=0;i<sky.Spans.Count;i++){var s=sky.Spans[i];s.Landed=s.Phase=="recovery"?1.85f:-1;sky.Spans[i]=s;}list.Add(sky);
            list.Add(Cast("rising-dragon","sword","skyfall",1.5f,1.7f,(1.7f,"recast",2.15f),(2.15f,"active",2.15f)));
            list.Add(Cast("shadow-step","daggers","shadow-step",1,1.15f,(1.15f,"windup",1.45f),(1.45f,"recovery",1.45f)));
            list.Add(Cast("kunai-mark","daggers","dagger-throw",1,1.19f,(1.19f,"windup",1.47f),(1.47f,"recovery",1.47f)));
            list.Add(Cast("kunai-mark","daggers","kunai-recast",2,2.2f,(2.2f,"recast",2.65f),(2.65f,"active",2.65f)));
            return list.Where(t=>MeleeKitMotion.Known(t.Kind)).ToArray();
        }
        private static Actor Pilot(string spec)=>new Actor{Id=1,SourceSpec=spec,Weapon=WeaponId.Sword,VisualSet=spec=="daggers"?1:spec=="greatsword"?2:spec=="shield"?3:0,CastRate=1,Hp=100,Grounded=true,MeleeKit=new NativeMeleeKit()};
        // The actor as the snapshot carries it at `time` (the span holding it), or no cast.
        private static void At(Actor a,Timeline t,float time,int spanIndex=-1)
        {
            var k=a.MeleeKit;k.Kind=null;k.Phase=null;a.SpecialKind=null;a.SpecialAt=-100;a.SpecialUntil=0;a.SpecialLanded=-1;
            for(int i=0;i<t.Spans.Count;i++)
            {
                var s=t.Spans[i];if(spanIndex>=0?i!=spanIndex:time<s.From||time>=s.To)continue;
                k.Kind=t.Kind;k.Phase=s.Phase;k.At=s.At;k.Release=s.Release;k.Until=s.Until;a.SpecialKind=s.Special;a.SpecialAt=s.At;a.SpecialRelease=s.Release;a.SpecialUntil=s.Until;a.SpecialReleased=time>=s.Release;a.SpecialLanded=s.Landed;return;
            }
        }
        private static CapturedRig.Pose Guard(int set){var row=new float[CapturedClip.Channels];CapturedClips.Sets[set].First(c=>c.StateName=="idle").Sample(0,row);var p=new CapturedRig.Pose();CapturedRig.Sample(row,set,p);return p;}

        public static void Run(string root,ConversionContent data)
        {
            Poses();Continuity();Effects();Hud(data);Sounds(root);Sources(root);
            Console.WriteLine("PASS Melee kit presentation ("+count+" checks: "+string.Join(", ",MeleeKitMotion.Kinds)+" own a pose in every replicated phase (no T-pose, guard or legacy phrase, third person and first-person arms), continuous across phase boundaries and within phases, effects a pure function of replicated actors and events with quality tiers, Q/E cards for cooldown/recast/stamina, audio bank keys with built-in fallbacks, runtime hooks, no authority-only reads, metas)");
        }
        private static void Poses()
        {
            var k=new KitPose();
            foreach(var t in Timelines())
            {
                var a=Pilot(t.Spec);int set=a.VisualSet;var guard=Guard(set);guard.World();var reset=new CapturedRig.Pose();reset.World();
                for(int i=0;i<t.Spans.Count;i++)
                {
                    var s=t.Spans[i];float mid=s.From+(s.To-s.From)*.6f;At(a,t,mid);string what=t.Kind+" "+s.Phase+(a.SpecialKind=="skyfall"||a.SpecialKind=="kunai-recast"?" (recast)":"");
                    Check(MeleeKitMotion.Clock(a,mid,out var c)&&MeleeKitMotion.Owns(a,mid)&&MeleeKitMotion.State(a,mid)!=null,"owns "+what);
                    Check(MeleeKitMotion.Sample(a,mid,k)&&k.W>.3f,"layer weight mid-phase "+what);
                    foreach(bool fp in new[]{false,true})
                    {
                        var p=Guard(set);NativeSpecialPose.Special(p,a,mid,fp);p.World();
                        // The legacy phrase never runs for a kit cast: NativeSpecialPose hands it to the layer.
                        var q=Guard(set);MeleeKitMotion.Apply(q,a,mid,fp);q.World();
                        Check(Vector3.Distance(p.WorldPosition[CapturedRig.RHand],q.WorldPosition[CapturedRig.RHand])<1e-5f,"NativeSpecialPose routes to the kit layer "+what+(fp?" fp":""));
                        float fromGuard=Math.Max(Vector3.Distance(p.WorldPosition[CapturedRig.RHand],guard.WorldPosition[CapturedRig.RHand]),Vector3.Distance(p.WorldPosition[CapturedRig.LHand],guard.WorldPosition[CapturedRig.LHand]));
                        float angle=Angle(p.WorldRotation[CapturedRig.RHand],guard.WorldRotation[CapturedRig.RHand]);
                        Check(fromGuard>.08f||angle>.35f,"a pose of its own, not the guard: "+what+(fp?" fp":"")+" ("+fromGuard.ToString("0.00")+" m, "+angle.ToString("0.00")+" rad)");
                        float bent=0;foreach(int b in new[]{CapturedRig.LArm,CapturedRig.LElbow,CapturedRig.RArm,CapturedRig.RElbow})bent+=Angle(p.Rotation[b],Quaternion.Identity);
                        Check(Math.Max(Vector3.Distance(p.WorldPosition[CapturedRig.RHand],reset.WorldPosition[CapturedRig.RHand]),Vector3.Distance(p.WorldPosition[CapturedRig.LHand],reset.WorldPosition[CapturedRig.LHand]))>.15f&&bent>1,"not the T-pose (rest arms): "+what+(fp?" fp":""));
                        Check(p.WorldPosition.All(v=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z)),"finite pose "+what);
                    }
                    Check(Math.Abs(NativeSpecialPose.HeldRotation(a,mid).W)>.9999f,"the grips aim the weapon (no legacy held tilt) "+what);
                }
                At(a,t,t.Spans[0].From-.01f);Check(!MeleeKitMotion.Owns(a,t.Spans[0].From-.01f),"no layer before the cast "+t.Kind);
            }
        }
        private static float Angle(Quaternion a,Quaternion b){float d=Math.Abs(Quaternion.Dot(a,b));return 2*(float)Math.Acos(Math.Min(1,d));}
        private static void Continuity()
        {
            KitPose x=new KitPose(),y=new KitPose(),empty=new KitPose{W=0};
            foreach(var t in Timelines())
            {
                var a=Pilot(t.Spec);var b=Pilot(t.Spec);
                // Every replicated boundary: the instant before with the old phase's fields, the instant after with the new.
                for(int i=0;i+1<t.Spans.Count;i++)
                {
                    float tb=t.Spans[i].To;At(a,t,tb-1e-4f,i);At(b,t,tb+1e-4f,i+1);
                    Check(MeleeKitMotion.Sample(a,tb-1e-4f,x)&&MeleeKitMotion.Sample(b,tb+1e-4f,y),"both sides sampled "+t.Kind+" "+t.Spans[i].Phase);
                    Check(x.Distance(y)<.03f,"continuous "+t.Kind+" "+t.Spans[i].Phase+" -> "+t.Spans[i+1].Phase+" ("+x.Distance(y).ToString("0.000")+")");
                }
                // The cast starts and ends at the pose the pilot showed (weight 0 at At and at the final Until).
                float start=t.Spans[0].From,end=t.Spans[t.Spans.Count-1].To;At(a,t,start);MeleeKitMotion.Sample(a,start,x);Check(x.W<.01f,"enters from the base pose "+t.Kind);
                At(a,t,end-1e-4f);if(t.Spans[t.Spans.Count-1].Phase=="recovery"||t.Kind=="kunai-mark"&&a.SpecialKind=="kunai-recast"||t.Spans[t.Spans.Count-1].Phase=="active"&&t.Kind=="rising-dragon"){MeleeKitMotion.Sample(a,end-1e-4f,x);Check(x.W<.12f,"leaves to the base pose "+t.Kind+" "+x.W.ToString("0.00"));}
                // Within every phase: no step of 1/240 s moves any field more than a fast swing can (no snap).
                float worst=0,prevT=start,worstAt=0;string worstField="";bool has=false;
                for(float time=start;time<end;time+=1/240f)
                {
                    At(a,t,time);if(!MeleeKitMotion.Sample(a,time,y)){has=false;continue;}
                    if(has&&Index(t,time)==Index(t,prevT)){float d=x.Distance(y);if(d>worst){worst=d;worstAt=time;worstField=Field(x,y);}}
                    x.From(y);prevT=time;has=true;
                }
                Check(worst<.32f,"smooth within phases "+t.Kind+" (worst step "+worst.ToString("0.000")+" at "+worstAt.ToString("0.000")+" s, "+worstField+")");
            }
        }
        private static string Field(KitPose a,KitPose b)
        {
            string best="";float most=-1;
            foreach(var f in typeof(KitPose).GetFields())
            {
                object x=f.GetValue(a),y=f.GetValue(b);float d=x is float fx?Math.Abs(fx-(float)y):x is Vector3 vx?(vx-(Vector3)y).Length():0;
                if(d>most){most=d;best=f.Name;}
            }
            return best;
        }
        private static int Index(Timeline t,float time){for(int i=0;i<t.Spans.Count;i++)if(time>=t.Spans[i].From&&time<t.Spans[i].To)return i;return -1;}

        // ---- effects ----
        private sealed class Recorder:IKitFxSink
        {
            public readonly List<string> Calls=new List<string>();public int Glows,Streaks,Smokes,Sprites,Lines,Bands,View;
            static string F(Vector3 v)=>v.X.ToString("0.0000")+","+v.Y.ToString("0.0000")+","+v.Z.ToString("0.0000");
            public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){Glows++;Calls.Add("g"+F(at)+F(color)+size.ToString("0.000"));}
            public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){Streaks++;Calls.Add("s"+F(head)+F(velocity));}
            public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){Smokes++;Calls.Add("k"+F(at)+opacity.ToString("0.000"));}
            public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){Sprites++;if(view)View++;Calls.Add("p"+view+F(at));}
            public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){Lines++;if(view)View++;Calls.Add("l"+F(tail)+F(head));}
            public readonly List<Vector3> BandPoints=new List<Vector3>();
            public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){Bands++;if(view)View++;Calls.Add("b"+F(a)+"|"+F(c));BandPoints.Add(a);BandPoints.Add(b);BandPoints.Add(c);BandPoints.Add(d);}
            public int Total=>Glows+Streaks+Smokes+Sprites+Lines+Bands;
        }
        // A frame's view as Runtime builds it, from replicated actors only (no rigs: chests and blades fall back or skip).
        private sealed class Scene:IKitFxScene
        {
            public List<Actor> List=new List<Actor>();public int ViewerId;public List<(Vector3,Vector3,string,int)> ShotList=new List<(Vector3,Vector3,string,int)>();
            public IReadOnlyList<Actor> Actors=>List;
            public Vector3 Feet(Actor a)=>new Vector3(a.Position.x,a.Position.y,a.Position.z);
            public bool Chest(Actor a,out Vector3 c){c=Feet(a)+new Vector3(0,1.25f,0);return true;}
            public bool Blade(Actor a,bool left,out Vector3 r,out Vector3 t){var f=Feet(a);r=f+new Vector3(left?-.3f:.3f,1,.3f);t=r+new Vector3(0,0,.9f);return !a.WeaponAway;}
            public int Viewer=>ViewerId;
            public bool ViewBlade(bool left,out Vector3 r,out Vector3 t){r=new Vector3(.2f,1.2f,.4f);t=r+new Vector3(0,.2f,.8f);return ViewerId!=0;}
            public int WeaponColor(Actor a)=>NativeBlockFx.DefaultColor(a.SourceSpec,a.VisualSet);
            public int Shots=>ShotList.Count;
            public bool Shot(int i,out Vector3 at,out Vector3 v,out string fx,out int owner){var s=ShotList[i];at=s.Item1;v=s.Item2;fx=s.Item3;owner=s.Item4;return true;}
        }
        private static ArenaEvent E(int id,EventKind kind,float time,int source,int target,Vector3 at,string fx,float amount=0,float radius=0,Vector3? from=null,Vector3? normal=null)
            =>new ArenaEvent{Id=id,Kind=kind,Time=time,Source=source,Target=target,Position=new V3(at.X,at.Y,at.Z),Detail=fx,Amount=amount,Radius=radius,From=from.HasValue?new V3(from.Value.X,from.Value.Y,from.Value.Z):new V3(at.X,at.Y,at.Z),Normal=normal.HasValue?new V3(normal.Value.X,normal.Value.Y,normal.Value.Z):new V3()};
        private static Recorder Frame(Scene scene,List<ArenaEvent> events,float time,float quality,bool low)
        {var fx=new NativeMeleeKitFx();fx.Observe(events,time);var r=new Recorder();fx.Draw(r,scene,time,quality,low);return r;}
        private static void Effects()
        {
            var caster=Pilot("sword");caster.Position=new V3(0,0,0);var victim=Pilot("sword");victim.Id=2;victim.Position=new V3(0,0,4);victim.Yaw=180;
            var scene=new Scene();scene.List.Add(caster);scene.List.Add(victim);var events=new List<ArenaEvent>();
            void Expect(string what,Func<Recorder,bool> ok,float time){var r=Frame(scene,events,time,1,false);Check(ok(r),what);}
            int id=1;
            foreach(var t in Timelines())
            {
                // Every cast draws its telegraph (the weapon's glint, the path or the gathering effect) through its windup.
                caster.SourceSpec=t.Spec;caster.VisualSet=Pilot(t.Spec).VisualSet;At(caster,t,t.Spans[0].From+.08f);events.Clear();
                Expect("telegraph "+t.Kind+" "+t.Spans[0].Phase,r=>r.Total>=2,t.Spans[0].From+.08f);
                // The caster's own glint rides its first-person view weapon (the view layer).
                scene.ViewerId=1;Expect("telegraph on the view weapon in first person "+t.Kind,r=>r.View>=1,t.Spans[0].From+.08f);scene.ViewerId=0;
            }
            caster.MeleeKit=new NativeMeleeKit();caster.SpecialKind=null;
            var kinds=new HashSet<string>(MeleeKitMotion.Kinds);
            if(kinds.Contains("flash-step"))
            {
                events.Clear();events.Add(E(id++,EventKind.AbilityHit,1,1,2,new Vector3(0,1.1f,3.6f),"flash-step",26,normal:-Vector3.UnitZ));Expect("ability hit flash",r=>r.Glows>=2&&r.Streaks>=3,1.05f);
                events.Add(E(id++,EventKind.AbilityCast,1,1,0,new Vector3(0,.865f,0),"flash-step"));Expect("cast ring on the floor",r=>r.Bands>=10,1.05f);
                Expect("records age out",r=>r.Total==0,3);
            }
            if(kinds.Contains("rising-dragon"))
            {
                events.Clear();events.Add(E(id++,EventKind.Land,1,1,2,new Vector3(0,.865f,1.8f),"rising-dragon",0,2));Expect("Skyfall landing wave (floor band, pillar)",r=>r.Bands>=12&&r.Lines>=1,1.1f);
                events.Add(E(id++,EventKind.Teleport,1,1,0,new Vector3(0,1.9f,.65f),"rising-dragon",from:new Vector3(0,.865f,0)));Expect("Skyfall teleport streak",r=>r.Lines>=1,1.05f);
            }
            if(kinds.Contains("shadow-step"))
            {
                events.Clear();caster.SourceSpec="daggers";caster.VisualSet=1;var k=caster.MeleeKit;k.Afterimage=new V3(0,.865f,0);k.AfterimageUntil=2;k.EmpowerUntil=2.5f;
                Check(NativeMeleeKitFx.DecoyOpacity(k,1.5f)>.5f&&NativeMeleeKitFx.DecoyOpacity(k,2.01f)==0&&NativeMeleeKitFx.DecoyOpacity(k,1.9f)<.8f,"decoy visible for its whole life, then gone");
                var decoy=NativeMeleeKitFx.DecoyActor(caster,k);Check(MeleeKitMotion.Owns(decoy,NativeMeleeKitFx.DecoyPoseTime(k)),"the decoy is posed as the departure crouch");
                Expect("empowered daggers glow (both blades)",r=>r.Sprites>=10,1.5f);
                scene.ViewerId=1;Expect("empowered daggers on the view weapon in first person",r=>r.View>=6,1.5f);scene.ViewerId=0;
                k.AfterimageUntil=0;k.EmpowerUntil=0;
            }
            if(kinds.Contains("kunai-mark"))
            {
                var k=caster.MeleeKit;k.MarkTarget=2;k.MarkUntil=5;Expect("the mark's sigil over the marked target",r=>r.Glows>=2&&r.Lines>=4,2);
                scene.ViewerId=2;Expect("the marked pilot's own view draws no sigil (its HUD warns it)",r=>r.Lines==0,2);scene.ViewerId=0;
                Check(NativeMeleeKitHud.MarkedFor(scene.List.ToArray(),2,2,out int owner)>2.9f&&owner==1,"HUD mark warning for the target");k.MarkTarget=0;
                scene.ShotList.Add((new Vector3(0,1.2f,3),new Vector3(0,0,30),"kunai-mark",1));Expect("kunai in flight trails red",r=>r.Streaks>=1,2);scene.ShotList.Clear();
            }
            // Determinism: a JSON round trip of the snapshot (as guests and the killcam receive it) draws the identical frame,
            // and drawing twice changes nothing (no hidden state).
            caster.SourceSpec="sword";caster.VisualSet=0;var fs=Timelines().FirstOrDefault();
            if(fs!=null)
            {
                At(caster,fs,1.2f);events.Clear();events.Add(E(id++,EventKind.AbilityCast,1,1,0,new Vector3(0,.865f,0),fs.Kind));
                var a=Frame(scene,events,1.2f,1,false);
                var copy=new Scene();foreach(var x in scene.List)copy.List.Add((Actor)Fields(x));
                var b=Frame(copy,events,1.2f,1,false);Check(a.Calls.SequenceEqual(b.Calls)&&a.Total>0,"replicated copy draws the same frame");
                var full=Frame(scene,events,1.2f,1,false);var reduced=Frame(scene,events,1.2f,.3f,false);var low=Frame(scene,events,1.2f,1,true);
                Check(full.Calls.SequenceEqual(a.Calls),"pure function of its inputs");Check(reduced.Total<full.Total&&low.Total<full.Total,"Reduced effects and the low tier thin the effects");
            }
            // Only kit events are kept; another fx tag is not this effect's.
            var only=new NativeMeleeKitFx();only.Observe(new List<ArenaEvent>{E(900,EventKind.Teleport,1,1,0,Vector3.Zero,"blink-strike"),E(901,EventKind.Hit,1,1,2,Vector3.Zero,"melee")},1);Check(only.Records.Count==0,"ignores non-kit events");
            only.Observe(new List<ArenaEvent>{E(5,EventKind.AbilityHit,.9f,1,2,Vector3.Zero,MeleeKitMotion.Kinds[0])},1);Check(only.Records.Count==1,"event ids restart (killcam/new arena) start over");
        }
        // A public-field copy (what JsonUtility carries on the wire and the killcam records): nothing private survives it.
        private static object Fields(object o)
        {
            if(o==null)return null;var t=o.GetType();if(t.IsPrimitive||t.IsEnum||o is string)return o;
            if(o is Array arr){var c=Array.CreateInstance(t.GetElementType(),arr.Length);for(int i=0;i<arr.Length;i++)c.SetValue(Fields(arr.GetValue(i)),i);return c;}
            var copy=t.IsValueType?Activator.CreateInstance(t):System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(t);
            foreach(var f in t.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))if(!f.IsInitOnly)f.SetValue(copy,Fields(f.GetValue(o)));
            return copy;
        }
        private static void Hud(ConversionContent data)
        {
            var k=new NativeMeleeKit();
            var ready=NativeMeleeKitHud.Of("q","flash-step",k,0,6,100,18,5);Check(ready.Ready&&ready.State=="READY","ready card");
            var low=NativeMeleeKitHud.Of("q","flash-step",k,0,6,10,18,5);Check(low.Low&&!low.Ready&&low.State=="LOW STAMINA","stamina gating once ready");
            var cooling=NativeMeleeKitHud.Of("q","flash-step",k,3,6,100,18,5);Check(!cooling.Ready&&cooling.State=="3.0s"&&Math.Abs(cooling.Dial-.5f)<1e-4f,"cooldown with its dial");
            var active=NativeMeleeKitHud.Of("q","flash-step",new NativeMeleeKit{Kind="flash-step",Until=6},5.8f,6,100,18,5);Check(active.Active&&active.State=="ACTIVE","cast running");
            var recast=NativeMeleeKitHud.Of("e","rising-dragon",new NativeMeleeKit{RecastKind="rising-dragon",RecastUntil=5.45f,Target=2},9.5f,10,0,35,5);Check(recast.Recast&&recast.State.StartsWith("RECAST 0.")&&Math.Abs(recast.Dial-.5f)<1e-3f,"recast window: its own state, drains, costs no stamina");
            var mark=NativeMeleeKitHud.Of("e","kunai-mark",new NativeMeleeKit{RecastKind="kunai-mark",RecastUntil=7,Target=2},9,11,100,30,5);Check(mark.Recast&&Math.Abs(mark.Dial-.5f)<1e-3f,"Kunai mark recast window over 4 s");
            var over=NativeMeleeKitHud.Of("e","kunai-mark",new NativeMeleeKit{RecastKind="kunai-mark",RecastUntil=4,Target=2},3,11,100,30,5);Check(!over.Recast&&over.State=="3.0s","closed recast window returns to the cooldown");
            // The dial counts the cooldown actually started: a Flash Step hit's half refund moves it on, it resets once ready.
            var span=new NativeMeleeKitHud.Span();Check(span.Update(6,6)==6&&span.Update(5.9f,6)==6,"span from the started cooldown");Check(span.Update(2.9f,6)==6,"a refund keeps the span (the dial jumps on)");
            Check(span.Update(0,6)==6&&span.Update(4,6)==4,"a new shorter cooldown (Kunai miss 4 s) is its own span");
            foreach(var a in data.abilities.Where(x=>NativeMeleeRules.Standard(x.spec)))Check(a.cost>0&&a.cooldown>0,"every melee card has a cost and cooldown: "+a.id);
        }
        private static void Sounds(string root)
        {
            var o=new NativeKitSound[NativeMeleeKitSounds.Max];var clips=new HashSet<string>(Directory.GetFiles(Path.Combine(root,"Assets/Conversion/Resources/Audio/audio"),"*.wav").Select(Path.GetFileNameWithoutExtension));
            int heard=0;
            foreach(var kind in MeleeKitMotion.Kinds)
            {
                foreach(bool recast in new[]{false,true}){int n=NativeMeleeKitSounds.Release(kind,recast,o);for(int i=0;i<n;i++){Check(NativeSoundKeys.Known(o[i].Key),"release key "+o[i].Key);Check(o[i].Clip==null||clips.Contains(o[i].Clip),"release fallback clip ships: "+o[i].Clip);heard++;}}
                foreach(EventKind e in new[]{EventKind.AbilityCast,EventKind.AbilityHit,EventKind.Teleport,EventKind.Recast,EventKind.Mark,EventKind.Absorb,EventKind.Bounce,EventKind.Land,EventKind.FissureStart})
                {int n=NativeMeleeKitSounds.Event(e,kind,1,false,o);for(int i=0;i<n;i++){Check(NativeSoundKeys.Known(o[i].Key),"event key "+o[i].Key);Check(o[i].Clip==null||clips.Contains(o[i].Clip),"event fallback clip ships: "+o[i].Clip);}}
                Check(NativeMeleeKitSounds.Event(EventKind.AbilityHit,kind,10,true,o)<=1,"the victim hears its hurt, not the attacker's impact: "+kind);
            }
            Check(heard>=MeleeKitMotion.Kinds.Length,"every cast's release is heard");
            Check(NativeMeleeKitSounds.Event(EventKind.AbilityHit,"blink-strike",1,false,o)==0,"other abilities keep their own cues");
        }
        private static void Sources(string root)
        {
            string R(string p)=>Code(File.ReadAllText(Path.Combine(root,p)));
            var motion=R("Assets/ChipArena/Core/MeleeKitMotion.cs");var fx=R("Assets/ChipArena/Core/NativeMeleeKitFx.cs");var hud=R("Assets/ChipArena/Core/NativeMeleeKitHud.cs");var runtime=R("Assets/ChipArena/Runtime/NativeMeleeKitPresentation.cs");
            // Nothing reads authority-only state: the layer and the effects see Actor/MeleeKit, events and the posed rigs only.
            foreach(var (file,text) in new[]{("MeleeKitMotion",motion),("NativeMeleeKitFx",fx),("NativeMeleeKitHud",hud)})
                foreach(string banned in new[]{"NativePilot","NativeArena","NativeMeleeAction",".Special.","Swing","p.Stats","UnityEngine","System.Random","DateTime"})Check(!text.Contains(banned),file+" reads no authority-only state ("+banned+")");
            foreach(string banned in new[]{".Special","Swing","QReady","EReady","Stamina","arena.Pilots","Time.timeScale",".Body.position"})Check(!runtime.Contains(banned),"runtime reads replicated data only ("+banned+")");
            Check(Regex.IsMatch(runtime,@"meleeKitFx\.Observe\(s\.Events"),"events from the snapshot (host, guests, killcam)");
            var special=R("Assets/ChipArena/Core/NativeSpecialPose.cs");Check(special.Contains("MeleeKitMotion.Apply(p,a,time,firstPerson)")&&special.Contains("MeleeKitMotion.Owns(a,time)"),"every rig route reaches the layer through NativeSpecialPose");
            var arena=R("Assets/ChipArena/Runtime/ArenaPresentation.cs");Check(arena.Contains("RenderMeleeKit(s,renderTime);")&&arena.Contains("MeleeKitFx(arena,camera,fields);meleeFlash?.Render(arena,camera,fields,WeaponEffectsQuality);WeaponFx(arena,camera,fields);")&&arena.Contains("ClearMeleeKit();"),"arena hooks: Render, RenderGlow before the weapons drawer, reset");
            var audio=R("Assets/ChipArena/Runtime/NativeAudio.cs");Check(audio.Contains("NativeMeleeKitSounds.Event(")&&audio.Contains("NativeMeleeKitSounds.Release("),"audio hooks");
            var hudRt=R("Assets/ChipArena/Runtime/NativeFightHud.cs");Check(hudRt.Contains("NativeMeleeKitHud.Of(")&&hudRt.Contains("NativeMeleeKitHud.ShieldAway("),"HUD hooks");
            var dressing=R("Assets/ChipArena/Core/NativeWeaponDressing.cs");Check(dressing.Contains("MeleeKitMotion.Sweeps(a,time)"),"blade trails follow the kit's strike windows");
            foreach(string file in new[]{"Core/MeleeKitMotion.cs","Core/NativeMeleeKitFx.cs","Core/NativeMeleeKitHud.cs","Core/NativeMeleeKitSounds.cs","Runtime/NativeMeleeKitPresentation.cs"})
            {
                string meta=Path.Combine(root,"Assets/ChipArena",file+".meta");Check(File.Exists(meta),"meta: "+file);
                var guid=Regex.Match(File.ReadAllText(meta),@"^guid: ([0-9a-f]{32})$",RegexOptions.Multiline);Check(guid.Success,"meta guid: "+file);
                int owners=Directory.GetFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories).Count(m=>File.ReadAllText(m).Contains("guid: "+guid.Groups[1].Value));Check(owners==1,"unique guid: "+file);
            }
        }
    }
}
#endif
