using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Where the Cyber Lord's effects draw (Runtime: NativeCyberLordPresentation; the render harness and Tests record it).
    /// Glow/Streak/Smoke: the shared effects renderer (NativeVfxDraw: world, sorted, soft). Sprite/Line/Band: the weapons drawer
    /// (NativeWeaponFxDraw), with `view` on the first-person view-model camera. Voxel: an unlit glowing primitive (the weapon
    /// pieces, NativeSourceModels.QueuePiece), the hardlight constructs. Positions are Unity world metres; colours linear.
    /// </summary>
    public interface ICyberFxSink
    {
        void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed);
        void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed);
        void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor);
        void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile);
        void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard);
        void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color);
        void Voxel(bool view,WeaponShape shape,Vector3 at,Quaternion rotation,Vector3 scale,Vector3 color);
    }
    /// <summary>What the effects read of the presented frame (allocation-free once bound).</summary>
    public interface ICyberFxScene
    {
        IReadOnlyList<Actor> Actors{get;}
        /// <summary>The drawn feet (the smoothed root the rig stands on).</summary>
        Vector3 Feet(Actor a);
        bool Chest(Actor a,out Vector3 chest);
        /// <summary>A posed fist's centre (third person), or the first-person viewer's view fist.</summary>
        bool Fist(Actor a,bool left,out Vector3 fist);
        /// <summary>The local first-person pilot (0: none): its own body effects go to the view layer.</summary>
        int Viewer{get;}
        /// <summary>The local pilot whatever the camera (the Kernel Panic participants' presentation clock is theirs only).</summary>
        int Local{get;}
        bool ViewFist(bool left,out Vector3 fist);
        Vector3 Eye{get;}
        float Floor(Vector3 at);
    }
    /// <summary>A silhouette the runtime draws as a see-through ghost of a pilot model (NativeSourceModels.Afterimage).</summary>
    public struct CyberGhost
    {
        /// <summary>Split: the actor's own posed rig offset by Offset (the glitched RGB split). Clone: a Kernel Panic fork-bomb
        /// clone of the caster at Position / Yaw, posed on the combo take `Step` at its strike age `PoseAge`.</summary>
        public const int Split=0,Clone=1;
        public int Kind,Actor,Step,Index;public Vector3 Offset,Position;public float Yaw,PoseAge,Opacity;public Vector3 Tint;
    }

    /// <summary>
    /// The Cyber Lord's world effects (owner: the hardlight brawler; docs/CYBER-LORD.md), each a pure function of what host,
    /// guests, spectators and the killcam all hold: every Cyber Lord's view (CyberLordFeed: combo, the six cast windows, the
    /// dash path and trail, the wall, the link, the crash field, the Kernel Panic sequence and its waypoints) and the cyber
    /// events (their Detail, CyberLordFeed.Hit), seeded by event id or cast time and aged on the presentation clock:
    /// the combo's hit sparks and the Pulse Palm's forward cone, the mana flowing into the gauntlets on a hit, the hardlight
    /// guard, the Data Dash's glitch streak and its lingering data trail, the Firewall's hex wall forming, absorbing (ripples)
    /// and burning (flicker) and collapsing, the Packet Burst's glitch shards, the Siphon Link's tether streaming packets home
    /// and its heal glint, System Crash's gathering slam, 4 m glitch field and the glitched stun on its victims, and Kernel
    /// Panic's lunge, launch streaks, glitch-teleport tears, hammer-fist trail, fork-bomb clones shattering into pixels, the
    /// falling code column and its shockwave. Effects quality (Reduced, .3) thins particles and never strobes; the low
    /// graphics tier halves sprites and voxel counts and drops smoke. The Kernel Panic participants (caster and victim, as
    /// the local pilot) see its effects on their own hit-stopped clock (KernelPanicFeel.Clock); everyone else on the match's.
    /// Nothing here reaches the authority, the network frame, hit timing, hitboxes, damage or movement.
    /// </summary>
    public sealed partial class NativeCyberLordFx
    {
        public struct Record{public int Id,Source,Target;public EventKind Kind;public CyberHit Hit;public float Time,Amount;public Vector3 Position,From;}
        private readonly List<Record> records=new List<Record>(MaxRecords);private int cursor,drainSource;private float clock=float.NegativeInfinity,drainAt=-100;
        public IReadOnlyList<Record> Records=>records;
        public const int MaxRecords=128;public const float RecordSeconds=4;
        public const int Mote=0,Core=1,Ring=2,Snow=3,Soft=0,Hot=1,RingSprite=2,Sparkle=3;
        public const int MaxGhosts=24;
        private readonly CyberGhost[] ghosts=new CyberGhost[MaxGhosts];private int ghostCount;
        /// <summary>This frame's silhouettes (glitched splits, fork-bomb clones), filled by Draw.</summary>
        public int Ghosts=>ghostCount;public CyberGhost Ghost(int i)=>ghosts[i];
        private readonly CyberLordView view=new CyberLordView(),other=new CyberLordView();
        public void Clear(){records.Clear();cursor=0;clock=float.NegativeInfinity;ghostCount=0;ClearBoosts();dashTrailCount=0;}
        /// <summary>Reads the fresh cyber events (idempotent per frame); a new arena or the killcam's replica (ids restart, the clock
        /// rewinds) starts over.</summary>
        public void Observe(IReadOnlyList<ArenaEvent> events,float time)
        {
            if(events==null)return;int newest=0;for(int i=0;i<events.Count;i++)if(events[i].Id>newest)newest=events[i].Id;
            if(newest>0&&newest<cursor||time<clock-.25f){records.Clear();cursor=0;drainSource=0;drainAt=-100;}
            clock=time;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];if(e.Id<=cursor||!CyberLordFeed.Kind(e.Kind))continue;var hit=CyberLordFeed.Hit(e.Detail);
                if(hit==CyberHit.Siphon&&CyberLordFeed.Strike(e.Kind)){drainSource=e.Source;drainAt=e.Time;}
                else if(hit==CyberHit.None&&CyberLordFeed.SiphonHeal(e.Kind,e.Detail,e.Source,e.Target,e.Time,drainSource,drainAt))hit=CyberHit.Siphon;
                if(hit==CyberHit.None||time-e.Time>RecordSeconds)continue;
                if(records.Count>=MaxRecords)records.RemoveAt(0);
                records.Add(new Record{Id=e.Id,Source=e.Source,Target=e.Target,Kind=e.Kind,Hit=hit,Time=e.Time,Amount=e.Amount,Position=N(e.Position),From=N(e.From)});
            }
            if(newest>cursor)cursor=newest;
            for(int i=records.Count-1;i>=0;i--)if(time-records[i].Time>RecordSeconds)records.RemoveAt(i);
        }
        private static Vector3 N(V3 v)=>new Vector3(v.x,v.y,v.z);

        // ---- palette ----
        public const int Cyan=NativeCyberLordLook.Cyan,White=NativeCyberLordLook.Core,Magenta=NativeCyberLordLook.Magenta,Deep=NativeCyberLordLook.Deep,Code=0x7dffcf,Burn=0xff4d6d,Heal=0x7dffb0;
        public static Vector3 Lin(int hex,float gain=1)=>NativeCyberLordLook.Lin(hex,gain);
        private static float H(long k,uint salt){unchecked{uint h=(uint)k*0x9e3779b1u^salt*0x85ebca6bu;h^=h>>15;h*=0x2c1b3c6du;h^=h>>12;h*=0x297a2d39u;h^=h>>15;return (h&0xffffff)/16777216f;}}
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        private static float Ramp(float t,float a,float b)=>Clamp01((t-a)/Math.Max(1e-4f,b-a));
        private static Vector3 Forward(float yawDegrees)=>KernelPanicPath.Forward(yawDegrees);
        private static Vector3 Right(Vector3 f)=>new Vector3(f.Z,0,-f.X);
        private static Vector3 V(V3 v)=>new Vector3(v.x,v.y,v.z);
        private static int Count(int full,float q,bool low){float n=full*Math.Max(.35f,Math.Min(1,q));if(low)n*=.5f;return Math.Max(1,(int)Math.Round(n));}
        private static Quaternion Yaw(float yawDegrees)=>Quaternion.CreateFromAxisAngle(Vector3.UnitY,yawDegrees*(float)Math.PI/180);
        private static readonly Vector3 Up=Vector3.UnitY;

        /// <summary>Draws every live Cyber Lord effect of the frame at `time`.</summary>
        public void Draw(ICyberFxSink sink,ICyberFxScene scene,float time,float quality,bool low)
        {
            ghostCount=0;if(sink==null||scene?.Actors==null)return;var actors=scene.Actors;bool reduced=quality<.5f;ExpireTrails(time);
            for(int i=0;i<actors.Count;i++)
            {
                var a=actors[i];if(!CyberLordFeed.Read(a,view))continue;
                // World effects that outlive the cast (any caster state): the trails, the wall, the field, the link.
                NoteTrail(view,time);
                Wall(sink,view,time,quality,low);
                Field(sink,view,time,quality,low);
                if(!a.Alive)continue;
                Link(sink,scene,a,view,time,quality,low);
                bool me=scene.Viewer==a.Id;
                if(time<a.BlockUntil)GuardFx(sink,scene,a,view,time,quality,low,me);
                Palm(sink,scene,a,view,time,quality,low,me);
                for(int s=0;s<CyberLordSpec.Four;s++){var p=view.Phase(s,time);if(p.Live)Cast(sink,scene,a,view,s,p,time,quality,low,me);}
                var op=view.Phase(CyberLordSpec.Four,time);if(op.Live||view.PanicOutcome==CyberLordSpec.Whiff&&time<op.End+.4f)Opener(sink,scene,a,view,op,time,quality,low,me);
                LungeTrail(sink,scene,a,view,time,quality,low);
                if(view.PanicLive(time)||view.PanicStart>-90&&time<view.PanicStart+KernelPanicPath.End+.6f&&time>=view.PanicConnect)
                {
                    float pt=KernelPanicFeel.Participant(view,scene.Local)?KernelPanicFeel.Clock(view,time,reduced):time;
                    Panic(sink,scene,a,view,pt,quality,low,me);
                }
            }
            for(int i=0;i<dashTrailCount;i++)Trail(sink,dashTrails[i],time,quality,low||TrailLow(i));
            for(int i=0;i<records.Count;i++)Event(sink,scene,records[i],time,quality,low);
            AirBoosts(sink,scene,time,quality,low);
            Glitched(scene,time,quality,low);
        }

        // ---------------------------------------------------------------------------------------------------------------
        // The combo: hit sparks (events), the Pulse Palm's cone (the palm's replicated hit), the mana flow into the gauntlets
        // ---------------------------------------------------------------------------------------------------------------
        public const float SparkSeconds=.32f,ManaFlowSeconds=.38f,PalmSeconds=.3f;
        private void Event(ICyberFxSink s,ICyberFxScene scene,in Record r,float time,float q,bool low)
        {
            float age=time-r.Time;if(age<0)return;long seed=r.Id*2654435761L;
            switch(r.Hit)
            {
                case CyberHit.Jab:case CyberHit.Cross:case CyberHit.Palm:
                    if(r.Kind!=EventKind.Hit&&r.Kind!=EventKind.AbilityHit)break;
                    if(age<SparkSeconds)HitSpark(s,r.Position,age,r.Hit==CyberHit.Palm?1.35f:r.Hit==CyberHit.Cross?1.1f:.9f,seed,q,low);
                    if(age<ManaFlowSeconds+.05f)ManaFlow(s,scene,r,age,seed,q,low);
                    break;
                case CyberHit.Dash:if(age<SparkSeconds&&(r.Kind==EventKind.Hit||r.Kind==EventKind.AbilityHit))GlitchSpark(s,r.Position,age,.9f,seed,q,low);break;
                case CyberHit.Burst:if(age<SparkSeconds&&(r.Kind==EventKind.Hit||r.Kind==EventKind.AbilityHit))GlitchSpark(s,r.Position,age,.55f,seed,q,low);break;
                case CyberHit.Firewall:
                    if(r.Kind==EventKind.Absorb){if(age<.6f)Ripple(s,scene,r,age,q,low);}
                    else if(age<.35f&&CyberLordFeed.Strike(r.Kind))BurnFlicker(s,r.Position,age,seed,q,low);
                    break;
                case CyberHit.Siphon:
                    if(r.Kind==EventKind.Heal){if(age<.55f)HealGlint(s,scene,r,age,seed,q,low);}
                    break;
                case CyberHit.Crash:if(age<SparkSeconds&&CyberLordFeed.Strike(r.Kind))GlitchSpark(s,r.Position,age,1.1f,seed,q,low);break;
                case CyberHit.PanicWave:if(age<SparkSeconds&&CyberLordFeed.Strike(r.Kind))GlitchSpark(s,r.Position,age,1.2f,seed,q,low);break;
                default:
                    // (Step 4's three hits land together on the host; the fork-bomb clones draw their own staggered sparks.)
                    int step=CyberLordFeed.PanicStep(r.Hit);if(step>0&&step!=4&&age<.5f&&CyberLordFeed.Strike(r.Kind))HitSpark(s,r.Position,age,step==5?2.1f:1.5f,seed,q,low);
                    break;
            }
        }
        /// <summary>A hardlight hit: a white core flash, a hex ring snapping out, streak sparks and pixel shards flying off.</summary>
        private static void HitSpark(ICyberFxSink s,Vector3 at,float age,float size,long seed,float q,bool low)
        {
            float u=age/SparkSeconds,fade=1-Smooth(u);
            s.Sprite(false,at,Lin(White,4.5f*fade*fade),.35f*size*(1+u),Hot);
            s.Sprite(false,at,Lin(Cyan,2.0f*fade),(.3f+.7f*Smooth(u*1.6f))*size,RingSprite);
            s.Glow(at,Lin(Cyan,.8f*fade),0,.55f*size,Core,u,seed);
            int n=Count(10,q,low);
            for(int i=0;i<n;i++)
            {
                float h1=H(seed+i,3),h2=H(seed+i,5),h3=H(seed+i,7);var d=Vector3.Normalize(new Vector3(h1-.5f,(h2-.35f)*.9f,h3-.5f)+new Vector3(0,.001f,0));
                float sp=(5+7*H(seed+i,11))*size;s.Streak(at+d*(sp*age*(1-.5f*u)),d*sp,Lin(i%3==0?Magenta:Cyan,2.6f*fade),.022f*size,.12f,.03f,.9f,u,seed+i);
            }
            int px=low?3:Count(7,q,low);
            for(int i=0;i<px;i++)
            {
                var d=Vector3.Normalize(new Vector3(H(seed+i,13)-.5f,H(seed+i,17)*.8f,H(seed+i,19)-.5f));float dist=(.25f+.6f*H(seed+i,23))*size*Smooth(u*1.5f);
                float k=.05f*size*(1-u);s.Voxel(false,WeaponShape.Cube,at+d*dist-Up*(1.5f*age*age),Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1,H(seed+i,29),.5f)),age*12),new Vector3(k,k,k*.4f),Lin(i%2==0?White:Cyan,2.2f));
            }
        }
        /// <summary>A glitch hit (the abilities): RGB-split sparks, square pixels and a short scan bar.</summary>
        private static void GlitchSpark(ICyberFxSink s,Vector3 at,float age,float size,long seed,float q,bool low)
        {
            float u=age/SparkSeconds,fade=1-Smooth(u);
            s.Sprite(false,at,Lin(Magenta,2.4f*fade),.3f*size,Hot);s.Sprite(false,at+new Vector3(.04f,0,0),Lin(Cyan,2.4f*fade),.3f*size,Hot);
            int n=Count(8,q,low);
            for(int i=0;i<n;i++)
            {
                var d=Vector3.Normalize(new Vector3(H(seed+i,3)-.5f,H(seed+i,5)-.3f,H(seed+i,7)-.5f));float dist=(.2f+.7f*H(seed+i,9))*size*Smooth(u*1.8f);
                float k=.045f*size*fade;int slot=(int)(age*30);if(q<.5f)slot=0;if(H(seed+i*31+slot,13)<.25f)continue;
                s.Voxel(false,WeaponShape.Cube,at+d*dist,Quaternion.Identity,new Vector3(k*1.6f,k,k*.3f),Lin(i%2==0?Magenta:Cyan,2.2f));
            }
            s.Band(false,at-new Vector3(.4f*size,0,0)+Up*.02f,at+new Vector3(.4f*size,0,0)+Up*.02f,at+new Vector3(.4f*size,.03f,0),at-new Vector3(.4f*size,-.03f,0),fade,fade,Lin(Cyan,1.6f));
        }
        /// <summary>The mana a hit restores (6) as an energy flow from the struck point into the striker's gauntlets.</summary>
        private static void ManaFlow(ICyberFxSink s,ICyberFxScene scene,in Record r,float age,long seed,float q,bool low)
        {
            Actor src=null;var actors=scene.Actors;for(int i=0;i<actors.Count;i++)if(actors[i]?.Id==r.Source){src=actors[i];break;}if(src==null||!src.Alive)return;
            bool view=scene.Viewer==src.Id;int n=Count(6,q,low);
            for(int i=0;i<n;i++)
            {
                bool left=i%2==0;Vector3 fist;if(!(view?scene.ViewFist(left,out fist):scene.Fist(src,left,out fist)))continue;
                float delay=.05f*i/n,u=Clamp01((age-delay)/ManaFlowSeconds);if(u<=0||u>=1)continue;
                var mid=Vector3.Lerp(r.Position,fist,.5f)+Up*(.25f+.2f*H(seed+i,3))+new Vector3(H(seed+i,5)-.5f,0,H(seed+i,7)-.5f)*.4f;
                float k=Smooth(u);var p=Vector3.Lerp(Vector3.Lerp(r.Position,mid,k),Vector3.Lerp(mid,fist,k),k);
                // The caster's own flow ends on its view fists (first person); the start of the arc stays in the world.
                bool onView=view&&u>.55f;s.Sprite(onView,p,Lin(i%3==0?White:Cyan,2.4f*(1-.3f*u)),onView?.035f:.09f,Hot);
                if(!low&&!onView)s.Streak(p,(Vector3.Lerp(mid,fist,k)-Vector3.Lerp(r.Position,mid,k))*3,Lin(Cyan,1.4f),.018f,.08f,.03f,.9f,u,seed+i);
            }
        }
        /// <summary>The Pulse Palm's forward cone (1.6 m) on the palm's replicated hit: hex rings driving out and a cone shell.</summary>
        private static void Palm(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,float time,float q,bool low,bool me)
        {
            if(v.ComboStep!=2||!(v.ComboHit>-90))return;float age=time-v.ComboHit;if(age<0||age>=PalmSeconds)return;
            float u=age/PalmSeconds,fade=1-Smooth(u);var f=Forward(v.Yaw);var r=Right(f);long seed=a.Id*7919L+(long)Math.Round(v.ComboHit*240);
            Vector3 origin;if(!(me?scene.ViewFist(false,out origin):scene.Fist(a,false,out origin)))origin=scene.Feet(a)+Up*1.3f+f*.6f;
            if(me)origin=scene.Feet(a)+Up*1.3f+f*.45f;
            for(int k=0;k<3;k++)
            {
                float d=CyberLordSpec.PalmCone*Clamp01(u*1.6f-k*.18f);if(d<=0)continue;float rad=.18f+d*.55f;var c=origin+f*d;float g=fade*(1-k*.25f);
                int n=low?6:12;
                for(int i=0;i<6;i++)
                {
                    float a0=i*(float)Math.PI/3,a1=a0+(float)Math.PI/3;var p0=c+r*((float)Math.Cos(a0)*rad)+Up*((float)Math.Sin(a0)*rad);var p1=c+r*((float)Math.Cos(a1)*rad)+Up*((float)Math.Sin(a1)*rad);
                    if(!me||d>.6f)s.Line(false,p0,p1,Lin(k==0?White:Cyan,2.6f*g),.03f,false);
                }
                if(n>6&&!me)s.Sprite(false,c,Lin(Cyan,.8f*g),rad*1.5f,RingSprite);
            }
            // The cone shell: a fan of soft bands from the palm out to 1.6 m.
            float reach=CyberLordSpec.PalmCone*Smooth(u*1.4f),end=.18f+reach*.55f;int fan=low?4:8;
            for(int i=0;i<fan;i++)
            {
                float a0=i*MeleeMath.TAU/fan,a1=(i+1)*MeleeMath.TAU/fan;Vector3 o0=origin+f*.1f+(r*(float)Math.Cos(a0)+Up*(float)Math.Sin(a0))*.12f,o1=origin+f*.1f+(r*(float)Math.Cos(a1)+Up*(float)Math.Sin(a1))*.12f;
                Vector3 e0=origin+f*reach+(r*(float)Math.Cos(a0)+Up*(float)Math.Sin(a0))*end,e1=origin+f*reach+(r*(float)Math.Cos(a1)+Up*(float)Math.Sin(a1))*end;
                if(!me)s.Band(false,o0,e0,e1,o1,.9f*fade,.25f*fade,Lin(Cyan,1.4f));
            }
            s.Glow(origin+f*(.3f+reach*.6f),Lin(Cyan,.55f*fade),0,.45f+.35f*reach,Core,u,seed);
        }

        // ---------------------------------------------------------------------------------------------------------------
        // RMB: the hardlight guard
        // ---------------------------------------------------------------------------------------------------------------
        /// <summary>A hex panel of light raised in front of the crossed forearms while blocking, brightening on a blocked hit.</summary>
        private void GuardFx(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,float time,float q,bool low,bool me)
        {
            float at=a.BlockAt>-90?a.BlockAt:a.BlockUntil-Rules.BlockDuration,u=Smooth((time-at)/.07f)*(1-Smooth((time-(a.BlockUntil-.06f))/.06f));if(u<=0)return;
            var f=Forward(a.Yaw);var r=Right(f);var c=scene.Feet(a)+Up*1.22f+f*.52f;if(me)c=scene.Eye+f*.62f-Up*.12f;
            float hit=0;for(int i=0;i<records.Count;i++){var rec=records[i];if(rec.Target==a.Id&&rec.Kind==EventKind.Block){float s0=time-rec.Time;if(s0>=0&&s0<.2f)hit=Math.Max(hit,1-s0/.2f);}}
            float rad=(me?.13f:.17f)*(.6f+.4f*u),gain=(1.4f+2.2f*hit)*u,flick=q<.5f?1:.9f+.1f*(float)Math.Sin(time*47);
            HexCell(s,me,c,r,Up,rad,Lin(White,gain*flick),me?.012f:.018f);
            for(int i=0;i<6;i++){float ang=i*(float)Math.PI/3+(float)Math.PI/6;var o=c+(r*(float)Math.Cos(ang)+Up*(float)Math.Sin(ang))*rad*1.75f-f*.04f;HexCell(s,me,o,r,Up,rad,Lin(Cyan,gain*.8f*flick),me?.01f:.014f);}
            if(!me)s.Glow(c,Lin(Cyan,.5f*u+.9f*hit),0,1.1f,Core,0,a.Id);
            s.Band(me,c-r*rad*2.6f-Up*rad*2.4f,c+r*rad*2.6f-Up*rad*2.4f,c+r*rad*2.6f+Up*rad*2.4f,c-r*rad*2.6f+Up*rad*2.4f,.25f*u+.5f*hit,.25f*u+.5f*hit,Lin(Cyan,.9f));
        }
        private static void HexCell(ICyberFxSink s,bool view,Vector3 c,Vector3 x,Vector3 y,float rad,Vector3 color,float width)
        {
            for(int i=0;i<6;i++){float a0=i*(float)Math.PI/3,a1=a0+(float)Math.PI/3;s.Line(view,c+(x*(float)Math.Cos(a0)+y*(float)Math.Sin(a0))*rad,c+(x*(float)Math.Cos(a1)+y*(float)Math.Sin(a1))*rad,color,width,false);}
        }

        // ---------------------------------------------------------------------------------------------------------------
        // The abilities' casts (windup telegraphs and the active phases)
        // ---------------------------------------------------------------------------------------------------------------
        private void Cast(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,int slot,CyberPhase p,float time,float q,bool low,bool me)
        {
            long seed=a.Id*7919L+(long)Math.Round(p.At*240);var f=Forward(v.Yaw);var r=Right(f);var feet=scene.Feet(a);
            Vector3 lf,rf;bool hasL=me?scene.ViewFist(true,out lf):scene.Fist(a,true,out lf),hasR=me?scene.ViewFist(false,out rf):scene.Fist(a,false,out rf);
            if(!hasL)lf=feet+Up*1.2f+f*.35f+r*-.2f;if(!hasR)rf=feet+Up*1.2f+f*.35f+r*.2f;var palms=(lf+rf)*.5f;
            switch(slot)
            {
                case CyberLordSpec.Q:DashCast(s,a,v,p,time,seed,q,low,me);break;
                case CyberLordSpec.E:if(p.Part==0){for(int i=0;i<2;i++)Charge(s,me,i==0?lf:rf,p.U,time,seed+i,q,low);}break;
                case CyberLordSpec.One:BurstCast(s,v,p,time,palms,f,r,seed,q,low,me);break;
                case CyberLordSpec.Two:if(p.Part==0)Charge(s,me,rf,p.U,time,seed,q,low);break;
                case CyberLordSpec.Three:CrashCast(s,v,p,time,feet,lf,rf,seed,q,low,me);break;
            }
        }
        /// <summary>Hardlight gathering in a fist: converging motes and a growing core.</summary>
        private static void Charge(ICyberFxSink s,bool view,Vector3 fist,float u,float time,long seed,float q,bool low)
        {
            s.Sprite(view,fist,Lin(White,1.4f+2.4f*u),view?.04f+.05f*u:.12f+.18f*u,Hot);
            int n=Count(8,q,low);
            for(int i=0;i<n;i++){float h=H(seed+i,3),k=(time*2.2f+h)%1f;var d=Vector3.Normalize(new Vector3(H(seed+i,5)-.5f,H(seed+i,7)-.5f,H(seed+i,9)-.5f));s.Sprite(view,fist+d*(.45f*(1-k))*(view?.4f:1),Lin(Cyan,2*k*u),view?.015f:.045f,Sparkle);}
        }
        // ---- Q: Data Dash ----
        public const float DashGhostSeconds=.18f;
        private static void DashCast(ICyberFxSink s,Actor a,CyberLordView v,CyberPhase p,float time,long seed,float q,bool low,bool me)
        {
            var from=V(v.DashFrom);var to=V(v.DashTo);var path=to-from;float len=path.Length();if(len<.05f)return;var f=path/len;var r=Right(f);
            if(p.Part==0){float u=p.U;for(int i=0;i<(low?3:6);i++){float k=(i+.5f)/6;var at=from+f*(len*k*u)+Up*(.08f+.1f*H(seed+i,3));s.Voxel(false,WeaponShape.Cube,at,Yaw(v.Yaw),new Vector3(.06f,.06f,.06f),Lin(Cyan,1.6f*u));}return;}
            if(p.Part!=1)return;
            // The glitch streak: the body's run is torn into horizontal RGB-split bands strung from the start to the body.
            float k1=Smooth(p.U);var head=from+f*(len*k1);int bands=low?4:Count(8,q,low);
            for(int i=0;i<bands;i++)
            {
                float y=.15f+1.6f*H(seed+i,3),w=.03f+.05f*H(seed+i,5),back=len*k1*(.35f+.65f*H(seed+i,7)),side=(H(seed+i,9)-.5f)*.5f;
                int slot=(int)(time*24);if(q>=.5f&&H(seed+i*17+slot,11)<.2f)continue;
                var p1=head+Up*y+r*side;var p0=p1-f*back;
                s.Band(false,p0-Up*w,p1-Up*w,p1+Up*w,p0+Up*w,0,1,Lin(i%3==0?Magenta:i%3==1?Cyan:White,1.8f));
                if(!low)s.Band(false,p0-Up*w+r*.05f,p1-Up*w+r*.05f,p1+Up*w+r*.05f,p0+Up*w+r*.05f,0,.6f,Lin(Magenta,1.2f));
            }
            s.Streak(head+Up*.9f,-f*36,Lin(White,2.2f),.08f,1.6f,.06f,.9f,p.U,seed);
            // Pixel blocks shed off the body.
            int px=Count(10,q,low);for(int i=0;i<px;i++){float h=H(seed+i,13),k=(p.U+h)%1f;var at=head-f*(k*1.8f)+Up*(.2f+1.5f*H(seed+i,17))+r*((H(seed+i,19)-.5f)*.7f);float z=.05f*(1-k);s.Voxel(false,WeaponShape.Cube,at,Quaternion.Identity,new Vector3(z,z,z),Lin(i%2==0?Cyan:Magenta,2));}
        }
        /// <summary>The lingering data trail (1.2 s from the dash's end): a strip of data blocks along the path that ticks (every .3 s)
        /// and fades; one per remembered dash (NativeCyberLordDashTrails.cs), so a chained dash leaves the last one to finish.</summary>
        private static void Trail(ICyberFxSink s,in DashTrail d,float time,float q,bool low)
        {
            float end=d.End;if(!(d.Until>-90)||time>=d.Until+.2f||!(end>-90)||time<d.Born)return;
            var from=d.From;var to=d.To;var path=to-from;float len=path.Length();if(len<.05f)return;var f=path/len;var r=Right(f);
            float born=d.Born,grow=Clamp01((time-born)/Math.Max(.05f,end-born)),fade=1-Smooth((time-(d.Until-.25f))/.45f),y=from.Y+.03f;long seed=d.Actor*131L+(long)Math.Round(born*240);
            // A tick pulse runs down the trail every .3 s (the damage ticks).
            float tick=time<end?0:CyberLordSpec.TrailTick-((time-end)%CyberLordSpec.TrailTick),pulse=time<end?0:Smooth(1-tick/.12f);
            int cells=low?10:Count(22,q,low);
            for(int i=0;i<cells;i++)
            {
                float k=(i+.5f)/cells;if(k>grow)break;float h=H(seed+i,3),z=.045f+.05f*H(seed+i,7);var at=new Vector3(from.X,y,from.Z)+f*(len*k)+r*((h-.5f)*.55f)+Up*(z*.5f+.04f+.18f*H(seed+i,11)+.05f*pulse);
                float blink=q<.5f?1:(H(seed+i*7+(int)(time*14),5)>.3f?1:.35f);float g=(1.1f+1.6f*pulse)*fade*blink;
                s.Voxel(false,WeaponShape.Cube,at,Yaw(d.Yaw)*Quaternion.CreateFromAxisAngle(Up,h*3),new Vector3(z,z,z),Lin(i%5==0?Magenta:Cyan,g));
                // A few rising data bits over the trail.
                if(!low&&i%3==0){float rise=((time*1.3f+h)%1f);s.Voxel(false,WeaponShape.Cube,at+Up*(.05f+.6f*rise),Quaternion.Identity,new Vector3(.025f,.025f,.006f),Lin(White,1.6f*(1-rise)*fade));}
            }
            // An upright curtain of light along the path (soft at the floor and the top; never a strip on the floor).
            var root=new Vector3(from.X,y,from.Z);var tip=root+f*(len*grow);
            s.Band(false,root,tip,tip+Up*.55f,root+Up*.55f,.35f*fade,(.5f+.5f*pulse)*fade,Lin(Cyan,.9f));
        }
        // ---- E: Firewall ----
        /// <summary>The hex cell size (circumradius, m) on the full and low tiers.</summary>
        public const float WallCell=.25f,WallCellLow=.42f;
        /// <summary>The Firewall's hex grid at `time`: the cells light up from the base centre out as it forms, live with a slow
        /// scan, ripple where projectiles are absorbed, flicker red where enemies burn on it, and fall apart into pixels as it collapses.</summary>
        private void Wall(ICyberFxSink s,CyberLordView v,float time,float q,bool low)
        {
            if(!(v.WallBorn>-90)||time<v.WallBorn||time>=v.WallUntil+CyberLordSpec.WallCollapse)return;
            var c=V(v.WallAt);var f=Forward(v.WallYaw);var x=Right(f);float W=CyberLordSpec.WallWidth,Hh=CyberLordSpec.WallHeight,age=time-v.WallBorn;
            float form=Smooth(age/CyberLordSpec.WallForm),collapse=Clamp01((time-v.WallUntil)/CyberLordSpec.WallCollapse);long seed=v.Id*977L+(long)Math.Round(v.WallBorn*240);
            float cell=low?WallCellLow:WallCell,dx=cell*1.5f,dy=cell*(float)Math.Sqrt(3);int cols=(int)Math.Floor(W/dx),rows=(int)Math.Ceiling(Hh/dy);
            float scan=(age*.9f)%1f;bool reduced=q<.5f;
            // The plane's soft fill and its base and top edges.
            float fill=.18f*form*(1-collapse);var b0=c-x*(W*.5f);var b1=c+x*(W*.5f);
            float rise=Math.Max(.18f,Hh*form);s.Band(false,b0+Up*.02f,b1+Up*.02f,b1+Up*rise,b0+Up*rise,fill,fill,Lin(Cyan,1.2f));
            s.Band(false,b0+Up*.02f,b1+Up*.02f,b1+Up*.18f,b0+Up*.18f,form*(1-collapse),form*(1-collapse),Lin(White,1.6f));
            for(int col=0;col<=cols;col++)
            {
                float cx=-W*.5f+cell+col*dx;if(cx>W*.5f-cell*.5f)break;
                for(int row=0;row<=rows;row++)
                {
                    float cy=(row+(col%2==0?.5f:1f))*dy-dy*.5f;if(cy>Hh-cell*.6f||cy<cell*.4f)continue;
                    // Formation: from the base centre outward.
                    float reach=(Math.Abs(cx)/(W*.5f)*.6f+cy/Hh*.4f),on=Smooth((form*1.25f-reach)/.2f);if(on<=0)continue;
                    long cs=seed+col*37+row*101;var centre=c+x*cx+Up*cy;
                    // Collapse: cells drop, spin and shrink into falling pixels from the top down.
                    float drop=Clamp01(collapse*1.6f-(1-cy/Hh)*.6f);
                    float g=1.15f+.6f*Smooth(1-Math.Abs(cy/Hh-scan)/.12f);
                    for(int i=0;i<records.Count;i++){var rec=records[i];if(rec.Source!=v.Id||rec.Hit!=CyberHit.Firewall)continue;float ra=time-rec.Time;if(ra<0||ra>.6f)continue;
                        float d=(centre-rec.Position).Length();if(rec.Kind==EventKind.Absorb){float front=ra*4.5f;g+=2.2f*Smooth(1-Math.Abs(d-front)/.35f)*(1-ra/.6f);}
                        else{float k=Smooth(1-d/.9f)*(1-ra/.35f);if(k>0)g+=k*(reduced?1.5f:(H(cs+(int)(time*40),3)>.4f?3:.5f));}}
                    float flick=reduced?1:(H(cs+(int)(time*20),5)>.08f?1:.4f);var col3=Lin(Cyan,g*on*flick*(1-drop));
                    float rad=cell*(.92f-.5f*drop);
                    if(drop>0){centre-=Up*(drop*drop*1.8f);centre+=f*(H(cs,7)-.5f)*drop*.6f;if(centre.Y-c.Y<rad+.15f)continue;}
                    // Three edges per cell (the grid shares the rest), as thin glowing bars.
                    for(int e=0;e<(drop>0?6:3);e++)
                    {
                        float a0=e*(float)Math.PI/3,a1=a0+(float)Math.PI/3;var p0=centre+(x*(float)Math.Cos(a0)+Up*(float)Math.Sin(a0))*rad;var p1=centre+(x*(float)Math.Cos(a1)+Up*(float)Math.Sin(a1))*rad;
                        var mid=(p0+p1)*.5f;var along=p1-p0;float l=along.Length();if(l<1e-4f)continue;
                        var rot=Quaternion.CreateFromAxisAngle(f,(float)Math.Atan2(along.Y,Vector3.Dot(along,x)));
                        s.Voxel(false,WeaponShape.Cube,mid,rot*Yaw(v.WallYaw),new Vector3(l,.016f,.016f),col3);
                    }
                    if(drop>.4f&&!low&&H(cs,11)>.5f){float z=.05f*(1-drop);s.Voxel(false,WeaponShape.Cube,centre+Up*.1f,Quaternion.Identity,new Vector3(z,z,z),Lin(Magenta,1.8f));}
                }
            }
            if(!low&&form>.2f&&collapse<1)s.Glow(c+Up*(Hh*.5f),Lin(Cyan,.35f*form*(1-collapse)),0,3.2f,Core,0,seed);
        }
        /// <summary>An absorbed projectile: a ring of light spreading over the wall from the impact, a white flash.</summary>
        private static void Ripple(ICyberFxSink s,ICyberFxScene scene,in Record r,float age,float q,bool low)
        {
            float u=age/.6f,fade=1-Smooth(u);s.Sprite(false,r.Position,Lin(White,3.2f*fade*fade),.35f,Hot);s.Sprite(false,r.Position,Lin(Cyan,2*fade),.5f+2.6f*Smooth(u),RingSprite);
        }
        /// <summary>An enemy burning on the wall: a red-white flicker at the touch and sparks off it.</summary>
        private static void BurnFlicker(ICyberFxSink s,Vector3 at,float age,long seed,float q,bool low)
        {
            float u=age/.35f,fade=1-u;bool on=q<.5f||H(seed+(int)(age*40),3)>.3f;if(on)s.Sprite(false,at,Lin(Burn,3*fade),.45f,Hot);
            int n=Count(6,q,low);for(int i=0;i<n;i++){var d=Vector3.Normalize(new Vector3(H(seed+i,5)-.5f,H(seed+i,7)*.6f,H(seed+i,9)-.5f));s.Streak(at+d*(age*3),d*4,Lin(i%2==0?Burn:White,2.2f*fade),.018f,.08f,.03f,.9f,u,seed+i);}
        }
        // ---- 1: Packet Burst ----
        public const float ShardSpeed=34,ShardLength=.22f;
        private static void BurstCast(ICyberFxSink s,CyberLordView v,CyberPhase p,float time,Vector3 palms,Vector3 f,Vector3 r,long seed,float q,bool low,bool me)
        {
            if(p.Part==0){Charge(s,me,palms,p.U,time,seed,q,low);return;}
            float rel=v.Release[CyberLordSpec.One],age=time-rel;if(age<0)return;
            float flash=1-Smooth(age/.12f);if(flash>0){s.Sprite(me,palms,Lin(White,3*flash),me?.08f:.3f,Hot);for(int i=0;i<4;i++){float k=.05f*flash;s.Voxel(false,WeaponShape.Cube,palms+new Vector3(H(seed+i,3)-.5f,H(seed+i,5)-.5f,H(seed+i,7)-.5f)*.3f,Quaternion.Identity,new Vector3(k,k,k*.3f),Lin(i%2==0?Magenta:Cyan,2.4f));}}
            int n=(int)CyberLordSpec.BurstShards;float half=CyberLordSpec.BurstAngle*.5f*(float)Math.PI/180,life=CyberLordSpec.BurstRange/ShardSpeed;
            for(int i=0;i<n;i++)
            {
                float t=age-.012f*i;if(t<0||t>life+.06f)continue;float ang=(-1+2*(i+.5f)/n)*half+(H(seed+i,3)-.5f)*.08f,pitch=(H(seed+i,5)-.5f)*.18f;
                var dir=Vector3.Normalize(f*(float)Math.Cos(ang)+r*(float)Math.Sin(ang)+Up*pitch);var at=palms+dir*(ShardSpeed*Math.Min(t,life));float end=Smooth((t-life)/.06f);
                var rot=Quaternion.CreateFromAxisAngle(Up,(float)Math.Atan2(dir.X,dir.Z))*Quaternion.CreateFromAxisAngle(Vector3.UnitX,-(float)Math.Asin(MeleeMath.Clamp(dir.Y,-1,1)))*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,t*14+i);
                float k=1-end;s.Voxel(false,WeaponShape.Cube,at,rot,new Vector3(.06f,.06f,ShardLength)*k,Lin(i%2==0?White:Cyan,2.6f));if(!low&&i%2==0)s.Glow(at,Lin(Cyan,.5f*k),0,.28f,Core,t/life,seed+i+80);
                // RGB-split trails on either side (glitch).
                s.Streak(at+r*.03f,dir*ShardSpeed,Lin(Magenta,1.6f*k),.02f,.5f,.03f,.9f,t/life,seed+i);
                if(!low)s.Streak(at-r*.03f,dir*ShardSpeed,Lin(Cyan,1.6f*k),.02f,.5f,.03f,.9f,t/life,seed+i+40);
            }
        }
        // ---- 2: Siphon Link ----
        public const float PacketRate=7,PacketSeconds=.35f;
        private static void Link(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,float time,float q,bool low)
        {
            if(v.LinkTarget==0||!(v.LinkAt>-90)||time<v.LinkAt||time>=v.LinkUntil+.3f)return;Actor target=null;var actors=scene.Actors;for(int i=0;i<actors.Count;i++)if(actors[i]?.Id==v.LinkTarget){target=actors[i];break;}
            if(target==null)return;bool me=scene.Viewer==a.Id;
            Vector3 from;if(!(me?scene.ViewFist(false,out from):scene.Fist(a,false,out from)))from=scene.Feet(a)+Up*1.3f;
            Vector3 to=scene.Chest(target,out var tc)?tc:scene.Feet(target)+Up*1.2f;long seed=a.Id*613L+(long)Math.Round(v.LinkAt*240);
            float age=time-v.LinkAt,on=Smooth(age/.12f);var d=to-from;float len=d.Length();if(len<.1f)return;var dir=d/len;var side=Vector3.Normalize(Vector3.Cross(dir,Up)+new Vector3(0,0,1e-4f));var up=Vector3.Cross(side,dir);
            // The link snaps when it ends (lost sight or range before 2.5 s, or its end): sparks at the break, the beam gone.
            if(time>=v.LinkUntil){float u=(time-v.LinkUntil)/.3f;bool broken=v.LinkUntil<v.LinkAt+CyberLordSpec.LinkSeconds-.05f;var mid=from+d*.5f;
                if(broken)GlitchSpark(s,mid,u*.3f,.8f,seed,q,low);s.Sprite(false,to,Lin(Cyan,2*(1-u)),.4f,RingSprite);return;}
            int segs=low?6:12;var prev=from;
            for(int i=1;i<=segs;i++)
            {
                float k=i/(float)segs;int slot=(int)(time*(q<.5f?10:30));float j=(H(seed+i*13+slot,3)-.5f)*.07f*(float)Math.Sin(k*Math.PI);
                var p=from+d*k+side*j+up*(H(seed+i*7+slot,5)-.5f)*.05f*(float)Math.Sin(k*Math.PI);
                s.Line(me&&k<.25f,prev,p,Lin(i%2==0?Cyan:White,2.2f*on),.05f,false);prev=p;
            }
            s.Line(false,from+side*.04f,to+side*.04f,Lin(Magenta,1.1f*on),.02f,false);if(!low)s.Glow(from+d*.5f,Lin(Cyan,.35f*on),0,Math.Min(2.2f,len*.35f),Mote,age,seed+3);
            // Data packets streaming from the target back to the caster.
            int packets=Math.Max(1,(int)Math.Round(PacketRate*Math.Max(.35f,Math.Min(1,q))));
            for(int i=0;i<packets;i++)
            {
                float k=((age*PacketRate/packets)*1.6f+i/(float)packets)%1f,ease=k*k*(3-2*k);var p=to-d*ease+up*.04f*(float)Math.Sin(k*9+i);
                s.Voxel(false,WeaponShape.Cube,p,Quaternion.CreateFromAxisAngle(dir,time*6+i),new Vector3(.05f,.05f,.05f)*(1-.4f*k),Lin(i%3==0?Heal:Cyan,2.4f));
            }
            s.Sprite(false,to,Lin(Cyan,1.6f*on),.38f,RingSprite);s.Sprite(me,from,Lin(White,1.8f*on),me?.05f:.18f,Hot);
        }
        private static void HealGlint(ICyberFxSink s,ICyberFxScene scene,in Record r,float age,long seed,float q,bool low)
        {
            Actor a=null;var actors=scene.Actors;for(int i=0;i<actors.Count;i++)if(actors[i]?.Id==r.Target){a=actors[i];break;}if(a==null||!a.Alive)return;
            bool me=scene.Viewer==a.Id;Vector3 at=me?scene.Eye-Up*.42f+Forward(a.Yaw)*.5f:scene.Chest(a,out var c)?c:scene.Feet(a)+Up*1.2f;float u=age/.55f,fade=1-Smooth(u);
            s.Sprite(me,at,Lin(Heal,2.6f*fade),me?.07f:.35f,Sparkle);s.Sprite(me,at,Lin(White,2*fade*fade),me?.04f:.18f,Hot);
            int n=Count(4,q,low);for(int i=0;i<n;i++){var o=new Vector3(H(seed+i,3)-.5f,0,H(seed+i,5)-.5f)*.5f+Up*(.3f*u+.2f*H(seed+i,7));float k=.035f*fade;
                if(!me){s.Voxel(false,WeaponShape.Cube,at+o,Quaternion.Identity,new Vector3(k*3,k,k),Lin(Heal,2.2f));s.Voxel(false,WeaponShape.Cube,at+o,Quaternion.Identity,new Vector3(k,k*3,k),Lin(Heal,2.2f));}}
        }
        // ---- 3: System Crash ----
        private static void CrashCast(ICyberFxSink s,CyberLordView v,CyberPhase p,float time,Vector3 feet,Vector3 lf,Vector3 rf,long seed,float q,bool low,bool me)
        {
            if(p.Part==0)
            {
                // The windup: glitch energy gathers in both fists, the floor below begins to fracture.
                Charge(s,me,lf,p.U,time,seed,q,low);Charge(s,me,rf,p.U,time,seed+5,q,low);
                int n=Count(6,q,low);for(int i=0;i<n;i++){float a0=H(seed+i,3)*MeleeMath.TAU,len=CyberLordSpec.CrashRadius*.6f*Smooth(p.U)*(.5f+.5f*H(seed+i,5));var d=new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0));
                    var at=feet+d*(.3f+len)+Up*.05f;s.Streak(at+Up*(.6f*((time*1.7f+H(seed+i,7))%1f)),Up*2.5f,Lin(Cyan,1.6f*p.U),.02f,.12f,.03f,.9f,p.U,seed+i);}
            }
        }
        /// <summary>The 4 m glitch field: the slam's shockwave, a disc of flickering glitch tiles, scan columns at its rim, until its end.</summary>
        private static void Field(ICyberFxSink s,CyberLordView v,float time,float q,bool low)
        {
            if(!(v.CrashBorn>-90)||time<v.CrashBorn||time>=v.CrashUntil+.3f)return;var c=V(v.CrashAt);float age=time-v.CrashBorn,R=CyberLordSpec.CrashRadius;long seed=v.Id*373L+(long)Math.Round(v.CrashBorn*240);
            float fade=1-Smooth((time-v.CrashUntil)/.3f),y=c.Y+.025f;bool reduced=q<.5f;
            // The slam: a white flash, an upright wave of light racing out to 4 m in .2 s, dust.
            if(age<.45f){float u=Clamp01(age/.2f),k=1-Smooth(age/.45f);s.Sprite(false,c+Up*.2f,Lin(White,4*k*k),1.2f,Hot);s.Glow(c+Up*.3f,Lin(Cyan,.6f*k),0,R*.6f,Ring,u,seed);
                Wave(s,c,R*u,.45f+.7f*(1-u),Lin(White,1.6f),k*(1-u),low?14:28);if(!low)for(int i=0;i<Count(8,q,low);i++){float a0=i*MeleeMath.TAU/8+H(seed+i,3)*.4f;var d=new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0));s.Smoke(c+d*(R*u*.8f)+Up*.15f,Lin(0x3a4a6a),.45f*k,.6f+.6f*u,u,seed+i,c.Y);}}
            float grow=Smooth(age/.22f)*fade;
            // The field's volume: corrupted voxels popping up out of the floor on hashed slots, data rising off it, an edge-less
            // glow over it (no tiles or rim ring on the floor).
            float tile=low?.7f:.5f;int n=(int)Math.Ceiling(R/tile);int slot=(int)(time*(reduced?4:12));
            for(int ix=-n;ix<=n;ix++)for(int iz=-n;iz<=n;iz++)
            {
                float px=ix*tile,pz=iz*tile,d=(float)Math.Sqrt(px*px+pz*pz);if(d>R*grow)continue;long ts=seed+ix*73+iz*151;float h=H(ts+slot,3);if(h<.62f)continue;
                float z=(.05f+.07f*H(ts+slot,5))*grow,hop=(reduced?.03f:.04f+.22f*H(ts+slot,7));var off=new Vector3(H(ts+slot,9)-.5f,0,H(ts+slot,13)-.5f)*tile*.5f;
                s.Voxel(false,WeaponShape.Cube,new Vector3(c.X+px,y,c.Z+pz)+off+Up*(z*.5f+hop),Quaternion.CreateFromAxisAngle(Up,h*6),new Vector3(z,z,z),Lin(h>.95f?Magenta:h>.86f?White:Cyan,(1.1f+.9f*H(ts,11))*grow));
            }
            int rising=Count(12,q,low);for(int i=0;i<rising;i++){float a0=H(seed+i,15)*MeleeMath.TAU,rr=R*Math.Min(grow,1)*(float)Math.Sqrt(H(seed+i,17)),k=((age*1.1f+H(seed+i,19))%1f);
                var p=new Vector3(c.X+(float)Math.Cos(a0)*rr,y+.05f+1.2f*k,c.Z+(float)Math.Sin(a0)*rr);s.Streak(p,Up*2.2f,Lin(i%4==0?Magenta:Cyan,1.4f*grow*(1-k)),.018f,.1f,.03f,.9f,k,seed+i);}
            s.Glow(c+Up*.35f,Lin(Cyan,.22f*grow),0,R*.9f,Mote,0,seed+7);
            // Scan columns rising at the rim.
            int cols=Count(10,q,low);for(int i=0;i<cols;i++){float a0=i*MeleeMath.TAU/cols+age*.6f,h=((age*.8f+H(seed+i,11))%1f);var p0=c+new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0))*R;
                s.Band(false,p0+Up*.02f,p0+Up*(.2f+1.4f*h),p0+Up*(.2f+1.4f*h)+Vector3.Normalize(new Vector3(-(float)Math.Sin(a0),0,(float)Math.Cos(a0)))*.08f,p0+Up*.02f+Vector3.Normalize(new Vector3(-(float)Math.Sin(a0),0,(float)Math.Cos(a0)))*.08f,.8f*grow,0,Lin(Cyan,1.4f));}
        }
        /// <summary>An upright wall of light round `c` at `radius`, `height` tall (the slam waves: upright, never a ring on the floor;
        /// the ribbon fades it out at the floor and the top).</summary>
        private static void Wave(ICyberFxSink s,Vector3 c,float radius,float height,Vector3 color,float alpha,int n)
        {
            if(radius<.05f||alpha<=0)return;
            for(int i=0;i<n;i++){float a0=i*MeleeMath.TAU/n,a1=(i+1)*MeleeMath.TAU/n;var d0=new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0))*radius;var d1=new Vector3((float)Math.Cos(a1),0,(float)Math.Sin(a1))*radius;
                s.Band(false,c+d0+Up*.02f,c+d1+Up*.02f,c+d1+Up*height,c+d0+Up*height,alpha,alpha,color);}
        }

        // ---------------------------------------------------------------------------------------------------------------
        // The glitched look: RGB-split silhouettes and pixel blocks on System Crash's stunned victims and Kernel Panic's victim
        // ---------------------------------------------------------------------------------------------------------------
        /// <summary>How strongly `actor` shows glitched at `time` (0..1): a System Crash hit's 1 s stun, or Kernel Panic's victim.</summary>
        public float GlitchOf(int actor,float time,IReadOnlyList<Actor> actors)
        {
            float g=0;
            for(int i=0;i<records.Count;i++){var r=records[i];if(r.Target!=actor||r.Hit!=CyberHit.Crash||r.Kind==EventKind.Heal)continue;float s=time-r.Time;if(s>=0&&s<CyberLordSpec.CrashStun)g=Math.Max(g,s<.1f?1:1-.4f*Smooth((s-.6f)/.4f));}
            for(int i=0;i<actors.Count;i++){var a=actors[i];if(!CyberLordFeed.Read(a,other)||other.PanicVictim!=actor||!other.PanicLive(time))continue;float age=other.PanicAge(time);g=Math.Max(g,float.IsNaN(age)||age<0?.6f:.85f);}
            return g;
        }
        private void Glitched(ICyberFxScene scene,float time,float q,bool low)
        {
            var actors=scene.Actors;bool reduced=q<.5f;
            for(int i=0;i<actors.Count&&ghostCount<MaxGhosts-2;i++)
            {
                var a=actors[i];if(a==null||!a.Alive)continue;float g=GlitchOf(a.Id,time,actors);if(g<=0)continue;
                // The split pulls the copies apart on glitch steps (24 Hz; steady and smaller with reduced effects).
                int step=(int)(time*(reduced?4:24));float jitter=reduced?.5f:H(a.Id*97+step,3);float off=(.025f+.05f*jitter)*g;
                if(!reduced&&H(a.Id*61+step,5)<.15f)off*=2.2f;
                var right=Right(Forward(a.Yaw));
                ghosts[ghostCount++]=new CyberGhost{Kind=CyberGhost.Split,Actor=a.Id,Offset=right*off+Up*(reduced?0:(H(a.Id*7+step,9)-.5f)*.02f),Tint=Lin(Magenta,1.4f),Opacity=.42f*g};
                ghosts[ghostCount++]=new CyberGhost{Kind=CyberGhost.Split,Actor=a.Id,Offset=-right*off,Tint=Lin(Cyan,1.4f),Opacity=.42f*g};
            }
        }
        /// <summary>The fists' flare (NativeCyberLordLook.Pieces) from pilot `id`'s latest combo hit at or before `time`: the striking
        /// fist flares, and the gauntlets light as the mana flow arrives.</summary>
        public NativeCyberLordLook.Flare FlareOf(int id,float time)
        {
            float at=-100,charge=-100;int step=0;
            for(int i=0;i<records.Count;i++){var r=records[i];if(r.Source!=id||r.Time>time||r.Kind==EventKind.Heal)continue;if(r.Hit>=CyberHit.Jab&&r.Hit<=CyberHit.Palm&&r.Time>at){at=r.Time;step=r.Hit-CyberHit.Jab;charge=r.Time+ManaFlowSeconds;}}
            return NativeCyberLordLook.FlareAt(time,at,step,charge);
        }
        /// <summary>A fork-bomb clone's stand-in actor (posed by the runtime on a rig of its own): the caster's look, the combo take
        /// `g.Step` struck at its hit, and the pose time for the ghost's age (CloneTime).</summary>
        public const float CloneSwingAt=1000;
        public static void CloneActor(Actor clone,Actor caster,in CyberGhost g)
        {
            clone.Id=-1000-g.Index;clone.SourceSpec=CyberLordSpec.Id;clone.Weapon=WeaponId.Sword;clone.Grounded=true;clone.Hp=100;clone.CastRate=1;clone.Yaw=g.Yaw;clone.Position=new V3(g.Position.X,g.Position.Y,g.Position.Z);
            clone.BodyColor=caster?.BodyColor??clone.BodyColor;clone.AccentColor=caster?.AccentColor??clone.AccentColor;clone.Hat=caster?.Hat??"none";
            CyberLordFixture.Combo(clone,g.Step,CloneSwingAt);
        }
        public static float CloneTime(in CyberGhost g)=>CloneSwingAt+CyberLordSpec.ComboWindups[g.Step]-.16f+g.PoseAge;
        /// <summary>Pixel blocks crawling over a glitched pilot's body (the runtime passes its posed box centres).</summary>
        public void GlitchBlocks(ICyberFxSink s,int actor,Vector3 chest,float g,float time,float q,bool low)
        {
            int n=low?2:Count(6,q,low);bool reduced=q<.5f;int step=(int)(time*(reduced?3:18));
            for(int i=0;i<n;i++){long k=actor*131L+step*17+i;if(H(k,3)<.35f)continue;var o=new Vector3(H(k,5)-.5f,(H(k,7)-.5f)*2.2f,H(k,9)-.5f)*.5f;float z=(.04f+.06f*H(k,11))*g;s.Voxel(false,WeaponShape.Cube,chest+o,Quaternion.Identity,new Vector3(z*1.8f,z,z*.4f),Lin(H(k,13)>.5f?Magenta:Cyan,2.2f));}
        }

        // ---------------------------------------------------------------------------------------------------------------
        // 4: Kernel Panic
        // ---------------------------------------------------------------------------------------------------------------
        private static void Opener(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,CyberPhase p,float time,float q,bool low,bool me)
        {
            long seed=a.Id*4241L+(long)Math.Round(v.At[CyberLordSpec.Four]*240);var f=Forward(v.Yaw);var feet=scene.Feet(a);
            Vector3 fist;if(!(me?scene.ViewFist(false,out fist):scene.Fist(a,false,out fist)))fist=feet+Up*1.3f+f*.4f;
            if(p.Live&&p.Part==0){Charge(s,me,fist,p.U,time,seed,q,low);s.Glow(feet+Up*.9f,Lin(Magenta,.6f*p.U),0,1.6f,Core,p.U,seed);return;}
            if(p.Live&&p.Part==1)
            {
                // The lunge: speed lines raking back off the body, the fist's hardlight spearhead.
                int n=Count(10,q,low);for(int i=0;i<n;i++){float h=H(seed+i,3),ph=(time*9+h)%1f;var head=feet+Right(f)*((H(seed+i,5)-.5f)*1.1f)+Up*(.3f+1.5f*H(seed+i,7))-f*(ph*1.4f);s.Streak(head,-f*(16+10*h),Lin(i%3==0?Magenta:Cyan,2.4f*(1-ph)),.03f,.6f,.05f,.9f,ph,seed+i);}
                s.Sprite(me,fist,Lin(White,3.2f),me?.07f:.32f,Hot);s.Streak(fist,-f*30,Lin(Cyan,2.6f),.08f,1.4f,.05f,.9f,p.U,seed);return;
            }
            // A whiff: the spearhead fizzles into a null glitch where the punch ran out.
            if(v.PanicOutcome==CyberLordSpec.Whiff){float age=time-v.Until[CyberLordSpec.Four];if(age>=0&&age<.4f)GlitchSpark(s,fist+f*.2f,age*.8f,.8f,seed,q,low);}
        }
        /// <summary>The lunge's blink trail for .3 s from the contact (the host moves the body there at once): RGB-split bands from
        /// where the caster stood (a whiff: its cast spot; a connect: back along the facing from the contact) to where it is.</summary>
        public const float LungeTrailSeconds=.3f,LungeBack=3;
        private static void LungeTrail(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,float time,float q,bool low)
        {
            float c=v.Until[CyberLordSpec.Four],age=time-c;if(!(c>-90)||age<0||age>=LungeTrailSeconds||v.PanicOutcome==0)return;
            var f=Forward(v.PanicYaw);Vector3 to,from;
            if(v.PanicOutcome==CyberLordSpec.Whiff){to=scene.Feet(a);from=v.PanicCasterStart;}else{to=v.PanicCasterStart;from=to-f*LungeBack;}
            var d=to-from;d.Y=0;float len=d.Length();if(len<.2f)return;var r=Right(d/len);float fade=1-Smooth(age/LungeTrailSeconds);long seed=a.Id*577L+v.PanicId*13L;
            int bands=low?3:Count(7,q,low);
            for(int i=0;i<bands;i++)
            {
                float y=.2f+1.5f*H(seed+i,3),w=.025f+.05f*H(seed+i,5),side=(H(seed+i,9)-.5f)*.5f;var p0=from+r*side+Up*y;var p1=to+r*side+Up*y;
                s.Band(false,p0-Up*w,p1-Up*w,p1+Up*w,p0+Up*w,0,fade,Lin(i%3==0?Magenta:i%3==1?Cyan:White,1.8f));
            }
        }
        /// <summary>
        /// The sequence `t` (presentation time; the participants' hit-stopped clock): the connect flash held through the cut-in,
        /// the Breaker's launch streaks (and a wall-splat crack), the glitch-teleport tears out of and into each blink, the
        /// rising kick's streaks, the hammer-fist's trail, the fork-bomb clones (CyberGhost.Clone) striking and shattering into
        /// pixels, the code column falling onto the landing point and its 5 m shockwave.
        /// </summary>
        private void Panic(ICyberFxSink s,ICyberFxScene scene,Actor a,CyberLordView v,float t,float q,bool low,bool me)
        {
            long seed=a.Id*9173L+v.PanicSeed*31L+(long)Math.Round(v.PanicConnect*240);float age=t-v.PanicStart;var S=KernelPanicPath.Steps;var f=Forward(v.PanicYaw);var r=Right(f);
            bool ended=v.PanicOutcome==CyberLordSpec.EndedEarly&&v.PanicEndedAt>-90&&t>=v.PanicEndedAt;
            var anchor=v.Point(KernelPanicPath.Anchor);var victim=KernelPanicPath.Victim(v,age);var vChest=victim+Up*1.15f;
            // The cut-in: the contact frozen, sparks held in the air trembling.
            if(t<v.PanicStart)
            {
                float u=(t-v.PanicConnect)/CyberLordSpec.CutIn,at=1-u;var hit=anchor+Up*1.25f-f*.25f;
                s.Sprite(false,hit,Lin(White,2.6f+(q<.5f?0:.6f*(float)Math.Sin(t*60))),.5f,Hot);s.Sprite(false,hit,Lin(Magenta,1.6f),.9f+.1f*u,RingSprite);
                int n=Count(12,q,low);for(int i=0;i<n;i++){var d=Vector3.Normalize(new Vector3(H(seed+i,3)-.5f,H(seed+i,5)-.4f,H(seed+i,7)-.5f));float dist=.15f+.35f*H(seed+i,9)+.02f*u;var p=hit+d*dist+d*(q<.5f?0:.006f*(float)Math.Sin(t*80+i));
                    s.Voxel(false,WeaponShape.Cube,p,Quaternion.CreateFromAxisAngle(d,i),new Vector3(.03f,.03f,.08f),Lin(i%2==0?White:Cyan,2.4f));}
                return;
            }
            if(ended)return;
            // 1. Breaker: the launch burst and streaks trailing the flying victim; a crack ring if it splats on a wall.
            if(age<S[1]+.15f)
            {
                float u=age/S[1];if(u<.4f)ImpactBurst(s,anchor+Up*1.2f,age,1.4f,seed,q,low);
                var dir=v.Point(KernelPanicPath.LaunchEnd)-anchor;float len=dir.Length();if(len>.1f)dir/=len;
                int n=Count(12,q,low);float fade=1-Smooth((age-S[1])/.15f);
                for(int i=0;i<n;i++){float h=H(seed+i,11);var o=r*((H(seed+i,13)-.5f)*.9f)+Up*(.2f+1.4f*H(seed+i,15));s.Streak(victim+o-dir*(.2f+.6f*h),-dir*(18+10*h),Lin(i%3==0?Magenta:i%3==1?Cyan:White,2.6f*fade),.035f,.9f,.05f,.9f,u,seed+i);}
                if(v.PanicWall&&age>.25f){float w=age-.27f;if(w>=0)WallSplat(s,v.Point(KernelPanicPath.LaunchEnd)+Up*1.1f,dir,w,seed,q,low);}
            }
            // 2. Blink-behind: a tear out where the caster stood, a tear in behind the victim, the kick's upward streaks.
            Tear(s,v.Point(KernelPanicPath.Start),age-S[1]+.04f,false,seed+101,q,low);
            Tear(s,v.Point(KernelPanicPath.Behind),age-S[1],true,seed+103,q,low);
            if(age>=S[1]+.08f&&age<S[2]+.1f)
            {
                float k=age-KernelPanicPath.HitAge(2);if(k>=0&&k<.3f)ImpactBurst(s,victim+Up*.9f,k,1.1f,seed+7,q,low);
                int n=Count(10,q,low);float u=Clamp01((age-S[1])/(S[2]-S[1]));for(int i=0;i<n;i++){float h=H(seed+i*3,17);var o=r*((H(seed+i,19)-.5f)*.8f)+f*((H(seed+i,21)-.5f)*.8f);s.Streak(victim+o-Up*(.3f+.8f*h),Up*(16+8*h),Lin(i%2==0?Cyan:White,2.2f*(1-u)),.03f,.8f,.05f,.9f,u,seed+i);}
            }
            // 3. Blink-above: out behind, in above the apex, the hammer-fist's arc trail down onto the victim.
            Tear(s,v.Point(KernelPanicPath.Behind)+Up*.2f,age-S[2]+.04f,false,seed+107,q,low);
            Tear(s,v.Point(KernelPanicPath.Above),age-S[2],true,seed+109,q,low);
            if(age>=S[2]&&age<S[2]+.4f)
            {
                // (The hammer lands on the step's start, as the caster tears in above: the arc sweeps down in .06 s and fades.)
                float u=Clamp01((age-S[2])/.06f),fade=1-Smooth((age-S[2]-.06f)/.34f);var top=v.Point(KernelPanicPath.Above)+Up*1.9f+f*.15f;var hit=v.Point(KernelPanicPath.Apex)+Up*1.25f;
                // A crescent of light from overhead down through the strike.
                int segs=low?6:10;for(int i=0;i<segs;i++){float a0=i/(float)segs*Math.Min(1,u*2.4f),a1=(i+1)/(float)segs*Math.Min(1,u*2.4f);var p0=Arc(top,hit,f,a0);var p1=Arc(top,hit,f,a1);float w=.03f+.09f*a1;
                    s.Band(false,p0-r*w,p1-r*w,p1+r*w,p0+r*w,.2f*fade,1.2f*fade*Math.Max(.2f,a1),Lin(i%2==0?White:Cyan,2));}
                float k=age-KernelPanicPath.HitAge(3);if(k>=0&&k<.3f){ImpactBurst(s,hit,k,1.3f,seed+11,q,low);for(int i=0;i<Count(8,q,low);i++){var d=Vector3.Normalize(new Vector3(H(seed+i,23)-.5f,-1.2f,H(seed+i,25)-.5f));s.Streak(hit+d*(k*6),d*20,Lin(Cyan,2.4f*(1-k/.3f)),.03f,.7f,.05f,.9f,k/.3f,seed+i);}}
            }
            // 4. Fork bomb: three clones round the falling victim, each tearing in, striking on its beat and shattering into pixels.
            for(int i=0;i<KernelPanicPath.Clones.Length;i++)
            {
                float strike=KernelPanicPath.Clones[i],ca=age-(strike-.16f);if(ca<0||ca>.16f+.42f)continue;
                var at=CloneAt(v,i,strike);var face=KernelPanicPath.Victim(v,strike)-at;face.Y=0;float yaw=face.LengthSquared()>1e-4f?(float)(Math.Atan2(face.X,face.Z)*180/Math.PI):v.PanicYaw;
                Tear(s,at,ca-.02f,true,seed+200+i,q,low);
                float shatter=ca-.24f;
                if(shatter<0&&ghostCount<MaxGhosts){float op=Smooth(ca/.06f);ghosts[ghostCount++]=new CyberGhost{Kind=CyberGhost.Clone,Actor=a.Id,Index=i,Step=i==1?0:i==2?2:1,Position=at,Yaw=yaw,PoseAge=ca,Tint=Lin(i==1?Magenta:Cyan,1.8f),Opacity=.85f*op};}
                if(ca>=.16f&&ca<.36f)HitSpark(s,KernelPanicPath.Victim(v,strike)+Up*1.1f,ca-.16f,.9f,seed+300+i,q,low);
                if(shatter>=0)Shatter(s,at,yaw,shatter,i==1?Magenta:Cyan,seed+400+i,q,low);
            }
            // 5. Kernel Panic: the caster tears in by the landing, the code column falls, the shockwave races out 5 m.
            Tear(s,v.Point(KernelPanicPath.Reappear),age-(S[4]-.1f),true,seed+113,q,low);
            CodeColumn(s,v.Point(KernelPanicPath.Landing),age,seed+500,q,low);
        }
        private static Vector3 Arc(Vector3 top,Vector3 hit,Vector3 f,float k){var p=Vector3.Lerp(top,hit,k);p+=f*(.7f*(float)Math.Sin(k*Math.PI));return p;}
        /// <summary>Where fork-bomb clone `i` stands for its strike: on three sides of the victim's fall, 1.3 m out.</summary>
        public static Vector3 CloneAt(CyberLordView v,int i,float strike)
        {
            var vic=KernelPanicPath.Victim(v,strike);var f=Forward(v.PanicYaw);float ang=(i-1)*2.1f+(float)Math.PI;var d=Vector3.Transform(f,Quaternion.CreateFromAxisAngle(Up,ang));
            var at=vic+d*1.3f;at.Y=Math.Max(v.Point(KernelPanicPath.Landing).Y,vic.Y-.7f);return at;
        }
        private static void ImpactBurst(ICyberFxSink s,Vector3 at,float age,float size,long seed,float q,bool low)
        {
            float u=Clamp01(age/.35f),fade=1-Smooth(u);s.Sprite(false,at,Lin(White,5*fade*fade),.6f*size,Hot);s.Sprite(false,at,Lin(Magenta,2.4f*fade),(.6f+1.8f*Smooth(u*1.5f))*size,RingSprite);
            s.Sprite(false,at,Lin(Cyan,2*fade),(.4f+2.4f*Smooth(u))*size,RingSprite);s.Glow(at,Lin(Cyan,1.6f*fade),0,1.6f*size,Core,u,seed);
        }
        /// <summary>A glitch-teleport tear at `at` (age from the blink): a vertical slit of RGB-split bands opening and snapping
        /// shut, pixels spilling; `into` (arriving) widens, then flashes; leaving collapses the silhouette into the slit.</summary>
        public const float TearSeconds=.26f;
        private static void Tear(ICyberFxSink s,Vector3 at,float age,bool into,long seed,float q,bool low)
        {
            if(age<0||age>=TearSeconds)return;float u=age/TearSeconds,open=into?(float)Math.Sin(Math.Min(1,u*1.3f)*Math.PI):1-Smooth(u),fade=1-Smooth((u-.6f)/.4f);
            var side=Vector3.UnitX;var depth=Vector3.UnitZ;int bands=low?4:Count(9,q,low);
            for(int i=0;i<bands;i++)
            {
                float y=.05f+1.9f*(i+H(seed+i,3)*.8f)/bands,w=(.06f+.35f*H(seed+i,5))*open,h=.03f+.05f*H(seed+i,7),dx=(H(seed+i+(int)(age*40),9)-.5f)*.25f*open;
                var c=at+Up*y+side*dx;var col=i%3==0?Magenta:i%3==1?Cyan:White;
                s.Band(false,c-side*w-Up*h,c+side*w-Up*h,c+side*w+Up*h,c-side*w+Up*h,1.3f*fade,1.3f*fade,Lin(col,1.6f));
                s.Band(false,c-depth*w-Up*h,c+depth*w-Up*h,c+depth*w+Up*h,c-depth*w+Up*h,.9f*fade,.9f*fade,Lin(col,1.3f));
            }
            s.Line(false,at+Up*.02f,at+Up*(2.1f*open+.1f),Lin(White,3.2f*fade),.035f,false);
            if(into&&u>.3f&&u<.6f)s.Sprite(false,at+Up*1.05f,Lin(White,3*(1-Math.Abs(u-.45f)/.15f)),1.1f,Hot);
            int px=Count(6,q,low);for(int i=0;i<px;i++){float k=.04f*fade;var o=new Vector3(H(seed+i,11)-.5f,H(seed+i,13)*2,H(seed+i,15)-.5f)*new Vector3(.8f,1,.8f)*(into?1-u:u);s.Voxel(false,WeaponShape.Cube,at+o,Quaternion.Identity,new Vector3(k,k,k*.4f),Lin(i%2==0?Cyan:Magenta,2.2f));}
        }
        private static void WallSplat(ICyberFxSink s,Vector3 at,Vector3 dir,float age,long seed,float q,bool low)
        {
            if(age>.6f)return;float u=age/.6f,fade=1-Smooth(u);var x=Vector3.Normalize(Vector3.Cross(dir,Up)+new Vector3(1e-4f,0,0));
            s.Sprite(false,at,Lin(White,4*fade*fade),.8f,Hot);
            int n=low?5:9;for(int i=0;i<n;i++){float a0=i*MeleeMath.TAU/n+H(seed+i,3)*.4f,len=(.5f+.7f*H(seed+i,5))*Smooth(u*3);var d=x*(float)Math.Cos(a0)+Up*(float)Math.Sin(a0);
                s.Line(false,at+dir*.05f,at+dir*.05f+d*len,Lin(i%2==0?Cyan:Magenta,2.4f*fade),.03f,true);}
        }
        private static void Shatter(ICyberFxSink s,Vector3 at,float yaw,float age,int color,long seed,float q,bool low)
        {
            if(age>.45f)return;float u=age/.45f;int n=low?8:Count(18,q,low);
            for(int i=0;i<n;i++)
            {
                var o=new Vector3(H(seed+i,3)-.5f,H(seed+i,5)*1.9f,H(seed+i,7)-.5f)*new Vector3(.55f,1,.4f);var d=Vector3.Normalize(new Vector3(o.X,.4f,o.Z)+new Vector3(0,0,1e-4f));
                float k=.06f*(1-u)*(.6f+.6f*H(seed+i,9));var p=at+Vector3.Transform(o,Quaternion.CreateFromAxisAngle(Up,yaw*(float)Math.PI/180))+d*(u*1.4f)-Up*(2.2f*age*age);
                s.Voxel(false,WeaponShape.Cube,p,Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1,H(seed+i,11),.4f)),age*14),new Vector3(k,k,k),Lin(i%3==0?White:color,2.2f));
            }
        }
        /// <summary>Step 5's falling column of code: glyph blocks raining down in rings onto the landing point from 1.75 s, the
        /// impact at 1.9 s with its 5 m shockwave on others, the column glitching until 2.6 s, then dissolving upward.</summary>
        public const float ColumnRadius=.95f,ColumnHeight=9,ColumnFall=.15f;
        private static void CodeColumn(ICyberFxSink s,Vector3 land,float age,long seed,float q,bool low)
        {
            float start=KernelPanicPath.Steps[4]-ColumnFall,end=KernelPanicPath.Recovery+.5f;if(age<start-.1f||age>end)return;bool reduced=q<.5f;
            float fall=Clamp01((age-start)/ColumnFall),dissolve=Clamp01((age-KernelPanicPath.Recovery)/.5f);
            // The streams: columns of glyph blocks in a ring, falling head first, their tails reaching the sky.
            int streams=low?8:Count(16,q,low),glyphs=low?6:10;int step=(int)(age*(reduced?6:20));
            for(int i=0;i<streams;i++)
            {
                float a0=i*MeleeMath.TAU/streams+H(seed+i,3)*.2f,rad=ColumnRadius*(.35f+.65f*H(seed+i,5));var o=new Vector3((float)Math.Cos(a0)*rad,0,(float)Math.Sin(a0)*rad);
                float head=land.Y+ColumnHeight*(1-fall)+(H(seed+i,7)*.6f)*(1-fall);
                for(int g=0;g<glyphs;g++)
                {
                    float y=head+g*.32f+dissolve*(3+2*H(seed+i,9))*dissolve;if(y>land.Y+ColumnHeight+1)continue;float lit=g==0?2.8f:1.6f*(1-g/(float)glyphs);
                    if(H(seed+i*31+g*7+step,11)<.12f)continue;float z=.07f*(1-dissolve*.7f);bool wide=H(seed+i*17+g+step,13)>.5f;
                    s.Voxel(false,WeaponShape.Cube,land+o+Up*(y-land.Y),Quaternion.CreateFromAxisAngle(Up,a0),new Vector3(wide?z*1.6f:z*.7f,z*1.3f,.012f),Lin(g==0?White:H(seed+i+g,15)>.85f?Magenta:Code,lit*(1-dissolve)));
                }
            }
            // The column's glow shaft.
            // (Two crossed planes, bright at the foot and fading up, soft at their sides.)
            float shaft=fall*(1-dissolve);if(shaft>0){s.Band(false,land-Vector3.UnitX*ColumnRadius+Up*.05f,land-Vector3.UnitX*ColumnRadius*.6f+Up*ColumnHeight,land+Vector3.UnitX*ColumnRadius*.6f+Up*ColumnHeight,land+Vector3.UnitX*ColumnRadius+Up*.05f,.6f*shaft,0,Lin(Code,1.2f));
                s.Band(false,land-Vector3.UnitZ*ColumnRadius+Up*.05f,land-Vector3.UnitZ*ColumnRadius*.6f+Up*ColumnHeight,land+Vector3.UnitZ*ColumnRadius*.6f+Up*ColumnHeight,land+Vector3.UnitZ*ColumnRadius+Up*.05f,.6f*shaft,0,Lin(Code,1.2f));}
            // The impact: a blinding core, the upright shockwave racing out 5 m, pixels flung up off the floor, the dust.
            float k=age-KernelPanicPath.Steps[4];
            if(k>=0&&k<.7f)
            {
                float u=Clamp01(k/.32f),fade=1-Smooth(k/.7f);s.Sprite(false,land+Up*.5f,Lin(White,6*fade*fade),1.8f,Hot);s.Glow(land+Up*.4f,Lin(Code,1.1f*fade),0,KernelPanicPath.Shockwave*.6f,Ring,u,seed);
                // (Tall as it leaves, fading as it spreads, gone by the 5 m edge: an upright wall, never a ring left on the floor.)
                float wave=fade*(1-u)*(1-u);Wave(s,land,KernelPanicPath.Shockwave*u,.6f+1.1f*(1-u),Lin(White,1.6f),wave,low?18:36);Wave(s,land,KernelPanicPath.Shockwave*u*.82f,.45f+.8f*(1-u),Lin(Code,1.4f),wave,low?14:28);
                int tiles=low?10:Count(24,q,low);for(int i=0;i<tiles;i++){float a0=H(seed+i,17)*MeleeMath.TAU,d=KernelPanicPath.Shockwave*u*(.6f+.4f*H(seed+i,19));var p=land+new Vector3((float)Math.Cos(a0)*d,.05f+.6f*(float)Math.Sin(Clamp01(u*1.2f)*Math.PI)*H(seed+i,21),(float)Math.Sin(a0)*d);
                    float z=.09f*fade;p.Y+=z*.6f;s.Voxel(false,WeaponShape.Cube,p,Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3((float)Math.Sin(a0),.3f,-(float)Math.Cos(a0))),u*2),new Vector3(z,z*.7f,z),Lin(i%3==0?White:Code,2.2f));}
                if(!low)for(int i=0;i<Count(10,q,low);i++){float a0=i*MeleeMath.TAU/10;var d=new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0));s.Smoke(land+d*(KernelPanicPath.Shockwave*u*.7f)+Up*.2f,Lin(0x2a3550),.5f*fade,.8f+.8f*u,u,seed+i,land.Y);}
            }
        }
    }

    /// <summary>
    /// Kernel Panic's feel for its two participants (presentation only; the simulation clock is never touched): brief
    /// hit-stops on each step's hit and a slow beat round the final slam, as a warp of the presentation clock their poses,
    /// effects and cameras read (KernelPanicFeel.Clock), plus a glitch colour shift on their screens. Third-party viewers see the
    /// unwarped choreography. Reduced effects halve the holds and soften the slow beat. The warp is monotone (the lag is the
    /// largest of its pieces, each growing no faster than real time), continuous, and back on the match clock before the
    /// recovery ends, so nothing drifts.
    /// </summary>
    public static class KernelPanicFeel
    {
        /// <summary>Holds (s) on the five steps' hits and on each clone strike.</summary>
        public static readonly float[] Holds={.07f,.055f,.06f,.035f,.09f};
        public const float CloneHold=.03f,SlowRate=.35f,SlowLead=.12f,SlowHold=.32f;
        /// <summary>Whether `local` is the caster or the victim of this sequence.</summary>
        public static bool Participant(CyberLordView v,int local)=>local!=0&&(v.Id==local||v.PanicVictim==local);
        // A warp piece: from `at`, the clock holds for `hold`, then runs `catchUp` faster than real time until it catches up.
        private static float Hold(float t,float at,float hold,float catchUp)
        {
            if(t<=at)return 0;if(t<=at+hold)return t-at;
            float back=(t-at-hold)*catchUp;return Math.Max(0,hold-back);
        }
        /// <summary>The participants' presentation time at match time `time` (time minus the held lag; equal outside the sequence).</summary>
        public static float Clock(CyberLordView v,float time,bool reduced)
        {
            if(!(v.PanicStart>-90))return time;float age=time-v.PanicStart;if(age<-.01f||age>KernelPanicPath.End)return time;
            float k=reduced?.5f:1,lag=0;
            for(int i=0;i<5;i++)lag=Math.Max(lag,Hold(age,KernelPanicPath.HitAge(i+1),Holds[i]*k,i==4?.6f:2.5f));
            for(int i=0;i<KernelPanicPath.Clones.Length;i++)lag=Math.Max(lag,Hold(age,KernelPanicPath.Clones[i],CloneHold*k,3));
            // The slow beat round the slam: the clock runs at SlowRate from SlowLead before it for SlowHold, then catches up.
            float s0=KernelPanicPath.Steps[4]-SlowLead,slow=reduced?.6f:SlowRate;
            if(age>s0){float inSlow=Math.Min(age-s0,SlowHold);float behind=inSlow*(1-slow);float after=age-(s0+SlowHold);if(after>0)behind=Math.Max(0,behind-after*.9f);lag=Math.Max(lag,behind);}
            return time-Math.Max(0,lag);
        }
        /// <summary>The participants' screen glitch at match time `time` (NativeGlitchScreen; zero outside the sequence).</summary>
        public static NativeSheldonGlitch Screen(CyberLordView v,float time,bool reduced,bool low)
        {
            var g=new NativeSheldonGlitch{Passes=low?1:2,Seed=(int)Math.Floor(Math.Max(0,time)*(reduced?6:24))};
            if(!(v.PanicConnect>-90)||time<v.PanicConnect)return g;float age=time-v.PanicStart;
            float Hit(float at,float decay){float s=age-at;if(s<0)return 0;float e=(float)Math.Exp(-s/decay);return reduced?e*MeleeMath.Smooth(s/.06f)*.5f:e;}
            // The cut-in's connect, every step's hit, a heavier shift on the slam.
            float c=time-v.PanicConnect;if(c>=0&&c<.25f){float e=(float)Math.Exp(-c/.08f);g.Split+=(reduced?.25f:.8f)*e;g.Tear+=(reduced?0:.5f)*e;}
            for(int i=0;i<5;i++){float h=Hit(KernelPanicPath.HitAge(i+1),i==4?.22f:.1f);g.Split+=(i==4?.9f:.45f)*h;g.Chroma+=(i==4?.7f:.3f)*h;if(!reduced)g.Tear+=(i==4?.6f:.25f)*h;}
            if(!reduced)for(int i=0;i<KernelPanicPath.Clones.Length;i++){float h=Hit(KernelPanicPath.Clones[i],.07f);g.Split+=.3f*h;g.Blocks+=.4f*h;}
            float slam=Hit(KernelPanicPath.Steps[4],.5f);g.Scan+=.25f*slam;g.Noise+=(reduced?0:.2f)*slam;
            g.Split=Math.Min(1,g.Split);g.Tear=Math.Min(1,g.Tear);g.Chroma=Math.Min(1,g.Chroma);if(low)g.Blocks=0;return g;
        }
    }
}
