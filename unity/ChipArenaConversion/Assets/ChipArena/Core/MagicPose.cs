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
        public static void Apply(CapturedRig.Pose p,Actor a,float time,bool firstPerson,float[] channels)
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
                float release=NativeAbilityRules.PrimaryWindup(a.MagicVisual,a.CastRate),windup=release*.6f,age=time-a.AttackAt;
                float coil=age<windup?Smooth(age/Math.Max(.01f,windup)):1-Smooth((age-windup)/Math.Max(.01f,release-windup));
                float drive=age>=windup?Smooth((age-windup)/Math.Max(.01f,release-windup))*(1-Smooth((age-release-.1f)/.3f)):0;
                tilt+=a.AbilityAt>a.AttackAt?o[11]*c*(firstPerson?.5f:1):(-.2f*coil+.12f*drive)*c;
            }
            // The shaft's +z points up in chest space, independent of the carrying elbow.
            p.World();var chest=p.WorldRotation[CapturedRig.Chest];
            int parent=CapturedRig.Bones[CapturedRig.LHand].Parent;
            p.Rotation[CapturedRig.LHand]=Quaternion.Inverse(p.WorldRotation[parent])*chest*CapturedRig.Euler(-(float)Math.PI/2+tilt);
            if(time<a.BlockUntil)
            {
                var wrist=CapturedRig.Rot(CapturedRig.Y,-(float)Math.PI/2)*CapturedRig.Rot(CapturedRig.X,-.25f);
                Vector3 left=CapturedRig.V(.2f,.22f,.34f),axis=Vector3.Transform(CapturedRig.Z,wrist);
                CapturedRig.Arm(p,true,left,wrist,CapturedRig.V(1,-.8f,-.25f));
                CapturedRig.Arm(p,false,left+axis*.4f,CapturedRig.Euler(-1.5f,0,.2f),CapturedRig.V(-1,-.7f,-.2f));
            }
            p.World();
        }
    }
}
