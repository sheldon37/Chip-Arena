using System;
using System.Collections.Generic;
using UnityEngine;
using NVector3=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The voxel pilot's parts for the costume (Core NativeVoxelCostume): the Prototype's arrays as the bank keeps them, or a
    // built skin's tier (the bank's own build at load). Read once per variant and tier.
    public sealed partial class NativeSourceModels
    {
        public bool CostumeParts(int skin,int tier,List<CostumePart> into)
        {
            into.Clear();
            if(skin==NativeBodySkins.PrototypeIndex)
            {
                if(!models.TryGetValue(NativeBodySkins.PrototypeModel,out var parts))return false;
                foreach(var p in parts)if(p.Core!=null)into.Add(new CostumePart(NativeBodySkins.PartIndex(p.Name,p.Bone),p.Bone,p.Core));
                return into.Count>0;
            }
            if(!pilotBodies.TryGetValue(NativeBodySkins.Model(skin,tier),out var body))return false;
            for(int i=0;i<body.Parts.Length;i++)into.Add(new CostumePart(i,body.Parts[i].Bone,body.Parts[i].Mesh));
            return true;
        }
    }

    // The voxel costume on the model (owner 2026-11-18: "The voxel player model + weapons can be selected as skins or costumes
    // instead"; docs/VOXEL-RETIREMENT.md 2.3, O1(a); Core NativeVoxelCostume): a pilot whose look wears "costume:voxel" is dressed
    // (VisualDress) in the voxel pilot's blocks, in the variant its skin map's body entry picks, fitted once per variant and tier
    // onto the bake's rest skeleton: two skinned meshes (the blocks, and the trims and lights that glow) rigid on the body's own
    // bones, in place of everything the body wears, as the Hot Dog is (NativeHotDogPresentation.cs). So every route that dresses
    // a model body draws it: the live pilot, first person's forearms, corpses and their cuts, the draft, the broadcast's bust,
    // portraits, the Locker, the deploy cast and the eject, the rooftop, killcams and replays (the look travels per seat). It
    // draws in the model's toon (its outline, hit flash, spawn-in, dissolve, cuts and wound pits are the body's own): its
    // texture a palette of the voxel paints, its vertex colours the toon's layers, its nine colours per renderer the pilot's
    // body and accent tints (CostumeTint). Hats sit on its head block (CostumeHatOn), the chest core and wrist device at its
    // anchors (CostumeAnchors); the face gear, the robot plates, the cloth hood and the back emblem are not drawn on it. Without
    // the bake the pilot draws the voxel style in the same variant. Presentation only.
    public sealed partial class ArenaPresentation
    {
        public const string CostumeRenderer="VoxelCostume",CostumeGlowRenderer="VoxelCostume glow",CostumeHat="VoxelCostume hat";
        // The dressed look's own entry naming the variant (VisualLookOf adds it; never sent or saved: the variant travels as the
        // skin map's body entry), so a body is dressed again when only the variant changes (VisualRetone).
        private const string CostumeSkinEntry="voxelbody:";
        private sealed class CostumeAsset
        {
            public CostumeBuild Build;public Mesh Main,Glow;public string[] MainBones,GlowBones;public Material MainMaterial,GlowMaterial;public Texture2D Palette;
            // The hat's fit on the head block in the head bone's frame (NativeVoxelCostume.HatFit), and the gear's places in their bones' frames.
            public Vector3 HatMove;public float HatScale=1;public string CoreParent;public Vector3 CoreLocal;public float DeviceRadius;
        }
        private static readonly Dictionary<int,CostumeAsset> costumeAssets=new Dictionary<int,CostumeAsset>();
        private static NativeSourceModels costumeBank;private static NativeVisualRig.Rig costumeRig;
        // A dressed body's costume renderers by its root, and the tints they last took.
        private sealed class CostumeWorn{public SkinnedMeshRenderer Main,Glow;public string Body,Accent;public int Skin=-1;public float Pulse=-1;public MaterialPropertyBlock Block;}
        private static readonly Dictionary<GameObject,CostumeWorn> costumeByRoot=new Dictionary<GameObject,CostumeWorn>();
        private static readonly NVector3[] costumeTints=new NVector3[9];

        /// <summary>The variant a costumed pilot wears (its skin map's body entry; the view body is the local seat's).</summary>
        private int CostumeSkinOf(int id)=>BodySkin(id==ViewBodyId?LocalId:id);
        private readonly Dictionary<(string,int),string> costumeLooks=new Dictionary<(string,int),string>();
        /// <summary>A dressed costumed look with its variant entry (cached: choosing it allocates nothing after the first time).</summary>
        private string CostumeLook(string dressed,int skin)
        {
            var key=(dressed,skin);if(!costumeLooks.TryGetValue(key,out var look)){if(costumeLooks.Count>64)costumeLooks.Clear();costumeLooks[key]=look=dressed+","+CostumeSkinEntry+skin;}
            return look;
        }
        /// <summary>The variant a dressed look names (Standard when it names none). Allocation free.</summary>
        private static int CostumeSkinIn(string look)
        {
            if(string.IsNullOrEmpty(look))return NativeBodySkins.StandardIndex;int at=look.IndexOf(CostumeSkinEntry,StringComparison.Ordinal);
            if(at<0||at+CostumeSkinEntry.Length>=look.Length)return NativeBodySkins.StandardIndex;int k=look[at+CostumeSkinEntry.Length]-'0';
            return k>=0&&k<NativeBodySkins.Count?k:NativeBodySkins.StandardIndex;
        }
        /// <summary>Whether a body (`root`) wears the costume (its renderer is there).</summary>
        private static bool CostumeOn(GameObject root)=>root!=null&&costumeByRoot.TryGetValue(root,out var c)&&c.Main!=null;

        /// <summary>VisualDress, in the costume: everything the body would wear off, the costume's two meshes on the body's bones,
        /// its hat anchor and the gear at its anchors. Without a build (no bank yet, a bake without a bone the fit needs) the outfit
        /// stays as it is.</summary>
        private static void CostumeWear(GameObject root,string look)
        {
            var asset=CostumeAssetFor(CostumeSkinIn(look),NativeBodySkins.Full);if(asset==null||root==null)return;
            var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);var weaponHolder=root.transform.Find("VisualWeapons");int layer=root.layer;
            foreach(var s in skins)if(s.sharedMesh!=null&&s.name.StartsWith("Body.",StringComparison.Ordinal)){layer=s.gameObject.layer;break;}
            foreach(var s in skins)if(weaponHolder==null||!s.transform.IsChildOf(weaponHolder))s.gameObject.SetActive(false);
            var skeleton=root.transform.Find("VisualSkeleton");if(skeleton==null)return;
            var named=new Dictionary<string,Transform>(StringComparer.Ordinal);foreach(var t in skeleton.GetComponentsInChildren<Transform>(true))if(!named.ContainsKey(t.name))named[t.name]=t;
            var worn=new CostumeWorn{Block=new MaterialPropertyBlock()};
            worn.Main=CostumeRendererMake(root,asset.Main,asset.MainBones,asset.MainMaterial,CostumeRenderer,named,layer);
            worn.Glow=CostumeRendererMake(root,asset.Glow,asset.GlowBones,asset.GlowMaterial,CostumeGlowRenderer,named,layer);
            if(worn.Main==null)return;
            if(costumeByRoot.Count>256){var gone=new List<GameObject>();foreach(var k in costumeByRoot.Keys)if(k==null)gone.Add(k);foreach(var k in gone)costumeByRoot.Remove(k);}
            costumeByRoot[root]=worn;CostumeTintOn(worn,"#e5e7eb","#70e1d0",CostumeSkinIn(look),null,null);
            // Hats ride an anchor on the head bone at the head block's fit (CostumeHatOn).
            if(named.TryGetValue(asset.Build.HeadBone??"",out var head)){var hat=new GameObject(CostumeHat).transform;hat.SetParent(head,false);hat.localPosition=asset.HatMove;hat.localRotation=Quaternion.identity;hat.localScale=Vector3.one*asset.HatScale;}
            CostumeAnchors(root,asset);
        }
        // A costume mesh's renderer on a body's bones (the bones by name, as the bake's skeleton names them).
        private static SkinnedMeshRenderer CostumeRendererMake(GameObject root,Mesh mesh,string[] bones,Material material,string name,Dictionary<string,Transform> named,int layer)
        {
            if(mesh==null||material==null||mesh.vertexCount==0)return null;var at=new Transform[bones.Length];
            for(int b=0;b<bones.Length;b++)if(!named.TryGetValue(bones[b],out at[b]))return null;
            var go=new GameObject(name){layer=layer};go.transform.SetParent(root.transform,false);
            var r=go.AddComponent<SkinnedMeshRenderer>();r.sharedMesh=mesh;r.bones=at;r.sharedMaterial=material;r.localBounds=mesh.bounds;
            r.updateWhenOffscreen=true;r.skinnedMotionVectors=false;return r;
        }
        /// <summary>The chest core on the costume's sternum plate, the wrist device's band round its forearm (the bake's own
        /// device place, its radius the costume forearm's): set in the anchors' bones' frames, so VisualAnchorsFor leaves them.</summary>
        private static void CostumeAnchors(GameObject root,CostumeAsset build)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                if(t.name=="VisualGearCore"&&build.CoreParent!=null&&t.parent!=null&&t.parent.name==build.CoreParent)t.localPosition=build.CoreLocal;
                else if(t.name=="VisualGearDeviceRim"&&build.DeviceRadius>.01f&&t.localPosition.sqrMagnitude>1e-8f)t.localPosition=t.localPosition.normalized*build.DeviceRadius;
            }
        }
        /// <summary>The frame hat `hat` rides on head bone `head`: on a costumed body, the anchor at the head block's fit; otherwise
        /// the head itself (null for null).</summary>
        private static Transform CostumeHatOn(string hat,Transform head)
        {
            if(head==null)return null;var anchor=head.Find(CostumeHat);return anchor!=null?anchor:head;
        }
        /// <summary>The pilot's tints on its costume (its body colour, and its accent: the Sheldon variant's the Sheldon glow), written
        /// only when they change.</summary>
        private static void CostumeTint(GameObject root,string body,string accent,int skin,string hat,string hatColor)
        {
            if(root==null||!costumeByRoot.TryGetValue(root,out var worn)||worn.Main==null)return;
            if(worn.Skin==skin&&string.Equals(worn.Body,body,StringComparison.Ordinal)&&string.Equals(worn.Accent,skin==NativeBodySkins.SheldonIndex?NativeBodySkins.SheldonGlow(hat,hatColor,accent):accent,StringComparison.Ordinal))return;
            CostumeTintOn(worn,body,accent,skin,hat,hatColor);
        }
        private static void CostumeTintOn(CostumeWorn worn,string body,string accent,int skin,string hat,string hatColor)
        {
            string glowColour=skin==NativeBodySkins.SheldonIndex?NativeBodySkins.SheldonGlow(hat,hatColor,accent):accent;worn.Skin=skin;worn.Body=body;worn.Accent=glowColour;
            var b=BodyTint(body);var a=AccentTint(glowColour);var bn=new NVector3(b.r,b.g,b.b);var an=new NVector3(a.r,a.g,a.b);
            for(int k=0;k<2;k++)
            {
                var r=k==0?worn.Main:worn.Glow;if(r==null)continue;NativeVoxelCostume.Tints(k==1,bn,an,costumeTints);
                r.GetPropertyBlock(worn.Block);for(int c=0;c<9;c++){var t=costumeTints[c];worn.Block.SetVector(BodyColours[c],new Vector4(t.X,t.Y,t.Z,1));}
                worn.Block.SetFloat(AccentGlowId,k==1?NativeVoxelCostume.Glow*(worn.Pulse<0?1:worn.Pulse):0);r.SetPropertyBlock(worn.Block);
            }
        }
        /// <summary>The glowing paints' pulse with the block cooldown (VisualAccentGlow's share of its full glow), when it changes.</summary>
        private static void CostumeGlowPulse(GameObject root,float share)
        {
            if(root==null||!costumeByRoot.TryGetValue(root,out var worn)||worn.Glow==null||worn.Pulse==share)return;
            worn.Pulse=share;worn.Glow.GetPropertyBlock(worn.Block);worn.Block.SetFloat(AccentGlowId,NativeVoxelCostume.Glow*share);worn.Glow.SetPropertyBlock(worn.Block);
        }

        // ---- the shared builds: once per variant and tier, from the bake's rest skeleton ----
        private static CostumeAsset CostumeAssetFor(int skin,int tier)
        {
            int key=skin*NativeBodySkins.Tiers+tier;if(costumeAssets.TryGetValue(key,out var made))return made;
            if(visualBodyPrefab==null||costumeBank==null)return null;
            costumeAssets[key]=null; // (one try a variant and tier: a failure is logged once and the outfit stays)
            var root=visualBodyPrefab.transform;var skel=root.Find("VisualSkeleton");if(skel==null)return null;
            var rest=new Dictionary<string,Transform>(StringComparer.Ordinal);foreach(var t in skel.GetComponentsInChildren<Transform>(true))if(!rest.ContainsKey(t.name))rest[t.name]=t;
            var toRoot=root.worldToLocalMatrix;
            try{costumeRig??=new NativeVisualRigBinding(root,skel).Rig;}catch(Exception e){Debug.LogWarning("Voxel costume: the bake's skeleton does not bind ("+e.Message+"); costumed pilots wear their outfit.");return null;}
            var joints=NativeVoxelCostume.Joints(costumeRig,n=>{if(!rest.TryGetValue(n,out var t))return null;var p=toRoot.MultiplyPoint3x4(t.position);return new NVector3(p.x,p.y,p.z);});
            // (The bank builds the Standard and Sheldon bodies once at load, and keeps the Prototype's arrays: NativeBodySkinModels.cs.)
            var parts=new List<CostumePart>();if(!costumeBank.CostumeParts(skin,tier,parts))return null;
            var build=NativeVoxelCostume.Build(parts,joints,skin,tier);
            if(build==null){Debug.LogWarning("Voxel costume: the bake lacks a joint the fit needs; costumed pilots wear their outfit.");return null;}
            Material template=null;foreach(var r in visualBodyPrefab.GetComponentsInChildren<Renderer>(true))if(r.sharedMaterial!=null&&r.sharedMaterial.HasProperty(UseCustomTextureId)&&r.sharedMaterial.HasProperty(AccentGlowId)){template=r.sharedMaterial;break;}
            if(template==null){Debug.LogWarning("Voxel costume: no toon in the bake; costumed pilots wear their outfit.");return null;}
            var a=new CostumeAsset{Build=build,Palette=CostumePalette(build)};
            a.Main=CostumeMeshOf(build.Main,rest,root,"Voxel costume "+NativeBodySkins.Labels[skin]+" "+tier,out a.MainBones);
            a.Glow=CostumeMeshOf(build.Glow,rest,root,"Voxel costume glow "+NativeBodySkins.Labels[skin]+" "+tier,out a.GlowBones);
            a.MainMaterial=CostumeMaterial(template,a.Palette,false);a.GlowMaterial=CostumeMaterial(template,a.Palette,true);
            // The hat's fit in the head bone's frame.
            if(build.HeadBone!=null&&rest.TryGetValue(build.HeadBone,out var head))
            {
                var toHead=head.worldToLocalMatrix*root.localToWorldMatrix;var top=toHead.MultiplyPoint3x4(CostumeV(build.HeadTop));var front=toHead.MultiplyPoint3x4(CostumeV(build.HeadFront));
                float half=build.HeadHalf/Mathf.Max(1e-4f,head.lossyScale.x/Mathf.Max(1e-4f,root.lossyScale.x));
                NativeVoxelCostume.HatFit(new NVector3(top.x,top.y,top.z),new NVector3(front.x,front.y,front.z),half,out a.HatScale,out var move);a.HatMove=new Vector3(move.X,move.Y,move.Z);
            }
            // The chest core's place on the sternum plate in its bone's frame (the bake's core rides its own bone).
            var core=root.GetComponentsInChildren<Transform>(true);
            foreach(var t in core)if(t.name=="VisualGearCore"&&t.parent!=null&&build.Core.Found){a.CoreParent=t.parent.name;a.CoreLocal=t.parent.worldToLocalMatrix.MultiplyPoint3x4(root.localToWorldMatrix.MultiplyPoint3x4(CostumeV(build.Core.At)));break;}
            if(build.Device.Found)a.DeviceRadius=build.Device.Size;
            costumeAssets[key]=a;
            Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"Voxel costume: {0} tier {1} fitted on the bake ({2} + {3} triangles, {4} paints).",NativeBodySkins.Labels[skin],tier,build.Main.Triangles,build.Glow.Triangles,build.Cells);
            return a;
        }
        private static Vector3 CostumeV(NVector3 v)=>new Vector3(v.X,v.Y,v.Z);
        // The palette: each cell a flat colour (a paint, or a glowing paint's split), linear, no mips (a cell never bleeds).
        private static Texture2D CostumePalette(CostumeBuild build)
        {
            int cells=NativeVoxelCostume.PaletteCells,texels=NativeVoxelCostume.PaletteTexels,size=cells*texels;
            var tex=new Texture2D(size,size,TextureFormat.RGBAHalf,false,true){name="Voxel costume palette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            var px=new Color[size*size];
            for(int c=0;c<cells*cells;c++)
            {
                var p=build.Palette[c];var col=new Color(p.X,p.Y,p.Z,1);int cx=c%cells,cy=c/cells;
                for(int y=0;y<texels;y++)for(int x=0;x<texels;x++)px[(cy*texels+y)*size+cx*texels+x]=col;
            }
            tex.SetPixels(px);tex.Apply(false,true);return tex;
        }
        private static Mesh CostumeMeshOf(CostumeMesh m,Dictionary<string,Transform> rest,Transform root,string name,out string[] bones)
        {
            bones=m.Bones.ToArray();int n=m.Positions.Count;if(n==0)return null;
            var vertices=new Vector3[n];var normals=new Vector3[n];var uv=new Vector2[n];var colors=new Color[n];var weights=new BoneWeight[n];
            for(int i=0;i<n;i++)
            {
                var p=m.Positions[i];var q=m.Normals[i];var u=m.Uv[i];var c=m.Colors[i];
                vertices[i]=new Vector3(p.X,p.Y,p.Z);normals[i]=new Vector3(q.X,q.Y,q.Z);uv[i]=new Vector2(u.X,u.Y);colors[i]=new Color(c.X,c.Y,c.Z,c.W);weights[i]=new BoneWeight{boneIndex0=m.Bone[i],weight0=1};
            }
            var bind=new Matrix4x4[bones.Length];for(int b=0;b<bind.Length;b++)bind[b]=rest[bones[b]].worldToLocalMatrix*root.localToWorldMatrix;
            var mesh=new Mesh{name=name,indexFormat=n>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16,hideFlags=HideFlags.HideAndDontSave};
            mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.colors=colors;mesh.boneWeights=weights;mesh.bindposes=bind;mesh.SetTriangles(m.Indices,0);mesh.RecalculateBounds();
            return mesh;
        }
        // The body's toon on the palette: its nine colours the layers' tints (per renderer), the glow renderer's accent glow its
        // paints' (NativeVoxelCostume.Glow, pulsed per renderer), outlined, cut out by the palette's alpha (whole).
        private static Material CostumeMaterial(Material template,Texture2D palette,bool glow)
        {
            var m=new Material(template){name=glow?"Voxel costume glow":"Voxel costume",hideFlags=HideFlags.HideAndDontSave};
            m.SetTexture(TextureId,palette);m.SetTextureScale(TextureId,Vector2.one);m.SetTextureOffset(TextureId,Vector2.zero);
            m.SetFloat(UseCustomTextureId,1);m.SetFloat(VertexAlbedoId,0);m.SetFloat("_UseOutline",1);m.SetFloat("_Cull",2);m.SetFloat(CutoffId,.5f);
            m.SetFloat(AccentGlowId,glow?NativeVoxelCostume.Glow:0);m.SetVector(SkinToneId,Vector4.zero);
            return m;
        }
    }
}
