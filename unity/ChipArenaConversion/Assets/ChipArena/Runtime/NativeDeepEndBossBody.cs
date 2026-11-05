using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // THE DEEP END's body as a real model (tools/gear/deep_end_boss.py, the bank Resources/ChipArenaPackBossDeepEnd; docs/EXPEDITION-BOSS-BACKROOMS.md section 4). Presentation only: Core DeepEndBossRig's pose is all it reads.
    //  - The skin: one mesh per tier on the spine's 31 joints, each vertex's four weights a cubic B-spline of its place along the spine (uv0.x), so it bends as a curve with no creases. CPU skinned, as the project skins. Materials: the gear's toon (ZombieGearMaterials, so it sits with the
    //    game's other enemies) and the glaze over it (Resources/NativeDeepEndGlaze: the room's reflection, the highlights, chrome); the small running lights its own glowing sub-mesh.
    //  - The rigid parts (the flank lamps, the iris's blades, the grate, the throat, the mandibles, the fins): the bank's props drawn in their joint's frame.
    //  - Without the bank (CI, other checkouts): a plain loft from Core NativeDeepEndBossLook.Fallback on the same bones, so nothing crashes and the moves still read.
    public sealed partial class ArenaPresentation
    {
        private sealed class DeepEndSkin{public GameObject Go;public SkinnedMeshRenderer Renderer;public bool Glows;public MaterialPropertyBlock Solid,Glow;}
        private readonly DeepEndSkin[] deepEndSkins=new DeepEndSkin[3];private GameObject deepEndRoot;private Transform[] deepEndBones;private Matrix4x4[] deepEndBind;private bool deepEndBuilt,deepEndHasBank;private int deepEndTierShown=-1;
        private struct DeepEndPart{public Mesh Mesh;public bool Glows;}
        private readonly DeepEndPart[,] deepEndParts=new DeepEndPart[3,DeepEndPartNames.Length];private static readonly string[] DeepEndPartNames={"lamp","lamp-broken","blade","grate","throat","rail-r","rail-l","fin-r","fin-l","float"};
        private const int PartLamp=0,PartLampBroken=1,PartBlade=2,PartGrate=3,PartThroat=4,PartRailR=5,PartRailL=6,PartFinR=7,PartFinL=8,PartFloat=9;
        private static readonly float[] DeepEndPartGloss={2,2,2,2,0,2,2,.9f,.9f,.7f};
        private Material deepEndGlaze;private MaterialPropertyBlock deepEndBlock,deepEndGlazeBlock;
        private static readonly int DeepGlossId=Shader.PropertyToID("_DeepGloss");

        private void DeepEndBodyBuild()
        {
            if(deepEndBuilt)return;deepEndBuilt=true;
            var shader=Resources.Load<Shader>("NativeDeepEndGlaze");if(shader!=null&&shader.isSupported)deepEndGlaze=new Material(shader){name="THE DEEP END glaze",hideFlags=HideFlags.HideAndDontSave};
            deepEndRoot=new GameObject("THE DEEP END"){hideFlags=HideFlags.DontSave};int n=NativeDeepEndBossLook.Spine;deepEndBones=new Transform[n];deepEndBind=new Matrix4x4[n];
            for(int i=0;i<n;i++){var b=new GameObject("spine-"+i).transform;b.SetParent(deepEndRoot.transform,false);deepEndBones[i]=b;deepEndBind[i]=Matrix4x4.Translate(new Vector3(0,0,i*NativeDeepEndBossLook.Seg));}
            var bank=NativePackBanks.Bank(NativeDeepEndBossLook.Bank);deepEndBlock=new MaterialPropertyBlock();deepEndGlazeBlock=new MaterialPropertyBlock();
            var mats=ZombieGearMaterials();
            for(int tier=0;tier<3;tier++)
            {
                Mesh mesh=null;bool glows=false;
                var prop=bank?.Find(NativeDeepEndBossLook.Prop("body",tier));
                if(prop!=null&&prop.Parts.Length>0&&mats!=null){try{mesh=DeepEndSkinMesh(bank,prop,out glows);deepEndHasBank=true;}catch(Exception e){Debug.LogWarning("THE DEEP END body: "+e.Message);}}
                if(mesh==null&&tier==0)mesh=DeepEndFallbackMesh();
                if(mesh!=null)
                {
                    var go=new GameObject("skin-"+tier){hideFlags=HideFlags.DontSave};go.transform.SetParent(deepEndRoot.transform,false);var r=go.AddComponent<SkinnedMeshRenderer>();r.sharedMesh=mesh;r.bones=deepEndBones;r.rootBone=deepEndBones[0];r.quality=SkinQuality.Bone4;r.updateWhenOffscreen=false;r.localBounds=new Bounds(Vector3.zero,Vector3.one*400);r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
                    var list=new List<Material>();if(mats!=null){if(glows)list.Add(mats[1]);list.Add(mats[0]);}if(deepEndGlaze!=null)list.Add(deepEndGlaze);r.sharedMaterials=list.ToArray();go.SetActive(false);
                    deepEndSkins[tier]=new DeepEndSkin{Go=go,Renderer=r,Glows=glows,Solid=new MaterialPropertyBlock(),Glow=new MaterialPropertyBlock()};
                }
                if(bank==null||mats==null)continue;
                for(int k=0;k<DeepEndPartNames.Length;k++)
                {
                    var pr=bank.Find(NativeDeepEndBossLook.Prop(DeepEndPartNames[k],tier));if(pr==null||pr.Parts.Length==0)continue;
                    try{var built=ZombieGearBuild(bank,pr);if(built?.Mesh!=null)deepEndParts[tier,k]=new DeepEndPart{Mesh=built.Mesh,Glows=built.Mesh.subMeshCount>1};}catch(Exception e){Debug.LogWarning("THE DEEP END "+pr.Name+": "+e.Message);}
                }
            }
            for(int t=2;t>=0;t--)if(deepEndSkins[t]==null)deepEndSkins[t]=deepEndSkins[t<2?t+1:0]??deepEndSkins[0];
            var life=gameObject.GetComponent<NativeStationLifetime>();if(life==null)life=gameObject.AddComponent<NativeStationLifetime>();life.Gone+=DeepEndBodyDispose;
        }
        // The bank's mesh with its bones: glowing sub-mesh first, the solid second (so the glaze, the renderer's extra material, draws the solid one over again).
        private Mesh DeepEndSkinMesh(NativePackBank.Bank bank,NativePackBank.Prop prop,out bool glows)
        {
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;var src=bank.Meshes[prop.Parts[0].Mesh];int n=src.Positions.Length;var vs=new Vector3[n];var ns=new Vector3[n];var cs=new Color[n];var uv=new Vector2[n];var bw=new BoneWeight[n];
            for(int i=0;i<n;i++)
            {
                var p=src.Positions[i];var q=src.Normals[i];var t=src.Uv0[i];vs[i]=new Vector3(p.X,p.Y,p.Z);ns[i]=new Vector3(q.X,q.Y,q.Z);uv[i]=new Vector2(t.X,t.Y);
                var c=src.Colors!=null?new Color(src.Colors[i*4]/255f,src.Colors[i*4+1]/255f,src.Colors[i*4+2]/255f,src.Colors[i*4+3]/255f):Color.white;cs[i]=linear?new Color(Mathf.GammaToLinearSpace(c.r),Mathf.GammaToLinearSpace(c.g),Mathf.GammaToLinearSpace(c.b),c.a):c;
                bw[i]=DeepEndWeights(t.X);
            }
            var solid=new List<int>();var glow=new List<int>();foreach(var sub in src.Submeshes){var mat=sub.Material>=0&&sub.Material<bank.Materials.Count?bank.Materials[sub.Material]:null;var into=mat!=null&&mat.EmissionColor.LengthSquared()>0?glow:solid;foreach(int k in sub.Indices)into.Add(k);}
            glows=glow.Count>0;var mesh=new Mesh{name=prop.Name,hideFlags=HideFlags.HideAndDontSave};if(n>65535)mesh.indexFormat=IndexFormat.UInt32;mesh.vertices=vs;mesh.normals=ns;mesh.colors=cs;mesh.uv=uv;mesh.boneWeights=bw;mesh.bindposes=deepEndBind;
            mesh.subMeshCount=glows?2:1;if(glows){mesh.SetTriangles(glow,0,false);mesh.SetTriangles(solid,1,false);}else mesh.SetTriangles(solid,0,false);mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
        }
        // A vertex at spine coordinate q on the cubic B-spline's four joints (the ends folded back on themselves).
        private static BoneWeight DeepEndWeights(float q)
        {
            NativeDeepEndBossLook.Weights(q,out int b0,out float w0,out float w1,out float w2,out float w3);int last=NativeDeepEndBossLook.Spine-1;
            int i0=Mathf.Clamp(b0,0,last),i1=Mathf.Clamp(b0+1,0,last),i2=Mathf.Clamp(b0+2,0,last),i3=Mathf.Clamp(b0+3,0,last);
            // Merge what clamped onto one joint (Unity wants distinct, sorted bones for its fastest path; weights summing to 1).
            if(i1==i0){w1+=w0;w0=0;}if(i2==i3){w2+=w3;w3=0;}
            return new BoneWeight{boneIndex0=i1,weight0=w1,boneIndex1=i2,weight1=w2,boneIndex2=i0,weight2=w0,boneIndex3=i3,weight3=w3};
        }
        private Mesh DeepEndFallbackMesh()
        {
            NativeDeepEndBossLook.Fallback(out var p,out var n,out var c,out var u,out var t);var vs=new Vector3[p.Length];var ns=new Vector3[p.Length];var cs=new Color[p.Length];var uv=new Vector2[p.Length];var bw=new BoneWeight[p.Length];bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
            for(int i=0;i<p.Length;i++){vs[i]=new Vector3(p[i].X,p[i].Y,p[i].Z);ns[i]=new Vector3(n[i].X,n[i].Y,n[i].Z);var col=new Color(c[i].X,c[i].Y,c[i].Z,c[i].W);cs[i]=linear?col.linear:col;uv[i]=new Vector2(u[i].X,u[i].Y);bw[i]=DeepEndWeights(u[i].X);}
            var m=new Mesh{name="THE DEEP END stand-in",hideFlags=HideFlags.HideAndDontSave};m.vertices=vs;m.normals=ns;m.colors=cs;m.uv=uv;m.boneWeights=bw;m.bindposes=deepEndBind;m.triangles=t;m.RecalculateBounds();m.UploadMeshData(true);return m;
        }
        // The pose on the bones and the parts' draws.
        private void DeepEndBossDraw(Camera eye,float now)
        {
            DeepEndBodyBuild();var pose=deepEndRig.Shown;var fx=deepEndFx;DeepEndBossDraws=0;
            if(deepEndRoot!=null&&!deepEndRoot.activeSelf)deepEndRoot.SetActive(true);
            // The tier by the nearest of its joints (it's long: the tail can be at your feet with the head across the room).
            var at=eye!=null?eye.transform.position:Vector3.zero;float d2=float.MaxValue;for(int i=0;i<NativeDeepEndBossLook.Spine;i+=3){var q=pose.Position[i];float dx=q.X-at.x,dy=q.Y-at.y,dz=q.Z-at.z;d2=Math.Min(d2,dx*dx+dy*dy+dz*dz);}
            float d=Mathf.Sqrt(d2);int tier=d<34?0:d<70?1:2;if(LowGraphics)tier=Math.Max(1,tier);DeepEndBossTier=tier;
            for(int i=0;i<NativeDeepEndBossLook.Spine;i++)deepEndBones[i].SetPositionAndRotation(NativeBossPoseApply.ToUnity(pose.Position[i]),NativeBossPoseApply.ToUnity(pose.Rotation[i]));
            var skin=deepEndSkins[tier];if(deepEndTierShown!=tier){for(int t=0;t<3;t++)if(deepEndSkins[t]!=null&&deepEndSkins[t]!=skin)deepEndSkins[t].Go.SetActive(false);deepEndTierShown=tier;}
            float flash=deepEndFlash;deepEndFlash=Mathf.Max(0,deepEndFlash-Time.unscaledDeltaTime/.14f);float code=flash>0?1+Mathf.Clamp01(flash):1;
            if(skin!=null)
            {
                if(!skin.Go.activeSelf)skin.Go.SetActive(true);var r=skin.Renderer;
                skin.Solid.Clear();skin.Solid.SetFloat(HitFlashId,code);skin.Solid.SetFloat(ActorLitId,1);skin.Solid.SetFloat(VertexAlbedoId,.03f+.42f*(1-Mathf.Clamp01(NativeDeepEndBossStage.Shown.Ambient)));
                if(skin.Glows){skin.Glow.Clear();skin.Glow.SetFloat(VertexAlbedoId,ZombieGearGlow*Mathf.Max(0,2.2f*fx.MarkerGlow));skin.Glow.SetFloat(ActorLitId,1);r.SetPropertyBlock(skin.Glow,0);r.SetPropertyBlock(skin.Solid,1);}else r.SetPropertyBlock(skin.Solid,0);
                DeepEndBossDraws++;
            }
            var mats=ZombieGearMaterials();if(mats==null)return;int T=tier;
            void Part(int k,in Matrix4x4 m,float glow)
            {
                var part=deepEndParts[T,k];if(part.Mesh==null)return;deepEndBlock.Clear();deepEndBlock.SetFloat(HitFlashId,code);deepEndBlock.SetFloat(ActorLitId,1);
                Graphics.DrawMesh(part.Mesh,m,mats[0],0,null,0,deepEndBlock,ShadowCastingMode.Off,false);DeepEndBossDraws++;
                if(part.Glows){deepEndBlock.SetFloat(VertexAlbedoId,ZombieGearGlow*Mathf.Max(0,glow));Graphics.DrawMesh(part.Mesh,m,mats[1],0,null,1,deepEndBlock,ShadowCastingMode.Off,false);DeepEndBossDraws++;}
                if(deepEndGlaze!=null&&DeepEndPartGloss[k]>0){deepEndGlazeBlock.SetFloat(DeepGlossId,DeepEndPartGloss[k]);Graphics.DrawMesh(part.Mesh,m,deepEndGlaze,0,null,0,deepEndGlazeBlock,ShadowCastingMode.Off,false);DeepEndBossDraws++;}
            }
            // The flank lamps.
            for(int i=0;i<NativeDeepEndBossLook.Lamps;i++)
            {
                deepEndRig.LampFrame(i,out var p,out var q,out float s);var m=Matrix4x4.TRS(NativeBossPoseApply.ToUnity(p),NativeBossPoseApply.ToUnity(q),Vector3.one*s);bool broken=deepEndView.LampBroken(i,now);Part(broken?PartLampBroken:PartLamp,m,3.2f*fx.LampGlow[i]);
            }
            Matrix4x4 J(int j,float s=1)=>Matrix4x4.TRS(NativeBossPoseApply.ToUnity(pose.Position[j]),NativeBossPoseApply.ToUnity(pose.Rotation[j]),Vector3.one*s);
            Part(PartGrate,J(NativeDeepEndBossLook.Grate),0);Part(PartThroat,J(NativeDeepEndBossLook.Throat),fx.ThroatGlow);Part(PartRailR,J(NativeDeepEndBossLook.RailR),0);Part(PartRailL,J(NativeDeepEndBossLook.RailL),0);Part(PartFinR,J(NativeDeepEndBossLook.FinR),0);Part(PartFinL,J(NativeDeepEndBossLook.FinL),0);
            for(int k=0;k<NativeDeepEndBossLook.Blades;k++)Part(PartBlade,J(NativeDeepEndBossLook.BladeFirst+k,deepEndRig.BladeScale[k]),0);
            if(fx.FloatOn)Part(PartFloat,Matrix4x4.TRS(NativeBossPoseApply.ToUnity(fx.FloatAt),NativeBossPoseApply.ToUnity(fx.FloatTurn),Vector3.one),0);
        }
        private void DeepEndBodyHide(){if(deepEndRoot!=null&&deepEndRoot.activeSelf)deepEndRoot.SetActive(false);}
        private void DeepEndBodyDispose()
        {
            if(deepEndRoot!=null)Destroy(deepEndRoot);deepEndRoot=null;deepEndBones=null;deepEndBuilt=false;deepEndTierShown=-1;for(int t=0;t<3;t++){var s=deepEndSkins[t];deepEndSkins[t]=null;if(s?.Renderer!=null&&s.Renderer.sharedMesh!=null)CapturedAnimation.Release(s.Renderer.sharedMesh);for(int k=0;k<DeepEndPartNames.Length;k++){if(deepEndParts[t,k].Mesh!=null)CapturedAnimation.Release(deepEndParts[t,k].Mesh);deepEndParts[t,k]=default;}}
            if(deepEndGlaze!=null)CapturedAnimation.Release(deepEndGlaze);deepEndGlaze=null;DeepEndWaterDispose();
        }
    }
}
