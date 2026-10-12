using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The pose route a struck pilot is drawn by (Runtime/CapturedAnimation); the hit layer adds what that route lacks.</summary>
    public enum HitVictim{Melee,Fighter,Gun,Magic}

    /// <summary>
    /// The attacker side of a struck pilot's latest reaction, or of a block, recovered on every peer (host, guests, killcam)
    /// from the frame's Hit / Headshot / Block / Emperor / Arcade events and the attacker's replicated actor. No network data.
    /// Melee: a standard swing, special or thrown weapon, a DEV fighter strike, a PvE add's swing or the Siren's claw.
    /// Heft: the attacker's weapon weight on a .5 (light) .. 1.5 (heavy) scale. From: the attacker's position (Unity frame).
    /// </summary>
    public struct HitSource{public bool Known,Melee,Head,HasFrom;public float At,Heft;public V3 From;}

    /// <summary>
    /// Presentation rules for melee hit reactions (presentation only: never read by authority, no change to hit timing,
    /// hitboxes, damage, cooldowns, stun lengths, movement or the network frame). Pure functions, no allocation.
    /// </summary>
    public static class NativeHitRules
    {
        public const float MinHeft=.5f,MaxHeft=1.5f;
        public static bool Standard(string spec)=>spec=="sword"||spec=="daggers"||spec=="greatsword"||spec=="shield";
        public static bool Fighter(string spec)=>spec=="emperor"||spec=="ansatsuken";
        /// <summary>
        /// Weapon heft from the attacker's spec: the content weights (daggers 2, sword 5, shield 8, greatsword 12) map to .6 / 1 /
        /// 1.15 / 1.4 and the Siren's claws to 1.35; a DEV fighter strike or a PvE add (no spec) scales with the blow's damage.
        /// </summary>
        public static float Heft(string spec,float amount)
        {
            float a=amount>0&&amount<1e6f?amount:0;
            switch(spec)
            {
                case "daggers":return .6f;case "sword":return 1;case "shield":return 1.15f;case "greatsword":return 1.4f;case "siren":return 1.35f;
                case "emperor":case "ansatsuken":return MeleeMath.Clamp(.62f+a/40,.75f,1.35f);
                case "pistol":case "rifle":case "machinegun":return .5f;
                case "fire":case "water":case "lightning":case "darkness":case "leet":return .55f;
                default:return MeleeMath.Clamp(.7f+a/60,.7f,1.1f);
            }
        }
        /// <summary>
        /// Whether a Hit/Headshot event was a melee blow. Source 0 with a direct hit is a PvE add's swing (the only direct,
        /// source-less damage); a DEV fighter's strike announces itself with an Emperor/Arcade event at the same time (a Hadoken
        /// does not); every Siren hit is non-direct ("secondary"), and only her claws (melee damage) record a reaction, so a
        /// Siren hit is a claw when `reacted` (the target's replicated reaction is from that tick); her shots, fields and areas
        /// are not. A standard melee pilot only deals direct damage with its weapon (swings, specials, thrown shield and
        /// daggers). Guns and magic never melee.
        /// </summary>
        public static bool Melee(int source,string detail,string spec,bool fighterStrike,bool reacted)
        {
            if(source==0)return detail=="direct";
            if(Fighter(spec))return fighterStrike;
            if(spec=="siren")return reacted&&detail=="secondary";
            return Standard(spec)&&detail=="direct";
        }
        /// <summary>
        /// The impact envelope: 0 before the blow, a cubic ease-out rise to 1 over `rise` (the crisp impact), a `hold` on the
        /// peak (the impact frame), then a recovery that is exactly 0 at rise + hold + fall, with a slight rebound. In [-.02, 1].
        /// </summary>
        public static float Envelope(float age,float rise,float hold,float fall)
        {
            if(!(age>0))return 0;
            if(age<rise){float u=1-age/rise;return 1-u*u*u;}
            age-=rise;if(age<hold)return 1;age-=hold;if(!(fall>0)||age>=fall)return 0;
            float v=age/fall,w=1-v;return w*w*(1+2*v)-.12f*(float)Math.Sin(Math.PI*v)*v;
        }
        /// <summary>
        /// The impact timing for a heft within `span` seconds (the stun, or a light hit's own window): heavier blows rise and
        /// hold longer and recover slower; a span shorter than the minimum shrinks all three so the reaction ends inside it.
        /// </summary>
        public static void Timing(float heft,float span,out float rise,out float hold,out float fall)
        {
            float h=MeleeMath.Clamp(heft,MinHeft,MaxHeft);rise=.022f+.016f*h;hold=.014f+.02f*h;float want=.2f+.14f*h,least=rise+hold+.06f;
            if(!(span>=least)){float k=Math.Max(0,span>0?span:0)/least;rise*=k;hold*=k;fall=.06f*k;return;}
            fall=Math.Min(want,span-rise-hold);
        }
        /// <summary>A light (unstunned) melee hit's own window: longer for heavier weapons.</summary>
        public static float LightSpan(float heft)=>.24f+.16f*MeleeMath.Clamp(heft,MinHeft,MaxHeft);
        /// <summary>
        /// The dazed-stun weight: eases in from .06 s after the stun's first blow `at` (past the impact frame) over .1 s and out
        /// over the last clamp(35%, .06, .16) s of what the latest blow `last` left of the stun (a combo's short extensions keep
        /// a short ease-out); exactly 0 before the blow and from the authoritative stun end, so presentation never extends the
        /// lockout. NativeStunClock holds `at` through a combo.
        /// </summary>
        public static float Daze(float time,float at,float until)=>Daze(time,at,until,at);
        public static float Daze(float time,float at,float until,float last)
        {
            if(!(time>=at)||!(time<until))return 0;float span=until-(last>at?last:at),o=MeleeMath.Clamp(span*.35f,.06f,.16f);
            return MeleeMath.Clamp(MeleeMath.Ease((time-at-.06f)/.1f)*(1-MeleeMath.Ease((time-(until-o))/o)),0,1);
        }
        /// <summary>The fastest the daze climbs back when a blow extends a stun in its ease-out (per second; above the ease-in's 18.75).</summary>
        public const float DazeRise=20;
        /// <summary>A blow's variant (+1 / -1 by its 60 Hz tick); the layer alternates it along a chain, so chained blows read as separate hits.</summary>
        public static float Variant(float at)=>!(Math.Abs(at)<1e6f)?1:(((long)Math.Floor(at*60+.5f))&1)==0?1:-1;
        /// <summary>A native (Unity-frame) world vector in a rig's facing frame (source frame: +x the rig's left), `yaw` in source radians.</summary>
        public static Vector3 Local(V3 world,float yaw)
        {
            float c=(float)Math.Cos(yaw),s=(float)Math.Sin(yaw),x=-world.x,z=world.z;return new Vector3(x*c-z*s,world.y,x*s+z*c);
        }
        /// <summary>Turns a facing-frame vector recorded at `fromYaw` into the frame of a rig facing `toYaw` (source radians).</summary>
        public static Vector3 Reframe(Vector3 d,float fromYaw,float toYaw)
        {
            float a=toYaw-fromYaw,c=(float)Math.Cos(a),s=(float)Math.Sin(a);return new Vector3(d.X*c-d.Z*s,d.Y,d.X*s+d.Z*c);
        }
        /// <summary>The first-person kick: Unity camera-local Euler degrees (x pitch down, y yaw right, z roll left) and offset (metres).</summary>
        public struct Kick{public float Pitch,Yaw,Roll;public Vector3 Offset;public float Weight;}
        public const float KickPitch=3.1f,KickYaw=1.4f,KickRoll=3.2f,KickOffset=.034f;
        /// <summary>The felt-hit share of the melee kick (owner 2026-10-12: hits "more felt"), and each stagger weight's kick for
        /// the hits the melee kick does not cover (bullets, magic, blasts): a flinch, a step and a heavy blow.</summary>
        public const float MeleeKick=1.2f;
        public static float StaggerKick(StaggerWeight w)=>w==StaggerWeight.Heavy?1f:w==StaggerWeight.Step?.62f:w==StaggerWeight.Flinch?.36f:0;
        /// <summary>
        /// The local pilot's camera kick for a melee hit (`hit` matched to the actor's reaction) or a blocked melee blow (`block`):
        /// the view snaps away from the blow (pitches up from the front, down from behind, rolls with a side push, a little
        /// yaw and a push of the eye along the blow), scaled by heft and the reaction's strength, a crisp 20 ms rise and a
        /// recovery within .3-.48 s; a block kicks at 40%. `scale` is the settings scale (0 with camera shake off). Bounded by
        /// the Kick* limits whatever the inputs; false (and a zero kick) when nothing applies. Never touches aim or input.
        /// </summary>
        public static bool CameraKick(Actor a,float time,HitSource hit,HitSource block,float scale,out Kick k)=>CameraKick(a,time,hit,block,scale,out k,null,0);
        /// <summary>As above, plus the felt-hit kick of the local pilot's recent non-melee staggering hits (`staggers`, the
        /// ledger's records oldest first): a short snap away from where each came from, by its weight, within the same limits.</summary>
        public static bool CameraKick(Actor a,float time,HitSource hit,HitSource block,float scale,out Kick k,StaggerHit[] staggers,int count)
        {
            k=default;if(a==null||!(scale>0)||!(a.Hp>0))return false;scale=Math.Min(1,scale);
            float pitch=0,yaw=0,roll=0;Vector3 off=Vector3.Zero;float weight=0;
            if(staggers!=null)for(int i=0;i<count&&i<staggers.Length;i++)
            {
                var g=staggers[i];if(g.Melee||!g.HasDir||g.Weight==StaggerWeight.None)continue;float age=time-g.At;if(!(age>=0)||age>.5f)continue;
                float e=Envelope(age,.018f,.012f,.2f+.06f*(int)g.Weight),A=StaggerKick(g.Weight)*MeleeMath.Clamp(g.Scale,.7f,1.25f)*scale*e;
                Vector3 d=Unit(Local(new V3(g.Dir.x,0,g.Dir.z),-a.Yaw*(float)Math.PI/180));d.Y=0;
                pitch+=d.Z*2.1f*A*(g.Head?1.4f:1);roll+=d.X*2.3f*A;yaw+=-d.X*.6f*A;off+=new Vector3(-d.X*.012f,-.006f,d.Z*.015f)*A;weight=Math.Max(weight,e*scale);
            }
            if(hit.Melee&&a.ReactionAt>-90&&!a.ReactionParry&&Math.Abs(hit.At-a.ReactionAt)<.004f&&time>=a.ReactionAt)
            {
                float h=MeleeMath.Clamp(hit.Heft,MinHeft,MaxHeft),age=time-a.ReactionAt,dur=.3f+.12f*h,e=Envelope(age,.02f,.018f,dur-.038f);
                float s=Finite(a.ReactionStrength,.6f),A=h*(.7f+.3f*MeleeMath.Clamp(s,0,1))*scale*e*MeleeKick;Vector3 d=Unit(new Vector3(Finite(a.ReactionDir.x,0),0,Finite(a.ReactionDir.z,-1)));
                bool head=hit.Head||a.ReactionZone==1;float v=Variant(a.ReactionAt);
                pitch+=d.Z*2.1f*A*(head?1.5f:1);roll+=d.X*2.3f*A+v*.5f*A;yaw+=-d.X*.8f*A;off+=new Vector3(-d.X*.014f,-.008f,d.Z*.018f)*A;weight=Math.Max(weight,e*scale);
            }
            if(block.Known&&block.Melee&&block.HasFrom&&time>=block.At&&time<block.At+.5f)
            {
                float h=MeleeMath.Clamp(block.Heft,MinHeft,MaxHeft),e=Envelope(time-block.At,.02f,.02f,.24f+.08f*h),A=.4f*h*scale*e;
                Vector3 d=Unit(Local(new V3(a.Position.x-block.From.x,0,a.Position.z-block.From.z),-a.Yaw*(float)Math.PI/180));d.Y=0;
                pitch+=d.Z*2.1f*A;roll+=d.X*2.3f*A;off+=new Vector3(-d.X*.014f,-.008f,d.Z*.018f)*A;weight=Math.Max(weight,e*.4f*scale);
            }
            // A soft limit (the curve keeps its shape as it nears the limit), then a hard bound.
            k.Pitch=Soft(pitch,KickPitch);k.Yaw=Soft(yaw,KickYaw);k.Roll=Soft(roll,KickRoll);k.Offset=new Vector3(Soft(off.X,KickOffset),Soft(off.Y,KickOffset),Soft(off.Z,KickOffset));
            k.Weight=MeleeMath.Clamp(weight,0,1);return k.Weight>0;
        }
        /// <summary>
        /// The first-person view model while the local pilot is stunned (the guard drops: lowered and tilted down, eased in and
        /// out inside the stun, held through a combo by the viewer's `stun` clock) and jolted by a melee hit (pushed back toward
        /// the eye): camera-local offset (metres) and pitch (degrees).
        /// </summary>
        public static Vector3 ViewDip(Actor a,float time,NativeStunClock stun,HitSource hit,out float pitch)
        {
            pitch=0;if(a==null||!(a.Hp>0))return Vector3.Zero;
            float daze=stun!=null?stun.Weight(a,time):0;
            Vector3 off=new Vector3(0,-.05f,-.02f)*daze;pitch=7*daze;
            if(hit.Melee&&a.ReactionAt>-90&&Math.Abs(hit.At-a.ReactionAt)<.004f&&time>=a.ReactionAt)
            {
                float h=MeleeMath.Clamp(hit.Heft,MinHeft,MaxHeft),e=Envelope(time-a.ReactionAt,.022f,.02f,.22f+.1f*h);
                off+=new Vector3(0,-.015f,-.03f)*h*e;pitch+=-4*h*e;
            }
            pitch=MeleeMath.Clamp(Finite(pitch,0),-8,8);return new Vector3(0,MeleeMath.Clamp(Finite(off.Y,0),-.08f,0),MeleeMath.Clamp(Finite(off.Z,0),-.08f,0));
        }
        public static float Finite(float v,float fallback)=>float.IsNaN(v)||float.IsInfinity(v)?fallback:v;
        private static float Soft(float v,float limit)=>MeleeMath.Clamp(limit*(float)Math.Tanh(Finite(v,0)/limit),-limit,limit);
        private static Vector3 Unit(Vector3 v){float l=v.Length();return l>1e-4f&&l<1e6f?v/l:new Vector3(0,0,-1);}
    }

    /// <summary>
    /// One viewer's dazed-stun weight for one pilot (a rig's hit layer; the first-person view model). The daze eases in from
    /// the start of the continuous stun: a blow landing while the pilot is still stunned (a fighter combo or a dagger string
    /// extending StunUntil) keeps that start, so the pose stays dazed instead of dropping to zero and easing back in at every
    /// hit. Its ease-out is sized by the latest blow, and a stun extended during its ease-out climbs back at DazeRise instead
    /// of popping. A reaction older than the last unstunned frame is not this stun's blow. Exactly 0 from the authoritative
    /// stun end and while a parried attacker reels (the route's own). Another pilot or a rewind (the killcam) starts over.
    /// Presentation only, no allocation.
    /// </summary>
    public sealed class NativeStunClock
    {
        private int id=int.MinValue;private bool live;private float start=-100,end,calm=float.NegativeInfinity,last=float.NaN,weight;
        /// <summary>The start of the stun being shown (the daze's ease-in and its sway phase); -100 before any.</summary>
        public float Start=>start;
        public void Reset(){id=int.MinValue;live=false;start=-100;end=0;calm=float.NegativeInfinity;last=float.NaN;weight=0;}
        public float Weight(Actor a,float time)
        {
            if(a==null||!(Math.Abs(time)<1e6f)){Reset();return 0;}
            if(a.Id!=id||!(time>=last-.05f)){Reset();id=a.Id;}
            float dt=time>last?time-last:0;last=Math.Max(time,float.IsNaN(last)?time:last);
            if(!(a.StunUntil>time)){live=false;calm=time;weight=0;return 0;}
            if(a.ReactionParry){live=false;weight=0;return 0;}
            // The host's render clock leads the simulation by up to a tick (a blow may land just after the last calm frame);
            // a guest's trails its snapshot by about .1 s, so the stun's blow may still be ahead of `time`: it is the start.
            bool fresh=a.ReactionAt>-90&&a.StunUntil>a.ReactionAt&&a.ReactionAt>=calm-.05f;
            float at=fresh?a.ReactionAt:Math.Max(-100,a.StunUntil-.3f);
            // A new stun (none shown, the shown one ended before this blow, or a blow before the start shown) restarts the
            // daze; a blow inside the stun keeps its start while StunUntil is extended.
            if(!live||at>=end||at<start)start=at;
            live=true;end=a.StunUntil;
            float w=NativeHitRules.Daze(time,start,a.StunUntil,fresh?a.ReactionAt:start);
            if(w>weight)w=Math.Min(w,weight+dt*NativeHitRules.DazeRise);
            weight=w;return w;
        }
    }

    /// <summary>
    /// Client-only record of the latest classified hit on, and block by, each pilot (host, guests and the killcam each keep
    /// their own, fed every rendered frame from the snapshot's events; events are read once by id). No allocation once warm.
    /// </summary>
    public sealed class NativeHitLedger
    {
        private struct Strike{public int Source;public float At,Amount;}
        private readonly Dictionary<int,HitSource> hits=new Dictionary<int,HitSource>(),blocks=new Dictionary<int,HitSource>();
        private readonly Dictionary<int,Strike> strikes=new Dictionary<int,Strike>();
        private int last;
        /// <summary>Bumped by every <see cref="Clear"/>, so a rig's stagger knows its hits started over.</summary>
        public int Epoch{get;private set;}
        public void Clear(){hits.Clear();blocks.Clear();strikes.Clear();foreach(var r in staggers.Values)r.Clear();foreach(var b in blades.Values)b.Clear();blows.Clear();last=0;Epoch++;}
        private static Actor Find(Actor[] actors,int id){if(actors==null||id<=0)return null;for(int i=0;i<actors.Length;i++)if(actors[i]!=null&&actors[i].Id==id)return actors[i];return null;}
        // ---- felt hits (Core/NativeStagger), melee kill blows (Core/NativeMeleeSlice) and blade blood (Core/NativeBlood) ----
        public const int StaggerSlots=8,BladeSlots=6;
        private sealed class Ring
        {
            public readonly StaggerHit[] Items=new StaggerHit[StaggerSlots];public int Count,Head;
            public void Clear(){Count=0;Head=0;}
            public ref StaggerHit Newest=>ref Items[(Head+Count-1)%StaggerSlots];
            public void Add(in StaggerHit h){if(Count<StaggerSlots){Items[(Head+Count)%StaggerSlots]=h;Count++;}else{Items[Head]=h;Head=(Head+1)%StaggerSlots;}}
        }
        private sealed class Blade{public readonly float[] At=new float[BladeSlots];public int Count,Next;public void Clear(){Count=0;Next=0;}}
        private readonly Dictionary<int,Ring> staggers=new Dictionary<int,Ring>();private readonly Dictionary<int,Blade> blades=new Dictionary<int,Blade>();
        private readonly Dictionary<int,MeleeBlow> blows=new Dictionary<int,MeleeBlow>();private int serial;
        private struct Blast{public int Source;public float At;public V3 Position;}
        private readonly Blast[] blasts=new Blast[16];private int blastCount;
        /// <summary>The recent staggering hits on `target`, oldest first, into `into` (at most <see cref="StaggerSlots"/>).</summary>
        public int Staggers(int target,StaggerHit[] into)
        {
            if(into==null||!staggers.TryGetValue(target,out var r))return 0;int n=Math.Min(r.Count,into.Length);
            for(int i=0;i<n;i++)into[i]=r.Items[(r.Head+r.Count-n+i)%StaggerSlots];return n;
        }
        /// <summary>The latest melee blow on `target` landed at `at` (within a tick), with its attacker's replicated swing.</summary>
        public bool Blow(int target,float at,out MeleeBlow blow)=>blows.TryGetValue(target,out blow)&&Math.Abs(blow.At-at)<.02f;
        /// <summary>How bloody `source`'s blade is at `time` (0..1): each damaging melee hit adds .4, kept for 4 s and gone by 10 s.</summary>
        public float BladeBlood(int source,float time)
        {
            if(!blades.TryGetValue(source,out var b))return 0;float sum=0;
            for(int i=0;i<b.Count;i++)sum+=NativeBloodRules.BladeShare(time-b.At[i]);return Math.Min(1,sum);
        }
        private void Stagger(in ArenaEvent e,Actor attacker,Actor target,bool melee)
        {
            string spec=e.Source==0?null:attacker?.SourceSpec;var w=NativeStaggerRules.Classify(spec,e.DamageKind??e.Detail??"direct",e.Amount);if(w==StaggerWeight.None)return;
            if(!staggers.TryGetValue(e.Target,out var ring))staggers[e.Target]=ring=new Ring();
            // Pellets from one gun in one tick are one volley.
            if(ring.Count>0){ref var n=ref ring.Newest;if(n.Source==e.Source&&Math.Abs(n.At-e.Time)<.004f&&!n.Melee&&!melee){n.Amount+=e.Amount;n.Weight=NativeStaggerRules.Volley(n.Weight,spec,n.Amount);n.Scale=NativeStaggerRules.Scale(n.Weight,n.Amount);n.Head|=e.Kind==EventKind.Headshot;return;}}
            // Where it came from: the authority's hit direction (ArenaEvent.Normal, filled for pilots since PR #136 as for adds), else
            // a blast's centre, else the attacker, else the hit point.
            bool has=NativeStaggerRules.Along(e.Normal,out V3 dir);V3 at=target!=null?target.Position:e.Position;
            if(!has&&e.Detail=="blast")for(int i=0;i<blastCount&&!has;i++)if(blasts[i].Source==e.Source&&Math.Abs(blasts[i].At-e.Time)<.02f)has=NativeStaggerRules.Push(at,blasts[i].Position,out dir);
            if(!has&&attacker!=null)has=NativeStaggerRules.Push(at,attacker.Position,out dir);
            if(!has)has=NativeStaggerRules.Push(at,e.Position,out dir);
            bool longStun=target!=null&&target.StunUntil-e.Time>NativeStagger.LongStun&&Math.Abs(target.ReactionAt-e.Time)<.004f;
            // Its place in a chain from the same attacker (the previous link within ChainSeconds).
            int chain=0;if(ring.Count>0){ref var n=ref ring.Newest;if(n.Source==e.Source&&e.Time-n.At>=0&&e.Time-n.At<=NativeStaggerRules.ChainSeconds)chain=n.Chain+1;}
            ring.Add(new StaggerHit{At=e.Time,Amount=e.Amount,Weight=w,Scale=NativeStaggerRules.Scale(w,e.Amount),Head=e.Kind==EventKind.Headshot,Melee=melee,HasDir=has,Dir=dir,Source=e.Source,Serial=++serial,Long=longStun,Chain=chain});
        }
        private void MeleeHit(in ArenaEvent e,Actor attacker)
        {
            if(attacker==null||e.Source<=0)return;
            if(!blades.TryGetValue(e.Source,out var b))blades[e.Source]=b=new Blade();
            b.At[b.Next]=e.Time;b.Next=(b.Next+1)%BladeSlots;b.Count=Math.Min(BladeSlots,b.Count+1);
            var a=attacker;bool special=a.SpecialKind!=null&&e.Time>=a.SpecialAt-.05f&&e.Time<=a.SpecialUntil+.05f,kit=e.Detail=="ability"&&a.MeleeKit!=null&&!string.IsNullOrEmpty(a.MeleeKit.Kind);
            blows[e.Target]=new MeleeBlow{At=e.Time,Source=e.Source,Spec=a.SourceSpec,CommitYaw=a.CommitYaw,Index=a.Combo+1,SwingAt=a.SwingAt,SwingUntil=a.SwingUntil,Air=!a.Grounded,
                Special=special?a.SpecialKind:null,Kit=kit?a.MeleeKit.Kind:null,From=a.Position,Yaw=a.Yaw,Detail=e.Detail,Amount=e.Amount};
        }
        public void Observe(Snapshot s)
        {
            var events=s?.Events;if(events==null)return;int newest=0;
            for(int i=0;i<events.Length;i++)if(events[i].Id>newest)newest=events[i].Id;
            // Event ids restart with a new arena (a new match, the killcam's replica): start over.
            if(newest>0&&newest<last)Clear();
            blastCount=0;
            for(int i=0;i<events.Length;i++)
            {
                var e=events[i];if(e.Id<=last)continue;
                if(e.Kind==EventKind.Explosion&&blastCount<blasts.Length)blasts[blastCount++]=new Blast{Source=e.Source,At=e.Time,Position=e.Position};
                if(e.Target<=0)continue;
                if(e.Kind==EventKind.Emperor||e.Kind==EventKind.Arcade)strikes[e.Target]=new Strike{Source=e.Source,At=e.Time,Amount=e.Amount};
            }
            for(int i=0;i<events.Length;i++)
            {
                var e=events[i];if(e.Id<=last)continue;
                if((e.Kind==EventKind.Hit||e.Kind==EventKind.Headshot)&&e.Target>0)
                {
                    var attacker=Find(s.Actors,e.Source);var target=Find(s.Actors,e.Target);string spec=e.Source==0?null:attacker?.SourceSpec;
                    bool fighter=strikes.TryGetValue(e.Target,out var st)&&st.Source==e.Source&&Math.Abs(st.At-e.Time)<.004f;
                    // The target's replicated reaction is from this hit's tick.
                    bool reacted=target!=null&&Math.Abs(target.ReactionAt-e.Time)<.004f;
                    var hit=new HitSource{Known=true,Melee=NativeHitRules.Melee(e.Source,e.Detail,spec,fighter,reacted),Head=e.Kind==EventKind.Headshot,At=e.Time,
                        Heft=NativeHitRules.Heft(spec,fighter?st.Amount:e.Amount),HasFrom=attacker!=null,From=attacker!=null?attacker.Position:e.Position};
                    // Keep the hit that made the target's current reaction: a later hit that recorded none (a damage field's
                    // tick, a Siren shot, splash) never replaces it, and a same-tick non-melee hit (the shrapnel blast a sword
                    // blow sets off) never replaces a melee one, so the layer, the kick and the view jolt keep their blow.
                    bool store=true;
                    if(hits.TryGetValue(e.Target,out var old))
                        store=Math.Abs(old.At-e.Time)<.004f?hit.Melee||!old.Melee:reacted||target==null||!(Math.Abs(old.At-target.ReactionAt)<.004f);
                    if(store)hits[e.Target]=hit;
                    Stagger(e,e.Source==0?null:attacker,target,hit.Melee);
                    // A standard melee weapon's swing, thrown blade or Q/E kit hit ("ability": the Whirlwind's pulses, Earthsplitter):
                    // the blow a kill is sliced by and the blood on the blade.
                    if(e.Amount>0&&NativeHitRules.Standard(spec)&&(hit.Melee||e.Detail=="ability"))MeleeHit(e,attacker);
                }
                else if(e.Kind==EventKind.Block&&e.Source>0)
                {
                    // BlockProc: Source the blocker, Target the attacker (0 for a PvE add). Only a melee blow's block shoves
                    // the guard or kicks the view; a blocked bullet (BlockProc runs for direct primary shots too) is not kept,
                    // so it never replaces the record of a melee block.
                    var attacker=Find(s.Actors,e.Target);string spec=attacker?.SourceSpec;
                    if(attacker==null||NativeHitRules.Standard(spec)||NativeHitRules.Fighter(spec)||spec=="siren")
                        blocks[e.Source]=new HitSource{Known=true,Melee=true,At=e.Time,Heft=attacker==null?.9f:NativeHitRules.Heft(spec,0),HasFrom=attacker!=null,From=attacker!=null?attacker.Position:e.Position};
                }
            }
            if(newest>last)last=newest;
        }
        /// <summary>The classified hit that made the reaction recorded at `reactionAt` on `target`, if this ledger saw it.</summary>
        public bool Hit(int target,float reactionAt,out HitSource hit)
        {
            if(hits.TryGetValue(target,out hit)&&Math.Abs(hit.At-reactionAt)<.004f)return true;hit=default;return false;
        }
        /// <summary>The latest melee block by `blocker` within `window` seconds before `time`.</summary>
        public bool Block(int blocker,float time,float window,out HitSource block)
        {
            if(blocks.TryGetValue(blocker,out block)&&time>=block.At&&time-block.At<=window)return true;block=default;return false;
        }
    }

    /// <summary>
    /// The native melee hit layer, one per rig, run after a route's own reactions (the source port's impulse recoil, the
    /// captured stagger and block impact) and before its ground pass and pose transitions. Presentation only: it reads the
    /// replicated Actor (reaction, stun end, block window) and the ledger, never writes either, and only moves the rig's
    /// bones and its root presentation (the hips), never the authoritative position, so locomotion, hitboxes and timing are
    /// untouched; the feet stay planted while stunned. Per blow it adds:
    ///  - a crisp, weight-scaled impact accent (cubic rise, a held impact frame, a damped recovery), directional from the
    ///    attacker's front, back or side (lean back with the head snapped back and the guard knocked up; a lurch forward with
    ///    the head whipping back; a bend and twist away from a side blow), alternating a little between successive blows;
    ///  - a head snap on a headshot;
    ///  - a dazed stun pose (guard dropped, head lolling, a slow sway, soft knees) for as long as the stun lasts, held
    ///    through a combo's extensions (NativeStunClock) and easing out to exactly zero at its authoritative end;
    ///  - a guard shove when a block absorbs a melee blow, scaled by the attacker's heft (the last one fading under the next);
    ///  - for gun and magic pilots (whose route shows no stunned reaction) the stunned recoil with a planted catch step.
    /// A light (unstunned) melee hit is an upper-body accent over the gait, so travel keeps working. Non-melee hits keep
    /// the source's light reaction only. No allocation per frame.
    /// </summary>
    public sealed class NativeHitLayer
    {
        private struct Accent{public bool Live,Head,Stunned;public float At,Heft,Strength,Turn,Variant,FromYaw;public Vector3 Dir;public int Set;public PackSlot Slot;}
        private Accent current,previous;private bool seen;private readonly NativeStunClock stun=new NativeStunClock();
        // The melee block being shoved and the one before it (fading out under it).
        private HitSource shove,shoveOld;private bool shoveLive,shoveOldLive;
        private Actor actor;private NativeHitLedger ledger;private HitVictim kind;private float rigYaw;
        private MeleeRig rig;private float now;private readonly Action change;
        private readonly Vector3[] heldAnkle=new Vector3[2],heldKnee=new Vector3[2];private readonly Quaternion[] heldFoot=new Quaternion[2];
        /// <summary>The last frame's weights (for the checks): impact envelope, daze, block shove and the summed bone offset.</summary>
        public float ImpactWeight{get;private set;}
        public float DazeWeight{get;private set;}
        public float BlockWeight{get;private set;}
        public bool Applied{get;private set;}
        /// <summary>The last frame's pack clips (Core/NativePackReact, for the checks): the hit clip's set, slot and weight, the
        /// Stunned loop's weight and the block hit's slot and weight (set -1 / weight 0: the procedural layer alone).</summary>
        public int PackSet{get;private set;}=NativePackRules.None;
        public PackSlot Slot{get;private set;}
        public float PackWeight{get;private set;}
        public float PackStunWeight{get;private set;}
        public PackSlot BlockSlot{get;private set;}
        public float PackBlockWeight{get;private set;}
        /// <summary>The hit clip's whole-body share (hands, hips travel): 1 inside the stun, exactly 0 from its end on.</summary>
        public float PackBody{get;private set;}
        private float hitBody;
        private readonly NativePackPose pack=new NativePackPose();private readonly float[] packAt=new float[3],packMarks=new float[3];
        private bool blockSecond;
        // The share of the melee accent's own hip push while the stagger shifts the hips (so the two never add up past its bound).
        private float pushShare=1;
        public NativeHitLayer(){change=Apply;}
        /// <summary>
        /// Whether the layer's pack clips own this stun (the captured melee route then holds the guard instead of its captured
        /// stagger and impulse recoil; the DEV fighters keep their recoil under the Unarmed clip): a melee blow (or an unknown one, which a stun implies) stunned a pilot whose weapon style has a
        /// reaction set; a parried attacker's reel stays the route's own.
        /// </summary>
        public bool Drives(float time)
        {
            var a=actor;if(ledger==null||a==null||a.ReactionParry||!(a.StunUntil>time)||Skip(a,time)||NativePackRules.SetFor(a.SourceSpec,kind)<0)return false;
            if(!(a.ReactionAt>-90&&a.ReactionAt<=time&&a.StunUntil>a.ReactionAt+.02f))return false;
            if(ledger.Hit(a.Id,a.ReactionAt,out var hit)&&!hit.Melee)return false;
            // The blow's clip as the layer chose it (or, on the frame it lands, as it will choose it): a blow from behind has none.
            if(seen&&current.At==a.ReactionAt)return current.Slot!=PackSlot.None;
            Vector3 dir=new Vector3(NativeHitRules.Finite(a.ReactionDir.x,0),0,NativeHitRules.Finite(a.ReactionDir.z,-1));
            return NativePackRules.Direction(NativeHitRules.Reframe(dir,NativeHitRules.Finite(a.ReactionYaw,0),bound!=null?bound.Rig.GroupYaw:0),1)!=PackSlot.None;
        }
        /// <summary>Whether the layer's pack block hit owns this block's impact (a melee blow absorbed by a pilot whose weapon
        /// style has a reaction set); MeleeMotion then leaves its own Block-GetHit out.</summary>
        public bool DrivesBlock(float time)
        {
            var a=actor;return ledger!=null&&a!=null&&!Skip(a,time)&&NativePackRules.SetFor(a.SourceSpec,kind)>=0&&ledger.Block(a.Id,time,.5f,out var b)&&b.Melee&&a.BlockUntil>=b.At-.05f;
        }
        /// <summary>The captured-melee and DEV fighter routes: MeleeMotion runs the layer (Layer) before its ground pass.</summary>
        public void Bind(MeleeMotion m,Actor a,NativeHitLedger hits,HitVictim victim){if(m==null)return;m.HitLayer=this;bound=m;actor=a;ledger=hits;kind=victim;rigYaw=float.NaN;}
        private MeleeMotion bound;
        /// <summary>The gun and magic routes: over the posed native pose (in `scratch`), before the route's Finish.</summary>
        public void Over(MeleeRig scratch,CapturedRig.Pose pose,Actor a,NativeHitLedger hits,HitVictim victim,float time,float yaw,Vector3? at=null)
        {
            actor=a;ledger=hits;kind=victim;rigYaw=yaw;Applied=false;ImpactWeight=DazeWeight=BlockWeight=0;PackSet=NativePackRules.None;PackWeight=PackStunWeight=PackBlockWeight=PackBody=0;
            if(scratch==null||pose==null)return;stagger.Observe(hits,a,time);if(!Wanted(a,time)){stagger.Drop();return;}
            scratch.ReadPose(pose);scratch.GroupYaw=yaw;if(at.HasValue)scratch.GroupPosition=at.Value;Layer(scratch,time);scratch.WritePose(pose);
        }
        /// <summary>
        /// After the route's pose transitions (MeleeMotion.FinishPose, GunMotion / MagicMotion.Finish), which blend every bone
        /// from the pose shown when an action changed: the feet the stagger planted this frame are put back where it planted
        /// them, so a transition never slides them. On a posed pose (the gun and magic routes) through the scratch rig.
        /// </summary>
        public void Replant(MeleeRig r)=>stagger.Replant(r);
        public void Replant(MeleeRig scratch,CapturedRig.Pose pose){if(scratch==null||pose==null||!stagger.Placed)return;scratch.ReadPose(pose);stagger.Replant(scratch);scratch.WritePose(pose);pose.World();}
        /// <summary>The felt-hit stagger this layer runs (Core/NativeStagger), for the checks.</summary>
        public NativeStagger Stagger=>stagger;
        private readonly NativeStagger stagger=new NativeStagger();
        // Dead, juggled, knocked down, rolling, in an E special, held by the Siren, the Siren herself, or no arena ledger
        // (intro, portrait, corpse and viewer rigs): the route's own layer owns the frame.
        private static bool Skip(Actor a,float time)=>a==null||!(a.Hp>0)||a.SourceSpec=="siren"||a.GrabOwner!=0||NativeJugglePose.Active(a.JuggleUntil,time)||NativeSpecialPose.Down(a,time)||a.Rolling(time)||NativeSpecialPose.Active(a,time);
        private bool Wanted(Actor a,float time)=>ledger!=null&&!Skip(a,time)&&(a.ReactionAt>-90&&time>=a.ReactionAt&&time<a.ReactionAt+2||a.StunUntil>time||time<a.BlockUntil+.5f||stagger.Wanted(time));
        // per-frame values Apply reads
        private float stunAt,stunUntil,daze,blockE,blockHeft,oldE,oldHeft;private Vector3 blockDir,oldDir;private bool stepping,stepBased;private float stepU,stepPlant,stepAt;private Vector3 stepDir,stepBase;private float stepHeft;
        public void Layer(MeleeRig r,float time)
        {
            Applied=false;ImpactWeight=DazeWeight=BlockWeight=0;PackSet=NativePackRules.None;PackWeight=PackStunWeight=PackBlockWeight=PackBody=0;var a=actor;if(r==null)return;
            // The captured-melee and fighter routes come here straight from MeleeMotion (Over has already read the hits).
            if(float.IsNaN(rigYaw))stagger.Observe(ledger,a,time);if(!Wanted(a,time)){stagger.Drop();return;}
            float yaw=float.IsNaN(rigYaw)?r.GroupYaw:rigYaw;rig=r;now=time;
            // The reaction being shown (a new one keeps the interrupted blow's tail for .09 s).
            bool reaction=a.ReactionAt>-90&&time>=a.ReactionAt;
            if(reaction&&(!seen||current.At!=a.ReactionAt))
            {
                bool known=ledger.Hit(a.Id,a.ReactionAt,out var hit);bool stunned=a.StunUntil>a.ReactionAt+.02f;
                // Unknown source (a missed event): a stun only comes from melee, so a stunned blow is melee of medium heft. A
                // parried attacker's reel is the route's own (the source's parry motion).
                bool melee=!a.ReactionParry&&(known?hit.Melee:stunned);
                seen=true;previous=current;current=new Accent{Live=melee,Head=a.ReactionZone==1||known&&hit.Head,Stunned=stunned,At=a.ReactionAt,Heft=known?hit.Heft:1,Set=NativePackRules.SetFor(a.SourceSpec,kind),
                    Strength=NativeHitRules.Finite(a.ReactionStrength,.6f),Turn=NativeHitRules.Finite(a.ReactionTurn,0),Variant=NativeHitRules.Variant(a.ReactionAt),FromYaw=NativeHitRules.Finite(a.ReactionYaw,0),
                    Dir=new Vector3(NativeHitRules.Finite(a.ReactionDir.x,0),0,NativeHitRules.Finite(a.ReactionDir.z,-1))};
                if(current.Dir.LengthSquared()<1e-6f)current.Dir=new Vector3(0,0,-1);else current.Dir=Vector3.Normalize(current.Dir);
                // A blow chained within .8 s of the last one takes the other variant, so a combo reads hit by hit.
                if(previous.Live&&current.At-previous.At<.8f)current.Variant=-previous.Variant;
                // The pack clip for the blow, chosen once from where it came (the rig's facing as it landed).
                current.Slot=NativePackRules.Direction(NativeHitRules.Reframe(current.Dir,current.FromYaw,yaw),current.Heft);
            }
            // A rewind (the killcam) or a cleared reaction starts over.
            if(a.ReactionAt<=-90||seen&&time<current.At){seen=false;current.Live=previous.Live=false;}
            // The continuous stun (held through a combo's extensions; 0 from its end and while a parried attacker reels).
            stunUntil=a.StunUntil;daze=stun.Weight(a,time);stunAt=stun.Start;
            // A block absorbing a melee blow; a new block takes over from the last one, which fades out under it.
            blockE=oldE=0;blockHeft=oldHeft=0;blockDir=oldDir=new Vector3(0,0,-1);
            if(ledger.Block(a.Id,time,.5f,out var block)&&block.Melee&&a.BlockUntil>=block.At-.05f)
            {
                if(!shoveLive||block.At!=shove.At){blockSecond=shoveLive&&block.At>shove.At&&block.At-shove.At<.8f&&!blockSecond;shoveOldLive=shoveLive&&shove.At<block.At;shoveOld=shove;shove=block;shoveLive=true;}
                Shoved(a,shove,time,yaw,out blockE,out blockHeft,out blockDir);
                float fade=shoveOldLive?1-MeleeMath.Ease((time-shove.At)/.09f):0;
                if(fade>0){Shoved(a,shoveOld,time,yaw,out oldE,out oldHeft,out oldDir);oldE*=fade;}else shoveOldLive=false;
            }
            else shoveLive=shoveOldLive=false;
            bool grounded=a.Grounded;stepping=false;
            if(current.Live&&current.Stunned&&(kind==HitVictim.Gun||kind==HitVictim.Magic)&&grounded&&time<stunUntil)
            {
                float span=Math.Max(.05f,stunUntil-current.At),u=(time-current.At)/span;stepping=true;stepU=MeleeMath.Ease((u-.1f)/.35f);
                stepPlant=Math.Min(1,MeleeMath.Ease((time-current.At)/.05f))*(1-MeleeMath.Ease((u-.72f)/.28f));stepDir=NativeHitRules.Reframe(current.Dir,current.FromYaw,yaw);stepHeft=current.Heft;
            }
            // Nothing to add this frame: leave the route's pose exactly as it is.
            bool impact=Showing(ref current,time)||previous.Live&&previous.At<current.At&&time-current.At<.09f&&Showing(ref previous,time);
            // A pack clip's window (Core/NativePackReact) can outlast the procedural accent's.
            impact|=PackShowing(ref current,time)||previous.Live&&previous.At<current.At&&time-current.At<.09f&&PackShowing(ref previous,time);
            bool blockClip=shoveLive&&NativePackRules.SetFor(a.SourceSpec,kind)>=0&&NativePackRules.BlockWeight(time-shove.At)>0;
            bool staggered=stagger.Wanted(time)&&!a.ReactionParry;
            if(!impact&&!(daze>0)&&blockE==0&&oldE==0&&!stepping&&!blockClip&&!staggered){stagger.Drop();return;}
            // While stunned (planted feet) keep the ankles where the route put them; otherwise the ground pass re-plants the gait's feet.
            bool hold=grounded&&(r.LegsAuthored||r.LegsLayered||a.StunUntil>time);
            if(hold||staggered)for(int i=0;i<2;i++)heldAnkle[i]=r.ReadAnkle(i==0,out heldFoot[i],out heldKnee[i]);
            // The catch step is measured from where the foot stood when the blow landed (the gait follows the placed ankles,
            // so a step measured from the current ankle would keep adding up).
            if(stepping&&(!stepBased||stepAt!=current.At)){stepBased=true;stepAt=current.At;stepBase=heldAnkle[stepDir.X>=0?0:1];}
            Vector3 routeBody=r.P[CapturedRig.Body];pushShare=staggered?.35f:1;
            change();
            // The felt-hit stagger (Core/NativeStagger) over everything above: the hips shift along the blow, the upper body
            // recoils (lighter where the melee accent and pack clips already recoil it), and the feet stay planted in the
            // world, stepping when the hips or the push leave them behind.
            bool stepped=false;
            if(staggered)
            {
                stagger.Body(r,a,ledger,time,MeleeRecoil);
                // Everything together never shifts the hips more than the stagger's hard bound from where the route put them.
                Vector3 b=r.P[CapturedRig.Body],off=new Vector3(b.X-routeBody.X,0,b.Z-routeBody.Z);float l=off.Length();
                if(l>NativeStaggerRules.HardHip){off*=NativeStaggerRules.HardHip/l;r.P[CapturedRig.Body]=new Vector3(routeBody.X+off.X,b.Y,routeBody.Z+off.Z);}
                // The stagger's real steps replace the old catch step (it slid its foot home inside the stun).
                stepped=stagger.Feet(r,a,time,heldAnkle,heldFoot,heldKnee,a.StunUntil>time);stepping=false;
            }
            else stagger.Drop();
            if(hold&&!stepped)
            {
                for(int i=0;i<2;i++)
                {
                    Vector3 target=heldAnkle[i];
                    // A gun or magic pilot's catch step: the foot on the push side lifts and steps along the blow, then stays.
                    if(stepping&&(i==0)==(stepDir.X>=0))
                    {
                        Vector3 step=stepBase+new Vector3(stepDir.X,0,stepDir.Z)*.11f*stepHeft*stepU;step.Y+=(float)Math.Sin(Math.PI*stepU)*.07f;
                        target=MeleeMath.Lerp(target,step,stepPlant);
                    }
                    target.Y=Math.Max(target.Y,MeleeRig.FLOOR);r.SolveLeg(i==0,target,0,0,0,heldFoot[i],heldKnee[i]);
                }
            }
            Applied=true;
        }
        private void Apply()
        {
            var r=rig;float time=now,yaw=float.IsNaN(rigYaw)?r.GroupYaw:rigYaw;
            PackSet=NativePackRules.None;PackWeight=PackStunWeight=PackBlockWeight=PackBody=0;int set=NativePackRules.SetFor(actor.SourceSpec,kind);
            var fit=Fit(set);float routeClear=fit!=null&&(current.Live||daze>0||shoveLive)?pack.RouteClear(r,fit):0;
            // The pack clips first (Core/NativePackReact): the blow's directional GetHit (the interrupted blow's clip fading out
            // under it), the Stunned loop and the block hit, laid over the route's pose.
            float fade=previous.Live&&previous.At<current.At?1-MeleeMath.Ease((time-current.At)/.09f):0;
            float before=fade>0?PackHit(r,ref previous,time,fade):0,clip=current.Live?PackHit(r,ref current,time,1):0;
            if(clip>0){PackSet=current.Set;Slot=current.Slot;PackWeight=clip;PackBody=hitBody;}
            if(daze>0&&set>=0){PackStunWeight=daze*NativePackRules.LongStun(stunUntil-stunAt)*(1-.6f*clip);PackStun(r,set,time,PackStunWeight);}
            if(set>=0&&shoveLive)
            {
                if(shoveOldLive)PackBlock(r,set,shoveOld,!blockSecond,time,1-MeleeMath.Ease((time-shove.At)/.09f));
                PackBlockWeight=PackBlock(r,set,shove,blockSecond,time,1);BlockSlot=blockSecond?PackSlot.Block2:PackSlot.Block;
            }
            // The procedural layer on top: the fallback where no clip plays, and the blow's weight and exact direction over one.
            // The interrupted blow fades out under the new one, so chained hits read as separate impacts, not a pop.
            if(fade>0)Impact(r,ref previous,time,yaw,fade,1-PackShare*Math.Min(1,before/fade));
            if(current.Live)ImpactWeight=Impact(r,ref current,time,yaw,1,1-PackShare*clip);
            if(daze>0)Daze(r,time,daze*(1-StunShare*PackStunWeight/daze));
            float k=(kind==HitVictim.Melee?.55f:1)*(1-BlockShare*PackBlockWeight);
            if(oldE!=0)Shove(r,oldE*oldHeft*k,oldDir);
            if(blockE!=0)Shove(r,blockE*blockHeft*k,blockDir);
            // The held blade's last clearance over everything above (with the clips, over a clear route pose).
            if(routeClear>0)pack.ClearBlade(r,fit,set==NativePackRules.Greatsword,routeClear*Math.Max(Math.Max(clip,before),Math.Max(PackStunWeight,PackBlockWeight)));
            DazeWeight=daze;BlockWeight=blockE;
        }
        /// <summary>How much of the procedural accent, daze and block shove a fully weighted pack clip replaces.</summary>
        public const float PackShare=.55f,StunShare=.65f,BlockShare=.5f;
        /// <summary>The stagger's upper-body recoil for a melee blow, whose accent and pack clip already recoil the trunk.</summary>
        public const float MeleeRecoil=.15f;
        /// <summary>A light (unstunned) blow's share of its clip: the upper body and a little of the hips over the gait.</summary>
        public const float LightPack=.6f;
        /// <summary>The share of a hit or block clip's turn about the vertical the pilot shows (the captures spin up to 67°).</summary>
        public const float HitTurn=.5f;
        /// <summary>The share of a stunned hit clip's hand travel and turn the hands show (the captures fling the arms wide); the
        /// hands ease in over 70 ms behind the torso's crisp impact, so a held weapon is not snapped across the body.</summary>
        public const float HitArms=.75f;
        // A weapon style's blade (kept clear of the head and torso); null for the fists.
        private static MeleeSet Fit(int set)=>set==NativePackRules.Sword?MeleeSet.For("sword"):set==NativePackRules.Daggers?MeleeSet.For("daggers"):set==NativePackRules.Greatsword?MeleeSet.For("greatsword"):set==NativePackRules.Shield?MeleeSet.For("shield"):null;
        // The blow's GetHit clip over its window (a stunned blow's ends at the stun end): the whole body with the hands and
        // hips over the held feet while stunned, the upper body and a little of the hips over the gait on a light blow;
        // scaled by the attacker's heft. Returns the clip weight.
        private float PackHit(MeleeRig r,ref Accent h,float time,float weight)
        {
            if(h.Set<0||!(weight>0))return 0;var clip=NativePackRules.Clip(h.Set,h.Slot);if(clip==null)return 0;
            float heft=MeleeMath.Clamp(h.Heft,NativeHitRules.MinHeft,NativeHitRules.MaxHeft),age=time-h.At;
            float span=NativePackRules.HitSpan(clip,h.Stunned,Span(ref h,heft),NativeHitRules.LightSpan(heft)),w=NativePackRules.Weight(age,span)*weight;if(!(w>1e-4f))return 0;
            // The whole-body share (hands, hips travel over the held feet) is the stun's own and is gone at its end.
            float body=hitBody=h.Stunned?NativePackRules.StunFade(time,h.At,stunUntil):0,amp=MeleeMath.Clamp(.1f+.5f*heft,.4f,.8f)*(LightPack+(1-LightPack)*body);
            pack.Layer(r,clip,NativePackRules.ClipTime(clip,age,span,packAt,packMarks),null,0,w*amp,.4f+.6f*body,HitArms*body*MeleeMath.Ease(age/.07f),NativePackRules.MainHand(h.Set),Fit(h.Set),h.Set==NativePackRules.Greatsword,body,HitTurn);
            return w;
        }
        // The Stunned loop, held as its offset from the set's guard, from the continuous stun's start.
        private void PackStun(MeleeRig r,int set,float time,float w)
        {
            var clip=NativePackRules.Clip(set,PackSlot.Stun);if(clip==null||!(w>1e-4f))return;
            pack.Layer(r,clip,Math.Max(0,time-stunAt),NativePackRules.Clip(set,PackSlot.Guard),0,w,1,1,NativePackRules.MainHand(set),Fit(set),set==NativePackRules.Greatsword,1,1);
        }
        // A melee blow absorbed by the guard: the set's Block-GetHit (alternating with its second take along a string of
        // blocks), the guard arm shoved through the hands, scaled by the attacker's heft.
        private float PackBlock(MeleeRig r,int set,HitSource block,bool second,float time,float weight)
        {
            var clip=NativePackRules.Clip(set,second?PackSlot.Block2:PackSlot.Block);if(clip==null||!(weight>0))return 0;
            float age=time-block.At,w=NativePackRules.BlockWeight(age)*weight;if(!(w>1e-4f))return 0;
            float heft=MeleeMath.Clamp(block.Heft,NativeHitRules.MinHeft,NativeHitRules.MaxHeft);
            pack.Layer(r,clip,NativePackRules.BlockTime(clip,age,packAt,packMarks),null,0,w*MeleeMath.Clamp(.55f+.35f*heft,.75f,1),.5f,1,true,Fit(set),set==NativePackRules.Greatsword,0,HitTurn);
            return w;
        }
        // The blow's impact window: its stun (or a light hit's own span), as Impact times it.
        private float Span(ref Accent h,float heft)=>h.Stunned?Math.Max(.05f,(stunUntil>h.At?stunUntil:h.At+.05f)-h.At):NativeHitRules.LightSpan(heft);
        private bool PackShowing(ref Accent h,float time)
        {
            if(!h.Live||h.Set<0||time<h.At)return false;var clip=NativePackRules.Clip(h.Set,h.Slot);if(clip==null)return false;float heft=MeleeMath.Clamp(h.Heft,NativeHitRules.MinHeft,NativeHitRules.MaxHeft);
            return NativePackRules.Weight(time-h.At,NativePackRules.HitSpan(clip,h.Stunned,Span(ref h,heft),NativeHitRules.LightSpan(heft)))>0;
        }
        private bool Showing(ref Accent h,float time)
        {
            if(!h.Live||time<h.At)return false;float heft=MeleeMath.Clamp(h.Heft,NativeHitRules.MinHeft,NativeHitRules.MaxHeft);
            NativeHitRules.Timing(heft,Span(ref h,heft),out float rise,out float hold,out float fall);return time-h.At<rise+hold+fall;
        }
        // The impact accent. Base routes that already recoil (captured melee, fighters) get a lighter accent; gun and magic
        // pilots, whose routes show nothing while stunned, get the full stunned recoil from it.
        private float Impact(MeleeRig r,ref Accent h,float time,float yaw,float weight,float amp=1)
        {
            if(!(weight>0))return 0;float age=time-h.At;if(age<0)return 0;
            float heft=MeleeMath.Clamp(h.Heft,NativeHitRules.MinHeft,NativeHitRules.MaxHeft);
            NativeHitRules.Timing(heft,Span(ref h,heft),out float rise,out float hold,out float fall);if(age>=rise+hold+fall)return 0;
            bool native=kind==HitVictim.Gun||kind==HitVictim.Magic;float route=h.Stunned?(native?1.3f:.75f):(native?.8f:.9f);
            float A=heft*(.6f+.4f*MeleeMath.Clamp(h.Strength,0,1))*route*weight*MeleeMath.Clamp(amp,0,1);
            float e1=NativeHitRules.Envelope(age,rise,hold,fall),e2=NativeHitRules.Envelope(age-.016f,rise,hold,fall),e3=NativeHitRules.Envelope(age-.03f,rise,hold,fall),e4=NativeHitRules.Envelope(age-.045f,rise,hold,fall);
            Vector3 d=NativeHitRules.Reframe(h.Dir,h.FromYaw,yaw);float front=Math.Max(0,-d.Z),back=Math.Max(0,d.Z),sx=d.X,v=h.Variant;
            bool moving=!h.Stunned&&actor!=null&&actor.Velocity.x*actor.Velocity.x+actor.Velocity.z*actor.Velocity.z>1;
            // The root presentation (hips) is knocked along the blow and dips; the planted feet hold while stunned.
            Vector3 b=r.P[CapturedRig.Body];float push=(h.Stunned?.05f:.02f)*(moving?.5f:1)*pushShare;
            b.X+=d.X*push*A*e1;b.Z+=d.Z*push*A*e1;b.Y-=(h.Stunned?.03f:.01f)*A*e1;r.P[CapturedRig.Body]=b;
            r.AddE(CapturedRig.Body,d.Z*.07f*A*e1,h.Turn*.3f*A*e1,-d.X*.06f*A*e1);
            r.AddE(CapturedRig.Spine,(back*.15f-front*.1f)*A*e2,0,-sx*.1f*A*e2);
            r.AddE(CapturedRig.Chest,(-front*.1f-back*.1f)*A*e2,(h.Turn*.4f+v*.1f)*A*e2,-sx*.08f*A*e2);
            r.AddE(CapturedRig.Neck,(-front*.08f-back*.12f)*A*e3,0,0);
            r.AddE(CapturedRig.Head,(-front*.14f-back*.2f)*A*e3,-v*.06f*A*e3,(-sx*.2f+v*.07f)*A*e3);
            // The guard is knocked: up and open from the front, flung down and back from behind (both clavicles by the same
            // pitch about the chest's x axis, so two-handed grips and a held gun keep their hands together).
            float arm=(-front*.15f+back*.16f)*A*e4;
            r.Premultiply(CapturedRig.LClav,MeleeMath.Euler(arm,0,0));r.Premultiply(CapturedRig.RClav,MeleeMath.Euler(arm,0,0));
            if(h.Head)
            {
                // Over the source reaction's own head snap (.48 of a head zone): a faster, harder snap of the head on the neck.
                float eh=NativeHitRules.Envelope(age-.008f,rise*.7f,hold,fall*1.25f)*Math.Min(heft,1.2f)*weight;
                r.AddE(CapturedRig.Neck,d.Z*.12f*eh,0,0);r.AddE(CapturedRig.Head,d.Z*.26f*eh,0,-sx*.24f*eh);
            }
            return e1*weight;
        }
        // The dazed stun: guard dropped, head lolling, a slow sway and soft knees.
        private void Daze(MeleeRig r,float time,float w)
        {
            float phase=(time-stunAt)*MeleeMath.TAU*1.5f,s=(float)Math.Sin(phase),drop=.3f*w;
            r.Premultiply(CapturedRig.LClav,MeleeMath.Euler(drop,0,0));r.Premultiply(CapturedRig.RClav,MeleeMath.Euler(drop,0,0));
            r.AddE(CapturedRig.Spine,.04f*w,0,0);r.AddE(CapturedRig.Chest,.03f*w,0,.05f*w*s);
            r.AddE(CapturedRig.Neck,.08f*w,0,0);r.AddE(CapturedRig.Head,.15f*w,0,.09f*w*(float)Math.Sin(phase+.8f));
            Vector3 b=r.P[CapturedRig.Body];b.Y-=.035f*w;b.X+=.012f*w*s;r.P[CapturedRig.Body]=b;
        }
        // A melee block's shove envelope, heft and push direction (away from the attacker, in the rig's facing frame).
        private static void Shoved(Actor a,HitSource block,float time,float yaw,out float e,out float heft,out Vector3 dir)
        {
            heft=MeleeMath.Clamp(block.Heft,NativeHitRules.MinHeft,NativeHitRules.MaxHeft);e=NativeHitRules.Envelope(time-block.At,.026f,.03f,.2f+.1f*heft);dir=new Vector3(0,0,-1);
            if(block.HasFrom){var d=NativeHitRules.Local(new V3(a.Position.x-block.From.x,0,a.Position.z-block.From.z),yaw);d.Y=0;if(d.LengthSquared()>1e-6f)dir=Vector3.Normalize(d);}
        }
        // A block absorbing a blow: the guard is driven up toward the face and the body is shoved back from the attacker.
        private static void Shove(MeleeRig r,float k,Vector3 d)
        {
            Vector3 b=r.P[CapturedRig.Body];b.X+=d.X*.035f*k;b.Z+=d.Z*.035f*k;b.Y-=.02f*k;r.P[CapturedRig.Body]=b;
            r.AddE(CapturedRig.Spine,d.Z*.1f*k,0,-d.X*.06f*k);r.AddE(CapturedRig.Chest,d.Z*.06f*k,0,0);r.AddE(CapturedRig.Head,d.Z*.08f*k,0,-d.X*.08f*k);
            r.Premultiply(CapturedRig.LClav,MeleeMath.Euler(-.1f*k,0,0));r.Premultiply(CapturedRig.RClav,MeleeMath.Euler(-.1f*k,0,0));
        }
    }
}
