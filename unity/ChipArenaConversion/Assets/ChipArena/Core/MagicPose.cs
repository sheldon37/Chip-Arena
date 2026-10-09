using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // Source magic-motion.ts: analytic coil / drive / follow-through / ready phrases.
    // Only the rig adaptation below differs: native quaternion bones and shared chest-space IK.
    public static class MagicPose
    {
        private static readonly Dictionary<string,float[][]> AbilityKeys=new Dictionary<string,float[][]>
        {
            {"flame-lance",new[]{new[]{-2.3f,0.35f,-0.45f,-1.75f,0.3f,0.5f,0.5f,-0.55f,-0.1f,-0.08f,0.03f},new[]{-1.5f,0f,-0.04f,-0.05f,-0.2f,0f,0f,0.45f,0.2f,0.15f,0.05f}}},
            {"tidal-wave",new[]{new[]{-0.45f,-0.9f,-1f,-1f,0.2f,-0.7f,0.5f,-0.6f,-0.03f,-0.05f,0.04f,-0.2f,-0.3f},new[]{-2f,0.5f,-0.2f,-0.2f,-0.2f,0.3f,0f,0.45f,0.12f,0.1f,0.03f,0.25f,-0.6f}}},
            {"blink-strike",new[]{new[]{-1.2f,0.2f,-0.3f,-1.2f,0.1f,0.2f,0.2f,-0.1f,0.15f,0.02f,0.12f},new[]{-1.4f,0.1f,-0.1f,-0.4f,-0.1f,0f,0f,0.1f,0.08f,0.05f,0.06f}}},
            {"shadow-shackles",new[]{new[]{-1.4f,0.6f,-0.3f,-2.3f,0.4f,0.2f,0.6f,-0.65f,-0.1f,-0.08f,0.04f},new[]{-1.75f,0f,-0.04f,-0.08f,-0.18f,0f,0f,0.5f,0.17f,0.13f,0.05f}}},
            {"meteor-strike",new[]{new[]{-2.9f,0.1f,-0.2f,-0.6f,0.3f,0f,0f,-0.2f,-0.12f,-0.06f,-0.02f,-0.5f,-2.6f},new[]{-1.2f,0f,-0.1f,-0.3f,-0.3f,0f,0f,0.2f,0.25f,0.15f,0.05f,0.7f,-1f}}},
            {"geyser-wall",new[]{new[]{-0.9f,0.2f,-0.4f,-1.2f,0.1f,0.3f,0.3f,-0.3f,0.05f,0f,0.08f,-0.6f,0.3f},new[]{-1f,-0.6f,-0.2f,-0.3f,-0.1f,-0.3f,0f,0.5f,0.12f,0.08f,0.06f,0.4f,-0.4f}}},
            {"ball-lightning",new[]{new[]{-1f,0.1f,-0.3f,-1.6f,-0.1f,0.3f,0.3f,-0.15f,-0.06f,-0.04f,0.02f,0f,-0.6f},new[]{-1.6f,-0.15f,-0.05f,-0.1f,-0.2f,0f,0f,0.15f,0.15f,0.12f,0.03f,0.3f,-1.2f}}},
            {"singularity",new[]{new[]{-2f,0.4f,-0.3f,-0.5f,0.2f,0f,0f,-0.2f,-0.05f,-0.04f,0.01f,-0.3f,-2.2f},new[]{-1.3f,-0.3f,-0.2f,-1.9f,0.6f,0.3f,0.6f,0.3f,0.1f,0.05f,0.03f,0.3f,-1.8f}}},
        };
        private static readonly Dictionary<string,float[][]> FirstKeys=new Dictionary<string,float[][]>
        {
            {"meteor-strike",new[]{new[]{-1.3f,0.1f,-0.2f,-1.6f,0.1f,0.2f,0.3f,-0.1f,-0.05f,-0.04f,0.01f,-0.2f,-0.2f},new[]{-1.55f,0f,-0.05f,-0.1f,-0.2f,0f,0f,0.15f,0.12f,0.08f,0.03f,0.45f,-0.4f}}},
            {"singularity",new[]{new[]{-1.2f,0.2f,-0.3f,-1.5f,0.2f,0.2f,0.4f,-0.15f,-0.05f,-0.03f,0.01f,-0.15f,-0.2f},new[]{-1.6f,0f,-0.05f,-0.15f,0.5f,0f,0.4f,0.2f,0.12f,0.08f,0.03f,0.4f,-0.35f}}},
            {"geyser-wall",new[]{new[]{-0.9f,0.2f,-0.4f,-1.2f,0.1f,0.3f,0.3f,-0.3f,0.05f,0f,0.06f,-0.3f,0f},new[]{-1.1f,-0.6f,-0.2f,-0.3f,-0.1f,-0.3f,0f,0.5f,0.1f,0.06f,0.04f,0.25f,-0.15f}}},
            {"tidal-wave",new[]{new[]{-0.45f,-0.9f,-1f,-1f,0.2f,-0.7f,0.5f,-0.3f,-0.03f,-0.05f,0.04f,-0.1f},new[]{-1.9f,0.5f,-0.2f,-0.2f,-0.2f,0.3f,0f,0.15f,0.1f,0.08f,0.03f,0.1f,-0.2f}}},
        };

        private static float Smooth(float t){t=MathUtil.Clamp(t,0,1);return t*t*t*(t*(t*6-15)+10);}
        private static float[] Pad(params float[] values){var p=new float[13];Array.Copy(values,p,Math.Min(13,values.Length));return p;}
        private static float[] Ready(int e,float pitch)=>e==3?Pad(-1.35f-pitch*.6f,.12f,-.12f,-.45f,-.1f,.15f,.15f,.12f,.05f,.05f,.02f):Pad(-1-pitch*.45f,.15f,-.25f,-1.3f,-.1f,.3f,.3f,-.05f,.02f,.02f,0);
        private static float[] Key(float[] key,float pitch){var p=Pad(key);p[0]-=pitch*.6f;return p;}
        private static float[] Coil(int e,float pitch,float[][] ab)
        {
            if(ab!=null)return Key(ab[0],pitch);
            if(e==2)return Pad(-.55f,-.75f,-.95f,-1.15f,.2f,-.7f,.45f,-.53f,-.03f,-.05f,.035f);
            if(e==3)return Pad(-1.2f-pitch*.45f,.3f,-.3f,-.95f,.25f,.35f,.3f,-.2f,-.03f,-.02f,.015f);
            if(e==4)return Pad(-1.35f,.55f,-.3f,-2.25f,.35f,.2f,.55f,-.6f,-.1f,-.08f,.04f);
            return Pad(-2.3f,.3f,-.42f,-1.75f,.2f,.45f,.45f,-.48f,-.09f,-.065f,.03f);
        }
        private static float[] Release(int e,float pitch,float[][] ab)=>ab!=null?Key(ab[1],pitch):Pad(-(e==4?1.95f:1.65f)+(e==3?.25f:.18f)-pitch*.65f,e==2?-.12f:0,-.04f,-.08f,-.18f,0,0,e==4?.46f:.4f,.17f,.13f,.05f);
        private static float[] Follow(int e,float pitch,float[][] ab)
        {var p=Release(e,pitch,ab);p[0]+=e==4?.08f:.12f;p[1]+=e==2?.25f:.05f;p[3]-=.12f;p[4]-=e==1?.5f:.3f;p[7]+=.1f;p[8]+=.03f;p[9]+=.02f;p[10]+=.015f;return p;}
        private static void Mix(float[] o,float[] a,float[] b,float t){for(int i=0;i<13;i++)o[i]=a[i]+(b[i]-a[i])*t;}
        private static void Phrase(float[] o,int e,float pitch,float age,float windup,float release,float rate,float[] start,float[][] ab=null)
        {
            float f=.1f/rate,s=.3f/rate;
            if(age<windup)Mix(o,start,Coil(e,pitch,ab),Smooth(age/Math.Max(.01f,windup)));
            else if(age<release)Mix(o,Coil(e,pitch,ab),Release(e,pitch,ab),(float)Math.Pow(MathUtil.Clamp((age-windup)/Math.Max(.01f,release-windup),0,1),1.4));
            else if(age<release+f){float t=(age-release)/f;Mix(o,Release(e,pitch,ab),Follow(e,pitch,ab),t*(2-t));}
            else Mix(o,Follow(e,pitch,ab),Ready(e,pitch),Smooth((age-release-f)/s));
        }
        public static float Sample(Actor a,float time,float pitch,bool firstPerson,float[] output)
        {
            float rate=Math.Max(.15f,a.CastRate),age=time-a.AttackAt,release=NativeAbilityRules.PrimaryWindup(a.MagicVisual,rate),windup=release*.6f;
            if(a.AbilityAt>a.AttackAt&&time>=a.AbilityAt&&a.AbilityId!=null)
            {
                float[][] keys=null;
                if(firstPerson)FirstKeys.TryGetValue(a.AbilityId,out keys);
                if(keys==null)AbilityKeys.TryGetValue(a.AbilityId,out keys);
                if(keys!=null)
                {
                    float abAge=time-a.AbilityAt,span=a.AbilityRelease-a.AbilityAt;
                    float weight=Smooth(abAge/Math.Max(.04f,span*.6f))*(1-Smooth((abAge-span-.55f/rate)/.3f));
                    if(weight>.001f){Phrase(output,a.MagicVisual,pitch,abAge,span*.6f,span,rate,Ready(a.MagicVisual,pitch),keys);return weight;}
                }
            }
            if(age<0||age>10){Array.Clear(output,0,output.Length);return 0;}
            float Hold(float since)=>1-Smooth((since-.45f)/.3f);
            float ramp=age<windup?Smooth(age/Math.Max(.01f,windup)):1,weightPrimary=ramp;
            var start=Ready(a.MagicVisual,pitch);
            if(a.LastShot<a.AttackAt&&a.AttackAt-a.LastShot<.75f)
            {
                float initial=Hold(a.AttackAt-a.LastShot);
                Phrase(start,a.MagicVisual,pitch,a.AttackAt-(a.LastShot-release),windup,release,rate,Ready(a.MagicVisual,pitch));
                weightPrimary=initial+(1-initial)*ramp;
            }
            Phrase(output,a.MagicVisual,pitch,age,windup,release,rate,start);
            if(a.LastShot>=a.AttackAt-1e-6f&&a.LastShot<=time+1e-6f&&age>release)weightPrimary*=Hold(time-a.LastShot);
            return weightPrimary;
        }
        public static void Apply(CapturedRig.Pose p,Actor a,float time,bool firstPerson,float[] channels,float guardWeight=-1)
        {
            // Remove the captured sword carry from the torso and arms, retain travel-aware lower body.
            for(int b=CapturedRig.Spine;b<=CapturedRig.RHand;b++){p.Position[b]=CapturedRig.Bones[b].Position;p.Rotation[b]=Quaternion.Identity;}
            p.Rotation[CapturedRig.Pelvis]=Quaternion.Identity;
            float pitch=firstPerson?0:a.Pitch*(float)Math.PI/180;
            p.Rotation[CapturedRig.LArm]=CapturedRig.Euler(-.24f-pitch*.08f,-.08f,.16f);
            p.Rotation[CapturedRig.LElbow]=CapturedRig.Euler(-.42f);
            p.Rotation[CapturedRig.RArm]=CapturedRig.Euler(-.12f,0,-.08f);
            p.Rotation[CapturedRig.RElbow]=CapturedRig.Euler(-.3f);
            float c=Sample(a,time,pitch,firstPerson,channels),tilt=.12f;
            if(c>.001f)
            {
                var o=channels;float twist=o[7]*(a.MagicVisual==4?1.15f:a.MagicVisual==2?1.1f:1)*c;
                p.Rotation[CapturedRig.Pelvis]=CapturedRig.Euler(0,twist*.35f);
                p.Rotation[CapturedRig.Spine]=CapturedRig.Euler(0,twist*.65f);
                p.Rotation[CapturedRig.Chest]=CapturedRig.Euler(0,twist*.48f);
                p.Rotation[CapturedRig.Head]=CapturedRig.Euler(0,-twist*.55f);
                p.Rotation[CapturedRig.Body]=CapturedRig.Euler(o[8]*c);
                p.Position[CapturedRig.Body]+=CapturedRig.V(0,-o[10]*c,o[9]*c);
                p.Rotation[CapturedRig.RClav]=CapturedRig.Euler(0,-.12f*c);
                p.Rotation[CapturedRig.LArm]*=CapturedRig.Euler(o[12]*c*(firstPerson?.5f:1));
                p.Rotation[CapturedRig.RArm]=Quaternion.Slerp(p.Rotation[CapturedRig.RArm],CapturedRig.Euler(o[0],o[1],o[2]),c);
                p.Rotation[CapturedRig.RElbow]=Quaternion.Slerp(p.Rotation[CapturedRig.RElbow],CapturedRig.Euler(o[3]),c);
                p.Rotation[CapturedRig.RHand]=Quaternion.Slerp(Quaternion.Identity,CapturedRig.Euler(o[4],o[5],o[6]),c);
                tilt+=CastTilt(a,time,firstPerson,o,c);
            }
            Staff(p,a,time,tilt,0,guardWeight);
        }
        // Staff tilt of the cast phrase over its weight `c`: dips back on the coil, tips toward the target through the release.
        public static float CastTilt(Actor a,float time,bool firstPerson,float[] o,float c)
        {
            float release=NativeAbilityRules.PrimaryWindup(a.MagicVisual,a.CastRate),windup=release*.6f,age=time-a.AttackAt;
            float coil=age<windup?Smooth(age/Math.Max(.01f,windup)):1-Smooth((age-windup)/Math.Max(.01f,release-windup));
            float drive=age>=windup?Smooth((age-windup)/Math.Max(.01f,release-windup))*(1-Smooth((age-release-.1f)/.3f)):0;
            return a.AbilityAt>a.AttackAt?o[11]*c*(firstPerson?.5f:1):(-.2f*coil+.12f*drive)*c;
        }
        private static readonly int[] ArmBones={CapturedRig.LArm,CapturedRig.LElbow,CapturedRig.LHand,CapturedRig.RArm,CapturedRig.RElbow,CapturedRig.RHand};
        private static readonly Quaternion[] SavedArms=new Quaternion[6];
        // The staff held in chest space (pitched `tiltX` from upright, rolled `rollZ`: the magic gait's stride sway), then the
        // staff guard over it.
        public static void Staff(CapturedRig.Pose p,Actor a,float time,float tiltX,float rollZ,float guardWeight)
        {
            // The shaft's +z points up in chest space, independent of the carrying elbow.
            p.World();var chest=p.WorldRotation[CapturedRig.Chest];
            int parent=CapturedRig.Bones[CapturedRig.LHand].Parent;
            p.Rotation[CapturedRig.LHand]=Quaternion.Inverse(p.WorldRotation[parent])*chest*CapturedRig.Euler(-(float)Math.PI/2+tiltX,0,rollZ);
            float g=guardWeight<0?(time<a.BlockUntil?1:0):MathUtil.Clamp(guardWeight,0,1);
            if(g>.001f)
            {
                p.World();var carryWrist=p.WorldRotation[CapturedRig.LHand];
                var spine=NativeFirstPersonPose.EulerXYZ(p.Rotation[CapturedRig.Spine]);p.Rotation[CapturedRig.Spine]=CapturedRig.Euler(spine.X,spine.Y*(1-g),spine.Z);
                p.Rotation[CapturedRig.Chest]*=CapturedRig.Euler(.1f*g);p.Rotation[CapturedRig.Head]*=CapturedRig.Euler(.15f*g);p.World();
                var guardChest=p.WorldRotation[CapturedRig.Chest];
                var saved=SavedArms;var armBones=ArmBones;
                for(int i=0;i<6;i++)saved[i]=p.Rotation[armBones[i]];
                var wrist=CapturedRig.Rot(CapturedRig.Y,-(float)Math.PI/2)*CapturedRig.Rot(CapturedRig.X,-.25f);
                Vector3 left=CapturedRig.V(.2f,.22f,.34f),axis=Vector3.Transform(CapturedRig.Z,wrist);
                CapturedRig.Arm(p,true,left,wrist,CapturedRig.V(1,-.8f,-.25f));
                CapturedRig.Arm(p,false,left+axis*.4f,CapturedRig.Euler(-1.5f,0,.2f),CapturedRig.V(-1,-.7f,-.2f));
                for(int i=0;i<6;i++)p.Rotation[armBones[i]]=Quaternion.Slerp(saved[i],p.Rotation[armBones[i]],g);
                // Hold staff orientation in chest/world space, as orientHeld does in the source;
                // interpolating each joint alone makes the crystal arc through the reticle.
                p.World();p.Rotation[CapturedRig.LHand]=Quaternion.Inverse(p.WorldRotation[parent])*Quaternion.Slerp(carryWrist,guardChest*wrist,g);
            }
            p.World();
        }
    }
    // A third-person magic pilot as the source's sampleCharacter poses it (presentation only): motion-quality locomotion in
    // the magic stance (gait feet, body bob, sway and lean, the pelvis/spine/chest counter-twist, head, shoulders and arm
    // swing), the sprint lean, then magic-motion's magicLayer over that gait rather than over a reset rest pose: the staff
    // carry fades into the arm swing with speed, the cast phrase adds its twist, weight shift and right-arm drive, the staff
    // stays upright in chest space with its stride sway, then the staff guard. Finish runs the ground pass after the native
    // overlays (roll, reactions, recovery). The first-person view model keeps MagicPose.Apply.
    public sealed class MagicMotion
    {
        public readonly MeleeRig Rig=new MeleeRig();
        private readonly MeleeGait gait=new MeleeGait();
        private readonly MeleeView view=new MeleeView();
        private float last=-100,dt=1/60f,sprintBlend;private int viewId=int.MinValue;
        private static string Spec(int element)=>element==2?"water":element==3?"lightning":element==4?"darkness":"fire";
        // The caller sets Rig.GroupPosition / GroupYaw (source frame) first.
        public void Sample(Actor a,float time,CapturedRig.Pose pose,float[] channels,float guardWeight)
        {
            dt=last<-90||time<last||time-last>.35f?1/60f:Math.Min(.1f,time-last);last=time;
            const float D=(float)Math.PI/180;var v=view;var r=Rig;
            if(a.Id!=viewId){viewId=a.Id;v.Id=a.Id.ToString();}
            v.Spec=Spec(a.MagicVisual);v.Yaw=-a.Yaw*D;v.Pitch=a.Pitch*D;v.Grounded=a.Grounded;v.Hp=a.Hp;v.Sprinting=a.Sprinting;v.HitStunUntil=a.StunUntil;
            v.RollAt=a.RollAt;v.RollUntil=a.RollAt>-90?a.RollAt+a.VisualRollDuration:-100;
            // sampleCharacter: hitstun and rolls hold the stride (the action moves the root, not running steps).
            bool planted=v.HitStunUntil>time||v.Rolling(time);
            r.Reset();r.LegsAuthored=r.LegsLayered=r.MocapPosed=false;
            if(planted)r.Motion*=(float)Math.Exp(-MeleeMath.Clamp(dt,0,.05f)*22);
            v.Vel=planted?new Vector3(0,a.Velocity.y,0):new Vector3(-a.Velocity.x,a.Velocity.y,a.Velocity.z);
            gait.Locomotion(r,v,time,dt,planted);
            v.Vel=new Vector3(-a.Velocity.x,a.Velocity.y,a.Velocity.z);
            // melee-guard sprintLean: the magic specs have no stance lean, only the sprint lean.
            float run=MeleeMath.Ease((r.Motion-.1f)/.65f);
            sprintBlend+=((v.Sprinting?1:0)-sprintBlend)*(1-(float)Math.Exp(-Math.Min(.05f,Math.Max(0,dt))*10));
            if(sprintBlend>1e-4f)r.AddE(CapturedRig.Spine,.12f*sprintBlend*run,0,0);
            // magicLayer: the staff carry, faded out with speed so the arm swings with the gait.
            float pitch=v.Pitch,carry=MeleeMath.Clamp(1-MeleeMath.Ease((r.Motion-.10f)/.65f),0,1);
            r.Slerp(CapturedRig.LArm,MeleeMath.Euler(-.24f-pitch*.08f,-.08f,.16f),carry);r.Slerp(CapturedRig.LElbow,MeleeMath.Euler(-.42f,0,0),carry);r.SetE(CapturedRig.LHand,0,0,0);
            float c=MagicPose.Sample(a,time,pitch,false,channels),tilt=.12f;
            if(c>.001f)
            {
                // Hip/shoulder separation added over the gait (pelvis leads, chest follows, the head stays on the target).
                var o=channels;float twist=o[7]*(a.MagicVisual==4?1.15f:a.MagicVisual==2?1.1f:1)*c;
                r.AddE(CapturedRig.Pelvis,0,twist*.35f,0);r.AddE(CapturedRig.Spine,0,twist*.65f,0);r.AddE(CapturedRig.Chest,0,twist*.48f,0);
                var head=r.E[CapturedRig.Head];r.SetE(CapturedRig.Head,head.X,-twist*.55f,head.Z);
                r.AddE(CapturedRig.Body,o[8]*c,0,0);r.P[CapturedRig.Body]+=new Vector3(0,-o[10]*c,o[9]*c);
                var clav=r.E[CapturedRig.RClav];r.SetE(CapturedRig.RClav,clav.X,-.12f*c,clav.Z);r.AddE(CapturedRig.LArm,o[12]*c,0,0);
                r.Slerp(CapturedRig.RArm,MeleeMath.Euler(o[0],o[1],o[2]),c);r.Slerp(CapturedRig.RElbow,MeleeMath.Euler(o[3],0,0),c);r.Slerp(CapturedRig.RHand,MeleeMath.Euler(o[4],o[5],o[6]),c);
                tilt+=MagicPose.CastTilt(a,time,false,o,c);
            }
            r.WritePose(pose);
            // Staff upright in chest space with the small stride sway.
            float sway=MeleeMath.Clamp(r.Motion,0,1);
            MagicPose.Staff(pose,a,time,tilt+(float)Math.Cos(2*r.Stride)*.04f*sway,(float)Math.Sin(r.Stride)*.08f*sway,guardWeight);
        }
        // Ground pass after every layer: the pelvis follows the travel (chest counter-rotated) and the legs re-plant.
        public void Finish(CapturedRig.Pose pose,float time,bool skip)
        {
            Rig.ReadPose(pose);gait.GroundPass(Rig,view,time,dt,skip);Rig.WritePose(pose);pose.World();
        }
    }
}
