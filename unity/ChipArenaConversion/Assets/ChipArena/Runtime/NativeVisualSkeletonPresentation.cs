using System;
using System.Collections.Generic;
using UnityEngine;
using NVector3=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The model's skeleton on its bodies (owner 2026-10-28: "Can you design a full skeleton inside the player (From skull to
    // toe) that can be visible during gibs + wounds? Ananomically correct and in the same style of the model"; Core
    // NativeModelSkeleton). One shared mesh, built from the bake's rest skeleton the first time a body needs it, skinned rigidly
    // to the body's own bones, in the body's toon coloured by its vertices, with its outline. Every model corpse wears it inside
    // (a cut shows its bones, NativeVisualCutPresentation.cs), and a living pilot's body while an open wound shows into it
    // (NativeVisualWoundPresentation.cs). Presentation only.
    public sealed partial class ArenaPresentation
    {
        private const string SkeletonRenderer="Bones.Skeleton";
        private static Mesh skeletonMesh;private static Material skeletonMaterial;private static string[] skeletonBones;private static byte[] skeletonKinds;private static bool skeletonFailed;

        // The shared mesh and material: the skeleton built at the bake's rest joints (root space), bound to them; the material a
        // copy of the body's toon drawing its vertex colours (a faint glow), with the outline. False without a bake or a bone.
        private static bool VisualSkeletonAssets()
        {
            if(skeletonMesh!=null)return true;if(skeletonFailed||visualBodyPrefab==null)return false;
            var root=visualBodyPrefab.transform;var skel=root.Find("VisualSkeleton");Material template=null;
            foreach(var r in visualBodyPrefab.GetComponentsInChildren<Renderer>(true))if(r.sharedMaterial!=null&&r.sharedMaterial.HasProperty(VertexAlbedoId)){template=r.sharedMaterial;break;}
            if(skel==null||template==null){skeletonFailed=true;Debug.LogWarning("Visual body: no skeleton or toon in the bake; corpses keep no bones.");return false;}
            var at=new Dictionary<string,Transform>();foreach(var t in skel.GetComponentsInChildren<Transform>(true))if(!at.ContainsKey(t.name))at[t.name]=t;
            var toRoot=root.worldToLocalMatrix;
            var sk=NativeModelSkeleton.Build(n=>{if(!at.TryGetValue(n,out var t))return null;var p=toRoot.MultiplyPoint3x4(t.position);return new NVector3(p.x,p.y,p.z);});
            if(sk==null){skeletonFailed=true;Debug.LogWarning("Visual body: the bake lacks a bone the skeleton needs; corpses keep no bones.");return false;}
            int n=sk.Positions.Count;var vertices=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];var weights=new BoneWeight[n];bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
            for(int i=0;i<n;i++)
            {
                var p=sk.Positions[i];var q=sk.Normals[i];var c=sk.Colors[i];vertices[i]=new Vector3(p.X,p.Y,p.Z);normals[i]=new Vector3(q.X,q.Y,q.Z);
                var col=new Color(c.X,c.Y,c.Z,c.W);colors[i]=linear?col.linear:col;colors[i].a=c.W;weights[i]=new BoneWeight{boneIndex0=sk.Bone[i],weight0=1};
            }
            var bind=new Matrix4x4[sk.Bones.Count];for(int b=0;b<bind.Length;b++)bind[b]=at[sk.Bones[b]].worldToLocalMatrix*root.localToWorldMatrix;
            var mesh=new Mesh{name="Visual skeleton",indexFormat=n>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16,hideFlags=HideFlags.HideAndDontSave};
            mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.boneWeights=weights;mesh.bindposes=bind;mesh.SetTriangles(sk.Indices,0);mesh.RecalculateBounds();
            var m=new Material(template){name="Visual skeleton",hideFlags=HideFlags.HideAndDontSave};m.SetTexture(TextureId,Texture2D.whiteTexture);
            m.SetFloat(VertexAlbedoId,.04f);m.SetFloat(UseCustomTextureId,0);m.SetFloat("_UseOutline",1);m.SetFloat("_Cull",2);m.SetFloat(CutoffId,0);m.SetFloat(AccentGlowId,0);
            skeletonBones=sk.Bones.ToArray();skeletonKinds=sk.Kind.ToArray();skeletonMaterial=m;skeletonMesh=mesh;return true;
        }
        // A body being made (`root`, its skeleton at rest): its skeleton's renderer on its own bones, or null.
        private static SkinnedMeshRenderer VisualSkeletonMake(GameObject root,Transform skeleton)
        {
            if(!VisualSkeletonAssets())return null;var named=new Dictionary<string,Transform>();
            foreach(var t in skeleton.GetComponentsInChildren<Transform>(true))if(!named.ContainsKey(t.name))named[t.name]=t;
            var bones=new Transform[skeletonBones.Length];for(int b=0;b<bones.Length;b++)if(!named.TryGetValue(skeletonBones[b],out bones[b]))return null;
            var go=new GameObject(SkeletonRenderer);go.transform.SetParent(root.transform,false);
            var r=go.AddComponent<SkinnedMeshRenderer>();r.sharedMesh=skeletonMesh;r.bones=bones;r.sharedMaterial=skeletonMaterial;r.localBounds=skeletonMesh.bounds;
            r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=true;r.skinnedMotionVectors=false;r.updateWhenOffscreen=true;return r;
        }
    }
}
