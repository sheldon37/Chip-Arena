using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    internal static class CommunityClipExport
    {
        [Serializable] private sealed class Track { public string bone;public Quaternion[] rotations; }
        [Serializable] private sealed class Motion { public string action;public float seconds;public Track[] tracks; }

        [MenuItem("Assets/Chip Arena/Export weapon bone animation",true)]
        private static bool CanExport()=>Selection.activeObject is AnimationClip;

        [MenuItem("Assets/Chip Arena/Export weapon bone animation")]
        private static void Export()
        {
            var clip=(AnimationClip)Selection.activeObject;
            var keyed=new HashSet<string>(AnimationUtility.GetCurveBindings(clip)
                .Where(b=>b.type==typeof(Transform)&&(b.propertyName.StartsWith("m_LocalRotation",StringComparison.Ordinal)||
                    b.propertyName.IndexOf("Euler",StringComparison.OrdinalIgnoreCase)>=0))
                .Select(b=>b.path),StringComparer.Ordinal);
            var bones=CapturedRig.Bones;
            var tracks=bones.Where(b=>keyed.Contains(b.Path)).Select(b=>new Track{bone=b.Name}).ToArray();
            if(tracks.Length==0||clip.length<=0||clip.length>60)
                throw new InvalidOperationException("Expected a 0–60 second generic clip with rotation curves on the Chip Arena motion-root rig");
            string file=EditorUtility.SaveFilePanel("Export weapon animation","","attack","json");
            if(string.IsNullOrEmpty(file))return;
            var root=new GameObject("Export rig"){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                var transforms=new Transform[bones.Length];
                var byName=new Dictionary<string,Transform>(StringComparer.Ordinal);
                for(int i=0;i<bones.Length;i++)
                {
                    var b=bones[i];var t=new GameObject(b.Name).transform;
                    t.SetParent(b.Parent<0?root.transform:transforms[b.Parent],false);
                    transforms[i]=t;byName.Add(b.Name,t);
                }
                int samples=Mathf.Clamp(Mathf.CeilToInt(clip.length*Mathf.Clamp(clip.frameRate,1,60))+1,2,3600);
                foreach(var track in tracks)track.rotations=new Quaternion[samples];
                for(int i=0;i<samples;i++)
                {
                    clip.SampleAnimation(root,clip.length*i/(samples-1));
                    foreach(var track in tracks)track.rotations[i]=byName[track.bone].localRotation.normalized;
                }
                File.WriteAllText(file,JsonUtility.ToJson(new Motion{action="attack",seconds=clip.length,tracks=tracks},true));
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
