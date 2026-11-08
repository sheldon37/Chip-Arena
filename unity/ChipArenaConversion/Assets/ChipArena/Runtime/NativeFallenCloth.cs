using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // NOCTIEL's robe and hood as cloth (Core NativeFallenRobe and NativeFallenHood; the owner, 2026-11-07). Each phase's skirt and the hood are skinned
    // renderers on the body's own skeleton, in the bake's mesh space beside its body mesh (the same bind poses), drawn with NOCTIEL's material; each
    // one's paint map is registered by name and it is handed to the licensed cloth simulation through NativeVisualCloth (a local bridge), one handle
    // each so only what's shown runs. Without the bridge they are skinned and follow the body. Phase 3 keeps the old rigid remnant at the waist and
    // the hood; phase 4 has neither. The reflection in the water is each baked each frame and drawn mirrored. Presentation only: nothing is
    // networked or read back.
    public sealed partial class ArenaPresentation
    {
        private sealed class FbClothPiece{public string Name;public GameObject Root;public SkinnedMeshRenderer Renderer;public Mesh Mesh,Baked;public Texture2D Paint;public object Handle;public bool Shown,Running;}
        /// <summary>Phase 1's skirt, phase 2's, the hood.</summary>
        private readonly FbClothPiece[] fbCloth=new FbClothPiece[3];private MaterialPropertyBlock fbClothBlock;private Vector3 fbClothLast;private bool fbClothKnown;
        private const int FbHood=2;
        /// <summary>Past this distance from the eye (m) the cloth holds as drawn: it's a giant, seen from far.</summary>
        private const float FbClothFar=90;
        /// <summary>The skirt is on: the old strips aren't drawn in phases 1 and 2.</summary>
        private bool FallenClothOn=>fbCloth[0]!=null;
        /// <summary>The hood is on: the rigid cowl isn't drawn in phases 1 to 3.</summary>
        private bool FallenHoodOn=>fbCloth[FbHood]!=null;
        private void FallenClothBuild(SkinnedMeshRenderer most,Transform skeleton)
        {
            fbClothBlock=new MaterialPropertyBlock();
            for(int k=0;k<2;k++)fbCloth[k]=FallenClothPiece(NativeFallenRobe.Name+" "+(k+1),NativeFallenRobe.Build(k+1),NativeFallenRobe.Bones,NativeFallenRobe.Paint(4,32),4,32,most,skeleton);
            if(fbCloth[0]==null||fbCloth[1]==null){for(int k=0;k<2;k++)FallenClothDrop(k);Debug.LogWarning("NOCTIEL: the robe's bones are missing; the strips stay");}
            fbCloth[FbHood]=FallenClothPiece(NativeFallenHood.Name,NativeFallenHood.Build(),NativeFallenHood.Bones,NativeFallenHood.Paint(4,64),4,64,most,skeleton);
            Debug.Log("NOCTIEL: the robe is cloth "+(FallenClothOn?fbCloth[0].Handle!=null?"(simulated)":"(skinned; no cloth bridge)":"(off)")+", the hood "+(FallenHoodOn?fbCloth[FbHood].Handle!=null?"(simulated)":"(skinned)":"(off: the cowl stays)"));
        }
        // One cloth: a skinned renderer on the named bones under its own root beside the body mesh (so the simulation takes it alone, never the bake's
        // hidden pieces), its paint map registered by its name, attached to the simulation when the bridge is there. Null without its bones.
        private FbClothPiece FallenClothPiece(string name,NativeFallenRobe.Grid g,string[] boneNames,uint[] px,int pw,int ph,SkinnedMeshRenderer most,Transform skeleton)
        {
            var bones=new Transform[boneNames.Length];var binds=new Matrix4x4[bones.Length];
            for(int i=0;i<bones.Length;i++){var bn=boneNames[i];if(!fbBone.TryGetValue(bn,out bones[i])||bones[i]==null||!fbBind.TryGetValue(bn,out binds[i])){Debug.LogWarning("NOCTIEL: no "+bn+" for "+name);return null;}}
            int n=g.Positions.Length;var v=new Vector3[n];var nm=new Vector3[n];var uv=new Vector2[n];var c=new Color32[n];var w=new BoneWeight[n];
            for(int i=0;i<n;i++)
            {
                var p=g.Positions[i];v[i]=new Vector3(p.X,p.Y,p.Z);var q=g.Normals[i];nm[i]=new Vector3(q.X,q.Y,q.Z);uv[i]=new Vector2(g.Uv[i].X,g.Uv[i].Y);uint h=g.Colors[i];c[i]=new Color32((byte)(h>>16),(byte)(h>>8),(byte)h,g.Alpha[i]);
                float t=g.Weight1[i];w[i]=t>0?new BoneWeight{boneIndex0=0,weight0=1-t,boneIndex1=g.Bone1[i],weight1=t}:new BoneWeight{boneIndex0=0,weight0=1};
            }
            var mesh=new Mesh{name=name,hideFlags=HideFlags.HideAndDontSave};mesh.SetVertices(v);mesh.SetNormals(nm);mesh.SetUVs(0,uv);mesh.SetColors(c);mesh.SetTriangles(g.Triangles,0);mesh.boneWeights=w;mesh.bindposes=binds;mesh.RecalculateBounds();
            var root=new GameObject(name+" cloth"){hideFlags=HideFlags.DontSave};root.transform.SetParent(most.transform.parent,false);
            var go=new GameObject(name){hideFlags=HideFlags.DontSave};go.transform.SetParent(root.transform,false);go.transform.localPosition=most.transform.localPosition;go.transform.localRotation=most.transform.localRotation;go.transform.localScale=most.transform.localScale;
            var smr=go.AddComponent<SkinnedMeshRenderer>();smr.sharedMesh=mesh;smr.bones=bones;smr.rootBone=bones[0];smr.sharedMaterial=fbBody;smr.updateWhenOffscreen=true;smr.shadowCastingMode=ShadowCastingMode.Off;smr.receiveShadows=false;smr.lightProbeUsage=LightProbeUsage.Off;smr.reflectionProbeUsage=ReflectionProbeUsage.Off;smr.enabled=false;
            var paint=new Texture2D(pw,ph,TextureFormat.RGBA32,false){name=name+" paint",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            var pc=new Color32[px.Length];for(int i=0;i<px.Length;i++)pc[i]=new Color32((byte)(px[i]>>16),(byte)(px[i]>>8),(byte)px[i],255);paint.SetPixels32(pc);paint.Apply(false,false);
            NativeVisualCloth.PaintMaps[name]=paint;
            object handle=null;
            if(NativeVisualCloth.Registered)
            {
                try{handle=NativeVisualCloth.Attach(root,skeleton);if(handle!=null)NativeVisualCloth.Enable(handle,false);}
                catch(Exception e){Debug.LogWarning("NOCTIEL: "+name+"'s cloth failed ("+e.Message+"); it stays skinned");handle=null;}
            }
            return new FbClothPiece{Name=name,Root=root,Renderer=smr,Mesh=mesh,Baked=new Mesh{name=name+" baked",hideFlags=HideFlags.HideAndDontSave},Paint=paint,Handle=handle};
        }
        /// <summary>After the pose: the phase's skirt shown and simulating (near and not on the low tier), the others off; drawn mirrored in the water.</summary>
        private void FallenClothFrame(FallenBossFrame f,Vector3 eye,float flash,Matrix4x4 mirror,bool shown)
        {
            if(!FallenClothOn&&!FallenHoodOn)return;
            int want=!shown?-1:f.Phase==1?0:f.Phase==2?1:-1;bool hood=shown&&f.Phase<=3;var at=fbRoot.transform.position;
            // A jump (a respawn, the preview looping, the body shown again) starts the cloth over from the pose.
            bool jump=!fbClothKnown||(at-fbClothLast).sqrMagnitude>64;fbClothLast=at;fbClothKnown=shown;
            bool near=!LowGraphics&&(eye-at).sqrMagnitude<FbClothFar*FbClothFar;
            for(int k=0;k<fbCloth.Length;k++)
            {
                var c=fbCloth[k];if(c==null)continue;bool show=k==FbHood?hood:k==want,run=show&&near&&c.Handle!=null;
                if(c.Renderer.enabled!=show)c.Renderer.enabled=show;
                if(c.Handle!=null)
                {
                    if(run!=c.Running){try{NativeVisualCloth.Enable(c.Handle,run);}catch(Exception e){Debug.LogWarning("NOCTIEL: cloth "+e.Message);c.Handle=null;}c.Running=run;if(run)jump=true;}
                    if(run&&jump&&c.Handle!=null){try{NativeVisualCloth.Reset(c.Handle);}catch(Exception e){Debug.LogWarning("NOCTIEL: cloth "+e.Message);c.Handle=null;}}
                }
                c.Shown=show;if(!show)continue;
                var block=fbClothBlock;block.Clear();block.SetFloat(FbGlowId,0);block.SetFloat(FbFlashId,flash);block.SetFloat(FbMirrorId,0);block.SetVector(FbClipId,FbNoCut);c.Renderer.SetPropertyBlock(block);
                // The reflection: the cloth as it is this frame, mirrored in the water.
                var tr=c.Renderer.transform;c.Renderer.BakeMesh(c.Baked);block.SetFloat(FbMirrorId,1);
                Graphics.DrawMesh(c.Baked,mirror*Matrix4x4.TRS(tr.position,tr.rotation,Vector3.one),fbBody,0,null,0,block,ShadowCastingMode.Off,false);FallenBossDraws++;
            }
        }
        private void FallenClothDrop(int k)
        {
            var c=fbCloth[k];if(c==null)return;fbCloth[k]=null;
            NativeVisualCloth.PaintMaps.Remove(c.Name);
            if(c.Root!=null)Destroy(c.Root);if(c.Mesh!=null)Destroy(c.Mesh);if(c.Baked!=null)Destroy(c.Baked);if(c.Paint!=null)Destroy(c.Paint);
        }
        private void FallenClothDispose(){for(int k=0;k<fbCloth.Length;k++)FallenClothDrop(k);fbClothKnown=false;}
    }
}
