using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // THE HUM's body (tools/gear/hum_boss.py; the local bank Resources/ChipArenaPackBossHum): per tier one smooth skinned mesh on the boss's own bones with its hardware skinned rigidly in it, drawn with the toon material the game's enemies
    // use, and the lit parts (each spine tube, the head's tubes, the diffuser's halves, the Ballast) as rigid meshes with their own light. The bones are a flat set of transforms set straight from Core HumBossRig's pose: a joint's matrix is its place, its turn and minus its bind place.
    // Skin weights come in the bank's uv channels (uv0: bone0 + weight0 x .998, bone1 + weight1 x .998; uv1: bones 2 and 3). Without the bank (CI, other checkouts) no body draws: the effects and the room still run. Presentation only.
    public sealed partial class ArenaPresentation
    {
        public const string HumBossBank="ChipArenaPackBossHum";
        private const int HumGlowParts=NativeHumBossLook.Tubes+4;
        private readonly Mesh[] humBody=new Mesh[3];private readonly Mesh[][] humGlow=new Mesh[3][];private bool humBankRead,humBankAbsent;
        private GameObject humRoot;private Transform[] humBones;private SkinnedMeshRenderer humSkin;private MaterialPropertyBlock humBodyBlock,humGlowBlock;private int humSkinTier=-1;private bool humWallOut;
        private static readonly int[] HumGlowJoint=MakeHumGlowJoints();
        private static int[] MakeHumGlowJoints()
        {
            var j=new int[HumGlowParts];for(int i=0;i<NativeHumBossLook.Tubes;i++)j[i]=NativeHumBossLook.Tube(i);
            j[NativeHumBossLook.Tubes]=NativeHumBossLook.Head;j[NativeHumBossLook.Tubes+1]=NativeHumBossLook.JawU;j[NativeHumBossLook.Tubes+2]=NativeHumBossLook.JawL;j[NativeHumBossLook.Tubes+3]=NativeHumBossLook.Ballast;return j;
        }
        private static string HumGlowName(int part)=>part<NativeHumBossLook.Tubes?"tube-"+part:part==NativeHumBossLook.Tubes?"head":part==NativeHumBossLook.Tubes+1?"jaw-u":part==NativeHumBossLook.Tubes+2?"jaw-l":"ballast";
        // Reads the bank's meshes once (all tiers: a boss on screen is worth the moment). False: no bank: no body.
        private bool HumBankEnsure()
        {
            if(humBankRead)return !humBankAbsent;humBankRead=true;var bank=NativePackBanks.Bank(HumBossBank);if(bank==null||ZombieGearMaterials()==null){humBankAbsent=true;return false;}
            try
            {
                var bind=new Matrix4x4[NativeHumBossLook.Count];for(int j=0;j<bind.Length;j++){var b=NativeHumBossLook.Bind[j];bind[j]=Matrix4x4.Translate(new Vector3(-b.X,-b.Y,-b.Z));}
                for(int tier=0;tier<3;tier++)
                {
                    var prop=bank.Find("hum-body-"+tier);if(prop==null||prop.Parts.Length==0){if(tier==0){humBankAbsent=true;return false;}humBody[tier]=humBody[tier-1];}
                    else humBody[tier]=HumSkinned(bank.Meshes[prop.Parts[0].Mesh],"THE HUM body "+tier,bind);
                    humGlow[tier]=new Mesh[HumGlowParts];
                    for(int p=0;p<HumGlowParts;p++){var g=bank.Find("hum-"+HumGlowName(p)+"-"+tier);if(g!=null&&g.Parts.Length>0)humGlow[tier][p]=HumRigid(bank.Meshes[g.Parts[0].Mesh],"THE HUM "+g.Name);}
                }
            }
            catch(Exception e){Debug.LogWarning("THE HUM: the bank is unreadable ("+e.Message+"); it draws no body.");humBankAbsent=true;return false;}
            return true;
        }
        private static Mesh HumSkinned(NativePackBank.Mesh src,string name,Matrix4x4[] bind)
        {
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;int n=src.Positions.Length;var v=new Vector3[n];var nr=new Vector3[n];var cs=new Color[n];var bw=new BoneWeight[n];
            for(int i=0;i<n;i++)
            {
                var p=src.Positions[i];v[i]=new Vector3(p.X,p.Y,p.Z);var q=src.Normals[i];nr[i]=new Vector3(q.X,q.Y,q.Z);
                var c=src.Colors!=null?new Color(src.Colors[i*4]/255f,src.Colors[i*4+1]/255f,src.Colors[i*4+2]/255f,src.Colors[i*4+3]/255f):Color.gray;cs[i]=linear?new Color(Mathf.GammaToLinearSpace(c.r),Mathf.GammaToLinearSpace(c.g),Mathf.GammaToLinearSpace(c.b),c.a):c;
                var a=src.Uv0[i];var b=src.Uv1[i];int i0=(int)a.X,i1=(int)a.Y,i2=(int)b.X,i3=(int)b.Y;float w0=(a.X-i0)/.998f,w1=(a.Y-i1)/.998f,w2=(b.X-i2)/.998f,w3=(b.Y-i3)/.998f;float s=w0+w1+w2+w3;if(s<=0){w0=1;s=1;}
                bw[i]=new BoneWeight{boneIndex0=i0,weight0=w0/s,boneIndex1=i1,weight1=w1/s,boneIndex2=i2,weight2=w2/s,boneIndex3=i3,weight3=w3/s};
            }
            var mesh=new Mesh{name=name,hideFlags=HideFlags.HideAndDontSave};if(n>65535)mesh.indexFormat=IndexFormat.UInt32;
            mesh.vertices=v;mesh.normals=nr;mesh.colors=cs;mesh.boneWeights=bw;mesh.bindposes=bind;mesh.SetTriangles(src.Submeshes[0].Indices,0,false);mesh.bounds=new Bounds(new Vector3(0,2,0),new Vector3(16,14,16));mesh.UploadMeshData(true);return mesh;
        }
        private static Mesh HumRigid(NativePackBank.Mesh src,string name)
        {
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;int n=src.Positions.Length;var v=new Vector3[n];var nr=new Vector3[n];var cs=new Color[n];
            for(int i=0;i<n;i++){var p=src.Positions[i];v[i]=new Vector3(p.X,p.Y,p.Z);var q=src.Normals[i];nr[i]=new Vector3(q.X,q.Y,q.Z);var c=src.Colors!=null?new Color(src.Colors[i*4]/255f,src.Colors[i*4+1]/255f,src.Colors[i*4+2]/255f,src.Colors[i*4+3]/255f):Color.white;cs[i]=linear?new Color(Mathf.GammaToLinearSpace(c.r),Mathf.GammaToLinearSpace(c.g),Mathf.GammaToLinearSpace(c.b),c.a):c;}
            var mesh=new Mesh{name=name,hideFlags=HideFlags.HideAndDontSave};if(n>65535)mesh.indexFormat=IndexFormat.UInt32;
            mesh.vertices=v;mesh.normals=nr;mesh.colors=cs;mesh.SetTriangles(src.Submeshes[0].Indices,0,false);mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
        }
        private void HumBodyEnsure()
        {
            if(humRoot!=null)return;
            humRoot=new GameObject("THE HUM"){hideFlags=HideFlags.HideAndDontSave};humBones=new Transform[NativeHumBossLook.Count];
            for(int j=0;j<humBones.Length;j++){var t=new GameObject(NativeHumBossLook.Names[j]).transform;t.SetParent(humRoot.transform,false);humBones[j]=t;}
            var go=new GameObject("THE HUM skin");go.transform.SetParent(humRoot.transform,false);humSkin=go.AddComponent<SkinnedMeshRenderer>();humSkin.bones=humBones;humSkin.rootBone=humBones[0];humSkin.quality=SkinQuality.Bone4;humSkin.updateWhenOffscreen=false;
            humSkin.localBounds=new Bounds(new Vector3(0,2,0),new Vector3(90,14,90));humSkin.sharedMaterial=ZombieGearMaterials()[0];humSkin.shadowCastingMode=ShadowCastingMode.Off;humSkin.receiveShadows=true;
            humBodyBlock=new MaterialPropertyBlock();humGlowBlock=new MaterialPropertyBlock();humSkinTier=-1;humWallOut=true;
            var life=gameObject.GetComponent<NativeStationLifetime>();if(life==null)life=gameObject.AddComponent<NativeStationLifetime>();life.Gone+=DisposeHumBoss;
        }
        // HumBossScenery: the skin on this frame's pose, the lit parts at their own levels.
        private void HumBodyDraw(Camera eye)
        {
            if(!HumBankEnsure())return;HumBodyEnsure();var pose=humBoss.Shown;var fx=humBossFx;
            float d=eye!=null?Vector3.Distance(eye.transform.position,NativeBossPoseApply.ToUnity(humBoss.Root)):0;int tier=d<24?0:d<50?1:2;if(LowGraphics)tier=Math.Max(1,tier);HumBossTier=tier;
            if(!humRoot.activeSelf)humRoot.SetActive(true);
            if(tier!=humSkinTier){humSkinTier=tier;humSkin.sharedMesh=humBody[tier];humSkin.localBounds=new Bounds(new Vector3(0,2,0),new Vector3(90,14,90));}
            for(int j=0;j<humBones.Length;j++)humBones[j].SetPositionAndRotation(NativeBossPoseApply.ToUnity(pose.Position[j]),NativeBossPoseApply.ToUnity(pose.Rotation[j]));
            for(int k=0;k<3;k++)humBones[NativeHumBossLook.Neck0+k].localScale=new Vector3(1,1,humBoss.NeckStretch[k]);
            // The Wall Hand's arm collapses to nothing until it's out.
            if(humBoss.WallArmOut!=humWallOut){humWallOut=humBoss.WallArmOut;var sc=humWallOut?Vector3.one:Vector3.one*1e-4f;for(int j=NativeHumBossLook.WallFore;j<humBones.Length;j++)humBones[j].localScale=sc;}
            float flash=humFlash;humFlash=Mathf.Max(0,humFlash-Time.unscaledDeltaTime/.14f);float code=flash>0?1+Mathf.Clamp01(flash):1;
            var b=humBodyBlock;b.Clear();b.SetFloat(HitFlashId,code);b.SetFloat(ActorLitId,1);humSkin.SetPropertyBlock(b);HumBossDraws++;
            var glow=humGlow[tier];var mat=ZombieGearMaterials()[1];var g=humGlowBlock;
            for(int p=0;p<HumGlowParts;p++)
            {
                var mesh=glow[p];if(mesh==null)continue;int j=HumGlowJoint[p];float level=p<NativeHumBossLook.Tubes?fx.Tube[p]:p==NativeHumBossLook.Tubes?fx.HeadTubes:p==NativeHumBossLook.Tubes+1?fx.DiffuserU:p==NativeHumBossLook.Tubes+2?fx.DiffuserL:fx.Ballast;
                var bp=NativeHumBossLook.Bind[j];var m=Matrix4x4.TRS(NativeBossPoseApply.ToUnity(pose.Position[j]),NativeBossPoseApply.ToUnity(pose.Rotation[j]),Vector3.one)*Matrix4x4.Translate(new Vector3(-bp.X,-bp.Y,-bp.Z));
                g.Clear();g.SetFloat(HitFlashId,1);g.SetFloat(ActorLitId,1);g.SetFloat(VertexAlbedoId,Mathf.Max(1e-4f,ZombieGearGlow*level));
                Graphics.DrawMesh(mesh,m,mat,0,null,0,g,ShadowCastingMode.Off,false);HumBossDraws++;
            }
        }
        private void HumBodyHide(){if(humRoot!=null&&humRoot.activeSelf)humRoot.SetActive(false);}
        private void HumBodyDispose()
        {
            if(humRoot!=null){CapturedAnimation.Release(humRoot);humRoot=null;}humBones=null;humSkin=null;
            for(int t=0;t<3;t++){if(humBody[t]!=null&&(t==0||!ReferenceEquals(humBody[t],humBody[t-1])))CapturedAnimation.Release(humBody[t]);humBody[t]=null;if(humGlow[t]!=null)foreach(var m in humGlow[t])if(m!=null)CapturedAnimation.Release(m);humGlow[t]=null;}
            humBankRead=false;humBankAbsent=false;humSkinTier=-1;
        }
    }
}
