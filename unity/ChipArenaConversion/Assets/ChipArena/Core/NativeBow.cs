using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// PROVISIONAL: the Bow's proposed replicated data (docs/NEW-KITS.md section 2) as presentation fixtures fill it, until the
    /// simulation's real fields land (Astra). It is never on the wire ([NonSerialized] on Actor) and nothing in play writes it;
    /// only NativeBowWire.Of reads it. When the real names land, NativeBowWire.Of reads them instead and this struct goes.
    /// All times are on the authoritative presentation clock; zero or less means "none".
    /// </summary>
    public struct NativeBowFeed
    {
        /// <summary>The current draw's start (zero while not drawing).</summary>
        public float DrawAt;
        /// <summary>The draw fraction (0..1) of the latest release (its time is the existing Actor.LastShot).</summary>
        public float ReleaseDraw;
        /// <summary>The latest draw cancelled with no shot (RMB, sprint), when the simulation stamps it (optional: else watched).</summary>
        public float CancelAt;
        /// <summary>Volley (Q) and Rain of Arrows (E): cast and release times (else the existing AbilityId/AbilityAt/AbilityRelease).</summary>
        public float VolleyAt,VolleyRelease,RainAt,RainRelease;
        /// <summary>Rain of Arrows' marked point (world, Unity) and circle radius (else NativeBow.RainRadius).</summary>
        public V3 RainTarget;public float RainRadius;public bool HasRainTarget;
    }

    /// <summary>
    /// THE FIELD MAP: the only place the bow presentation names replicated data (Actor fields, shot and hit events). Every
    /// pose, effect, sound and HUD card reads the bow through NativeBowWire.Of / Shot / Arrow, so when the simulation's real
    /// names land (or differ from the proposal) only this class changes. Proposed contract (NEW-KITS.md section 2):
    ///   draw start and the draw fraction at release      Actor.Bow.DrawAt, Actor.Bow.ReleaseDraw; release time = Actor.LastShot
    ///   per-arrow draw fraction on shot events           ArenaEvent Kind Shot, Detail bow / bow-full / volley / rain, Amount = draw,
    ///                                                    Projectile = the arrow's bullet id
    ///   Volley and Rain cast times                       Actor.Bow.VolleyAt/RainAt (or AbilityId volley / rain-of-arrows + AbilityAt/AbilityRelease)
    ///   Rain target point and radius                     Actor.Bow.RainTarget/RainRadius
    ///   full draw time                                   .8 s / Actor.CastRate (attackRate)
    /// </summary>
    public static class NativeBowWire
    {
        public const string Spec="bow",VolleyId="volley",RainId="rain-of-arrows";
        public const string DetailQuick="bow",DetailFull="bow-full",DetailVolley="volley",DetailRain="rain";
        public static bool Is(string spec)=>spec==Spec;
        public static bool Is(Actor a)=>a!=null&&a.SourceSpec==Spec;
        private static bool F(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v)&&Math.Abs(v)<10000000;
        private static float T(float v)=>F(v)&&v>0?v:-100;
        /// <summary>The bow as the presentation reads it from `a` (a non-bow or malformed actor reads as an idle bow). No allocation.</summary>
        public static NativeBow.Read Of(Actor a)
        {
            var r=NativeBow.Read.None;if(!Is(a))return r;r.Bow=true;var f=a.Bow;
            r.DrawAt=T(f.DrawAt);r.ReleaseAt=T(a.LastShot);r.ReleaseDraw=F(f.ReleaseDraw)?NativeBow.Clamp01(f.ReleaseDraw):1;r.CancelAt=T(f.CancelAt);
            float rate=F(a.CastRate)&&a.CastRate>.05f?a.CastRate:1;r.DrawTime=NativeBow.FullDraw/rate;
            r.VolleyAt=T(f.VolleyAt);r.VolleyRelease=Math.Max(r.VolleyAt,T(f.VolleyRelease));
            r.RainAt=T(f.RainAt);r.RainRelease=Math.Max(r.RainAt,T(f.RainRelease));
            // The existing cast stamp (NativeAbilities.PressAbility: AbilityId, AbilityAt, AbilityRelease) when no bow field is set.
            if(r.VolleyAt<0&&a.AbilityId==VolleyId&&T(a.AbilityAt)>0){r.VolleyAt=a.AbilityAt;r.VolleyRelease=Math.Max(a.AbilityAt,T(a.AbilityRelease));}
            if(r.RainAt<0&&a.AbilityId==RainId&&T(a.AbilityAt)>0){r.RainAt=a.AbilityAt;r.RainRelease=Math.Max(a.AbilityAt,T(a.AbilityRelease));}
            r.RainRadius=F(f.RainRadius)&&f.RainRadius>.2f&&f.RainRadius<20?f.RainRadius:NativeBow.RainRadius;
            r.HasRainTarget=f.HasRainTarget&&F(f.RainTarget.x)&&F(f.RainTarget.y)&&F(f.RainTarget.z);r.RainTarget=r.HasRainTarget?new Vector3(f.RainTarget.x,f.RainTarget.y,f.RainTarget.z):Vector3.Zero;
            r.Seed=a.Id;return r;
        }
        /// <summary>A bow shot event: the arrow's kind and draw fraction (Detail and Amount); false for any other event.</summary>
        public static bool Shot(in ArenaEvent e,out NativeBow.Arrow kind,out float draw)
        {
            kind=NativeBow.Arrow.Quick;draw=0;if(e.Kind!=EventKind.Shot||e.Detail==null)return false;
            switch(e.Detail){case DetailQuick:kind=NativeBow.Arrow.Quick;break;case DetailFull:kind=NativeBow.Arrow.Full;break;case DetailVolley:kind=NativeBow.Arrow.Volley;break;case DetailRain:kind=NativeBow.Arrow.Rain;break;default:return false;}
            draw=F(e.Amount)?NativeBow.Clamp01(e.Amount):kind==NativeBow.Arrow.Full?1:kind==NativeBow.Arrow.Volley?NativeBow.VolleyDraw:0;
            if(kind==NativeBow.Arrow.Volley&&!(e.Amount>0))draw=NativeBow.VolleyDraw;return true;
        }
        /// <summary>The arrow's shot event's projectile id (the bullet it names).</summary>
        public static int Projectile(in ArenaEvent e)=>e.Projectile;
        /// <summary>The draw an arrow in flight shows when its shot event was not seen: from its launch speed (45 to 110 m/s).</summary>
        public static float SpeedDraw(float speed)=>F(speed)?NativeBow.Clamp01((speed-NativeBow.SpeedQuick)/(NativeBow.SpeedFull-NativeBow.SpeedQuick)):0;
        /// <summary>A replicated hit that sticks an arrow: a direct Hit or Headshot by a bow pilot (the attacker's spec, from its seat).</summary>
        public static bool Sticks(in ArenaEvent e,string sourceSpec)=>(e.Kind==EventKind.Hit||e.Kind==EventKind.Headshot)&&Is(sourceSpec)&&e.Detail!="field"&&e.DamageKind!="collapse";
        /// <summary>A bow arrow striking the world: an Impact event by a bow pilot.</summary>
        public static bool Lodges(in ArenaEvent e,string sourceSpec)=>e.Kind==EventKind.Impact&&Is(sourceSpec);
    }

    /// <summary>
    /// The Bow's presentation rules and timeline (owner 2026-10-12: "For ranged i want to add a "Bow" class", tap or
    /// hold-to-draw). The numbers are the proposal's (NEW-KITS.md section 2); the simulation (Astra) owns them and may tune them.
    /// Everything here is a pure function of NativeBow.Read (NativeBowWire.Of) and the presentation clock, so host, guests,
    /// spectators and the killcam replay draw the same frame. Nothing here reaches the authority, the network frame, hit
    /// timing, hitboxes, damage, cooldowns or movement.
    /// </summary>
    public static class NativeBow
    {
        // ---- rule numbers the presentation follows (proposal; the simulation's constants replace them when they land) ----
        public const float FullDraw=.8f,QuickBelow=.25f,Nock=.35f,SteadyHold=2.5f,FatigueFull=4f,DrawMove=.7f;
        public const float SpeedQuick=45,SpeedFull=110,GravityQuick=30,GravityFull=14,SpreadQuick=.012f,SpreadFull=.0015f,SpreadFatigue=.01f;
        public const int VolleyArrows=5;public const float VolleyFan=14,VolleyDraw=.7f,VolleySpeed=90,VolleyCooldown=8;
        public const float RainRange=40,RainRadius=3.5f,RainDelay=.6f,RainSpan=1.2f,RainSlowSeconds=1.5f,RainSlow=.2f,RainCooldown=12;public const int RainArrows=12;
        // ---- presentation spans ----
        /// <summary>The release's follow-through (the string hand flies back past the ear), its settle, a draw's let-down, the
        /// bow raised into the draw, the string's buzz after a release, the Volley and sky-shot poses and their pre-draws.</summary>
        public const float Follow=.12f,Settle=.22f,LetDown=.3f,Raise=.16f,Buzz=.22f,VolleyPose=.55f,VolleyPre=.06f,RainPose=.8f,RainPre=.16f;
        public const float LimbFlex=.21f;
        /// <summary>The sky shot's draw (the steep lob: the bow arm reaches up only so far over the bladed lean).</summary>
        public const float SkyDraw=.62f;
        public enum Arrow:byte{Quick,Full,Volley,Rain}
        public enum Phase:byte{Carry,Nock,Draw,Hold,Release,LetDown,Volley,Rain}

        /// <summary>One pilot's bow, sanitised (NativeBowWire.Of). Times -100 when absent.</summary>
        public struct Read
        {
            public bool Bow,HasRainTarget;public float DrawAt,DrawTime,ReleaseAt,ReleaseDraw,CancelAt,VolleyAt,VolleyRelease,RainAt,RainRelease,RainRadius;public Vector3 RainTarget;public int Seed;
            public static Read None=>new Read{DrawAt=-100,DrawTime=FullDraw,ReleaseAt=-100,ReleaseDraw=1,CancelAt=-100,VolleyAt=-100,VolleyRelease=-100,RainAt=-100,RainRelease=-100,RainRadius=NativeBow.RainRadius};
            public bool Drawing(float time)=>DrawAt>0&&time>=DrawAt-.001f&&DrawAt>=ReleaseAt-.001f;
            /// <summary>The draw fraction 0..1 at `time` while drawing (linear in the draw time: the meter), else 0.</summary>
            public float DrawFraction(float time)=>Drawing(time)?Clamp01((time-DrawAt)/Math.Max(.05f,DrawTime)):0;
            /// <summary>Seconds held at full draw (0 before full).</summary>
            public float Held(float time)=>Drawing(time)?Math.Max(0,time-DrawAt-DrawTime):0;
        }

        /// <summary>
        /// The frame's bow: its phase and age, the string's pull (0 brace .. 1 full draw), the draw meter, the fatigue tremor
        /// (0..1), whether an arrow sits on the string (and how far the draw hand is along the nock path from the quiver),
        /// the release's follow-through weight, the bow raised weight (0 carry .. 1 on the aim), the string's buzz, the bow
        /// arm's extension (0 the bow in close with the string hand ahead .. 1 the arm out at full draw: the push-pull draw)
        /// and, for the Volley and sky shot, their cast weights.
        /// </summary>
        public struct View
        {
            public Phase Phase;public float Age,Pull,Meter,Tremor,Held,Follow,Raised,Buzz,Fan,Sky,Nocking,Extend;public bool Nocked,Full;public int Arrows;
        }
        public static float Clamp01(float v)=>v<0?0:v>1?1:v;
        public static float Smooth(float v){v=Clamp01(v);return v*v*(3-2*v);}
        public static float Ease(float v){v=Clamp01(v);return v*v*v*(v*(v*6-15)+10);}
        /// <summary>The string pull shown for draw fraction f: a quick start, settling into the anchor (the pack's draws).</summary>
        public static float PullOf(float f)=>1-(float)Math.Pow(1-Clamp01(f),1.7f);
        /// <summary>The fatigue tremor's weight after `held` seconds at full draw: steady for 2.5 s, rising to full by 4 s.</summary>
        public static float Fatigue(float held)=>Clamp01((held-SteadyHold)/(FatigueFull-SteadyHold));
        /// <summary>The fatigue spread (rad) the simulation proposes: .0015 rising linearly to .01 between 2.5 and 4 s held.</summary>
        public static float FatigueSpread(float held)=>SpreadFull+(SpreadFatigue-SpreadFull)*Fatigue(held);
        public static float Lerp(float a,float b,float t)=>a+(b-a)*t;
        /// <summary>A shot's draw-scaled numbers (damage, speed, gravity): the presentation's own copy for the trail and the HUD.</summary>
        public static float SpeedOf(float draw)=>Lerp(SpeedQuick,SpeedFull,Clamp01(draw));

        /// <summary>The frame's bow at `time`. `watchedCancel` / `watchedDraw`: a let-down the presentation observed (NativeBowWatch)
        /// when the simulation does not stamp CancelAt. No allocation.</summary>
        public static View At(in Read r,float time,float watchedCancel=-100,float watchedDraw=0)
        {
            var v=new View{Phase=Phase.Carry,Nocked=true,Arrows=1,Raised=0};if(!r.Bow)return v;
            float cancel=r.CancelAt>0?r.CancelAt:watchedCancel,cancelDraw=r.CancelAt>0?CancelPull(r):watchedDraw;
            // The newest event at or before `time` owns the frame: a cast, the draw, a release or a let-down.
            float best=-1;int which=0;
            void Pick(float at,int k){if(at>0&&time>=at-.001f&&at>=best){best=at;which=k;}}
            Pick(r.ReleaseAt,1);Pick(r.DrawAt,2);Pick(cancel,3);Pick(r.VolleyAt,4);Pick(r.RainAt,5);
            // A release at the cast's own time is the cast's (Volley and Rain loose their arrows as shots too).
            if(which==1&&r.VolleyAt>0&&Math.Abs(r.ReleaseAt-r.VolleyRelease)<.06f)which=4;
            if(which==1&&r.RainAt>0&&Math.Abs(r.ReleaseAt-r.RainRelease)<.06f)which=5;
            switch(which)
            {
                case 2:
                {
                    float f=r.DrawFraction(time),age=time-r.DrawAt;v.Age=age;v.Meter=f;v.Pull=PullOf(f);v.Raised=Smooth(age/Raise);v.Full=f>=1;
                    v.Phase=f>=1?Phase.Hold:Phase.Draw;v.Held=r.Held(time);v.Tremor=Fatigue(v.Held);v.Extend=v.Pull;
                    // A draw begun inside the last release's nock window finishes nocking first (the draw cannot start before it).
                    if(r.ReleaseAt>0&&r.DrawAt-r.ReleaseAt<Nock){float n=(time-r.ReleaseAt)/Nock;if(n<1){v.Nocking=Clamp01(n);v.Nocked=n>=.86f;}}
                    return v;
                }
                case 1:
                {
                    float age=time-r.ReleaseAt;v.Age=age;v.Follow=FollowWeight(age,r.ReleaseDraw);v.Raised=1-Smooth((age-Follow-Settle*.4f)/(Nock+.25f-Follow));
                    v.Buzz=age<Buzz?(1-age/Buzz):0;v.Extend=Clamp01(r.ReleaseDraw)*(1-Smooth((age-Follow)/(Nock+.15f-Follow)));
                    if(age<Nock){v.Phase=age<Follow?Phase.Release:Phase.Nock;v.Nocking=Clamp01(age/Nock);v.Nocked=age>=Nock*.86f;}
                    return v;
                }
                case 3:
                {
                    float age=time-cancel;v.Age=age;v.Phase=age<LetDown?Phase.LetDown:Phase.Carry;v.Pull=cancelDraw*(1-Smooth(age/LetDown));v.Extend=v.Pull;v.Raised=1-Smooth((age-LetDown*.3f)/(LetDown+.2f));return v;
                }
                case 4:
                {
                    float rel=r.VolleyRelease>r.VolleyAt?r.VolleyRelease:r.VolleyAt,age=time-r.VolleyAt,after=time-rel;v.Age=age;
                    if(age>=VolleyPose+Math.Max(0,rel-r.VolleyAt)){goto case 1;}
                    v.Phase=Phase.Volley;v.Arrows=VolleyArrows;v.Fan=Smooth(Math.Min(age/.05f,(VolleyPose-after)/.18f));
                    // The fan draw over a cast windup when the simulation gives one; an immediate cast snaps to 70% and looses.
                    float pre=rel-r.VolleyAt;if(after<0){v.Pull=VolleyDraw*PullOf(age/Math.Max(.05f,pre));v.Extend=v.Pull;v.Raised=Smooth(age/Math.Min(Raise,Math.Max(.05f,pre)));}
                    else{v.Pull=0;v.Extend=VolleyDraw*(1-Smooth((after-Follow)/(Nock+.15f-Follow)));v.Raised=1-Smooth((after-VolleyPose*.5f)/(VolleyPose*.5f));v.Follow=FollowWeight(after,VolleyDraw);v.Buzz=after<Buzz?1-after/Buzz:0;v.Nocking=Clamp01(after/Nock);v.Nocked=v.Nocking>=.86f;v.Arrows=v.Nocked?1:0;}
                    return v;
                }
                case 5:
                {
                    float rel=r.RainRelease>r.RainAt?r.RainRelease:r.RainAt+RainPre,age=time-r.RainAt,after=time-rel;v.Age=age;
                    if(after>=RainPose){v.Phase=Phase.Carry;return v;}
                    v.Phase=Phase.Rain;v.Sky=Smooth(Math.Min(age/.12f,(RainPose-after)/.3f));v.Raised=v.Sky;
                    if(after<0){v.Pull=SkyDraw*PullOf(age/Math.Max(.05f,rel-r.RainAt));v.Extend=v.Pull;v.Arrows=3;}
                    // After the sky loose the bow arm comes back in before the next arrow is set on the string (the high bow is out of the string hand's reach).
                    else{v.Pull=0;v.Extend=SkyDraw*(1-Smooth((after-Follow)/(Nock-.08f-Follow)));v.Follow=FollowWeight(after,1);v.Buzz=after<Buzz?1-after/Buzz:0;v.Nocking=Clamp01(after/Nock);v.Nocked=v.Nocking>=.86f;v.Arrows=v.Nocked?1:0;}
                    return v;
                }
            }
            return v;
        }
        private static float CancelPull(in Read r)=>r.DrawAt>0&&r.CancelAt>=r.DrawAt?PullOf(Clamp01((r.CancelAt-r.DrawAt)/Math.Max(.05f,r.DrawTime))):0;
        /// <summary>The follow-through weight `age` after a release of draw `draw`: a snap back over the first 40% of Follow,
        /// held, then eased out over Settle; a quick shot's is shorter and smaller.</summary>
        public static float FollowWeight(float age,float draw)
        {
            if(age<0)return 0;float k=.45f+.55f*Clamp01(draw),span=Follow*(.6f+.4f*k);
            if(age<span*.4f)return k*Smooth(age/(span*.4f));if(age<span)return k;return k*(1-Smooth((age-span)/Settle));
        }

        // ---- the fatigue tremor: a deterministic sum of sines (seeded by the pilot), bow-hand frame metres and radians ----
        /// <summary>The tremor of the bow hand at `time` for weight `w` (offset m, and a small turn about its two cross axes, rad).</summary>
        public static void Tremor(int seed,float time,float w,out Vector3 offset,out float yaw,out float pitch)
        {
            offset=Vector3.Zero;yaw=pitch=0;if(w<=0)return;float s=(seed%97)*.37f;
            float a=(float)(Math.Sin(time*17.3+s)*.6+Math.Sin(time*23.9+s*1.7)*.4),b=(float)(Math.Sin(time*19.1+s*2.3)*.55+Math.Sin(time*29.7+s*.6)*.45),c=(float)Math.Sin(time*13.7+s*3.1);
            float k=w*w*(3-2*w);offset=new Vector3(a*.0045f,b*.0055f,c*.002f)*k;yaw=a*.006f*k;pitch=b*.008f*k;
        }

        // ---- the bow frame (the left hand: +x the bow's left, +y up the riser, +z downrange; the fist round the grip) ----
        /// <summary>The arrow line (x, y) beside the riser, the brace (string at rest) and full-draw nock depths, the riser's
        /// axis depth, the limbs' pivots and the string's anchors at the limb tips (both at rest).</summary>
        public const float ArrowX=.012f,ArrowY=.024f,BraceZ=-.125f,FullZ=-.82f,RiserZ=.035f,PivotY=.25f,PivotZ=.02f,TipY=.622f,TipZ=-.125f;
        public const float ArrowLength=.9f,ArrowHead=.065f;
        /// <summary>The grip's centre in the fist (the riser passes through it).</summary>
        public static readonly Vector3 Grip=new Vector3(0,-.06f,RiserZ);
        /// <summary>The nock point (on the string) at pull `pull` (0 brace .. 1 full draw).</summary>
        public static Vector3 NockAt(float pull)=>new Vector3(ArrowX,ArrowY,BraceZ+(FullZ-BraceZ)*Clamp01(pull));
        /// <summary>A limb's flex (rad about +x, positive bends the tips back toward the string) at pull `pull`.</summary>
        public static float Flex(float pull)=>LimbFlex*Clamp01(pull)*(1+.1f*Clamp01(pull));
        /// <summary>A point of the upper (top true) or lower limb, given at rest, after the limb's flex at `pull`.</summary>
        public static Vector3 Limb(bool top,Vector3 rest,float pull)
        {
            float a=Flex(pull)*(top?-1:1),py=top?PivotY:-PivotY,y=rest.Y-py,z=rest.Z-PivotZ,c=(float)Math.Cos(a),s=(float)Math.Sin(a);
            return new Vector3(rest.X,py+y*c-z*s,PivotZ+y*s+z*c);
        }
        /// <summary>The string's anchors at the limb tips at `pull`.</summary>
        public static Vector3 Anchor(bool top,float pull)=>Limb(top,new Vector3(ArrowX,top?TipY:-TipY,TipZ),pull);
        /// <summary>The string's two segments' midpoints offset by the buzz (a decaying vibration after a release, m).</summary>
        public static float StringBuzz(float buzz,float time)=>buzz<=0?0:.012f*buzz*buzz*(float)Math.Sin(time*95);
    }

    /// <summary>
    /// Watches one pilot's replicated draw to see a let-down the simulation does not stamp: the draw start cleared with no
    /// release at or after it. One per presented pilot; the time and the pull it had are what NativeBow.At eases out.
    /// </summary>
    public sealed class NativeBowWatch
    {
        private float lastDraw=-100,lastTime=-100,lastPull;public float CancelAt=-100,CancelPull;
        public void Observe(in NativeBow.Read r,float time)
        {
            if(time<lastTime-.25f||time-lastTime>1){lastDraw=-100;CancelAt=-100;}
            if(lastDraw>0&&r.DrawAt!=lastDraw&&!(r.ReleaseAt>=lastDraw-.001f)&&r.CancelAt<=0){CancelAt=time;CancelPull=lastPull;}
            if(r.DrawAt>0&&r.Drawing(time))lastPull=NativeBow.PullOf(r.DrawFraction(time));
            if(r.DrawAt>0&&CancelAt>0&&r.DrawAt>CancelAt)CancelAt=-100;
            lastDraw=r.DrawAt;lastTime=time;
        }
        public void Clear(){lastDraw=lastTime=CancelAt=-100;lastPull=CancelPull=0;}
    }

    /// <summary>
    /// The bow's HUD: the draw meter near the crosshair (the draw fraction, the quick-shot mark at .25 s of draw, the full-draw
    /// lock and the fatigue warning) and the Q (Volley) / E (Rain of Arrows) cards, drawn by NativeFightHud's cyber card drawer
    /// with the kit's icons. Rules are the simulation's; the HUD reads NativeBowWire.Of and the slots' ready times.
    /// </summary>
    public static class NativeBowHud
    {
        public const string VolleyIcon="ability-volley",RainIcon="ability-rain";
        public struct Meter{public bool Shown,Full,Quick,Fatigue,Release;public float Fill,QuickMark,Alpha,Pulse,Tremor;}
        /// <summary>The meter at `time`: shown while drawing and briefly after a release (holding the released fill).</summary>
        public static Meter MeterOf(in NativeBow.Read r,float time)
        {
            var m=new Meter{QuickMark=NativeBow.Clamp01(NativeBow.QuickBelow/Math.Max(.05f,r.DrawTime))};if(!r.Bow)return m;
            if(r.Drawing(time))
            {
                float f=r.DrawFraction(time),held=r.Held(time);m.Shown=true;m.Fill=f;m.Full=f>=1;m.Quick=time-r.DrawAt<NativeBow.QuickBelow;m.Alpha=NativeBow.Smooth((time-r.DrawAt)/.08f);
                m.Tremor=NativeBow.Fatigue(held);m.Fatigue=m.Tremor>0;m.Pulse=m.Full?NativeBow.Clamp01(1-(held/.18f)):0;return m;
            }
            float age=time-r.ReleaseAt;
            if(r.ReleaseAt>0&&age>=0&&age<.35f){m.Shown=true;m.Release=true;m.Fill=r.ReleaseDraw;m.Full=r.ReleaseDraw>=1;m.Alpha=1-NativeBow.Smooth(age/.35f);}
            return m;
        }
        public struct Card{public string State,Icon;public bool Ready,Active,Low;public float Dial;}
        public static bool Covers(string id)=>id==NativeBowWire.VolleyId||id==NativeBowWire.RainId;
        public static string Icon(string id)=>id==NativeBowWire.VolleyId?VolleyIcon:id==NativeBowWire.RainId?RainIcon:null;
        /// <summary>One card: LOOSING (Volley's pose), MARKED / RAINING with time left (Rain's fall), the cooldown, READY.</summary>
        public static Card Of(string id,in NativeBow.Read r,float time,float remaining,float span)
        {
            var c=new Card{Icon=Icon(id)};
            if(id==NativeBowWire.VolleyId&&r.VolleyAt>0&&time>=r.VolleyAt&&time<Math.Max(r.VolleyRelease,r.VolleyAt)+.25f){c.State="LOOSING";c.Active=true;c.Dial=1;return c;}
            if(id==NativeBowWire.RainId&&r.RainAt>0&&time>=r.RainAt)
            {
                float fall=r.RainAt+NativeBow.RainDelay,end=fall+NativeBow.RainSpan;
                if(time<fall){c.State="MARKED "+(fall-time).ToString("0.0")+"s";c.Active=true;c.Dial=1;return c;}
                if(time<end){c.State="RAINING";c.Active=true;c.Dial=NativeBow.Clamp01((end-time)/NativeBow.RainSpan);return c;}
            }
            if(remaining>0){c.State=remaining.ToString("0.0")+"s";c.Dial=span>0?1-NativeBow.Clamp01(remaining/span):0;return c;}
            c.State="READY";c.Ready=true;c.Dial=1;return c;
        }
    }

    /// <summary>
    /// The bow's sounds on existing NativeSoundBank keys (no new key): the draw's creak (the structure creak, bank only), the
    /// release's thwip (the thrown-dagger whoosh, built-in: the daggers clip, raised) and the arrow's thunk into a wall or the
    /// floor (the wood impact, built-in: the wood clip). Hits on pilots keep the existing body-hit cue.
    /// </summary>
    public static class NativeBowSounds
    {
        public static readonly NativeKitSound Creak=new NativeKitSound{Key="structure.creak",Clip=null,Gain=.22f};
        public static readonly NativeKitSound Thwip=new NativeKitSound{Key="melee.daggers.throw",Clip="daggers",Gain=.42f};
        public static readonly NativeKitSound Thunk=new NativeKitSound{Key="impact.wood",Clip="wood",Gain=.36f};
        /// <summary>The thwip's rate: a full draw a little lower and heavier, a quick shot higher and lighter.</summary>
        public static float ThwipRate(float draw)=>1.35f-.25f*NativeBow.Clamp01(draw);
        /// <summary>The creak's rate (a longer draw creaks lower).</summary>
        public static float CreakRate(float drawTime)=>NativeBow.Clamp01(.8f/Math.Max(.2f,drawTime))*.3f+.75f;
        /// <summary>The creak plays once a draw has lasted this long (a tap shot never creaks).</summary>
        public const float CreakAfter=.18f;
    }
    /// <summary>A live bow piece in the bow frame (source frame): its model's placement (position, rotation, scale).</summary>
    public struct NativeBowPiece
    {
        public Vector3 Position,Scale;public Quaternion Rotation;
        public Vector3 Apply(Vector3 local)=>Position+Vector3.Transform(local*Scale,Rotation);
    }
    /// <summary>
    /// The live bow's pieces in the bow frame for one frame's pull: each limb turned about its pivot by the flex, the two string
    /// segments from the tip anchors to the nock (a unit +z segment scaled to each; the released string buzzing), and the
    /// arrows on the string (the Volley's fanned about the world's up as the canted bow carries it). Pure functions; the
    /// runtime and the checks place the models with them.
    /// </summary>
    public static class NativeBowRig
    {
        public static NativeBowPiece Identity=>new NativeBowPiece{Rotation=Quaternion.Identity,Scale=Vector3.One};
        public static NativeBowPiece Limb(bool top,float pull)
        {
            float a=NativeBow.Flex(pull)*(top?-1:1);var q=Quaternion.CreateFromAxisAngle(Vector3.UnitX,a);var pivot=new Vector3(0,top?NativeBow.PivotY:-NativeBow.PivotY,NativeBow.PivotZ);
            return new NativeBowPiece{Rotation=q,Scale=Vector3.One,Position=pivot-Vector3.Transform(pivot,q)};
        }
        /// <summary>The nock point the string bends to: the pull's nock, buzzing sideways after a release.</summary>
        public static Vector3 Nock(float pull,float buzz,float time)=>NativeBow.NockAt(pull)+new Vector3(NativeBow.StringBuzz(buzz,time),0,0);
        public static NativeBowPiece String(bool top,float pull,float buzz,float time)
        {
            var a=NativeBow.Anchor(top,pull);var d=Nock(pull,buzz,time)-a;float len=d.Length();
            var q=len>1e-6f?CapturedRig.FromTo(Vector3.UnitZ,d/len):Quaternion.Identity;return new NativeBowPiece{Position=a,Rotation=q,Scale=new Vector3(1,1,Math.Max(1e-4f,len))};
        }
        /// <summary>Arrow `i` of `count` on the string at `pull`: its nock on the nock point, along the bow's +z; a fan spreads
        /// them `fanDegrees` in all about the world's up (bow frame (sin cant, cos cant, 0)).</summary>
        public static NativeBowPiece Arrow(float pull,int i=0,int count=1,float fanDegrees=0,float cant=0)
        {
            var q=Quaternion.Identity;
            if(count>1&&fanDegrees>0){float a=(i-(count-1)*.5f)*fanDegrees/(count-1)*(float)Math.PI/180;q=Quaternion.CreateFromAxisAngle(new Vector3((float)Math.Sin(cant),(float)Math.Cos(cant),0),a);}
            return new NativeBowPiece{Position=NativeBow.NockAt(pull),Rotation=q,Scale=Vector3.One};
        }
    }
}

