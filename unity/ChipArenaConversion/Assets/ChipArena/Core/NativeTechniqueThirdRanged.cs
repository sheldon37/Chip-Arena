using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The third set's pistol, machine gun, rifle and bow: effects, previews, sounds and poses.
    public sealed partial class NativeTechniqueFx
    {
        private static readonly string[] RangedBuilt={};
        private static bool ThirdRanged(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            switch(k.Kind){default:return false;}
        }
    }
    public sealed partial class NativeTechniquePreview
    {
        private static bool WriteRanged(NativeTechnique k,string id,Vector3 feet,Vector3 f,Vector3 r,Vector3 up,Vector3 hand,float t0,bool first){switch(id){default:return false;}}
        private static float PeriodRanged(string id){switch(id){default:return 0;}}
        private static bool AimRanged(NativeTechnique k,string id,int target,Vector3 at){switch(id){default:return false;}}
    }
    public static partial class NativeTechniqueSounds
    {
        private static int MomentRanged(string kind,int m,NativeKitSound[] o,ref bool atPoint){int n=0;switch(kind){default:return n;}}
    }
    public static partial class NativeVisualMotion
    {
        private static bool PoseRanged(Set set,NativeTechnique k,float time,float back,IClips clips,out Pick pick){pick=default;switch(k.Kind){default:return false;}}
    }
}
