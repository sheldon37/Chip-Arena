using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The one way a captured rig's bones are written from a pose (CapturedRig.Pose, source frame). Unity rejects a non-finite
    /// localPosition or localRotation and logs "transform.localPosition assign attempt for '...' is not valid" for every bone
    /// on every frame it is fed; a pose with any non-finite joint (NativeIntroMotion.Finite) is not written at all, so the rig
    /// holds its last finite pose, and the first such pose of each owner is logged once.
    /// </summary>
    public static class NativePoseWriter
    {
        private static HashSet<string> warned;
        public static bool Write(Transform[] bones,CapturedRig.Pose pose,string owner)
        {
            if(!NativeIntroMotion.Finite(pose)){Warn(owner);return false;}
            int n=Mathf.Min(bones.Length,pose.Position.Length);
            for(int i=0;i<n;i++){bones[i].localPosition=CapturedAnimation.Vec(CapturedRig.Mirror(pose.Position[i]));bones[i].localRotation=CapturedAnimation.Quat(CapturedRig.Mirror(pose.Rotation[i]));}
            return true;
        }
        public static void Warn(string owner){warned??=new HashSet<string>();if(warned.Add(owner))Debug.LogWarning("Held the last finite pose of "+owner+": a non-finite pose was not written to its bones.");}
    }
}
