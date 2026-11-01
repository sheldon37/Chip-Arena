using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NQuaternion=System.Numerics.Quaternion;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class SourceMotionSet {public SourceMotionClip[] clips;}
    [Serializable] public sealed class SourceMotionClip {public string key;public float duration;public bool loop;public int frames;public float[] values,roots,cameras;}
    public static class NativeSourceMotion
    {
        private static Dictionary<string,SourceMotionClip> clips;
        // Decode the shared bank during startup, before any combat or replay can request a clip.
        public static void Prewarm()=>Ensure();
        private static void Ensure(){if(clips!=null)return;var data=JsonUtility.FromJson<SourceMotionSet>(NativeSourceModels.Decode("SourceMotion"));clips=new Dictionary<string,SourceMotionClip>();foreach(var c in data.clips){if(c.frames<2||c.duration<=0||c.values==null||c.values.Length!=c.frames*22*7||c.values.Any(v=>!MathUtil.Finite(v))||c.key.StartsWith("intro-")&&(c.roots==null||c.roots.Length!=c.frames*10||c.cameras==null||c.cameras.Length!=c.frames*10||c.roots.Any(v=>!MathUtil.Finite(v))||c.cameras.Any(v=>!MathUtil.Finite(v))))throw new InvalidOperationException("Invalid motion clip "+c.key);clips.Add(c.key,c);}
            // The weapon intros' clearance plans are built with the clips (NativeIntroMotion.Prepare), not on an entrance's first frame.
            foreach(var c in data.clips)if(c.key.StartsWith("intro-weapon-",StringComparison.Ordinal))NativeIntroMotion.Prepare(c.key.Substring(6),c.values,c.frames,c.duration);}
        public static int ClipCount{get{Ensure();return clips.Count;}}
        private static readonly Dictionary<string,NativeEntranceClip> entranceClips=new Dictionary<string,NativeEntranceClip>();
        /// <summary>A weapon signature's baked clip for the entrance director (made once per clip), or null.</summary>
        public static NativeEntranceClip EntranceClip(string style)
        {
            Ensure();if(style==null)return null;if(entranceClips.TryGetValue(style,out var made))return made;if(!clips.TryGetValue("intro-"+style,out var c))return null;
            made=new NativeEntranceClip(c.values,c.roots,c.frames,c.duration);entranceClips.Add(style,made);return made;
        }

        public static bool Sample(string key,float age,float duration,CapturedRig.Pose pose,bool closed=false)
        {
            Ensure();if(!clips.TryGetValue(key,out var clip))return false;float fraction=clip.loop?Mathf.Repeat(age,clip.duration)/clip.duration:Mathf.Clamp01(age/(duration>0?duration:clip.duration));NativeFighterPose.Frames(clip.frames,fraction,closed,out int a,out int b,out float t);
            for(int bone=0;bone<22;bone++){int first=(a*22+bone)*7,last=(b*22+bone)*7;var v=clip.values;var p=new NVector(v[first],v[first+1],v[first+2]);var q=new NQuaternion(v[first+3],v[first+4],v[first+5],v[first+6]);pose.Position[bone]=NVector.Lerp(p,new NVector(v[last],v[last+1],v[last+2]),t);pose.Rotation[bone]=NQuaternion.Slerp(q,new NQuaternion(v[last+3],v[last+4],v[last+5],v[last+6]),t);}return true;
        }
        // One pose for every intro frame (main thread only; NativeIntroMotion writes every joint), so the entrance allocates nothing per frame.
        private static readonly CapturedRig.Pose introPose=new CapturedRig.Pose();private static string introStyle,introKey;
        /// <summary>The pose Intro last wrote on its rig (the intro's visual body binds to it).</summary>
        public static CapturedRig.Pose IntroPose=>introPose;
        public static void Intro(CapturedAnimation.Player rig,string style,float age,Camera camera)
        {
            Ensure();if(style!=introStyle){introStyle=style;introKey="intro-"+style;}string key=introKey;if(!clips.TryGetValue(key,out var clip))return;
            // A non-finite clock (never produced by the match or preview clocks) holds the last frame instead of writing NaN.
            if(!MathUtil.Finite(age)){NativePoseWriter.Warn("the intro rig (non-finite age)");return;}
            var pose=introPose;NativeIntroMotion.Pose(style,clip.values,clip.frames,clip.duration,age,pose);NativePoseWriter.Write(rig.Bones,pose,"the intro rig");
            float index=Mathf.Clamp01(age/clip.duration)*(clip.frames-1);int a=Math.Min(clip.frames-1,(int)index)*10,b=Math.Min(clip.frames-1,(int)index+1)*10;float f=index-(int)index;
            float At(float[] v,int n)=>Mathf.Lerp(v[a+n],v[b+n],f);Vector3 V(float[] v,int n)=>new Vector3(-At(v,n),At(v,n+1),At(v,n+2));
            var roots=clip.roots;rig.Root.transform.position=V(roots,0);rig.Root.transform.rotation=Quaternion.Slerp(new Quaternion(roots[a+3],-roots[a+4],-roots[a+5],roots[a+6]),new Quaternion(roots[b+3],-roots[b+4],-roots[b+5],roots[b+6]),f);rig.Root.transform.localScale=new Vector3(At(roots,7),At(roots,8),At(roots,9));
            camera.transform.SetPositionAndRotation(V(clip.cameras,0),Quaternion.LookRotation(V(clip.cameras,3),V(clip.cameras,6)));camera.fieldOfView=At(clip.cameras,9);
        }
        public static void Pilot(CapturedRig.Pose pose,Actor a,float time)
        {
            if(a.SourceSpec=="siren")
            {
                string action=a.SourceAction,key="siren-"+(action=="grab"?"grab":!string.IsNullOrEmpty(action)?action+"-"+a.SourceSide:"idle-"+(a.SourceRanged?"ranged":"melee"));
                Sample(key,key.Contains("idle")?time:time-a.SourceAt,a.SourceUntil-a.SourceAt,pose);return;
            }
            Fighter(pose,a,time,false,out _);
        }
        /// <summary>
        /// Samples the DEV fighting style's own clip for this frame (NativeFighterPose.Clip: Demon Step, block, the action while
        /// it lasts, else the guard idle, also while travelling) into `pose`. False, with nothing written, for other specs and for
        /// the Emperor's dodge roll, which keeps the roll pose. `stunned` keeps the guard under a hit reaction. `closeWave` (the
        /// third-person fighter pose) ends the wavedash clip on its first pose (NativeFighterPose.Frames); first person keeps it.
        /// </summary>
        public static bool Fighter(CapturedRig.Pose pose,Actor a,float time,bool stunned,out string key,bool closeWave=false)
        {
            key=null;if(!NativeFighterPose.Handles(a.SourceSpec))return false;
            bool roll=a.Rolling(time),block=!stunned&&time<a.BlockUntil,acting=!stunned&&NativeFighterPose.Acting(a.SourceAction,a.SourceUntil,time);
            key=NativeFighterPose.Clip(a.SourceSpec,acting?a.SourceAction:null,a.SourceTotal,a.SourceIndex,roll,block);if(key==null)return false;
            float at,duration;
            if(roll){at=a.RollAt;duration=.3f;}else if(block){at=a.BlockAt;duration=a.BlockUntil-a.BlockAt;}
            else if(acting){at=a.SourceAt;duration=a.SourceAction=="charge"?1.4f:a.SourceUntil-a.SourceAt;}else{at=0;duration=0;}
            return Sample(key,time-at,duration,pose,closeWave&&NativeFighterPose.Closed(key));
        }
    }
}
