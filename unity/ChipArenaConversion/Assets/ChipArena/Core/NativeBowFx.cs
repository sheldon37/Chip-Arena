using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The bow effects' sink: the kit primitives plus the arrow model (its nock, unit direction, opacity; view: the
    /// first-person layer; standard: the pilot's skin) on the shared effects renderer, the weapons drawer and the source bank.</summary>
    public interface IBowFxSink:IKitFxSink
    {
        void Arrow(Vector3 nock,Vector3 dir,float opacity,bool view,bool standard);
    }
    /// <summary>A bow arrow in flight as the presentation sees it (Unity world): the replicated shot's place and velocity, its
    /// owner and birth, its draw fraction and kind (NativeBowWire: from its shot event, else its launch speed).</summary>
    public struct NativeBowArrowView{public int Id,Owner;public Vector3 Position,Velocity;public float Born,Draw;public NativeBow.Arrow Kind;public bool Standard;}
    /// <summary>What the bow effects read of the presented frame.</summary>
    public interface IBowFxScene
    {
        IReadOnlyList<Actor> Actors{get;}
        /// <summary>The first-person viewer's id (0: none).</summary>
        int Viewer{get;}
        int Arrows{get;}
        NativeBowArrowView ArrowAt(int i);
        /// <summary>A bow pilot's posed bow-hand frame (the left hand; view: the first-person view bow).</summary>
        bool Bow(Actor a,bool view,out GunFrame frame);
        /// <summary>Whether `a` draws the Standard bow (else the Prototype Series).</summary>
        bool Standard(Actor a);
        /// <summary>Whether the simulation's own rain arrows were seen for this cast (then they fall as shots and the
        /// presentation draws no procedural fall).</summary>
        bool RainShots(Actor a,float rainAt);
    }

    /// <summary>
    /// The Bow's effects (Claude's presentation of the rules proposed in docs/NEW-KITS.md section 2), each a pure function of
    /// the replicated bow (NativeBowWire.Of), its arrows in flight, the posed bow and the presentation clock, seeded by cast and
    /// shot times: the arrow model on every shot with a trail that brightens with the draw (a full-draw shot burns brighter and
    /// carries a glint for its first moments), the full-draw glint on the nocked arrowhead (with a star when the draw locks),
    /// the string's snap, Volley's five-arrow fan streak, and Rain of Arrows: the aim marker ring over the target, the sky
    /// volley streaking up, twelve arrows falling into the circle (when the simulation does not fall its own), their ground
    /// hits (dust, a spark, a stuck arrow) and the slow tint on pilots the rain hits. Effects quality (Reduced, .3) thins them;
    /// the low graphics tier halves the particles and drops the glints' sparkle. Nothing here reaches the authority.
    /// </summary>
    public static class NativeBowFx
    {
        public const int Trail=0xb9ffd4,TrailFull=0x7fd36b,TrailCore=0xf2fff6,Glint=0xf6fff0,Marker=0x7fd36b,MarkerCore=0xd9ffe4,Dust=0x8a8f99,Spark=0xfff2cf,Slow=0x6fe3c8;
        public const float TrailLength=.9f,GlintLife=.18f,FanLife=.16f,SkyLife=.34f,FallSpeed=38,FallLead=.55f,GroundLife=3f,DustLife=.6f,SlowFade=.25f;
        public static Vector3 Lin(int hex,float gain=1)=>NativeMeleeKitFx.Lin(hex,gain);
        private static float H(long seed,long k)=>(float)NativeWeaponFx.Hash(seed,k);
        private static int Count(int full,float q,bool low){float n=full*Math.Max(.3f,Math.Min(1,q));if(low)n*=.5f;return Math.Max(1,(int)Math.Round(n));}

        /// <summary>Draws every live bow effect of the frame (not the stuck arrows: NativeBowStuck).</summary>
        public static void Draw(IBowFxSink sink,IBowFxScene scene,float time,float quality,bool low)
        {
            if(sink==null||scene==null)return;
            for(int i=0;i<scene.Arrows;i++)InFlight(sink,scene.ArrowAt(i),time,quality,low);
            var actors=scene.Actors;if(actors==null)return;int viewer=scene.Viewer;
            for(int i=0;i<actors.Count;i++)
            {
                var a=actors[i];if(!NativeBowWire.Is(a))continue;var r=NativeBowWire.Of(a);var v=NativeBow.At(r,time);bool view=viewer!=0&&a.Id==viewer;
                if(a.Alive&&scene.Bow(a,view,out var f))Held(sink,a,r,v,f,view,time,quality,low);
                if(r.RainAt>0&&time>=r.RainAt&&time<r.RainAt+NativeBow.RainDelay+NativeBow.RainSpan+GroundLife)Rain(sink,a,r,scene.RainShots(a,r.RainAt),time,quality,low);
            }
        }

        // ---- arrows in flight ----
        /// <summary>The trail's look for a shot of draw `draw`: its length (m), width, gain and colour; a full draw burns
        /// brighter, longer and greener.</summary>
        public static void TrailLook(float draw,NativeBow.Arrow kind,out float length,out float width,out float gain,out Vector3 color)
        {
            draw=NativeBow.Clamp01(draw);bool full=kind==NativeBow.Arrow.Full||draw>=.999f;
            length=TrailLength*(.45f+.75f*draw)*(kind==NativeBow.Arrow.Rain?.7f:1);width=.012f+.012f*draw;gain=.5f+1.6f*draw*draw+(full?.7f:0);
            color=Vector3.Lerp(Lin(Trail),Lin(TrailFull),full?1:draw*.6f);
        }
        public static void InFlight(IBowFxSink sink,in NativeBowArrowView w,float time,float quality,bool low)
        {
            float speed=w.Velocity.Length();if(!(speed>.5f))return;var dir=w.Velocity/speed;
            // The model: its head at the shot's place (the replicated projectile), the shaft back along the flight.
            sink.Arrow(w.Position-dir*NativeBow.ArrowLength,dir,1,false,w.Standard);
            TrailLook(w.Draw,w.Kind,out float length,out float width,out float gain,out var color);float age=Math.Max(0,time-w.Born);
            float grow=NativeBow.Clamp01(age*speed/Math.Max(.2f,length));var tail=w.Position-dir*(NativeBow.ArrowLength*.85f);
            sink.Line(false,tail-dir*(length*grow),tail,color*gain,width,false);
            if(!low&&quality>.5f)sink.Line(false,tail-dir*(length*.45f*grow),tail,Lin(TrailCore,gain*.6f),width*.35f,false);
            // A full-draw shot's glint for its first moments, at the head.
            bool full=w.Kind==NativeBow.Arrow.Full||w.Draw>=.999f;
            if(full&&age<GlintLife){float g=1-age/GlintLife;sink.Sprite(false,w.Position,Lin(Glint,2.2f*g),.16f*g+.05f,low?NativeMeleeKitFx.Soft:NativeMeleeKitFx.Sparkle);}
        }

        // ---- the held bow: the full-draw glint, the string's snap, Volley's fan streak, the sky volley ----
        private static void Held(IBowFxSink sink,Actor a,in NativeBow.Read r,in NativeBow.View v,in GunFrame f,bool view,float time,float quality,bool low)
        {
            float sc=f.Scale;long seed=NativeWeaponFx.Seed(r.DrawAt>0?r.DrawAt:r.ReleaseAt,a.Id);
            if(v.Phase==NativeBow.Phase.Hold&&v.Nocked)
            {
                // The nocked arrowhead glints at full draw: a star as the draw locks, then a slow pulse (faster when tiring).
                var head=f.At(NativeBow.ArrowX,NativeBow.ArrowY,NativeBow.FullZ+NativeBow.ArrowLength);float held=v.Held;
                float lockStar=held<.2f?1-held/.2f:0,pulse=.55f+.45f*(float)Math.Sin(time*(6+10*v.Tremor)+H(seed,1)*6);
                if(lockStar>0)sink.Sprite(view,head,Lin(Glint,(view?1.6f:2.6f)*lockStar),(.22f*lockStar+.05f)*sc,low?NativeMeleeKitFx.Soft:NativeMeleeKitFx.Sparkle);
                sink.Sprite(view,head,Lin(TrailFull,(view?.7f:1.1f)*pulse),.07f*sc,NativeMeleeKitFx.Soft);
                if(!low&&quality>.5f)sink.Glow(head,Lin(TrailFull,.5f*pulse),.4f,.18f*sc,NativeMeleeKitFx.Mote,0,H(seed,2));
            }
            // The string's snap at the release: a thin flash along the string's line through the nock.
            if(v.Buzz>.5f)
            {
                float k=(v.Buzz-.5f)*2;var top=f.At(NativeBow.ArrowX,NativeBow.TipY,NativeBow.TipZ);var bottom=f.At(NativeBow.ArrowX,-NativeBow.TipY,NativeBow.TipZ);
                sink.Line(view,top,bottom,Lin(TrailCore,.9f*k),.006f*sc,false);
            }
            // Volley: five streaks fanning out of the rest (14 degrees in all) for an instant after the loose.
            if(r.VolleyAt>0)
            {
                float rel=Math.Max(r.VolleyAt,r.VolleyRelease),age=time-rel;
                if(age>=0&&age<FanLife)
                {
                    float k=1-age/FanLife;var rest=f.At(NativeBow.ArrowX,NativeBow.ArrowY,NativeBow.RiserZ+.04f);var fwd=f.Dir(0,0,1);var up=Vector3.UnitY;
                    for(int i=0;i<NativeBow.VolleyArrows;i++)
                    {
                        float ang=(i-(NativeBow.VolleyArrows-1)*.5f)*NativeBow.VolleyFan/(NativeBow.VolleyArrows-1)*(float)Math.PI/180;
                        var d=Vector3.Transform(fwd,Quaternion.CreateFromAxisAngle(up,ang));float reach=1.6f+age*NativeBow.VolleySpeed*.12f;
                        sink.Line(view,rest+d*(.05f+age*8),rest+d*reach,Lin(TrailFull,1.6f*k),(.02f+.01f*k)*sc,false);
                        if(!low)sink.Sprite(view,rest+d*reach,Lin(Glint,1.2f*k),.08f*sc,NativeMeleeKitFx.Sparkle);
                    }
                    sink.Sprite(view,rest,Lin(Glint,2f*k),.18f*k*sc+.04f,NativeMeleeKitFx.Hot);
                }
            }
            // Rain of Arrows: the sky volley streaking up out of the bow at the loose.
            if(r.RainAt>0)
            {
                float rel=r.RainRelease>r.RainAt?r.RainRelease:r.RainAt+NativeBow.RainPre,age=time-rel;
                if(age>=0&&age<SkyLife)
                {
                    float k=1-age/SkyLife;var rest=f.At(NativeBow.ArrowX,NativeBow.ArrowY,NativeBow.RiserZ+.04f);var fwd=f.Dir(0,0,1);int n=Count(6,quality,low);
                    for(int i=0;i<n;i++)
                    {
                        var d=Vector3.Normalize(fwd+new Vector3(H(seed,10+i)-.5f,H(seed,20+i)*.3f,H(seed,30+i)-.5f)*.18f);float head=2+age*55,len=1.2f+1.6f*k;
                        sink.Line(view,rest+d*Math.Max(0,head-len),rest+d*head,Lin(TrailFull,1.4f*k),.02f*sc,false);
                    }
                }
            }
        }

        // ---- Rain of Arrows over its target ----
        /// <summary>Rain arrow i of a cast: the time it lands, where (inside the circle) and the unit direction it falls along
        /// (steep, from the archer's side). Seeded by the cast and the pilot, so every peer agrees.</summary>
        public static void RainArrow(in NativeBow.Read r,int i,out float landAt,out Vector3 at,out Vector3 dir)
        {
            long seed=NativeWeaponFx.Seed(r.RainAt,r.Seed);float u=(i+.3f+.4f*H(seed,40+i))/NativeBow.RainArrows;
            landAt=r.RainAt+NativeBow.RainDelay+NativeBow.RainSpan*u;
            float ang=H(seed,50+i)*6.2831853f,rad=r.RainRadius*(float)Math.Sqrt(.05f+.95f*H(seed,60+i));
            at=r.RainTarget+new Vector3((float)Math.Cos(ang)*rad,0,(float)Math.Sin(ang)*rad);
            dir=Vector3.Normalize(new Vector3((H(seed,70+i)-.5f)*.35f,-1,(H(seed,80+i)-.5f)*.35f));
        }
        private static void Rain(IBowFxSink sink,Actor a,in NativeBow.Read r,bool simulated,float time,float quality,bool low)
        {
            if(!r.HasRainTarget)return;long seed=NativeWeaponFx.Seed(r.RainAt,a.Id);float fall=r.RainAt+NativeBow.RainDelay,end=fall+NativeBow.RainSpan;
            // The marker: a ring traced round the circle over the delay, a soft inner disc pulsing, held through the fall.
            float age=time-r.RainAt,fade=time<end?1:1-NativeBow.Clamp01((time-end)/.25f);
            if(fade>0)
            {
                float draw=NativeBow.Clamp01(age/.18f),pulse=.6f+.4f*(float)Math.Sin(age*14),warn=time<fall?1:.6f;int segs=low?24:40;var c=r.RainTarget+new Vector3(0,.04f,0);
                Ring(sink,c,r.RainRadius,.07f,Lin(Marker,1.4f*fade*warn),segs,draw,age*.8f);
                Ring(sink,c,r.RainRadius*.97f,.025f,Lin(MarkerCore,1.2f*fade*pulse),segs,draw,-age*1.3f);
                if(time<fall){float k=NativeBow.Clamp01((fall-time)/NativeBow.RainDelay);Ring(sink,c,r.RainRadius*(.15f+.85f*k),.03f,Lin(Marker,.9f*fade),segs/2,1,0);}
                if(!low)for(int s=0;s<4;s++){float ang=s*1.5707963f+age*.8f;var p=c+new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang))*r.RainRadius;sink.Sprite(false,p,Lin(MarkerCore,1.2f*fade),.12f,NativeMeleeKitFx.Sparkle);}
            }
            int n=low?NativeBow.RainArrows/2:NativeBow.RainArrows;
            for(int i=0;i<NativeBow.RainArrows;i++)
            {
                if(low&&i%2==1)continue;RainArrow(r,i,out float land,out var at,out var dir);float until=land-time;
                // Falling: the arrow and its streak over the last FallLead seconds (skipped when the simulation falls its own).
                if(!simulated&&until>0&&until<FallLead)
                {
                    var head=at-dir*(until*FallSpeed);sink.Arrow(head-dir*NativeBow.ArrowLength,dir,1,false,true);
                    sink.Line(false,head-dir*(NativeBow.ArrowLength+1.4f),head-dir*NativeBow.ArrowLength*.8f,Lin(TrailFull,1.3f),.016f,false);
                }
                // The ground hit: dust, a spark, the arrow left standing in the ground.
                float since=time-land;
                if(since>=0&&since<DustLife)
                {
                    float k=1-since/DustLife;int puffs=Count(3,quality,low);
                    for(int p=0;p<puffs;p++)sink.Smoke(at+new Vector3((H(seed,90+i*5+p)-.5f)*.3f,.05f+since*.4f,(H(seed,95+i*5+p)-.5f)*.3f),Lin(Dust),.35f*k,.12f+.35f*since,since/DustLife,H(seed,100+i*5+p)*10,r.RainTarget.Y);
                    if(since<.08f)sink.Sprite(false,at+new Vector3(0,.04f,0),Lin(Spark,2.4f*(1-since/.08f)),.22f,NativeMeleeKitFx.Hot);
                }
                if(!simulated&&since>=0&&since<GroundLife){float op=since>GroundLife-.4f?(GroundLife-since)/.4f:1;var head=at+dir*.16f;sink.Arrow(head-dir*NativeBow.ArrowLength,dir,op,false,true);}
            }
        }
        /// <summary>A flat ring of `segments` band quads (radius, width), traced `draw` of the way round, turned by `spin`.</summary>
        public static void Ring(IKitFxSink sink,Vector3 c,float radius,float width,Vector3 color,int segments,float draw,float spin)
        {
            int shown=(int)Math.Ceiling(segments*NativeBow.Clamp01(draw));
            for(int s=0;s<shown;s++)
            {
                float a0=spin+s*6.2831853f/segments,a1=spin+(s+1)*6.2831853f/segments;var d0=new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0));var d1=new Vector3((float)Math.Cos(a1),0,(float)Math.Sin(a1));
                sink.Band(false,c+d0*(radius-width),c+d1*(radius-width),c+d1*(radius+width),c+d0*(radius+width),0,0,color);
            }
        }

        // ---- the slow tint on pilots the rain hits ----
        /// <summary>The slow tint's weight on a pilot hit by a rain arrow at `hitAt` (1.5 s, the rule's slow; faded in and out).</summary>
        public static float SlowWeight(float hitAt,float time)
        {
            float age=time-hitAt;if(!(age>=0&&age<NativeBow.RainSlowSeconds))return 0;
            return NativeBow.Smooth(age/.08f)*(1-NativeBow.Smooth((age-(NativeBow.RainSlowSeconds-SlowFade))/SlowFade));
        }
        /// <summary>Whether a replicated hit is a rain arrow's: by a bow pilot inside the circle during the fall.</summary>
        public static bool RainHit(in NativeBow.Read r,Vector3 point,float at)
        {
            if(!r.HasRainTarget||r.RainAt<=0)return false;float fall=r.RainAt+NativeBow.RainDelay-.05f;if(at<fall||at>fall+NativeBow.RainSpan+.15f)return false;
            float dx=point.X-r.RainTarget.X,dz=point.Z-r.RainTarget.Z;return dx*dx+dz*dz<=(r.RainRadius+.6f)*(r.RainRadius+.6f);
        }
        /// <summary>The slow tint round a pilot (feet `feet`, Unity world): a cold ring at the feet and motes sinking round the body.</summary>
        public static void SlowTint(IBowFxSink sink,Vector3 feet,float w,float time,int id,float quality,bool low)
        {
            if(w<=0)return;Ring(sink,feet+new Vector3(0,.05f,0),.55f,.04f,Lin(Slow,1.2f*w),low?16:28,1,time*1.5f);
            int motes=Count(8,quality,low);long seed=id*7919L;
            for(int i=0;i<motes;i++)
            {
                float ph=(time*.9f+H(seed,i))%1f,ang=H(seed,20+i)*6.2831853f+time*.6f;var p=feet+new Vector3((float)Math.Cos(ang)*.38f,1.7f-1.5f*ph,(float)Math.Sin(ang)*.38f);
                sink.Glow(p,Lin(Slow,.9f*w*(1-ph)),.5f,.06f,NativeMeleeKitFx.Mote,ph,H(seed,40+i)*10);
            }
        }
    }

    /// <summary>
    /// Arrows that stick (cosmetic, from replicated hit points): in a wall or the floor for six seconds (faded over the last
    /// .4 s), in a pilot until it respawns or the round ends, riding the struck body part's joint (the live rig, the corpse
    /// after a death, a severed part's own joint), its head buried along the flight. A fixed pool (no allocation per frame):
    /// at most PerPilot in one pilot and Capacity in all (the oldest go first); the low graphics tier keeps fewer.
    /// </summary>
    public sealed class NativeBowStuck
    {
        public const int Capacity=128,PerPilot=12,LowCapacity=48;public const float WallLife=6,FadeOut=.4f,Bury=.14f;
        public enum Kind:byte{None,Wall,Pilot}
        public struct Record{public Kind Kind;public int Event,Target,Bone;public Vector3 Point,Dir;public float Born,Draw;public bool Standard;}
        private readonly Record[] records=new Record[Capacity];public int Count{get;private set;}
        public Record this[int i]=>records[i];
        private int Find(int evt){for(int i=0;i<Count;i++)if(records[i].Event==evt)return i;return -1;}
        private int Slot(int target,bool low)
        {
            int cap=low?LowCapacity:Capacity;
            if(target!=0){int n=0,oldest=-1;for(int i=0;i<Count;i++)if(records[i].Kind==Kind.Pilot&&records[i].Target==target){n++;if(oldest<0||records[i].Born<records[oldest].Born)oldest=i;}if(n>=PerPilot)return oldest;}
            if(Count<cap)return Count++;
            int o=0;for(int i=1;i<Count;i++)if(records[i].Born<records[o].Born)o=i;return o;
        }
        /// <summary>An arrow in a wall or the floor at `point` along `dir` (world; once per event id).</summary>
        public void AddWall(int evt,Vector3 point,Vector3 dir,float time,float draw,bool standard,bool low=false)
        {
            if(Find(evt)>=0)return;int s=Slot(0,low);records[s]=new Record{Kind=Kind.Wall,Event=evt,Point=point,Dir=Unit(dir),Born=time,Draw=draw,Standard=standard};
        }
        /// <summary>An arrow in pilot `target`'s joint `bone`: `local` the hit point and `localDir` the flight in that joint's frame.</summary>
        public void AddPilot(int evt,int target,int bone,Vector3 local,Vector3 localDir,float time,float draw,bool standard,bool low=false)
        {
            if(Find(evt)>=0)return;int s=Slot(target,low);records[s]=new Record{Kind=Kind.Pilot,Event=evt,Target=target,Bone=bone,Point=local,Dir=Unit(localDir),Born=time,Draw=draw,Standard=standard};
        }
        private static Vector3 Unit(Vector3 v){float l=v.Length();return l>1e-6f?v/l:Vector3.UnitZ;}
        /// <summary>Drops the expired wall arrows (and any from the future: a rewind); keeps the pilots'.</summary>
        public void Update(float time)
        {
            for(int i=0;i<Count;)
            {
                var r=records[i];bool gone=r.Kind==Kind.Wall&&(time-r.Born>=WallLife)||time<r.Born-.5f;
                if(gone){records[i]=records[--Count];records[Count]=default;}else i++;
            }
        }
        /// <summary>The arrows in one pilot go (it respawned or was revived).</summary>
        public void ClearPilot(int target){for(int i=0;i<Count;)if(records[i].Kind==Kind.Pilot&&records[i].Target==target){records[i]=records[--Count];records[Count]=default;}else i++;}
        /// <summary>Everything goes (the round ended, a new arena).</summary>
        public void Clear(){for(int i=0;i<Count;i++)records[i]=default;Count=0;}
        public int Pilot(int target){int n=0;for(int i=0;i<Count;i++)if(records[i].Kind==Kind.Pilot&&records[i].Target==target)n++;return n;}
        /// <summary>The opacity a record shows at `time` (walls fade over their last FadeOut).</summary>
        public static float Opacity(in Record r,float time)=>r.Kind==Kind.Wall?NativeBow.Clamp01((WallLife-(time-r.Born))/FadeOut):1;
        /// <summary>Resolves a pilot's joint to its world matrix (Unity), from the live rig or its corpse; false hides the arrow.</summary>
        public delegate bool BoneOf(int target,int bone,out Matrix4x4 world);
        /// <summary>Draws every stuck arrow: walls in the world, pilots' through `bone` (the head buried Bury along the flight).</summary>
        public void Draw(IBowFxSink sink,float time,BoneOf bone)
        {
            for(int i=0;i<Count;i++)
            {
                var r=records[i];Vector3 point=r.Point,dir=r.Dir;
                if(r.Kind==Kind.Pilot){if(bone==null||!bone(r.Target,r.Bone,out var m))continue;point=Vector3.Transform(r.Point,m);dir=Unit(Vector3.TransformNormal(r.Dir,m));}
                var head=point+dir*Bury;sink.Arrow(head-dir*NativeBow.ArrowLength,dir,Opacity(r,time),false,r.Standard);
            }
        }
        /// <summary>The joint nearest a hit point among a posed rig's joints (Unity world matrices) that can carry an arrow: the
        /// spine, chest, head, arms and legs (the body boxes' joints), by distance to each joint's segment midpoint.</summary>
        public static readonly int[] Carriers={CapturedRig.Pelvis,CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Head,CapturedRig.LArm,CapturedRig.LElbow,CapturedRig.RArm,CapturedRig.RElbow,CapturedRig.LHip,CapturedRig.LKnee,CapturedRig.RHip,CapturedRig.RKnee};
        public static int Nearest(Func<int,Matrix4x4> joint,Vector3 point)
        {
            int best=CapturedRig.Chest;float d=float.MaxValue;
            foreach(int b in Carriers)
            {
                var m=joint(b);var o=Vector3.Transform(Vector3.Zero,m);var mid=b==CapturedRig.Head?Vector3.Transform(new Vector3(0,.165f,0),m):b==CapturedRig.Chest?Vector3.Transform(new Vector3(0,.12f,0),m):b==CapturedRig.Pelvis?o:Vector3.Transform(new Vector3(0,-.13f,0),m);
                float dd=Vector3.DistanceSquared(mid,point);if(dd<d){d=dd;best=b;}
            }
            return best;
        }
    }
}
