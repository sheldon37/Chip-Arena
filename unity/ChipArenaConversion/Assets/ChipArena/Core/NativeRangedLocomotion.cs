using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The gun and magic routes' travel carry from the owner's Explosive pack (owner 2026-10-12: "Give both ranged and magic
    /// different run/sprint animations, theirs are currently too basic"): per carry the slow walk, the walk with its seven
    /// strafes, the run in eight directions and the Unarmed sprint (tools/export_pack_ranged_locomotion.mjs,
    /// NativePackRangedLocomotion.Generated), with a style per weapon and school. Presentation only.
    /// </summary>
    public static class NativeRangedLocoRules
    {
        /// <summary>The carries: the pistol (Armed walk, Unarmed run), the long guns (2Hand-Shooting) and the staff (2Hand-Staff,
        /// mirrored onto the game's left-hand staff).</summary>
        public const int None=-1,Pistol=0,Shooting=1,Staff=2;
        public static int SetFor(string spec)=>spec=="pistol"?Pistol:spec=="rifle"||spec=="machinegun"?Shooting:MeleeStance.MagicSpec(spec)?Staff:None;
        public static CapturedClip Clip(int set,int slot)
        {
            if(set<0||set>=NativePackRangedLocomotion.Sets.Length)return null;var s=NativePackRangedLocomotion.Sets[set];return slot>=0&&slot<s.Length?s[slot]:null;
        }
        /// <summary>
        /// One weapon's (or school's) travel character. Torso: the share of each clip's per-stride spine, chest, neck and head
        /// swing kept about its cycle-mean carry at a walk and at a run (the voxel legs step at about 2.6 times the capture's
        /// cadence), the run's lean over the gait body's own (rad: the clips' mean chest is turned onto it; the pack's deep run
        /// hunch once buried the rifle muzzle), the share of the pack's neck and head (the gaze stays the gait's). Hands: the free
        /// hand's and the weapon or staff hand's per-stride keep. Body: a run crouch and a heavy step's extra dip at the step
        /// rate (m, both downward so the planted feet still reach), and a stride roll (rad). Aim: the share of the torso's stride
        /// swing kept under a raised gun (the muzzle stays on the aim: the hold orients the gun in the world). Staff: the share
        /// of the pack staff's tilt the left-hand staff follows.
        /// </summary>
        public struct Style{public float TorsoWalk,TorsoRun,Lean,HeadKeep,FreeKeep,HandKeep,Crouch,Bob,Roll,AimSway,StaffFollow;}
        public static Style StyleOf(string spec)
        {
            switch(spec)
            {
                // light and agile: a lively torso, the free arm pumping, the pistol tucked in a compact low ready
                case "pistol":return new Style{TorsoWalk=.4f,TorsoRun=.32f,Lean=.1f,HeadKeep=.5f,FreeKeep=.4f,HandKeep=.22f,AimSway=.7f};
                // steady and tactical: the quietest torso and hands, a slight crouch, the barrel level behind the stride
                case "rifle":return new Style{TorsoWalk=.3f,TorsoRun=.18f,Lean=.03f,HeadKeep=.35f,FreeKeep=.12f,HandKeep=.12f,Crouch=.012f,AimSway=.35f};
                // heavy and braced: a lower, rolling, dipping stride under the weight, the gun at the hip
                case "machinegun":return new Style{TorsoWalk=.3f,TorsoRun=.3f,Lean=-.03f,HeadKeep=.4f,FreeKeep=.2f,HandKeep=.22f,Crouch=.035f,Bob=.014f,Roll=.025f,AimSway=.45f};
                // magic: flowing, the staff riding steady in the left hand, the casting hand swinging soft and wide
                case "fire":return new Style{TorsoWalk=.3f,TorsoRun=.15f,Lean=.14f,HeadKeep=.5f,FreeKeep=.38f,HandKeep=.22f,StaffFollow=.3f};
                case "water":return new Style{TorsoWalk=.3f,TorsoRun=.16f,Lean=.05f,HeadKeep=.5f,FreeKeep=.5f,HandKeep=.3f,Roll=.012f,StaffFollow=.15f};
                case "lightning":return new Style{TorsoWalk=.28f,TorsoRun=.15f,Lean=.09f,HeadKeep=.45f,FreeKeep=.3f,HandKeep=.2f,StaffFollow=.35f};
                case "darkness":return new Style{TorsoWalk=.3f,TorsoRun=.15f,Lean=.2f,HeadKeep=.4f,FreeKeep=.32f,HandKeep=.18f,Crouch=.03f,StaffFollow=.1f};
                case "leet":return new Style{TorsoWalk=.3f,TorsoRun=.15f,Lean=.08f,HeadKeep=.5f,FreeKeep=.4f,HandKeep=.22f,StaffFollow=.25f};
                default:return new Style{TorsoWalk=.4f,TorsoRun=.3f,HeadKeep=.5f,FreeKeep=.4f,HandKeep=.2f,AimSway=.5f,StaffFollow=.5f};
            }
        }
        /// <summary>The run carries in chest space (x the rig's left, y up, z forward) for a lowered gun: the gun hand's grip and
        /// the gun's orientation (CapturedRig.Euler: + x tips the muzzle down, + y turns it toward the rig's left, z cants it).
        /// The pistol tucked low in front of the belly, the rifle at a low ready with the muzzle down the line of travel, the
        /// machine gun slung at the hip.</summary>
        public static readonly Vector3 PistolGrip=new Vector3(-.13f,-.2f,.22f),RifleGrip=new Vector3(-.2f,-.04f,.24f),MachineGunGrip=new Vector3(-.24f,-.11f,.2f);
        public static readonly Vector3 PistolCarry=new Vector3(.75f,.22f,-.1f),RifleCarry=new Vector3(.36f,.3f,-.18f),MachineGunCarry=new Vector3(.1f,.32f,-.42f);
        /// <summary>
        /// The first-person run bob per weapon over the source view layer's (NativeViewSway; within the camera-shake setting):
        /// gains on its side and lift bob, a roll and a nod at the stride (rad) and a slow side wave at half the stride (m). The
        /// pistol light and quick, the rifle steady, the machine gun heavy and dipping, water flowing, darkness low and quiet;
        /// every other weapon keeps the source's bob.
        /// </summary>
        public static void ViewRun(string spec,out float side,out float lift,out float roll,out float nod,out float wave)
        {
            side=lift=1;roll=nod=wave=0;
            switch(spec)
            {
                case "pistol":side=1.35f;lift=1.1f;roll=.012f;break;
                case "rifle":side=.6f;lift=.6f;break;
                case "machinegun":side=.9f;lift=1.6f;nod=.018f;break;
                case "fire":side=1.1f;lift=1.1f;roll=.01f;break;
                case "water":side=.7f;lift=.55f;wave=.012f;break;
                case "lightning":side=1.1f;break;
                case "darkness":side=.8f;lift=.6f;break;
            }
        }
        /// <summary>The walk-to-run blend (MeleeMotion's: 3.2 to 6 m/s), the move weight's ramp over the gait's motion and its
        /// fastest blend (s), and the air blend (s): the carry eases out of a takeoff and back in on landing. The slow walk
        /// under 2.4 m/s ahead and the travel direction's rate are NativeLocoRules'.</summary>
        public const float RunFrom=3.2f,RunSpan=2.8f,MoveRamp=.3f,MoveBlend=.2f,AirBlend=.2f;
        /// <summary>The pelvis counter of the steady torso (MeleeMotion: the spine counters most of the gait's pelvis twist and roll).</summary>
        public const float PelvisTwistCounter=.75f,PelvisRollCounter=.8f;
        /// <summary>The share of the gait's head orientation restored over the pack's (the voxel head is a big block: the
        /// capture's run nod and roll tipped the gaze).</summary>
        public const float GazeKeep=.8f;
        /// <summary>The hands' follow of the pack carry (rad/s, a critically damped spring: about a 35 ms lag).</summary>
        public const float HandFollow=30;
        /// <summary>The share of the staff set's casting-hand line kept out to the side (the rest comes in over the body).</summary>
        public const float CastIn=.72f;
        /// <summary>The share of the chest's forward lean the staff stands back up against while travelling.</summary>
        public const float StaffUpright=.6f;
        /// <summary>The seat (NativeRangedLocomotion.Seat): the leg's reach it keeps every planted ankle within (MeleeRig's .485
        /// less a margin), the most it lowers the hips (m), its rise rate (m/s: a foot lifting never pops the hips back up;
        /// lowering stays immediate, so the planted ankles always reach), and the anticipation: the recent peak deficit's
        /// memory (s; SeatForget once the travel carry fades) and the rate (m/s) the settled crouch follows it.</summary>
        public const float SeatReach=.484f,SeatMax=.1f,SeatRise=.3f,SeatMemory=2f,SeatForget=.25f,SeatAdapt=.12f;
        /// <summary>The share of the run's torso keep the sprint clip keeps (its stride is the gait's fastest).</summary>
        public const float SprintTorso=.6f;
    }

    /// <summary>
    /// Lays the pack's directional walk, run and sprint carries over a gun or magic pilot's gait (Core/GunMotion,
    /// MagicMotion), in place of the gait's procedural torso and arm swing: the two clips bracketing the travel direction
    /// (from the facing) are sampled at the gait's stride phase from their own left-foot strike and blended, the slow walk
    /// under 2.4 m/s ahead (the two directions' blend eased), the sprint clip at the sprint weight; each keeps only part of
    /// its per-stride swing (the weapon's Style), a strafe's mean hip turn folded into the spine, and the clips' mean chest is
    /// turned onto the gait body's line plus the style's lean, so the chest faces the aim; the head turns back toward the
    /// gait's gaze; the hands follow the pack carry through a short spring. The legs stay the gait's planted procedural feet;
    /// the body only ever drops (a crouch, a heavy step, the reach seat), so the ground pass re-solves the same ankles and the
    /// planted ones always reach (Seat). A raised gun keeps the native hold (the torso aimed, the gun oriented on the aim in
    /// the world) with a share of the stride swing re-added about it; a lowered gun rides the pack's grip motion in its
    /// weapon's carry (fed to the hold's lowered carry); the staff rides the pack's staff hand and tilt, the casting hand its
    /// free hand. The weights follow the gait's motion, the ground and the aim on eased ramps, so every transition is
    /// continuous. No allocation per frame.
    /// </summary>
    public sealed class NativeRangedLocomotion
    {
        private readonly float[] rowA=new float[CapturedClip.Channels],rowB=new float[CapturedClip.Channels],mean=new float[CapturedClip.Channels],meanB=new float[CapturedClip.Channels];
        private float dx,dz=1,last=-100,speedV,asideKeep=1,ground=1,move;private bool speedHas;
        private static readonly float[][][] headings=new float[NativePackRangedLocomotion.Sets.Length][][];
        private static bool warmed;
        public NativeRangedLocomotion()
        {
            if(warmed)return;
            for(int s=0;s<NativePackRangedLocomotion.Sets.Length;s++)for(int k=0;k<NativePackRangedLocomotion.Slots.Length;k++){NativeRangedLocoRules.Clip(s,k).Decode();Heading(s,k);}
            NativePackRangedLocomotion.Sprint.Decode();NativePackRangedLocomotion.Sprint.CycleMean();warmed=true;
        }
        /// <summary>The travel direction (radians from the facing, + to the rig's left) the last frame blended, its two
        /// directions and their blend, the move, run and sprint weights shown.</summary>
        public float Direction{get;private set;}public int First{get;private set;}public int Second{get;private set;}public float Mix{get;private set;}
        public float Weight{get;private set;}public float Run{get;private set;}public float Sprint{get;private set;}
        /// <summary>The staff's tilt toward the pack's staff (rad, + its top forward; MagicPose.Staff), set by Magic.</summary>
        public float StaffTilt{get;private set;}
        // ---- the frame's blended pack carry (chest space) ----
        // right: the rig's right hand (the gun hand; the casting hand on the mirrored staff set), left: its left (the pistol's
        // free hand, the long guns' support hand, the staff hand), each damped about its cycle mean; leftFwd the left fist's
        // weapon axis (the staff, NativePackRangedLocomotion's staff set).
        private Vector3 right,rightMean,left,leftMean,leftFwd;private Quaternion rightQ=Quaternion.Identity,leftQ=Quaternion.Identity;
        private Vector3 rightS,rightV,leftS,leftV,fwdS,fwdV;private bool followHas;
        private void Follow(ref Vector3 x,ref Vector3 v,Vector3 target,float dt)
        {
            if(!followHas||dt<0){x=target;v=Vector3.Zero;return;}
            float h=Math.Min(dt,.05f),w=NativeRangedLocoRules.HandFollow;v=(v-(x-target)*w*w*h)/(1+2*w*h+w*w*h*h);x+=v*h;
        }
        private Quaternion swingSpine=Quaternion.Identity,swingChest=Quaternion.Identity,meanSpine=Quaternion.Identity,meanChest=Quaternion.Identity;
        private static Vector3 Row(float[] f,int i)=>new Vector3(f[i],f[i+1],f[i+2]);
        private static Quaternion Rot(float[] f,int i){Vector3 v=Row(f,i);float a=v.Length();return a<1e-9f||!(a<1e3f)?Quaternion.Identity:MeleeMath.AxisAngle(v/a,a);}
        private static void Write(float[] f,int i,Quaternion q)
        {
            q=Quaternion.Normalize(q);if(q.W<0)q=-q;float w=MeleeMath.Clamp(q.W,-1,1),s=(float)Math.Sqrt(Math.Max(0,1-w*w)),a=2*(float)Math.Acos(w),k=s<1e-7f?2:a/s;
            f[i]=q.X*k;f[i+1]=q.Y*k;f[i+2]=q.Z*k;
        }
        private static Quaternion Frame(Vector3 fwd,Vector3 up)
        {
            fwd=fwd.LengthSquared()<1e-8f?Vector3.UnitZ:Vector3.Normalize(fwd);up-=fwd*Vector3.Dot(up,fwd);up=up.LengthSquared()<1e-8f?Vector3.UnitY:Vector3.Normalize(up);
            return MeleeMath.Basis(Vector3.Cross(up,fwd),up,fwd);
        }
        // A clip's mean hip heading relative to its family's forward clip (NativeLocomotionCarry).
        private static float Heading(int set,int slot)
        {
            var h=headings[set]??=new float[NativePackRangedLocomotion.Slots.Length][];
            if(h[slot]==null)
            {
                float Mean(CapturedClip c){float[] m=c.CycleMean();Vector3 fwd=Vector3.Transform(Vector3.UnitZ,MeleeMath.AxisAngle(MeleeMath.Y,m[MeleeCapture.Yaw])*Rot(m,MeleeCapture.Pelvis));return (float)Math.Atan2(fwd.X,fwd.Z);}
                int forward=slot>=NativeLocoRules.Run?NativeLocoRules.Run:slot>=NativeLocoRules.Walk?NativeLocoRules.Walk:NativeLocoRules.Slow;
                h[slot]=new[]{MeleeMath.Wrap(Mean(NativeRangedLocoRules.Clip(set,slot))-Mean(NativeRangedLocoRules.Clip(set,forward)))};
            }
            return h[slot][0];
        }
        private static void Sample(CapturedClip c,float heading,float phase,float[] row,float[] avg)
        {
            c.Sample(c.Marks[0]+phase*c.Duration,row);Array.Copy(c.CycleMean(),avg,CapturedClip.Channels);
            if(Math.Abs(heading)<1e-4f)return;Quaternion turn=MeleeMath.AxisAngle(MeleeMath.Y,heading);
            Write(row,MeleeCapture.Spine,turn*Rot(row,MeleeCapture.Spine));Write(avg,MeleeCapture.Spine,turn*Rot(avg,MeleeCapture.Spine));
        }
        private static void Damp(float[] f,float[] m,int from,int to,float keep){for(int c=from;c<to;c++)f[c]=m[c]+(f[c]-m[c])*keep;}
        // A family's row for the travel direction (the two bracketing clips, the slow walk under SlowTo ahead), its torso damped.
        private void Family(int set,int first,float phase,int a,int b,float t,float slow,float torsoKeep)
        {
            Sample(NativeRangedLocoRules.Clip(set,first+a),Heading(set,first+a),phase,rowA,mean);
            if(t>1e-4f){Sample(NativeRangedLocoRules.Clip(set,first+b),Heading(set,first+b),phase,rowB,meanB);MeleeCapture.BlendRowsTurning(rowA,rowB,t,rowA);MeleeCapture.BlendRows(mean,meanB,t,mean);}
            if(slow>1e-4f){Sample(NativeRangedLocoRules.Clip(set,NativeLocoRules.Slow),0,phase,rowB,meanB);MeleeCapture.BlendRowsTurning(rowA,rowB,slow,rowA);MeleeCapture.BlendRows(mean,meanB,slow,mean);}
            Damp(rowA,mean,MeleeCapture.Spine,MeleeCapture.Head+3,torsoKeep);
        }
        private void SprintRow(float phase,float torsoKeep)
        {
            Sample(NativePackRangedLocomotion.Sprint,0,phase,rowA,mean);Damp(rowA,mean,MeleeCapture.Spine,MeleeCapture.Head+3,torsoKeep);
        }
        private static readonly int[] TorsoBones={CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LClav,CapturedRig.RClav};
        private static readonly int[] TorsoChannels={MeleeCapture.Spine,MeleeCapture.Chest,MeleeCapture.Neck,MeleeCapture.Head,MeleeCapture.LClav,MeleeCapture.RClav};
        // The row's torso over the rig at `w` (the neck and head at the style's share), its hands accumulated at `w` into the
        // frame's carry, and its spine and chest swing about the cycle mean (for the aim) mixed in at `w`.
        private void Lay(MeleeRig r,float w,in NativeRangedLocoRules.Style st,float rightKeep,float leftKeep,bool hands=true)
        {
            if(w<=1e-4f)return;
            for(int i=0;i<TorsoBones.Length;i++)r.Slerp(TorsoBones[i],Rot(rowA,TorsoChannels[i]),i==2||i==3?w*st.HeadKeep:w);
            swingSpine=MeleeMath.Slerp(swingSpine,Rot(rowA,MeleeCapture.Spine)*MeleeMath.Inverse(Rot(mean,MeleeCapture.Spine)),w);
            swingChest=MeleeMath.Slerp(swingChest,Rot(rowA,MeleeCapture.Chest)*MeleeMath.Inverse(Rot(mean,MeleeCapture.Chest)),w);
            meanSpine=MeleeMath.Slerp(meanSpine,Rot(mean,MeleeCapture.Spine),w);meanChest=MeleeMath.Slerp(meanChest,Rot(mean,MeleeCapture.Chest),w);
            if(!hands)return;
            Vector3 g=Row(rowA,MeleeCapture.Grip),gm=Row(mean,MeleeCapture.Grip),l=Row(rowA,MeleeCapture.LHand),lm=Row(mean,MeleeCapture.LHand);
            right=Vector3.Lerp(right,gm+(g-gm)*rightKeep,w);rightMean=Vector3.Lerp(rightMean,gm,w);
            left=Vector3.Lerp(left,lm+(l-lm)*leftKeep,w);leftMean=Vector3.Lerp(leftMean,lm,w);
            Vector3 blade=Row(rowA,MeleeCapture.Blade),edge=Row(rowA,MeleeCapture.Edge);edge-=blade*Vector3.Dot(edge,blade);
            Quaternion gq=edge.LengthSquared()<1e-8f?Quaternion.Identity:MeleeMath.Basis(Vector3.Normalize(edge),Vector3.Cross(Vector3.Normalize(blade),Vector3.Normalize(edge)),Vector3.Normalize(blade));
            rightQ=MeleeMath.Slerp(rightQ,gq,w);leftQ=MeleeMath.Slerp(leftQ,Frame(Row(rowA,MeleeCapture.LFwd),Row(rowA,MeleeCapture.LUp)),w);
            leftFwd=Vector3.Lerp(leftFwd,Row(mean,MeleeCapture.LFwd)+(Row(rowA,MeleeCapture.LFwd)-Row(mean,MeleeCapture.LFwd))*leftKeep,w);
        }
        /// <summary>
        /// The pack carry over the gait's torso (and the frame's hand carry) for a gun or magic pilot: `v` the route's view
        /// (its velocity in the source frame), `sprint` the sprint carry's weight (NativeSprintMotion.Lean). Returns the move
        /// weight shown (0: the route keeps its gait). Call after the gait and the sprint lean, before the gun hold or magic layer.
        /// </summary>
        private float Torso(MeleeRig r,MeleeView v,string spec,float time,float sprint)
        {
            int s=NativeRangedLocoRules.SetFor(spec);Weight=Run=Sprint=0;swingSpine=swingChest=meanSpine=meanChest=Quaternion.Identity;
            float dt=last<-90||time<last-MeleeGait.REWIND||time-last>.35f?-1:Math.Max(0,time-last);last=time;
            float cy=(float)Math.Cos(r.GroupYaw),sy=(float)Math.Sin(r.GroupYaw),lvx=v.Vel.X*cy-v.Vel.Z*sy,lvz=v.Vel.X*sy+v.Vel.Z*cy,raw=(float)Math.Sqrt(lvx*lvx+lvz*lvz);
            if(!speedHas||dt<0){speedHas=true;speedV=raw;}else speedV+=(raw-speedV)*(1-(float)Math.Exp(-dt/.25f));
            if(raw>.3f)
            {
                float ux=lvx/raw,uz=lvz/raw;
                if(dt<0){dx=ux;dz=uz;}else{float k=1-(float)Math.Exp(-dt*NativeLocoRules.DirectionRate);dx+=(ux-dx)*k;dz+=(uz-dz)*k;}
            }
            // Off the ground the carry eases out (and back in on landing) on its own smoothstep ramp: the gait's ground weight
            // falls fastest in its first frame, which jerked the arms from the carry toward the jump's air pose.
            float on=v.Grounded?1:0;ground=dt<0?on:on>ground?Math.Min(on,ground+dt/NativeRangedLocoRules.AirBlend):Math.Max(on,ground-dt/NativeRangedLocoRules.AirBlend);
            // The move weight follows the gait's motion, at most 1/MoveBlend a second, eased: a stun's or a roll's release
            // regains the gait's motion within a few frames, which snapped the carry back on.
            float to=s<0?0:MeleeMath.Clamp(r.Motion/NativeRangedLocoRules.MoveRamp,0,1);move=dt<0?to:to>move?Math.Min(to,move+dt/NativeRangedLocoRules.MoveBlend):Math.Max(to,move-dt/NativeRangedLocoRules.MoveBlend);
            float w=MeleeMath.Ease(move)*NativeSprintRules.Shape(ground);
            if(w<=1e-3f){followHas=false;return 0;}
            var st=NativeRangedLocoRules.StyleOf(spec);
            float dir=(float)Math.Atan2(dx,dz),run=MeleeMath.Ease((speedV-NativeRangedLocoRules.RunFrom)/NativeRangedLocoRules.RunSpan),phase=MeleeMath.Frac(r.Stride/MeleeMath.TAU);
            // The two directions' blend eases at each of the eight (smoothstep): a turn through them never changes the carry's
            // pace in a frame where one neighbour's clip sits further off than the other's.
            NativeLocoRules.Bracket(dir,out int a,out int b,out float t);t=t*t*(3-2*t);Direction=dir;First=a;Second=b;Mix=t;
            float slow=(1-MeleeMath.Ease((speedV-NativeLocoRules.SlowFrom)/(NativeLocoRules.SlowTo-NativeLocoRules.SlowFrom)))*Math.Max(0,(float)Math.Cos(dir));
            float ahead=Math.Max(0,(float)Math.Cos(dir)),aside=MeleeMath.Lerp(NativeLocoRules.AsideTorso,1,ahead*ahead*ahead);sprint=MeleeMath.Clamp(sprint,0,1);asideKeep=aside;
            Vector3 pelvis=r.E[CapturedRig.Pelvis];right=rightMean=left=leftMean=leftFwd=Vector3.Zero;rightQ=leftQ=Quaternion.Identity;Quaternion gaze=r.ChainQuat(CapturedRig.Head,-1);
            // The weapon hand keeps HandKeep and the free hand FreeKeep; on the mirrored staff set the staff is the left hand.
            bool staff=s==NativeRangedLocoRules.Staff;float rk=staff?st.FreeKeep:st.HandKeep,lk=staff?st.HandKeep:st.FreeKeep;
            Family(s,NativeLocoRules.Walk,phase,a,b,t,slow,st.TorsoWalk*aside);Lay(r,w,st,rk,lk);
            if(run>1e-3f){Family(s,NativeLocoRules.Run,phase,a,b,t,0,st.TorsoRun*aside);Lay(r,w*run,st,rk,lk);}
            // The sprint: the pack's Unarmed sprint torso, its cadence and lean (NativeSprintMotion lays each weapon's arms over it).
            if(sprint>1e-3f){SprintRow(phase,st.TorsoRun*NativeRangedLocoRules.SprintTorso);Lay(r,w*sprint,st,0,0,false);}
            Weight=w;Run=run;Sprint=sprint;
            // The hands follow the blended carry through a critically damped spring (HandFollow): the capture's sharp beats
            // (a walking stick's plant), run at the gait's faster cadence, become a flowing carry instead of a corner.
            Follow(ref rightS,ref rightV,right,dt);Follow(ref leftS,ref leftV,left,dt);Follow(ref fwdS,ref fwdV,leftFwd,dt);right=rightS;left=leftS;leftFwd=fwdS;followHas=true;
            // The steady torso (MeleeMotion.SteadyTorso): the spine counters most of the gait's pelvis twist and roll.
            float r1=w*Math.Max(run,sprint);
            r.Premultiply(CapturedRig.Spine,MeleeMath.AxisAngle(MeleeMath.Z,-NativeRangedLocoRules.PelvisRollCounter*pelvis.Z*w)*MeleeMath.AxisAngle(MeleeMath.Y,-NativeRangedLocoRules.PelvisTwistCounter*pelvis.Y*w));
            // The posture: the clips' cycle-mean chest (the capture's deep run hunch and its bladed, rolled carries, which buried a
            // muzzle and turned the chest off the aim) is turned onto the gait body's own line plus the style's lean, about the
            // spine, so only the pack's per-stride swing, its spine-to-chest bend and its shoulders remain over the facing.
            Quaternion body=r.Q[CapturedRig.Body],bp=body*r.Q[CapturedRig.Pelvis],fix=body*MeleeMath.AxisAngle(MeleeMath.X,st.Lean*r1)*MeleeMath.Inverse(body*meanSpine*meanChest);
            r.SetQ(CapturedRig.Spine,MeleeMath.Inverse(bp)*fix*bp*r.Q[CapturedRig.Spine]);
            // The style's stride roll, then the gaze: the head turned back toward the gait's level, aimed look.
            if(st.Roll>0)r.Premultiply(CapturedRig.Chest,MeleeMath.AxisAngle(MeleeMath.Z,st.Roll*r1*(float)Math.Sin(r.Stride)));
            r.SetQ(CapturedRig.Head,MeleeMath.Inverse(r.ChainQuat(CapturedRig.Neck,-1))*MeleeMath.Slerp(r.ChainQuat(CapturedRig.Head,-1),gaze,NativeRangedLocoRules.GazeKeep*w));
            // The body's run crouch and a heavy step's dip (downward only: the planted feet still reach).
            float drop=st.Crouch*r1+st.Bob*r1*(.5f+.5f*(float)Math.Cos(2*r.Stride));if(drop>0)r.P[CapturedRig.Body]-=new Vector3(0,drop,0);
            return w;
        }
        /// <summary>
        /// GunMotion.Sample, after the gait and the sprint lean and before the gun hold: the pack torso; the pistol's arms (the
        /// free arm's pump, the gun hand tucked in its low ready with the pack's fist motion), kept by RangedPose.Arms while the
        /// pistol is lowered. Returns the move weight.
        /// </summary>
        public float Gun(MeleeRig r,MeleeView v,string spec,float time,float sprint)
        {
            float w=Torso(r,v,spec,time,sprint);if(w<=0||spec!="pistol")return w;
            r.Hand(true,left,leftQ,w);
            var c=NativeRangedLocoRules.PistolCarry;r.Hand(false,NativeRangedLocoRules.PistolGrip+(right-rightMean),CapturedRig.Euler(c.X,c.Y,c.Z),w);
            return w;
        }
        // ---- the seat (the planted feet always reach) ----
        private float seatDrop,seatTime=-100,seatPeak,seatBase;
        /// <summary>The hips lowered (m) the last Seat.</summary>
        public float SeatDrop=>seatDrop;
        /// <summary>
        /// After the ground pass (GunMotion, MagicMotion.Finish): the gait plants each foot where it lands and lets the hips run
        /// over it, so late in a long diagonal or strafing stance the planted ankle fell beyond the leg's reach and skated with
        /// the body (up to .7 m/s on a walking strafe). The hips drop just enough for every planted ankle to reach its gait target
        /// (the knees bend a little deeper), rising back at most SeatRise, and both legs re-solve onto the gait's own targets:
        /// the contact foot stays still on the floor. Only while this carry shows, with the legs the gait's (it eases out in
        /// the air).
        /// </summary>
        public void Seat(MeleeRig r,MeleeGait gait,bool grounded,float time,bool skip)
        {
            float dt=seatTime<-90||time<seatTime-MeleeGait.REWIND||time-seatTime>.35f?-1:Math.Max(0,time-seatTime);seatTime=time;
            // The seat holds while the gait owns the legs (easing out once the carry fades or the pilot leaves the ground); a roll,
            // a knockdown or a layer's own legs drop it (their transitions blend the pose).
            float need=0;bool own=!skip&&!r.LegsAuthored,live=own&&grounded&&Weight>0;
            if(!own){seatDrop=seatPeak=seatBase=0;return;}
            if(live)for(int i=0;i<2;i++)
            {
                bool left=i==0;if(!gait.Planted(left)||!gait.GaitFoot(left,out Vector3 t,out _,out _,out _))continue;
                Vector3 h=r.HipInGroup(left);float dx=t.X-h.X,dz=t.Z-h.Z,R2=NativeRangedLocoRules.SeatReach*NativeRangedLocoRules.SeatReach,flat=Math.Min(dx*dx+dz*dz,R2*.9f);
                need=Math.Max(need,(h.Y-t.Y)-(float)Math.Sqrt(R2-flat));
            }
            need=MeleeMath.Clamp(need,0,NativeRangedLocoRules.SeatMax)*Weight;
            // The stride's deficit recurs at every footfall: the hips settle at its recent peak (remembered over SeatMemory,
            // approached at SeatAdapt), so a steady stride never drops them in a frame; a deficit beyond it still lowers at once.
            if(dt<0){seatPeak=seatBase=need;}
            else{seatPeak=Math.Max(need,seatPeak*(float)Math.Exp(-dt/(Weight>.5f?NativeRangedLocoRules.SeatMemory:NativeRangedLocoRules.SeatForget)));seatBase+=MeleeMath.Clamp(seatPeak-seatBase,-NativeRangedLocoRules.SeatAdapt*dt,NativeRangedLocoRules.SeatAdapt*dt);}
            float want=Math.Max(need,seatBase);
            seatDrop=dt<0||want>=seatDrop?want:Math.Max(want,seatDrop-NativeRangedLocoRules.SeatRise*dt);
            if(seatDrop<=1e-5f)return;
            r.P[CapturedRig.Body]-=new Vector3(0,seatDrop,0);
            for(int i=0;i<2;i++){bool left=i==0;if(gait.GaitFoot(left,out Vector3 t,out float yaw,out float pitch,out float toe))r.SolveLeg(left,t,yaw,pitch,toe);}
        }
        /// <summary>After the gun hold's torso (GunMotion: the Toward blends at `k`): a share of the stride swing re-added about
        /// the aimed spine and chest, zero on average, so the torso's aim yaw and pitch hold and the muzzle (oriented in the world
        /// by RangedPose.Arms) stays on the aim.</summary>
        public void Aim(MeleeRig r,string spec,float k)
        {
            // Off the forward line the torso swing was damped for the lowered carry (AsideTorso); under the aim the strafe and
            // backpedal keep their own (the hold steadies the chest's mean).
            if(Weight<=0||k<=1e-4f)return;float s=NativeRangedLocoRules.StyleOf(spec).AimSway*k/Math.Max(.5f,asideKeep);
            r.Premultiply(CapturedRig.Spine,MeleeMath.Slerp(Quaternion.Identity,swingSpine,s));r.Premultiply(CapturedRig.Chest,MeleeMath.Slerp(Quaternion.Identity,swingChest,s));
        }
        /// <summary>
        /// GunMotion.Sample, before RangedPose.Arms: a long gun's lowered carry (`target`, `aim`: the hold's carry, chest
        /// space) moved onto its weapon's run carry with the pack's two-hand grip motion at the move weight (the rifle at a low
        /// ready, the machine gun at the hip; the support hand follows on the handguard), the muzzle's pitch less the chest's
        /// forward `lean` (rad) as the hold's carry. The hold blends it with the aim once, so a shot or a strafe raises the gun
        /// at the hold's own pace. The pistol's carry is laid before the hold (Gun).
        /// </summary>
        public void LongGun(Actor a,float lean,ref Vector3 target,ref Quaternion aim)
        {
            float w=Weight;if(w<=1e-4f||a==null||a.Weapon==WeaponId.Pistol)return;bool mg=a.Weapon==WeaponId.MachineGun;
            Vector3 bob=right-rightMean,c=mg?NativeRangedLocoRules.MachineGunCarry:NativeRangedLocoRules.RifleCarry;
            target=Vector3.Lerp(target,(mg?NativeRangedLocoRules.MachineGunGrip:NativeRangedLocoRules.RifleGrip)+bob,w);
            aim=MeleeMath.Slerp(aim,CapturedRig.Euler(c.X-lean-bob.Y*.6f,c.Y+bob.X*.5f,c.Z),w);
        }
        /// <summary>
        /// MagicMotion.Sample, after the gait, the sprint lean and the staff carry and before the cast phrase: the pack torso,
        /// the staff hand (the left) on the pack's staff hand, the casting hand on its free hand (the cast phrase slerps from
        /// it), and the staff tilted toward the pack's staff (StaffTilt). Returns the move weight.
        /// </summary>
        public float Magic(MeleeRig r,MeleeView v,string spec,float time,float sprint)
        {
            StaffTilt=0;float w=Torso(r,v,spec,time,sprint);if(w<=0)return 0;var st=NativeRangedLocoRules.StyleOf(spec);
            // The mirrored capture: the rig's grip channels are the free (casting) hand, its left-hand channels the staff hand.
            r.Hand(true,left,r.ChainQuat(CapturedRig.LHand,CapturedRig.Chest),w);
            // The capture's free hand swings wide of its narrow shoulders; the voxel shoulders are far wider, so its line comes in.
            Vector3 cast=right;cast.X=rightMean.X*NativeRangedLocoRules.CastIn+(right.X-rightMean.X);r.Hand(false,cast,rightQ,w);
            // The staff follows the pack's staff a little, and stands up against the chest's lean (it rides in chest space);
            // the sprint carry tilts it itself.
            Vector3 f=leftFwd.LengthSquared()<1e-6f?Vector3.UnitY:Vector3.Normalize(leftFwd),c=Vector3.Transform(Vector3.UnitZ,r.ChainQuat(CapturedRig.Chest,-1));
            float lean=(float)Math.Asin(MeleeMath.Clamp(-c.Y,-1,1));
            StaffTilt=MeleeMath.Clamp(MeleeMath.Clamp((float)Math.Atan2(f.Z,Math.Max(.2f,f.Y))*st.StaffFollow,-.6f,.5f)-NativeRangedLocoRules.StaffUpright*lean,-.5f,.25f)*w*(1-Sprint);
            return w;
        }
    }
}
