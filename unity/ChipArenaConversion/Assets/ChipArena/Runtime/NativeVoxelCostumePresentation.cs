using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    // (VisualDress) in the voxel pilot's blocks, in the variant its skin map's body entry picks, at the graphics setting's tier
    // (the voxel skins' Full, Reduced and Low sculpts; the Prototype has one), fitted once per variant and tier onto the bake's
    // rest skeleton: two skinned meshes (the blocks, and the trims and lights that glow) rigid on the body's own bones, in place
    // of everything the body wears, as the Hot Dog is (NativeHotDogPresentation.cs). So every route that dresses a model body
    // draws it: the live pilot, first person's forearms, corpses and their cuts, the draft, the broadcast's bust, portraits, the
    // Locker, the deploy cast and the eject, the rooftop, killcams and replays (the look travels per seat). It draws in the
    // model's toon (its outline, hit flash, spawn-in, dissolve, cuts and wound pits are the body's own): its texture a palette
    // of the voxel paints, its vertex colours the toon's layers, its nine colours per renderer the pilot's body and accent tints
    // (CostumeTint, from every bind: VisualBodyBind, the view body, death bodies). Hats are the voxel hat sculpts on its head
    // block through the head map (CostumeHatOn), the chest core and wrist device at its anchors (CostumeAnchors), the saya, the
    // Bombadier's satchel, the team bands and the body chips on its blocks (CostumeAnchorShift and the costume's measures); the
    // face gear, the robot plates, the cloth hood and the back emblem are not drawn on it. Each variant and tier is built ahead
    // (CostumeWarmStep: the fit on a worker, the meshes once a frame) while a seat on show wears it, so dressing a body finds it
    // ready; a build that fails is tried again after CostumeRetry seconds. Without the bake the pilot draws the voxel style in the
    // same variant. Presentation only.
    public sealed partial class ArenaPresentation
    {
        public const string CostumeRenderer="VoxelCostume",CostumeGlowRenderer="VoxelCostume glow",CostumeHat="VoxelCostume hat";
        // The dressed look's own entry naming the variant and the tier (VisualLookOf adds it; never sent or saved: the variant
        // travels as the skin map's body entry), so a body is dressed again when only the variant or the setting changes
        // (VisualRetone).
        private const string CostumeSkinEntry="voxelbody:";
        /// <summary>A failed build is tried again after this long (s): a bank or bake still loading is not a failure for the session.</summary>
        private const float CostumeRetry=15;
        /// <summary>The saya's and the satchel's room off the costume's hip block (m: half their thickness).</summary>
        private const float CostumeSayaClear=.03f,CostumeSatchelClear=.055f;
        private sealed class CostumeAsset
        {
            public CostumeBuild Build;public Mesh Main,Glow;public int[] MainBones,GlowBones;public Material MainMaterial,GlowMaterial;public Texture2D Palette;
            // The head map in the head bone's frame (a voxel hat on the block), the gear's places in their bones' frames.
            public Vector3 HatPosition,HatScale=Vector3.one;public Quaternion HatRotation=Quaternion.identity;public string CoreParent;public Vector3 CoreLocal;public float DeviceRadius;
            // The renderers' root bone (the skeleton's transform index) and their bounds in its frame, for culling offscreen.
            public int RootBone=-1;public Bounds Bounds;
            // The saya (in the pelvis bone's frame) and the satchel (in the pelvis joint's frame) moved onto the hip block.
            public Vector3 SayaShift,SatchelShift;
            public double BuildMs,MeshMs;
        }
        private static readonly Dictionary<int,CostumeAsset> costumeAssets=new Dictionary<int,CostumeAsset>();
        private static readonly Dictionary<int,float> costumeFailedAt=new Dictionary<int,float>();
        private static readonly Dictionary<int,Task<CostumeBuild>> costumeJobs=new Dictionary<int,Task<CostumeBuild>>();
        private static NativeSourceModels costumeBank;private static NativeVisualRig.Rig costumeRig;private static CostumeJoint[] costumeJoints;
        // The bake skeleton's transforms in hierarchy order (every instance of the prefab lists them alike) by name.
        private static Dictionary<string,int> costumeSkeletonIndex;
        // A dressed body's costume renderers by its root, and the tints they last took.
        private sealed class CostumeWorn{public SkinnedMeshRenderer Main,Glow;public CostumeAsset Asset;public string Body,Accent;public int Skin=-1;public float Pulse=-1;public MaterialPropertyBlock Block;}
        private static readonly Dictionary<GameObject,CostumeWorn> costumeByRoot=new Dictionary<GameObject,CostumeWorn>();
        private static readonly NVector3[] costumeTints=new NVector3[9];

        /// <summary>The variant a costumed pilot wears (its skin map's body entry; the view body is the local seat's).</summary>
        private int CostumeSkinOf(int id)=>BodySkin(id==ViewBodyId?LocalId:id);
        /// <summary>The tier a costume draws at: the graphics setting's (Low graphics the Low sculpt, Reduced effects the Reduced),
        /// first person's for the view body; the Prototype's one sculpt at every tier.</summary>
        private int CostumeTierOf(int id,int skin)=>skin==NativeBodySkins.PrototypeIndex?NativeBodySkins.Full:NativeBodySkins.Tier(LowGraphics,WeaponEffectsQuality,0,id==ViewBodyId);
        private readonly Dictionary<(string,int,int),string> costumeLooks=new Dictionary<(string,int,int),string>();
        /// <summary>A dressed costumed look with its variant and tier entry (cached: choosing it allocates nothing after the first time).</summary>
        private string CostumeLook(string dressed,int skin,int tier)
        {
            var key=(dressed,skin,tier);if(!costumeLooks.TryGetValue(key,out var look)){if(costumeLooks.Count>64)costumeLooks.Clear();costumeLooks[key]=look=dressed+","+CostumeSkinEntry+skin+tier;}
            return look;
        }
        /// <summary>The variant a dressed look names (Standard when it names none). Allocation free.</summary>
        private static int CostumeSkinIn(string look)=>CostumeDigit(look,0,NativeBodySkins.StandardIndex,NativeBodySkins.Count);
        /// <summary>The tier a dressed look names (Full when it names none). Allocation free.</summary>
        private static int CostumeTierIn(string look)=>CostumeDigit(look,1,NativeBodySkins.Full,NativeBodySkins.Tiers);
        private static int CostumeDigit(string look,int at,int none,int count)
        {
            if(string.IsNullOrEmpty(look))return none;int i=look.IndexOf(CostumeSkinEntry,StringComparison.Ordinal);
            if(i<0||i+CostumeSkinEntry.Length+at>=look.Length)return none;int k=look[i+CostumeSkinEntry.Length+at]-'0';
            return k>=0&&k<count?k:none;
        }
        /// <summary>Whether a body (`root`) wears the costume (its renderer is there).</summary>
        private static bool CostumeOn(GameObject root)=>root!=null&&costumeByRoot.TryGetValue(root,out var c)&&c.Main!=null;
        /// <summary>The costume a body wears (null for none).</summary>
        private static CostumeAsset CostumeOf(GameObject root)=>root!=null&&costumeByRoot.TryGetValue(root,out var c)&&c.Main!=null?c.Asset:null;
        private static int CostumeKey(int skin,int tier)=>skin*NativeBodySkins.Tiers+tier;

        /// <summary>VisualDress, in the costume: everything the body would wear off, the costume's two meshes on the body's bones,
        /// its hat anchor and the gear at its anchors. Without a build (no bank yet, a bake without a bone the fit needs) the outfit
        /// stays as it is.</summary>
        private static void CostumeWear(GameObject root,string look)
        {
            var asset=CostumeAssetFor(CostumeSkinIn(look),CostumeTierIn(look));if(asset==null||root==null||costumeSkeletonIndex==null)return;
            var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);var weaponHolder=root.transform.Find("VisualWeapons");int layer=root.layer;
            foreach(var s in skins)if(s.sharedMesh!=null&&s.name.StartsWith("Body.",StringComparison.Ordinal)){layer=s.gameObject.layer;break;}
            var skeleton=root.transform.Find("VisualSkeleton");if(skeleton==null)return;
            var bones=skeleton.GetComponentsInChildren<Transform>(true);
            var worn=new CostumeWorn{Block=new MaterialPropertyBlock(),Asset=asset};
            worn.Main=CostumeRendererMake(root,asset,asset.Main,asset.MainBones,asset.MainMaterial,CostumeRenderer,bones,layer);
            worn.Glow=CostumeRendererMake(root,asset,asset.Glow,asset.GlowBones,asset.GlowMaterial,CostumeGlowRenderer,bones,layer);
            if(worn.Main==null){if(worn.Glow!=null)Destroy(worn.Glow.gameObject);return;}
            // (The outfit goes only once the costume is on: a body the costume cannot bind keeps its clothes.)
            bool keepHeld=NativeVoxelCostume.Keeps(NativeVoxelCostume.Piece.HeldWeapon);
            foreach(var s in skins)if(!keepHeld||weaponHolder==null||!s.transform.IsChildOf(weaponHolder))s.gameObject.SetActive(false);
            if(costumeByRoot.Count>256){var gone=new List<GameObject>();foreach(var k in costumeByRoot.Keys)if(k==null)gone.Add(k);foreach(var k in gone)costumeByRoot.Remove(k);}
            costumeByRoot[root]=worn;CostumeTintOn(worn,"#e5e7eb","#70e1d0",CostumeSkinIn(look),null,null);
            // Hats ride an anchor on the head bone: the head map, so a voxel hat sits on the block as on the voxel pilot's head.
            if(costumeSkeletonIndex.TryGetValue(asset.Build.HeadBone??"",out int hi)&&hi<bones.Length)
            {var hat=new GameObject(CostumeHat).transform;hat.SetParent(bones[hi],false);hat.localPosition=asset.HatPosition;hat.localRotation=asset.HatRotation;hat.localScale=asset.HatScale;}
            CostumeAnchors(root,asset);
        }
        // A costume mesh's renderer on a body's bones (by the skeleton's hierarchy order, as the bake's every instance lists it),
        // culled offscreen on the body's own root bone.
        private static SkinnedMeshRenderer CostumeRendererMake(GameObject root,CostumeAsset asset,Mesh mesh,int[] bones,Material material,string name,Transform[] skeleton,int layer)
        {
            if(mesh==null||material==null||mesh.vertexCount==0)return null;var at=new Transform[bones.Length];
            for(int b=0;b<bones.Length;b++){if(bones[b]<0||bones[b]>=skeleton.Length)return null;at[b]=skeleton[bones[b]];}
            var go=new GameObject(name){layer=layer};go.transform.SetParent(root.transform,false);
            var r=go.AddComponent<SkinnedMeshRenderer>();r.sharedMesh=mesh;r.bones=at;r.sharedMaterial=material;
            if(asset.RootBone>=0&&asset.RootBone<skeleton.Length){r.rootBone=skeleton[asset.RootBone];r.localBounds=asset.Bounds;r.updateWhenOffscreen=false;}
            else{r.localBounds=mesh.bounds;r.updateWhenOffscreen=true;}
            r.skinnedMotionVectors=false;return r;
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
        /// <summary>The frame hat `hat` rides on head bone `head`: on a costumed body, the anchor at the head map (a voxel head's
        /// frame on the block); otherwise the head itself (null for null).</summary>
        private static Transform CostumeHatOn(string hat,Transform head)
        {
            if(head==null)return null;var anchor=head.Find(CostumeHat);return anchor!=null?anchor:head;
        }
        /// <summary>Whether `head` is a costume's hat anchor (a hat there is the voxel sculpt in the head map's frame, no fit).</summary>
        private static bool CostumeHatAnchor(Transform head)=>head!=null&&head.name==CostumeHat;
        /// <summary>The bank model of hat `hat` on a pilot whose rig `rig` draws its body this frame: the voxel sculpt made for the
        /// voxel head on a costumed body (the plan's "hat-* at scale 1", on the head map), else the model's own where it has one.</summary>
        private string VisualHatModel(string hat,string color,CapturedAnimation.Player rig)
        {
            if(rig!=null&&visualBodies.TryGetValue(rig,out var body)&&CostumeOn(body.Root))return HatModel(hat,color);
            return VisualHatModel(hat,color);
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

        // ---- ahead of use: the costumes the seats on show wear, built before their bodies are dressed ----
        /// <summary>Once a frame (VisualCorpsePoolStep): the next costume the local pilot or a displayed seat wears that is not
        /// built yet is started on a worker (the fit is engine-free), and a finished one made into meshes; one a frame at most.</summary>
        private void CostumeWarmStep()
        {
            if(visualBodyPrefab==null||sourceModels==null)return;costumeBank=sourceModels;
            int done=-1;foreach(var pair in costumeJobs)if(pair.Value.IsCompleted){done=pair.Key;break;}
            if(done>=0){CostumeAssetFor(done/NativeBodySkins.Tiers,done%NativeBodySkins.Tiers);return;}
            if(NativeVoxelCostume.Worn(LocalLook)&&CostumeWarm(LocalId))return;
            var seats=WeaponLoadout?.Match?.Seats;if(seats==null)return;
            for(int i=0;i<seats.Count;i++){var s=seats[i];if(s!=null&&NativeVoxelCostume.Worn(s.look)&&CostumeWarm(s.id))return;}
        }
        // Pilot `id`'s costume started on a worker when neither built nor building nor failed a moment ago; true when started.
        private bool CostumeWarm(int id)
        {
            int skin=CostumeSkinOf(id),key=CostumeKey(skin,CostumeTierOf(id,skin));
            if(costumeAssets.ContainsKey(key)||costumeJobs.ContainsKey(key)||costumeFailedAt.TryGetValue(key,out float at)&&Time.realtimeSinceStartup-at<CostumeRetry)return false;
            if(!CostumeReady(out _))return false;
            var parts=new List<CostumePart>();if(!costumeBank.CostumeParts(skin,key%NativeBodySkins.Tiers,parts)){CostumeFailed(key,"the bank has no parts for it yet");return false;}
            var joints=costumeJoints;int tier=key%NativeBodySkins.Tiers;
            costumeJobs[key]=Task.Run(()=>{var watch=System.Diagnostics.Stopwatch.StartNew();var b=NativeVoxelCostume.Build(parts,joints,skin,tier);if(b!=null)b.BuildMs=watch.Elapsed.TotalMilliseconds;return b;});
            return true;
        }
        private static void CostumeFailed(int key,string why)
        {
            bool first=!costumeFailedAt.ContainsKey(key);costumeFailedAt[key]=Time.realtimeSinceStartup;
            if(first)Debug.LogWarning("Voxel costume: "+NativeBodySkins.Labels[key/NativeBodySkins.Tiers]+" tier "+key%NativeBodySkins.Tiers+": "+why+"; costumed pilots wear their outfit (tried again in "+CostumeRetry+" s).");
        }
        // The bake's rest skeleton bound and its joints read (once); false (and why) where it cannot be.
        private static bool CostumeReady(out string why)
        {
            why=null;if(costumeJoints!=null)return true;if(visualBodyPrefab==null||costumeBank==null){why="no bake or bank yet";return false;}
            var root=visualBodyPrefab.transform;var skel=root.Find("VisualSkeleton");if(skel==null){why="the bake has no skeleton";return false;}
            var all=skel.GetComponentsInChildren<Transform>(true);var index=new Dictionary<string,int>(StringComparer.Ordinal);for(int i=0;i<all.Length;i++)if(!index.ContainsKey(all[i].name))index[all[i].name]=i;
            var toRoot=root.worldToLocalMatrix;
            try{costumeRig??=new NativeVisualRigBinding(root,skel).Rig;}catch(Exception e){why="the bake's skeleton does not bind ("+e.Message+")";return false;}
            costumeJoints=NativeVoxelCostume.Joints(costumeRig,n=>{if(!index.TryGetValue(n,out int i))return null;var p=toRoot.MultiplyPoint3x4(all[i].position);return new NVector3(p.x,p.y,p.z);});
            costumeSkeletonIndex=index;return true;
        }

        // ---- the shared builds: once per variant and tier, from the bake's rest skeleton ----
        private static CostumeAsset CostumeAssetFor(int skin,int tier)
        {
            if(skin==NativeBodySkins.PrototypeIndex)tier=NativeBodySkins.Full;
            int key=CostumeKey(skin,tier);if(costumeAssets.TryGetValue(key,out var made))return made;
            if(costumeFailedAt.TryGetValue(key,out float failed)&&Time.realtimeSinceStartup-failed<CostumeRetry)return null;
            if(!CostumeReady(out string why)){if(visualBodyPrefab!=null&&costumeBank!=null)CostumeFailed(key,why);return null;}
            CostumeBuild build;
            if(costumeJobs.TryGetValue(key,out var job)){costumeJobs.Remove(key);try{build=job.Result;}catch(Exception e){build=null;Debug.LogWarning("Voxel costume: the fit failed ("+e.GetBaseException().Message+").");}}
            else
            {
                var parts=new List<CostumePart>();if(!costumeBank.CostumeParts(skin,tier,parts)){CostumeFailed(key,"the bank has no parts for it yet");return null;}
                var watch=System.Diagnostics.Stopwatch.StartNew();build=NativeVoxelCostume.Build(parts,costumeJoints,skin,tier);if(build!=null)build.BuildMs=watch.Elapsed.TotalMilliseconds;
            }
            if(build==null){CostumeFailed(key,"the bake lacks a joint the fit needs");return null;}
            var mesh=System.Diagnostics.Stopwatch.StartNew();
            var root=visualBodyPrefab.transform;var skel=root.Find("VisualSkeleton");var all=skel.GetComponentsInChildren<Transform>(true);
            Material template=null;foreach(var r in visualBodyPrefab.GetComponentsInChildren<Renderer>(true))if(r.sharedMaterial!=null&&r.sharedMaterial.HasProperty(UseCustomTextureId)&&r.sharedMaterial.HasProperty(AccentGlowId)){template=r.sharedMaterial;break;}
            if(template==null){CostumeFailed(key,"no toon in the bake");return null;}
            var a=new CostumeAsset{Build=build,Palette=CostumePalette(build)};
            a.Main=CostumeMeshOf(build.Main,all,root,"Voxel costume "+NativeBodySkins.Labels[skin]+" "+tier,out a.MainBones);
            a.Glow=CostumeMeshOf(build.Glow,all,root,"Voxel costume glow "+NativeBodySkins.Labels[skin]+" "+tier,out a.GlowBones);
            a.MainMaterial=CostumeMaterial(template,a.Palette,false);a.GlowMaterial=CostumeMaterial(template,a.Palette,true);
            Transform Bone(string name)=>name!=null&&costumeSkeletonIndex.TryGetValue(name,out int i)&&i<all.Length?all[i]:null;
            // The head map in the head bone's frame: a voxel hat's point p at the block's HeadOrigin + p * HeadSize.
            var head=Bone(build.HeadBone);
            if(head!=null)
            {
                var m=head.worldToLocalMatrix*root.localToWorldMatrix*Matrix4x4.TRS(CostumeV(build.HeadOrigin),Quaternion.identity,Vector3.one*build.HeadSize);
                a.HatPosition=m.GetColumn(3);a.HatRotation=m.rotation;a.HatScale=m.lossyScale;
            }
            // The chest core's place on the sternum plate in its bone's frame (the bake's core rides its own bone).
            foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name=="VisualGearCore"&&t.parent!=null&&build.Core.Found){a.CoreParent=t.parent.name;a.CoreLocal=t.parent.worldToLocalMatrix.MultiplyPoint3x4(root.localToWorldMatrix.MultiplyPoint3x4(CostumeV(build.Core.At)));break;}
            if(build.Device.Found)a.DeviceRadius=build.Device.Size;
            // Culled on the body's own renderers' root bone, the costume's rest bounds in its frame with room for the pose.
            foreach(var r in visualBodyPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(r.rootBone!=null&&r.name.StartsWith("Body.",StringComparison.Ordinal)&&costumeSkeletonIndex.TryGetValue(r.rootBone.name,out int rb)){a.RootBone=rb;break;}
            if(a.RootBone>=0&&a.Main!=null)
            {
                var toBone=all[a.RootBone].worldToLocalMatrix*root.localToWorldMatrix;var bounds=new Bounds(toBone.MultiplyPoint3x4(a.Main.bounds.center),Vector3.zero);
                foreach(var mesh2 in new[]{a.Main,a.Glow})if(mesh2!=null){var b=mesh2.bounds;for(int c=0;c<8;c++)bounds.Encapsulate(toBone.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((c&1)!=0?1:-1,(c&2)!=0?1:-1,(c&4)!=0?1:-1))));}
                // (.8 m of room for the pose, in the root bone's own units.)
                bounds.Expand(.8f/Mathf.Max(1e-4f,all[a.RootBone].lossyScale.x/Mathf.Max(1e-4f,root.lossyScale.x)));a.Bounds=bounds;
            }
            // The saya and the satchel seated on the hip block (Core NativeVoxelCostume.Seat): the saya's rest place on the pelvis
            // bone (Core NativeKatanaSaya.RestAt) and the satchel's in the pelvis joint's upright frame (NativeBombPresentation.cs BombGear).
            var pelvis=Bone(costumeJoints[CapturedRig.Pelvis].Bone);
            if(pelvis!=null)
            {
                var toRoot=root.worldToLocalMatrix;Vector3 R(Vector3 world)=>toRoot.MultiplyPoint3x4(world);NVector3 N(Vector3 v)=>new NVector3(v.x,v.y,v.z);
                var centre=N(R(pelvis.position));
                // (KatanaSaya: the pelvis bone's rigid frame, its position and turn without its scale.)
                var saya=N(R(pelvis.position+pelvis.rotation*SayaVec(NativeKatanaSaya.RestAt)));
                var seated=NativeVoxelCostume.Seat(build,saya,centre,CostumeSayaClear,build.Main.Bones.Contains(pelvis.name)?pelvis.name:null);
                a.SayaShift=Quaternion.Inverse(pelvis.rotation)*root.TransformVector(CostumeV(seated-saya));
                var satchel=centre+new NVector3(BombSatchelAt.x,BombSatchelAt.y,BombSatchelAt.z);
                a.SatchelShift=CostumeV(NativeVoxelCostume.Seat(build,satchel,centre,CostumeSatchelClear,build.Main.Bones.Contains(pelvis.name)?pelvis.name:null)-satchel);
            }
            a.MeshMs=mesh.Elapsed.TotalMilliseconds;costumeAssets[key]=a;costumeFailedAt.Remove(key);
            Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"Voxel costume: {0} tier {1} fitted on the bake ({2} + {3} triangles, {4} paints): the fit {5:0.0} ms ({6}), its meshes {7:0.0} ms on the main thread.",NativeBodySkins.Labels[skin],tier,build.Main.Triangles,build.Glow.Triangles,build.Cells,build.BuildMs,job!=null?"on a worker":"on the main thread",a.MeshMs);
            return a;
        }
        private static Vector3 CostumeV(NVector3 v)=>new Vector3(v.X,v.Y,v.Z);
        /// <summary>A costumed body's saya shift (the pelvis bone's frame) and satchel shift (the pelvis joint's), zero without the costume.</summary>
        private static Vector3 CostumeSayaShift(GameObject root)=>CostumeOf(root)?.SayaShift??Vector3.zero;
        private static Vector3 CostumeSatchelShift(GameObject root)=>CostumeOf(root)?.SatchelShift??Vector3.zero;
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
        // A built mesh with its bones as the skeleton's hierarchy indices (the bind poses read off the bake at rest).
        private static Mesh CostumeMeshOf(CostumeMesh m,Transform[] skeleton,Transform root,string name,out int[] bones)
        {
            bones=new int[m.Bones.Count];for(int b=0;b<bones.Length;b++)bones[b]=costumeSkeletonIndex.TryGetValue(m.Bones[b],out int i)?i:-1;
            int n=m.Positions.Count;if(n==0)return null;
            var vertices=new Vector3[n];var normals=new Vector3[n];var uv=new Vector2[n];var colors=new Color[n];var weights=new BoneWeight[n];
            for(int i=0;i<n;i++)
            {
                var p=m.Positions[i];var q=m.Normals[i];var u=m.Uv[i];var c=m.Colors[i];
                vertices[i]=new Vector3(p.X,p.Y,p.Z);normals[i]=new Vector3(q.X,q.Y,q.Z);uv[i]=new Vector2(u.X,u.Y);colors[i]=new Color(c.X,c.Y,c.Z,c.W);weights[i]=new BoneWeight{boneIndex0=m.Bone[i],weight0=1};
            }
            var bind=new Matrix4x4[bones.Length];for(int b=0;b<bind.Length;b++)bind[b]=bones[b]>=0?skeleton[bones[b]].worldToLocalMatrix*root.localToWorldMatrix:Matrix4x4.identity;
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
