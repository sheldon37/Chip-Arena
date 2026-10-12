using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The captured melee route's travel carry from the owner's Explosive pack (owner 2026-10-12: "running still needs
    /// adjustments on melee. the arms kind of wiggle around when running. Make it look more natural when moving~ Refer to the
    /// movement in our animation pack"): per melee weapon the slow walk, the walk with its seven strafes and the run in eight
    /// directions (tools/export_pack_locomotion.mjs, NativePackLocomotion.Generated). Presentation only.
    /// </summary>
    public static class NativeLocoRules
    {
        public const int None=-1,Sword=0,Daggers=1,Greatsword=2,Shield=3;
        /// <summary>Slots: the slow walk, then the walk and run families, eight directions each from the facing, counter-clockwise
        /// (to the rig's left) in 45 degree steps.</summary>
        public const int Slow=0,Walk=1,Run=9,Directions=8;
        public static int SetFor(string spec)=>spec=="sword"?Sword:spec=="daggers"?Daggers:spec=="greatsword"?Greatsword:spec=="shield"?Shield:None;
        public static CapturedClip Clip(int set,int slot)
        {
            if(set<0||set>=NativePackLocomotion.Sets.Length)return null;var s=NativePackLocomotion.Sets[set];return slot>=0&&slot<s.Length?s[slot]:null;
        }
        /// <summary>
        /// The share of each clip's per-stride arm swing kept about its cycle-mean carry (the hands, blades and elbow poles):
        /// the voxel legs step at about 2.6 times the capture's cadence, so the captured pump read as the arms flapping. The
        /// weapon hand keeps the least (a steady carry), the free hand a little more (a light pump in step); the torso keeps
        /// MeleeMotion's walk and run shares. The direction follows the travel at DirectionRate (1/s).
        /// </summary>
        public const float WalkArmKeep=.25f,RunArmKeep=.15f,WalkFreeKeep=.45f,RunFreeKeep=.35f,AsideTorso=.3f,DirectionRate=9,SlowFrom=1.2f,SlowTo=2.4f;
        /// <summary>First person: the share of the walk and run clips' motion the melee view arms keep (the view layer's bob
        /// carries the stride; the captured pump at the gait's cadence wiggled the view weapon).</summary>
        public const float FirstPersonCarry=.3f;
        /// <summary>The two directions bracketing `angle` (radians from the facing, + to the rig's left) and the blend between them.</summary>
        public static void Bracket(float angle,out int a,out int b,out float t)
        {
            float f=MeleeMath.Wrap(angle)/(MeleeMath.TAU/Directions);if(f<0)f+=Directions;
            a=(int)Math.Floor(f)%Directions;b=(a+1)%Directions;t=MeleeMath.Clamp(f-(float)Math.Floor(f),0,1);
        }
    }

    /// <summary>
    /// Lays the pack's directional walk and run carries over the captured melee route (MeleeMotion.Ready's travel block), in
    /// place of the forward-only captured walk and run: the two clips bracketing the travel direction (from the facing) are
    /// sampled at the gait's stride phase from their own left-foot strike and blended, the slow walk under 2.4 m/s ahead; each
    /// keeps its cycle-mean torso and arm carry and only part of its per-stride swing (MeleeMotion's torso shares, this class's
    /// arm shares), and a strafe's mean hip turn (relative to its family's forward clip) is folded into the spine, so the
    /// procedural pelvis turning toward the travel and the ground pass's counter-turn leave the chest facing the aim as the pack
    /// does. The legs stay the gait's planted procedural feet (no skating); the upper body, arms and weapon carry are the
    /// pack's. Same weights and order as the captured walk and run (walk at `carry`, run at `carry` x the run blend), so the
    /// steady-torso counter and every transition are unchanged. No allocation per frame.
    /// </summary>
    public sealed class NativeLocomotionCarry
    {
        private readonly float[] rowA=new float[CapturedClip.Channels],rowB=new float[CapturedClip.Channels],mean=new float[CapturedClip.Channels],meanB=new float[CapturedClip.Channels];
        private float dx,dz=1,last=-100;
        private static readonly float[][][] headings=new float[NativePackLocomotion.Sets.Length][][];
        private static bool warmed;
        /// <summary>Decodes every loop and its cycle mean and strafe heading once, so no direction allocates when first used.</summary>
        public NativeLocomotionCarry()
        {
            if(warmed)return;
            for(int s=0;s<NativePackLocomotion.Sets.Length;s++)for(int k=0;k<NativePackLocomotion.Slots.Length;k++){NativeLocoRules.Clip(s,k).Decode();Heading(s,k);}
            warmed=true;
        }
        /// <summary>The travel direction (radians from the facing, + to the rig's left) the last Carry blended.</summary>
        public float Direction{get;private set;}
        /// <summary>The two directions (0..7, NativeLocoRules) and the blend between them the last Carry used.</summary>
        public int First{get;private set;}public int Second{get;private set;}public float Mix{get;private set;}
        private static Vector3 Row(float[] f,int i)=>new Vector3(f[i],f[i+1],f[i+2]);
        private static Quaternion Rot(float[] f,int i){Vector3 v=Row(f,i);float a=v.Length();return a<1e-9f||!(a<1e3f)?Quaternion.Identity:MeleeMath.AxisAngle(v/a,a);}
        private static void Write(float[] f,int i,Quaternion q)
        {
            q=Quaternion.Normalize(q);if(q.W<0)q=-q;float w=MeleeMath.Clamp(q.W,-1,1),s=(float)Math.Sqrt(Math.Max(0,1-w*w)),a=2*(float)Math.Acos(w),k=s<1e-7f?2:a/s;
            f[i]=q.X*k;f[i+1]=q.Y*k;f[i+2]=q.Z*k;
        }
        // A clip's mean hip heading (the pelvis forward's yaw over its cycle), relative to its family's forward clip.
        private static float Heading(int set,int slot)
        {
            var h=headings[set]??=new float[NativePackLocomotion.Slots.Length][];
            if(h[slot]==null)
            {
                float Mean(CapturedClip c){float[] m=c.CycleMean();Vector3 fwd=Vector3.Transform(Vector3.UnitZ,MeleeMath.AxisAngle(MeleeMath.Y,m[MeleeCapture.Yaw])*Rot(m,MeleeCapture.Pelvis));return (float)Math.Atan2(fwd.X,fwd.Z);}
                int forward=slot>=NativeLocoRules.Run?NativeLocoRules.Run:slot>=NativeLocoRules.Walk?NativeLocoRules.Walk:NativeLocoRules.Slow;
                h[slot]=new[]{MeleeMath.Wrap(Mean(NativeLocoRules.Clip(set,slot))-Mean(NativeLocoRules.Clip(set,forward)))};
            }
            return h[slot][0];
        }
        // One clip at the stride phase, its strafe heading folded into the spine, and its cycle mean (heading folded too).
        private void Sample(int set,int slot,float phase,float[] row,float[] avg)
        {
            var c=NativeLocoRules.Clip(set,slot);c.Sample(c.Marks[0]+phase*c.Duration,row);Array.Copy(c.CycleMean(),avg,CapturedClip.Channels);
            float h=Heading(set,slot);if(Math.Abs(h)<1e-4f)return;Quaternion turn=MeleeMath.AxisAngle(MeleeMath.Y,h);
            Write(row,MeleeCapture.Spine,turn*Rot(row,MeleeCapture.Spine));Write(avg,MeleeCapture.Spine,turn*Rot(avg,MeleeCapture.Spine));
        }
        private static void Damp(float[] f,float[] m,int from,int to,float keep){for(int c=from;c<to;c++)f[c]=m[c]+(f[c]-m[c])*keep;}
        // A family's pose for the travel direction: the two bracketing clips blended (and the slow walk under SlowTo ahead).
        private void Family(int set,int first,float phase,int a,int b,float t,float slow,float torsoKeep,float armKeep,float freeKeep)
        {
            Sample(set,first+a,phase,rowA,mean);
            if(t>1e-4f){Sample(set,first+b,phase,rowB,meanB);MeleeCapture.BlendRowsTurning(rowA,rowB,t,rowA);MeleeCapture.BlendRows(mean,meanB,t,mean);}
            if(slow>1e-4f){Sample(set,NativeLocoRules.Slow,phase,rowB,meanB);MeleeCapture.BlendRowsTurning(rowA,rowB,slow,rowA);MeleeCapture.BlendRows(mean,meanB,slow,mean);}
            Damp(rowA,mean,MeleeCapture.Spine,MeleeCapture.Head+3,torsoKeep);Damp(rowA,mean,MeleeCapture.Grip,MeleeCapture.LHand,armKeep);Damp(rowA,mean,MeleeCapture.LHand,MeleeCapture.TwoHand,freeKeep);
        }
        /// <summary>
        /// The travel carry at `carry` (walk) and `carry` x `run` (run) for the stride phase `phase` (0..1); false (the route
        /// keeps its captured walk and run) for a weapon without pack locomotion.
        /// </summary>
        public bool Carry(MeleeRig r,MeleeSet set,MeleeView p,float carry,float run,float phase,float time)
        {
            int s=NativeLocoRules.SetFor(p.Spec);if(s<0||set==null||carry<=.001f)return false;
            float dt=last<-90||time<last-MeleeGait.REWIND||time-last>.35f?-1:Math.Max(0,time-last);last=time;
            float cy=(float)Math.Cos(r.GroupYaw),sy=(float)Math.Sin(r.GroupYaw),lvx=p.Vel.X*cy-p.Vel.Z*sy,lvz=p.Vel.X*sy+p.Vel.Z*cy,speed=(float)Math.Sqrt(lvx*lvx+lvz*lvz);
            if(speed>.3f)
            {
                float ux=lvx/speed,uz=lvz/speed;
                if(dt<0){dx=ux;dz=uz;}else{float k=1-(float)Math.Exp(-dt*NativeLocoRules.DirectionRate);dx+=(ux-dx)*k;dz+=(uz-dz)*k;}
            }
            float dir=(float)Math.Atan2(dx,dz);Direction=dir;NativeLocoRules.Bracket(dir,out int a,out int b,out float t);
            float slow=(1-MeleeMath.Ease((speed-NativeLocoRules.SlowFrom)/(NativeLocoRules.SlowTo-NativeLocoRules.SlowFrom)))*Math.Max(0,(float)Math.Cos(dir));
            First=a;Second=b;Mix=t;
            // Off the forward line the strafe and backpedal torsos keep less of their per-stride swing (their captured roll and
            // nod sat over a pelvis turned the other way): the steady torso's bounds hold in every direction.
            float ahead=Math.Max(0,(float)Math.Cos(dir)),aside=MeleeMath.Lerp(NativeLocoRules.AsideTorso,1,ahead*ahead*ahead);
            if(carry*run<.999f)
            {
                Family(s,NativeLocoRules.Walk,phase,a,b,t,slow,MeleeMotion.WalkTorsoKeep*aside,NativeLocoRules.WalkArmKeep,NativeLocoRules.WalkFreeKeep);
                MeleeCapture.Apply(r,set,rowA,CaptureOptions.Of(carry,set.Fit,legs:0,root:0));
            }
            if(run>.001f)
            {
                Family(s,NativeLocoRules.Run,phase,a,b,t,0,MeleeMotion.RunTorsoKeep*aside,NativeLocoRules.RunArmKeep,NativeLocoRules.RunFreeKeep);
                MeleeCapture.Apply(r,set,rowA,CaptureOptions.Of(carry*run,set.Fit,legs:0,root:0));
            }
            return true;
        }
    }
}
