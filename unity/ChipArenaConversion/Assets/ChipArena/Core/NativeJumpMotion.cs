using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The jump animation (owner 2026-10-12: "can we add jump animations? (Of course, changes based on weapon. Daggers can
    /// double-jump)"): which of the owner's Explosive pack jump sets (tools/export_pack_jumps.mjs, NativePackJumps.Generated)
    /// each weapon plays, how its phases are read from the presented pilot, and the timing of the dagger air flip.
    /// Presentation only: pure functions and constants, never read by authority. Jump physics, input, prediction, bots and
    /// the double-jump rule itself are simulation (Astra; see docs/MELEE-FEEL.md "Jump and double jump").
    /// </summary>
    public static class NativeJumpRules
    {
        /// <summary>Sets in NativePackJumps order.</summary>
        public const int None=-1,Sword=0,Daggers=1,Greatsword=2,Shield=3,Shooting=4,Staff=5,Unarmed=6;
        /// <summary>Slots in NativePackJumps.Slots order.</summary>
        public const int Guard=0,Jump=1,Fall=2,Land=3,Flip=4;
        /// <summary>
        /// The pack set closest to the weapon, as the melee and reaction sets map them: Armed for the sword, Armed in a reverse
        /// grip for the daggers, 2Hand-Sword for the greatsword, the Armed body for the shield (its arm keeps its own guard),
        /// 2Hand-Shooting for the rifle and machine gun, Unarmed for the pistol and the DEV fighters' fists, 2Hand-Staff for the
        /// four schools and L33t H4x. None for the Siren, whose own drift keeps its air tuck (NativeSirenDrift).
        /// </summary>
        public static int SetFor(string spec)
        {
            switch(spec)
            {
                case "sword":return Sword;case "daggers":return Daggers;case "greatsword":return Greatsword;case "shield":return Shield;
                case "rifle":case "machinegun":return Shooting;case "pistol":case "emperor":case "ansatsuken":return Unarmed;
                case "fire":case "water":case "lightning":case "darkness":case "leet":return Staff;
                default:return None;
            }
        }
        public static CapturedClip Clip(int set,int slot)
        {
            if(set<0||set>=NativePackJumps.Sets.Length)return null;var s=NativePackJumps.Sets[set];return slot>=0&&slot<s.Length?s[slot]:null;
        }
        /// <summary>Whether a second upward impulse in the air shows the dagger flip (the owner's "Daggers can double-jump").</summary>
        public static bool Flips(string spec)=>spec=="daggers";

        // ---- phase detection on the presented root (host, guests, killcam and spectators all draw the same thing) ----
        /// <summary>
        /// A guest draws remote pilots about .1 s behind the newest host frame while the replicated contact flag is the newest
        /// frame's, so the flag alone would land a guest's pilot in mid-air. The tracker reads the presented root's vertical
        /// motion and lets the flag only confirm it: a takeoff once the flag says airborne and the root moves (or after
        /// Confirm), or the root rises fast before the flag (the guest's own predicted jump); a landing once the flag says
        /// grounded and the root has stopped descending (slower than Stopped, or after Confirm). m/s and seconds.
        /// </summary>
        public const float Moving=1.2f,Stopped=.6f,TakeoffRise=3.5f,Confirm=.2f,VelocityRate=60,SampleHold=.05f;
        /// <summary>
        /// An air jump: while airborne the presented vertical speed rises AirJumpRise above its running minimum (gravity alone
        /// only lowers it), ends above AirJumpMin, at least AirJumpAfter after the takeoff and AirJumpGap after the last one,
        /// and no stun, juggle, knockdown, roll or special owns the pilot. No simulation signal exists yet; an explicit
        /// replicated AirJumpAt would replace this (handoff in docs/MELEE-FEEL.md).
        /// </summary>
        public const float AirJumpRise=3f,AirJumpMin=2f,AirJumpAfter=.12f,AirJumpGap=.2f;
        /// <summary>The base jump impulse (Rules.JumpSpeed; the chip stat scales it) and gravity, for an unknown launch.</summary>
        public const float JumpSpeed=8.5f,Gravity=23;

        // ---- pose timing ----
        /// <summary>The push-off blend after a jump's takeoff, the blend into the fall loop after walking off a ledge, the
        /// dagger flip's length and the share of it the tuck covers.</summary>
        public const float PushBlend=.06f,FallBlend=.15f,FlipSeconds=.45f,LegReturn=.08f;
        /// <summary>The flip's turn (fraction of a full turn) at u = age / FlipSeconds: a fast tucked rotation that leaves
        /// and lands at rest (a cycloid, finished by u = .85, the rest held upright for the fall).</summary>
        public static float FlipTurn(float u){float x=MeleeMath.Clamp(u/.85f,0,1);return x-(float)Math.Sin(MeleeMath.TAU*x)/MeleeMath.TAU;}
        /// <summary>The impact's strength from the fall speed: 0 below 2.5 m/s, .67 after a standing jump (8.5 m/s), 1 from a
        /// 3 m drop (11.7 m/s) on.</summary>
        public static float Impact(float fallSpeed)=>MeleeMath.Clamp((fallSpeed-2.5f)/9f,0,1);
        /// <summary>The landing's length: longer for a harder impact, shorter while running on (the gait carries it).</summary>
        public static float LandSeconds(float impact,float run)=>MeleeMath.Lerp(.3f,.46f,impact)*(1-.35f*MeleeMath.Clamp(run,0,1));
        /// <summary>The deepest hips drop a landing may add (rig metres): the capture's squat eased onto the voxel legs.</summary>
        public const float MaxDrop=.15f;
        /// <summary>First person: the view model's hop (NativeViewSway: a spring of ViewHopOmega, damping .6, in metres; a kick of
        /// .16 m/s peaks near 8 mm): the hands lag the body on a takeoff and an air jump and sink on a landing, by the fall speed.</summary>
        public const float ViewTakeoffKick=.16f,ViewAirJumpKick=.2f,ViewLandKick=.1f,ViewLandFall=.12f,ViewHopOmega=12,TakeoffSpeed=2.5f;
        /// <summary>The view hop's kick (m/s down) for one view-layer step: a takeoff (contact lost rising faster than TakeoffSpeed,
        /// the move.jump rule), an air jump (the replicated vertical speed stepping up by AirJumpRise in the air) or a landing
        /// (by the last fall speed); 0 otherwise.</summary>
        public static float ViewHop(bool wasGrounded,bool grounded,float lastVy,float vy)
        {
            if(!MathUtil.Finite(vy)||!MathUtil.Finite(lastVy))return 0;
            if(grounded)return wasGrounded?0:ViewLandKick+ViewLandFall*MeleeMath.Clamp(-lastVy/12,0,1);
            if(wasGrounded)return vy>TakeoffSpeed?ViewTakeoffKick:0;
            return vy-lastVy>AirJumpRise&&vy>AirJumpMin?ViewAirJumpKick:0;
        }
    }

    /// <summary>
    /// Jump phases of one presented pilot from what every peer draws (NativeJumpRules): the contact flag, the presented root
    /// height and the presentation clock. Takeoff (a jump, or walking off a ledge), the airtime's vertical speed, air jumps,
    /// and the landing with its fall speed. A rewind or a gap (the killcam, a joining guest) restarts it on the current state
    /// with no event. No allocation.
    /// </summary>
    public sealed class NativeJumpTrack
    {
        public bool Has,Airborne,Jumped;
        /// <summary>Takeoff, landing and last air-jump times; the fall speed of the last landing (m/s, positive); the floor's
        /// root height before the takeoff; the presented vertical speed (smoothed) and the launch (the highest vertical
        /// speed shortly after the last takeoff or air jump).</summary>
        public float AirAt=-100,LandAt=-100,AirJumpAt=-100,FallSpeed,Floor,Vy,Launch=NativeJumpRules.JumpSpeed,Time=-100;
        public int AirJumps,Takeoffs,Landings;
        private float lastY,lastAt,raw,flagAt,minRecent,minAir;private bool flagAir;
        /// <summary>
        /// The hook for the explicit simulation signal (a replicated air-jump time, docs/MELEE-FEEL.md "Jump and double jump"): the
        /// caller passes it (-100 for none) each frame after Observe; from the first call, air jumps come only from it (no inference).
        /// </summary>
        public bool Explicit;
        public void AirJumpSignal(float at)
        {
            Explicit=true;if(!(at>-90)||!MathUtil.Finite(at))return;
            if(Airborne&&at>AirJumpAt+1e-4f&&at>=AirAt&&at<=Time+1e-4f){AirJumpAt=at;AirJumps++;minRecent=minAir=Vy;Launch=Math.Max(4,Vy);}
        }
        public void Reset(){Explicit=false;Has=false;Airborne=Jumped=false;AirAt=LandAt=AirJumpAt=-100;AirJumps=Takeoffs=Landings=0;FallSpeed=Vy=raw=0;Launch=NativeJumpRules.JumpSpeed;Time=-100;}
        /// <summary>One presented frame: `grounded` the replicated contact flag, `y` the presented root height (any fixed offset),
        /// `blocked` an overlay that owns the pilot (stun, juggle, knockdown, roll, special), which suppresses air jumps.</summary>
        public void Observe(float time,bool grounded,float y,bool blocked)
        {
            if(!MathUtil.Finite(time)||!MathUtil.Finite(y))return;
            if(!Has||time<Time-MeleeGait.REWIND||time-Time>.35f)
            {
                Has=true;Time=lastAt=time;lastY=y;Vy=raw=0;flagAir=!grounded;flagAt=time;Airborne=!grounded;Jumped=false;AirAt=Airborne?time-1:-100;LandAt=-100;AirJumpAt=-100;
                AirJumps=0;minRecent=minAir=0;Floor=y;FallSpeed=0;Launch=NativeJumpRules.JumpSpeed;return;
            }
            // A root that moves in fixed steps (a guest's own predicted pilot moves at the 60 Hz tick while frames run faster)
            // is measured from step to step: an unchanged height is a new sample only once SampleHold has passed.
            Time=Math.Max(Time,time);float dt=time-lastAt;
            // (A root at rest, or held while the flag says grounded, is sampled every frame: the first step out of rest is
            // measured over one frame, and a stepped root that stops on the floor reads as stopped at once.)
            if(dt>1e-5f&&(Math.Abs(y-lastY)>1e-6f||dt>=NativeJumpRules.SampleHold||Math.Abs(raw)<1e-4f||grounded)){raw=(y-lastY)/dt;Vy+=(raw-Vy)*(1-(float)Math.Exp(-dt*NativeJumpRules.VelocityRate));lastAt=time;lastY=y;}
            bool air=!grounded;if(air!=flagAir){flagAir=air;flagAt=time;}
            float flagAge=time-flagAt;
            if(!Airborne)
            {
                if(flagAir&&(Math.Abs(raw)>NativeJumpRules.Moving||flagAge>=NativeJumpRules.Confirm)||raw>NativeJumpRules.TakeoffRise)
                {Airborne=true;AirAt=time;Jumped=raw>NativeJumpRules.Moving;AirJumps=0;Vy=raw;minRecent=minAir=Vy;Launch=Jumped?Math.Max(raw,4):NativeJumpRules.JumpSpeed;Takeoffs++;}
                else Floor=y;
                return;
            }
            // The launch window (AirJumpAfter after a takeoff or an air jump): a guest's interpolated rise can still speed up
            // from one snapshot segment to the next; that is the same launch, never a new air jump.
            if(time-Math.Max(AirAt,AirJumpAt)<NativeJumpRules.AirJumpAfter){Launch=Math.Max(Launch,Vy);minRecent=Vy;}
            else if(!Explicit&&!blocked&&Vy-minRecent>NativeJumpRules.AirJumpRise&&Math.Max(raw,Vy)>NativeJumpRules.AirJumpMin&&time-AirJumpAt>NativeJumpRules.AirJumpGap)
            {AirJumpAt=time;AirJumps++;minRecent=minAir=Vy;Launch=Math.Max(4,Vy);return;}
            if(blocked||Vy<minRecent)minRecent=Vy;
            minAir=Math.Min(minAir,Vy);
            if(!flagAir&&(raw>-NativeJumpRules.Stopped||flagAge>=NativeJumpRules.Confirm))
            {Airborne=false;LandAt=time;FallSpeed=Math.Max(0,-minAir);Landings++;Floor=y;Vy=0;}
        }
    }

    /// <summary>
    /// The per-weapon jump layer over a route's pose (Core/MeleeMotion for the captured melee weapons and the DEV fighters,
    /// GunMotion, MagicMotion), driven by the presented airtime, not a fixed timeline, so it follows the real arc:
    ///  - takeoff: the pack Jump take from its push-off frame, blended in over PushBlend (a remote pilot's jump is only known
    ///    once it leaves the ground, so there is no anticipation crouch); walking off a ledge eases into the Fall loop;
    ///  - rise, apex and fall by vertical speed: the take's push-off to its tuck while rising, the tuck to its fall-entry
    ///    pose past the apex, then the Fall loop as the descent gathers speed;
    ///  - landing: the Land take's compression, its hips drop scaled by the fall speed (NativeJumpRules.Impact) on top of
    ///    the gait's own small dip, shorter while running on; the legs return from the air pose to the gait's planted feet
    ///    over LegReturn as layered legs, so the ground pass keeps the ankles on the floor;
    ///  - the dagger air jump: the Jump-Flip's tuck with a procedural front flip about the hips (FlipTurn over
    ///    FlipSeconds); a landing mid-flip finishes or unwinds the turn within .1 s.
    /// Torso and arms are laid as offsets from the set's guard frame (NativePackPose.LayerRows: the weapon stays in its
    /// socket, a two-handed hilt keeps its support hand, blades are kept clear of the head and torso). The route's own
    /// upper-body actions keep priority: a swing, block or special drops the arms and most of the torso; the gun hold, the
    /// cast and the staff are posed after this layer. Juggles, knockdowns, rolls, stuns and death own the pilot (weight 0).
    /// In the air an ankle never drops below the floor the pilot left. Presentation only, no allocation per frame.
    /// </summary>
    public sealed class NativeJumpMotion
    {
        public readonly NativeJumpTrack Track=new NativeJumpTrack();
        private readonly NativePackPose pack=new NativePackPose();
        private readonly float[] row=new float[CapturedClip.Channels],rowB=new float[CapturedClip.Channels],row0=new float[CapturedClip.Channels];
        private readonly Quaternion[] held=new Quaternion[6];private bool hasHeld;
        private float own,phase,fall,last=-100,flipAt=-100,flipLand=-100,flipFrom;
        private static readonly int[] Legs={CapturedRig.LHip,CapturedRig.LKnee,CapturedRig.LFoot,CapturedRig.RHip,CapturedRig.RKnee,CapturedRig.RFoot};
        /// <summary>The layer's state (rise, apex, fall, land, flip or ground) and its weights, as the last frame left them.</summary>
        public string State{get;private set;}="ground";
        public float Weight=>own;public float AirWeight{get;private set;}public float LandWeight{get;private set;}
        /// <summary>The dagger flip's current turn (radians about the hips' lateral axis), 0 outside a flip.</summary>
        public float FlipAngle{get;private set;}
        /// <summary>The landing's added hips drop this frame (rig metres).</summary>
        public float Drop{get;private set;}
        public const string Rise="jump rise",Apex="jump apex",Fall="fall",Landing="land",AirFlip="air flip",Ground="ground";

        /// <summary>The captured melee route (Core/MeleeMotion.Sample, after the action layers, before the hit layer and the
        /// ground pass). Swings, blocks and specials keep the upper body; juggles, knockdowns, rolls and stuns own the pilot.</summary>
        /// <summary>The hook for a heavier landing (the melee jump attack, NativeAirAttackRules.LandScale): scales the landing's
        /// hips drop; 1 otherwise. Set by MeleeMotion before each Melee call.</summary>
        public float LandScale=1;
        /// <summary>The hook for a swing whose own footwork carries the pilot through the air (the greatsword's leaping sprint
        /// attack, MeleeMotion.SprintCut over the authority's hop): no air or landing pose from the layer while it is set (the
        /// track is still observed). Set by MeleeMotion before each Melee call; it clears after the call.</summary>
        public bool Leap;
        public void Melee(MeleeRig r,MeleeView p,float time,float dt)
        {
            bool leap=Leap;Leap=false;
            bool blocked=p.Hp<=0||p.Juggle||p.Down||p.Rolling(time)||p.HitStunUntil>time||p.Action(time);
            bool busy=MeleeMotion.AttackPose(p,time).Active||p.Guarding||p.BlockTime>0;
            int set=NativeJumpRules.SetFor(p.Spec);bool shield=set==NativeJumpRules.Shield;
            // The shield rides the chest: half the clip's torso turn and a shallower squat keep it off the head and the floor.
            Layer(r,set,p.Spec,p.Grounded,time,dt,blocked,(busy?.35f:1)*(shield?.5f:1),busy?0:1,!shield,MeleeSet.For(p.Spec),set==NativeJumpRules.Greatsword,true,1-MeleeMath.Ease(r.Motion/.2f),(shield?.55f:1)*LandScale,p.AirJumpAt,leap);
        }
        /// <summary>The DEV fighters (MeleeMotion.FighterFinish): only the legs and hips of a plain jump; a style action (their
        /// own air motion), a juggle, knockdown, dodge or stun keeps the style's pose, and legs the style authors stay its own.</summary>
        public void Fighter(MeleeRig r,MeleeView p,float time,float dt)
        {
            bool blocked=p.Hp<=0||p.Juggle||p.Down||p.Rolling(time)||p.HitStunUntil>time||p.Action(time);
            Layer(r,NativeJumpRules.SetFor(p.Spec),p.Spec,p.Grounded,time,dt,blocked,0,0,false,null,false,!r.LegsAuthored,0,airJumpAt:p.AirJumpAt);
        }
        /// <summary>A gun or magic route (GunMotion, MagicMotion: after the gait, before the gun hold or the magic layer, which
        /// keep their arms and aim over it).</summary>
        public void Ranged(MeleeRig r,Actor a,string spec,float time,float dt,float torso)
        {
            bool blocked=a.Hp<=0||a.Rolling(time)||a.StunUntil>time||a.JuggleUntil>time||NativeSpecialPose.Active(a,time)||NativeSpecialPose.Down(a,time);
            Layer(r,NativeJumpRules.SetFor(spec),spec,a.Grounded,time,dt,blocked,torso,0,false,null,false,true,0,airJumpAt:a.AirJumps>0?a.AirJumpAt:-100);
        }
        private static Quaternion Rot(float[] f,int i){Vector3 v=new Vector3(f[i],f[i+1],f[i+2]);float a=v.Length();return a<1e-9f||!(a<1e3f)?Quaternion.Identity:MeleeMath.AxisAngle(v/a,a);}
        private static Quaternion Delta(float[] a,float[] b,int i)=>MeleeMath.Inverse(Rot(a,i))*Rot(b,i);
        /// <summary>The share of the clip's neck and head turn kept: a pilot in the air keeps looking where it aims (the pack's
        /// tucks and landings drop the big voxel head onto the chest).</summary>
        public const float GazeKeep=.35f;
        private void Gaze(float[] f){for(int c=MeleeCapture.Neck;c<MeleeCapture.Head+3;c++)f[c]=row0[c]+(f[c]-row0[c])*GazeKeep;}
        private static float Smooth(float x,float target,float rate,float dt)=>x+(target-x)*(1-(float)Math.Exp(-rate*dt));
        /// <summary>
        /// The layer. `torso` and `arms` (0..1): the shares of the clip's torso and hand travel; `main`: the weapon hand
        /// follows the clip (off: the shield arm keeps its guard); `fit`: the held weapon kept clear; `legs`: the clip's legs
        /// in the air; `still`: how far the route's captured guard owns the hips height (the melee guard, standing), which
        /// overrides the gait's own landing dip.
        /// </summary>
        public void Layer(MeleeRig r,int set,string spec,bool grounded,float time,float dt,bool blocked,float torso,float arms,bool main,MeleeSet fit,bool twoHand,bool legs,float still,float dropScale=1,float? airJumpAt=null,bool leap=false)
        {
            if(r==null)return;
            bool restart=last<-90||time<last-MeleeGait.REWIND||time-last>.35f;last=time;dt=restart?1/60f:MeleeMath.Clamp(dt,0,.05f);
            var t=Track;if(airJumpAt.HasValue)t.Explicit=true;t.Observe(time,grounded,r.GroupPosition.Y,blocked);
            if(airJumpAt.HasValue)t.AirJumpSignal(airJumpAt.Value);
            if(restart){own=blocked?0:1;phase=fall=-1;flipAt=flipLand=-100;hasHeld=false;}
            own=blocked||set<0?0:Smooth(own,1,20,dt);
            AirWeight=LandWeight=FlipAngle=Drop=0;State=Ground;
            var guard=NativeJumpRules.Clip(set,NativeJumpRules.Guard);if(guard==null||own<=1e-4f){hasHeld=false;return;}
            guard.Sample(0,row0);float yaw0=row0[MeleeCapture.Yaw];
            if(t.AirJumps>0&&t.AirJumpAt>flipAt&&t.Airborne&&NativeJumpRules.Flips(spec)){flipAt=t.AirJumpAt;flipLand=-100;}
            if(leap){hasHeld=false;phase=fall=-1;return;}
            if(t.Airborne)Air(r,set,time,dt,torso,arms,main,fit,twoHand,legs);
            else Land(r,set,time,dt,torso,arms,main,fit,twoHand,still,dropScale);
        }
        private void Air(MeleeRig r,int set,float time,float dt,float torso,float arms,bool main,MeleeSet fit,bool twoHand,bool legs)
        {
            var t=Track;var jump=NativeJumpRules.Clip(set,NativeJumpRules.Jump);var loop=NativeJumpRules.Clip(set,NativeJumpRules.Fall);
            float age=time-t.AirAt,presence=t.Jumped?MeleeMath.Ease(age/NativeJumpRules.PushBlend):MeleeMath.Ease(age/NativeJumpRules.FallBlend);
            // Rise: the take's push-off to its tuck by the share of the launch spent; past the apex the tuck to the fall-entry
            // pose, then the Fall loop as the descent reaches about half the launch speed (straight to it off a ledge).
            float J=Math.Max(4,t.Launch),vy=t.Vy,m1=jump.Marks[1],m2=jump.Marks[2],m3=jump.Marks[3];
            float targetPhase=vy>=0?m1+(m2-m1)*MeleeMath.Smooth(1-vy/J):m2+(m3-m2)*MeleeMath.Ease(-vy/J/.7f);
            float targetFall=t.Jumped||t.AirJumps>0?MeleeMath.Ease((-vy/J-.35f)/.45f):1;
            // The phase follows a critically quick spring, so an air jump or a late takeoff call never snaps the pose.
            if(phase<0){phase=t.Jumped?m1:m3;fall=targetFall;}else{phase=Smooth(phase,targetPhase,28,dt);fall=Smooth(fall,targetFall,20,dt);}
            jump.Sample(phase,row);
            if(fall>1e-4f){loop.Sample(time-t.AirAt,rowB);MeleeCapture.BlendRowsTurning(row,rowB,fall,row);}
            float w=own*presence;AirWeight=w;State=vy>2?Rise:vy>-2?Apex:Fall;
            // The hips turn of the blended take (the flip's own pelvis channel turns through a full circle: the flip is procedural).
            Quaternion pelvis=MeleeMath.Slerp(Quaternion.Identity,Delta(row0,row,MeleeCapture.Pelvis),.6f*w);
            float flipU=(time-flipAt)/NativeJumpRules.FlipSeconds;
            if(flipU>=0&&flipU<1)
            {
                var flip=NativeJumpRules.Clip(set,NativeJumpRules.Flip);flip.Sample(flipU*flip.Duration,rowB);
                float fw=MeleeMath.Ease(flipU/.1f)*(1-MeleeMath.Ease((flipU-.8f)/.2f));MeleeCapture.BlendRowsTurning(row,rowB,fw,row);
                FlipAngle=MeleeMath.TAU*NativeJumpRules.FlipTurn(flipU);State=AirFlip;
            }
            Gaze(row);
            pack.LayerRows(r,row,row0,row0[MeleeCapture.Yaw],w*MeleeMath.Clamp(torso,0,1),0,arms,main,fit,twoHand,0,0);
            r.SetQ(CapturedRig.Pelvis,r.Q[CapturedRig.Pelvis]*pelvis);
            if(legs)
            {
                for(int i=0;i<2;i++)
                {
                    int hip=i==0?CapturedRig.LHip:CapturedRig.RHip;
                    r.Slerp(hip,Rot(row,i==0?MeleeCapture.LHip:MeleeCapture.RHip),w);
                    r.Slerp(hip+1,MeleeMath.AxisAngle(MeleeMath.X,Math.Max(0,row[i==0?MeleeCapture.LKnee:MeleeCapture.RKnee])),w);
                    r.Slerp(hip+2,Rot(row,i==0?MeleeCapture.LAnkle:MeleeCapture.RAnkle),w);
                }
                r.LegsLayered=true;
            }
            Spin(r,FlipAngle);
            KeepAboveFloor(r);
            for(int k=0;k<6;k++)held[k]=r.Q[Legs[k]];hasHeld=legs;
        }
        // The flip about the hips' lateral axis, pivoting a little above them (the tucked body's centre).
        private static readonly Vector3 Pivot=new Vector3(0,.1f,0);
        private static void Spin(MeleeRig r,float angle)
        {
            if(Math.Abs(angle)<1e-5f)return;Quaternion q=MeleeMath.AxisAngle(MeleeMath.X,angle);
            r.P[CapturedRig.Body]+=Pivot-Vector3.Transform(Pivot,q);r.SetQ(CapturedRig.Body,q*r.Q[CapturedRig.Body]);
        }
        // In the air the ankles and toes stay above the floor the pilot left (rig space: the stance floor lowered by the
        // presented height gained since): a push-off or a low air pose near the floor never dips a foot into it.
        private void KeepAboveFloor(MeleeRig r)
        {
            float floor=MeleeRig.FLOOR-Math.Max(0,r.GroupPosition.Y-Track.Floor);
            for(int i=0;i<2;i++)
            {
                bool left=i==0;Vector3 ankle=r.ReadAnkle(left,out Quaternion q,out Vector3 knee);Vector3 toe=r.ChainPoint(left?CapturedRig.LToe:CapturedRig.RToe,-1);
                float lift=Math.Max(floor-ankle.Y,floor-.02f-toe.Y);if(lift<=1e-4f)continue;
                r.SolveLeg(left,ankle+new Vector3(0,lift,0),0,0,0,q,knee+new Vector3(0,lift,0));
            }
        }
        private void Land(MeleeRig r,int set,float time,float dt,float torso,float arms,bool main,MeleeSet fit,bool twoHand,float still,float dropScale)
        {
            var t=Track;phase=fall=-1;float since=time-t.LandAt;
            // A landing mid-flip finishes a turn past its middle and unwinds an early one, within .1 s.
            float flipU=(t.LandAt-flipAt)/NativeJumpRules.FlipSeconds;
            if(flipU>=0&&flipU<1&&since<.1f){if(flipLand!=t.LandAt){flipLand=t.LandAt;flipFrom=MeleeMath.TAU*NativeJumpRules.FlipTurn(flipU);}
                float to=flipFrom>MeleeMath.PI?MeleeMath.TAU:0;FlipAngle=flipFrom+(to-flipFrom)*MeleeMath.Ease(since/.1f);}
            if(t.Landings==0||since<0)return;
            var land=NativeJumpRules.Clip(set,NativeJumpRules.Land);float impact=NativeJumpRules.Impact(t.FallSpeed),run=MeleeMath.Clamp(r.Motion,0,1);
            float span=NativeJumpRules.LandSeconds(impact,run),u=since/span;
            if(u<1&&impact>0)
            {
                // From the contact frame (a few hundredths past the take's air pose) to the guard it settles into.
                float from=land.Marks[0]+.03f,ct=from+(land.Duration-from)*MeleeMath.Clamp(u,0,1);land.Sample(ct,row);
                float k=MeleeMath.Lerp(.45f,1,impact)*MeleeMath.Ease(since/.03f)*(1-MeleeMath.Ease((u-.7f)/.3f)),w=own*k;LandWeight=w;State=Landing;
                Gaze(row);
                pack.LayerRows(r,row,row0,row0[MeleeCapture.Yaw],w*MeleeMath.Clamp(torso,0,1),0,arms*.8f,main,fit,twoHand,0,0);
                // The hips drop: the take's squat below its guard, scaled by the impact (lighter running on), over the gait's own
                // landing dip where the route kept it.
                float squat=MeleeMath.Clamp(row0[MeleeCapture.By]-row[MeleeCapture.By],0,.4f)*(1-.45f*run),drop=Math.Min(NativeJumpRules.MaxDrop,squat*MeleeMath.Lerp(.3f,.7f,impact))*dropScale*own*MeleeMath.Ease(since/.03f);
                float extra=Math.Max(0,drop-r.Land*(1-MeleeMath.Clamp(still,0,1)));Drop=extra;
                Vector3 b=r.P[CapturedRig.Body];b.Y-=extra;b.Z-=extra*.25f;r.P[CapturedRig.Body]=b;
                r.AddE(CapturedRig.Spine,extra*.4f,0,0);
            }
            // The air legs return to the gait's planted feet as layered legs (the ground pass keeps the ankles on the floor).
            if(hasHeld)
            {
                float k=MeleeMath.Ease(since/NativeJumpRules.LegReturn);
                if(k>=1)hasHeld=false;
                // (The feet keep the gait's flat, floor-safe heading and pitch at once: a pointed air foot would dip its toe in.)
                else
                {
                    for(int i=0;i<2;i++)
                    {
                        bool left=i==0;int hip=left?CapturedRig.LHip:CapturedRig.RHip;r.ReadAnkle(left,out Quaternion foot,out _);
                        r.SetQ(hip,MeleeMath.Slerp(held[i*3],r.Q[hip],k));r.SetQ(hip+1,MeleeMath.Slerp(held[i*3+1],r.Q[hip+1],k));
                        r.PelvisFrame(out Quaternion pq,out _);r.SetQ(hip+2,MeleeMath.Inverse(pq*r.Q[hip]*r.Q[hip+1])*foot);
                    }
                    KeepAboveFloor(r);r.LegsLayered=true;if(State==Ground)State=Landing;
                }
            }
            Spin(r,FlipAngle);
        }
    }
}
