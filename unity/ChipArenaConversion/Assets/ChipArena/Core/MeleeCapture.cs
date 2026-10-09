using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // Port of mocap/mocap-pose.ts (presentation only): poses the rig from one sampled capture row at a weight over the
    // layers below. Torso, clavicles and legs take the retargeted joint rotations; the arms go through the rig's own IK
    // (main hand to the retargeted grip with the blade along the captured blade and edge, the support hand on the weapon's
    // support grip while the capture holds the weapon two-handed). Root travel is stripped: the authority owns movement.
    public enum MeleeClear{Full,Turn,None}
    public struct MeleeFit{public bool OneHand,Floor;public float Pommel,HalfWidth;public MeleeClear? Clear;}
    public struct CaptureOptions
    {
        public float Weight,Legs,Root;public bool Arms;public MeleeClear Clear;public MeleeFit Fit;
        public static CaptureOptions Of(float weight,MeleeFit fit,MeleeClear clear=MeleeClear.Full,float legs=1,float root=1,bool arms=true)
            =>new CaptureOptions{Weight=weight,Legs=legs,Root=root,Arms=arms,Clear=fit.Clear??clear,Fit=fit};
    }
    // One weapon's captured set (mocap-layer MOCAP_SETS) and its weapon dimensions (weapon-visuals blade tips).
    public sealed class MeleeSet
    {
        public string Spec;public int Index;public CapturedClip Idle,Walk,Run,Block,BlockHit,Stagger;public CapturedClip[] Cuts;public MeleeFit Fit;public float[] Anticipation;
        public float BladeEnd;public Vector3 Tip;public bool Dual;
        private static CapturedClip Clip(int set,string state){foreach(var c in CapturedClips.Sets[set])if(c.StateName==state)return c;return null;}
        private static MeleeSet Make(string spec,int set,string[] cuts,MeleeFit fit,float[] anticipation,Vector3 tip,bool dual)
            =>new MeleeSet{Spec=spec,Index=set,Idle=Clip(set,"idle"),Walk=Clip(set,"walk"),Run=Clip(set,"run"),Block=Clip(set,"block"),BlockHit=Clip(set,"blockHit"),Stagger=Clip(set,"stagger"),
                Cuts=Array.ConvertAll(cuts,c=>Clip(set,c)),Fit=fit,Anticipation=anticipation,Tip=tip,BladeEnd=tip.Z-.08f,Dual=dual};
        public static readonly MeleeSet[] All=
        {
            Make("sword",0,new[]{"cutA","cutB","cutC"},new MeleeFit{OneHand=true,Floor=true,Pommel=.19f,HalfWidth=.05f},new[]{1,.6f,1},new Vector3(0,0,1.28f),false),
            Make("daggers",1,new[]{"cutA","cutB","cutC","cutD"},new MeleeFit{OneHand=true,Floor=true,Pommel=.19f,HalfWidth=.045f},new[]{.35f},new Vector3(0,0,.84f),true),
            Make("greatsword",2,new[]{"cutA","cutB"},new MeleeFit{Floor=true,Pommel=.27f,HalfWidth=.09f},new[]{1f},new Vector3(0,0,1.69f),false),
            Make("shield",3,new[]{"cutA","cutB"},new MeleeFit{OneHand=true,Floor=true,Pommel=.27f,HalfWidth=.09f,Clear=MeleeClear.None},new[]{1f},new Vector3(0,.4f,.29f),false),
        };
        public static MeleeSet For(string spec){foreach(var s in All)if(s.Spec==spec)return s;return null;}
        public CapturedClip Cut(int attackIndex){int n=Cuts.Length;return Cuts[((Math.Max(1,attackIndex)-1)%n+n)%n];}
        public float AnticipationFor(int attackIndex)=>Anticipation[Anticipation.Length==1?0:(Math.Max(1,attackIndex)-1)%Cuts.Length];
    }
    public static class MeleeCapture
    {
        // Channel layout (mocap-clip MOCAP_CHANNELS).
        public const int Yaw=0,By=2,Pelvis=4,Spine=7,Chest=10,Neck=13,Head=16,LClav=19,RClav=22,LHip=25,RHip=28,LAnkle=31,RAnkle=34,LKnee=37,RKnee=38,
            Grip=39,Blade=42,Edge=45,RPole=48,LHand=51,LFwd=54,LUp=57,LPole=60,TwoHand=63;
        private static Vector3 Row(float[] f,int i)=>new Vector3(f[i],f[i+1],f[i+2]);
        private static Quaternion Rot(float[] f,int i){Vector3 v=Row(f,i);float a=v.Length();return a<1e-9f?Quaternion.Identity:MeleeMath.AxisAngle(v/a,a);}
        private static void Set(MeleeRig r,int bone,Quaternion q,float w){if(w>=1)r.SetQ(bone,q);else r.Slerp(bone,q,w);}
        private static readonly Vector3 PoleR=Vector3.Normalize(new Vector3(-1,-.7f,-.2f)),PoleL=Vector3.Normalize(new Vector3(1,-.8f,-.25f));
        // The clip's elbow pole for a hand target, re-taken perpendicular to this frame's reach, into the clavicle's frame.
        private static Vector3 Pole(MeleeRig r,bool left,Vector3 target,float[] f,int i)
        {
            Vector3 d=Vector3.Normalize(new Vector3(target.X-(left?.345f:-.345f),target.Y-.18f,target.Z)),o=Row(f,i);o-=d*Vector3.Dot(o,d);
            if(o.LengthSquared()<.01f)o=left?PoleL:PoleR;
            return Vector3.Transform(Vector3.Normalize(o),MeleeMath.Inverse(r.Q[left?CapturedRig.LClav:CapturedRig.RClav]));
        }
        public static void Apply(MeleeRig r,MeleeSet set,float[] f,CaptureOptions o)
        {
            float w=o.Weight;if(w<=.001f)return;r.MocapPosed=true;float root=o.Root*w,legs=o.Legs*w;
            // Hips height rides the motion root; the heading turns the pelvis (with the legs under it).
            if(root>.001f){Vector3 b=r.P[CapturedRig.Body];b.Y+=(f[By]-.025f-b.Y)*root;r.P[CapturedRig.Body]=b;Set(r,CapturedRig.Pelvis,MeleeMath.AxisAngle(MeleeMath.Y,f[Yaw])*Rot(f,Pelvis),root);}
            Set(r,CapturedRig.Spine,Rot(f,Spine),w);Set(r,CapturedRig.Chest,Rot(f,Chest),w);Set(r,CapturedRig.Neck,Rot(f,Neck),w);Set(r,CapturedRig.Head,Rot(f,Head),w);
            Set(r,CapturedRig.LClav,Rot(f,LClav),w);Set(r,CapturedRig.RClav,Rot(f,RClav),w);
            if(legs>.001f)
            {
                Set(r,CapturedRig.LHip,Rot(f,LHip),legs);Set(r,CapturedRig.RHip,Rot(f,RHip),legs);
                Set(r,CapturedRig.LKnee,MeleeMath.AxisAngle(MeleeMath.X,Math.Max(0,f[LKnee])),legs);Set(r,CapturedRig.RKnee,MeleeMath.AxisAngle(MeleeMath.X,Math.Max(0,f[RKnee])),legs);
                Set(r,CapturedRig.LFoot,Rot(f,LAnkle),legs);Set(r,CapturedRig.RFoot,Rot(f,RAnkle),legs);
            }
            if(!o.Arms)return;
            Vector3 grip=Row(f,Grip),blade=Vector3.Normalize(Row(f,Blade)),edge=Row(f,Edge);edge-=blade*Vector3.Dot(edge,blade);
            if(edge.LengthSquared()<1e-8f)edge=new Vector3(1,0,0)-blade*blade.X;edge=Vector3.Normalize(edge);
            // The voxel shoulders, chest and head are bigger than the captured human's: keep the blade (and hilt) clear. The
            // extent is the weapon's at its current length scale (mocap-pose.ts bladeEnd and pommel times weapon.scale.z).
            if(o.Clear!=MeleeClear.None)MeleeClearance.Shared.Clear(r,ref grip,ref blade,ref edge,-o.Fit.Pommel*r.WeaponReach,set.BladeEnd*r.WeaponReach,o.Clear!=MeleeClear.Turn,o.Fit.HalfWidth,o.Fit.Floor);
            r.BraceWeapon(grip,MeleeMath.Basis(edge,Vector3.Cross(blade,edge),blade),w,Pole(r,false,grip,f,RPole),!o.Fit.OneHand);
            // The support hand leaves the grip only where the capture lets go of it.
            float free=(1-MeleeMath.Clamp(f[TwoHand],0,1))*w;
            if(free>.001f)
            {
                Vector3 hand=Row(f,LHand),a=Vector3.Normalize(Row(f,LFwd)),c=Row(f,LUp);c-=a*Vector3.Dot(c,a);if(c.LengthSquared()<1e-8f)c=Vector3.UnitY;c=Vector3.Normalize(c);
                r.Hand(true,hand,MeleeMath.Basis(Vector3.Cross(c,a),c,a),free,Pole(r,true,hand,f,LPole));
            }
        }
        private static readonly int[] RollChannels={Edge,LUp};
        public static float[] BlendRows(float[] a,float[] b,float k,float[] o){for(int i=0;i<o.Length;i++)o[i]=a[i]+(b[i]-a[i])*k;return o;}
        // blendRows for poses far apart: the roll channels turn along the great circle between them.
        public static float[] BlendRowsTurning(float[] a,float[] b,float k,float[] o)
        {
            foreach(int i in RollChannels)
            {
                Vector3 da=Vector3.Normalize(Row(a,i)),db=Vector3.Normalize(Row(b,i));da=Vector3.Transform(da,MeleeMath.Slerp(Quaternion.Identity,MeleeMath.FromTo(da,db),k));
                o[i]=da.X;o[i+1]=da.Y;o[i+2]=da.Z;
            }
            for(int i=0;i<o.Length;i++)if(!(i>=Edge&&i<Edge+3||i>=LUp&&i<LUp+3))o[i]=a[i]+(b[i]-a[i])*k;
            return o;
        }
        // Final floor guard: a blade tip below the floor plane turns its wrist up about the horizontal axis across the blade
        // just enough to bring the tip onto the plane; a near-vertical blade keeps the axis its weapon turned about before.
        // The tip is the stretched 'blade-tip' socket the source reads (its z times the weapon's reach scale).
        private static readonly float VertIn=(float)Math.Sin(15*Math.PI/180),VertOut=(float)Math.Sin(30*Math.PI/180);
        public static void KeepBladesOffFloor(MeleeRig r,MeleeSet set,Vector3[] keptWorld)
        {
            Quaternion g=MeleeMath.AxisAngle(MeleeMath.Y,r.GroupYaw),gi=MeleeMath.Inverse(g);
            for(int k=0;k<2;k++)
            {
                bool left=k==1;if(left?!set.Dual:!r.WeaponVisible)continue;int wrist=left?CapturedRig.LHand:CapturedRig.RHand;
                Quaternion wq=r.ChainQuat(wrist,-1);Vector3 gg=r.ChainPoint(wrist,-1),gt=gg+Vector3.Transform(r.Reached(set.Tip),wq),gd=gt-gg;float len=gd.Length();if(len<1e-4f)continue;gd/=len;
                Vector3 kept=Vector3.Transform(keptWorld[k],gi),gn=Vector3.Cross(gd,MeleeMath.Y);float own=gn.Length(),u=MeleeMath.SmoothStep(own,VertIn,VertOut);
                Vector3 ga=kept-gd*Vector3.Dot(kept,gd);if(ga.LengthSquared()<1e-8f)ga=gn;ga=ga.LengthSquared()>0?Vector3.Normalize(ga):ga;
                // Leaning out again, the kept axis turns about the blade onto the blade's own, never flipping over to it.
                if(u>0){gn/=Math.Max(own,1e-9f);float turn=(float)Math.Atan2(Vector3.Dot(Vector3.Cross(ga,gn),gd),Vector3.Dot(ga,gn));ga=Vector3.Normalize(MeleeMath.ApplyAxisAngle(ga,gd,turn*u));}
                keptWorld[k]=Vector3.Transform(ga,g);
                const float floor=MeleeClearance.FloorClear;if(gt.Y>=floor||gg.Y<=floor)continue;
                float dy=gd.Y,uy=Vector3.Cross(ga,gd).Y,R=(float)Math.Sqrt(dy*dy+uy*uy),want=(floor-gg.Y)/len,phi=(float)Math.Atan2(uy,dy);if(phi<=0)phi+=MeleeMath.TAU;
                float lift=Math.Min(MeleeMath.PI,Math.Max(0,phi-(float)Math.Acos(MeleeMath.Clamp(want/Math.Max(R,1e-6f),-1,1))));if(lift<=0)continue;
                r.SetQ(wrist,MeleeMath.Inverse(r.ChainQuat(MeleeRig.Parent(wrist),-1))*(MeleeMath.AxisAngle(ga,lift)*wq));
            }
        }
        // Recovery between the already fitted contact and guard poses (applyMocapRecovery). The wrists interpolate in chest
        // space, then convert through the blended arm chain, so a reverse-grip blade never swings through the floor.
        private static readonly int[] RecoveryBones={CapturedRig.Pelvis,CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,CapturedRig.LClav,CapturedRig.RClav,CapturedRig.LArm,CapturedRig.RArm,CapturedRig.LElbow,CapturedRig.RElbow,CapturedRig.LHand,CapturedRig.RHand};
        public sealed class RecoveryCache{public bool Has;public string Spec;public float Attack,Rate,Reach;public int Index;public readonly Quaternion[] From=new Quaternion[13],To=new Quaternion[13];public float FromY,ToY;public readonly Quaternion[] FromWrist=new Quaternion[2],ToWrist=new Quaternion[2];}
        private static void Capture(MeleeRig r,Quaternion[] q,Quaternion[] wrists,out float y)
        {
            for(int i=0;i<RecoveryBones.Length;i++)q[i]=r.Q[RecoveryBones[i]];y=r.P[CapturedRig.Body].Y;
            wrists[0]=r.ChainQuat(CapturedRig.RHand,CapturedRig.Chest);wrists[1]=r.ChainQuat(CapturedRig.LHand,CapturedRig.Chest);
        }
        public static void Recovery(MeleeRig r,MeleeSet set,float[] contact,float[] guard,float k,float attack,int index,float rate,RecoveryCache cache,Vector3[] kept)
        {
            // Keyed by the attack (the source's id:spec:lastAttack:attackIndex:reach:rate string; the id is the cache's owner).
            if(!(cache.Has&&cache.Spec==set.Spec&&cache.Attack==attack&&cache.Index==index&&cache.Rate==rate&&cache.Reach==r.WeaponReach))
            {
                ApplyCleared(r,set,contact,MeleeClear.Turn);KeepBladesOffFloor(r,set,kept);Capture(r,cache.From,cache.FromWrist,out cache.FromY);
                ApplyCleared(r,set,guard,MeleeClear.Full);KeepBladesOffFloor(r,set,kept);Capture(r,cache.To,cache.ToWrist,out cache.ToY);cache.Has=true;cache.Spec=set.Spec;cache.Attack=attack;cache.Index=index;cache.Rate=rate;cache.Reach=r.WeaponReach;
            }
            r.MocapPosed=true;
            for(int i=0;i<RecoveryBones.Length;i++)r.SetQ(RecoveryBones[i],MeleeMath.Slerp(cache.From[i],cache.To[i],k));
            Vector3 b=r.P[CapturedRig.Body];b.Y=cache.FromY+(cache.ToY-cache.FromY)*k;r.P[CapturedRig.Body]=b;
            for(int i=0;i<2;i++){int h=i==0?CapturedRig.RHand:CapturedRig.LHand;Quaternion w=MeleeMath.Slerp(cache.FromWrist[i],cache.ToWrist[i],k);r.SetQ(h,MeleeMath.Inverse(r.ChainQuat(MeleeRig.Parent(h),CapturedRig.Chest))*w);}
            if(!set.Fit.OneHand)r.SupportHand(1);
        }
        // The recovery's endpoints always clear with their own mode, whatever the set's fit says (the source's spread order).
        private static void ApplyCleared(MeleeRig r,MeleeSet set,float[] row,MeleeClear clear)
        {
            var o=CaptureOptions.Of(1,set.Fit,legs:0);o.Clear=clear;Apply(r,set,row,o);
        }
        // Recovery-only wrist settling (finishMocapRecovery), measured relative to the chest so whole-body spins stay intact.
        public sealed class WristHistory{public string Key;public float Time;public readonly Quaternion[] Q=new Quaternion[2];public bool Has;}
        public static void FinishRecovery(MeleeRig r,MeleeSet set,float time,bool recovering,float rate,string key,WristHistory previous,Vector3[] kept)
        {
            KeepBladesOffFloor(r,set,kept);float dt=previous.Has?time-previous.Time:0;bool limited=false;
            if(recovering&&previous.Has&&previous.Key==key&&dt>0&&dt<=.1f)
                for(int i=0;i<2;i++)
                {
                    int h=i==0?CapturedRig.RHand:CapturedRig.LHand;Quaternion settle=r.ChainQuat(h,CapturedRig.Chest);float angle=MeleeMath.AngleTo(previous.Q[i],settle),limit=18*rate*dt;
                    if(angle>limit){limited=true;settle=MeleeMath.Slerp(previous.Q[i],settle,limit/angle);r.SetQ(h,MeleeMath.Inverse(r.ChainQuat(MeleeRig.Parent(h),CapturedRig.Chest))*settle);}
                }
            if(limited)KeepBladesOffFloor(r,set,kept);
            if(!previous.Has||previous.Key!=key){previous.Has=true;previous.Key=key;previous.Time=time;}
            previous.Time=time;previous.Q[0]=r.ChainQuat(CapturedRig.RHand,CapturedRig.Chest);previous.Q[1]=r.ChainQuat(CapturedRig.LHand,CapturedRig.Chest);
        }
    }
}
