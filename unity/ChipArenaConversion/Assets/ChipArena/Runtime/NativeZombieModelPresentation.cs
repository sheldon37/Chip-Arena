using System;
using System.Collections.Generic;
using UnityEngine;
using NVector3=System.Numerics.Vector3;

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
    //  - Decay openings (Core NativeZombieDecay): a variant's sites cut through skin and clothes in the meshes' rest space (their
    //    fourth texture coordinates, written once per mesh; the shader's DecayClip), each onto a flesh pit on its bone with the
    //    skeleton's pieces there (Core NativeModelSkeleton) inside; planned once per variant at the bake's rest.
    //  - A spitter wears its throat sac (Core NativeZombieSac) on neck_01 and the head, swelling and glowing through a spit's
    //    wind-up with the gait's own swell (NativeHordeFx.Swell), as the voxel sac does.
    //  - Severed, sliced and gibbed parts collapse on the model at their joint and fly off as the model's own: each renderer
    //    baked once at the launch, the triangles skinned mostly to that joint's bones kept (cached per mesh and joint), drawn
    //    with the zombie's own materials and looks (its tears, blood and openings); the gore's raw cap and blood stay. A settled corpse is baked once into static meshes and drawn from them,
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
            // Whether it crawls (its legs gone: the model's pelvis at the gait's height, NativeZombiePosture.Stance) and how far its
            // gait sank its hips this pose (NativeHordeGait.Sunk).
            public bool Crawl;public float Sunk;
            // The spitter's sac and its swell as last written.
            public SkinnedMeshRenderer Sac;public float Swell=-1;
        }
        private readonly Dictionary<int,ZombieBody> zombieBodies=new Dictionary<int,ZombieBody>();private readonly CapturedRig.Pose zombieFitPose=new CapturedRig.Pose();private readonly List<int> zombieBodiesGone=new List<int>();
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
        private bool ZombieModelPose(CapturedAnimation.Player rig,int id,int kind,int tier,bool planted,bool airborne,bool crawl,float sunk,int lost,int hidden,float flash,float glow,float swell)
        {
            if(rig==null||!ZombieModelled(kind,tier))return false;var z=ZombieModelOf(id,kind);if(z==null)return false;
            z.Dying=false;z.Crawl=crawl;z.Sunk=sunk;ZombieModelBind(z,rig,planted,airborne,lost,hidden,flash,glow,swell);return true;
        }
        /// <summary>A dying add's fall (HordeKindDeath, its rig posed from minionPose): its model, true; false: the voxel body falls.</summary>
        private bool ZombieModelDeath(CapturedAnimation.Player rig,int id,int kind,int lost,int hidden)
        {
            if(rig==null||!zombieBodies.TryGetValue(id,out var z)||z.Body.Root==null||z.Kind!=kind||!ZombieModelsOn)return false;
            z.Dying=true;z.Sunk=0;ZombieModelBind(z,rig,false,false,lost,hidden,1,0,0);return true;
        }
        private void ZombieModelBind(ZombieBody z,CapturedAnimation.Player rig,bool planted,bool airborne,int lost,int hidden,float flash,float glow,float swell)
        {
            var root=z.Body.Root;var t=root.transform;var rt=rig.Root.transform;
            // The floor point under the rig root, as a pilot's body (NativeVisualRig.Floor), at the rig's scale (the Hulk's).
            t.SetPositionAndRotation(rt.TransformPoint(0,NativeVisualRig.Floor,0),rt.rotation);t.localScale=rt.lossyScale;
            if(!root.activeSelf)root.SetActive(true);
            if(z.PosedFrame<Time.frameCount-1)z.Body.Binding.State.Forget();
            // The gait's pose fitted for the model's body (Core/NativeZombiePosture: the hunch up the back, the arms from their
            // shoulders, the hips at the game's distance and standing on the model's legs); the rig keeps the gait's own.
            minionPose.World();var pose=zombieFitPose;NativeZombiePosture.Fit(minionPose,pose);NativeZombiePosture.Stance(z.Body.Binding.State,z.Kind,z.Crawl,z.Sunk);
            z.Body.Binding.Apply(pose,default(NativeVisualRig.Grasp),planted,airborne);
            ZombieCollapse(z,lost,hidden);ZombieFlash(z,flash,glow);ZombieSacSwell(z,swell);
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
        // The spitter's sac swollen by `swell` (NativeHordeFx.Swell: up through a spit's wind-up, released at the spit), its glow
        // with it; written when it changes.
        private static void ZombieSacSwell(ZombieBody z,float swell)
        {
            if(z.Sac==null)return;swell=Mathf.Clamp01(swell);if(Mathf.Abs(swell-z.Swell)<.01f)return;z.Swell=swell;
            z.Sac.SetBlendShapeWeight(0,swell*100);var b=z.Body.Block;z.Sac.GetPropertyBlock(b);b.SetFloat(VertexAlbedoId,NativeZombieSac.Glow(swell));z.Sac.SetPropertyBlock(b);
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
            var sac=v.Sac?ZombieSacMake(root,skeleton,v):null;if(sac!=null){var all=new Renderer[body.All.Length+1];body.All.CopyTo(all,0);all[all.Length-1]=sac;body.All=all;}
            if(v.Decay!=null&&v.Decay.Length>0)body.All=ZombieDecayMake(root,skeleton,v,body.All);
            foreach(var r in body.All){r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;if(r is SkinnedMeshRenderer s)s.updateWhenOffscreen=false;}
            var z=new ZombieBody{Body=body,Variant=v,Sac=sac};
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
            if(arena==null||arena.Pve==null&&arena.Minions.Count==0&&!ZombieSummoner(arena)||!ZombieModelsOn||zombiePool.Count>=ZombiePoolTarget)return;
            if(zombieHolder==null){zombieHolder=new GameObject("Zombie model pool");zombieHolder.SetActive(false);DontDestroyOnLoad(zombieHolder);}
            zombieFill=InstantiateAsync(visualBodyPrefab,ZombiePoolBatch,zombieHolder.transform);
        }
        // A pilot who can summon zombies (Graveyard Shift, Corpse Party): bodies are made ahead for them too.
        private static bool ZombieSummoner(NativeArena arena)
        {
            foreach(var p in arena.Pilots){var chips=p.Seat?.chips;if(chips!=null&&(chips.TryGetValue("graveyard-shift",out int g)&&g>0||chips.TryGetValue("corpse-party",out int c)&&c>0))return true;}
            return false;
        }
        // A body no add wears any more: back to its variant's spares (hidden, whole), or taken apart past the spares kept.
        private void ZombieModelSpare(ZombieBody z)
        {
            if(z?.Body?.Root==null)return;
            if(!zombieSpares.TryGetValue(z.Variant,out var spares)){spares=new Stack<ZombieBody>();zombieSpares[z.Variant]=spares;}
            if(spares.Count>=ZombieSparesPerVariant){VisualBodyGone(z.Body.Root);z.Body.Root=null;return;}
            z.Body.Root.SetActive(false);ZombieCollapse(z,0,0);ZombieFlash(z,1,0);ZombieSacSwell(z,0);z.Id=0;z.Dying=false;spares.Push(z);
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
                if(VisualSkinPart(r)){VisualSkinToneOne(r,v.Skin);ZombieWearOn(r,0,0,v.Rot,seed,skinColour,0);continue;}
                if(ZombieEyePart(r)){var e=WearColor(v.Eyes);if(linear)e=e.linear;zombieBlock??=new MaterialPropertyBlock();r.GetPropertyBlock(zombieBlock);zombieBlock.SetVector(ZombieEyeId,new Vector4(e.r,e.g,e.b,1.35f));zombieBlock.SetFloat(UseOutlineId,0);r.SetPropertyBlock(zombieBlock);continue;}
                int slot=ZombieSlotOf(worn,r.name);if(slot<0)continue;
                if(v.Colours[slot]>=0)ZombieTintOn(r,v.Colours[slot],linear);
                if(slot>0)ZombieWearOn(r,v.Grime,v.Tears,0,seed,skinColour,v.Blood);
            }
            lookNamed.Clear();return worn;
        }
        private static int ZombieSlotOf(LookPiece[] worn,string renderer)
        {
            for(int s=0;s<worn.Length;s++)if(worn[s]?.renderers!=null)foreach(var n in worn[s].renderers)if(n==renderer)return s;
            return renderer.StartsWith("HairFront.",StringComparison.Ordinal)||renderer.StartsWith("HairBack.",StringComparison.Ordinal)?0:-1;
        }
        // The blank eyes: the eye mesh (the bake's Eyes piece; its iris and pupil are in its texture), drawn without its outline (a
        // map's tinted line round a blank eye reads as an iris: Brickfall's red).
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
        private static void ZombieWearOn(Renderer r,float grime,float tears,float rot,float seed,Color skin,float blood)
        {
            if(grime+tears+rot+blood<=0)return;zombieBlock??=new MaterialPropertyBlock();r.GetPropertyBlock(zombieBlock);
            zombieBlock.SetVector(ZombieWearId,new Vector4(grime,tears,rot,seed));zombieBlock.SetVector(ZombieSkinId,new Vector4(skin.r,skin.g,skin.b,blood));r.SetPropertyBlock(zombieBlock);
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
        // The spitter's throat sac (Core NativeZombieSac) on this body's neck_01 and head at their rest (after its build: the mesh,
        // kept per variant, is bound to that rest), drawn in the toon by its vertex colours with the outline; null without the
        // bones or the toon.
        private static readonly Dictionary<NativeZombieLooks.Variant,Mesh> zombieSacMeshes=new Dictionary<NativeZombieLooks.Variant,Mesh>();private static Material zombieSacMaterial;
        private static SkinnedMeshRenderer ZombieSacMake(GameObject root,Transform skeleton,NativeZombieLooks.Variant v)
        {
            Transform neck=null,head=null;foreach(var t in skeleton.GetComponentsInChildren<Transform>(true)){if(t.name=="neck_01")neck=t;else if(t.name=="head")head=t;}
            if(neck==null||head==null)return null;
            if(zombieSacMaterial==null)
            {
                Material template=null;foreach(var r in root.GetComponentsInChildren<Renderer>(true))if(r.sharedMaterial!=null&&r.sharedMaterial.HasProperty(VertexAlbedoId)){template=r.sharedMaterial;break;}
                if(template==null)return null;
                var m=new Material(template){name="Zombie sac",hideFlags=HideFlags.HideAndDontSave};m.SetTexture(TextureId,Texture2D.whiteTexture);
                m.SetFloat(VertexAlbedoId,NativeZombieSac.GlowRest);m.SetFloat(UseCustomTextureId,0);m.SetFloat(UseOutlineId,1);m.SetFloat("_Cull",2);m.SetFloat(CutoffId,0);m.SetFloat(AccentGlowId,0);
                zombieSacMaterial=m;
            }
            var toRoot=root.transform.worldToLocalMatrix;
            if(!zombieSacMeshes.TryGetValue(v,out var mesh))
            {
                Vector3 n=toRoot.MultiplyPoint3x4(neck.position),h=toRoot.MultiplyPoint3x4(head.position);
                var sac=NativeZombieSac.Build(new NVector3(n.x,n.y,n.z),new NVector3(h.x,h.y,h.z),new NVector3(0,0,1),v.Skin);if(sac==null)return null;
                int count=sac.Positions.Count;bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
                var vs=new Vector3[count];var ns=new Vector3[count];var ds=new Vector3[count];var cs=new Color[count];var ws=new BoneWeight[count];
                for(int i=0;i<count;i++)
                {
                    var p=sac.Positions[i];var q=sac.Normals[i];var d=sac.Swell[i];var c=sac.Colors[i];
                    vs[i]=new Vector3(p.X,p.Y,p.Z);ns[i]=new Vector3(q.X,q.Y,q.Z);ds[i]=new Vector3(d.X,d.Y,d.Z);
                    var col=new Color(c.X,c.Y,c.Z,1);cs[i]=linear?col.linear:col;cs[i].a=1;ws[i]=new BoneWeight{boneIndex0=0,weight0=1-sac.Head[i],boneIndex1=1,weight1=sac.Head[i]};
                }
                mesh=new Mesh{name="Zombie sac "+v.Id,hideFlags=HideFlags.HideAndDontSave};mesh.vertices=vs;mesh.normals=ns;mesh.colors=cs;mesh.boneWeights=ws;
                mesh.bindposes=new[]{neck.worldToLocalMatrix*root.transform.localToWorldMatrix,head.worldToLocalMatrix*root.transform.localToWorldMatrix};
                mesh.SetTriangles(sac.Indices,0);mesh.AddBlendShapeFrame("Swell",100,ds,null,null);mesh.RecalculateBounds();zombieSacMeshes[v]=mesh;
            }
            var go=new GameObject("Sac.Spitter");go.transform.SetParent(root.transform,false);
            var renderer=go.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.bones=new[]{neck,head};renderer.sharedMaterial=zombieSacMaterial;
            var bounds=mesh.bounds;bounds.Expand(1.2f);renderer.localBounds=bounds;renderer.skinnedMotionVectors=false;
            return renderer;
        }
        // ---- flying parts from the model ----
        private sealed class ZombiePiece{public Mesh Mesh;public Matrix4x4 Matrix;public Material[] Materials;public readonly MaterialPropertyBlock Block=new MaterialPropertyBlock();public bool Own;}
        private sealed class ZombieCut{public int[] Map;public int[][] Tris;}
        private static readonly Dictionary<(Mesh,int),ZombieCut> zombieCuts=new Dictionary<(Mesh,int),ZombieCut>();
        private static readonly Dictionary<Mesh,(Vector2[] uv,Color[] col,List<Vector3> rest)> zombieMeshData=new Dictionary<Mesh,(Vector2[],Color[],List<Vector3>)>();
        private readonly Stack<ZombiePiece> zombiePieces=new Stack<ZombiePiece>();private readonly Stack<Mesh> zombieBakes=new Stack<Mesh>();
        private readonly Dictionary<SkinnedMeshRenderer,Mesh> zombieBakedNow=new Dictionary<SkinnedMeshRenderer,Mesh>();private int zombieBakedFrame=-1;
        private readonly List<Vector3> zombieV=new List<Vector3>(),zombieN=new List<Vector3>(),zombieV2=new List<Vector3>(),zombieN2=new List<Vector3>(),zombieR2=new List<Vector3>();
        private readonly List<Vector2> zombieUv2=new List<Vector2>();private readonly List<Color> zombieC2=new List<Color>();
        /// <summary>HordeLaunch: the part from joint `joint` down of add `id`'s model as it stands (its renderers baked once this frame),
        /// or the voxel part (null) when the add draws no model; `reuse` (the slot's old pieces) given back first.</summary>
        private List<ZombiePiece> ZombiePartPieces(int id,int joint,List<ZombiePiece> reuse)
        {
            ZombiePiecesFree(reuse);
            if(!ZombieModelsOn||!zombieBodies.TryGetValue(id,out var z)||z.Body.Root==null||!z.Body.Root.activeInHierarchy||joint<=0||joint>=z.Joint.Length||z.Joint[joint]==null)return reuse;
            if(zombieBakedFrame!=Time.frameCount){foreach(var m in zombieBakedNow.Values)zombieBakes.Push(m);zombieBakedNow.Clear();zombieBakedFrame=Time.frameCount;}
            var under=z.Joint[joint];var list=reuse??new List<ZombiePiece>();
            foreach(var r in z.Body.All)
            {
                if(r==null||!r.gameObject.activeInHierarchy)continue;
                // A decay pit on the part: its own mesh where it lies.
                if(r is MeshRenderer pit){if(!pit.transform.IsChildOf(under)||!pit.TryGetComponent<MeshFilter>(out var pf)||pf.sharedMesh==null)continue;var own=ZombiePieceNew(false);own.Mesh=pf.sharedMesh;own.Matrix=pit.transform.localToWorldMatrix;own.Materials=pit.sharedMaterials;own.Block.Clear();list.Add(own);continue;}
                if(!(r is SkinnedMeshRenderer skin)||skin.sharedMesh==null||!skin.sharedMesh.isReadable)continue;
                var cut=ZombieCutOf(skin,joint,under);if(cut==null)continue;
                if(!zombieBakedNow.TryGetValue(skin,out var baked)){baked=zombieBakes.Count>0?zombieBakes.Pop():new Mesh{name="Zombie part bake",hideFlags=HideFlags.HideAndDontSave};skin.BakeMesh(baked,true);zombieBakedNow[skin]=baked;}
                var piece=ZombiePieceNew(true);ZombieCutMesh(piece.Mesh,baked,skin.sharedMesh,cut);
                var t=skin.transform;piece.Matrix=Matrix4x4.TRS(t.position,t.rotation,Vector3.one);piece.Materials=skin.sharedMaterials;skin.GetPropertyBlock(piece.Block);piece.Block.SetFloat(HitFlashId,1);
                list.Add(piece);
            }
            return list;
        }
        private ZombiePiece ZombiePieceNew(bool own)
        {
            ZombiePiece p=null;while(zombiePieces.Count>0&&p==null){var q=zombiePieces.Pop();if(q.Own==own)p=q;else if(q.Own&&q.Mesh!=null)Destroy(q.Mesh);}
            p??=new ZombiePiece{Own=own};if(own&&p.Mesh==null)p.Mesh=new Mesh{name="Zombie part",hideFlags=HideFlags.HideAndDontSave};return p;
        }
        private void ZombiePiecesFree(List<ZombiePiece> pieces){if(pieces==null)return;foreach(var p in pieces){p.Materials=null;if(!p.Own)p.Mesh=null;zombiePieces.Push(p);}pieces.Clear();}
        /// <summary>HordePartsDraw: a part cut from the model, turned and sunk as the voxel part would be; false for a voxel part.</summary>
        private bool ZombiePartDrawn(HordePart p,float age,float sink)
        {
            if(p.Pieces==null||p.Pieces.Count==0)return false;var turn=HordePartTurn(p,age,out _);
            foreach(var piece in p.Pieces)
            {
                var m=turn*piece.Matrix;m.m13-=sink;var mats=piece.Materials;if(mats==null||piece.Mesh==null)continue;
                for(int sub=0;sub<piece.Mesh.subMeshCount&&sub<mats.Length;sub++)if(mats[sub]!=null)Graphics.DrawMesh(piece.Mesh,m,mats[sub],0,null,sub,piece.Block,false,true);
            }
            return true;
        }
        // A mesh's triangles on the part from `under` down (two of their three vertices weighted most to its bones), per sub-mesh,
        // with the vertices they use; once per mesh and joint (every body of the bake shares them); null when none.
        private static ZombieCut ZombieCutOf(SkinnedMeshRenderer skin,int joint,Transform under)
        {
            var mesh=skin.sharedMesh;var key=(mesh,joint);if(zombieCuts.TryGetValue(key,out var cut))return cut;
            var bones=skin.bones;var inPart=new bool[bones.Length];bool any=false;
            for(int b=0;b<bones.Length;b++){inPart[b]=bones[b]!=null&&bones[b].IsChildOf(under);any|=inPart[b];}
            var weights=mesh.boneWeights;if(!any||weights.Length!=mesh.vertexCount){zombieCuts[key]=null;return null;}
            var mine=new bool[mesh.vertexCount];
            for(int i=0;i<mine.Length;i++)
            {
                var w=weights[i];int top=w.boneIndex0;float most=w.weight0;
                if(w.weight1>most){top=w.boneIndex1;most=w.weight1;}if(w.weight2>most){top=w.boneIndex2;most=w.weight2;}if(w.weight3>most)top=w.boneIndex3;
                mine[i]=top>=0&&top<inPart.Length&&inPart[top];
            }
            var map=new Dictionary<int,int>();var tris=new int[mesh.subMeshCount][];
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                var t=mesh.GetTriangles(sub);var kept=new List<int>();
                for(int i=0;i+2<t.Length;i+=3){if((mine[t[i]]?1:0)+(mine[t[i+1]]?1:0)+(mine[t[i+2]]?1:0)<2)continue;for(int k=0;k<3;k++){if(!map.TryGetValue(t[i+k],out int m)){m=map.Count;map[t[i+k]]=m;}kept.Add(m);}}
                tris[sub]=kept.ToArray();
            }
            if(map.Count==0){zombieCuts[key]=null;return null;}
            var back=new int[map.Count];foreach(var pair in map)back[pair.Value]=pair.Key;
            cut=new ZombieCut{Map=back,Tris=tris};zombieCuts[key]=cut;return cut;
        }
        // The part's mesh from the baked pose: its vertices and normals, the source's texture coordinates, colours and rest (the
        // decay openings stay cut on it).
        private void ZombieCutMesh(Mesh into,Mesh baked,Mesh source,ZombieCut cut)
        {
            if(!zombieMeshData.TryGetValue(source,out var data))
            {
                var rest=new List<Vector3>();if(source.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord3))source.GetUVs(3,rest);
                data=(source.uv,source.colors,rest);zombieMeshData[source]=data;
            }
            baked.GetVertices(zombieV);baked.GetNormals(zombieN);zombieV2.Clear();zombieN2.Clear();zombieUv2.Clear();zombieC2.Clear();zombieR2.Clear();
            foreach(int i in cut.Map)
            {
                zombieV2.Add(zombieV[i]);zombieN2.Add(i<zombieN.Count?zombieN[i]:Vector3.up);zombieUv2.Add(i<data.uv.Length?data.uv[i]:Vector2.zero);
                zombieC2.Add(i<data.col.Length?data.col[i]:Color.white);zombieR2.Add(i<data.rest.Count?data.rest[i]:Vector3.zero);
            }
            into.Clear();into.SetVertices(zombieV2);into.SetNormals(zombieN2);into.SetUVs(0,zombieUv2);if(data.col.Length>0)into.SetColors(zombieC2);if(data.rest.Count>0)into.SetUVs(3,zombieR2);
            into.subMeshCount=cut.Tris.Length;for(int s=0;s<cut.Tris.Length;s++)into.SetTriangles(cut.Tris[s],s,false);into.RecalculateBounds();
        }

        // ---- decay openings (Core NativeZombieDecay) ----
        private sealed class ZombieDecayPlan
        {
            public readonly Vector4[] At=new Vector4[NativeZombieDecay.Max],In=new Vector4[NativeZombieDecay.Max];public int Count;
            public readonly NativeZombieDecay.Site[] Sites=new NativeZombieDecay.Site[NativeZombieDecay.Max];public Mesh Bones;
        }
        private static readonly Dictionary<NativeZombieLooks.Variant,ZombieDecayPlan> zombieDecayPlans=new Dictionary<NativeZombieLooks.Variant,ZombieDecayPlan>();
        private static readonly Dictionary<Mesh,Matrix4x4> zombieRestMeshes=new Dictionary<Mesh,Matrix4x4>();
        private static readonly int DecaysId=Shader.PropertyToID("_Decays"),DecayAtId=Shader.PropertyToID("_DecayAt"),DecayInId=Shader.PropertyToID("_DecayIn");
        private static bool ZombieDecayCut(Renderer r)=>r!=null&&!ZombieEyePart(r)&&!r.name.StartsWith("Teeth.",StringComparison.Ordinal)&&r.name!="Sac.Spitter";
        /// <summary>A body's decay openings: its worn meshes' rest in their fourth texture coordinates (once per mesh), the openings on
        /// every renderer that is cut, a flesh pit on each opening's bone and the skeleton's pieces there; the renderers with the
        /// pits and the bones added.</summary>
        private Renderer[] ZombieDecayMake(GameObject root,Transform skeleton,NativeZombieLooks.Variant v,Renderer[] all)
        {
            var toRoot=root.transform.worldToLocalMatrix;
            foreach(var r in all)
            {
                if(!(r is SkinnedMeshRenderer s)||s.sharedMesh==null||!s.sharedMesh.isReadable||zombieRestMeshes.ContainsKey(s.sharedMesh))continue;
                var rel=toRoot*s.transform.localToWorldMatrix;var vs=s.sharedMesh.vertices;var rest=new List<Vector3>(vs.Length);foreach(var p in vs)rest.Add(rel.MultiplyPoint3x4(p));
                s.sharedMesh.SetUVs(3,rest);zombieRestMeshes[s.sharedMesh]=rel;
            }
            if(!zombieDecayPlans.TryGetValue(v,out var plan)){plan=ZombieDecayPlanOf(v,all);zombieDecayPlans[v]=plan;}
            if(plan==null||plan.Count==0)return all;
            zombieBlock??=new MaterialPropertyBlock();
            foreach(var r in all){if(!ZombieDecayCut(r))continue;r.GetPropertyBlock(zombieBlock);zombieBlock.SetFloat(DecaysId,plan.Count);zombieBlock.SetVectorArray(DecayAtId,plan.At);zombieBlock.SetVectorArray(DecayInId,plan.In);r.SetPropertyBlock(zombieBlock);}
            var named=new Dictionary<string,Transform>();foreach(var t in skeleton.GetComponentsInChildren<Transform>(true))if(!named.ContainsKey(t.name))named[t.name]=t;
            var prefabNamed=new Dictionary<string,Transform>();var prefabSkel=visualBodyPrefab.transform.Find("VisualSkeleton");
            if(prefabSkel!=null)foreach(var t in prefabSkel.GetComponentsInChildren<Transform>(true))if(!prefabNamed.ContainsKey(t.name))prefabNamed[t.name]=t;
            var added=new List<Renderer>(all);var prefabRoot=visualBodyPrefab.transform.localToWorldMatrix;
            for(int k=0;k<plan.Count;k++)
            {
                var site=plan.Sites[k];if(!named.TryGetValue(site.Bone,out var bone)||!prefabNamed.TryGetValue(site.Bone,out var restBone))continue;
                Vector3 at=plan.At[k],inward=plan.In[k];
                // A limb's pit rides its twist leaf nearest the opening (so it takes the limb's girth); the rest their bone.
                if(site.Limb)
                {
                    float best=float.MaxValue;
                    foreach(Transform c in restBone){if(c.childCount!=0||c.name.IndexOf("twist",StringComparison.OrdinalIgnoreCase)<0||!named.TryGetValue(c.name,out var tw))continue;float d=(visualBodyPrefab.transform.InverseTransformPoint(c.position)-at).sqrMagnitude;if(d<best){best=d;bone=tw;restBone=c;}}
                }
                var m=restBone.worldToLocalMatrix*prefabRoot;var dir=m.MultiplyVector(inward).normalized;var side=Vector3.Cross(dir,Mathf.Abs(dir.y)<.9f?Vector3.up:Vector3.right).normalized;
                var h=new VisualHole{Bone=-1,At=m.MultiplyPoint3x4(at),In=dir,Along=side,Radius=plan.At[k].w*1.04f,Half=0,Depth=plan.In[k].w,Rim=.003f};
                var pit=VisualHolePit(all,bone,h);if(pit!=null&&pit.TryGetComponent<MeshRenderer>(out var pr)){pit.name="Decay pit";added.Add(pr);}
            }
            // The skeleton's pieces in the openings, rigid on their bones as the corpses' skeleton is.
            if(plan.Bones!=null&&skeletonBones!=null)
            {
                var bones=new Transform[skeletonBones.Length];bool ok=true;for(int b=0;b<bones.Length;b++)if(!named.TryGetValue(skeletonBones[b],out bones[b])){ok=false;break;}
                if(ok)
                {
                    var go=new GameObject("Bones.Decay");go.transform.SetParent(root.transform,false);var sr=go.AddComponent<SkinnedMeshRenderer>();
                    sr.sharedMesh=plan.Bones;sr.bones=bones;sr.sharedMaterial=skeletonMaterial;var bb=plan.Bones.bounds;bb.Expand(1.2f);sr.localBounds=bb;
                    sr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;sr.skinnedMotionVectors=false;added.Add(sr);
                }
            }
            return added.ToArray();
        }
        // A variant's openings at the bake's rest: each site from the rest joints out onto the outermost worn surface (hair, eyes, teeth
        // and the sac left out), within 25 cm; and the skeleton's triangles within reach of an opening.
        private static ZombieDecayPlan ZombieDecayPlanOf(NativeZombieLooks.Variant v,Renderer[] all)
        {
            var prefab=visualBodyPrefab.transform;var skel=prefab.Find("VisualSkeleton");if(skel==null)return null;
            var at=new Dictionary<string,Transform>();foreach(var t in skel.GetComponentsInChildren<Transform>(true))if(!at.ContainsKey(t.name))at[t.name]=t;
            var toRoot=prefab.worldToLocalMatrix;
            NVector3? Rest(string n){if(!at.TryGetValue(n,out var t))return null;var p=toRoot.MultiplyPoint3x4(t.position);return new NVector3(p.x,p.y,p.z);}
            var meshes=new List<(Vector3[] p,int[] tri)>();
            foreach(var r in all)
            {
                if(!(r is SkinnedMeshRenderer s)||s.sharedMesh==null||!s.gameObject.activeSelf||!ZombieDecayCut(r)||r.name.StartsWith("Hair",StringComparison.Ordinal)||!zombieRestMeshes.TryGetValue(s.sharedMesh,out var rel))continue;
                var vs=s.sharedMesh.vertices;for(int i=0;i<vs.Length;i++)vs[i]=rel.MultiplyPoint3x4(vs[i]);meshes.Add((vs,s.sharedMesh.triangles));
            }
            var plan=new ZombieDecayPlan();
            foreach(var name in v.Decay)
            {
                if(plan.Count>=NativeZombieDecay.Max)break;var site=NativeZombieDecay.Find(name);
                if(!NativeZombieDecay.Place(site,Rest,out var p0,out var d0))continue;
                var p=new Vector3(p0.X,p0.Y,p0.Z);var d=new Vector3(d0.X,d0.Y,d0.Z);float far=-1;
                foreach(var (vs,tri) in meshes)for(int i=0;i+2<tri.Length;i+=3)if(ZombieRayTri(p,d,vs[tri[i]],vs[tri[i+1]],vs[tri[i+2]],out float t)&&t<.25f&&t>far)far=t;
                if(far<=0)continue;var on=p+d*far;
                plan.At[plan.Count]=new Vector4(on.x,on.y,on.z,site.Radius);plan.In[plan.Count]=new Vector4(-d.x,-d.y,-d.z,NativeZombieDecay.DepthOf(site,far));plan.Sites[plan.Count]=site;plan.Count++;
            }
            if(plan.Count>0&&VisualSkeletonAssets())plan.Bones=ZombieDecayBones(plan,v.Id);
            // Once per variant: where its openings went, for fitting them on the owner's PC.
            var line=new System.Text.StringBuilder("Zombie decay: "+v.Id+" "+plan.Count+" of "+v.Decay.Length+" openings");
            for(int k=0;k<plan.Count;k++)line.Append("; ").Append(plan.Sites[k].Name).Append(" at ").Append(((Vector3)plan.At[k]).ToString("F3")).Append(" depth ").Append(plan.In[k].w.ToString("F3"));
            line.Append(plan.Bones!=null?"; "+plan.Bones.vertexCount+" bone vertices":"; no bones");
            Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"{0}",line);
            return plan;
        }
        // The skeleton mesh's triangles whose middles lie within an opening's reach (its radius and 2 cm round its axis, from just
        // outside the surface to 3 cm past its depth), as their own mesh on the same bones and bind poses.
        private static Mesh ZombieDecayBones(ZombieDecayPlan plan,string id)
        {
            var vs=skeletonMesh.vertices;var tri=skeletonMesh.triangles;var keep=new List<int>();var map=new Dictionary<int,int>();
            for(int i=0;i+2<tri.Length;i+=3)
            {
                var c=(vs[tri[i]]+vs[tri[i+1]]+vs[tri[i+2]])/3;bool near=false;
                for(int k=0;k<plan.Count&&!near;k++){Vector3 a=plan.At[k],n=plan.In[k];var w=c-a;float t=Vector3.Dot(w,n);if(t<-.01f||t>plan.In[k].w+.03f)continue;var r=w-n*t;near=r.sqrMagnitude<(plan.At[k].w+.02f)*(plan.At[k].w+.02f);}
                if(near)for(int j=0;j<3;j++){if(!map.TryGetValue(tri[i+j],out int m)){m=map.Count;map[tri[i+j]]=m;}keep.Add(m);}
            }
            if(keep.Count==0)return null;
            var ns=skeletonMesh.normals;var cs=skeletonMesh.colors;var ws=skeletonMesh.boneWeights;int n2=map.Count;
            var v2=new Vector3[n2];var nm2=new Vector3[n2];var c2=new Color[n2];var w2=new BoneWeight[n2];
            foreach(var pair in map){v2[pair.Value]=vs[pair.Key];nm2[pair.Value]=ns[pair.Key];c2[pair.Value]=cs[pair.Key];w2[pair.Value]=ws[pair.Key];}
            var mesh=new Mesh{name="Zombie decay bones "+id,hideFlags=HideFlags.HideAndDontSave};mesh.vertices=v2;mesh.normals=nm2;mesh.colors=c2;mesh.boneWeights=w2;mesh.bindposes=skeletonMesh.bindposes;mesh.SetTriangles(keep,0);mesh.RecalculateBounds();
            return mesh;
        }
        // A ray (from `o` along unit `d`) against a triangle, either face (Moller-Trumbore): its distance.
        private static bool ZombieRayTri(Vector3 o,Vector3 d,Vector3 a,Vector3 b,Vector3 c,out float t)
        {
            t=0;var e1=b-a;var e2=c-a;var p=Vector3.Cross(d,e2);float det=Vector3.Dot(e1,p);if(Mathf.Abs(det)<1e-9f)return false;float inv=1/det;
            var s=o-a;float u=Vector3.Dot(s,p)*inv;if(u<0||u>1)return false;var q=Vector3.Cross(s,e1);float w=Vector3.Dot(d,q)*inv;if(w<0||u+w>1)return false;
            t=Vector3.Dot(e2,q)*inv;return t>0;
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
        private sealed class ZombieCorpse{public int Id;public float At;public Mesh[] Meshes;public Matrix4x4[] Matrix;public Material[][] Materials;public MaterialPropertyBlock[] Blocks;public bool[] Baked;public int Count;}
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
            var all=z.Body.All;var c=new ZombieCorpse{Id=r.Id,At=r.At,Meshes=new Mesh[all.Length],Matrix=new Matrix4x4[all.Length],Materials=new Material[all.Length][],Blocks=new MaterialPropertyBlock[all.Length],Baked=new bool[all.Length]};
            foreach(var rr in all)
            {
                // A decay pit: its own mesh where it lies now (its bone's), with its material.
                if(rr is MeshRenderer pit&&pit.gameObject.activeInHierarchy&&pit.TryGetComponent<MeshFilter>(out var pf)&&pf.sharedMesh!=null)
                {
                    c.Meshes[c.Count]=pf.sharedMesh;c.Matrix[c.Count]=pit.transform.localToWorldMatrix;c.Materials[c.Count]=pit.sharedMaterials;c.Blocks[c.Count]=new MaterialPropertyBlock();c.Baked[c.Count]=false;c.Count++;continue;
                }
                if(!(rr is SkinnedMeshRenderer skin)||!skin.gameObject.activeInHierarchy||skin.sharedMesh==null)continue;
                var mesh=zombieCorpseMeshes.Count>0?zombieCorpseMeshes.Pop():new Mesh{name="Zombie corpse"};skin.BakeMesh(mesh,true);
                var t=skin.transform;var block=new MaterialPropertyBlock();skin.GetPropertyBlock(block);block.SetFloat(HitFlashId,1);
                c.Meshes[c.Count]=mesh;c.Matrix[c.Count]=Matrix4x4.TRS(t.position,t.rotation,Vector3.one);c.Materials[c.Count]=skin.sharedMaterials;c.Blocks[c.Count]=block;c.Baked[c.Count]=true;c.Count++;
            }
            return c.Count>0?c:null;
        }
        private void ZombieCorpseFree(ZombieCorpse c){if(c==null)return;for(int i=0;i<c.Count;i++)if(c.Meshes[i]!=null&&c.Baked[i])zombieCorpseMeshes.Push(c.Meshes[i]);c.Count=0;}
        /// <summary>HordeCorpsesDraw, after the corpses: the baked corpses whose remains went (sunk away, or reused for another add).</summary>
        private void ZombieCorpsesKept()
        {
            if(zombieCorpses.Count==0)return;zombieCorpsesGone.Clear();
            foreach(var pair in zombieCorpses)if(!pair.Key.Live||!pair.Key.Body||pair.Key.Id!=pair.Value.Id||pair.Key.At!=pair.Value.At)zombieCorpsesGone.Add(pair.Key);
            foreach(var r in zombieCorpsesGone){ZombieCorpseFree(zombieCorpses[r]);zombieCorpses.Remove(r);}
        }
    }
}
