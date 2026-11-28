using System;
using System.Collections.Generic;
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
        private sealed class FbClothPiece{public string Name;public GameObject Root;public SkinnedMeshRenderer Renderer;public Mesh Mesh,Baked;public Texture2D Paint;public object Handle;public bool Shown,Running;public int[] Hem;public Vector3[] HemLast;public bool HemKnown;}
        /// <summary>Phase 1's skirt, phase 2's, the hood, phase 1's tabard and sash, phase 2's.</summary>
        private readonly FbClothPiece[] fbCloth=new FbClothPiece[5];private MaterialPropertyBlock fbClothBlock;private Vector3 fbClothLast;private bool fbClothKnown;
        private const int FbHood=2,FbTabard=3;
        // The motes the cloth sheds from its hems as it swings (the owner, 2026-11-08: "Also integrate effects with magica cloth"): pale light leaving the
        // simulated hems (read back from the cloth as drawn), more the faster a hem moves, drifting up and fading; embers off phase 2's burnt hems.
        private struct FbClothMote{public Vector3 At,Way;public float Age,Life,Size,Glow;}
        private const int FbClothMoteMax=160;private readonly FbClothMote[] fbClothMotes=new FbClothMote[FbClothMoteMax];private int fbClothMoteCount;private uint fbMoteSeed=0x9E3779B9u;private float fbMoteClock=-1;
        private readonly List<Vector3> fbClothVerts=new List<Vector3>();
        /// <summary>Motes a second from a hem point at rest, more per metre a second it moves (to a cap); how long they live.</summary>
        private const float FbMoteRest=.05f,FbMoteBySpeed=.32f,FbMoteFast=5f;
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
            for(int k=0;k<2;k++)fbCloth[FbTabard+k]=FallenClothPiece(NativeFallenTabard.Name+" "+(k+1),NativeFallenTabard.Build(k+1),NativeFallenTabard.Bones,NativeFallenTabard.Paint(32,64),32,64,most,skeleton);
            fbCloth[FbHood]=FallenClothPiece(NativeFallenHood.Name,NativeFallenHood.Build(),NativeFallenHood.Bones,NativeFallenHood.Paint(4,64),4,64,most,skeleton);
            Debug.Log("NOCTIEL: the robe is cloth "+(FallenClothOn?fbCloth[0].Handle!=null?"(simulated)":"(skinned; no cloth bridge)":"(off)")+", the hood "+(FallenHoodOn?fbCloth[FbHood].Handle!=null?"(simulated)":"(skinned)":"(off: the cowl stays)")+", the tabard "+(fbCloth[FbTabard]!=null?fbCloth[FbTabard].Handle!=null?"(simulated)":"(skinned)":"(off)"));
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
                float t=g.Weight1[i];int b0=g.Bone0!=null?g.Bone0[i]:0;w[i]=t>0&&g.Bone1[i]!=b0?new BoneWeight{boneIndex0=b0,weight0=1-t,boneIndex1=g.Bone1[i],weight1=t}:new BoneWeight{boneIndex0=b0,weight0=1};
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
            return new FbClothPiece{Name=name,Root=root,Renderer=smr,Mesh=mesh,Baked=new Mesh{name=name+" baked",hideFlags=HideFlags.HideAndDontSave},Paint=paint,Handle=handle,Hem=g.Hem??Array.Empty<int>(),HemLast=new Vector3[g.Hem!=null?g.Hem.Length:0]};
        }
        /// <summary>After the pose: the phase's skirt shown and simulating (near and not on the low tier), the others off; drawn mirrored in the water.</summary>
        private void FallenClothFrame(FallenBossFrame f,Vector3 origin,Vector3 eye,float flash,Matrix4x4 mirror,bool shown,float now)
        {
            float dt=fbMoteClock<0?0:Mathf.Clamp(now-fbMoteClock,0,.1f);fbMoteClock=now;FallenMotesStep(origin,dt);
            if(!FallenClothOn&&!FallenHoodOn)return;
            int want=!shown?-1:f.Phase==1?0:f.Phase==2?1:-1;bool hood=shown&&f.Phase<=3;var at=fbRoot.transform.position;
            // A jump (a respawn, the preview looping, the body shown again) starts the cloth over from the pose.
            bool jump=!fbClothKnown||(at-fbClothLast).sqrMagnitude>64;fbClothLast=at;fbClothKnown=shown;
            bool near=!LowGraphics&&(eye-at).sqrMagnitude<FbClothFar*FbClothFar;
            for(int k=0;k<fbCloth.Length;k++)
            {
                var c=fbCloth[k];if(c==null)continue;bool show=k==FbHood?hood:k>=FbTabard?k-FbTabard==want:k==want,run=show&&near&&c.Handle!=null;
                if(c.Renderer.enabled!=show)c.Renderer.enabled=show;
                if(c.Handle!=null)
                {
                    if(run!=c.Running){try{NativeVisualCloth.Enable(c.Handle,run);}catch(Exception e){Debug.LogWarning("NOCTIEL: cloth "+e.Message);c.Handle=null;}c.Running=run;if(run)jump=true;}
                    if(run&&jump&&c.Handle!=null){try{NativeVisualCloth.Reset(c.Handle);}catch(Exception e){Debug.LogWarning("NOCTIEL: cloth "+e.Message);c.Handle=null;}}
                }
                c.Shown=show;if(!show){c.HemKnown=false;continue;}
                var block=fbClothBlock;block.Clear();block.SetFloat(FbGlowId,0);block.SetFloat(FbFlashId,flash);block.SetFloat(FbMirrorId,0);block.SetVector(FbClipId,FbNoCut);c.Renderer.SetPropertyBlock(block);
                // The reflection: the cloth as it is this frame, mirrored in the water.
                var tr=c.Renderer.transform;c.Renderer.BakeMesh(c.Baked);block.SetFloat(FbMirrorId,1);
                Graphics.DrawMesh(c.Baked,mirror*Matrix4x4.TRS(tr.position,tr.rotation,Vector3.one),fbBody,0,null,0,block,ShadowCastingMode.Off,false);FallenBossDraws++;
                if(near&&!jump&&dt>0)FallenClothShed(c,tr,dt,f.Phase==2);else c.HemKnown=false;
            }
        }
        // A hem's motes this frame: where it is now (the cloth as drawn), how fast each point of it moves.
        private void FallenClothShed(FbClothPiece c,Transform tr,float dt,bool burnt)
        {
            if(c.Hem.Length==0)return;c.Baked.GetVertices(fbClothVerts);var pos=tr.position;var rot=tr.rotation;float rate=LowGraphics?.5f:1;
            for(int j=0;j<c.Hem.Length;j++)
            {
                int vi=c.Hem[j];if(vi>=fbClothVerts.Count)continue;var p=pos+rot*fbClothVerts[vi];
                if(c.HemKnown)
                {
                    var v=(p-c.HemLast[j])/dt;float speed=Mathf.Min(v.magnitude,FbMoteFast);float chance=dt*rate*(FbMoteRest+FbMoteBySpeed*speed)*(burnt?1.6f:1);
                    if(MoteRandom()<chance)FallenMoteAdd(p,v*.12f+new Vector3((MoteRandom()-.5f)*.2f,(burnt?.75f:.4f)+.3f*MoteRandom(),(MoteRandom()-.5f)*.2f),burnt);
                }
                c.HemLast[j]=p;
            }
            c.HemKnown=true;
        }
        private void FallenMoteAdd(Vector3 at,Vector3 way,bool ember)
        {
            if(fbClothMoteCount>=FbClothMoteMax)return;fbClothMotes[fbClothMoteCount++]=new FbClothMote{At=at,Way=way,Age=0,Life=1.6f+1.2f*MoteRandom(),Size=(ember?.022f:.03f)+.03f*MoteRandom(),Glow=ember?1.4f:.85f};
        }
        // The motes drift on, slowing across and rising, fading as they go; drawn with the boss's own effects (soft points of light).
        private void FallenMotesStep(Vector3 origin,float dt)
        {
            for(int i=0;i<fbClothMoteCount;i++)
            {
                var m=fbClothMotes[i];m.Age+=dt;if(m.Age>=m.Life){fbClothMotes[i]=fbClothMotes[--fbClothMoteCount];i--;continue;}
                float drag=Mathf.Exp(-dt*1.4f);m.Way.x*=drag;m.Way.z*=drag;m.Way.y+=dt*.12f;m.At+=m.Way*dt;fbClothMotes[i]=m;
                float u=m.Age/m.Life,fade=Mathf.Clamp01(u/.12f)*(1-u)*(1-u);var q=m.At-origin;
                fbFx.ClothMote(new System.Numerics.Vector3(q.x,q.y,q.z),m.Size*(1-.4f*u),fade*m.Glow);
            }
        }
        private float MoteRandom(){fbMoteSeed^=fbMoteSeed<<13;fbMoteSeed^=fbMoteSeed>>17;fbMoteSeed^=fbMoteSeed<<5;return (fbMoteSeed&0xFFFFFF)/16777216f;}
        private void FallenClothDrop(int k)
        {
            var c=fbCloth[k];if(c==null)return;fbCloth[k]=null;
            NativeVisualCloth.PaintMaps.Remove(c.Name);
            if(c.Root!=null)Destroy(c.Root);if(c.Mesh!=null)Destroy(c.Mesh);if(c.Baked!=null)Destroy(c.Baked);if(c.Paint!=null)Destroy(c.Paint);
        }
        private void FallenClothDispose(){for(int k=0;k<fbCloth.Length;k++)FallenClothDrop(k);fbClothKnown=false;fbClothMoteCount=0;fbMoteClock=-1;}
    }
}
