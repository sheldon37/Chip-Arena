using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The third set's dispatch (docs/WEAPON-TECHNIQUES.md, "The third set"). Each family's effects, previews, sounds and poses are one file: NativeTechniqueThirdMelee, ...Ranged, ...Magic.
    // On the same terms as the first two sets: pure functions of the replicated record and the clock, thinned by Reduced and the low tier, reaching nothing but the drawer.
    public sealed partial class NativeTechniqueFx
    {
        /// <summary>Whether a third-set technique has its effect yet. The checks read this, so they cover each as it lands.</summary>
        public static bool ThirdBuilt(string id)=>Array.IndexOf(MeleeBuilt,id)>=0||Array.IndexOf(RangedBuilt,id)>=0||Array.IndexOf(MagicBuilt,id)>=0;
        /// <summary>When the wind-up ends, for a kind whose `Arm` is that time: the rules write `Arm` only at the release, so until then it reads as `Release`.</summary>
        public static float ArmAt(NativeTechnique k)=>k.Arm>k.At?k.Arm:k.Release;
        private static void Third(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            if(!ThirdMelee(s,scene,a,k,t,seed,q,low)&&!ThirdRanged(s,scene,a,k,t,seed,q,low))ThirdMagic(s,scene,a,k,t,seed,q,low);
        }
    }
    public sealed partial class NativeTechniquePreview
    {
        private static void ThirdWrite(NativeTechnique k,string id,Vector3 feet,Vector3 f,Vector3 r,Vector3 up,Vector3 hand,float t0,bool first)
        {
            if(!WriteMelee(k,id,feet,f,r,up,hand,t0,first)&&!WriteRanged(k,id,feet,f,r,up,hand,t0,first))WriteMagic(k,id,feet,f,r,up,hand,t0,first);
        }
        private static float ThirdPeriod(string id){float p=PeriodMelee(id);if(p>0)return p;p=PeriodRanged(id);if(p>0)return p;p=PeriodMagic(id);return p>0?p:3.4f;}
        // Whoever the preview has to aim at, for the moves that hunt someone.
        private void ThirdAim(NativeTechnique k,string id){if(TargetId<=0)return;if(!AimMelee(k,id,TargetId,TargetAt)&&!AimRanged(k,id,TargetId,TargetAt))AimMagic(k,id,TargetId,TargetAt);}
    }
    public static partial class NativeTechniqueSounds
    {
        // The rules' `Release` for these is the wind-up's end, when the move goes off. Their first cue belongs on the press, and the `Arm` cue on the going off.
        private static bool ThirdFromPress(string kind)=>kind==NativeTechniques.GunSpin||kind==NativeTechniques.Combust||kind==NativeTechniques.Thunderclap||kind==NativeTechniques.Overload;
        private static int ThirdMoment(string kind,int m,NativeKitSound[] o,ref bool atPoint)
        {
            int n=MomentMelee(kind,m,o,ref atPoint);if(n==0)n=MomentRanged(kind,m,o,ref atPoint);if(n==0)n=MomentMagic(kind,m,o,ref atPoint);return n;
        }
    }
    public static partial class NativeVisualMotion
    {
        private static bool ThirdPose(Set set,NativeTechnique k,float time,float back,IClips clips,out Pick pick)
        {
            return PoseMelee(set,k,time,back,clips,out pick)||PoseRanged(set,k,time,back,clips,out pick)||PoseMagic(set,k,time,back,clips,out pick);
        }
    }
}
