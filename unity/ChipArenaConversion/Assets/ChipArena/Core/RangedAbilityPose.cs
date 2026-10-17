using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // Source support-hand gesture keys from ranged-motion.ts; animation never changes a shot.
    public static class RangedAbilityPose
    {
        private static readonly float[] Keys=new float[6];private static readonly Vector3[] Points=new Vector3[6];
        public static void Apply(CapturedRig.Pose pose,Actor a,float time,bool firstPerson)
        {
            // The pistol / machine-gun Q kit (Core/NativeGunKitMotion: Fan the Hammer, Overdrive) on both gun rig routes.
            NativeGunKitMotion.Apply(pose,a,time,firstPerson);
            float age=time-a.AbilityAt,release=a.AbilityRelease-a.AbilityAt,rel=time-a.AbilityRelease;
            if(age<0||age>3||a.AbilityId==null)return;
            if(a.AbilityId=="rocket")
            {
                float kick=rel<0?0:rel<.03f?rel/.03f:(float)Math.Pow(Math.Max(0,1-(rel-.03f)/.32f),2);
                pose.Rotation[CapturedRig.Body]*=CapturedRig.Euler(-.09f*kick);
                pose.Position[CapturedRig.Body]+=CapturedRig.V(0,0,-.08f*kick);return;
            }
            bool frag=a.AbilityId=="frag-grenade";
            if(!frag&&(a.AbilityId!="orbital-mark"||firstPerson||rel<-.1f||rel>.25f)||frag&&rel>.55f)return;
            pose.World();var chest=pose.WorldRotation[CapturedRig.Chest];
            var grip=Vector3.Transform(pose.WorldPosition[CapturedRig.LHand]-pose.WorldPosition[CapturedRig.Chest],Quaternion.Inverse(chest));
            // The gesture keys in scratch (presentation runs on one thread; no allocation per frame).
            var keys=Keys;var points=Points;int count=frag?6:3;
            if(frag){keys[0]=0;keys[1]=.35f*release;keys[2]=.75f*release;keys[3]=release;keys[4]=release+.15f;keys[5]=release+.55f;
                points[0]=grip;points[1]=CapturedRig.V(.2f,.02f,.22f);points[2]=firstPerson?CapturedRig.V(.3f,.12f,.3f):CapturedRig.V(.26f,.46f,.06f);points[3]=CapturedRig.V(.12f,firstPerson?.2f:.38f,.44f);points[4]=CapturedRig.V(0,.06f,.36f);points[5]=grip;}
            else{keys[0]=release-.1f;keys[1]=release;keys[2]=release+.25f;points[0]=grip;points[1]=CapturedRig.V(-.02f,.5f,.13f);points[2]=grip;}
            int index=0;while(index<count-2&&age>keys[index+1])index++;
            float u=CapturedClip.Ease((age-keys[index])/Math.Max(.001f,keys[index+1]-keys[index]));
            Vector3 target=Vector3.Lerp(points[index],points[index+1],u);
            float weight=CapturedClip.Ease(frag?Math.Min(age/.05f,(release+.55f-age)/.15f):Math.Min((rel+.1f)/.06f,(.25f-rel)/.08f));
            var arm=pose.Rotation[CapturedRig.LArm];var elbow=pose.Rotation[CapturedRig.LElbow];var hand=pose.Rotation[CapturedRig.LHand];
            var orientation=Quaternion.Inverse(chest)*pose.WorldRotation[CapturedRig.RHand];
            CapturedRig.Arm(pose,true,target,orientation,CapturedRig.V(1,.6f,0));
            pose.Rotation[CapturedRig.LArm]=Quaternion.Slerp(arm,pose.Rotation[CapturedRig.LArm],weight);
            pose.Rotation[CapturedRig.LElbow]=Quaternion.Slerp(elbow,pose.Rotation[CapturedRig.LElbow],weight);
            pose.Rotation[CapturedRig.LHand]=Quaternion.Slerp(hand,pose.Rotation[CapturedRig.LHand],weight);
        }
    }
}
