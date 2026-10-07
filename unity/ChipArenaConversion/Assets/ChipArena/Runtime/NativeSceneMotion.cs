using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeSceneLight {public string kind;public float[] values;}
    [Serializable] public sealed class NativeSceneClip {public string key;public float duration;public int frames,tracks,dropped=-1;public int[] victim;public float[] values,environment;public NativeSceneLight[] lights;}
    [Serializable] public sealed class NativeSceneClips {public NativeSceneClip[] clips;}
    public static class NativeSceneMotion
    {
        private static Dictionary<string,NativeSceneClip> clips;
        private static void Ensure(){if(clips!=null)return;var set=JsonUtility.FromJson<NativeSceneClips>(NativeSourceModels.Decode("SceneMotion"));clips=new Dictionary<string,NativeSceneClip>();foreach(var c in set.clips){if(c.frames<2||c.tracks<1||c.duration<=0||c.values.Length!=c.frames*c.tracks*20||c.values.Any(v=>!MathUtil.Finite(v))||c.victim.Any(v=>v<0||v>=c.tracks)||c.environment==null||c.environment.Length!=c.frames*8||c.environment.Any(v=>!MathUtil.Finite(v))||c.lights==null||c.lights.Any(l=>l.values.Length!=c.frames*11||l.values.Any(v=>!MathUtil.Finite(v))))throw new InvalidOperationException("Invalid cosmetic scene: "+c.key);clips.Add(c.key,c);}}
        public static int Count{get{Ensure();return clips.Count;}}
        public static NativeSceneClip Get(string key){Ensure();return clips.TryGetValue(key,out var clip)?clip:null;}
        public static Matrix4x4 Matrix(NativeSceneClip c,int track,float age)
        {if(track<0||track>=c.tracks)return Matrix4x4.identity;float frame=Mathf.Clamp01(age/c.duration)*(c.frames-1);int a=(track*c.frames+(int)frame)*20,b=(track*c.frames+Math.Min(c.frames-1,(int)frame+1))*20;float blend=frame-(int)frame;var m=new Matrix4x4();for(int i=0;i<16;i++){int row=i%4,column=i/4;float sign=(row==0?-1:1)*(column==0?-1:1);m[row,column]=Mathf.Lerp(c.values[a+i],c.values[b+i],blend)*sign;}return m;}
        public static Color Tint(NativeSceneClip c,int track,float age){if(track<0||track>=c.tracks)return Color.white;int f=Math.Min(c.frames-1,(int)(Mathf.Clamp01(age/c.duration)*(c.frames-1))),i=(track*c.frames+f)*20+17;return new Color(c.values[i],c.values[i+1],c.values[i+2]);}
        public static float Opacity(NativeSceneClip c,int track,float age)
        {if(track<0||track>=c.tracks)return 1;int frame=Math.Min(c.frames-1,(int)(Mathf.Clamp01(age/c.duration)*(c.frames-1)));return c.values[(track*c.frames+frame)*20+16];}
    }
    public sealed partial class ArenaPresentation
    {
        private bool SourceStage(string style,float age,Camera camera)
        {string key="stage-"+style;var clip=NativeSceneMotion.Get(key);if(clip==null)return false;NativeLighting.Intro(clip,age,camera);sourceModels.Queue(key,Matrix4x4.identity,b=>NativeSceneMotion.Matrix(clip,b,age),b=>NativeSceneMotion.Opacity(clip,b,age)>.001f,partOpacity:b=>NativeSceneMotion.Opacity(clip,b,age),partTint:b=>NativeSceneMotion.Tint(clip,b,age));return true;}
        private readonly Dictionary<int,(int seed,Matrix4x4[] bones)> carriedDeaths=new Dictionary<int,(int,Matrix4x4[])>();
        private bool SourceDeathScene(NativeDeath death,float age,float revival=-1)
        {
            if(death.kept&&death.style!="siren-crush")return false;if(death.kept)age=Math.Min(age,2.6f);string key="death-"+death.style;var clip=NativeSceneMotion.Get(key);if(clip==null)return false;Matrix4x4 root=Matrix4x4.TRS(death.position-Vector3.up*.865f,Quaternion.Euler(0,death.yaw,0),Vector3.one);
            float fade=revival>=0?1-Mathf.Clamp01(revival/.8f):1;sourceModels.Queue(key,root,b=>root*NativeSceneMotion.Matrix(clip,b,age),b=>NativeSceneMotion.Opacity(clip,b,age)>.001f,opacity:fade,partOpacity:b=>NativeSceneMotion.Opacity(clip,b,age),partTint:b=>NativeSceneMotion.Tint(clip,b,age));
            if(clip.dropped>=0&&!string.IsNullOrEmpty(death.actor?.SourceSpec))sourceModels.Queue("loose-"+death.actor.SourceSpec,root*NativeSceneMotion.Matrix(clip,clip.dropped,age),opacity:fade*NativeSceneMotion.Opacity(clip,clip.dropped,age));
            if(clip.victim.Length==22)
            {
                if(!carriedDeaths.TryGetValue(death.id,out var carried)||carried.seed!=death.seed){var matrices=new Matrix4x4[22];if(rigs.TryGetValue(death.id,out var rig)||minionRigs.TryGetValue(death.id,out rig))for(int i=0;i<22;i++)matrices[i]=rig.Bones[i].localToWorldMatrix;else for(int i=0;i<22;i++)matrices[i]=root*NativeSceneMotion.Matrix(clip,clip.victim[i],0);carried=(death.seed,matrices);carriedDeaths[death.id]=carried;}
                Matrix4x4 Bone(int b){var goal=root*NativeSceneMotion.Matrix(clip,clip.victim[b],age);float t=Mathf.Clamp01(age/(death.style=="siren-crush"?.08f:.2f));t=t*t*t*(t*(t*6-15)+10);if(t<1)for(int i=0;i<16;i++)goal[i]=Mathf.Lerp(carried.bones[b][i],goal[i],t);return goal;}
                bool Visible(int b)=>NativeSceneMotion.Opacity(clip,clip.victim[b],age)>.001f;
                float alpha=fade*(death.style=="ghost"?Math.Max(0,.6f-age*.12f):1);Color body=BodyTint(death.actor?.BodyColor),accent=AccentTint(death.actor?.AccentColor);if(death.style=="siren-crush"){body=Color.Lerp(body,NativeArena.ColorOf(0x0c0a10),.85f);accent=Color.Lerp(accent,NativeArena.ColorOf(0x0c0a10),.85f);}
                sourceModels.Queue(death.model??"pilot",root,Bone,Visible,body:body,accent:accent,opacity:alpha);
                if(!string.IsNullOrEmpty(death.actor?.Hat))sourceModels.Queue("hat-"+death.actor.Hat,root,Bone,Visible,opacity:alpha);
                bool weapon=death.style!="dev-bass-van"&&death.style!="dev-raging-demon"&&(death.style!="dev-clown"||age<.3f);if(weapon&&!string.IsNullOrEmpty(death.actor?.SourceSpec))sourceModels.Queue("weapon-"+death.actor.SourceSpec,root,Bone,Visible,opacity:alpha);
            }
            return true;
        }
    }
}
