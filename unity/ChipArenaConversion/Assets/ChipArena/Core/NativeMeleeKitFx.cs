using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Where the melee Q/E effects draw (Runtime: NativeMeleeKitPresentation; the offline render harness and Tests record it).
    /// Glow/Streak/Smoke go to the shared effects renderer (NativeVfxDraw: world, sorted, soft); Sprite/Line/Band go to the
    /// weapons drawer (NativeWeaponFxDraw), on the world camera or, with view, the first-person view-model camera. Positions
    /// are Unity world metres; colours linear (HDR above 1).
    /// </summary>
    public interface IKitFxSink
    {
        void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed);
        void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed);
        void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor);
        void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile);
        void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard);
        /// <summary>A soft band quad from world corners (a, b along one edge; d, c along the other), its brightness per corner.</summary>
        void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color);
    }
    /// <summary>
    /// What the effects read of the presented frame: the replicated actors, where each is drawn, its posed blades and chest
    /// (third person; the first-person viewer's from its view weapon), its weapon colour, and the melee projectiles in flight.
    /// </summary>
    public interface IKitFxScene
    {
        IReadOnlyList<Actor> Actors{get;}
        /// <summary>The drawn feet position (the smoothed root the rig stands on).</summary>
        Vector3 Feet(Actor a);
        bool Chest(Actor a,out Vector3 chest);
        bool Blade(Actor a,bool left,out Vector3 root,out Vector3 tip);
        /// <summary>The local first-person pilot's id (0: none); its own body effects go to the view layer.</summary>
        int Viewer{get;}
        bool ViewBlade(bool left,out Vector3 root,out Vector3 tip);
        int WeaponColor(Actor a);
        int Shots{get;}
        bool Shot(int index,out Vector3 at,out Vector3 velocity,out string fx,out int owner);
    }

    /// <summary>
    /// The melee Q/E effects (owner: melee on par with ranged/magic, more gap closers; Claude's presentation of Astra's kit),
    /// each a pure function of what host, guests, spectators and the killcam all hold: every pilot's replicated MeleeKit
    /// (Kind/Phase/At/Release/Until/Yaw, Afterimage, EmpowerUntil, MarkTarget/MarkUntil, ShieldOut/ShieldPosition, Fissure*,
    /// SuperArmour, Absorbed) and the fx-tagged events (AbilityCast, AbilityHit, Mark, Teleport, Recast, Absorb, Bounce, Land,
    /// FissureStart), seeded by event id or cast time, aged on the presentation clock. Per weapon accent: the sword's cold steel
    /// cyan, the daggers' violet shadow with a mint edge, the shield's guardian blue, the greatsword's molten amber; the Kunai
    /// mark is red for every viewer. Effects quality (Reduced, .3) thins the particles; the low graphics tier halves sprites and
    /// drops the smoke. Nothing here reaches the authority, the network frame, hit timing, hitboxes, damage or movement.
    /// </summary>
    public sealed class NativeMeleeKitFx
    {
        public struct Record{public int Id,Source,Target;public EventKind Kind;public float Time,Amount,Radius;public Vector3 Position,From,Normal;public string Fx;}
        private readonly List<Record> records=new List<Record>();private int cursor;private float clock=float.NegativeInfinity;
        public IReadOnlyList<Record> Records=>records;
        public const int MaxRecords=96;public const float RecordSeconds=1.6f;
        // Glow profiles: NativeVfxDraw (Mote, CoreGlow, Ring, Snow) and NativeWeaponFxDraw (Soft, HotCore, RingProfile, Sparkle).
        public const int Mote=0,Core=1,Ring=2,Snow=3,Soft=0,Hot=1,RingSprite=2,Sparkle=3;
        /// <summary>The kit's fx tags (the AbilityCast/AbilityHit/... Detail); other events are not this effect's.</summary>
        public static bool Tag(string fx)=>MeleeKitMotion.Known(fx);
        public static bool Kinds(EventKind k)=>k==EventKind.AbilityCast||k==EventKind.AbilityHit||k==EventKind.Mark||k==EventKind.Teleport||k==EventKind.Recast||k==EventKind.Absorb||k==EventKind.Bounce||k==EventKind.Land||k==EventKind.FissureStart;
        public void Clear(){records.Clear();cursor=0;clock=float.NegativeInfinity;}
        /// <summary>Reads the fresh kit events (idempotent per frame). Ids restart with a new arena or the killcam's replica, and its
        /// clock rewinds: both start over.</summary>
        public void Observe(IReadOnlyList<ArenaEvent> events,float time)
        {
            if(events==null)return;int newest=0;for(int i=0;i<events.Count;i++)if(events[i].Id>newest)newest=events[i].Id;
            if(newest>0&&newest<cursor||time<clock-.25f){records.Clear();cursor=0;}
            clock=time;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];if(e.Id<=cursor||!Kinds(e.Kind)||!Tag(e.Detail)||time-e.Time>RecordSeconds)continue;
                if(records.Count>=MaxRecords)records.RemoveAt(0);
                records.Add(new Record{Id=e.Id,Source=e.Source,Target=e.Target,Kind=e.Kind,Time=e.Time,Amount=e.Amount,Radius=e.Radius,Position=N(e.Position),From=N(e.From),Normal=N(e.Normal),Fx=e.Detail});
            }
            if(newest>cursor)cursor=newest;
            for(int i=records.Count-1;i>=0;i--)if(time-records[i].Time>RecordSeconds)records.RemoveAt(i);
        }
        private static Vector3 N(V3 v)=>new Vector3(v.x,v.y,v.z);

        // ---- the Shadow Step decoy model and the dash ghosts (Runtime draws them as weapon ghosts) ----
        public const int DashGhostSlots=12;public const float DashGhostSpacing=.9f,DashGhostLife=.3f;
        /// <summary>A Flash Step or Bulwark Charge pilot in its replicated active (dash) phase.</summary>
        public static bool Dashing(Actor a,float time)=>MeleeKitMotion.Clock(a,time,out var c)&&c.Part==KitPart.Active&&(c.Kind=="flash-step"||c.Kind=="bulwark-charge");
        public static float DashGhostFade(float age)=>age<0||age>=DashGhostLife?0:.6f*(float)Math.Pow(1-age/DashGhostLife,1.4);
        public static FxColor DashTint(string spec,int weapon){var c=Edge(spec,weapon)*1.4f;return new FxColor(c.X,c.Y,c.Z);}

        // ---- palette (sRGB hex; linear on use) ----
        public const int SwordCore=0xe6fdff,SwordEdge=0x7fe8ff,DaggerCore=0xb48cff,DaggerEdge=0x6effc8,DaggerSmoke=0x1c1430,ShieldCore=0xe4f2ff,ShieldEdge=0x5cb4ff,
            GreatCore=0xffe2a8,GreatEdge=0xff8a2a,Ember=0xff5a1e,Dust=0x8c7d6a,MarkRed=0xff2440,MarkCore=0xffd0d6,ArmourGold=0xffd56b;
        public static Vector3 Lin(int hex,float gain=1){NativeBlastSim.Linear(hex,out double r,out double g,out double b);return new Vector3((float)r,(float)g,(float)b)*gain;}
        /// <summary>The weapon's accent edge: its content colour pulled toward the kit hue so each weapon reads apart.</summary>
        public static Vector3 Edge(string spec,int weapon)
        {
            int kit=spec=="daggers"?DaggerEdge:spec=="shield"?ShieldEdge:spec=="greatsword"?GreatEdge:SwordEdge;
            return weapon>0?Vector3.Lerp(Lin(weapon),Lin(kit),.65f):Lin(kit);
        }
        public static Vector3 CoreOf(string spec)=>Lin(spec=="daggers"?DaggerCore:spec=="shield"?ShieldCore:spec=="greatsword"?GreatCore:SwordCore);
        private static float H(long k,uint salt){unchecked{uint h=(uint)k*0x9e3779b1u^salt*0x85ebca6bu;h^=h>>15;h*=0x2c1b3c6du;h^=h>>12;h*=0x297a2d39u;h^=h>>15;return (h&0xffffff)/16777216f;}}
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        private static Vector3 Forward(float yawRadians)=>new Vector3((float)Math.Sin(yawRadians),0,(float)Math.Cos(yawRadians));
        private static Vector3 Right(Vector3 f)=>new Vector3(f.Z,0,-f.X);
        private static Vector3 Flat(Vector3 v,Vector3 fallback){v.Y=0;return v.LengthSquared()>1e-6f?Vector3.Normalize(v):fallback;}
        private static int Count(int full,float q,bool low){float n=full*Math.Max(.35f,Math.Min(1,q));if(low)n*=.5f;return Math.Max(1,(int)Math.Round(n));}

        /// <summary>Draws every live kit effect of the frame at `time`.</summary>
        public void Draw(IKitFxSink sink,IKitFxScene scene,float time,float quality,bool low)
        {
            if(sink==null||scene?.Actors==null)return;var actors=scene.Actors;
            for(int i=0;i<actors.Count;i++)
            {
                var a=actors[i];if(a==null)continue;var k=a.MeleeKit;if(k==null)continue;
                // Effects that outlive the cast (any pilot state): the fissure, the decoy's smoke, the mark, the flying shield.
                if(!a.Alive)continue;
                if(MeleeKitMotion.Clock(a,time,out var c))Cast(sink,scene,a,c,time,quality,low);
            }
            for(int i=0;i<records.Count;i++)Event(sink,scene,records[i],time,quality,low);
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Casts: telegraphs (windup, recast anticipation) and the active-phase streaks
        // ---------------------------------------------------------------------------------------------------------------
        private void Cast(IKitFxSink s,IKitFxScene scene,Actor a,in KitClock c,float time,float q,bool low)
        {
            var feet=scene.Feet(a);var f=Forward(a.MeleeKit.Yaw);var r=Right(f);int weapon=scene.WeaponColor(a);var edge=Edge(c.Spec,weapon);var core=CoreOf(c.Spec);
            bool view=scene.Viewer==a.Id;long seed=a.Id*7919L+(long)Math.Round(c.At*240);
            float wind=c.Windup,u=Clamp01((time-c.At)/wind);bool lead=c.Part==KitPart.Windup||c.Part==KitPart.Recast;
            Vector3 chest=scene.Chest(a,out var ch)?ch:feet+new Vector3(0,1.25f,0);
            // The weapon's glint, growing through every windup (on the view weapon for the caster in first person).
            if(lead)
            {
                bool hasBlade=view?scene.ViewBlade(false,out var b0,out var b1):scene.Blade(a,false,out b0,out b1);
                float pulse=.6f+.4f*(float)Math.Sin(time*38+a.Id);
                if(hasBlade){Vector3 at=c.Spec=="shield"?(b0+b1)*.5f:b1;s.Sprite(view,at,edge*(1.6f+2.2f*u)*pulse,view?.09f+.07f*u:.22f+.3f*u,Hot);if(!low)s.Sprite(view,at,core*(1.2f*u),view?.16f:.6f*u,Sparkle);}
            }
            switch(c.Kind)
            {
                case "flash-step":
                    if(lead)Path(s,feet,f,7,u,edge,core,seed,low,true);
                    else if(c.Part==KitPart.Active&&!view)Dash(s,feet,f,r,time-c.Release,edge,core,seed,q,low,.22f);
                    break;
                case "rising-dragon":
                    if(lead&&!c.Recast)Updraft(s,feet,time-c.At,u,edge,core,seed,q,low);
                    else if(lead)Upward(s,feet,chest,u,edge,core,seed,low);
                    break;
            }
        }
        // A telegraph line on the floor along the cast heading, filling out to `range` m as the windup runs, a chevron at its end.
        private static void Path(IKitFxSink s,Vector3 feet,Vector3 f,float range,float u,Vector3 edge,Vector3 core,long seed,bool low,bool chevron)
        {
            float reach=range*Smooth(u*1.4f),y=feet.Y+.03f,alpha=Smooth(u/.3f);if(reach<.1f)return;var r=Right(f);var o=new Vector3(feet.X,y,feet.Z)+f*.35f;
            int n=low?4:8;
            for(int i=0;i<n;i++)
            {
                float a0=i/(float)n,a1=(i+1)/(float)n;Vector3 p0=o+f*(reach*a0),p1=o+f*(reach*a1);float w0=.05f+.04f*a0,w1=.05f+.04f*a1;
                float b0=alpha*(.35f+.65f*a0),b1=alpha*(.35f+.65f*a1);s.Band(false,p0-r*w0,p1-r*w1,p1+r*w1,p0+r*w0,b0,b1,edge*1.6f);
            }
            if(chevron)
            {
                Vector3 tip=o+f*reach;for(int side=-1;side<=1;side+=2){Vector3 back=tip-f*.35f+r*(.28f*side);s.Band(false,back-f*.04f,tip-f*.04f,tip+f*.04f,back+f*.04f,alpha,alpha,core*2f);}
                s.Glow(tip+new Vector3(0,.05f,0),edge*(1.4f*alpha),0,.35f,Core,u,seed);
            }
        }
        // A dash: wind streaks raking back past the body, a bright trail at the waist, dust kicked from the feet.
        private static void Dash(IKitFxSink s,Vector3 feet,Vector3 f,Vector3 r,float t,Vector3 edge,Vector3 core,long seed,float q,bool low,float span)
        {
            float u=Clamp01(t/span);int n=Count(10,q,low);
            for(int i=0;i<n;i++)
            {
                float h=H(seed+i,3),side=(H(seed+i,4)-.5f)*1.1f,up=.25f+1.5f*H(seed+i,5),ph=(t*7+h)%1f;
                Vector3 head=feet+r*side+Vector3.UnitY*up+f*(.5f-ph*1.6f);s.Streak(head,-f*(10+8*h),edge*(2.4f*(1-ph)),.035f,.5f,.06f,.9f,ph,seed+i);
            }
            s.Streak(feet+Vector3.UnitY*.85f-f*.25f,-f*30,core*1.5f,.06f,1.4f,.06f,.9f,u,seed);
            if(!low)for(int i=0;i<Count(3,q,low);i++){float a=(t*12+i/3f)%1f;s.Smoke(feet-f*(.4f+1.2f*a)+r*((H(seed+i,7)-.5f)*.6f)+Vector3.UnitY*.12f,Lin(Dust,.9f),.55f*(1-a),.4f+.5f*a,a,seed+i,feet.Y);}
        }
        // Rising Dragon's coil: a wind spiral climbing round the caster.
        private static void Updraft(IKitFxSink s,Vector3 feet,float t,float u,Vector3 edge,Vector3 core,long seed,float q,bool low)
        {
            int n=Count(14,q,low);
            for(int i=0;i<n;i++)
            {
                float k=i/(float)n,ang=k*12.566f+t*9,rad=.75f-.25f*k,y=.1f+1.7f*((k+t*1.5f)%1f);Vector3 p=feet+new Vector3((float)Math.Cos(ang)*rad,y,(float)Math.Sin(ang)*rad);
                Vector3 tan=new Vector3(-(float)Math.Sin(ang),.55f,(float)Math.Cos(ang));s.Streak(p,tan*5,edge*(1.7f*u),.03f,.18f,.05f,.9f,k,seed+i);
            }
            GroundRing(s,feet,.7f,.12f,edge*1.8f,u,low?12:18);
        }
        // Skyfall's anticipation: streaks pulling up from the caster toward where it leaps.
        private static void Upward(IKitFxSink s,Vector3 feet,Vector3 chest,float u,Vector3 edge,Vector3 core,long seed,bool low)
        {
            for(int i=0;i<(low?3:6);i++){float h=H(seed+i,21);Vector3 p=feet+new Vector3((h-.5f)*.8f,.2f+2.2f*u*H(seed+i,22),(H(seed+i,23)-.5f)*.8f);s.Streak(p,Vector3.UnitY*12,edge*(2f*u),.035f,.6f,.06f,.9f,u,seed+i);}
            s.Glow(chest,core*(1.5f*u),0,.7f,Core,u,seed);
        }


        // ---------------------------------------------------------------------------------------------------------------
        // Events
        // ---------------------------------------------------------------------------------------------------------------
        private void Event(IKitFxSink s,IKitFxScene scene,in Record e,float time,float q,bool low)
        {
            float age=time-e.Time;if(age<0)return;string spec=MeleeKitMotion.Spec(e.Fx);var src=Find(scene,e.Source);int weapon=src!=null?scene.WeaponColor(src):0;
            var edge=Edge(spec,weapon);var core=CoreOf(spec);long seed=e.Id*1000003L+17;
            switch(e.Kind)
            {
                case EventKind.Teleport:Teleport(s,e,age,edge,core,seed,q,low,scene.Viewer==e.Source);break;
                case EventKind.AbilityHit:Hit(s,scene,e,age,edge,core,seed,q,low);break;
                case EventKind.Land:Land(s,e,age,edge,core,seed,q,low);break;
                case EventKind.Recast:Recast(s,scene,e,age,edge,core,seed,q,low);break;
                case EventKind.AbilityCast:Cast(s,e,age,edge,core,seed,q,low);break;
            }
        }
        private static void Cast(IKitFxSink s,in Record e,float age,Vector3 edge,Vector3 core,long seed,float q,bool low)
        {
            // A short ring kicked out from the caster's feet as the cast begins (every cast reads, even with the camera behind).
            if(age>.3f)return;float u=age/.3f;Vector3 at=e.Position-new Vector3(0,.865f,0);GroundRing(s,at,.35f+1.1f*Smooth(u*1.4f),.18f*(1-u)+.04f,edge*2.2f,(1-u)*(1-u),low?12:20);
        }
        private static void Teleport(IKitFxSink s,in Record e,float age,Vector3 edge,Vector3 core,long seed,float q,bool low,bool viewer)
        {
            if(age>.7f)return;bool shadow=e.Fx=="shadow-step",kunai=e.Fx=="kunai-mark";Vector3 from=e.From,to=e.Position,lift=new Vector3(0,-.1f,0);
            var tint=kunai?Lin(MarkRed):edge;var smoke=shadow||kunai?Lin(DaggerSmoke):Lin(0x6f8796);float floorFrom=from.Y-.865f,floorTo=to.Y-.865f;
            Vector3 d=to-from;float len=d.Length();
            // The path: a thin line of light collapsing toward the arrival (.2 s); the shadow's trail leaves smoke along it.
            if(age<.2f&&len>.1f){float u=age/.2f;s.Line(false,Vector3.Lerp(from,to,Smooth(u))+lift,to+lift,tint*(2.4f*(1-u)),.05f*(1-u)+.01f,false);}
            if(shadow&&!low&&len>.1f&&age<.6f){int n=Count(6,q,low);float u=age/.6f;for(int i=0;i<n;i++){float t=(i+.5f)/n;s.Smoke(Vector3.Lerp(from,to,t)+new Vector3(0,.1f+.3f*H(seed+i,82),0),smoke,.55f*(1-u)*(1-t*.4f),.35f+.3f*u,u,seed+i+20,floorFrom+(floorTo-floorFrom)*t);}}
            // Departure: the pilot's smoke left where it stood. Arrival: a puff and a ring of light kicked out at the feet.
            int m=Count(6,q,low);
            for(int i=0;i<m;i++)
            {
                float h=H(seed+i,81),a=i/(float)m*6.2832f;var dir=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));
                if(!low&&age<.55f){float u=age/.55f;s.Smoke(from+dir*(.15f+.25f*u)+new Vector3(0,-.5f+.9f*h,0),smoke,.6f*(1-u),.32f+.3f*u,u,seed+i,floorFrom);if(!viewer)s.Smoke(to+dir*(.15f+.45f*u)+new Vector3(0,-.6f+.5f*h,0),smoke,.45f*(1-u)*(1-u),.3f+.35f*u,u,seed+i+50,floorTo);}
                if(age<.25f&&!viewer){float u=age/.25f;s.Streak(to+dir*(.15f+.9f*u)+new Vector3(0,-.75f+.35f*h,0),dir*(5*(1-u)),tint*(2f*(1-u)),.025f,.15f,.04f,.9f,u,seed+i+90);}
            }
            if(age<.35f){float u=age/.35f;GroundRing(s,new Vector3(to.X,floorTo,to.Z),.3f+1.2f*Smooth(u*1.3f),.14f*(1-u)+.03f,tint*2f,(1-u)*(1-u),low?12:20);s.Glow(from,tint*(1.2f*(1-u)),0,.9f*(1-u)+.2f,Core,u,seed+1);}
        }
        private void Hit(IKitFxSink s,IKitFxScene scene,in Record e,float age,Vector3 edge,Vector3 core,long seed,float q,bool low)
        {
            if(age>.9f)return;Vector3 at=e.Position,n=e.Normal.LengthSquared()>1e-4f?Vector3.Normalize(e.Normal):Vector3.UnitY;int count=Count(10,q,low);
            // Every ability hit: a hot flash and sparks back along the contact normal, in the weapon's accent.
            if(age<.25f)
            {
                float u=age/.25f;s.Glow(at,core*(3f*(1-u)*(1-u)),0,.5f+.5f*u,Core,u,seed);s.Glow(at,edge*(2f*(1-u)),0,.4f+1.3f*u,Ring,u,seed+1);
                for(int i=0;i<count;i++){var dir=Vector3.Normalize(n*1.2f+new Vector3(H(seed+i,91)-.5f,H(seed+i,92)-.3f,H(seed+i,93)-.5f)*1.6f);float sp=5+7*H(seed+i,94);s.Streak(at+dir*(sp*age),dir*sp,edge*(2.8f*(1-u)),.03f,.12f,.035f,.9f,u,seed+i);}
            }
            switch(e.Fx)
            {
                case "rising-dragon":
                {
                    // The launched target rides a wind spiral up for half a second (Skyfall's own strike is the plunge's flash).
                    var t=Find(scene,e.Target);if(t==null||age>.55f||(Find(scene,e.Source)?.SpecialKind=="skyfall"))break;Vector3 f=scene.Feet(t);float u=age/.55f;
                    for(int i=0;i<Count(10,q,low);i++){float k=i/10f,ang=k*12.566f+age*14,rad=.55f;Vector3 p=f+new Vector3((float)Math.Cos(ang)*rad,.2f+2f*k*Smooth(u*1.5f),(float)Math.Sin(ang)*rad);s.Streak(p,new Vector3(-(float)Math.Sin(ang),.8f,(float)Math.Cos(ang))*5,edge*(2.2f*(1-u)),.03f,.2f,.05f,.9f,u,seed+i+40);}
                    break;
                }
                case "flash-step":
                    // The thrust pierces: a line through the target along the dash.
                    if(age<.2f){float u=age/.2f;var d=-n;d.Y=0;if(d.LengthSquared()>1e-4f){d=Vector3.Normalize(d);s.Line(false,at-d*.6f,at+d*(1.4f+1.2f*u),core*(3.5f*(1-u)),.06f*(1-u)+.01f,false);}}
                    break;
            }
        }
        private static void Land(IKitFxSink s,in Record e,float age,Vector3 edge,Vector3 core,long seed,float q,bool low)
        {
            bool wolf=e.Fx=="wolf-knight";float life=wolf?.7f:.55f;if(age>life)return;float u=age/life,radius=e.Radius>0?e.Radius:(wolf?3.7f:2);
            Vector3 c=e.Position-new Vector3(0,.84f,0);var ring=wolf?Lin(ArmourGold):edge;var hot=wolf?Lin(GreatCore):core;
            // The wave: a bright band on the floor racing out to the strike radius, fading as it goes.
            float rr=radius*Smooth(Math.Min(1,age/(wolf?.28f:.22f))),width=.25f+.35f*(1-u),b=(1-u)*(1-u);int n=low?16:28;
            for(int i=0;i<n;i++)
            {
                float a0=i/(float)n*6.2832f,a1=(i+1)/(float)n*6.2832f;Vector3 d0=new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0)),d1=new Vector3((float)Math.Cos(a1),0,(float)Math.Sin(a1));Vector3 y=new Vector3(0,.03f,0);
                s.Band(false,c+y+d0*Math.Max(0,rr-width),c+y+d1*Math.Max(0,rr-width),c+y+d1*rr,c+y+d0*rr,b,b,ring*2.6f);
            }
            if(age<.18f){float k=age/.18f;s.Glow(c+Vector3.UnitY*.3f,hot*(3.5f*(1-k)),0,1.2f+1.5f*k,Core,k,seed);}
            // The Skyfall's blade-light pillar; the Wolf Knight's thrown rock and dust.
            if(!wolf&&age<.3f){float k=age/.3f;s.Line(false,c,c+Vector3.UnitY*(2.4f*(1-k*.4f)),core*(2.6f*(1-k)),.09f*(1-k)+.015f,false);}
            for(int i=0;i<Count(wolf?14:9,q,low);i++)
            {
                float a=i/(float)Count(wolf?14:9,q,low)*6.2832f+H(seed+i,111);var d=new Vector3((float)Math.Cos(a),0,(float)Math.Sin(a));
                if(age<.45f){float k=age/.45f;var v=d*(4+3*H(seed+i,112))+Vector3.UnitY*(3+3*H(seed+i,113));s.Streak(c+d*.3f+v*age+new Vector3(0,-11.5f*age*age,0)+Vector3.UnitY*.1f,v-new Vector3(0,23*age,0),hot*(2.4f*(1-k)),.035f,.12f,.03f,.9f,k,seed+i);}
                if(!low)s.Smoke(c+d*(rr*.9f)+Vector3.UnitY*.15f,Lin(Dust),.6f*(1-u),.5f+.6f*u,u,seed+i+40,c.Y);
            }
        }
        private static void Recast(IKitFxSink s,IKitFxScene scene,in Record e,float age,Vector3 edge,Vector3 core,long seed,float q,bool low)
        {
            // The recast's tell: a thin line from the caster to its target for the anticipation (.2 s), red for the Kunai.
            if(age>.22f)return;float u=age/.22f;var t=Find(scene,e.Target);Vector3 to=t!=null?(scene.Chest(t,out var c)?c:scene.Feet(t)+Vector3.UnitY*1.2f):e.Position;
            var tint=e.Fx=="kunai-mark"?Lin(MarkRed):edge;var src=Find(scene,e.Source);Vector3 from=src!=null?(scene.Chest(src,out var sc)?sc:scene.Feet(src)+Vector3.UnitY*1.2f):e.From;
            float flick=.6f+.4f*(float)Math.Sin(age*120);s.Line(false,from,Vector3.Lerp(from,to,Smooth(u*2)),tint*(2.2f*flick),.025f,false);
            s.Glow(to,tint*(1.6f*u*flick),0,.8f,Ring,u,seed);
        }
        /// <summary>A soft ring of light lying on the floor (radius to its outer edge), even all the way round.</summary>
        public static void GroundRing(IKitFxSink s,Vector3 centre,float radius,float width,Vector3 color,float brightness,int segments)
        {
            if(!(radius>.02f)||!(brightness>.002f))return;float inner=Math.Max(0,radius-width);Vector3 y=new Vector3(centre.X,centre.Y+.03f,centre.Z);
            for(int i=0;i<segments;i++)
            {
                float a0=i/(float)segments*6.2832f,a1=(i+1)/(float)segments*6.2832f;var d0=new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0));var d1=new Vector3((float)Math.Cos(a1),0,(float)Math.Sin(a1));
                s.Band(false,y+d0*inner,y+d1*inner,y+d1*radius,y+d0*radius,brightness,brightness,color);
            }
        }
        private static Actor Find(IKitFxScene scene,int id){if(id==0)return null;var l=scene.Actors;for(int i=0;i<l.Count;i++)if(l[i]!=null&&l[i].Id==id)return l[i];return null;}
    }
}
