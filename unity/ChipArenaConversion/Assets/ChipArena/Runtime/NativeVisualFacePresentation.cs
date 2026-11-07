using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // Faces on the model (Core/NativeFace: owner 2026-10-30, "Characters are currently emotionless"). Each model body's face
    // meshes (the head, the lashes, the brows, the teeth: whichever carry the pack's Expression_* shapes, found by name once per
    // mesh) take the face's weights each time the body is posed: a pilot's (VisualBodyBind: in a match, the menus, the intros,
    // the draft, a revive) from its actor's state, a zombie's (ZombieModelBind) from its gait, its jaw, its hit flash and its
    // spit. A weight is written only when it moved half a point, and a hidden part (the head under the Sheldon hood) is left.
    // Presentation only.
    public sealed partial class ArenaPresentation
    {
        private sealed class VisualFaceRig
        {
            public NativeFace.Face Face;public SkinnedMeshRenderer[] Parts;public int[][] Index;public float[][] Shown;
        }
        private static readonly Dictionary<Mesh,int[]> faceShapeIndex=new Dictionary<Mesh,int[]>();
        /// <summary>A body's face (made on first use from its worn face meshes; no parts when its outfit shows no face).</summary>
        private static VisualFaceRig VisualFaceOf(VisualBody body,int id)
        {
            if(body.Face!=null)return body.Face;
            var parts=new List<SkinnedMeshRenderer>();var index=new List<int[]>();
            foreach(var r in body.All){if(!(r is SkinnedMeshRenderer s)||s.sharedMesh==null)continue;var map=VisualFaceIndex(s.sharedMesh);if(map==null)continue;parts.Add(s);index.Add(map);}
            var rig=new VisualFaceRig{Face=new NativeFace.Face(id),Parts=parts.ToArray(),Index=index.ToArray(),Shown=new float[parts.Count][]};
            for(int p=0;p<rig.Shown.Length;p++){rig.Shown[p]=new float[NativeFace.Count];for(int k=0;k<NativeFace.Count;k++)rig.Shown[p][k]=-1;}
            body.Face=rig;return rig;
        }
        // A mesh's blend shape index for each of NativeFace.Shapes ("BS_mesh.Expression_" + name; -1 where it has none), or null
        // when it has none of them; once per mesh.
        private static int[] VisualFaceIndex(Mesh mesh)
        {
            if(faceShapeIndex.TryGetValue(mesh,out var known))return known;int[] map=null;
            for(int b=0;b<mesh.blendShapeCount;b++)
            {
                string n=mesh.GetBlendShapeName(b);int dot=n.LastIndexOf('.');string tail=dot>=0?n.Substring(dot+1):n;
                if(!tail.StartsWith(NativeFace.Prefix,StringComparison.Ordinal))continue;int k=Array.IndexOf(NativeFace.Shapes,tail.Substring(NativeFace.Prefix.Length));if(k<0)continue;
                if(map==null){map=new int[NativeFace.Count];for(int i=0;i<map.Length;i++)map[i]=-1;}map[k]=b;
            }
            faceShapeIndex[mesh]=map;return map;
        }
        // The face's weights onto its shown parts, each written when it moved.
        private static void VisualFaceShow(VisualFaceRig rig)
        {
            var w=rig.Face.Weight;
            for(int p=0;p<rig.Parts.Length;p++)
            {
                var r=rig.Parts[p];if(r==null||!r.enabled||!r.gameObject.activeInHierarchy)continue;var index=rig.Index[p];var shown=rig.Shown[p];
                for(int k=0;k<index.Length;k++){int b=index[k];if(b<0||Mathf.Abs(w[k]-shown[k])<.5f)continue;shown[k]=w[k];r.SetBlendShapeWeight(b,w[k]);}
            }
        }
        /// <summary>A pilot body's face this pose, from its actor (`time` the actor's clock).</summary>
        private static void VisualFacePilot(VisualBody body,Actor a,float time,bool fresh)
        {
            var rig=VisualFaceOf(body,a.Id);if(rig.Parts.Length==0)return;if(fresh)rig.Face.Forget();
            var p=new NativeFace.Pilot{Time=time,Health=a.Hp/100f,Stamina=a.Stamina,SinceHit=time-a.ReactionAt,HitStrength=a.ReactionStrength,SinceAttack=time-a.LastAttack,
                SinceSpecial=time-a.SpecialAt,SinceAbility=time-a.AbilityAt,SinceShot=time-a.LastShot,SinceBlock=time-a.BlockAt,SinceRoll=time-a.RollAt,
                Arena=body.Arena,Alive=a.Alive,Hurt=time<a.FlashUntil,Aiming=a.Aiming,Sprinting=a.Sprinting,Stunned=time<a.StunUntil,Attacking=a.Attacking(time),Kills=a.Kills};
            NativeFace.PilotFace(rig.Face,p);VisualFaceShow(rig);
        }
        /// <summary>A zombie body's face this pose (its jaw the gait's, `flash` the hit flash's code: 1 none).</summary>
        private static void ZombieFace(ZombieBody z,float flash,float swell,bool fresh)
        {
            var rig=VisualFaceOf(z.Body,z.Id);if(rig.Parts.Length==0)return;if(fresh)rig.Face.Forget();
            NativeFace.ZombieFace(rig.Face,new NativeFace.Zombie{Time=Time.time,Jaw=z.Jaw,Busy=z.Busy,Swell=swell,Kind=z.Kind,Hurt=flash!=1,Dying=z.Dying});VisualFaceShow(rig);
        }
    }
}
