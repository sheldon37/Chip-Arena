using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>What one horde add's motion reads this frame: only what host, guests and the killcam all hold (the add's id, kind
    /// and replicated state) and the presentation clock. Unity world axes; Yaw in degrees as the authority writes it.</summary>
    public struct HordeMotionInput
    {
        public int Kind,Look,Id;public uint Seed;public float Time,Yaw,Ground;public Vector3 Root,Velocity;public bool Crawl,Possessed;public int Lost;
        /// <summary>Seconds since the attack started (LastAttack), NaN with no attack showing; its strike and recovery
        /// (NativeHordeKinds.Strike / Recover, the authority's timing) and the arm it swings (1 right, -1 left).</summary>
        public float AttackAge,Strike,Recover;public NativeHordeKinds.Attack Attack;public int Side;
        /// <summary>The pilot it is going for (nearest within reach of its senses), for the alert stance and the head.</summary>
        public bool HasTarget;public Vector3 Target;
        /// <summary>How hurt it is (0 dead .. 1 whole): a hurt add limps and sags.</summary>
        public float Health;
        /// <summary>The DEV preview's gallery drives a treadmill: the feet plant as if the body moved at Velocity.</summary>
        public bool Treadmill;
        /// <summary>A door or cell entrance (the authority walks it in from behind the opening): `EntryAge` seconds since it began,
        /// `EntryWalk` when the walk starts (the wait before it wakes the zombie, the first steps lurch through the threshold).</summary>
        public bool Entering;public float EntryAge,EntryWalk;
        /// <summary>The Hulk's roar (Core/NativeHordeRoar): `RoarAge` seconds into it while `Roaring`.</summary>
        public bool Roaring;public float RoarAge;
        /// <summary>The heavy kinds' moves the rules do not replicate yet (Core/NativeHulkMoves, docs/SIREN-ZOMBIES.md section 10;
        /// dormant until they do): the attack's move at LastAttack (Content: the content's attack, as today); a charge (`ChargeAge` s
        /// since its start; `ChargeEndAge` s since its end and how it ended once `ChargeEnded`; `ChargeBreakAge` s since a wall it
        /// ran through broke, while `ChargeBroke`).</summary>
        public NativeHulkMoves.Move Move;public bool Charging,ChargeEnded,ChargeBroke;public float ChargeAge,ChargeEndAge,ChargeBreakAge;public NativeHulkMoves.ChargeEnd ChargeHow;
        /// <summary>A wall knock (Core/NativeBreach: a breach entrance, or a Hulk breaking a wall block): `KnockAge` s since the
        /// knocking began, its blows on NativeBreach.Blows, the break at NativeBreach.Knock, the carry through the hole after.</summary>
        public bool Knocking;public float KnockAge;
        /// <summary>The horde's roaming read (Core/NativeHordeRoam: wander, idle variants, alert, search); off by
        /// default, when the gait is exactly today's.</summary>
        public HordeRoamPose Roam;
        /// <summary>The presented root (Core/NativeHordeFlow): Root, Velocity and Yaw are the smoothed, presented ones, taken as they
        /// are (no speed differentiated from the root, no turn of the gait's own but the Hulk's pivots); YawRate (rad/s, + turning
        /// right), Bank and Pitch (rad) its lean into turns and speed changes. Off (the galleries, the checks' drives): today's.</summary>
        public bool Flow;public float YawRate,Bank,Pitch;
        /// <summary>The crowd tier it is drawn at (NativeHordeModels.Full, Reduced, Impostor): the secondary motion's detail.</summary>
        public int Tier;
        /// <summary>The authority cancelled the blow (a stagger in the wind-up): `CancelAge` s ago. The shown blow stops where it
        /// was (it never strikes) and lets go over <see cref="NativeHordeGait.CancelFade"/>.</summary>
        public bool Cancelled;public float CancelAge;
    }

    /// <summary>
    /// One horde add's animation (presentation only: never read by the authority, no change to hit timing, hitboxes, damage,
    /// cooldowns, movement or the network frame), on the owner's zombie mocap (owner 2026-10-12: "Their animations also look a
    /// lot more simple then what we used to have? We do have the zombie mo-cap ones we could utilize ... (Especially there walk
    /// animations, its not great)"; Core/NativeZombieMotion, Core/NativePackZombie.Generated.cs):
    /// <list type="bullet">
    /// <item>The body is the capture: each add walks, chases or sprints on one of its kind's takes (picked by its seed), blended
    /// by its real ground speed and phased to its own stride; standing it sways on one of the pack's idles. The capture's
    /// pelvis, spine, chest, head and both reaching arms are drawn as taken, the swinging foot's lift and turn too.</item>
    /// <item>Feet that plant: each foot stands where it landed in the world until the capture lifts it, then swings to where the
    /// body will be when it lands. The cycle's cadence and each foot's strike and lift follow the take (its natural speed scaled
    /// toward the add's by the square root), so the stride stretches with the authority's speed and nothing skates, on turns or
    /// under a shove either. The legs reach their feet with the capture's own knee direction; a long stride sinks the hips.</item>
    /// <item>Attacks: the swipe, lunge and smash play the pack's blows retimed so the hand lands exactly at LastAttack + Strike
    /// (the take's wind-up held into a telegraph, then its follow-through to Recover); the spit, belly and pounce stay
    /// procedural. Hits (Core/NativeHordeReact) layer recoil, shove and stagger steps; the authority's stagger plays a captured
    /// knockback reel. Deaths fall on the pack's takes the way the killing blow pushes.</item>
    /// <item>The crawler's four-limbed trot (and an add that lost a leg, which crawls) stays procedural: the pack's crawls are a
    /// tenth of the authority's crawl speed.</item>
    /// </list>
    /// Fixed fields and rows: no allocation per frame (every clip is decoded when the first gait is made).
    /// </summary>
    public sealed partial class NativeHordeGait
    {
        // ---- per-kind gait (stride per cycle of two steps at walk and run, duty, lift, bob, crouch, lean, width) ----
        private struct Def{public float WalkStride,RunStride,WalkSpeed,RunSpeed,Duty,RunDuty,Lift,RunLift,Bob,RunBob,Crouch,RunCrouch,Hunch,RunLean,Width,ArmSwing,RunArm,Hang,Sway,Roll,Twist;}
        private static readonly Def[] defs={
            // shambler: short shuffling steps, one foot dragged, slumped, a lurch
            new Def{WalkStride=.86f,RunStride=1.42f,WalkSpeed=1.5f,RunSpeed=3.4f,Duty=.64f,RunDuty=.42f,Lift=.05f,RunLift=.11f,Bob=.018f,RunBob=.04f,Crouch=.05f,RunCrouch=.09f,Hunch=.42f,RunLean=.5f,Width=.12f,ArmSwing=.32f,RunArm=.55f,Hang=.3f,Sway=.035f,Roll=.09f,Twist=.14f},
            // runner: long low strides, pitched forward
            new Def{WalkStride=1.0f,RunStride=1.72f,WalkSpeed=2.6f,RunSpeed=4.6f,Duty=.58f,RunDuty=.34f,Lift=.07f,RunLift=.15f,Bob=.02f,RunBob=.05f,Crouch=.08f,RunCrouch=.13f,Hunch=.45f,RunLean=.55f,Width=.1f,ArmSwing=.35f,RunArm=.6f,Hang=.1f,Sway=.02f,Roll=.06f,Twist=.2f},
            // brute: wide heavy stomps
            new Def{WalkStride=.9f,RunStride=1.25f,WalkSpeed=1.3f,RunSpeed=2.4f,Duty=.66f,RunDuty=.48f,Lift=.07f,RunLift=.1f,Bob=.035f,RunBob=.055f,Crouch=.1f,RunCrouch=.13f,Hunch=.62f,RunLean=.72f,Width=.15f,ArmSwing=.26f,RunArm=.42f,Hang=.3f,Sway=.05f,Roll=.13f,Twist=.1f},
            // spitter: hunched skitter
            new Def{WalkStride=.8f,RunStride=1.3f,WalkSpeed=1.6f,RunSpeed=2.9f,Duty=.6f,RunDuty=.42f,Lift=.06f,RunLift=.1f,Bob=.015f,RunBob=.03f,Crouch=.08f,RunCrouch=.11f,Hunch=.62f,RunLean=.72f,Width=.11f,ArmSwing=.18f,RunArm=.3f,Hang=-.2f,Sway=.025f,Roll=.07f,Twist=.12f},
            // crawler: a four-limbed trot
            new Def{WalkStride=.62f,RunStride=1.05f,WalkSpeed=2.2f,RunSpeed=3.9f,Duty=.6f,RunDuty=.44f,Lift=.05f,RunLift=.09f,Bob=.012f,RunBob=.025f,Crouch=0,RunCrouch=0,Hunch=0,RunLean=0,Width=.12f,ArmSwing=0,RunArm=0,Hang=0,Sway=.015f,Roll=.06f,Twist=.1f},
            // bloater: a short waddle
            new Def{WalkStride=.66f,RunStride=.95f,WalkSpeed=1.1f,RunSpeed=1.8f,Duty=.7f,RunDuty=.56f,Lift=.045f,RunLift=.07f,Bob=.03f,RunBob=.045f,Crouch=.13f,RunCrouch=.15f,Hunch=-.08f,RunLean=.05f,Width=.17f,ArmSwing=.2f,RunArm=.3f,Hang=-.3f,Sway=.06f,Roll=.17f,Twist=.06f},
            // hulk: the brute's stomp at its scale (the gait runs in the rig's metres: its .75 / 1.15 m/s are .42 / .64 here), as wide
            new Def{WalkStride=.8f,RunStride=1.05f,WalkSpeed=.42f,RunSpeed=.64f,Duty=.7f,RunDuty=.55f,Lift=.06f,RunLift=.09f,Bob=.035f,RunBob=.05f,Crouch=.1f,RunCrouch=.12f,Hunch=.62f,RunLean=.7f,Width=.16f,ArmSwing=.22f,RunArm=.35f,Hang=.3f,Sway=.05f,Roll=.13f,Twist=.08f}};

        public const float AnkleHeight=.07f,MaxDt=.1f,ResetGap=.5f;
        /// <summary>How much of a zombie walk take the Hulk keeps walking (the rest is its own heavy stride).</summary>
        public const float HeavyTakeShare=.15f;

        // ---- state ----
        private float time=float.NaN;private uint seed;private int id=int.MinValue;
        /// <summary>The cycle phase (cycles; one cycle is a step of each foot), the smoothed ground speed and the presented yaw (Unity, deg).</summary>
        public float Phase,Speed,Yaw;
        private Vector2 root;private Vector2 velocity;
        private struct Foot{public Vector2 W,From,To;public bool Swing,Scrape;public float T0,T1,Lift;public int Cycle;public float Y,Turn,FromTurn;}
        /// <summary>A standing foot twisted this far (degrees) from the body's facing steps round (the turn in place's shuffle), and
        /// a planted one never shows more than <see cref="TwistMost"/>.</summary>
        public const float TwistStep=34,TwistMost=48;
        private bool stepFresh;
        /// <summary>Foot `i`'s turn from the body's facing (rad, the rig's +y: toward its left): a planted foot's world facing held,
        /// a swinging one's eased from its lift's to the body's by the landing.</summary>
        private float FootTwist(int i,float t)
        {
            ref var ft=ref feet[i];float deg;
            if(ft.Swing){float s=Clamp01((t-ft.T0)/Math.Max(1e-3f,ft.T1-ft.T0));deg=ft.FromTurn*(1-Sm(s));}
            else deg=Wrap(ft.Turn-Yaw);
            deg=Math.Max(-TwistMost,Math.Min(TwistMost,deg));return -deg*BossMath.Deg;
        }
        /// <summary>Foot `i`'s world facing (Unity yaw, degrees) while planted, for the checks.</summary>
        public float FootTurn(int i)=>feet[i].Turn;
        private readonly Foot[] feet=new Foot[4];
        /// <summary>The presented yaw's facing and right (Unity world, horizontal).</summary>
        public Vector2 Forward,Right;
        /// <summary>The last frame's foot (and the crawler's hand) targets in Unity world, and whether each was in the air.</summary>
        public readonly Vector3[] FootWorld=new Vector3[4];public readonly bool[] FootAir=new bool[4];
        /// <summary>Footfalls this frame (a foot that planted), for the dust and the footfall cues (Core NativeFootfalls).</summary>
        public int Landed;
        /// <summary>Whether the last frame walked on four limbs (the crawler, a crawl after a lost leg, a kind with no capture): its
        /// hands' pulls are the footfalls (NativeZombieSteps.Sounds).</summary>
        public bool Quad=>quad;
        /// <summary>Whether foot `foot` is the shambler's dragged one (its toe trails through the swing; its plant scrapes).</summary>
        public bool Dragged(int foot)=>kind==NativeHordeKinds.Shambler&&foot<2&&(foot==0)==(limpSide<0)&&drag>.55f;
        // per-add variety, set from the seed
        private float cadence=1,hunchJitter,tilt,hang,idleRate=1,limpSide=1,drag;private int kind;private bool quad;
        // the add's takes (Core/NativeZombieMotion: -1 for the procedural crawl), this frame's blended stride shape, the rows
        private int walkClip=-1,runClip=-1,idleClip=-1;private float idlePhase,duty0=.6f,duty1=.6f,off1=.5f,lastDt,sinkNow;
        /// <summary>How far (rig metres) this pose's hips were sunk so the short voxel legs reach the stride's feet (0 while they
        /// reach). The model's longer legs need none of it (Core/NativeZombiePosture.Stance).</summary>
        public float Sunk{get;private set;}
        /// <summary>How far (rig metres) this pose's idle, walk and run takes sit their hips under the capture's standing hips, before a
        /// blow, a reel or a roar lays its own crouch on (0 for the procedural crawl). The takes were acted on a person's legs and
        /// bound to the voxel rig's short ones as a share of their length; on the model's longer legs that share reads as a squat
        /// (Core/NativeZombiePosture.Stance stands it up).</summary>
        public float Crouch{get;private set;}
        /// <summary>How much of this pose a blow, a reel, a roar or a keyed move owns (0 to 1): their crouches are the gait's to show
        /// on the model too (Core/NativeZombiePosture.Stance lifts only the rest).</summary>
        public float Busy{get;private set;}
        private readonly float[] rowW=new float[CapturedClip.Channels],rowR=new float[CapturedClip.Channels],rowI=new float[CapturedClip.Channels],rowA=new float[CapturedClip.Channels],row=new float[CapturedClip.Channels];
        private readonly float[] retimeAt=new float[4],retimeMarks=new float[4];
        private readonly Vector3[] mocapAnkle=new Vector3[2],mocapKnee=new Vector3[2],mocapHip=new Vector3[2];private readonly Quaternion[] mocapFoot=new Quaternion[2];
        /// <summary>The take this add walks, runs and stands on (NativeZombieClips indices; -1 off the capture).</summary>
        public int WalkClip=>walkClip;public int RunClip=>runClip;public int IdleClip=>idleClip;
        /// <summary>The capture clip and its time the last frame's blow played (-1: none or procedural), for the checks and effects.</summary>
        public int AttackClip{get;private set;}=-1;public float AttackClipTime{get;private set;}
        /// <summary>Forgets the add (a new arena, a rewind, a jump): the next frame plants its feet where it stands.</summary>
        public void Reset(){time=float.NaN;}

        private static float Sm(float x)=>MeleeMath.Smooth(x);
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Lerp(float a,float b,float t)=>a+(b-a)*t;
        private static float Frac(float x)=>x-(float)Math.Floor(x);
        private static float Wrap(float deg){deg%=360;if(deg>180)deg-=360;if(deg<-180)deg+=360;return deg;}
        private static Vector2 Dir(float yaw){float r=yaw*(float)Math.PI/180;return new Vector2((float)Math.Sin(r),(float)Math.Cos(r));}

        /// <summary>The walk-run blend at ground speed `v` for a kind (0 walk, 1 run).</summary>
        public static float RunBlend(int kind,float v){var d=defs[NativeHordeKinds.Base(kind)];return Clamp01((v-d.WalkSpeed*1.05f)/Math.Max(.1f,d.RunSpeed*.8f-d.WalkSpeed*1.05f));}
        /// <summary>The stride (m per cycle) at speed `v` for a kind's first takes (the step cue's spacing): the capture's cadence
        /// over the speed; the crawler's procedural stride.</summary>
        public static float Stride(int kind,float v)
        {
            kind=NativeHordeKinds.Index(kind);
            if(kind==NativeHordeKinds.Hulk)return HeavyStride(v);
            if(NativeZombieMotion.Captured(kind)){int w=NativeZombieMotion.Walks(kind)[0],r=NativeZombieMotion.Runs(kind)[0];float hz=Lerp(NativeZombieMotion.Cadence(w,v,kind),NativeZombieMotion.Cadence(r,v,kind),RunBlend(kind,v));return Math.Max(.2f,v)/Math.Max(.2f,hz);}
            return ProceduralStride(kind,v);
        }
        private static float ProceduralStride(int kind,float v){var d=defs[NativeHordeKinds.Base(kind)];float r=RunBlend(kind,v);return Lerp(d.WalkStride,d.RunStride,r)*(.75f+.25f*Clamp01(v/Math.Max(.1f,d.WalkSpeed)));}
        public static float Duty(int kind,float v){var d=defs[NativeHordeKinds.Base(kind)];return Lerp(d.Duty,d.RunDuty,RunBlend(kind,v));}

        /// <summary>
        /// Steps the add to `f.Time` and writes its living pose into `p` (source frame, the rig root at the body's position,
        /// rotated by <see cref="Yaw"/>). `react` adds the felt hits (null: none). The rig root's yaw to present is <see cref="Yaw"/>.
        /// </summary>
        public void Pose(in HordeMotionInput f,CapturedRig.Pose p,NativeHordeReact react=null)
        {
            NativeZombieMotion.Warm();Step(f,react);LifeStep(f,react,stepFresh);p.Reset();AttackClip=-1;armLayers=0;Sunk=0;Crouch=0;Busy=0;
            // A blow the authority cancelled (a stagger in the wind-up) stops where it was and lets go over CancelFade: it never
            // strikes, and it never snaps off.
            float age=f.AttackAge,keep=1;
            if(f.Cancelled&&!float.IsNaN(age)){float since=Math.Max(0,f.CancelAge);age-=since;keep=1-BossMath.Smoother(since/CancelFade);if(keep<=0||age<0)age=float.NaN;}
            Channels(age,f.Strike,f.Recover,f.Attack,out float cock,out float swing,out float follow,out float settle,out float attackW);
            if(keep<1){cock*=keep;swing*=keep;follow*=keep;attackW*=keep;}
            // ---- alert ----
            // (A scaled kind's rig speeds are its own over its scale: the Hulk is walking at half the speed a pilot-sized one is.)
            float v=Speed,ms=1/NativeHordeKinds.Scale[kind],moving=Sm((v-.12f*ms)/(.5f*ms));
            // (The upright kinds' stand-to-move blend changes at most MoveSlew a second: an authority stagger stops the body dead and
            // lets it go at full speed in one tick, and the take crossed from standing to the chase in two or three frames there.)
            if(!heavy&&!quad){float u=Clamp01((v-.12f*ms)/(.5f*ms)),most=MoveSlew*lastDt;moveArg=stepFresh?u:moveArg+Math.Max(-most,Math.Min(most,u-moveArg));moving=Sm(moveArg);}
            float alert=0,lookYaw=0,lookPitch=0;
            if(f.HasTarget)
            {
                var to=new Vector2(f.Target.X-f.Root.X,f.Target.Z-f.Root.Z);float dist=to.Length();
                // (The rig's yaw turns toward its left and its pitch bends down: the angle to a pilot on the right or above is negated.)
                // (A pilot passing behind it fades out of the look rather than flipping the head from one shoulder to the other.)
                if(dist>.05f){to/=dist;float ang=(float)Math.Atan2(Vector2.Dot(to,Right),Vector2.Dot(to,Forward));lookYaw=-Math.Max(-1.1f,Math.Min(1.1f,ang))*(1-BossMath.Smoother((Math.Abs(ang)-1.3f)/.9f));lookPitch=-(float)Math.Atan2(f.Target.Y+.5f-(f.Root.Y+.6f),dist);}
                alert=Clamp01((14-dist)/6)*(1-moving*.6f);
            }
            RoamPrepare(f,moving,lookYaw,lookPitch);
            if(quad){CrawlerPose(f,p,defs[NativeHordeKinds.Crawler],v,RunBlend(NativeHordeKinds.Crawler,v),moving,alert,lookYaw,cock,swing,follow,settle,attackW,react,age);return;}
            float run=heavy?RunBlend(kind,v):runNow,hurt=1-Clamp01(hurtShown*1.6f),t=f.Time,sprint=heavy?Sprint(v):0;var d=defs[NativeHordeKinds.Base(kind)];
            // ---- the capture: the walk and run takes at the stride's phase, the idle (the door's wake while it waits) ----
            // (A take played faster than its capture swings its trunk and arms less about its cycle's mean: NativeHordeLoco.Calm.)
            float cv=Math.Max(gaitSpeed,.8f*Speed);
            SampleCalm(walkClip,Phase,rowW,RateOf(walkClip,cv));RoamWalkRow();SampleCalm(runClip,Phase,rowR,RateOf(runClip,cv));NativeZombieClips.Blend(rowW,rowR,run,rowA);
            float wake=f.Entering&&f.EntryAge<f.EntryWalk?f.EntryAge:float.NaN;
            if(!float.IsNaN(wake))NativeZombieClips.Sample(NativeZombieMotion.Wake,Math.Min(wake,NativeZombieClips.Duration(NativeZombieMotion.Wake)),rowI);
            else NativeZombieClips.Sample(idleClip,t*idleRate+idlePhase,rowI);
            // (The Hulk keeps only a little of a zombie take walking: its heavy stride is its own, Core/NativeHordeHeavy.)
            NativeZombieClips.Blend(rowI,rowA,heavy?moving*HeavyTakeShare:moving,row);Crouch=Math.Max(0,-row[2]);
            // The capture's pelvis swings a long way about the travel (a person's whole turn per stride): kept to two thirds, so
            // the body reads along its path and the planted feet stay under it.
            row[0]*=.62f;
            if(heavy)HeavyArms(row,moving,run,sprint,t);
            // ---- the blow: the pack's take retimed onto the authority's strike, over the trunk and arms ----
            bool attacking=!float.IsNaN(age)&&age>=0&&age<f.Recover+.25f;
            // The heavy kinds' keyed moves (Core/NativeHordeHeavy): a punch, a swipe, a knock or a charge replaces the take and the
            // procedural attack; the smash keeps the pack's hammer under its keys.
            ResolveMove(f,attacking);if(Replaces){attacking=false;cock=swing=follow=settle=attackW=0;}
            int ac=attacking?NativeZombieMotion.Attack(f.Attack,f.Side,seed):-1;
            // A blow that starts while the last one is still letting go (an add in reach swings again the moment it recovers)
            // takes over from that one's tail, which fades as it always would, instead of snapping back to the gait first.
            // (A blow is known by its replicated start; a cancelled one is never handed on: it has let go already.)
            float start=f.Time-f.AttackAge;tailHeld=0;
            if(ac>=0&&curClip>=0&&!curCancelled&&Math.Abs(start-curStart)>1e-4f){tailClip=curClip;tailStart=curStart;tailStrike=curStrike;tailRecover=curRecover;tailAttack=curAttack;}
            if(ac>=0){curClip=ac;curStart=start;curStrike=f.Strike;curRecover=f.Recover;curAttack=f.Attack;curCancelled=f.Cancelled;}else if(!attacking)curClip=-1;
            if(tailClip>=0)
            {
                float ta=f.Time-tailStart;Channels(ta,tailStrike,tailRecover,tailAttack,out _,out _,out _,out _,out float tw);
                if(tw<=0||ta<0)tailClip=-1;else{AttackTake(tailClip,ta,tailStrike,tailRecover,tailAttack,rowA);Upper(row,rowA,tw,.7f,.6f);tailHeld=tw;}
            }
            if(ac>=0)
            {
                float ct=AttackTake(ac,age,f.Strike,f.Recover,f.Attack,rowA);AttackClip=ac;AttackClipTime=ct;
                Upper(row,rowA,attackW*BossMath.Smoother(age/Math.Max(.04f,Math.Min(.12f,f.Strike*.3f))),.7f,.6f);
            }
            // ---- the spitter's wind-up rears it back on the start of a knock-back reel (arms flung, chest up), held to the spit ----
            if(ac<0&&attacking&&f.Attack==NativeHordeKinds.Attack.Spit)
            {
                float hold=swing>0?1-swing:cock,k=hold*(1-settle);
                if(k>0){NativeZombieClips.Sample(NativeZombieMotion.SpitRear,.06f+.26f*cock,rowA);Upper(row,rowA,.8f*k,0,.35f);}
            }
            // ---- the Hulk's roar: arms flung wide on another reel's start, chest out (Core/NativeHordeRoar) ----
            float roarW=f.Roaring&&!Replaces?NativeHordeRoar.Weight(f.RoarAge):0;
            if(roarW>0){NativeZombieClips.Sample(NativeZombieMotion.Roar,.12f+.26f*Sm(f.RoarAge/.55f),rowA);Upper(row,rowA,.9f*roarW,0,.4f);}
            // ---- a town zombie's scream on its alert (Core/NativeHordeRoamMotion) ----
            RoamUpper(row,moving);
            // ---- the authority's stagger: a captured knockback reel over the trunk and arms ----
            // (One continuous stumble through a string of staggers, the take at its capture's own pace: NativeHordeReact.Stumble.)
            float knockAt=0,reel=react!=null?react.Stumble(t,out knockAt):0;
            if(reel>0){int kc=NativeZombieMotion.Knock(seed^(uint)(id*31));NativeZombieClips.Sample(kc,knockAt,rowA);Upper(row,rowA,reel*.85f,.5f,.5f);}
            ArmReach(row,.44f);NativeZombieClips.Pose(row,p,true,ShoulderInset(kind,f.Look));ApplyArmLayers(p);
            // The capture's legs (before the feet are planted): their swing lift, turn and knee direction.
            p.World();
            for(int i=0;i<2;i++){int hip=i==0?CapturedRig.LHip:CapturedRig.RHip;mocapHip[i]=p.WorldPosition[hip];mocapKnee[i]=p.WorldPosition[hip+1];mocapAnkle[i]=p.WorldPosition[hip+2];mocapFoot[i]=p.WorldRotation[hip+2];}
            // ---- the body over the capture: alert and hurt sag it; the procedural attacks' trunk ----
            var b=p.Position[CapturedRig.Body];b.Y-=alert*.04f+hurt*.05f;RoamBody(ref b,f,moving);
            AttackBody(f,cock,swing,follow,settle,out float aPitch,out float aYaw,out float aRoll,out float aCrouch,out float aHead);
            if(ac>=0){aPitch=aYaw=aRoll=aCrouch=aHead=0;}
            if(roarW>0){float tremble=(float)Math.Sin(f.RoarAge*31)*.035f*roarW;aPitch+=-.32f*roarW;aHead+=(-.55f+tremble)*roarW*(1-Sm((f.RoarAge-.9f)/.6f))+.15f*roarW*Sm((f.RoarAge-.9f)/.6f);aCrouch+=.03f*roarW;}
            b.Y-=aCrouch;p.Position[CapturedRig.Body]=b;
            if(aPitch!=0||aYaw!=0||aRoll!=0){Bend(p,CapturedRig.Spine,aPitch*.5f,aYaw*.35f,aRoll*.5f);Bend(p,CapturedRig.Chest,aPitch*.5f,aYaw*.35f,aRoll*.5f);}
            // ---- the Hulk's heavy body over its stride, then a keyed move's body and trunk ----
            if(heavy)HeavyTrunk(p,f,moving,run,sprint);
            MoveTrunk(p,f);
            RoamTrunk(p,f,moving);
            IronworksTrunk(f,p);
            LifeTrunk(p,f,moving);
            // ---- head: tracks its pilot, the Siren's crooked tilt and twitches ----
            // (A blow handing over to the next keeps its weight through the hand-over: the head's tracking never jumps.)
            float held=Math.Max(attackW,tailHeld);Busy=Math.Min(1,Math.Max(Math.Max(held,reel),Math.Max(roarW,moveW)));
            float twitch=heavy?.35f*Twitch(t*.75f):Twitch(t);float headPitch=lookPitch*.6f*alert+aHead,headYaw=lookYaw*(.55f+.35f*alert)*(1-held*.5f)+twitch*.5f,headRoll=tilt+twitch*.8f;
            RoamHead(f,moving,ref headPitch,ref headYaw,ref headRoll);
            Bend(p,CapturedRig.Neck,headPitch*.45f,headYaw*.45f,headRoll*.4f);Bend(p,CapturedRig.Head,headPitch*.55f,headYaw*.55f,headRoll*.6f);LifeHead(p,f,alert);RoamAim(p);
            // ---- reactions over the trunk (hips shove, recoil, head snap) ----
            react?.Trunk(p,f.Time,Forward,Right);
            {var before=p.Position[CapturedRig.Body];Balance(p,kind);if(!quad)HeavyBalanced(p.Position[CapturedRig.Body]-before);}
            // ---- legs: feet to their planted (or swinging) targets; a stride the legs cannot reach sinks the hips ----
            p.World();
            for(int i=0;i<2;i++)
            {
                // (A swinging foot lifts on the takes' own swing arcs, NativeZombieMotion.SwingLift, carried through the gait's swing.)
                if(feet[i].Swing&&moving>0&&!heavy)
                {
                    float s=Clamp01((f.Time-feet[i].T0)/Math.Max(1e-3f,feet[i].T1-feet[i].T0)),lift=Lerp(WalkSwing(i,s),NativeZombieMotion.SwingLift(runClip,i,s),run);
                    feet[i].Y=Lerp(feet[i].Y,Math.Min(.4f,lift+.01f),moving);
                }
            }
            float sink=0,hard=0;
            for(int i=0;i<2;i++)
            {
                // A planted foot holds the hips down; a swinging one starts to as it comes down over its spot (by its landing it holds
                // them fully), so the hips settle into a long stride's landing instead of dropping on its frame.
                float hold=1;if(feet[i].Swing){if(heavy)continue;float s=Clamp01((f.Time-feet[i].T0)/Math.Max(1e-3f,feet[i].T1-feet[i].T0));hold=BossMath.Smoother((s-.35f)/.65f);if(hold<=0)continue;}
                Vector3 local=ToLocal(FootWorld[i],f.Root);local.Y=f.Ground-f.Root.Y+AnkleHeight+(feet[i].Swing?0:feet[i].Y);var hipAt=p.WorldPosition[i==0?CapturedRig.LHip:CapturedRig.RHip];
                float dx=local.X-hipAt.X,dz=local.Z-hipAt.Z,h2=dx*dx+dz*dz,dy=hipAt.Y-local.Y,R=.485f;float need=(h2>=R*R?dy-.05f:dy-(float)Math.Sqrt(R*R-h2))*hold;if(need>sink)sink=need;
                // (A foot that stood planted before this frame must stay in reach now: the take's own hips can rise or lunge off it.)
                if(!feet[i].Swing&&(Landed&(1<<i))==0){float must=h2>=LegReach*LegReach?dy-.05f:dy-(float)Math.Sqrt(LegReach*LegReach-h2);if(must>hard)hard=must;}
            }
            // (The Hulk's long stride sits its hips lower: its knees bend deep under the weight.)
            sink=Math.Min(heavy?.26f:.17f,Math.Max(0,sink));
            // (The upright kinds' hips ease down into a stride on a spring, quicker than they come back up, rather than dropping on
            // the frame a foot comes into reach: a landing beyond reach is planted where the leg reached, so nothing slides
            // while they settle. The Hulk's stamp drops its hips with the foot.)
            if(heavy)sinkNow=sink>sinkNow?sink:sinkNow+(sink-sinkNow)*(1-(float)Math.Exp(-lastDt/.12f));
            else
            {
                hard=Math.Min(.17f,hard);float eased=Math.Max(0,sinkEase.Step(sink,sink>sinkEase.Value?SinkDown:SinkUp,lastDt));
                if(eased<hard){sinkEase.Value=hard;if(sinkEase.Velocity<0)sinkEase.Velocity=0;eased=hard;}sinkNow=eased;
            }
            // (The hips never drop below a deep crouch: the thighs would fold the knees into the floor.)
            {var bb=p.Position[CapturedRig.Body];float y0=bb.Y;bb.Y=Math.Max(bb.Y-sinkNow,Math.Min(bb.Y,heavy?-.26f:-.18f));Sunk=y0-bb.Y;p.Position[CapturedRig.Body]=bb;p.World();}
            for(int i=0;i<2;i++)
            {
                bool left=i==0;int hip=left?CapturedRig.LHip:CapturedRig.RHip,foot=hip+2;
                Vector3 local=ToLocal(FootWorld[i],f.Root);local.Y=f.Ground-f.Root.Y+AnkleHeight+feet[i].Y;
                // The knee goes where the capture's knee goes (zombie knock knees and splay), never backward, and never more than
                // KneeSplay aside about the leg's own line (a guard: on the straight walks and runs measured the knees stay within
                // about 20 degrees anyway; the capture's knee off its own leg's line could otherwise swing a leg out sideways).
                var kneeDir=mocapKnee[i]-(mocapHip[i]+mocapAnkle[i])*.5f;kneeDir.Z=Math.Max(kneeDir.Z,.02f);var pole=heavy?HeavyPole(left):CapturedRig.Unit(kneeDir+new Vector3(0,0,.04f),Vector3.UnitZ);
                float heel=Reached(p,f,i,hip,ref local);
                if(!heavy)pole=Splayed(pole,p.WorldPosition[hip],local);
                LegKept(p,left,local,pole,i);
                // A swinging leg folded so far its knee would scrape the floor lifts its foot instead (eased in off the lift and out
                // into the landing: the knee stood as low planted, and the foot jumped 10 cm in a frame as it lifted).
                if(feet[i].Swing)
                {
                    p.World();float knee=p.WorldPosition[hip+1].Y-(f.Ground-f.Root.Y),clear=heavy?.27f:.19f;
                    float s=Clamp01((f.Time-feet[i].T0)/Math.Max(1e-3f,feet[i].T1-feet[i].T0)),ease=Sm(s/ClearEase)*Sm((1-s)/ClearEase);
                    if(knee<clear&&ease>0){local.Y+=(clear-knee)*ease;LegKept(p,left,local,pole,i);}
                }
                // A planted foot keeps the way it landed while the body turns over it (the foot locks in turn as well as place); a
                // swinging one turns to the body's facing by its landing.
                if(f.Tier<NativeHordeModels.Impostor){float tw=FootTwist(i,f.Time);if(tw!=0)p.Rotation[foot]=p.Rotation[foot]*CapturedRig.Rot(Vector3.UnitY,tw);}
                // (A peeled heel pitches the foot over its ball: the toe stays flat where it stood.)
                if(heel>0)p.Rotation[foot]=p.Rotation[foot]*CapturedRig.Rot(Vector3.UnitX,heel);
                // A swinging foot takes the capture's turn (the dragged toe, the sprint's toe-off); a planted one lies flat.
                if(feet[i].Swing&&heavy){float s=Clamp01((f.Time-feet[i].T0)/Math.Max(1e-3f,feet[i].T1-feet[i].T0));p.Rotation[foot]=p.Rotation[foot]*CapturedRig.Euler(HeavyToe(s)*Clamp01(feet[i].Y/.1f));}
                else if(feet[i].Swing)
                {
                    float s=Clamp01((f.Time-feet[i].T0)/Math.Max(1e-3f,feet[i].T1-feet[i].T0)),k=Sm(s/.25f)*(1-Sm((s-.75f)/.25f))*moving*Clamp01(feet[i].Y/.14f);
                    if(k>0){var parent=p.WorldRotation[CapturedRig.Pelvis]*p.Rotation[hip]*p.Rotation[hip+1];p.Rotation[foot]=Quaternion.Slerp(p.Rotation[foot],Quaternion.Inverse(parent)*mocapFoot[i],k*.8f);}
                }
                float toe=feet[i].Swing?(-.35f+(kind==NativeHordeKinds.Shambler&&(i==0)==(limpSide<0)?.5f*drag:0))*Sm(Clamp01((f.Time-feet[i].T0)/Math.Max(1e-3f,feet[i].T1-feet[i].T0))/.2f):0;
                p.Rotation[i==0?CapturedRig.LToe:CapturedRig.RToe]=CapturedRig.Euler(-toe*.4f-heel);
            }
            // ---- arms: the capture's reach; the procedural attacks layer their own ----
            if(ac<0)AttackArms(f,p,cock,swing,follow,settle,attackW);
            MoveHands(p);
            react?.Arms(p,f.Time);
            IronworksArms(f,p);
            LifeArms(p,f,held);
            // (A keyed blow may carry the fist a little past the usual reach: a punch at full extension.)
            KeepHands(p,kind,Replaces?.18f:0);
            p.World();
        }
        // The blow's take retimed onto the authority's strike (its wind-up held into the telegraph, the impact exactly at the strike).
        private int curClip=-1,tailClip=-1;private float tailHeld;private bool curCancelled;private float curStart,curStrike,curRecover,tailStart,tailStrike,tailRecover;private NativeHordeKinds.Attack curAttack,tailAttack;
        private float AttackTake(int clip,float age,float strike,float recover,NativeHordeKinds.Attack attack,float[] into)
        {
            var c=NativeZombieClips.Clip(clip);float lead=Math.Min(SwingLead(attack),strike*.45f);
            retimeAt[0]=0;retimeAt[1]=strike-lead;retimeAt[2]=strike;retimeAt[3]=Math.Max(recover,strike+.05f);
            retimeMarks[0]=c.Marks[0];retimeMarks[1]=c.Marks[1];retimeMarks[2]=c.Marks[2];retimeMarks[3]=c.Marks[3];
            float ct=CapturedClip.Retime(retimeAt,retimeMarks,age);NativeZombieClips.Sample(clip,ct,into);return ct;
        }
        /// <summary>A hand the capture flings wider than a pilot's reach (round the hit volume) is drawn back to it, its arm
        /// re-reached with the same wrist and elbow direction, so the claws stay where shots at them land.</summary>
        private static void KeepHands(CapturedRig.Pose p,int kind,float extra=0)
        {
            p.World();float most=NativeHordeKinds.Radius[NativeHordeKinds.Index(kind)]+.26f+extra;var chest=p.WorldRotation[CapturedRig.Chest];var inv=Quaternion.Inverse(chest);var cpos=p.WorldPosition[CapturedRig.Chest];
            for(int i=0;i<2;i++)
            {
                bool left=i==0;int h=left?CapturedRig.LHand:CapturedRig.RHand;var w=p.WorldPosition[h];float hor=(float)Math.Sqrt(w.X*w.X+w.Z*w.Z);if(hor<=most)continue;
                var t=new Vector3(w.X*most/hor,w.Y,w.Z*most/hor);var elbow=p.WorldPosition[h-1];var shoulder=p.WorldPosition[h-2];
                CapturedRig.Arm(p,left,Vector3.Transform(t-cpos,inv),inv*p.WorldRotation[h],Vector3.Transform(elbow-(shoulder+w)*.5f,inv));p.World();
            }
        }
        // The capture's reach (a person's long arm stretched straight) kept a little bent, so the claws stay within reach of the
        // hit volume as a pilot's do: each hand's chest-space target no farther than `most` from its shoulder (source frame: the
        // right hand's shoulder at -x).
        private static void ArmReach(float[] row,float most)
        {
            for(int h=0;h<2;h++)
            {
                int c=h==0?39:51;float sx=h==0?-.345f:.345f;float dx=row[c]-sx,dy=row[c+1]-.18f,dz=row[c+2],l=(float)Math.Sqrt(dx*dx+dy*dy+dz*dz);
                if(l>most){float k=most/l;row[c]=sx+dx*k;row[c+1]=.18f+dy*k;row[c+2]=dz*k;}
                // ... and never flung straight out to the side (a hand at most .27 outside its shoulder).
                if(h==0&&row[c]<sx-.27f)row[c]=sx-.27f;else if(h==1&&row[c]>sx+.27f)row[c]=sx+.27f;
            }
        }
        // Blends a take's trunk and arms into `into` by `k`: pelvis to head and clavicles, both arms' reach, the heading by `yaw`
        // and the hips' height by `hips` (the legs stay the gait's).
        private void Upper(float[] into,float[] take,float k,float yaw,float hips)
        {
            if(k<=0)return;if(k>1)k=1;
            into[0]+=(take[0]-into[0])*k*yaw;into[2]+=(take[2]-into[2])*k*hips;
            for(int c=4;c<25;c++)into[c]+=(take[c]-into[c])*k;
            // The arms are a layer of their own (ApplyArmLayers): a straight blend of two takes' reach targets, wrists and elbow
            // poles can fold the pole onto the reach or pass a wrist through nothing, and the arm then flips half a turn mid-blend.
            if(armLayers<armWeight.Length){Array.Copy(take,armLayer[armLayers],CapturedClip.Channels);armWeight[armLayers++]=k;}
        }
        // The takes' arms over the gait's, in the order they were laid on: each layer's arms are solved on their own (its reach,
        // wrist and elbow pole agree), and the arm's joints turn toward that by the layer's weight.
        private readonly float[][] armLayer={new float[CapturedClip.Channels],new float[CapturedClip.Channels],new float[CapturedClip.Channels],new float[CapturedClip.Channels],new float[CapturedClip.Channels],new float[CapturedClip.Channels]};
        private readonly float[] armWeight=new float[6];private readonly Quaternion[] armSave=new Quaternion[6];private int armLayers;
        private static readonly int[] ArmJoints={CapturedRig.LArm,CapturedRig.LElbow,CapturedRig.LHand,CapturedRig.RArm,CapturedRig.RElbow,CapturedRig.RHand};
        private void ApplyArmLayers(CapturedRig.Pose p)
        {
            for(int l=0;l<armLayers;l++)
            {
                var f=armLayer[l];float w=armWeight[l];ArmReach(f,.44f);
                for(int j=0;j<ArmJoints.Length;j++)armSave[j]=p.Rotation[ArmJoints[j]];
                var blade=CapturedRig.Unit(new Vector3(f[42],f[43],f[44]),Vector3.UnitZ);var edge=new Vector3(f[45],f[46],f[47]);edge=CapturedRig.Unit(edge-blade*Vector3.Dot(blade,edge),Vector3.UnitX);
                CapturedRig.Arm(p,false,new Vector3(f[39],f[40],f[41]),CapturedRig.Basis(edge,Vector3.Cross(blade,edge),blade),new Vector3(f[48],f[49],f[50]));
                CapturedRig.Arm(p,true,new Vector3(f[51],f[52],f[53]),CapturedRig.Look(new Vector3(f[54],f[55],f[56]),new Vector3(f[57],f[58],f[59])),new Vector3(f[60],f[61],f[62]));
                if(w<1)for(int j=0;j<ArmJoints.Length;j++){int b=ArmJoints[j];p.Rotation[b]=BossMath.Slerp(armSave[j],p.Rotation[b],w);}
            }
            armLayers=0;
        }
        private static void Bend(CapturedRig.Pose p,int bone,float x,float y,float z){if(x==0&&y==0&&z==0)return;p.Rotation[bone]=CapturedRig.Euler(x,y,z)*p.Rotation[bone];}

        /// <summary>The farthest the face and the chest may reach ahead of the root (m): the hit capsule's radius, less a margin.</summary>
        public static float Reach(int kind)=>NativeHordeKinds.Radius[NativeHordeKinds.Index(kind)]-.06f;
        /// <summary>
        /// Keeps the trunk over the hit volume: a hunched or lunging add sits its hips back (its planted feet stay; the legs angle
        /// under it) until its face and chest are no farther ahead of the root than <see cref="Reach"/>, as a crouched creature
        /// balances, so what the camera shows is where the authority's capsule is (shots at the head land on the head).
        /// </summary>
        private static void Balance(CapturedRig.Pose p,int kind)
        {
            p.World();var head=p.WorldPosition[CapturedRig.Head]+Vector3.Transform(new Vector3(0,.24f,.18f),p.WorldRotation[CapturedRig.Head]);
            var crown=p.WorldPosition[CapturedRig.Head]+Vector3.Transform(new Vector3(0,.2f,0),p.WorldRotation[CapturedRig.Head]);
            var chest=p.WorldPosition[CapturedRig.Chest]+Vector3.Transform(new Vector3(0,.4f,.19f),p.WorldRotation[CapturedRig.Chest]);
            // ... the same for a capture that leans back or to a side: each of the face, crown and chest past the reach is drawn back
            // radially toward the capsule's axis (a hunched add sits back, a swaying one steps its hips under it). The corrections
            // add (each fading to nothing as its point comes inside), so the hips never jump when another point becomes the farthest.
            float reach=Reach(kind);
            if(kind==NativeHordeKinds.Hulk)
            {
                // (The Hulk keeps its heavy stance's balance as it was tuned, its legs already striding as far as they may: drawn back
                // along its farthest point.)
                Vector2 worst=Vector2.Zero;float most=0;Pick(head,ref worst,ref most);Pick(crown,ref worst,ref most);Pick(chest,ref worst,ref most);
                float over=most-reach;if(over<=0)return;var wd=worst/most;float sh=Math.Min(over,.32f);var hb=p.Position[CapturedRig.Body];hb.X-=wd.X*sh;hb.Z-=wd.Y*sh;p.Position[CapturedRig.Body]=hb;return;
            }
            var corr=Vector2.Zero;
            Over(head,reach,ref corr);Over(crown,reach,ref corr);Over(chest,reach,ref corr);
            float l=corr.Length();if(l<=1e-6f)return;var dir=corr/l;
            // The shift along that direction that brings every point inside (each point's over-reach over how squarely the shift
            // meets it): every term is continuous, so their largest is too.
            float shift=0;Need(head,reach,dir,ref shift);Need(crown,reach,dir,ref shift);Need(chest,reach,dir,ref shift);
            shift=Math.Min(shift,kind==NativeHordeKinds.Runner?.26f:.32f);
            var b=p.Position[CapturedRig.Body];b.X-=dir.X*shift;b.Z-=dir.Y*shift;p.Position[CapturedRig.Body]=b;
        }
        private static void Need(Vector3 v,float reach,Vector2 dir,ref float shift){float d=(float)Math.Sqrt(v.X*v.X+v.Z*v.Z),o=d-reach;if(o<=0||d<1e-5f)return;float meet=(v.X*dir.X+v.Z*dir.Y)/d;shift=Math.Max(shift,o/Math.Max(.35f,meet));}
        private static void Pick(Vector3 v,ref Vector2 worst,ref float most){float d=(float)Math.Sqrt(v.X*v.X+v.Z*v.Z);if(d>most){most=d;worst=new Vector2(v.X,v.Z);}}
        private static void Over(Vector3 v,float reach,ref Vector2 corr){float d=(float)Math.Sqrt(v.X*v.X+v.Z*v.Z),o=d-reach;if(o<=0||d<1e-5f)return;corr+=new Vector2(v.X,v.Z)*(o/d);}

        // ================================================================================================================
        // Stepping: the clock, the yaw, the speed and the feet
        // ================================================================================================================
        private void Step(in HordeMotionInput f,NativeHordeReact react)
        {
            kind=NativeHordeKinds.Index(f.Kind);quad=kind==NativeHordeKinds.Crawler||f.Crawl||!NativeZombieMotion.Captured(kind);
            int gk=quad?NativeHordeKinds.Crawler:kind;var d=defs[NativeHordeKinds.Base(gk)];heavy=kind==NativeHordeKinds.Hulk&&!quad;
            float dt=float.IsNaN(time)?0:f.Time-time;bool fresh=float.IsNaN(time)||id!=f.Id||dt<-.001f||dt>ResetGap;
            if(fresh)
            {
                id=f.Id;seed=f.Seed;Yaw=f.Yaw;root=new Vector2(f.Root.X,f.Root.Z);velocity=Vector2.Zero;Speed=0;dt=0;sinkNow=0;sinkEase.Reset(0);
                float H(int k)=>NativeHordeKinds.Hash(seed,40+k);
                Phase=H(0);cadence=.94f+.12f*H(1);hunchJitter=(H(2)-.5f)*.12f;tilt=(H(3)-.5f)*.36f;hang=H(4)*.3f;idleRate=.8f+.4f*H(5);limpSide=H(6)<.5f?-1:1;drag=kind==NativeHordeKinds.Shambler?.4f+.6f*H(7):0;
                if(NativeZombieMotion.Captured(kind)){walkClip=NativeZombieMotion.Walk(kind,seed);runClip=NativeZombieMotion.Run(kind,seed);idleClip=NativeZombieMotion.Idle(kind,seed);idlePhase=H(8)*NativeZombieClips.Duration(idleClip);}
                else walkClip=runClip=idleClip=-1;
                // (The Hulk's idle sways slower: a big body's time runs about the square root of its size slower.)
                HeavyReset();if(heavy)idleRate*=.75f;RoamReset(seed);curClip=tailClip=-1;for(int l=0;l<4;l++)legBendKnown[l]=false;
                Forward=Dir(Yaw);Right=new Vector2(Forward.Y,-Forward.X);
                for(int i=0;i<4;i++){feet[i]=default;feet[i].W=Home(i,new Vector2(f.Root.X,f.Root.Z),d);feet[i].Turn=Yaw;FootWorld[i]=new Vector3(feet[i].W.X,f.Ground,feet[i].W.Y);FootAir[i]=false;if(heavy)feet[i].Cycle=-1;if(i<2){overReach[i]=false;heelLift[i]=0;}}
            }
            time=f.Time;dt=Math.Max(0,Math.Min(MaxDt,dt));lastDt=dt;
            // (The health the sag reads, eased: Core/NativeHordeLife.)
            if(fresh)hurtEase.Reset(Clamp01(f.Health));hurtShown=Clamp01(hurtEase.Step(Clamp01(f.Health),HurtHalfLife,dt));roamTake=RoamTakeOf(f);stepFresh=fresh;
            // The facing turns quickly toward the authority's yaw (which snaps), faster while winding up an attack, so the strike faces
            // where the authority aims it.
            bool winding=!float.IsNaN(f.AttackAge)&&f.AttackAge>=0&&f.AttackAge<f.Strike;
            // (A presented root (Core/NativeHordeFlow) has turned already, on the limited turn: its facing is taken as it is.)
            if(!heavy&&f.Flow)Yaw=Wrap(f.Yaw);
            else if(!heavy){float rate=(winding?16:9)*dt*57.3f*(kind==NativeHordeKinds.Brute?.7f:kind==NativeHordeKinds.Hulk?.45f:1),diff=Wrap(f.Yaw-Yaw);Yaw=Wrap(Yaw+Math.Max(-rate,Math.Min(rate,diff)));if(fresh)Yaw=f.Yaw;}
            Forward=Dir(Yaw);Right=new Vector2(Forward.Y,-Forward.X);
            // Ground speed from how the body really moved (what the camera sees), or the treadmill's velocity.
            var now=new Vector2(f.Root.X,f.Root.Z);
            if(f.Flow)
            {
                // The presented root's own velocity: smooth on every peer, never differentiated from a stepped or frozen root.
                // (The speed the cycle and the take blends read is eased on a spring as well: a dead stop from a run, which the
                // root makes in a tenth of a second, turns the take's arms and trunk from the run to the stand over a quarter of
                // one instead of flinging them across in a few frames.)
                var fv=new Vector2(f.Velocity.X,f.Velocity.Z);if(!(fv.Length()<12))fv=Vector2.Zero;velocity=fv;root=now;
                if(fresh)speedEase.Reset(fv.Length());Speed=Math.Max(0,speedEase.Step(fv.Length(),SpeedHalfLife,dt));
            }
            else
            {
                Vector2 vel=f.Treadmill?new Vector2(f.Velocity.X,f.Velocity.Z):dt>1e-4f?(now-root)/dt:velocity;
                if(vel.Length()>12)vel=velocity;float k=1-(float)Math.Exp(-dt/.1f);velocity+=(vel-velocity)*k;root=now;
                Speed+=(velocity.Length()-Speed)*k;
            }
            // The Hulk's facing: the heavy turn rate walking, pivot steps standing (Core/NativeHordeHeavy).
            if(heavy){float was=Yaw;HeavyYaw(f,dt,winding||f.Knocking||f.Charging&&f.ChargeAge<NativeHulkMoves.ChargeWind,fresh);Forward=Dir(Yaw);Right=new Vector2(Forward.Y,-Forward.X);HeavyTurning(Wrap(Yaw-was),dt,fresh);}
            if(react!=null)react.Step(f.Time);
            // The hit-stop holds the gait for a moment on a melee blow (the strike's own clock never waits).
            float held=react!=null?react.Hold(f.Time):0;float gdt=dt*(1-held);
            // ---- the cycle: the body's cadence at this speed, the takes' feet's strikes and lifts ----
            // (Core/NativeHordeLoco: the cadence and the take blend read a gait speed eased off the root's; it sets off and stands
            // on two thresholds, so a jostled add does not flicker between stepping and standing.)
            GaitSpeedStep(fresh,dt);bool moving=heavy||quad?Speed>.2f:movingNow;float rateHz,stride;
            // The Hulk walks its own heavy stride (long, slow, both feet down long; flying between a charge's quick steps).
            if(heavy){stride=HeavyStride(Speed);rateHz=moving?Math.Max(.2f,Speed)/stride*cadence:0;duty0=duty1=HeavyDuty(Speed);off1=.5f;}
            else if(!quad)
            {
                // (The cadence follows the eased speed, but never lags far behind a burst of speed: a body that sets off takes
                // quick steps, not long ones.)
                float run=runNow,cv=Math.Max(gaitSpeed,.8f*Speed);rateHz=Lerp(WalkCadence(cv),NativeZombieMotion.Cadence(runClip,cv,kind),run)*cadence;
                // A run's stance is short (a walking take run fast keeps its feet down no more than half the cycle), and no stance
                // carries the hips past the legs' reach (NativeHordeLoco.StanceMost).
                duty0=Lerp(WalkDuty(0),Math.Min(.55f,NativeZombieMotion.Duty(runClip,0)),run);duty1=Lerp(WalkDuty(1),Math.Min(.55f,NativeZombieMotion.Duty(runClip,1)),run);
                duty0=Reachable(duty0,rateHz,Speed);duty1=Reachable(duty1,rateHz,Speed);
                // (The right foot strikes half a cycle after the left on every take: the takes are warped to it, NativeZombieMotion.Warp.)
                off1=NativeZombieMotion.Offset(runClip,1);stride=Math.Max(.2f,Speed)/Math.Max(.2f,rateHz);
                if(!moving)rateHz=0;
            }
            else{stride=ProceduralStride(gk,Speed);duty0=duty1=Duty(gk,Speed);off1=.5f;rateHz=moving?Speed/Math.Max(.2f,stride)*cadence:0;}
            // A heavy body setting off from a stand starts its cycle on the left foot's mid-stance (both feet are side by side there,
            // the right swinging first), so the long strides start in step instead of catching up over several.
            if(heavy){if(moving&&!wasMoving)Phase=(float)Math.Floor(Phase)+1+StartPhase;wasMoving=moving;}
            // The others set off on the cycle where it stands: each foot lifts when its own phase passes its stance (a foot already
            // past it goes first), instead of the second foot waiting out a whole cycle planted while the hips leave it behind.
            else{if(moving&&!setOff)for(int i=0;i<(quad?4:2);i++)if(!feet[i].Swing)feet[i].Cycle=int.MinValue;setOff=moving;}
            Phase+=rateHz*gdt;
            Landed=0;
            // A shove moves the hips off the feet: standing feet step back under them (the stagger-back steps).
            Vector2 shove=react!=null?react.Shove(f.Time):Vector2.Zero;bool staggering=react!=null&&react.Staggering(f.Time);
            int feetCount=quad?4:2;
            for(int i=0;i<feetCount;i++)
            {
                ref var ft=ref feet[i];
                float off=quad?FootOffset(i):i==1?off1:0,duty=quad?duty0:i==1?duty1:duty0;float u=Frac(Phase+off);int cycle=(int)Math.Floor(Phase+off);
                Vector2 home=Home(i,now+shove*(staggering?1.15f:.8f)+(!quad?HipsUnder():Vector2.Zero),d),ahead=Forward*(duty*stride*.5f*(heavy?TurnReach(i,d.Width):1));
                // A charge's wind-up scrapes the right foot back once (it then stands braced there until the run).
                bool braced=heavy&&f.Charging&&f.ChargeAge<NativeHulkMoves.ChargeWind&&!f.ChargeEnded;
                if(braced&&i==1&&!ft.Swing&&f.ChargeAge>=.24f&&f.ChargeAge<.5f&&!(Math.Abs(scrapedFor-(f.Time-f.ChargeAge))<.05f))
                {scrapedFor=f.Time-f.ChargeAge;ft.Swing=true;ft.Scrape=true;ft.From=ft.W;ft.To=ft.W-Forward*.2f;ft.T0=f.Time;ft.T1=f.Time+.3f;ft.Lift=.05f;}
                if(!ft.Swing&&!(braced&&!moving))
                {
                    // Moving: lift off once the cycle passes the duty (the foot is behind); standing: step only if left behind.
                    bool lift=false;float swingTime=0;
                    if(moving&&u>=duty&&ft.Cycle!=cycle){lift=true;swingTime=(1-u)/Math.Max(.3f,rateHz)+.02f;}
                    else if(!moving&&(Vector2.Distance(ft.W,home)>(staggering?.05f:heavy?.08f:.11f)||!quad&&!heavy&&Math.Abs(Wrap(ft.Turn-Yaw))>TwistStep)&&!OtherSwinging(i,feetCount)){lift=true;swingTime=(heavy?.34f:.2f)+.06f*NativeHordeKinds.Hash(seed,60+i);}
                    // A planted foot the hips (or a shove) have left out of reach steps at once.
                    // (Moving, it lands on the stride's own beat, so a foot left behind falls back into step instead of skipping along.)
                    // (A walking foot left out of reach on its ball goes even while the other swings: a moment in the air, never
                    // a slide.)
                    else if((Vector2.Distance(ft.W,home)>.45f+(quad?0:.25f*Clamp01(stride-1))&&!OtherSwinging(i,feetCount))||moving&&!quad&&overReach[i]){lift=true;swingTime=moving&&!quad?Math.Min(heavy?1.2f:.42f,(1-u)/Math.Max(.3f,rateHz)+.02f):.16f;}
                    if(lift){ft.Swing=true;ft.Scrape=false;ft.From=ft.W;ft.FromTurn=Wrap(ft.Turn-Yaw);ft.T0=f.Time;ft.T1=f.Time+Math.Max(.08f,Math.Min(heavy?1.2f:.5f,swingTime));ft.Cycle=cycle;ft.Lift=heavy?(moving?HeavyLift(Speed):.06f):(moving?Lerp(d.Lift,d.RunLift,RunBlend(gk,Speed)):d.Lift*.7f)*(kind==NativeHordeKinds.Shambler&&(i==0)==(limpSide<0)?1-.6f*drag:1);}
                }
                if(ft.Swing)
                {
                    float s=Clamp01((f.Time-ft.T0)/Math.Max(1e-3f,ft.T1-ft.T0));
                    // Where the body will be when the foot lands, half a stance ahead of the hip.
                    Vector2 land=ft.Scrape?ft.To:home+velocity*Math.Max(0,ft.T1-f.Time)+(moving?ahead:Vector2.Zero);
                    // A heavy foot is carried there and stamped down (it arrives over its spot, then drops); a scrape drags low.
                    if(heavy&&!ft.Scrape){ft.W=Vector2.Lerp(ft.From,land,Sm(Math.Min(1,s/.85f)));ft.Y=ft.Lift*Stamp(s);}
                    else{ft.W=Vector2.Lerp(ft.From,land,Sm(s));ft.Y=ft.Lift*(float)Math.Sin(Math.PI*s);}
                    if(s>=1){ft.Swing=false;ft.Scrape=false;ft.W=land;ft.Y=0;ft.Turn=Yaw;Landed|=1<<i;if(heavy)lastLand=f.Time;}
                }
                else{ft.Y=0;if(!moving)ft.Cycle=cycle;}
                FootWorld[i]=new Vector3(ft.W.X,f.Ground,ft.W.Y);FootAir[i]=ft.Swing;
            }
        }
        // Left 0, right .5; the crawler's hands (2, 3) trot with the opposite feet.
        private static float FootOffset(int i)=>i==0?0:i==1?.5f:i==2?.5f:0;
        /// <summary>The share of a swing over which a low knee's lift eases in after the foot leaves and out before it lands.</summary>
        public const float ClearEase=.22f;
        /// <summary>The farthest a walking knee turns aside of the add's forward about the leg's own line (rad, 20 degrees).</summary>
        public const float KneeSplay=.35f;
        /// <summary>A knee's pole kept within KneeSplay of forward about the leg's line from `hip` to `target` (the add's frame).</summary>
        public static Vector3 Splayed(Vector3 pole,Vector3 hip,Vector3 target)
        {
            var d=target-hip;float n=d.Length();if(n<1e-4f)return pole;d/=n;
            var ahead=Vector3.UnitZ-d*d.Z;float a=ahead.Length();if(a<.2f)return pole;ahead/=a;var side=Vector3.Cross(d,ahead);
            float along=Vector3.Dot(pole,ahead),aside=Vector3.Dot(pole,side);
            if(along>0&&Math.Abs(aside)<=along*(float)Math.Tan(KneeSplay))return pole;
            return ahead*(float)Math.Cos(KneeSplay)+side*(aside<0?-1:1)*(float)Math.Sin(KneeSplay);
        }
        private static float SideOf(int i)=>i==0||i==2?-1:1;
        private bool OtherSwinging(int i,int count){for(int j=0;j<count;j++)if(j!=i&&feet[j].Swing&&((j&1)!=(i&1)||count==2))return true;return false;}
        private Vector2 Home(int i,Vector2 at,in Def d)
        {
            float side=SideOf(i);var right=Right.LengthSquared()<.5f?new Vector2(1,0):Right;var fwd=Forward.LengthSquared()<.5f?new Vector2(0,1):Forward;
            // The crawler's hands stand ahead of its shoulders; a seed staggers the idle stance a little.
            float ahead=i>=2?.62f:i==0?.03f*(NativeHordeKinds.Hash(seed,70)-.5f):-.03f*(NativeHordeKinds.Hash(seed,70)-.5f);
            // (A trotting crawler's hind feet keep behind its hips, so a stride seldom carries one through under them, where its
            // hips, a hand's breadth over its ankles, fold the leg shut; standing they tuck in as before, inside its hit volume.)
            float width=i>=2?d.Width*1.3f:d.Width;return at+right*(side*width)+fwd*(ahead-(i<2&&quad?.1f+.3f*Clamp01(Speed/1.2f):0));
        }
        /// <summary>A Unity world point in the rig's source frame (root at the body, +x the add's left, +z forward).</summary>
        public Vector3 ToLocal(Vector3 w,Vector3 at){float dx=w.X-at.X,dz=w.Z-at.Z;return new Vector3(-(dx*Right.X+dz*Right.Y),w.Y-at.Y,dx*Forward.X+dz*Forward.Y);}
        /// <summary>A point in the rig's source frame back in the Unity world.</summary>
        public Vector3 ToWorld(Vector3 l,Vector3 at)=>new Vector3(at.X-l.X*Right.X+l.Z*Forward.X,at.Y+l.Y,at.Z-l.X*Right.Y+l.Z*Forward.Y);
        // The Siren's twitches: a sharp head jerk at hashed times about every 2-4 s, settling back.
        /// <summary>How long the head's crooked twitch takes to come in (s).</summary>
        public const float TwitchRise=.14f;
        private float Twitch(float t)
        {
            float period=2.2f+1.8f*NativeHordeKinds.Hash(seed,80),k=(t+period*NativeHordeKinds.Hash(seed,81))/period;int n=(int)Math.Floor(k);float u=(k-n)*period;
            // (Eased in over .14 s: the old straight 60 ms ramp started the head at full speed, a twitch on top of the hits'.)
            float amp=(float)(NativeGoreRules.Hash01(seed,n,82)-.5)*.9f;return u<TwitchRise?amp*Sm(u/TwitchRise):u<TwitchRise+.46f?amp*(1-Sm((u-TwitchRise)/.46f)):0;
        }

        // ================================================================================================================
        // Attacks: the wind-up into a held telegraph, the strike arriving at Strike exactly, follow-through and recovery
        // ================================================================================================================
        /// <summary>How long before the strike the blow starts to travel (the swing), per attack: the telegraph holds until then.</summary>
        public static float SwingLead(NativeHordeKinds.Attack a)=>a==NativeHordeKinds.Attack.Smash?.12f:a==NativeHordeKinds.Attack.Spit?.1f:a==NativeHordeKinds.Attack.Belly?.14f:.08f;
        /// <summary>
        /// The attack's channels at age a: cock (0..1 into the wind-up's telegraph pose, held), swing (0..1 across the strike,
        /// exactly 1 at Strike), follow (the follow-through after it), settle (back to the gait by Recover) and the overall weight.
        /// </summary>
        /// <summary>The strike's ease-in power (its speed is greatest on the contact frame) and the cancelled blow's let-go (s).</summary>
        public const float StrikeIn=2.2f,CancelFade=.3f;
        public static void Channels(float a,float strike,float recover,NativeHordeKinds.Attack kind,out float cock,out float swing,out float follow,out float settle,out float weight)
        {
            cock=swing=follow=settle=weight=0;if(float.IsNaN(a)||a<0||a>=recover+.25f)return;
            float lead=Math.Min(SwingLead(kind),strike*.45f),windEnd=strike-lead;
            // Anticipation: into the telegraph slow-in slow-out, held there.
            cock=BossMath.Smoother(a/Math.Max(.05f,windEnd*.75f));
            // The strike accelerates into contact (ease in: fastest on the contact frame, which is LastAttack + strike exactly)
            // instead of slowing into it.
            float u=a>=strike?1:a<windEnd?0:Clamp01((a-windEnd)/Math.Max(1e-3f,lead));swing=u>=1?1:(float)Math.Pow(u,StrikeIn);
            float through=kind==NativeHordeKinds.Attack.Smash?.28f:.16f;
            // Follow-through: carried a beat past contact, then eased out (decelerating), and the recovery settles back with weight
            // (over and back, landing still).
            follow=a<strike?0:1-BossEasing.Eval(BossEase.Out,(a-strike-through)/Math.Max(.05f,(recover-strike-through)*.6f),2.2f);
            settle=a<strike?0:BossEasing.Eval(BossEase.Settle,(a-strike)/Math.Max(.05f,recover-strike),2.2f);
            // The layer comes in over the first moment of the wind-up (no snap as it starts) and goes after Recover.
            weight=a<recover?BossMath.Smoother(a/Math.Max(.04f,Math.Min(.12f,windEnd*.4f))):1-BossMath.Smoother((a-recover)/.25f);
            if(a>=strike)cock=1-BossEasing.Eval(BossEase.Out,(a-strike)/through,2f);
        }
        private static float CockCrouch(NativeHordeKinds.Attack a)=>a==NativeHordeKinds.Attack.Lunge?.12f:a==NativeHordeKinds.Attack.Pounce?.14f:a==NativeHordeKinds.Attack.Smash?.02f:.04f;
        // The trunk's part of each attack (pitch + forward, yaw toward the striking side, roll), the crouch and the head.
        private static void AttackBody(in HordeMotionInput f,float cock,float swing,float follow,float settle,out float pitch,out float yaw,out float roll,out float crouch,out float head)
        {
            pitch=yaw=roll=crouch=head=0;if(cock+swing+follow<=0)return;float s=f.Side,hold=swing>0?1-swing:cock,hit=Math.Max(swing,follow*(1-settle));
            switch(f.Attack)
            {
                case NativeHordeKinds.Attack.Swipe:pitch=-.18f*hold+.38f*hit;yaw=s*(.45f*hold-.55f*hit);roll=-s*.12f*hold;head=.1f*hold-.1f*hit;break;
                case NativeHordeKinds.Attack.Lunge:pitch=.25f*hold+.4f*hit;crouch=.05f*hold-.03f*hit;head=-.3f*hold-.2f*hit;break;
                case NativeHordeKinds.Attack.Smash:pitch=-.42f*hold+.95f*hit;crouch=-.03f*hold+.13f*hit;head=.25f*hold-.45f*hit;break;
                case NativeHordeKinds.Attack.Spit:pitch=-.55f*hold+.62f*hit;head=.12f*hold-.42f*hit;crouch=.04f*hold;break;
                case NativeHordeKinds.Attack.Belly:pitch=-.4f*hold+.3f*hit;crouch=.06f*hold-.04f*hit;head=.2f*hold;break;
                case NativeHordeKinds.Attack.Pounce:pitch=.2f*hold-.1f*hit;crouch=.08f*hold-.12f*hit;break;
            }
            float w=1-settle;pitch*=w;yaw*=w;roll*=w;crouch*=w;head*=w;
        }
        // The arms' part: FK targets layered over the gait's arms, weighted by the attack.
        private static void AttackArms(in HordeMotionInput f,CapturedRig.Pose p,float cock,float swing,float follow,float settle,float w)
        {
            if(w<=0)return;float hold=swing>0?1-swing:cock,hit=Math.Max(swing,follow*(1-settle)),k=w*(1-settle*.85f);
            for(int i=0;i<2;i++)
            {
                bool left=i==0;int arm=left?CapturedRig.LArm:CapturedRig.RArm,elbow=arm+1,hand=arm+2;float sgn=left?1:-1;bool striker=(f.Side>0)!=left;
                Vector3 e=Vector3.Zero,el=Vector3.Zero;
                switch(f.Attack)
                {
                    case NativeHordeKinds.Attack.Swipe:
                        // The striking claw reared back and high, then raked across and down; the other arm braces forward.
                        if(striker){e=new Vector3(-2.5f*hold-1.15f*hit,-sgn*.3f*hold,sgn*(.9f*hold-.15f*hit));el=new Vector3(-1.5f*hold-.25f*hit,0,0);}
                        else{e=new Vector3(-.7f*hold+.3f*hit,0,sgn*.2f);el=new Vector3(-.8f*hold,0,0);}break;
                    case NativeHordeKinds.Attack.Lunge:
                        // Both blades drawn back low, then thrust forward together.
                        e=new Vector3(.9f*hold-1.65f*hit,0,sgn*(.35f*hold+.05f*hit));el=new Vector3(-1.2f*hold-.15f*hit,0,0);break;
                    case NativeHordeKinds.Attack.Smash:
                        // Both fists raised high over the head, then hammered down to the floor ahead.
                        e=new Vector3(-3.0f*hold-1.0f*hit,0,sgn*(.25f*hold+.05f*hit));el=new Vector3(-1.0f*hold-.15f*hit,0,0);break;
                    case NativeHordeKinds.Attack.Spit:
                        // The claws spread back as it rears (the reel's arms), then thrown forward with the spit.
                        e=new Vector3(.35f*hold-.45f*hit,0,sgn*(.7f*hold+.25f*hit));el=new Vector3(-1.1f*hold-.3f*hit,0,0);break;
                    case NativeHordeKinds.Attack.Belly:
                        e=new Vector3(.4f*hold-.6f*hit,0,sgn*(.8f*hold+.3f*hit));el=new Vector3(-.6f*hold,0,0);break;
                    case NativeHordeKinds.Attack.Pounce:
                        e=new Vector3(.4f*hold-1.6f*hit,0,sgn*.3f);el=new Vector3(-1.1f*hold,0,0);break;
                }
                if(f.Attack!=NativeHordeKinds.Attack.Swipe&&f.Attack!=NativeHordeKinds.Attack.Lunge&&f.Attack!=NativeHordeKinds.Attack.Smash&&!striker)e*=1;
                var target=CapturedRig.Euler(e.X,e.Y,e.Z);p.Rotation[arm]=Quaternion.Slerp(p.Rotation[arm],target,k);
                p.Rotation[elbow]=Quaternion.Slerp(p.Rotation[elbow],CapturedRig.Euler(el.X,el.Y,el.Z),k);
                // Claws spread in the telegraph and close through the blow.
                p.Rotation[hand]=Quaternion.Slerp(p.Rotation[hand],CapturedRig.Euler(-.5f*hold+.35f*hit,0,sgn*.25f*hold),k);
            }
        }

        // ================================================================================================================
        // The crawler: trunk pitched level, hands planted as forefeet (the same planting as the feet), head up looking ahead
        // ================================================================================================================
        /// <summary>How high the crawler carries its rig root above the floor (its pelvis sits .24 below it).</summary>
        public const float CrawlTrunk=.46f;
        private void CrawlerPose(in HordeMotionInput f,CapturedRig.Pose p,in Def d,float v,float run,float moving,float alert,float lookYaw,float cock,float swing,float follow,float settle,float attackW,NativeHordeReact react,float age)
        {
            float ph=Phase*(float)Math.PI*2,t=f.Time;float hold=swing>0?1-swing:cock,hit=Math.Max(swing,follow*(1-settle));
            float bob=d.Bob*moving*(float)Math.Cos(ph*2)+.006f*(1-moving)*(float)Math.Sin(t*2.6f*idleRate);
            // The body: its root sits at the crawl capsule's middle; the trunk is pitched near level, the pounce coils it back and launches.
            float pitch=1.28f+.12f*hold-.25f*hit+alert*.05f;
            p.Position[CapturedRig.Body]=new Vector3(d.Sway*moving*(float)Math.Sin(ph),f.Ground-f.Root.Y+CrawlTrunk+bob-.06f*hold+.1f*hit,-.12f-.12f*hold+.25f*hit);
            p.Rotation[CapturedRig.Pelvis]=CapturedRig.Euler(pitch,d.Twist*moving*(float)Math.Sin(ph),d.Roll*moving*(float)Math.Cos(ph));
            p.Rotation[CapturedRig.Spine]=CapturedRig.Euler(.05f-.1f*hold,-d.Twist*.5f*moving*(float)Math.Sin(ph),0);
            p.Rotation[CapturedRig.Chest]=CapturedRig.Euler(.02f-.1f*hold+.1f*hit,-d.Twist*.5f*moving*(float)Math.Sin(ph),0);
            float twitch=Twitch(t);
            p.Rotation[CapturedRig.Neck]=CapturedRig.Euler(-.42f-.12f*alert+.2f*hold,lookYaw*.4f+twitch*.3f,tilt*.4f);
            p.Rotation[CapturedRig.Head]=CapturedRig.Euler(-.62f-.1f*alert+.25f*hold-.3f*hit,lookYaw*.5f+twitch*.5f,tilt*.6f+twitch*.6f);
            LifeTrunk(p,f,moving);LifeHead(p,f,alert);
            react?.Trunk(p,f.Time,Forward,Right);
            // Its shoulders sit narrower than a biped's (inside the crawl capsule), the forelegs under the chest.
            {float cx=Math.Min(.2f,ClavicleX-ShoulderInset(kind,f.Look));p.Position[CapturedRig.LClav]=new Vector3(cx,.18f,.02f);p.Position[CapturedRig.RClav]=new Vector3(-cx,.18f,.02f);}
            p.World();
            for(int i=0;i<2;i++){Vector3 local=ToLocal(FootWorld[i],f.Root);local.Y=f.Ground-f.Root.Y+AnkleHeight+.04f+feet[i].Y;LegKept(p,i==0,local,HindPole,i);}
            // The forefeet: hands to their planted targets (chest frame), the pounce reaching them forward.
            p.World();var chest=p.WorldRotation[CapturedRig.Chest];var inv=Quaternion.Inverse(chest);var cpos=p.WorldPosition[CapturedRig.Chest];
            for(int i=0;i<2;i++)
            {
                bool left=i==0;int h=2+i;Vector3 local=ToLocal(FootWorld[h],f.Root);local.Y=f.Ground-f.Root.Y+.09f+feet[h].Y;
                local+=new Vector3(0,.25f*hit*(1-swing*.5f),.35f*hit);
                Vector3 inChest=Vector3.Transform(local-cpos,inv);Vector3 pole=Vector3.Transform(new Vector3(left?.15f:-.15f,.8f,-.55f),inv);
                var wrist=Quaternion.Normalize(CapturedRig.Euler(-1.5f+.5f*hold,0,0));
                CapturedRig.Arm(p,left,inChest,inv*wrist,pole);
            }
            react?.Arms(p,f.Time);
            p.World();
        }

        /// <summary>The crawler's hind knees point up and forward (a folded haunch), not straight ahead as a biped's.</summary>
        private static readonly Vector3 HindPole=Vector3.Normalize(new Vector3(0,.75f,.66f));
        /// <summary>
        /// <see cref="Leg"/> that keeps its knee on the side it was last frame: the pole leans toward the knee's last direction (rig
        /// frame), so a pole that comes to lie along the leg (the crawler's folded haunch, a long stride's straight leg) no longer
        /// flips the knee from one side to the other between frames. The ankle still meets its target exactly.
        /// </summary>
        private void LegKept(CapturedRig.Pose p,bool left,Vector3 target,Vector3 pole,int leg)
        {
            var pl=CapturedRig.Unit(pole,Vector3.UnitZ);var last=legBend[leg];if(legBendKnown[leg])pl=CapturedRig.Unit(pl+last*1.5f,pl);
            int hip=left?CapturedRig.LHip:CapturedRig.RHip;
            // A target almost on the hip (the crawler's hind feet, its hips a hand's breadth over its ankles) leaves the leg's
            // direction to rounding: it keeps last frame's direction there instead, so the folded leg never spins round.
            {var d=target-p.WorldPosition[hip];float n=d.Length();if(legBendKnown[leg]&&n<FoldNear){float w=Clamp01((n-.03f)/(FoldNear-.03f));var dir=CapturedRig.Unit(legDir[leg]*(1-w)+(n>1e-5f?d/n:legDir[leg])*w,legDir[leg]);target=p.WorldPosition[hip]+dir*Math.Max(n,.1f);}}
            Leg(p,left,target,pl);p.World();
            var knee=p.WorldPosition[hip+1]-(p.WorldPosition[hip]+p.WorldPosition[hip+2])*.5f;if(knee.LengthSquared()>1e-8f){legBend[leg]=Vector3.Normalize(knee);legBendKnown[leg]=true;}
            var shin=p.WorldPosition[hip+2]-p.WorldPosition[hip];if(shin.LengthSquared()>1e-8f)legDir[leg]=Vector3.Normalize(shin);
        }
        /// <summary>Within this of its hip (rig units) a leg's target only steers its direction gradually (the leg folds shut there).</summary>
        public const float FoldNear=.2f;
        private readonly Vector3[] legBend=new Vector3[4],legDir=new Vector3[4];private readonly bool[] legBendKnown=new bool[4];
        /// <summary>The leg's reach (rig units: just short of straight) and the foot's ball (the toe joint) from the ankle.</summary>
        public const float LegReach=.492f,BallAhead=.09f,BallDrop=.02f,HeelMost=1f;
        /// <summary>Each foot's heel lift (rad: 0 flat), for the checks: a planted foot rolled onto its ball.</summary>
        public readonly float[] Heel=new float[2];
        private readonly float[] heelLift=new float[2];private readonly Vector3[] heelRise=new Vector3[2];private readonly bool[] overReach=new bool[2];private bool setOff;
        /// <summary>The half-life (s) of the speed the cycle and the blends read, eased off the presented root's velocity.</summary>
        public const float SpeedHalfLife=.08f;private BossSpring speedEase;
        /// <summary>The most the take's stand-to-move blend changes a second (it crosses in a third of a second at the least).</summary>
        public const float MoveSlew=3f;private float moveArg;
        /// <summary>The half-life (s) of the health the hurt sag reads (the head's droop, the hunch, the loose head, the dead arm and
        /// the limp): a hit takes a pistol's quarter of a shambler's health in one frame, and the sag stepped with it.</summary>
        public const float HurtHalfLife=.2f;private BossSpring hurtEase;private float hurtShown=1;
        /// <summary>The half-lives (s) of the hips easing down into a long stride and back up out of it.</summary>
        public const float SinkDown=.045f,SinkUp=.1f;private BossSpring sinkEase;
        /// <summary>
        /// Keeps a planted foot where it stands when the hips have carried the leg past its reach, instead of letting the ankle
        /// slide after them: a foot landing beyond reach (a long stride reached for) is planted where the leg reached; a trailing
        /// foot left behind peels its heel and rolls over its ball (the ankle rises on the arc round the ball; the toe stays put),
        /// the way a long stride's back foot pushes off; a foot still out of reach steps (<see cref="Step"/>) rather than slide. A
        /// lifting foot lets the roll go over the first third of its swing, so nothing pops. `target` the ankle's target (rig
        /// frame), moved; returns the heel's lift (rad). Upright kinds (the Hulk's stamping feet stay flat: they only step; not
        /// the crawler); not on the cheapest tier.
        /// </summary>
        private float Reached(CapturedRig.Pose p,in HordeMotionInput f,int i,int hip,ref Vector3 target)
        {
            Heel[i]=0;overReach[i]=false;if(quad||f.Tier>=NativeHordeModels.Impostor){heelLift[i]=0;return 0;}
            var hipAt=p.WorldPosition[hip];
            if(feet[i].Swing)
            {
                if(heelLift[i]<=0)return 0;float s=Clamp01((f.Time-feet[i].T0)/Math.Max(1e-3f,feet[i].T1-feet[i].T0)),fade=1-Sm(s/.35f);
                if(fade<=0){heelLift[i]=0;return 0;}target+=heelRise[i]*fade;return Heel[i]=heelLift[i]*fade;
            }
            var flat=target;
            if((Landed&(1<<i))!=0)
            {
                float dy=hipAt.Y-target.Y,hx=target.X-hipAt.X,hz=target.Z-hipAt.Z,h=(float)Math.Sqrt(hx*hx+hz*hz),most=(float)Math.Sqrt(Math.Max(0,LegReach*LegReach-dy*dy));
                if(h>most&&h>1e-4f)
                {
                    float k=most/h;target.X=hipAt.X+hx*k;target.Z=hipAt.Z+hz*k;flat=target;
                    var w=ToWorld(target,f.Root);feet[i].W=new Vector2(w.X,w.Z);FootWorld[i]=new Vector3(w.X,f.Ground,w.Z);
                }
            }
            float heel=0;
            if(Vector3.DistanceSquared(hipAt,target)>LegReach*LegReach)
            {
                float tw=FootTwist(i,f.Time);var fwd=new Vector3((float)Math.Sin(tw),0,(float)Math.Cos(tw));
                if(!heavy&&Vector3.Dot(target-hipAt,fwd)<0)
                {
                    var ball=target+fwd*BallAhead-Vector3.UnitY*BallDrop;float lo=0,hi=HeelMost;
                    if(Vector3.DistanceSquared(hipAt,HeelAnkle(ball,fwd,hi))<=LegReach*LegReach)for(int n=0;n<10;n++){float m=(lo+hi)*.5f;if(Vector3.DistanceSquared(hipAt,HeelAnkle(ball,fwd,m))<=LegReach*LegReach)hi=m;else lo=m;}
                    heel=hi;target=HeelAnkle(ball,fwd,heel);
                }
                // (Still out of reach on its ball, or out of reach ahead: the foot steps next frame rather than slide. A heel peeled
                // (nearly) all the way goes too: the hips carry on, and the ball slid 3 cm the next frame as an add broke into a run.)
                overReach[i]=heel>=HeelMost*.95f||Vector3.DistanceSquared(hipAt,target)>LegReach*LegReach*1.0004f;
            }
            else overReach[i]=false;
            heelLift[i]=heel;heelRise[i]=target-flat;return Heel[i]=heel;
        }
        // The ankle with its heel lifted `heel` rad off the floor round the ball at `ball` (`fwd` the foot's facing, rig frame).
        private static Vector3 HeelAnkle(Vector3 ball,Vector3 fwd,float heel)
        {
            float c=(float)Math.Cos(heel),s=(float)Math.Sin(heel);return ball-fwd*(BallAhead*c-BallDrop*s)+Vector3.UnitY*(BallDrop*c+BallAhead*s);
        }
        /// <summary>CapturedRig.Leg with a chosen knee direction (rig frame): the hip and knee turned so the ankle meets `target`.</summary>
        private static void Leg(CapturedRig.Pose p,bool left,Vector3 target,Vector3 pole)
        {
            p.World();int hip=left?CapturedRig.LHip:CapturedRig.RHip,knee=hip+1,foot=hip+2;var parent=p.WorldRotation[CapturedRig.Pelvis];var inv=Quaternion.Inverse(parent);
            Vector3 d=Vector3.Transform(target-p.WorldPosition[hip],inv);float reach=Math.Max(.1f,Math.Min(.497f,d.Length()));d=d.LengthSquared()<1e-10f?-Vector3.UnitY:Vector3.Normalize(d);
            Vector3 pl=Vector3.Transform(pole,inv);Vector3 bend=pl-d*Vector3.Dot(d,pl);bend=bend.LengthSquared()<1e-8f?Vector3.UnitZ:Vector3.Normalize(bend);
            Vector3 upper=Vector3.Normalize(d*(reach*.5f)+bend*(float)Math.Sqrt(Math.Max(0,.0625f-reach*reach*.25f)));p.Rotation[hip]=CapturedRig.FromTo(-Vector3.UnitY,upper);
            Vector3 lower=Vector3.Transform(d*reach-upper*.25f,Quaternion.Inverse(p.Rotation[hip]));p.Rotation[knee]=CapturedRig.FromTo(-Vector3.UnitY,lower.LengthSquared()<1e-10f?-Vector3.UnitY:Vector3.Normalize(lower));
            p.Rotation[foot]=Quaternion.Inverse(parent*p.Rotation[hip]*p.Rotation[knee]);
        }
        // =============================================================================================================
        // Deaths (the body chars and burns away by height on NativeMinionFx's clock; these are the bodies' falls)
        // =============================================================================================================
        /// <summary>When a captured death's head reaches the floor (s after death): every take is retimed to it, the rest of
        /// the take (the last twitches and settling) laid over the remaining seconds.</summary>
        public const float DownAt=.95f;
        /// <summary>The floor under the rig root (the root stands half the add capsule above it).</summary>
        public const float Floor=-NativeHordeKinds.AddHeight*.5f;
        private static readonly float[] staticRow=new float[CapturedClip.Channels];private static readonly Vector3[] staticFeet=new Vector3[2],staticKnees=new Vector3[2];
        /// <summary>
        /// The kind's death at `age` seconds (0..NativeMinionFx.DeathSeconds), from where it stood. The upright kinds fall on the
        /// zombie pack's deaths: back, forward or to a side as `dir` pushes (source frame, horizontal: the killing blow's push;
        /// a sliced death falls away from it), a take per add by `seed`, the head on the floor at <see cref="DownAt"/> and the
        /// body kept on the floor throughout. The crawler flips onto its back and the bloater swells and bursts (its body hides at
        /// <see cref="BurstAt"/>; Core/NativeHordeFx throws the burst), procedurally.
        /// </summary>
        public static void Death(CapturedRig.Pose p,int kind,uint seed,float age,Vector2 dir,int look=-1)
        {
            if(kind==NativeHordeKinds.Crawler||kind==NativeHordeKinds.Bloater||!NativeZombieMotion.Captured(kind)){ProceduralDeath(p,kind,seed,age,dir,look);return;}
            int c=NativeZombieMotion.Death(seed,dir);var clip=NativeZombieClips.Clip(c);float down=clip.Marks[1],end=clip.Marks[2],a=Math.Max(0,age);
            float t=a<DownAt?a/DownAt*down:down+(end-down)*Clamp01((a-DownAt)/Math.Max(.1f,(float)NativeMinionFx.DeathSeconds-DownAt));
            NativeZombieClips.Sample(c,t,staticRow);NativeZombieClips.Pose(staticRow,p,true,ShoulderInset(kind,look));
            OnFloor(p);
        }
        /// <summary>Sets the pose's lowest part on the floor (neither through it nor floating over it).</summary>
        private static void OnFloor(CapturedRig.Pose p){float low=p.Lowest();var b=p.Position[CapturedRig.Body];b.Y+=Floor-low;p.Position[CapturedRig.Body]=b;p.World();}

        // =============================================================================================================
        // Entrances: the vent's climb, the dive into a vent, the brute's drop (the door and cell walk in on the gait)
        // =============================================================================================================
        /// <summary>The vent's rim (the edge the climber hauls itself over), behind its standing point (rig z; vent-fall.ts VENT_RIM_Z).</summary>
        public const float VentRim=-.38f;
        /// <summary>
        /// A vent climb at `tau` seconds into the source's 1.95 s climb (NativeEntryRules.Climb; a follower starts .35 s in), the
        /// root already at the authority's climbing position `climb` off its standing point (rig frame: y up, z forward). The
        /// zombie rises up the shaft with its claws reaching for the rim, slams its hands on the floor past the rim, hauls its
        /// chest over, then gets its feet under it and stands on the pack's crawl-to-stand.
        /// </summary>
        public static void VentClimb(CapturedRig.Pose p,int kind,uint seed,float tau,Vector3 climb,int look=-1)
        {
            // (The entrances can be the first horde pose of a session: the take tables are built here as well, never read unbuilt.)
            NativeZombieMotion.Warm();float u=Math.Max(0,Math.Min(1.95f,tau));int c=NativeZombieMotion.CrawlStand;float dur=NativeZombieClips.Duration(c);
            // The stand-up: the take's kneel at first, then its rise from 1.15 s, compressed into the climb's last .8 s.
            float stand=Sm((u-1.15f)/.8f);NativeZombieClips.Sample(c,stand*dur,staticRow);NativeZombieClips.Pose(staticRow,p,true,ShoulderInset(kind,look));
            float haul=Sm((u-.45f)/.5f)*(1-stand),reach=1-Sm((u-.5f)/.25f);
            // In the shaft: the body upright, the trunk pitched over the rim as it hauls; the stand-up takes the take's own trunk.
            float hold=1-stand;var b=p.Position[CapturedRig.Body];b.Y=Lerp(-.06f,b.Y,stand);b.Z=Lerp(-.04f,b.Z,stand);p.Position[CapturedRig.Body]=b;
            p.Rotation[CapturedRig.Pelvis]=Quaternion.Slerp(CapturedRig.Euler(.15f+.25f*haul),p.Rotation[CapturedRig.Pelvis],stand);
            p.Rotation[CapturedRig.Spine]=Quaternion.Slerp(CapturedRig.Euler(.1f+.35f*haul),p.Rotation[CapturedRig.Spine],stand);
            p.Rotation[CapturedRig.Chest]=Quaternion.Slerp(CapturedRig.Euler(-.12f*reach+.3f*haul),p.Rotation[CapturedRig.Chest],stand);
            p.Rotation[CapturedRig.Neck]=Quaternion.Slerp(CapturedRig.Euler(-.35f*reach-.3f*haul,0,.2f*(NativeHordeKinds.Hash(seed,120)-.5f)),p.Rotation[CapturedRig.Neck],stand);
            p.Rotation[CapturedRig.Head]=Quaternion.Slerp(CapturedRig.Euler(-.3f*reach-.35f*haul),p.Rotation[CapturedRig.Head],stand);
            // Legs hang and kick in the shaft until the feet find the floor.
            if(hold>0)for(int i=0;i<2;i++){int hip=i==0?CapturedRig.LHip:CapturedRig.RHip;float k=(float)Math.Sin(u*9+i*2.1f)*.25f;p.Rotation[hip]=Quaternion.Slerp(p.Rotation[hip],CapturedRig.Euler(-.5f+k),hold);p.Rotation[hip+1]=Quaternion.Slerp(p.Rotation[hip+1],CapturedRig.Euler(.9f-k),hold);}
            // The claws: reaching up for the rim, then planted on the floor past it (fixed in the world as the body hauls up).
            p.World();float floor=Floor-climb.Y,rim=VentRim-climb.Z;var chest=p.WorldRotation[CapturedRig.Chest];var inv=Quaternion.Inverse(chest);var cpos=p.WorldPosition[CapturedRig.Chest];
            float grab=hold*(1-Sm((u-1.35f)/.4f));
            // (The claws reach and plant where they did for the rig's own shoulders, as far in as the shoulders are set in.)
            float sin=ShoulderInset(kind,look);
            if(grab>0)for(int i=0;i<2;i++)
            {
                bool left=i==0;float sx=left?1:-1;float sway=(float)Math.Sin(u*13+i*1.7f)*.05f*reach;
                var up=new Vector3(sx*(.22f-sin),Math.Min(floor+.25f,cpos.Y+.75f),rim+.05f+sway);var plant=new Vector3(sx*(.26f-sin),floor+.06f,rim+.16f+(left?.02f:-.03f));
                var target=Vector3.Lerp(up,plant,Sm((u-.38f)/.17f));var wrist=Quaternion.Normalize(CapturedRig.Euler(-1.25f-.3f*reach,0,0));
                var restHand=p.WorldPosition[left?CapturedRig.LHand:CapturedRig.RHand];target=Vector3.Lerp(restHand,target,grab);
                CapturedRig.Arm(p,left,Vector3.Transform(target-cpos,inv),inv*wrist,Vector3.Transform(new Vector3(sx*.7f,.3f,-.6f),inv));
            }
            p.World();
        }
        /// <summary>A dive into a vent at `age` (0 .. .9 s): it throws itself forward and down (the pack's stand-to-crawl fall)
        /// and sinks into the shaft (hidden after .9 s).</summary>
        public static void Dive(CapturedRig.Pose p,int kind,uint seed,float age,int look=-1)
        {
            NativeZombieMotion.Warm();float a=Math.Max(0,Math.Min(.9f,age));int c=NativeZombieMotion.StandCrawl;NativeZombieClips.Sample(c,a/.9f*1.25f,staticRow);NativeZombieClips.Pose(staticRow,p,true,ShoulderInset(kind,look));
            var b=p.Position[CapturedRig.Body];b.Y-=1.5f*Sm((a-.3f)/.6f);p.Position[CapturedRig.Body]=b;p.World();
        }
        /// <summary>
        /// The brute's jump-down (Core/NativeHordeDrop) at `age` s after the add's SpawnAt, turning active `span` s after it, from
        /// `height` above its landing. Writes the pose (the root at the landing point) and the root's height over its standing
        /// height (`lift`); false while the landing spot is only telegraphed (nothing to draw). The leap is the pack's HyperChase
        /// jump start, the fall its air take, the landing one of its jump ends (by `seed`) with the feet on the floor.
        /// </summary>
        public static bool Drop(CapturedRig.Pose p,uint seed,float age,float span,float height,out float lift,out NativeHordeDrop.Phase phase)=>Drop(p,seed,age,span,height,NativeHordeDrop.Brute,out lift,out phase);
        /// <summary>The drop on a kind's profile (the Hulk's longer leap and recovery play the same takes slower).</summary>
        public static bool Drop(CapturedRig.Pose p,uint seed,float age,float span,float height,in NativeHordeDrop.DropProfile profile,out float lift,out NativeHordeDrop.Phase phase)
        {
            // (Before any gait has posed, JumpStart and JumpAir still read take 0, an idle: the leap played a 6 s idle in half a second.)
            NativeZombieMotion.Warm();phase=NativeHordeDrop.At(age,span,height,profile,out float into,out lift);
            switch(phase)
            {
                case NativeHordeDrop.Phase.Warn:p.Reset();p.World();return false;
                case NativeHordeDrop.Phase.Leap:{int c=NativeZombieMotion.JumpStart;NativeZombieClips.Sample(c,into/profile.Leap*NativeZombieClips.Duration(c),staticRow);break;}
                case NativeHordeDrop.Phase.Air:NativeZombieClips.Sample(NativeZombieMotion.JumpAir,into,staticRow);break;
                default:
                {
                    int c=NativeZombieMotion.Land(seed);var clip=NativeZombieClips.Clip(c);float deep=clip.Marks[1],end=clip.Marks[2];
                    // The compression lands at once, the rise is stretched over the recovery; once done it holds the take's end
                    // (the clock of the Done phase restarts at 0: reading the take from its start there popped the whole body).
                    float t=phase==NativeHordeDrop.Phase.Done?end:into<.12f?into/.12f*deep:deep+(end-deep)*Clamp01((into-.12f)/Math.Max(.1f,profile.Recover-.12f));
                    NativeZombieClips.Sample(c,t,staticRow);break;
                }
            }
            NativeZombieClips.Pose(staticRow,p);
            // The fall's last beat: both fists raised over the head to come down with the landing (Core/NativeHordeHeavy).
            if(phase==NativeHordeDrop.Phase.Air)
            {
                float up=FistsUp(NativeHordeDrop.Fall(height,profile)-into);
                if(up>0){Bend(p,CapturedRig.Chest,-.15f*up,0,0);Reach(p,false,new Vector3(-.36f,.5f,.12f),.85f*up);Reach(p,true,new Vector3(.36f,.5f,.12f),.85f*up);}
            }
            if(phase==NativeHordeDrop.Phase.Land||phase==NativeHordeDrop.Phase.Done)
            {
                // The feet stand on the floor where the take put them, the hips' compression over them (never deeper than the legs reach).
                p.World();var ankles=staticFeet;var knees=staticKnees;for(int i=0;i<2;i++){int hip=i==0?CapturedRig.LHip:CapturedRig.RHip;ankles[i]=p.WorldPosition[hip+2];knees[i]=p.WorldPosition[hip+1]-(p.WorldPosition[hip]+ankles[i])*.5f;}
                // The landing's key pose over the take (the Hulk's three-point, the brute's two fists; deepest just after LandAt,
                // rising by ActiveAt), the trunk inside the hit volume.
                LandKeys(p,profile,seed,phase==NativeHordeDrop.Phase.Land?into:profile.Recover+into);
                // The touchdown: the fall's last pose (the fists raised over the head) is where the landing starts. The hips and
                // trunk sink out of it into the compression over its first frames, slowing as the legs take the weight.
                bool slam=phase==NativeHordeDrop.Phase.Land&&into<SlamIn;
                if(slam)
                {
                    var air=slamFrom;NativeZombieClips.Sample(NativeZombieMotion.JumpAir,NativeHordeDrop.Fall(height,profile),staticRow);NativeZombieClips.Pose(staticRow,air);
                    Bend(air,CapturedRig.Chest,-.15f,0,0);Reach(air,false,new Vector3(-.36f,.5f,.12f),.85f);Reach(air,true,new Vector3(.36f,.5f,.12f),.85f);
                    float u=1-Clamp01(into/SlamIn),w=1-u*u;p.Position[CapturedRig.Body]=Vector3.Lerp(air.Position[CapturedRig.Body],p.Position[CapturedRig.Body],w);
                    p.Rotation[CapturedRig.Body]=Quaternion.Slerp(air.Rotation[CapturedRig.Body],p.Rotation[CapturedRig.Body],w);p.Rotation[CapturedRig.Pelvis]=Quaternion.Slerp(air.Rotation[CapturedRig.Pelvis],p.Rotation[CapturedRig.Pelvis],w);
                }
                var b=p.Position[CapturedRig.Body];b.Y=Math.Max(b.Y,-.42f);p.Position[CapturedRig.Body]=b;p.World();
                // (Never higher than the legs reach their spots: the hips sink to them.)
                float sinkTo=0;for(int i=0;i<2;i++){var hipAt=p.WorldPosition[i==0?CapturedRig.LHip:CapturedRig.RHip];float dx=ankles[i].X*1.15f-hipAt.X,dz=ankles[i].Z-hipAt.Z,h2=dx*dx+dz*dz,dy=hipAt.Y-(Floor+AnkleHeight),R=.49f;sinkTo=Math.Max(sinkTo,h2>=R*R?dy:dy-(float)Math.Sqrt(R*R-h2));}
                if(sinkTo>0){b.Y=Math.Max(-.42f,b.Y-sinkTo);p.Position[CapturedRig.Body]=b;p.World();}
                for(int i=0;i<2;i++){var k=knees[i];k.Z=Math.Max(k.Z,.05f);Leg(p,i==0,new Vector3(ankles[i].X*1.15f,Floor+AnkleHeight,ankles[i].Z),CapturedRig.Unit(k,Vector3.UnitZ));}
                KeyHands(p,staticKeys,1);
                // The slam: the fists come down from over the head onto the floor over the same frames, accelerating, instead of
                // arriving there on the touchdown's frame (the feet are on the floor from the touchdown).
                if(slam){float w=Clamp01(into/SlamIn);w*=w;var air=slamFrom;for(int j=CapturedRig.Spine;j<CapturedRig.LHip;j++)p.Rotation[j]=Quaternion.Slerp(air.Rotation[j],p.Rotation[j],w);}
            }
            p.World();return true;
        }
        /// <summary>How long (s) the drop's fists take from over the head to the floor after the touchdown.</summary>
        public const float SlamIn=.045f;private static readonly CapturedRig.Pose slamFrom=new CapturedRig.Pose();
        /// <summary>The procedural deaths (the crawler's flip, the bloater's burst; a kind off the capture).</summary>
        private static void ProceduralDeath(CapturedRig.Pose p,int kind,uint seed,float age,Vector2 dir,int look=-1)
        {
            p.Reset();Shoulders(p,ShoulderInset(kind,look));float a=Math.Max(0,age);float R(int k)=>NativeHordeKinds.Hash(seed,90+k);float side=R(0)<.5f?-1:1;
            float push=dir.LengthSquared()>1e-4f?(float)Math.Atan2(dir.X,dir.Y):0;
            float recoil=Sm(a/.12f)*(1-Sm((a-.18f)/.2f));
            switch(kind)
            {
                case NativeHordeKinds.Runner:
                {
                    float fall=Sm((a-.05f)/.45f),slide=Sm((a-.1f)/.6f);
                    p.Position[CapturedRig.Body]=new Vector3(0,-.35f*fall-.33f*Sm((a-.35f)/.3f),.55f*slide);
                    p.Rotation[CapturedRig.Body]=CapturedRig.Euler(1.45f*fall,push*.2f,side*.2f*fall);
                    Limp(p,fall,1.2f,side);break;
                }
                case NativeHordeKinds.Brute:
                {
                    float reel=Sm(a/.3f),kneel=Sm((a-.25f)/.35f),crash=Sm((a-.75f)/.45f);
                    p.Position[CapturedRig.Body]=new Vector3(0,-.25f*kneel-.4f*crash,-.12f*reel-.35f*crash);
                    p.Rotation[CapturedRig.Body]=CapturedRig.Euler(-.25f*reel+.15f*kneel-1.25f*crash,push*.15f,side*.1f*crash);
                    p.Rotation[side>0?CapturedRig.LHip:CapturedRig.RHip]=CapturedRig.Euler(-1.3f*kneel*(1-crash*.6f));p.Rotation[side>0?CapturedRig.LKnee:CapturedRig.RKnee]=CapturedRig.Euler(1.8f*kneel*(1-crash*.4f));
                    p.Rotation[side>0?CapturedRig.RKnee:CapturedRig.LKnee]=CapturedRig.Euler(.9f*kneel);
                    p.Rotation[CapturedRig.LArm]=CapturedRig.Euler(-.4f*reel-1.4f*crash,0,.6f*crash);p.Rotation[CapturedRig.RArm]=CapturedRig.Euler(-.4f*reel-1.4f*crash,0,-.6f*crash);
                    p.Rotation[CapturedRig.Head]=CapturedRig.Euler(-.5f*reel+.6f*crash);break;
                }
                case NativeHordeKinds.Spitter:
                {
                    float choke=Sm(a/.35f)*(1-Sm((a-.5f)/.3f)),fall=Sm((a-.35f)/.55f);
                    p.Position[CapturedRig.Body]=new Vector3(side*.15f*fall,-.62f*fall,0);
                    p.Rotation[CapturedRig.Body]=CapturedRig.Euler(.4f*choke+.3f*fall,0,side*1.35f*fall);
                    p.Rotation[CapturedRig.Neck]=CapturedRig.Euler(-.6f*choke+.4f*fall);p.Rotation[CapturedRig.Head]=CapturedRig.Euler(-.5f*choke+(float)Math.Sin(a*40)*.06f*choke);
                    p.Rotation[CapturedRig.LArm]=CapturedRig.Euler(-1.5f*choke,0,.3f);p.Rotation[CapturedRig.RArm]=CapturedRig.Euler(-1.5f*choke,0,-.3f);p.Rotation[CapturedRig.LElbow]=p.Rotation[CapturedRig.RElbow]=CapturedRig.Euler(-1.6f*choke);
                    Limp(p,fall,.8f,side);break;
                }
                case NativeHordeKinds.Crawler:
                {
                    float flip=Sm((a-.05f)/.4f),curl=Sm((a-.3f)/.6f);
                    p.Position[CapturedRig.Body]=new Vector3(0,-.25f-.12f*flip,0);
                    p.Rotation[CapturedRig.Body]=CapturedRig.Euler(1.3f-2.9f*flip,push*.3f,side*.4f*flip);
                    for(int i=0;i<2;i++){int hip=i==0?CapturedRig.LHip:CapturedRig.RHip,arm=i==0?CapturedRig.LArm:CapturedRig.RArm;p.Rotation[hip]=CapturedRig.Euler(-1.4f*curl);p.Rotation[hip+1]=CapturedRig.Euler(2.1f*curl);p.Rotation[arm]=CapturedRig.Euler(-1.9f*curl,0,(i==0?1:-1)*.4f);p.Rotation[arm+1]=CapturedRig.Euler(-1.5f*curl);}
                    p.Rotation[CapturedRig.Neck]=CapturedRig.Euler(-.6f+.5f*curl);break;
                }
                case NativeHordeKinds.Bloater:
                {
                    float swell=Sm(a/BurstAt),shake=a<BurstAt?(float)Math.Sin(a*55)*.04f*swell:0,slump=Sm((a-BurstAt)/.5f);
                    p.Position[CapturedRig.Body]=new Vector3(shake,-.05f*swell-.5f*slump,0);
                    p.Rotation[CapturedRig.Body]=CapturedRig.Euler(-.25f*swell+.6f*slump,0,side*.2f*slump);
                    p.Rotation[CapturedRig.LArm]=CapturedRig.Euler(-.4f*swell,0,.9f*swell);p.Rotation[CapturedRig.RArm]=CapturedRig.Euler(-.4f*swell,0,-.9f*swell);
                    p.Rotation[CapturedRig.Head]=CapturedRig.Euler(-.4f*swell);
                    Limp(p,slump,1,side);break;
                }
                default:
                {
                    // The shambler: recoils off the blow, its knees buckle, it drops to them and pitches onto its face.
                    float buckle=Sm((a-.1f)/.35f),topple=Sm((a-.45f)/.45f);
                    p.Position[CapturedRig.Body]=new Vector3(0,-.38f*buckle-.3f*topple,-.06f*recoil+.25f*topple);
                    p.Rotation[CapturedRig.Body]=CapturedRig.Euler(-.3f*recoil+.25f*buckle+1.2f*topple,push*.25f,side*.15f*buckle);
                    p.Rotation[CapturedRig.LHip]=p.Rotation[CapturedRig.RHip]=CapturedRig.Euler(-1.4f*buckle*(1-topple*.7f));
                    p.Rotation[CapturedRig.LKnee]=p.Rotation[CapturedRig.RKnee]=CapturedRig.Euler(2.0f*buckle*(1-topple*.5f));
                    p.Rotation[CapturedRig.LFoot]=p.Rotation[CapturedRig.RFoot]=CapturedRig.Euler(-.6f*buckle);
                    p.Rotation[CapturedRig.LArm]=CapturedRig.Euler(-.3f*recoil-1.2f*topple,0,.4f*recoil);p.Rotation[CapturedRig.RArm]=CapturedRig.Euler(-.3f*recoil-1.0f*topple,0,-.5f*recoil);
                    p.Rotation[CapturedRig.Head]=CapturedRig.Euler(-.45f*recoil+.3f*topple,0,side*.3f*buckle);break;
                }
            }
            p.World();
        }
        /// <summary>When the bloater's body bursts after death (its parts hide; the burst's ink and gibs fly).</summary>
        public const float BurstAt=.55f;
        private static void Limp(CapturedRig.Pose p,float k,float spread,float side)
        {
            p.Rotation[CapturedRig.LArm]=Quaternion.Slerp(p.Rotation[CapturedRig.LArm],CapturedRig.Euler(-.4f,0,.9f*spread),k*.8f);p.Rotation[CapturedRig.RArm]=Quaternion.Slerp(p.Rotation[CapturedRig.RArm],CapturedRig.Euler(-.4f,0,-.9f*spread),k*.8f);
            p.Rotation[CapturedRig.LHip]=Quaternion.Slerp(p.Rotation[CapturedRig.LHip],CapturedRig.Euler(-.2f,0,.15f*side),k);p.Rotation[CapturedRig.RHip]=Quaternion.Slerp(p.Rotation[CapturedRig.RHip],CapturedRig.Euler(-.1f,0,-.1f),k);
            p.Rotation[CapturedRig.LKnee]=Quaternion.Slerp(p.Rotation[CapturedRig.LKnee],CapturedRig.Euler(.5f),k);p.Rotation[CapturedRig.RKnee]=Quaternion.Slerp(p.Rotation[CapturedRig.RKnee],CapturedRig.Euler(.3f),k);
        }
    }
}
