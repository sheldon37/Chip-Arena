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
        /// does not); the Siren's claws are its non-direct ("secondary") hits; a standard melee pilot only deals direct damage
        /// with its weapon (swings, specials, thrown shield and daggers). Guns and magic never melee.
        /// </summary>
        public static bool Melee(int source,string detail,string spec,bool fighterStrike)
        {
            if(source==0)return detail=="direct";
            if(Fighter(spec))return fighterStrike;
            if(spec=="siren")return detail=="secondary";
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
        /// The dazed-stun weight: eases in from .06 s after the blow (past the impact frame) over .1 s and out over the last clamp(35%, .06, .16) s of
        /// the stun; exactly 0 before the blow and from the authoritative stun end, so presentation never extends the lockout.
        /// </summary>
        public static float Daze(float time,float at,float until)
        {
            if(!(time>=at)||!(time<until))return 0;float span=until-at,o=MeleeMath.Clamp(span*.35f,.06f,.16f);
            return MeleeMath.Clamp(MeleeMath.Ease((time-at-.06f)/.1f)*(1-MeleeMath.Ease((time-(until-o))/o)),0,1);
        }
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
        public const float KickPitch=2.6f,KickYaw=1.2f,KickRoll=2.8f,KickOffset=.03f;
        /// <summary>
        /// The local pilot's camera kick for a melee hit (`hit` matched to the actor's reaction) or a blocked melee blow (`block`):
        /// the view snaps away from the blow (pitches up from the front, down from behind, rolls with a side push, a little
        /// yaw and a push of the eye along the blow), scaled by heft and the reaction's strength, a crisp 20 ms rise and a
        /// recovery within .3-.48 s; a block kicks at 40%. `scale` is the settings scale (0 with camera shake off). Bounded by
        /// the Kick* limits whatever the inputs; false (and a zero kick) when nothing applies. Never touches aim or input.
        /// </summary>
        public static bool CameraKick(Actor a,float time,HitSource hit,HitSource block,float scale,out Kick k)
        {
            k=default;if(a==null||!(scale>0)||!(a.Hp>0))return false;scale=Math.Min(1,scale);
            float pitch=0,yaw=0,roll=0;Vector3 off=Vector3.Zero;float weight=0;
            if(hit.Melee&&a.ReactionAt>-90&&!a.ReactionParry&&Math.Abs(hit.At-a.ReactionAt)<.004f&&time>=a.ReactionAt)
            {
                float h=MeleeMath.Clamp(hit.Heft,MinHeft,MaxHeft),age=time-a.ReactionAt,dur=.3f+.12f*h,e=Envelope(age,.02f,.018f,dur-.038f);
                float s=Finite(a.ReactionStrength,.6f),A=h*(.7f+.3f*MeleeMath.Clamp(s,0,1))*scale*e;Vector3 d=Unit(new Vector3(Finite(a.ReactionDir.x,0),0,Finite(a.ReactionDir.z,-1)));
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
        /// out inside the stun) and jolted by a melee hit (pushed back toward the eye): camera-local offset (metres) and pitch (degrees).
        /// </summary>
        public static Vector3 ViewDip(Actor a,float time,HitSource hit,out float pitch)
        {
            pitch=0;if(a==null||!(a.Hp>0))return Vector3.Zero;
            float daze=a.ReactionParry?0:Daze(time,a.ReactionAt>-90&&a.ReactionAt<=time&&a.StunUntil>a.ReactionAt?a.ReactionAt:time,a.StunUntil);
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
    /// Client-only record of the latest classified hit on, and block by, each pilot (host, guests and the killcam each keep
    /// their own, fed every rendered frame from the snapshot's events; events are read once by id). No allocation once warm.
    /// </summary>
    public sealed class NativeHitLedger
    {
        private struct Strike{public int Source;public float At,Amount;}
        private readonly Dictionary<int,HitSource> hits=new Dictionary<int,HitSource>(),blocks=new Dictionary<int,HitSource>();
        private readonly Dictionary<int,Strike> strikes=new Dictionary<int,Strike>();
        private int last;
        public void Clear(){hits.Clear();blocks.Clear();strikes.Clear();last=0;}
        private static Actor Find(Actor[] actors,int id){if(actors==null||id<=0)return null;for(int i=0;i<actors.Length;i++)if(actors[i]!=null&&actors[i].Id==id)return actors[i];return null;}
        public void Observe(Snapshot s)
        {
            var events=s?.Events;if(events==null)return;int newest=0;
            for(int i=0;i<events.Length;i++)if(events[i].Id>newest)newest=events[i].Id;
            // Event ids restart with a new arena (a new match, the killcam's replica): start over.
            if(newest>0&&newest<last)Clear();
            for(int i=0;i<events.Length;i++)
            {
                var e=events[i];if(e.Id<=last||e.Target<=0)continue;
                if(e.Kind==EventKind.Emperor||e.Kind==EventKind.Arcade)strikes[e.Target]=new Strike{Source=e.Source,At=e.Time,Amount=e.Amount};
            }
            for(int i=0;i<events.Length;i++)
            {
                var e=events[i];if(e.Id<=last)continue;
                if((e.Kind==EventKind.Hit||e.Kind==EventKind.Headshot)&&e.Target>0)
                {
                    var attacker=Find(s.Actors,e.Source);string spec=e.Source==0?null:attacker?.SourceSpec;
                    bool fighter=strikes.TryGetValue(e.Target,out var st)&&st.Source==e.Source&&Math.Abs(st.At-e.Time)<.004f;
                    hits[e.Target]=new HitSource{Known=true,Melee=NativeHitRules.Melee(e.Source,e.Detail,spec,fighter),Head=e.Kind==EventKind.Headshot,At=e.Time,
                        Heft=NativeHitRules.Heft(spec,fighter?st.Amount:e.Amount),HasFrom=attacker!=null,From=attacker!=null?attacker.Position:e.Position};
                }
                else if(e.Kind==EventKind.Block&&e.Source>0)
                {
                    // BlockProc: Source the blocker, Target the attacker (0 for a PvE add).
                    var attacker=Find(s.Actors,e.Target);string spec=attacker?.SourceSpec;
                    blocks[e.Source]=new HitSource{Known=true,Melee=attacker==null||NativeHitRules.Standard(spec)||NativeHitRules.Fighter(spec)||spec=="siren",At=e.Time,
                        Heft=attacker==null?.9f:NativeHitRules.Heft(spec,0),HasFrom=attacker!=null,From=attacker!=null?attacker.Position:e.Position};
                }
            }
            if(newest>last)last=newest;
        }
        /// <summary>The classified hit that made the reaction recorded at `reactionAt` on `target`, if this ledger saw it.</summary>
        public bool Hit(int target,float reactionAt,out HitSource hit)
        {
            if(hits.TryGetValue(target,out hit)&&Math.Abs(hit.At-reactionAt)<.004f)return true;hit=default;return false;
        }
        /// <summary>The latest block by `blocker` within `window` seconds before `time`.</summary>
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
    ///  - a dazed stun pose (guard dropped, head lolling, a slow sway, soft knees) for as long as the stun lasts, easing out
    ///    to exactly zero at its authoritative end;
    ///  - a guard shove when a block absorbs a blow, scaled by the attacker's heft;
    ///  - for gun and magic pilots (whose route shows no stunned reaction) the stunned recoil with a planted catch step.
    /// A light (unstunned) melee hit is an upper-body accent over the gait, so travel keeps working. Non-melee hits keep
    /// the source's light reaction only. No allocation per frame.
    /// </summary>
    public sealed class NativeHitLayer
    {
        private struct Accent{public bool Live,Head,Stunned;public float At,Heft,Strength,Turn,Variant,FromYaw;public Vector3 Dir;}
        private Accent current,previous;private bool seen;
        private Actor actor;private NativeHitLedger ledger;private HitVictim kind;private float rigYaw;
        private MeleeRig rig;private float now;private readonly Action change;
        private readonly Vector3[] heldAnkle=new Vector3[2],heldKnee=new Vector3[2];private readonly Quaternion[] heldFoot=new Quaternion[2];
        /// <summary>The last frame's weights (for the checks): impact envelope, daze, block shove and the summed bone offset.</summary>
        public float ImpactWeight{get;private set;}
        public float DazeWeight{get;private set;}
        public float BlockWeight{get;private set;}
        public bool Applied{get;private set;}
        public NativeHitLayer(){change=Apply;}
        /// <summary>The captured-melee and DEV fighter routes: MeleeMotion runs the layer (Layer) before its ground pass.</summary>
        public void Bind(MeleeMotion m,Actor a,NativeHitLedger hits,HitVictim victim){if(m==null)return;m.HitLayer=this;actor=a;ledger=hits;kind=victim;rigYaw=float.NaN;}
        /// <summary>The gun and magic routes: over the posed native pose (in `scratch`), before the route's Finish.</summary>
        public void Over(MeleeRig scratch,CapturedRig.Pose pose,Actor a,NativeHitLedger hits,HitVictim victim,float time,float yaw)
        {
            actor=a;ledger=hits;kind=victim;rigYaw=yaw;Applied=false;ImpactWeight=DazeWeight=BlockWeight=0;
            if(scratch==null||pose==null||!Wanted(a,time))return;scratch.ReadPose(pose);scratch.GroupYaw=yaw;Layer(scratch,time);scratch.WritePose(pose);
        }
        // Dead, juggled, knocked down, rolling, in an E special, held by the Siren, the Siren herself, or no arena ledger
        // (intro, portrait, corpse and viewer rigs): the route's own layer owns the frame.
        private static bool Skip(Actor a,float time)=>a==null||!(a.Hp>0)||a.SourceSpec=="siren"||a.GrabOwner!=0||NativeJugglePose.Active(a.JuggleUntil,time)||NativeSpecialPose.Down(a,time)||a.Rolling(time)||NativeSpecialPose.Active(a,time);
        private bool Wanted(Actor a,float time)=>ledger!=null&&!Skip(a,time)&&(a.ReactionAt>-90&&time>=a.ReactionAt&&time<a.ReactionAt+2||a.StunUntil>time||time<a.BlockUntil+.5f);
        // per-frame values Apply reads
        private float stunAt,stunUntil,daze,blockE,blockHeft;private Vector3 blockDir;private bool stepping,stepBased;private float stepU,stepPlant,stepAt;private Vector3 stepDir,stepBase;private float stepHeft;
        public void Layer(MeleeRig r,float time)
        {
            Applied=false;ImpactWeight=DazeWeight=BlockWeight=0;var a=actor;if(r==null||!Wanted(a,time))return;
            float yaw=float.IsNaN(rigYaw)?r.GroupYaw:rigYaw;rig=r;now=time;
            // The reaction being shown (a new one keeps the interrupted blow's tail for .09 s).
            bool reaction=a.ReactionAt>-90&&time>=a.ReactionAt;
            if(reaction&&(!seen||current.At!=a.ReactionAt))
            {
                bool known=ledger.Hit(a.Id,a.ReactionAt,out var hit);bool stunned=a.StunUntil>a.ReactionAt+.02f;
                // Unknown source (a missed event): a stun only comes from melee, so a stunned blow is melee of medium heft. A
                // parried attacker's reel is the route's own (the source's parry motion).
                bool melee=!a.ReactionParry&&(known?hit.Melee:stunned);
                seen=true;previous=current;current=new Accent{Live=melee,Head=a.ReactionZone==1||known&&hit.Head,Stunned=stunned,At=a.ReactionAt,Heft=known?hit.Heft:1,
                    Strength=NativeHitRules.Finite(a.ReactionStrength,.6f),Turn=NativeHitRules.Finite(a.ReactionTurn,0),Variant=NativeHitRules.Variant(a.ReactionAt),FromYaw=NativeHitRules.Finite(a.ReactionYaw,0),
                    Dir=new Vector3(NativeHitRules.Finite(a.ReactionDir.x,0),0,NativeHitRules.Finite(a.ReactionDir.z,-1))};
                if(current.Dir.LengthSquared()<1e-6f)current.Dir=new Vector3(0,0,-1);else current.Dir=Vector3.Normalize(current.Dir);
                // A blow chained within .8 s of the last one takes the other variant, so a combo reads hit by hit.
                if(previous.Live&&current.At-previous.At<.8f)current.Variant=-previous.Variant;
            }
            // A rewind (the killcam) or a cleared reaction starts over.
            if(a.ReactionAt<=-90||seen&&time<current.At){seen=false;current.Live=previous.Live=false;}
            // The stun this blow (or a directionless stun) holds.
            stunUntil=a.StunUntil;stunAt=reaction&&a.StunUntil>a.ReactionAt?a.ReactionAt:Math.Max(-100,a.StunUntil-.3f);
            daze=a.StunUntil>time&&!a.ReactionParry?NativeHitRules.Daze(time,stunAt,a.StunUntil):0;
            // A block absorbing a blow.
            blockE=0;blockHeft=0;blockDir=new Vector3(0,0,-1);
            if(ledger.Block(a.Id,time,.5f,out var block)&&a.BlockUntil>=block.At-.05f)
            {
                blockHeft=MeleeMath.Clamp(block.Heft,NativeHitRules.MinHeft,NativeHitRules.MaxHeft);blockE=NativeHitRules.Envelope(time-block.At,.026f,.03f,.2f+.1f*blockHeft);
                if(block.HasFrom){var d=NativeHitRules.Local(new V3(a.Position.x-block.From.x,0,a.Position.z-block.From.z),yaw);d.Y=0;if(d.LengthSquared()>1e-6f)blockDir=Vector3.Normalize(d);}
            }
            bool grounded=a.Grounded;stepping=false;
            if(current.Live&&current.Stunned&&(kind==HitVictim.Gun||kind==HitVictim.Magic)&&grounded&&time<stunUntil)
            {
                float span=Math.Max(.05f,stunUntil-current.At),u=(time-current.At)/span;stepping=true;stepU=MeleeMath.Ease((u-.1f)/.35f);
                stepPlant=Math.Min(1,MeleeMath.Ease((time-current.At)/.05f))*(1-MeleeMath.Ease((u-.72f)/.28f));stepDir=NativeHitRules.Reframe(current.Dir,current.FromYaw,yaw);stepHeft=current.Heft;
            }
            // Nothing to add this frame: leave the route's pose exactly as it is.
            bool impact=Showing(ref current,time)||previous.Live&&previous.At<current.At&&time-current.At<.09f&&Showing(ref previous,time);
            if(!impact&&!(daze>0)&&blockE==0&&!stepping)return;
            // While stunned (planted feet) keep the ankles where the route put them; otherwise the ground pass re-plants the gait's feet.
            bool hold=grounded&&(r.LegsAuthored||r.LegsLayered||a.StunUntil>time);
            if(hold)for(int i=0;i<2;i++)heldAnkle[i]=r.ReadAnkle(i==0,out heldFoot[i],out heldKnee[i]);
            // The catch step is measured from where the foot stood when the blow landed (the gait follows the placed ankles,
            // so a step measured from the current ankle would keep adding up).
            if(stepping&&(!stepBased||stepAt!=current.At)){stepBased=true;stepAt=current.At;stepBase=heldAnkle[stepDir.X>=0?0:1];}
            change();
            if(hold)
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
            // The interrupted blow fades out under the new one, so chained hits read as separate impacts, not a pop.
            if(previous.Live&&previous.At<current.At)Impact(r,ref previous,time,yaw,1-MeleeMath.Ease((time-current.At)/.09f));
            if(current.Live)ImpactWeight=Impact(r,ref current,time,yaw,1);
            if(daze>0)Daze(r,time,daze);
            if(blockE!=0)Shove(r,blockE*blockHeft*(kind==HitVictim.Melee?.55f:1));
            DazeWeight=daze;BlockWeight=blockE;
        }
        // The blow's impact window: its stun (or a light hit's own span), as Impact times it.
        private float Span(ref Accent h,float heft)=>h.Stunned?Math.Max(.05f,(stunUntil>h.At?stunUntil:h.At+.05f)-h.At):NativeHitRules.LightSpan(heft);
        private bool Showing(ref Accent h,float time)
        {
            if(!h.Live||time<h.At)return false;float heft=MeleeMath.Clamp(h.Heft,NativeHitRules.MinHeft,NativeHitRules.MaxHeft);
            NativeHitRules.Timing(heft,Span(ref h,heft),out float rise,out float hold,out float fall);return time-h.At<rise+hold+fall;
        }
        // The impact accent. Base routes that already recoil (captured melee, fighters) get a lighter accent; gun and magic
        // pilots, whose routes show nothing while stunned, get the full stunned recoil from it.
        private float Impact(MeleeRig r,ref Accent h,float time,float yaw,float weight)
        {
            if(!(weight>0))return 0;float age=time-h.At;if(age<0)return 0;
            float heft=MeleeMath.Clamp(h.Heft,NativeHitRules.MinHeft,NativeHitRules.MaxHeft);
            NativeHitRules.Timing(heft,Span(ref h,heft),out float rise,out float hold,out float fall);if(age>=rise+hold+fall)return 0;
            bool native=kind==HitVictim.Gun||kind==HitVictim.Magic;float route=h.Stunned?(native?1.3f:.75f):(native?.8f:.9f);
            float A=heft*(.6f+.4f*MeleeMath.Clamp(h.Strength,0,1))*route*weight;
            float e1=NativeHitRules.Envelope(age,rise,hold,fall),e2=NativeHitRules.Envelope(age-.016f,rise,hold,fall),e3=NativeHitRules.Envelope(age-.03f,rise,hold,fall),e4=NativeHitRules.Envelope(age-.045f,rise,hold,fall);
            Vector3 d=NativeHitRules.Reframe(h.Dir,h.FromYaw,yaw);float front=Math.Max(0,-d.Z),back=Math.Max(0,d.Z),sx=d.X,v=h.Variant;
            bool moving=!h.Stunned&&actor!=null&&actor.Velocity.x*actor.Velocity.x+actor.Velocity.z*actor.Velocity.z>1;
            // The root presentation (hips) is knocked along the blow and dips; the planted feet hold while stunned.
            Vector3 b=r.P[CapturedRig.Body];float push=(h.Stunned?.05f:.02f)*(moving?.5f:1);
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
        // A block absorbing a blow: the guard is driven up toward the face and the body is shoved back from the attacker.
        private void Shove(MeleeRig r,float k)
        {
            Vector3 d=blockDir;Vector3 b=r.P[CapturedRig.Body];b.X+=d.X*.035f*k;b.Z+=d.Z*.035f*k;b.Y-=.02f*k;r.P[CapturedRig.Body]=b;
            r.AddE(CapturedRig.Spine,d.Z*.1f*k,0,-d.X*.06f*k);r.AddE(CapturedRig.Chest,d.Z*.06f*k,0,0);r.AddE(CapturedRig.Head,d.Z*.08f*k,0,-d.X*.08f*k);
            r.Premultiply(CapturedRig.LClav,MeleeMath.Euler(-.1f*k,0,0));r.Premultiply(CapturedRig.RClav,MeleeMath.Euler(-.1f*k,0,0));
        }
    }
}
