using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The zombies on the player's model (owner 2026-10-29: "Replace the old zombie appearance with models that belong beside the
    // current player character"; docs/ZOMBIE-MODELS.md, Core/NativeZombieLooks). An add of a modelled kind on the full tier (the
    // nearest 24, 16 on the low graphics tier) draws the bake's model in its variant in place of its voxel model, posed from the
    // same game pose its gait made (NativeVisualRig: the model's own proportions, planted feet), so its steps, blows, staggers,
    // entrances and death are the gait's, on the authority's clock and timings. The reduced and impostor tiers stay voxel.
    //  - A variant: its pieces (the first candidate the bake carries), covered parts, tag shapes, body and face shapes by name on
    //    every worn mesh, the bone build (twist-bone girth, hands, head, shoulders; set before the binding reads the rest pose),
    //    each piece's colour, the skin tone, the blank eyes and the wear, all written once when a body is made.
    //  - Bodies are kept per variant and reused (a spare is posed fresh); new ones come from their own pool of the bake's
    //    instances, made ahead off the main thread while a horde is about, at most a few a frame (an add without one this frame
    //    draws voxel), so a wave's arrival never stalls the frame.
    //  - Severed, sliced and gibbed parts collapse on the model at their joint (the gore's raw cap and blood are the voxel
    //    path's; the flying part is still its voxel part). A settled corpse is baked once into static meshes and drawn from them,
    //    sinking with the voxel corpse's timeline, and its body goes back to the spares.
    //  - The voxel models stay where the bake is absent, with --ca-voxel-zombies, while Settings · Player model is Classic and
    //    for kinds not yet modelled (Core NativeZombieLooks.Live; --ca-zombie-drafts draws the drafts of every kind).
    // Presentation only: nothing here is read by a rule, a hit volume, a timing, the AI or the network.
    public sealed partial class ArenaPresentation
    {
        public const string VoxelZombiesArgument="--ca-voxel-zombies",ZombieDraftsArgument="--ca-zombie-drafts";
        /// <summary>New bodies made a frame (from the pool, or one made at once when it is empty), and spares kept per variant.</summary>
        public const int ZombieMakesPerFrame=3,ZombieSparesPerVariant=6;
        private sealed class ZombieBody
        {
            public VisualBody Body;public NativeZombieLooks.Variant Variant;public int Id,Kind,PosedFrame=-10,Hidden=-1;public float Flash=1,SeenAt,Eye=-1;
            // The model bone of each game joint and its rest scale (a lost part's bone collapses).
            public readonly Transform[] Joint=new Transform[CapturedRig.Bones.Length];public readonly Vector3[] JointScale=new Vector3[CapturedRig.Bones.Length];
            public bool Dying;
        }
        private readonly Dictionary<int,ZombieBody> zombieBodies=new Dictionary<int,ZombieBody>();private readonly List<int> zombieBodiesGone=new List<int>();
        private readonly Dictionary<NativeZombieLooks.Variant,Stack<ZombieBody>> zombieSpares=new Dictionary<NativeZombieLooks.Variant,Stack<ZombieBody>>();
        private static int zombieArgs;private static bool zombieModelsFailed;private int zombieMakesFrame=-1,zombieMakes;
        private bool ZombieModelsOn
        {
            get
            {
                if(zombieArgs==0){var args=Environment.GetCommandLineArgs();zombieArgs=Array.IndexOf(args,VoxelZombiesArgument)>=0?-1:1;if(Array.IndexOf(args,ZombieDraftsArgument)>=0)NativeZombieLooks.Drafts=true;}
                return zombieArgs>0&&!zombieModelsFailed&&!Viewer&&sourceModels!=null&&!ClassicBodies&&VisualBodiesOn;
            }
        }
        /// <summary>Whether add `id` of `kind` draws on the model on `tier` (its wounds then go with the model's, not the voxel surface).</summary>
        private bool ZombieModelled(int kind,int tier)=>tier==NativeHordeModels.Full&&NativeZombieLooks.Modelled(kind)&&ZombieModelsOn;
        private bool ZombieModelDrawn(int id)=>zombieBodies.TryGetValue(id,out var z)&&z.PosedFrame>=Time.frameCount-1;

        // ================================================================================================================
        // Posing (HordeKindAdd, HordeKindDeath): the model in place of the voxel model
        // ================================================================================================================
        /// <summary>A living add (its rig posed from minionPose this frame): its model posed, true; false draws its voxel model.</summary>
        private bool ZombieModelPose(CapturedAnimation.Player rig,int id,int kind,int tier,bool planted,bool airborne,int lost,int hidden,float flash,float glow)
        {
            if(rig==null||!ZombieModelled(kind,tier))return false;var z=ZombieModelOf(id,kind);if(z==null)return false;
            z.Dying=false;ZombieModelBind(z,rig,planted,airborne,lost,hidden,flash,glow);return true;
        }
        /// <summary>A dying add's fall (HordeKindDeath, its rig posed from minionPose): its model, true; false: the voxel body falls.</summary>
        private bool ZombieModelDeath(CapturedAnimation.Player rig,int id,int kind,int lost,int hidden)
        {
            if(rig==null||!zombieBodies.TryGetValue(id,out var z)||z.Body.Root==null||z.Kind!=kind||!ZombieModelsOn)return false;
            z.Dying=true;ZombieModelBind(z,rig,false,false,lost,hidden,1,0);return true;
        }
        private void ZombieModelBind(ZombieBody z,CapturedAnimation.Player rig,bool planted,bool airborne,int lost,int hidden,float flash,float glow)
        {
            var root=z.Body.Root;var t=root.transform;var rt=rig.Root.transform;
            // The floor point under the rig root, as a pilot's body (NativeVisualRig.Floor), at the rig's scale (the Hulk's).
            t.SetPositionAndRotation(rt.TransformPoint(0,NativeVisualRig.Floor,0),rt.rotation);t.localScale=rt.lossyScale;
            if(!root.activeSelf)root.SetActive(true);
            if(z.PosedFrame<Time.frameCount-1)z.Body.Binding.State.Forget();
            var pose=minionPose;pose.World();
            z.Body.Binding.Apply(pose,default(NativeVisualRig.Grasp),planted,airborne);
            ZombieCollapse(z,lost,hidden);ZombieFlash(z,flash,glow);
            z.PosedFrame=Time.frameCount;z.SeenAt=Time.unscaledTime;
        }
        // The parts gone (the authority's lost limbs, a slice's or a burst's hidden joints): each root part's bone collapsed to its
        // joint (its subtree with it), the rest at their rest scale. Written only when the set changes.
        private static void ZombieCollapse(ZombieBody z,int lost,int hidden)
        {
            int n=CapturedRig.Bones.Length,mask=0;
            for(int j=1;j<n;j++)if(!HordeJointShown(lost,j)||hidden!=0&&HordeHiddenJoint(hidden,j))mask|=1<<j;
            if(mask==z.Hidden)return;z.Hidden=mask;
            for(int j=1;j<n;j++)if(z.Joint[j]!=null)z.Joint[j].localScale=z.JointScale[j];
            for(int j=1;j<n;j++){if((mask&(1<<j))==0||z.Joint[j]==null)continue;int p=CapturedRig.Bones[j].Parent;if(p>0&&(mask&(1<<p))!=0)continue;z.Joint[j].localScale=Vector3.one*1e-4f;}
        }
        // The struck flash (the voxel parts' code) and the eyes' glow through a wind-up, per renderer, written when they change.
        private static void ZombieFlash(ZombieBody z,float flash,float glow)
        {
            float eye=1.35f+.9f*Mathf.Clamp01(glow-1);
            if(flash==z.Flash&&Mathf.Abs(eye-z.Eye)<.02f)return;bool eyes=Mathf.Abs(eye-z.Eye)>=.02f;z.Flash=flash;z.Eye=eye;var b=z.Body.Block;
            foreach(var r in z.Body.All)
            {
                if(r==null)continue;r.GetPropertyBlock(b);b.SetFloat(HitFlashId,flash);
                if(eyes&&ZombieEyePart(r)){var e=b.GetVector(ZombieEyeId);if(e.w>.5f){e.w=eye;b.SetVector(ZombieEyeId,e);}}
                r.SetPropertyBlock(b);
            }
        }

        // ================================================================================================================
        // Bodies: made per variant, kept and reused
        // ================================================================================================================
        private ZombieBody ZombieModelOf(int id,int kind)
        {
            if(zombieBodies.TryGetValue(id,out var z))
            {
                if(z.Body.Root!=null&&z.Kind==kind)return z;
                ZombieModelSpare(z);zombieBodies.Remove(id);
            }
            var v=NativeZombieLooks.Of(kind,id);if(v==null)return null;
            if(zombieSpares.TryGetValue(v,out var spares))while(spares.Count>0){var s=spares.Pop();if(s.Body.Root==null)continue;z=s;break;}
            if(z==null||z.Body.Root==null)
            {
                if(zombieMakesFrame!=Time.frameCount){zombieMakesFrame=Time.frameCount;zombieMakes=0;}
                if(zombieMakes>=ZombieMakesPerFrame||zombiePool.Count==0)return null;
                zombieMakes++;z=ZombieModelMake(v);if(z==null)return null;
            }
            z.Id=id;z.Kind=kind;z.PosedFrame=-10;z.Dying=false;zombieBodies[id]=z;ZombieReseed(z,id);return z;
        }
        // The wear's patches by the add's own seed (two adds in one variant tear and stain differently).
        private static void ZombieReseed(ZombieBody z,int id)
        {
            float seed=NativeHordeKinds.Hash(NativeHordeKinds.Seed(id),31)*997+1;zombieBlock??=new MaterialPropertyBlock();
            foreach(var r in z.Body.All){if(r==null)continue;r.GetPropertyBlock(zombieBlock);var w=zombieBlock.GetVector(ZombieWearId);if(w.x+w.y+w.z<=0)continue;w.w=seed;zombieBlock.SetVector(ZombieWearId,w);r.SetPropertyBlock(zombieBlock);}
        }
        private ZombieBody ZombieModelMake(NativeZombieLooks.Variant v)
        {
            GameObject root=null;while(root==null&&zombiePool.Count>0)root=zombiePool.Pop();if(root==null)return null;
            root.transform.SetParent(null,false);root.SetActive(true);root.name="Zombie model "+v.Id;
            var skeleton=root.transform.Find("VisualSkeleton");
            if(skeleton==null){Destroy(root);zombieModelsFailed=true;Debug.LogWarning("Zombie models: "+VisualBodyResource+" has no VisualSkeleton; zombies draw voxel.");return null;}
            var worn=ZombieDress(root,v);float lift=ZombieLift(worn);if(lift!=0)skeleton.localPosition+=new Vector3(0,lift,0);
            ZombieBuild(skeleton,v.Build);
            var body=new VisualBody{Root=root,Binding=new NativeVisualRigBinding(root.transform,skeleton),Block=new MaterialPropertyBlock(),Look=v.Id,Lift=lift};
            body.All=VisualWorn(root);body.Cloth=Array.Empty<Renderer>();body.Weapons=VisualWeaponsOf(root.transform);
            foreach(var r in body.All){r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;if(r is SkinnedMeshRenderer s)s.updateWhenOffscreen=false;}
            var z=new ZombieBody{Body=body,Variant=v};
            var of=body.Binding.Rig.Of;
            for(int j=0;j<z.Joint.Length;j++){int b=j<of.Length?of[j]:-1;if(b<0||b>=body.Binding.Bones.Length)continue;z.Joint[j]=body.Binding.Bones[b];z.JointScale[j]=z.Joint[j].localScale;}
            ZombieModelLog(root);
            return z;
        }
        // The bake's instances the zombies are made from, made ahead off the main thread (InstantiateAsync, under an inactive holder)
        // while a horde is about, never at once (about 8 ms each); the pilots' corpse pool is left to the pilots.
        private const int ZombiePoolTarget=8,ZombiePoolBatch=4;
        private static readonly Stack<GameObject> zombiePool=new Stack<GameObject>();private static AsyncInstantiateOperation<GameObject> zombieFill;private static GameObject zombieHolder;
        /// <summary>HordeKindsBegin: the pool topped up while the arena has a horde (or adds) and the models are on.</summary>
        private void ZombieModelsPrewarm(NativeArena arena)
        {
            if(zombieFill!=null)
            {
                if(!zombieFill.isDone)return;var ready=zombieFill.Result;
                if(ready!=null)foreach(var made in ready)if(made!=null){made.SetActive(false);made.name="Zombie model (ready)";zombiePool.Push(made);}
                zombieFill=null;
            }
            if(arena==null||arena.Pve==null&&arena.Minions.Count==0||!ZombieModelsOn||zombiePool.Count>=ZombiePoolTarget)return;
            if(zombieHolder==null){zombieHolder=new GameObject("Zombie model pool");zombieHolder.SetActive(false);DontDestroyOnLoad(zombieHolder);}
            zombieFill=InstantiateAsync(visualBodyPrefab,ZombiePoolBatch,zombieHolder.transform);
        }
        // A body no add wears any more: back to its variant's spares (hidden, whole), or taken apart past the spares kept.
        private void ZombieModelSpare(ZombieBody z)
        {
            if(z?.Body?.Root==null)return;
            if(!zombieSpares.TryGetValue(z.Variant,out var spares)){spares=new Stack<ZombieBody>();zombieSpares[z.Variant]=spares;}
            if(spares.Count>=ZombieSparesPerVariant){VisualBodyGone(z.Body.Root);z.Body.Root=null;return;}
            z.Body.Root.SetActive(false);ZombieCollapse(z,0,0);ZombieFlash(z,1,0);z.Id=0;z.Dying=false;spares.Push(z);
        }
        /// <summary>After the deaths (DeathPresentation): the bodies posed this frame show, the rest hide; one unposed for 2 s goes
        /// back to the spares.</summary>
        private void ZombieModelsShown()
        {
            if(zombieBodies.Count==0)return;zombieBodiesGone.Clear();
            foreach(var pair in zombieBodies)
            {
                var z=pair.Value;if(z.Body.Root==null){zombieBodiesGone.Add(pair.Key);continue;}
                bool posed=z.PosedFrame==Time.frameCount;if(z.Body.Root.activeSelf!=posed)z.Body.Root.SetActive(posed);
                if(!posed&&Time.unscaledTime-z.SeenAt>2)zombieBodiesGone.Add(pair.Key);
            }
            foreach(int id in zombieBodiesGone){ZombieModelSpare(zombieBodies[id]);zombieBodies.Remove(id);}
        }
        private void ClearZombieModels()
        {
            foreach(var z in zombieBodies.Values)ZombieModelSpare(z);zombieBodies.Clear();
            foreach(var c in zombieCorpses.Values)ZombieCorpseFree(c);zombieCorpses.Clear();
        }

        // ================================================================================================================
        // Dressing a variant (once per body made)
        // ================================================================================================================
        private static readonly int ZombieTintId=Shader.PropertyToID("_ZombieTint"),ZombieWearId=Shader.PropertyToID("_ZombieWear"),ZombieEyeId=Shader.PropertyToID("_ZombieEye"),ZombieSkinId=Shader.PropertyToID("_ZombieSkin");
        private static readonly Dictionary<Mesh,int[]> zombieShapeIndex=new Dictionary<Mesh,int[]>();
        private static MaterialPropertyBlock zombieBlock;
        /// <summary>A body in variant `v`: its pieces on (the first candidate per slot the bake's look data has; none on a bare slot), the
        /// rest off, the covered parts off, the tag shapes, the body and face shapes, the colours, skin, eyes and wear. The worn
        /// pieces by slot (null where none).</summary>
        private static LookPiece[] ZombieDress(GameObject root,NativeZombieLooks.Variant v)
        {
            var worn=new LookPiece[NativeLook.Slots.Length];var d=VisualLookData();
            lookNamed.Clear();foreach(var r in root.GetComponentsInChildren<Renderer>(true))lookNamed[r.name]=r;
            if(d!=null)
            {
                for(int s=0;s<worn.Length;s++)foreach(var id in NativeZombieLooks.Candidates(v.Pieces[s]))
                {
                    if(id==NativeZombieLooks.None)break;LookPiece hit=null;foreach(var p in d.pieces)if(p!=null&&p.slot==NativeLook.Slots[s]&&p.id==id){hit=p;break;}
                    if(hit!=null){worn[s]=hit;break;}
                }
                lookCovers.Clear();lookTags.Clear();if(d.always!=null)lookTags.UnionWith(d.always);
                foreach(var p in worn)if(p!=null){if(p.covers!=null)lookCovers.UnionWith(p.covers);if(p.tags!=null)lookTags.UnionWith(p.tags);}
                foreach(var p in d.pieces)if(p?.renderers!=null)foreach(var n in p.renderers)if(lookNamed.TryGetValue(n,out var r))r.gameObject.SetActive(false);
                foreach(var p in worn)if(p?.renderers!=null)foreach(var n in p.renderers)if(lookNamed.TryGetValue(n,out var r))r.gameObject.SetActive(true);
                if(d.body!=null)foreach(var b in d.body)if(b!=null&&lookNamed.TryGetValue(b.renderer,out var r))r.gameObject.SetActive(!lookCovers.Contains(b.part));
                if(d.shapes!=null)foreach(var t in d.shapes)if(t!=null&&lookNamed.TryGetValue(t.renderer,out var r)&&r is SkinnedMeshRenderer skin&&skin.sharedMesh!=null&&t.shape>=0&&t.shape<skin.sharedMesh.blendShapeCount)skin.SetBlendShapeWeight(t.shape,lookTags.Contains(t.tag)?100:0);
            }
            // A bald head: no hair piece at all (an older bake without look data keeps its own).
            if(v.Pieces[0]==NativeZombieLooks.None)foreach(var r in lookNamed.Values)if(r.name.StartsWith("HairFront.",StringComparison.Ordinal)||r.name.StartsWith("HairBack.",StringComparison.Ordinal))r.gameObject.SetActive(false);
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;var skinColour=WearColor(v.Skin);if(linear)skinColour=skinColour.linear;
            foreach(var r in lookNamed.Values)if(VisualSkinPart(r))VisualBaseSkin(r.sharedMaterial);
            float seed=NativeHordeKinds.Hash(NativeGoreRules.Fnv(v.Id),5)*997+1;
            foreach(var r in lookNamed.Values)
            {
                if(!r.gameObject.activeSelf)continue;
                if(r is SkinnedMeshRenderer skin)ZombieShapes(skin,v.Shapes);
                if(VisualSkinPart(r)){VisualSkinToneOne(r,v.Skin);ZombieWearOn(r,0,0,v.Rot,seed,skinColour);continue;}
                if(ZombieEyePart(r)){var e=WearColor(v.Eyes);if(linear)e=e.linear;zombieBlock??=new MaterialPropertyBlock();r.GetPropertyBlock(zombieBlock);zombieBlock.SetVector(ZombieEyeId,new Vector4(e.r,e.g,e.b,1.35f));r.SetPropertyBlock(zombieBlock);continue;}
                int slot=ZombieSlotOf(worn,r.name);if(slot<0)continue;
                if(v.Colours[slot]>=0)ZombieTintOn(r,v.Colours[slot],linear);
                if(slot>0)ZombieWearOn(r,v.Grime,v.Tears,0,seed,skinColour);
            }
            lookNamed.Clear();return worn;
        }
        private static int ZombieSlotOf(LookPiece[] worn,string renderer)
        {
            for(int s=0;s<worn.Length;s++)if(worn[s]?.renderers!=null)foreach(var n in worn[s].renderers)if(n==renderer)return s;
            return renderer.StartsWith("HairFront.",StringComparison.Ordinal)||renderer.StartsWith("HairBack.",StringComparison.Ordinal)?0:-1;
        }
        // The blank eyes: the eye mesh (the bake's Eyes piece; its iris and pupil are in its texture).
        private static bool ZombieEyePart(Renderer r)=>r!=null&&r.name.StartsWith("Eyes.",StringComparison.Ordinal);
        // A piece's colour: rgb in the shader's space, w one over the piece's own main brightness (its layers' first colours).
        private static void ZombieTintOn(Renderer r,int rgb,bool linear)
        {
            var m=r.sharedMaterial;float main=0;int seen=0;
            if(m!=null)foreach(int id in ZombieMainColours)if(m.HasProperty(id)){var c=m.GetColor(id);if(linear)c=c.linear;main+=.299f*c.r+.587f*c.g+.114f*c.b;seen++;}
            main=seen>0?Mathf.Max(main/seen,.04f):.5f;var tint=WearColor(rgb);if(linear)tint=tint.linear;
            zombieBlock??=new MaterialPropertyBlock();r.GetPropertyBlock(zombieBlock);
            zombieBlock.SetVector(ZombieTintId,new Vector4(tint.r,tint.g,tint.b,1/main));
            // The accent channels (a pilot's colour) dulled to the piece's shade: a zombie wears no team trim.
            var dull=new Color(tint.r*.55f,tint.g*.55f,tint.b*.55f);foreach(int id in AccentChannels)zombieBlock.SetColor(id,linear?dull.gamma:dull);
            r.SetPropertyBlock(zombieBlock);
        }
        private static readonly int[] ZombieMainColours={Shader.PropertyToID("_Color_1"),Shader.PropertyToID("_Color_4"),Shader.PropertyToID("_Color_7")};
        private static void ZombieWearOn(Renderer r,float grime,float tears,float rot,float seed,Color skin)
        {
            if(grime+tears+rot<=0)return;zombieBlock??=new MaterialPropertyBlock();r.GetPropertyBlock(zombieBlock);
            zombieBlock.SetVector(ZombieWearId,new Vector4(grime,tears,rot,seed));zombieBlock.SetVector(ZombieSkinId,new Vector4(skin.r,skin.g,skin.b,1));r.SetPropertyBlock(zombieBlock);
        }
        // The body and face shapes by name on a worn mesh (the pack's Shape_* and face shapes; never a Tag_ or Expression_ shape).
        private static void ZombieShapes(SkinnedMeshRenderer skin,float[] weights)
        {
            var mesh=skin.sharedMesh;if(mesh==null||weights==null)return;
            if(!zombieShapeIndex.TryGetValue(mesh,out var index))
            {
                index=new int[NativeZombieLooks.ShapeNames.Length];
                for(int k=0;k<index.Length;k++){index[k]=-1;string want=NativeZombieLooks.ShapeNames[k];
                    for(int i=0;i<mesh.blendShapeCount;i++){string n=mesh.GetBlendShapeName(i);if(NativeZombieLooks.ShapeMatches(n,want)){index[k]=i;break;}}}
                zombieShapeIndex[mesh]=index;
            }
            for(int k=0;k<index.Length&&k<weights.Length;k++)if(index[k]>=0&&!float.IsNaN(weights[k]))skin.SetBlendShapeWeight(index[k],weights[k]);
        }
        // The feet piece's lift against the default shoes' (bare feet down onto the floor), as a pilot's.
        private static float ZombieLift(LookPiece[] worn)
        {
            var d=VisualLookData();if(d==null)return 0;string first=NativeLook.Ids[NativeLook.SlotIndex(NativeLook.Feet)][0];float rest=0;
            foreach(var p in d.pieces)if(p!=null&&p.slot==NativeLook.Feet&&p.id==first)rest=p.lift;
            var feet=worn[NativeLook.SlotIndex(NativeLook.Feet)];return feet==null?0:Mathf.Clamp(feet.lift-rest,-.05f,.05f);
        }
        /// <summary>The bone build on the model's skeleton, before the binding reads its rest pose: a limb's girth on its twist bones
        /// (leaves; their cross-section only, along the limb unchanged), the hands' and head's size, the shoulders' span (the upper
        /// arm set out along its collarbone).</summary>
        private static void ZombieBuild(Transform skeleton,NativeZombieLooks.Build b)
        {
            if(b==null)return;
            foreach(var t in skeleton.GetComponentsInChildren<Transform>(true))
            {
                string n=t.name;
                if(t.childCount==0&&n.IndexOf("twist",StringComparison.OrdinalIgnoreCase)>=0)
                {
                    float g=n.StartsWith("upperarm",StringComparison.Ordinal)?b.Arm:n.StartsWith("lowerarm",StringComparison.Ordinal)?b.Forearm:n.StartsWith("thigh",StringComparison.Ordinal)?b.Thigh:n.StartsWith("calf",StringComparison.Ordinal)?b.Calf:1;
                    if(Mathf.Abs(g-1)>1e-3f)ZombieGirth(t,g);
                }
                else if(n=="hand_l"||n=="hand_r"){if(Mathf.Abs(b.Hand-1)>1e-3f)t.localScale*=b.Hand;}
                else if(n=="head"){if(Mathf.Abs(b.Head-1)>1e-3f)t.localScale*=b.Head;}
                else if(n=="upperarm_l"||n=="upperarm_r"){if(Mathf.Abs(b.Shoulder-1)>1e-3f)t.localPosition*=b.Shoulder;}
            }
        }
        // A twist bone's cross-section scaled by `g`: its local axis nearest the limb (toward the parent's next joint) kept.
        private static void ZombieGirth(Transform twist,float g)
        {
            var parent=twist.parent;if(parent==null)return;Vector3 along=Vector3.zero;
            foreach(Transform c in parent)if(c!=twist&&c.childCount>0&&c.name.IndexOf("twist",StringComparison.OrdinalIgnoreCase)<0){along=c.localPosition;break;}
            if(along.sqrMagnitude<1e-8f)return;var a=Quaternion.Inverse(twist.localRotation)*along;a=new Vector3(Mathf.Abs(a.x),Mathf.Abs(a.y),Mathf.Abs(a.z));
            var s=twist.localScale;if(a.x>=a.y&&a.x>=a.z){s.y*=g;s.z*=g;}else if(a.y>=a.z){s.x*=g;s.z*=g;}else{s.x*=g;s.y*=g;}twist.localScale=s;
        }
        // Once: the bake's renderer and shape names in the log, for fitting the variants on the owner's PC.
        private static bool zombieLogged;
        private static void ZombieModelLog(GameObject root)
        {
            if(zombieLogged)return;zombieLogged=true;var names=new List<string>();bool eyes=false;
            foreach(var r in root.GetComponentsInChildren<Renderer>(true)){names.Add(r.name);eyes|=ZombieEyePart(r);}
            Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"Zombie models: on the bake's model ({0} renderers{1}).",names.Count,eyes?"":"; no Eyes. renderer: the eyes keep the bake's iris");
        }

        // ================================================================================================================
        // Corpses: baked once when the fall settles, drawn sinking with the voxel corpse's timeline
        // ================================================================================================================
        private sealed class ZombieCorpse{public int Id;public float At;public Mesh[] Meshes;public Matrix4x4[] Matrix;public Material[][] Materials;public MaterialPropertyBlock[] Blocks;public int Count;}
        private readonly Dictionary<NativeHordeGoreState.Remains,ZombieCorpse> zombieCorpses=new Dictionary<NativeHordeGoreState.Remains,ZombieCorpse>();
        private readonly List<NativeHordeGoreState.Remains> zombieCorpsesGone=new List<NativeHordeGoreState.Remains>();
        private readonly Stack<Mesh> zombieCorpseMeshes=new Stack<Mesh>();
        /// <summary>HordeCorpsesDraw: a settled corpse whose add drew on the model, drawn from its baked meshes, sunk by `sink`; true
        /// when drawn so (the voxel corpse then is not).</summary>
        private bool ZombieCorpseDrawn(NativeHordeGoreState.Remains r,float sink,float alpha)
        {
            if(!zombieCorpses.TryGetValue(r,out var c)||c.Id!=r.Id||c.At!=r.At)
            {
                if(c!=null){ZombieCorpseFree(c);zombieCorpses.Remove(r);}
                if(!zombieBodies.TryGetValue(r.Id,out var z)||!z.Dying||z.Body.Root==null)return false;
                c=ZombieCorpseBake(z,r);zombieBodies.Remove(r.Id);ZombieModelSpare(z);if(c==null)return false;zombieCorpses[r]=c;
            }
            if(alpha<=.05f)return true;
            for(int i=0;i<c.Count;i++)
            {
                var m=c.Matrix[i];m.m13-=sink;var mesh=c.Meshes[i];var mats=c.Materials[i];
                for(int sub=0;sub<mesh.subMeshCount&&sub<mats.Length;sub++)if(mats[sub]!=null)Graphics.DrawMesh(mesh,m,mats[sub],0,null,sub,c.Blocks[i],false,true);
            }
            return true;
        }
        private ZombieCorpse ZombieCorpseBake(ZombieBody z,NativeHordeGoreState.Remains r)
        {
            var all=z.Body.All;var c=new ZombieCorpse{Id=r.Id,At=r.At,Meshes=new Mesh[all.Length],Matrix=new Matrix4x4[all.Length],Materials=new Material[all.Length][],Blocks=new MaterialPropertyBlock[all.Length]};
            foreach(var rr in all)
            {
                if(!(rr is SkinnedMeshRenderer skin)||!skin.gameObject.activeInHierarchy||skin.sharedMesh==null)continue;
                var mesh=zombieCorpseMeshes.Count>0?zombieCorpseMeshes.Pop():new Mesh{name="Zombie corpse"};skin.BakeMesh(mesh,true);
                var t=skin.transform;var block=new MaterialPropertyBlock();skin.GetPropertyBlock(block);block.SetFloat(HitFlashId,1);
                c.Meshes[c.Count]=mesh;c.Matrix[c.Count]=Matrix4x4.TRS(t.position,t.rotation,Vector3.one);c.Materials[c.Count]=skin.sharedMaterials;c.Blocks[c.Count]=block;c.Count++;
            }
            return c.Count>0?c:null;
        }
        private void ZombieCorpseFree(ZombieCorpse c){if(c==null)return;for(int i=0;i<c.Count;i++)if(c.Meshes[i]!=null)zombieCorpseMeshes.Push(c.Meshes[i]);c.Count=0;}
        /// <summary>HordeCorpsesDraw, after the corpses: the baked corpses whose remains went (sunk away, or reused for another add).</summary>
        private void ZombieCorpsesKept()
        {
            if(zombieCorpses.Count==0)return;zombieCorpsesGone.Clear();
            foreach(var pair in zombieCorpses)if(!pair.Key.Live||!pair.Key.Body||pair.Key.Id!=pair.Value.Id||pair.Key.At!=pair.Value.At)zombieCorpsesGone.Add(pair.Key);
            foreach(var r in zombieCorpsesGone){ZombieCorpseFree(zombieCorpses[r]);zombieCorpses.Remove(r);}
        }
    }
}
