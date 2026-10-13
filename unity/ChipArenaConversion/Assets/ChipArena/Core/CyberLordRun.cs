using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The DEV Cyber Lord's run, sprint and second-jump takes (owner 2026-10-12: "Change the cyber lord running / sprint
    /// animation, I have this for reference [BIPER Runner Action Animation Pack]... Unless theres better ones in the pack we
    /// have. Give this class a double jump as well; Second jump has a cyber effect"): the owner's CLazy Runner pack
    /// (tools/export_pack_cyberlord_run.mjs, NativePackCyberLordRun.Generated). The run is the pack's Dash set in eight
    /// directions, the sprint its Boost set ahead and on the forward diagonals (the same fist pump driven deeper, so the
    /// sprint ramps without the arms changing their carry), the boost its Esc_BoostDash_Up_Fwd. Presentation only: pure
    /// numbers and lookups, never read by the authority.
    /// </summary>
    public static class CyberLordRunRules
    {
        /// <summary>Slots (NativePackCyberLordRun order): the run by direction from the facing, counter-clockwise (to the rig's left)
        /// in 45 degree steps, then the sprint ahead, forward-left and forward-right, then the boost.</summary>
        public const int Run=0,Sprint=8,SprintLeft=9,SprintRight=10,Boost=11,Clips=12,Directions=8;
        public static CapturedClip Clip(int i)=>i>=0&&i<NativePackCyberLordRun.Clips.Length?NativePackCyberLordRun.Clips[i]:null;
        /// <summary>
        /// The share of each take's per-stride swing kept about its cycle-mean carry: the torso (the spine, chest, neck, head,
        /// clavicles and hips), the right fist and the left fist. The voxel legs step at about 1.7 (run) and 1.5 (sprint)
        /// times the captured cadence, so the full pump would flail; the sprint keeps a little more of its drive. Off the
        /// forward line the torso keeps AsideTorso of its swing (a strafe's captured roll sat over hips turned the other way).
        /// The direction follows the travel at DirectionRate (1/s), as NativeLocoRules'.
        /// </summary>
        public const float TorsoKeep=.7f,ArmKeep=.68f,FreeKeep=.72f,SprintTorsoKeep=.8f,SprintArmKeep=.8f,AsideTorso=.35f,DirectionRate=NativeLocoRules.DirectionRate;
        /// <summary>The share of the takes' mean forward lean kept (the pack leans a sprinter's 40 degrees; the gait body adds its
        /// own run lean): the run and the sprint.</summary>
        public const float RunLean=1f,SprintLean=1f;
        /// <summary>The sprint take for a run direction: ahead and the two forward diagonals; -1 for any other (the authority
        /// sprints only within 53 degrees of the facing).</summary>
        public static int SprintFor(int direction)=>direction==0?Sprint:direction==1?SprintLeft:direction==7?SprintRight:-1;
    }

    /// <summary>
    /// The Cyber Lord's travel carry (CyberLordMotion.BaseRow): the two run takes bracketing the travel direction (from the
    /// facing) sampled at the gait's stride phase from their own left-foot strike and blended, and over them the sprint takes
    /// at the replicated sprint flag's eased ramp (NativeSprintRules' .15 s); each keeps only part of its per-stride swing
    /// (CyberLordRunRules) about its cycle mean, and a strafe's mean chest turn (relative to the forward take) is folded out of
    /// the spine, so with the procedural pelvis turning toward the travel and the ground pass's counter-turn the chest stays on
    /// the aim (the melee carry, NativeLocomotionCarry, folds the hips' heading instead). The legs stay the gait's planted procedural feet, whose stride
    /// is matched to the replicated speed (MeleeGait.CycleLength): no foot slides; the upper body, arms and lean are the
    /// pack's. No allocation per frame.
    /// </summary>
    public sealed class CyberLordRunCarry
    {
        private readonly float[] rowA=new float[CapturedClip.Channels],rowB=new float[CapturedClip.Channels],mean=new float[CapturedClip.Channels],meanB=new float[CapturedClip.Channels],
            sprintRow=new float[CapturedClip.Channels],sprintMean=new float[CapturedClip.Channels];
        private float dx,dz=1,last=-100,ramp;
        private static readonly float[] headings=new float[CyberLordRunRules.Clips];
        private static bool warmed;
        /// <summary>Decodes every take, its cycle mean and strafe heading once, so no direction allocates when first used.</summary>
        public CyberLordRunCarry()
        {
            if(warmed)return;
            for(int i=0;i<CyberLordRunRules.Clips;i++){var c=CyberLordRunRules.Clip(i);c.Decode();if(c.Loop)c.CycleMean();}
            for(int i=0;i<CyberLordRunRules.Boost;i++)headings[i]=-MeleeMath.Wrap(Chest(CyberLordRunRules.Clip(i))-Chest(CyberLordRunRules.Clip(i>=CyberLordRunRules.Sprint?CyberLordRunRules.Sprint:CyberLordRunRules.Run)));
            warmed=true;
        }
        /// <summary>The travel direction (radians from the facing, + to the rig's left), its bracketing run takes (0..7) and their
        /// blend, and the sprint weight the last Row used.</summary>
        public float Direction{get;private set;}public int First{get;private set;}public int Second{get;private set;}public float Mix{get;private set;}public float Sprint{get;private set;}
        private static Vector3 R(float[] f,int i)=>new Vector3(f[i],f[i+1],f[i+2]);
        private static Quaternion Rot(float[] f,int i){Vector3 v=R(f,i);float a=v.Length();return a<1e-9f||!(a<1e3f)?Quaternion.Identity:MeleeMath.AxisAngle(v/a,a);}
        private static void Write(float[] f,int i,Quaternion q)
        {
            q=Quaternion.Normalize(q);if(q.W<0)q=-q;float w=MeleeMath.Clamp(q.W,-1,1),s=(float)Math.Sqrt(Math.Max(0,1-w*w)),a=2*(float)Math.Acos(w),k=s<1e-7f?2:a/s;
            f[i]=q.X*k;f[i+1]=q.Y*k;f[i+2]=q.Z*k;
        }
        // A take's mean chest heading over its hips (the spine and chest's mean turn): the runtime's ground pass counter-turns the
        // procedural hips' own turn toward the travel, so the chest faces where these two joints turn it. (The Runner pack's
        // diagonals twist the chest up to 34 degrees off the aim over hips facing ahead; the melee carry's fold of the hips'
        // heading, NativeLocomotionCarry, would leave that twist in.)
        private static float Chest(CapturedClip c){float[] m=c.CycleMean();Vector3 f=Vector3.Transform(Vector3.UnitZ,Rot(m,MeleeCapture.Spine)*Rot(m,MeleeCapture.Chest));return (float)Math.Atan2(f.X,f.Z);}
        /// <summary>A take's strafe heading (rad; 0 ahead): the turn folded into its spine that brings its mean chest onto the aim.</summary>
        public static float Heading(int clip)=>clip>=0&&clip<CyberLordRunRules.Boost?headings[clip]:0;
        // One take at the stride phase and its cycle mean, the strafe heading folded into both spines.
        private static void Sample(int clip,float phase,float[] row,float[] avg)
        {
            var c=CyberLordRunRules.Clip(clip);c.Sample(c.Marks[0]+phase*c.Duration,row);Array.Copy(c.CycleMean(),avg,CapturedClip.Channels);
            float h=headings[clip];if(Math.Abs(h)<1e-4f)return;Quaternion turn=MeleeMath.AxisAngle(MeleeMath.Y,h);
            Write(row,MeleeCapture.Spine,turn*Rot(row,MeleeCapture.Spine));Write(avg,MeleeCapture.Spine,turn*Rot(avg,MeleeCapture.Spine));
        }
        private static void Damp(float[] f,float[] m,int from,int to,float keep){for(int c=from;c<to;c++)f[c]=m[c]+(f[c]-m[c])*keep;}
        // A family's row for two takes and their blend: damped about the blended mean, the mean lean kept at `lean`.
        private static void Pair(int a,int b,float t,float phase,float torso,float arms,float free,float lean,float[] row,float[] avg,float[] scratch,float[] scratchMean)
        {
            Sample(a,phase,row,avg);
            if(b>=0&&t>1e-4f){Sample(b,phase,scratch,scratchMean);MeleeCapture.BlendRowsTurning(row,scratch,t,row);MeleeCapture.BlendRows(avg,scratchMean,t,avg);}
            Damp(row,avg,0,MeleeCapture.Grip,torso);Damp(row,avg,MeleeCapture.Grip,MeleeCapture.LHand,arms);Damp(row,avg,MeleeCapture.LHand,CapturedClip.Channels,free);
            // The lean: the spine's and chest's forward bend eased toward upright by `lean` (their per-stride nod is kept).
            row[MeleeCapture.Spine]-=avg[MeleeCapture.Spine]*(1-lean);row[MeleeCapture.Chest]-=avg[MeleeCapture.Chest]*(1-lean);
        }
        /// <summary>
        /// The carry row for rig `r` (its stride and group yaw) and actor `a` (its velocity and sprint flag) at `time` into
        /// `into`. The sprint ramps in and out over NativeSprintRules.Blend while the flag shows a grounded sprint.
        /// </summary>
        public void Row(MeleeRig r,Actor a,float time,float[] into)
        {
            float dt=last<-90||time<last-MeleeGait.REWIND||time-last>.35f?-1:Math.Max(0,time-last);last=time;
            // The velocity in the rig's group frame (the source frame: +x the rig's left; Unity's x mirrored).
            float vx=-a.Velocity.x,vz=a.Velocity.z,cy=(float)Math.Cos(r.GroupYaw),sy=(float)Math.Sin(r.GroupYaw),lvx=vx*cy-vz*sy,lvz=vx*sy+vz*cy,speed=(float)Math.Sqrt(lvx*lvx+lvz*lvz);
            if(speed>.3f)
            {
                float ux=lvx/speed,uz=lvz/speed;
                if(dt<0){dx=ux;dz=uz;}else{float k=1-(float)Math.Exp(-dt*CyberLordRunRules.DirectionRate);dx+=(ux-dx)*k;dz+=(uz-dz)*k;}
            }
            float dir=(float)Math.Atan2(dx,dz);Direction=dir;NativeLocoRules.Bracket(dir,out int d0,out int d1,out float t);First=d0;Second=d1;Mix=t;
            float ahead=Math.Max(0,(float)Math.Cos(dir)),aside=MeleeMath.Lerp(CyberLordRunRules.AsideTorso,1,ahead*ahead*ahead),phase=MeleeMath.Frac(r.Stride/MeleeMath.TAU);
            Pair(CyberLordRunRules.Run+d0,CyberLordRunRules.Run+d1,t,phase,CyberLordRunRules.TorsoKeep*aside,CyberLordRunRules.ArmKeep,CyberLordRunRules.FreeKeep,CyberLordRunRules.RunLean,rowA,mean,rowB,meanB);
            bool on=NativeSprintRules.Shows(a.Sprinting,a.Grounded,speed);ramp=dt<0?(on?1:0):NativeSprintRules.Step(ramp,on,dt);float sw=NativeSprintRules.Shape(ramp);Sprint=sw;
            if(sw>1e-4f)
            {
                // The sprint's bracket: ahead and the forward diagonals (any other direction leans on the nearer diagonal).
                int s0,s1;float st;
                if(d0==0&&d1==1){s0=CyberLordRunRules.Sprint;s1=CyberLordRunRules.SprintLeft;st=t;}
                else if(d0==7&&d1==0){s0=CyberLordRunRules.SprintRight;s1=CyberLordRunRules.Sprint;st=t;}
                else{s0=dir>0?CyberLordRunRules.SprintLeft:CyberLordRunRules.SprintRight;s1=-1;st=0;}
                Pair(s0,s1,st,phase,CyberLordRunRules.SprintTorsoKeep*aside,CyberLordRunRules.SprintArmKeep,CyberLordRunRules.FreeKeep,CyberLordRunRules.SprintLean,sprintRow,sprintMean,rowB,meanB);
                MeleeCapture.BlendRowsTurning(rowA,sprintRow,sw,rowA);
            }
            Array.Copy(rowA,into,CapturedClip.Channels);
        }
        /// <summary>The carry's share shown this frame (CyberLordMotion.BaseRow's travel weight), read by the seat.</summary>
        public float Weight;
        // ---- the seat (the planted feet always reach; NativeRangedLocomotion.Seat's rule and tuning) ----
        private float seatDrop,seatTime=-100,seatPeak,seatBase;
        /// <summary>The hips lowered (m) the last Seat.</summary>
        public float SeatDrop=>seatDrop;
        /// <summary>
        /// After the ground pass (MeleeMotion.FighterFinish, MeleeMotion.FighterSeat): the gait plants each foot where it lands
        /// and lets the hips run over it, so late in a diagonal stance the planted ankle fell beyond the leg's reach and skated
        /// with the body (up to .9 m/s on a run diagonal, measured on the old route too). The hips drop just enough for every
        /// planted ankle to reach its gait target, rising back at most SeatRise, and both legs re-solve onto the gait's own
        /// targets: the contact foot stays still on the floor. Only while the carry shows, with the legs the gait's.
        /// </summary>
        public void Seat(MeleeRig r,MeleeGait gait,bool grounded,float time,bool skip)
        {
            float dt=seatTime<-90||time<seatTime-MeleeGait.REWIND||time-seatTime>.35f?-1:Math.Max(0,time-seatTime);seatTime=time;
            float need=0;bool own=!skip&&!r.LegsAuthored,live=own&&grounded&&Weight>0;
            if(!own){seatDrop=seatPeak=seatBase=0;return;}
            if(live)for(int i=0;i<2;i++)
            {
                bool left=i==0;if(!gait.Planted(left)||!gait.GaitFoot(left,out Vector3 t,out _,out _,out _))continue;
                Vector3 h=r.HipInGroup(left);float dx=t.X-h.X,dz=t.Z-h.Z,R2=NativeRangedLocoRules.SeatReach*NativeRangedLocoRules.SeatReach,flat=Math.Min(dx*dx+dz*dz,R2*.9f);
                need=Math.Max(need,(h.Y-t.Y)-(float)Math.Sqrt(R2-flat));
            }
            need=MeleeMath.Clamp(need,0,NativeRangedLocoRules.SeatMax)*Math.Min(1,Weight);
            if(dt<0){seatPeak=seatBase=need;}
            else{seatPeak=Math.Max(need,seatPeak*(float)Math.Exp(-dt/(Weight>.5f?NativeRangedLocoRules.SeatMemory:NativeRangedLocoRules.SeatForget)));seatBase+=MeleeMath.Clamp(seatPeak-seatBase,-NativeRangedLocoRules.SeatAdapt*dt,NativeRangedLocoRules.SeatAdapt*dt);}
            float want=Math.Max(need,seatBase);
            seatDrop=dt<0||want>=seatDrop?want:Math.Max(want,seatDrop-NativeRangedLocoRules.SeatRise*dt);
            if(seatDrop<=1e-5f)return;
            r.P[CapturedRig.Body]-=new Vector3(0,seatDrop,0);
            for(int i=0;i<2;i++){bool left=i==0;if(gait.GaitFoot(left,out Vector3 t,out float yaw,out float pitch,out float toe))r.SolveLeg(left,t,yaw,pitch,toe);}
        }
    }

    /// <summary>
    /// The Cyber Lord's second jump (the hardlight boost; owner 2026-10-12: "Give this class a double jump as well; Second jump
    /// has a cyber effect"), keyed only on the replicated air jump (Actor.AirJumps / AirJumpAt, read through the rig's
    /// NativeJumpTrack exactly as the dagger flip is: host, guests, killcam and spectators draw the same thing). The pack's
    /// Esc_BoostDash_Up_Fwd from just before its push-off: one leg stamps down on the hardlight hex as the body leaves it,
    /// then both knees tuck and the body leans into the rise, arms driving, and it eases back into the fall. The jump layer
    /// (NativeJumpMotion) takes its legs and hips, the Cyber Lord's style layer (CyberLordMotion) its torso and fists; the
    /// effect is NativeCyberLordFx's (NativeCyberLordAirBoostFx.cs). Until the authority grants the Cyber Lord an air jump it
    /// never fires in a match; the Dev Sandbox previews it (PreviewAt). Presentation only.
    /// </summary>
    public static class CyberLordAirBoost
    {
        /// <summary>Whether `spec`'s air jump plays the boost (the daggers flip instead).</summary>
        public static bool Handles(string spec)=>spec==CyberLordSpec.Id;
        public static CapturedClip Clip=>CyberLordRunRules.Clip(CyberLordRunRules.Boost);
        /// <summary>The timing (s after the air jump): the take enters Lead before its push-off, reaches the push-off at Push and
        /// its full tuck at Tuck, holds it to Hold and has blended back into the jump layer's fall by Seconds.</summary>
        public const float Lead=.04f,Push=.05f,Tuck=.3f,Hold=.38f,Seconds=.6f,In=.08f;
        /// <summary>The share of the take's lean and fist drive the style layer keeps over the stance (the voxel torso is two
        /// blocks; the pack's 40 degree fold reads as a collapse on it).</summary>
        public const float Torso=.8f,Arms=.9f;
        /// <summary>The first-person view model's share of the boost's fists (gentler: the view's own hop carries the body).</summary>
        public const float FirstPerson=.25f;
        /// <summary>The take time at `age` s after the air jump (monotone: the lead-in to the push-off, then on to the tuck, held).</summary>
        public static float TakeTime(float age)
        {
            var c=Clip;float push=c.Marks[1],end=c.Marks[2];
            if(age<=0)return push-Lead;if(age<Push)return push-Lead+Lead*(age/Push);
            if(age<Tuck)return push+(end-push)*MeleeMath.Smooth((age-Push)/(Tuck-Push));return end;
        }
        /// <summary>The boost's weight at `age` (0 outside it): in over In, held, out from Hold to Seconds.</summary>
        public static float Weight(float age)=>!(age>=0)||age>=Seconds?0:MeleeMath.Ease(age/In)*(1-MeleeMath.Ease((age-Hold)/(Seconds-Hold)));
        /// <summary>The boost's age on a rig's jump track at `time` (NaN: none live): an air jump while still airborne.</summary>
        public static float Age(NativeJumpTrack t,float time)=>t!=null&&t.Airborne&&t.AirJumps>0&&t.AirJumpAt>-90&&time>=t.AirJumpAt&&time-t.AirJumpAt<Seconds?time-t.AirJumpAt:float.NaN;
        /// <summary>The replicated air-jump signal for `a` (the jump layer's MeleeView.AirJumpAt rule), or the Dev Sandbox preview's
        /// stand-in time while the authority has granted none.</summary>
        public static float Signal(Actor a,float preview=-100)=>a==null?-100:a.AirJumps>0?a.AirJumpAt:preview;
        /// <summary>The Dev Sandbox preview (Settings never; the sandbox's "Preview Cyber Double Jump"): the local pilot's own jump
        /// shows the boost PreviewDelay after its takeoff, as if the authority had granted the air jump there (no impulse: the
        /// arc stays the single jump's). -100 while there is none to show (and once a track has taken it: the signal is one-shot).</summary>
        public const float PreviewDelay=.3f;
        public static float PreviewAt(NativeJumpTrack t,float time)=>t!=null&&t.Airborne&&t.Jumped&&t.AirJumps==0&&t.AirAt>-90&&time>=t.AirAt+PreviewDelay?t.AirAt+PreviewDelay:-100;
    }
}
