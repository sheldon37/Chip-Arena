using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The moments of a plunging stab a cue keys to.</summary>
    public enum NativePlungeMoment{Drop,Impact,Free}

    /// <summary>
    /// The plunging stab's effects (docs/MELEE-FEEL.md "Plunging stab"), drawn through the melee kit's sink
    /// (Runtime/NativeAirAttackFxPresentation, the shared effects renderer) from what every peer holds: each pilot's
    /// NativePlungeData (NativePlungeFields, the one field mapping), its drawn feet and posed blade, the impact event
    /// (NativePlungeFields.Impact) and the plunger's Hit events. Every pilot is watched by a NativePlungeWatch, the rule the rig uses.
    ///  - The drop: weapon-accent speed streaks trailing up off the blade, shoulders and knees, and an air-tear at the blade's
    ///    point (a hot core in a flickering ring with a bow shock round it).
    ///  - The impact (sized by the fall height: small at 2 m, big at 8 m): a flash at the point, a shock ring, a ring of light
    ///    out to the rule's ring radius, cracks running over the floor, hot sparks, a dust wave rolling out, and (through the
    ///    destruction kit: Structures) a crater stain with a chip and cracks, a rubble burst and the camera kick.
    ///  - The direct victim: a hit flash, a ring and sparks at its chest.
    ///  - The pull-out: a puff and grit where the blade comes out.
    /// Reduced effects (.3) and the low tier thin it (and the shake follows the camera-shake setting and its cap). The first-
    /// person viewer's own streaks give way to the view's speed lines. Presentation only; no allocation once warm.
    /// </summary>
    public sealed class NativePlungeFx
    {
        public const int Mote=0,Core=1,Ring=2;
        /// <summary>The air-tear's share on the first-person view blade (it sits close to the eye).</summary>
        public const float ViewTear=.6f;
        /// <summary>One impact or pull-out: who, where (the ground point under the feet and the blade's contact ahead of them),
        /// how high the fall was and its 2-8 m weight, the rule's ring radius, the facing (Unity yaw, degrees) and its seed.</summary>
        public struct Burst{public int Pilot,Seed;public string Spec;public float At,Height,Heavy,Radius,Floor,Yaw;public Vector3 Feet,Point;public bool Pull;}
        /// <summary>A direct hit on the victim (its chest when posed, else the event point).</summary>
        public struct Flash{public int Pilot,Target,Seed;public string Spec;public float At;public Vector3 Point,Dir;}
        private sealed class Pilot{public readonly NativePlungeWatch Watch=new NativePlungeWatch();public int Impacts,Frees;public Vector3 Feet;public float Yaw;public bool HasEvent;public Vector3 EventAt;public float EventRadius,EventTime=-100;}
        private readonly Dictionary<int,Pilot> pilots=new Dictionary<int,Pilot>();
        private readonly List<Burst> bursts=new List<Burst>(MaxBursts);private readonly List<Flash> flashes=new List<Flash>(MaxFlashes);
        /// <summary>This frame's new impacts and pull-outs, for the destruction kit (Structures) and anything else keyed to them.</summary>
        public readonly List<Burst> Fresh=new List<Burst>(8);
        private int cursor,seeds;private float clock=float.NegativeInfinity;
        public const int MaxBursts=12,MaxFlashes=8;
        public int Live=>bursts.Count;public int LiveFlashes=>flashes.Count;
        public void Clear(){pilots.Clear();bursts.Clear();flashes.Clear();Fresh.Clear();cursor=0;clock=float.NegativeInfinity;}
        /// <summary>Starts a frame at `time`: a rewind (the killcam) starts over.</summary>
        public void Begin(float time){Fresh.Clear();if(time<clock-.25f){bursts.Clear();flashes.Clear();foreach(var p in pilots.Values)p.Watch.Reset();cursor=0;}clock=time;}
        /// <summary>The facing a pilot's stab is drawn along (Unity yaw, degrees): the swing's committed heading while it holds.</summary>
        public static float Facing(Actor a)=>a!=null&&a.CommitAt>-90&&Math.Abs(a.CommitAt-a.AttackAt)<.002f?a.CommitYaw:a?.Yaw??0;
        public static Vector3 Forward(float yawDegrees){float y=yawDegrees*(float)Math.PI/180;return new Vector3((float)Math.Sin(y),0,(float)Math.Cos(y));}
        /// <summary>How far ahead of the feet the blade's contact plants (the plant keys' reach, NativePlungePose).</summary>
        public static float Reach(string spec){var k=NativePlungePose.For(spec);return k==null?.8f:k.Plant.Grip.Z;}
        public static float Side(string spec){var k=NativePlungePose.For(spec);return k==null?0:-k.Plant.Grip.X;}

        /// <summary>One presented pilot this frame: `d` its plunge data, `feet` where it is drawn (Unity world), `yaw` its facing,
        /// `grounded` the replicated contact, `owned` a special, stun, roll, juggle or knockdown owning it.</summary>
        public void Track(int id,string spec,in NativePlungeData d,Vector3 feet,float yaw,bool grounded,float time,bool owned)
        {
            if(!pilots.TryGetValue(id,out var p))pilots[id]=p=new Pilot();
            p.Watch.Observe(d,spec,feet.Y,grounded,time,owned);p.Feet=feet;p.Yaw=yaw;var w=p.Watch;
            if(w.Impacts>p.Impacts&&w.Down)
            {
                p.Impacts=w.Impacts;Vector3 ground=p.HasEvent&&Math.Abs(p.EventTime-w.ImpactAt)<.3f?p.EventAt:feet;
                float r=p.HasEvent&&p.EventRadius>0?p.EventRadius:NativePlungeRules.Radius(spec);
                Add(new Burst{Pilot=id,Seed=++seeds,Spec=spec,At=time,Height=w.Height,Heavy=NativePlungeRules.Heavy(w.Height),Radius=r,Floor=ground.Y,Yaw=yaw,Feet=ground,Point=Contact(ground,yaw,spec)});
            }
            else p.Impacts=w.Impacts;
            if(w.Frees>p.Frees&&w.Down){p.Frees=w.Frees;Add(new Burst{Pilot=id,Seed=++seeds,Spec=spec,At=time,Height=w.Height,Heavy=NativePlungeRules.Heavy(w.Height),Radius=NativePlungeRules.Radius(spec),Floor=feet.Y,Yaw=yaw,Feet=feet,Point=Contact(feet,yaw,spec),Pull=true});}
            else p.Frees=w.Frees;
        }
        /// <summary>The blade's contact on the floor: Reach ahead of the feet along the facing, Side across it.</summary>
        public static Vector3 Contact(Vector3 feet,float yaw,string spec){Vector3 f=Forward(yaw),right=new Vector3(f.Z,0,-f.X);return feet+f*Reach(spec)+right*Side(spec);}
        private void Add(in Burst b){if(bursts.Count>=MaxBursts)bursts.RemoveAt(0);bursts.Add(b);Fresh.Add(b);}
        /// <summary>The frame's events (idempotent): a plunge impact event gives its pilot's watch the replicated time and point;
        /// a direct hit by a plunging pilot flashes its victim.</summary>
        public void Events(IReadOnlyList<ArenaEvent> events,float time)
        {
            if(events==null)return;int newest=0;for(int i=0;i<events.Count;i++)if(events[i].Id>newest)newest=events[i].Id;
            if(newest>0&&newest<cursor)cursor=0;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];if(e.Id<=cursor||time-e.Time>1)continue;
                if(NativePlungeFields.Impact(e)&&pilots.TryGetValue(e.Source,out var p))
                {p.HasEvent=true;p.EventAt=new Vector3(e.Position.x,e.Position.y,e.Position.z);p.EventRadius=e.Radius;p.EventTime=e.Time;p.Watch.ImpactEvent(e.Time,NativePlungeFields.Height(e));}
                else if((e.Kind==EventKind.Hit||e.Kind==EventKind.Headshot)&&pilots.TryGetValue(e.Source,out var s)&&s.Watch.Down&&NativePlungeFields.Direct(e,e.Source,s.Watch.PlungeAt,s.Watch.ImpactAt,new V3(s.Feet.X,s.Feet.Y,s.Feet.Z)))
                {
                    if(flashes.Count>=MaxFlashes)flashes.RemoveAt(0);
                    flashes.Add(new Flash{Pilot=e.Source,Target=e.Target,Seed=++seeds,Spec=s.Watch.Spec,At=e.Time,Point=new Vector3(e.Position.x,e.Position.y,e.Position.z),Dir=new Vector3(e.Normal.x,e.Normal.y,e.Normal.z)});
                }
            }
            if(newest>cursor)cursor=newest;
        }
        /// <summary>The pilot's watch (null before it was tracked).</summary>
        public NativePlungeWatch Watch(int id)=>pilots.TryGetValue(id,out var p)?p.Watch:null;

        // ---- sizes (the fall height's 2-8 m weight `heavy`, 0..1) ----
        /// <summary>The crater's radius (the stain and rim), the cracks' reach, the dust wave's reach and the rubble burst's size (m).</summary>
        public static float Crater(float radius,float heavy)=>radius*(.4f+.6f*MeleeMath.Clamp(heavy,0,1));
        public static float CrackReach(float radius,float heavy)=>radius*(.45f+.95f*MeleeMath.Clamp(heavy,0,1));
        public static float DustReach(float radius,float heavy)=>radius*(1.1f+1f*MeleeMath.Clamp(heavy,0,1));
        public static float Rubble(float heavy)=>.45f+.75f*MeleeMath.Clamp(heavy,0,1);
        public const float RubbleFrom=.25f;public const int RubbleColor=0x9a9ea6;
        /// <summary>The camera kick (the destruction kit's shake: distance falloff, the camera-shake setting, Reduced halving it,
        /// the .3 m cap): peak offset (m) and length (s), heavier for a bigger fall; the jump attack's is .025-.065 m.</summary>
        public static float ShakePeak(float heavy)=>.035f+.065f*MeleeMath.Clamp(heavy,0,1);
        public static float ShakeSeconds(float heavy)=>.32f+.28f*MeleeMath.Clamp(heavy,0,1);
        public const float FlashLife=.13f,ShockLife=.26f,RingLife=.3f,CrackLife=.55f,SparkLife=.5f,DustLife=.8f,PullLife=.5f,HitLife=.22f,Life=.95f;
        public const int Cracks=10,ReducedCracks=4,Sparks=16,ReducedSparks=6,Dust=16,ReducedDust=6,RingMotes=22,ReducedRingMotes=10,Streaks=6,ReducedStreaks=3,HitSparks=10,ReducedHitSparks=4,PullPuffs=5,ReducedPullPuffs=2;
        public static int Count(int full,int reduced,float quality,bool low)=>NativeAirAttackFx.Count(full,reduced,quality,low);
        /// <summary>Reduced effects dim the flashes (and the speed lines) as the sprint's do.</summary>
        public static float Dim(float quality)=>NativeSprintFx.Dim(quality);
        private static float Out(float u)=>1-(1-u)*(1-u)*(1-u);
        private static float Hash(int seed,int k){uint h=(uint)(seed*73856093)^(uint)(k*19349663);h^=h>>13;h*=0x5bd1e995;h^=h>>15;return (h&0xffffff)/16777216f;}

        // ---- the destruction kit's share (crater marks, rubble, the camera kick) ----
        /// <summary>The crater stain and chip at the contact and cracks round it, a rubble burst and the shake, by the fall height;
        /// a pull-out leaves a small chip and puff of grit. Decals stay on the floor (the ground point's height).</summary>
        public static void Structures(NativeStructureFx st,in Burst b,float time)
        {
            if(st==null)return;var at=new V3(b.Point.X,b.Floor,b.Point.Z);var up=new V3(0,1,0);uint seed=(uint)b.Seed*2654435761u;
            if(b.Pull){if(b.Spec=="greatsword")st.Break(NativeStructureMaterial.Concrete,RubbleColor,new V3(at.x,b.Floor+.08f,at.z),new V3(.3f,.16f,.3f),b.Floor,seed^0x51u,time,false);return;}
            float crater=Crater(b.Radius,b.Heavy),size=Rubble(b.Heavy);
            st.Quake(new V3(b.Feet.X,b.Floor,b.Feet.Z),ShakePeak(b.Heavy),ShakeSeconds(b.Heavy),seed,time);
            st.AddMark(NativeStructureMark.Stain,at,up,crater*2.1f,seed^1,time);
            st.AddMark(NativeStructureMark.Chip,at,up,.45f+.5f*b.Heavy,seed^2,time);
            int n=3+(int)Math.Round(4*b.Heavy);
            for(int i=0;i<n;i++){float a=i*MeleeMath.TAU/n+Hash(b.Seed,i)*.6f,d=crater*(.55f+.35f*Hash(b.Seed,i+40));st.AddMark(NativeStructureMark.Crack,new V3(at.x+(float)Math.Cos(a)*d,b.Floor,at.z+(float)Math.Sin(a)*d),up,(.8f+.9f*b.Heavy)*(.8f+.4f*Hash(b.Seed,i+60)),seed^(uint)(10+i),time);}
            // Rubble only from a real drop (from about 3.5 m): the smaller ones throw the effect's own grit.
            if(b.Heavy>=RubbleFrom)st.Break(NativeStructureMaterial.Concrete,RubbleColor,new V3(at.x,b.Floor+size*.25f,at.z),new V3(size,size*.5f,size),b.Floor,seed^0x77u,time,false);
        }

        // ---- the cues (existing bank keys only) ----
        public struct Cue{public string Key;public float Gain,Rate;}
        /// <summary>The sounds of one moment (into `o`, returns the count): the drop's whoosh (the structure whoosh with the
        /// weapon's own swing, lower), the impact (the ground impact under the landing thump, a structure thud growing with the
        /// fall and the weapon's hit, lower and louder for a bigger fall), the pull-out (debris grit and a short swing). Existing
        /// bank keys only; no allocation.</summary>
        public static int Sounds(NativePlungeMoment m,string spec,float heavy,Cue[] o)
        {
            if(o==null||o.Length<4)return 0;float h=MeleeMath.Clamp(heavy,0,1),heavyRate=spec=="greatsword"?.85f:spec=="daggers"?1.1f:1;int n=0;
            switch(m)
            {
                case NativePlungeMoment.Drop:o[n++]=new Cue{Key="structure.whoosh",Gain=.55f,Rate=1.3f*heavyRate};o[n++]=new Cue{Key=SwingKey(spec),Gain=.5f,Rate=.8f*heavyRate};break;
                case NativePlungeMoment.Impact:
                    o[n++]=new Cue{Key="impact.ground",Gain=.75f+.25f*h,Rate=heavyRate*(1.05f-.2f*h)};o[n++]=new Cue{Key="move.land",Gain=.7f+.2f*h,Rate=.88f*heavyRate};
                    o[n++]=new Cue{Key="structure.thud",Gain=.3f+.55f*h,Rate=heavyRate*(1-.15f*h)};o[n++]=new Cue{Key=HitKey(spec),Gain=.45f,Rate=.75f*heavyRate};break;
                case NativePlungeMoment.Free:o[n++]=new Cue{Key="impact.debris",Gain=.4f,Rate=1.1f};o[n++]=new Cue{Key=SwingKey(spec),Gain=.3f,Rate=.95f*heavyRate};break;
            }
            return n;
        }

        /// <summary>The weapon's own swing and hit keys (the shield has no swing of its own: the miss whoosh; its hit is the bash).</summary>
        public static string SwingKey(string spec)=>spec=="daggers"?"melee.daggers.swing":spec=="greatsword"?"melee.greatsword.swing":spec=="shield"?"melee.miss":"melee.sword.swing";
        public static string HitKey(string spec)=>spec=="daggers"?"melee.daggers.hit":spec=="greatsword"?"melee.greatsword.hit":spec=="shield"?"melee.shield.bash":"melee.sword.hit";
        // ---- drawing ----
        private static Vector3 Lin(int hex,float gain=1)=>NativeMeleeKitFx.Lin(hex,gain);
        /// <summary>Draws every live plunge effect at `time` (the scene's actors, posed blades and chests; the first-person viewer's
        /// own streaks are left to its speed lines).</summary>
        public void Draw(IKitFxSink sink,IKitFxScene scene,float time,float quality,bool low)
        {
            if(sink==null||scene==null)return;float dim=Dim(quality);
            int streaks=Count(Streaks,ReducedStreaks,quality,low);
            var actors=scene.Actors;
            for(int i=0;i<actors.Count;i++)
            {
                var a=actors[i];if(a==null||!pilots.TryGetValue(a.Id,out var p)||!p.Watch.Is(a.AttackAt)||!p.Watch.Clock(time,out var c))continue;
                if(c.Phase!=NativePlungePhase.Drop&&!(c.Phase==NativePlungePhase.Windup&&c.Tuck>.6f))continue;
                Vector3 edge=NativeMeleeKitFx.Edge(a.SourceSpec,scene.WeaponColor(a)),core=NativeMeleeKitFx.CoreOf(a.SourceSpec);
                // The first-person viewer: no body streaks (its speed lines carry the drop), the air-tear rides its view blade's point.
                if(a.Id==scene.Viewer)
                {
                    float vw=(c.Phase==NativePlungePhase.Drop?MeleeMath.Ease(c.Fall/.05f):.4f)*dim*ViewTear;
                    if(scene.ViewBlade(false,out var vr,out var vt))AirTear(sink,time,a.Id,vw,vt,vr,edge,core);
                    if(a.SourceSpec=="daggers"&&scene.ViewBlade(true,out var lr,out var lt))AirTear(sink,time+.37f,a.Id+7,vw*.7f,lt,lr,edge,core);
                    continue;
                }
                Vector3 feet=scene.Feet(a),f=Forward(p.Yaw),right=new Vector3(f.Z,0,-f.X);
                bool blade=scene.Blade(a,false,out var root,out var tip);if(!blade){tip=feet+f*.4f+new Vector3(0,-.3f,0);root=tip+new Vector3(0,1,0);}
                float w=c.Phase==NativePlungePhase.Drop?MeleeMath.Ease(c.Fall/.05f):.4f;
                DropStreaks(sink,streaks,a.Id*31+(int)(p.Watch.At*60),c.Fall,w*dim,feet,f,right,tip,edge);
                AirTear(sink,time,a.Id,w*dim,tip,root,edge,core);
                if(a.SourceSpec=="daggers"&&scene.Blade(a,true,out var lroot,out var ltip))AirTear(sink,time+.37f,a.Id+7,w*dim*.7f,ltip,lroot,edge,core);
            }
            for(int i=bursts.Count-1;i>=0;i--)if(time-bursts[i].At>=Life||time<bursts[i].At-.25f)bursts.RemoveAt(i);
            for(int i=flashes.Count-1;i>=0;i--)if(time-flashes[i].At>=HitLife||time<flashes[i].At-.25f)flashes.RemoveAt(i);
            foreach(var b in bursts){if(b.Pull)PullOut(sink,b,time-b.At,quality,low,scene);else Impact(sink,b,time-b.At,quality,low,scene,dim);}
            foreach(var h in flashes)Hit(sink,h,time-h.At,quality,low,scene,dim);
        }
        /// <summary>Drop streak k: trailing up off the blade's point (two), the shoulders and the knees, sliding up as the drop
        /// carries on and growing with the fall; `head` its lowest point, `trail` up along it.</summary>
        public static bool DropStreak(int k,int seed,float fall,Vector3 feet,Vector3 forward,Vector3 right,Vector3 tip,out Vector3 head,out Vector3 trail,out float alpha)
        {
            head=feet;trail=Vector3.Zero;alpha=0;if(k<0||k>=Streaks)return false;
            float off=Hash(seed,k),u=(float)((Math.Max(0,fall)/.16f+off)%1.0),grow=.45f+1.1f*MeleeMath.Ease(Math.Max(0,fall)/.2f);
            Vector3 at=k<2?tip+right*(k==0?.07f:-.07f)+forward*.03f:k<4?feet+right*(k==2?.3f:-.3f)+new Vector3(0,1.3f,0)-forward*.05f:feet+right*(k==4?.18f:-.18f)+new Vector3(0,.95f,0)+forward*.25f;
            head=at+new Vector3(0,.25f*u,0);trail=new Vector3(0,grow*(.65f+.35f*(1-u)),0);alpha=(float)Math.Sin(Math.PI*u)*(.6f+.4f*Hash(seed,k+9));return alpha>1e-3f;
        }
        private static void DropStreaks(IKitFxSink sink,int n,int seed,float fall,float weight,Vector3 feet,Vector3 f,Vector3 right,Vector3 tip,Vector3 edge)
        {
            if(weight<=1e-3f)return;
            for(int k=0;k<n;k++){int j=n<Streaks?k*Streaks/n:k;if(DropStreak(j,seed,fall,feet,f,right,tip,out var head,out var trail,out float a))sink.Streak(head,-trail,edge*(2f*a*weight),j<2?.03f:.024f,trail.Length(),1,.92f,a,seed+j);}
        }
        /// <summary>The air-tear at the blade's point: a hot core in a flickering accent ring, and a bow shock of short streaks
        /// swept back up from it.</summary>
        private static void AirTear(IKitFxSink sink,float time,int seed,float weight,Vector3 tip,Vector3 root,Vector3 edge,Vector3 core)
        {
            if(weight<=1e-3f)return;float flick=.75f+.25f*(float)Math.Sin(time*95+seed);
            sink.Glow(tip,core*(1.8f*weight*flick),0,.16f,Core,0,seed);sink.Glow(tip+new Vector3(0,.04f,0),edge*(1.6f*weight),0,.34f+.06f*flick,Ring,(time*7)%1,seed+1);
            Vector3 up=root-tip;float len=up.Length();up=len>1e-4f?up/len:Vector3.UnitY;Vector3 side=Vector3.Normalize(Vector3.Cross(up,Math.Abs(up.Y)<.9f?Vector3.UnitY:Vector3.UnitX)),side2=Vector3.Cross(up,side);
            for(int k=0;k<4;k++){float a=k*MeleeMath.TAU/4+time*3;Vector3 o=side*(float)Math.Cos(a)+side2*(float)Math.Sin(a);Vector3 head=tip+o*.05f+up*.02f;sink.Streak(head,-(up*.32f+o*.12f),edge*(1.4f*weight),.018f,.2f,1,.95f,0,seed+3+k);}
        }
        /// <summary>Impact spark k of n: thrown up and out from the contact (ballistic), its head and velocity.</summary>
        public static bool Spark(int k,int n,int seed,float age,float heavy,Vector3 point,out Vector3 head,out Vector3 velocity,out float alpha)
        {
            head=point;velocity=Vector3.Zero;alpha=0;float life=SparkLife*(.6f+.4f*Hash(seed,k+3));if(!(age>=0&&age<life)||n<=0)return false;
            float a=MeleeMath.TAU*(k+.5f*Hash(seed,k))/n,s=(3.5f+5.5f*heavy)*(.5f+.5f*Hash(seed,k+5)),up=(2.5f+4f*heavy)*(.6f+.4f*Hash(seed,k+7));
            var v0=new Vector3((float)Math.Cos(a)*s,up,(float)Math.Sin(a)*s);velocity=v0+new Vector3(0,-23*age,0);
            head=point+v0*age+new Vector3(0,.05f-11.5f*age*age,0);if(head.Y<point.Y+.02f)return false;alpha=(1-age/life)*(1-age/life);return true;
        }
        /// <summary>Grit chunk k of n: a small piece of floor thrown up and out from the contact (ballistic, landing and
        /// settling), farther and higher for a bigger fall.</summary>
        public static bool Grit(int k,int n,int seed,float age,float heavy,Vector3 point,float floor,out Vector3 at,out float size)
        {
            at=point;size=0;if(!(age>=0&&age<GritLife)||n<=0)return false;
            float a=MeleeMath.TAU*(k+.6f*Hash(seed,k+31))/n,s=(1.2f+2.8f*heavy)*(.4f+.6f*Hash(seed,k+33)),up=(2.2f+3.3f*heavy)*(.5f+.5f*Hash(seed,k+35));
            float t=age,land=2*up/23;if(t>land)t=land;
            at=new Vector3(point.X+(float)Math.Cos(a)*s*t,floor+.03f+up*t-11.5f*t*t,point.Z+(float)Math.Sin(a)*s*t);size=(.05f+.05f*Hash(seed,k+37))*(1+.6f*heavy)*Math.Min(1,(GritLife-age)/.15f);return true;
        }
        public const float GritLife=.9f;public const int GritBits=12,ReducedGritBits=5;public const int GritColor=0x5e5a55;
        /// <summary>Crack k of n: a streak running out over the floor from the contact, fast then stopping (CrackReach by height).</summary>
        public static bool Crack(int k,int n,int seed,float age,float radius,float heavy,Vector3 point,out Vector3 head,out Vector3 dir,out float length,out float alpha)
        {
            head=point;dir=Vector3.UnitX;length=alpha=0;float u=age/CrackLife;if(!(u>=0&&u<1)||n<=0)return false;
            float a=k*2.39996323f+Hash(seed,k)*.7f;dir=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));
            float reach=CrackReach(radius,heavy)*(.6f+.4f*Hash(seed,k+17)),run=reach*Out(Math.Min(1,u*2.6f));
            head=point+dir*run+new Vector3(0,NativeAirAttackFx.FloorClear,0);length=Math.Max(.05f,run);alpha=(1-u)*(1-u);return true;
        }
        /// <summary>Dust wave puff k of n: round the feet, rolling out low over the floor to DustReach (by height), growing and fading.</summary>
        public static bool Wave(int k,int n,int seed,float age,float radius,float heavy,Vector3 feet,float floor,out Vector3 at,out float size,out float opacity)
        {
            at=feet;size=opacity=0;float u=age/DustLife;if(!(u>=0&&u<1)||n<=0)return false;
            float a=MeleeMath.TAU*k/n+Hash(seed,k+5)*.4f,r=(.2f+DustReach(radius,heavy)*Out(u))*(.88f+.24f*Hash(seed,k+9));
            at=new Vector3(feet.X+(float)Math.Cos(a)*r,floor+.1f+(.12f+.2f*heavy)*Out(u),feet.Z+(float)Math.Sin(a)*r);
            size=(.28f+.22f*heavy)+(.35f+.3f*heavy)*Out(u);opacity=.55f*(float)Math.Pow(1-u,1.3)*Math.Min(1,age/.03f)*(.7f+.3f*heavy);return true;
        }
        /// <summary>The ring of light out to the rule's ring radius: mote k of n just over the floor, spreading from the feet.</summary>
        public static bool RingMote(int k,int n,float age,float radius,Vector3 feet,float floor,out Vector3 at,out float size,out float alpha)
        {
            at=feet;size=alpha=0;float u=age/RingLife;if(!(u>=0&&u<1)||n<=0)return false;
            float a=MeleeMath.TAU*k/n,r=radius*(.15f+.85f*Out(u));at=new Vector3(feet.X+(float)Math.Sin(a)*r,floor+.06f,feet.Z+(float)Math.Cos(a)*r);size=.24f-.08f*u;alpha=(float)Math.Pow(1-u,1.3);return true;
        }
        private void Impact(IKitFxSink sink,in Burst b,float age,float quality,bool low,IKitFxScene scene,float dim)
        {
            if(age<0)return;Vector3 edge=Lin(Edge(b.Spec)),core=NativeMeleeKitFx.CoreOf(b.Spec),dust=Lin(NativeMeleeKitFx.Dust);float h=b.Heavy;
            var point=new Vector3(b.Point.X,b.Floor,b.Point.Z);var feet=new Vector3(b.Feet.X,b.Floor,b.Feet.Z);
            if(age<FlashLife){float u=age/FlashLife;sink.Glow(point+new Vector3(0,.12f,0),core*((1.6f+1.4f*h)*(1-u)*(1-u)*dim),0,(.5f+1.1f*h)*(.7f+.3f*u),Core,u,b.Seed);}
            if(age<ShockLife){float u=age/ShockLife;sink.Glow(point+new Vector3(0,.15f,0),edge*(2.2f*(float)Math.Pow(1-u,1.4)*dim),0,.3f+(1.1f+1.5f*h)*Out(u),Ring,u,b.Seed+1);}
            int motes=Count(RingMotes,ReducedRingMotes,quality,low);
            for(int k=0;k<motes;k++)if(RingMote(k,motes,age,b.Radius,feet,b.Floor,out var at,out float size,out float a))sink.Glow(at,edge*(2f*a*dim),0,size,Mote,age/RingLife,b.Seed*7+k);
            int cracks=Count(Cracks,ReducedCracks,quality,low);
            for(int k=0;k<cracks;k++)if(Crack(k,cracks,b.Seed,age,b.Radius,h,point,out var head,out var dir,out float len,out float a))sink.Streak(head,dir*len,edge*(1.9f*a*dim),.035f+.015f*h,len,1,.65f,age/CrackLife,b.Seed*29+k);
            int sparks=Count(Sparks,ReducedSparks,quality,low);var hot=b.Spec=="greatsword"?Lin(NativeMeleeKitFx.Ember,2.2f):core*1.8f;
            for(int k=0;k<sparks;k++)if(Spark(k,sparks,b.Seed,age,h,point,out var head,out var v,out float a))sink.Streak(head,v,hot*(a*dim),.022f,.08f,.035f,.85f,age/SparkLife,b.Seed*13+k);
            int grit=Count(GritBits,ReducedGritBits,quality,low);var gritTone=Lin(GritColor);
            for(int k=0;k<grit;k++)if(Grit(k,grit,b.Seed,age,h,point,b.Floor,out var at,out float size))sink.Smoke(at,gritTone,.95f,size,Math.Min(.6f,age/GritLife),b.Seed*19+k,b.Floor-.2f);
            int puffs=Count(Dust,ReducedDust,quality,low);
            for(int k=0;k<puffs;k++)if(Wave(k,puffs,b.Seed,age,b.Radius,h,feet,b.Floor,out var at,out float size,out float op))sink.Smoke(at,dust,op,size,age/DustLife,b.Seed*17+k,b.Floor);
        }
        private static int Edge(string spec)=>spec=="daggers"?NativeMeleeKitFx.DaggerEdge:spec=="shield"?NativeMeleeKitFx.ShieldEdge:spec=="greatsword"?NativeMeleeKitFx.GreatEdge:NativeMeleeKitFx.SwordEdge;
        private static void PullOut(IKitFxSink sink,in Burst b,float age,float quality,bool low,IKitFxScene scene)
        {
            if(age<0||age>=PullLife)return;float u=age/PullLife;var dust=Lin(NativeMeleeKitFx.Dust);var point=new Vector3(b.Point.X,b.Floor,b.Point.Z);int n=Count(PullPuffs,ReducedPullPuffs,quality,low);
            for(int k=0;k<n;k++){float a=MeleeMath.TAU*k/n+Hash(b.Seed,k)*.8f,r=.08f+.3f*Out(u);var at=point+new Vector3((float)Math.Cos(a)*r,.08f+.35f*Out(u)*(.6f+.4f*Hash(b.Seed,k+3)),(float)Math.Sin(a)*r);
                sink.Smoke(at,dust,.45f*(float)Math.Pow(1-u,1.4)*Math.Min(1,age/.03f),.16f+.22f*Out(u),u,b.Seed*11+k,b.Floor);}
        }
        private static void Hit(IKitFxSink sink,in Flash f,float age,float quality,bool low,IKitFxScene scene,float dim)
        {
            if(age<0||age>=HitLife)return;float u=age/HitLife;Vector3 at=f.Point;
            var actors=scene.Actors;for(int i=0;i<actors.Count;i++){var a=actors[i];if(a!=null&&a.Id==f.Target&&scene.Chest(a,out var chest)){at=chest;break;}}
            Vector3 edge=Lin(Edge(f.Spec)),core=NativeMeleeKitFx.CoreOf(f.Spec);
            sink.Glow(at,core*(3f*(1-u)*dim),0,.55f+.35f*u,Core,u,f.Seed);sink.Glow(at,edge*(2.2f*(float)Math.Pow(1-u,1.3)*dim),0,.25f+.9f*Out(u),Ring,u,f.Seed+1);
            int n=Count(HitSparks,ReducedHitSparks,quality,low);
            for(int k=0;k<n;k++)
            {
                float a=MeleeMath.TAU*(k+.4f*Hash(f.Seed,k))/n,s=4+4*Hash(f.Seed,k+5);var v=new Vector3((float)Math.Cos(a)*s,2.5f+3f*Hash(f.Seed,k+7),(float)Math.Sin(a)*s);
                sink.Streak(at+v*age+new Vector3(0,-11.5f*age*age,0),v+new Vector3(0,-23*age,0),core*(1.6f*(1-u)*dim),.02f,.06f,.03f,.85f,u,f.Seed*5+k);
            }
        }
    }
}
