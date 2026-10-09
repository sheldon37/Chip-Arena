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
        private static void Ensure(){if(clips!=null)return;var data=JsonUtility.FromJson<SourceMotionSet>(NativeSourceModels.Decode("SourceMotion"));clips=new Dictionary<string,SourceMotionClip>();foreach(var c in data.clips){if(c.frames<2||c.duration<=0||c.values==null||c.values.Length!=c.frames*22*7||c.values.Any(v=>!MathUtil.Finite(v))||c.key.StartsWith("intro-")&&(c.roots==null||c.roots.Length!=c.frames*10||c.cameras==null||c.cameras.Length!=c.frames*10||c.roots.Any(v=>!MathUtil.Finite(v))||c.cameras.Any(v=>!MathUtil.Finite(v))))throw new InvalidOperationException("Invalid motion clip "+c.key);clips.Add(c.key,c);}}
        public static int ClipCount{get{Ensure();return clips.Count;}}
        public static bool Sample(string key,float age,float duration,CapturedRig.Pose pose)
        {
            Ensure();if(!clips.TryGetValue(key,out var clip))return false;float fraction=clip.loop?Mathf.Repeat(age,clip.duration)/clip.duration:Mathf.Clamp01(age/(duration>0?duration:clip.duration));float index=fraction*(clip.frames-1);int a=Math.Min(clip.frames-1,(int)index),b=Math.Min(clip.frames-1,a+1);float t=index-a;
            for(int bone=0;bone<22;bone++){int first=(a*22+bone)*7,last=(b*22+bone)*7;var v=clip.values;var p=new NVector(v[first],v[first+1],v[first+2]);var q=new NQuaternion(v[first+3],v[first+4],v[first+5],v[first+6]);pose.Position[bone]=NVector.Lerp(p,new NVector(v[last],v[last+1],v[last+2]),t);pose.Rotation[bone]=NQuaternion.Slerp(q,new NQuaternion(v[last+3],v[last+4],v[last+5],v[last+6]),t);}return true;
        }
        public static void Intro(CapturedAnimation.Player rig,string style,float age,Camera camera)
        {
            Ensure();string key="intro-"+style;if(!clips.TryGetValue(key,out var clip))return;var pose=new CapturedRig.Pose();Sample(key,age,clip.duration,pose);
            for(int i=0;i<22;i++){rig.Bones[i].localPosition=CapturedAnimation.Vec(CapturedRig.Mirror(pose.Position[i]));rig.Bones[i].localRotation=CapturedAnimation.Quat(CapturedRig.Mirror(pose.Rotation[i]));}
            float index=Mathf.Clamp01(age/clip.duration)*(clip.frames-1);int a=Math.Min(clip.frames-1,(int)index)*10,b=Math.Min(clip.frames-1,(int)index+1)*10;float f=index-(int)index;
            float At(float[] v,int n)=>Mathf.Lerp(v[a+n],v[b+n],f);Vector3 V(float[] v,int n)=>new Vector3(-At(v,n),At(v,n+1),At(v,n+2));
            var roots=clip.roots;rig.Root.transform.position=V(roots,0);rig.Root.transform.rotation=Quaternion.Slerp(new Quaternion(roots[a+3],-roots[a+4],-roots[a+5],roots[a+6]),new Quaternion(roots[b+3],-roots[b+4],-roots[b+5],roots[b+6]),f);rig.Root.transform.localScale=new Vector3(At(roots,7),At(roots,8),At(roots,9));
            camera.transform.SetPositionAndRotation(V(clip.cameras,0),Quaternion.LookRotation(V(clip.cameras,3),V(clip.cameras,6)));camera.fieldOfView=At(clip.cameras,9);
        }
        // The DEV fighting styles (bare fists, no blade): their poses come from the source clips and the fighter travel.
        public static bool Fighter(string spec)=>spec=="emperor"||spec=="ansatsuken";
        // `phase` is the travel stride (cycles) while the pilot moves on foot, else negative; `speed` its ground speed.
        public static void Pilot(CapturedRig.Pose pose,Actor a,float time,float phase=-1,float speed=0)
        {
            if(a.SourceSpec!="emperor"&&a.SourceSpec!="ansatsuken"&&a.SourceSpec!="siren")return;
            string key=a.SourceSpec+"-idle",action=a.SourceAction;float at=a.SourceAt,duration=a.SourceUntil-at;
            if(a.SourceSpec=="siren")key="siren-"+(action=="grab"?"grab":!string.IsNullOrEmpty(action)?action+"-"+a.SourceSide:"idle-"+(a.SourceRanged?"ranged":"melee"));
            else if(a.SourceSpec=="ansatsuken"&&a.Rolling(time)){key="ansatsuken-step";at=a.RollAt;duration=.3f;}
            else if(time<a.BlockUntil){key=a.SourceSpec+"-block";at=a.BlockAt;duration=a.BlockUntil-a.BlockAt;}
            else if(!string.IsNullOrEmpty(action)){key=a.SourceSpec+"-"+action+"-"+(action=="charge"||action=="wave"||action=="hadoken"||action=="shinku"||action=="electric"?1:a.SourceTotal)+"-"+(action=="jab"||action=="combo"?a.SourceIndex:0);if(action=="charge")duration=1.4f;}
            if(!string.IsNullOrEmpty(action)||time<a.BlockUntil||a.Rolling(time)||a.SourceSpec=="siren"||a.Velocity.Length<.3f)Sample(key,key.Contains("idle")?time:time-at,duration,pose);
            // Travelling (or airborne) without an action: the guard over the gait's feet, fading into the unarmed running
            // carry, never the sword set's run the base pose sampled (MeleeGait.FighterTravel).
            else if(Sample(key,time,0,travelGuard))MeleeGait.FighterTravel(pose,travelGuard,Mathf.Max(0,phase),phase<0?0:speed);
        }
        private static readonly CapturedRig.Pose travelGuard=new CapturedRig.Pose();
    }
}
