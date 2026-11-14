using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The horde adds' Siren effects (Core/NativeMinionFx.cs), drawn only where an add itself is drawn (SourceWorld for living and
    // entering adds, SourceMinionDeath for the dying), from what host, guests and the killcam replay all hold (the add's id, its
    // vent entry, whether it is the possessed add and since when, its death record) and the presentation clock (FieldClock).
    // Owner 2026-10-19 ("I want them to be more "standard" zombies, that produce gore when shot / gibbed, not just black flames"):
    // the horde is standard zombies now, so the Siren's minion treatment is gone from every kind: no black-flame tongues, no shed
    // licks, no violet crack haze, no glow-cell flicker on the Siren's seven (NativeHordeKindModels), no char and burn-away death
    // or its black flames, ash and scorch (the corpse and the gore are Runtime/NativeHordeGorePresentation.cs), and a vent climb
    // kicks up dust and grit instead of the black-flame pool. What stays is the Siren boss's own mechanic on her possessed add:
    //  - The possession hint by its level (Core/NativeMinionHint.cs, from the replicated PossessedAt): the crown seam and halo
    //    pulse, from level 2 the Siren's grin floating over the head within 25 m and the black-flame trail dripped into the world
    //    flame pool while the add walks, at level 3 all brighter. The grin is hints.ts's canvas baked once into a texture
    //    (NativeMinionHint.Glyph, off the main thread from the first PvE frame or horde add) and drawn as one quad at its opacity
    //    after the effect layer (Resources/NativeMinionGrin.shader). The trail emits in SourceWorld, before the pool ticks.
    //  - The zone kinds' glow cells (their heat and lights) still flicker on _MinionClock with the add's phase and drop bits
    //    (HordeGlow); the Siren's seven's materials ignore them.
    // Presentation only: no hit, collider, timing or network state changes. Allocation free in steady state.
    public sealed partial class ArenaPresentation
    {
        private struct HordeFx{public CapturedAnimation.Player Rig;public int Id,Variant,Lost,Look;public uint Seed;public float Time,Emerge,Death,Floor,Ground,Phase,PossessedAt;public Vector3 Velocity;public bool Possessed,Body;}
        private HordeFx[] hordeFx=new HordeFx[64];private int hordeFxCount;private float hordeClock;
        private static readonly int MinionClockId=Shader.PropertyToID("_MinionClock");
        private static readonly int[] HordeLimbBits={1,2,4,8,16,32,64,128,256},HordeLimbBones={5,7,11,8,12,14,18,15,19};
        // The dying body's joint matrices and (the Cinderling's ember crumble only) its burn-away and char, read by Queue while it
        // runs; no per-frame closures.
        private CapturedAnimation.Player hordeDeathRig;private uint hordeDeathSeed;private float hordeDeathAge;private bool hordeDeathBurn;private Func<int,Matrix4x4> hordeDeathBone;private Func<int,Color> hordeDeathTint;
        private static readonly Color VentDustTint=NativeVfxDraw.Linear(NativeZombieFx.DustHex)*.75f;
        private static readonly Color HordeViolet=NativeArena.ColorOf(NativeMinionFx.Violet);
        // The possessed add's trail: where and when it was last drawn, and the per-slot random stream (one delegate, made once).
        private int hordeTrailId=-1;private float hordeTrailClock;private Vector3 hordeTrailAt;private readonly NativeMinionHint.SlotRandom hordeTrailRandom=new NativeMinionHint.SlotRandom();
        // The grin's texture, quad and material (made once the bake is ready) and the block carrying its opacity.
        private Texture2D hordeGrinTexture;private Mesh hordeGrinQuad;private Material hordeGrinMaterial;private MaterialPropertyBlock hordeGrinBlock;private bool hordeGrinFailed;
        private static readonly int GrinOpacityId=Shader.PropertyToID("_Opacity");

        private static bool HordeJointShown(int lost,int joint){for(int i=0;i<HordeLimbBits.Length;i++)if((lost&HordeLimbBits[i])!=0&&BoneWithin(joint,HordeLimbBones[i]))return false;return true;}
        // A source-frame row-major matrix as a Unity matrix (x mirrored: S·M·S).
        private static Matrix4x4 FromSource(double[] s){var m=new Matrix4x4();for(int r=0;r<4;r++)for(int c=0;c<4;c++)m[r,c]=(float)(((r==0)^(c==0))?-s[r*4+c]:s[r*4+c]);return m;}
        private static Quaternion SourceTurn(double x,double y,double z){NativeFxMath.EulerQuaternion(x,y,z,out double qx,out double qy,out double qz,out double qw);return new Quaternion((float)qx,-(float)qy,-(float)qz,(float)qw);}
        /// <summary>The clock every minion effect runs on this frame; SourceWorld calls it before it poses the adds. A PvE arena
        /// starts the grin's bake (once, off the main thread), long before a possession can reach level 2.</summary>
        private void BeginHordeFx(NativeArena arena){hordeFxCount=0;hordeClock=FieldClock(arena);Shader.SetGlobalFloat(MinionClockId,hordeClock);if(arena.Pve!=null)NativeMinionHint.Glyph.Prepare();}
        /// <summary>The glow tint a horde add draws with this frame: white emission, its phase and drop bits in w (NativeSourceLit
        /// MinionFlicker; BeginHordeFx has set the clock).</summary>
        private Vector4 HordeGlow(int id)=>new Vector4(1,1,1,(float)NativeMinionFx.GlowW(NativeMinionFx.Phase(id),hordeClock));
        /// <summary>
        /// A posed horde add: keeps what the effects pass draws of it (the possessed add's hint, a vent climb's dust) and drips the
        /// possessed add's trail. emerge: seconds into its vent climb (NaN without one); death: seconds since it died (NaN alive);
        /// floor: the floor its climb's dust kicks off; ground: the floor under it now; possessedAt: its replicated possession start
        /// (NaN when it is not the possessed add); body: whether its body is still drawn; look: a kind's model (kind x 8 + look) or -1
        /// the bank's. Any add also starts the grin's bake (a guest or killcam that never saw the arena's PvE state).
        /// </summary>
        private void HordeAdd(CapturedAnimation.Player rig,int id,float emerge,float death,float floor,float ground,float possessedAt,Vector3 velocity,int lost,bool body,int look=-1)
        {
            if(rig==null||sourceModels==null)return;NativeMinionHint.Glyph.Prepare();bool possessed=!float.IsNaN(possessedAt);
            if(possessed&&body&&float.IsNaN(death))HordeTrail(id,possessedAt,rig.Root.transform.position,ground,velocity);
            bool emerging=!float.IsNaN(emerge)&&emerge>=0&&emerge<NativeMinionFx.EmergeSeconds+.4f;if(!(possessed&&body&&float.IsNaN(death))&&!emerging)return;
            if(hordeFxCount==hordeFx.Length)Array.Resize(ref hordeFx,hordeFx.Length*2);
            hordeFx[hordeFxCount++]=new HordeFx{Rig=rig,Id=id,Variant=NativeMinionFx.Variant(id),Lost=lost,Seed=NativeMinionFx.Seed(id),Time=hordeClock,Emerge=emerging?emerge:float.NaN,Death=death,Floor=floor,Ground=ground,Phase=(float)NativeMinionFx.Phase(id),PossessedAt=possessedAt,Velocity=velocity,Possessed=possessed,Body=body,Look=look};
        }
        /// <summary>
        /// horde-view.ts: from level 2 the walking possessed add drips black flame .25 m over its floor into the world flame pool
        /// (NativeMinionHint.Trail), on slots counted from its possession start and placed along its path since the last frame it
        /// was drawn, so every client and frame rate leaves the same trail. A rewind, a jump or another add starts it afresh.
        /// </summary>
        private void HordeTrail(int id,float possessedAt,Vector3 at,float ground,Vector3 velocity)
        {
            if(id==hordeTrailId)NativeMinionHint.Trail(WorldFlames(),hordeTrailRandom,id,possessedAt,hordeTrailClock,hordeClock,-hordeTrailAt.x,hordeTrailAt.z,-at.x,at.z,ground,-velocity.x,velocity.z,Mathf.Clamp01(EffectsQuality));
            hordeTrailId=id;hordeTrailClock=hordeClock;hordeTrailAt=at;
        }
        /// <summary>
        /// Whether the grin can be drawn: its texture, quad and material are made here, on the main thread, the first frame the
        /// bake (NativeMinionHint.Glyph.Prepare, started by BeginHordeFx and HordeAdd) is ready. Until then the grin is left out
        /// rather than waited for (a guest joining a possession past level 2 sees it a few frames late, never a hitch).
        /// </summary>
        private bool HordeGrinReady()
        {
            if(hordeGrinMaterial!=null)return true;if(hordeGrinFailed)return false;var image=NativeMinionHint.Glyph.Ready;
            if(image==null){if(NativeMinionHint.Glyph.Failure!=null){hordeGrinFailed=true;Debug.LogWarning("Siren possession grin bake failed: "+NativeMinionHint.Glyph.Failure);}return false;}
            var shader=Resources.Load<Shader>("NativeMinionGrin");if(shader==null){hordeGrinFailed=true;Debug.LogWarning("NativeMinionGrin shader is missing");return false;}
            hordeGrinTexture=new Texture2D(image.Width,image.Height,TextureFormat.RGBA32,image.Levels,false){name="Siren possession grin",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,hideFlags=HideFlags.HideAndDontSave};
            hordeGrinTexture.LoadRawTextureData(image.Rgba);hordeGrinTexture.Apply(false,true);
            hordeGrinQuad=new Mesh{name="Siren possession grin",hideFlags=HideFlags.HideAndDontSave,vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)},uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)},triangles=new[]{0,1,2,0,2,3}};
            hordeGrinBlock=new MaterialPropertyBlock();
            hordeGrinMaterial=new Material(shader){name="Siren possession grin",renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent+2,mainTexture=hordeGrinTexture,hideFlags=HideFlags.HideAndDontSave};
            return true;
        }
        /// <summary>
        /// The Siren's grin (hints.ts glyph(), baked by NativeMinionHint.Glyph) as one .78 x .29 m quad facing the camera at
        /// `center`, the whole image at `opacity`, as the source's sprite fades its canvas: one draw, after the effect layer's (so
        /// over the seam and halo it floats above).
        /// </summary>
        private void HordeGrin(Camera camera,Vector3 center,float opacity)
        {
            hordeGrinBlock.SetFloat(GrinOpacityId,opacity);
            Graphics.DrawMesh(hordeGrinQuad,Matrix4x4.TRS(center,camera.transform.rotation,new Vector3((float)NativeMinionHint.Glyph.Width,(float)NativeMinionHint.Glyph.Height,1)),hordeGrinMaterial,0,camera,0,hordeGrinBlock,UnityEngine.Rendering.ShadowCastingMode.Off,false);
        }
        // The crack of a bank variant (look -1) or of a minion kind's look (kind x 8 + look, Core/NativeHordeModels): the possession
        // hint splits a cracked crown.
        private static bool HordeCracked(int variant,int look)=>look<0?NativeMinionFx.Cracked[variant]:NativeHordeModels.Cracked(look>>3,look&7);
        private void DisposeHordeGrin()
        {
            if(hordeGrinMaterial!=null)CapturedAnimation.Release(hordeGrinMaterial);if(hordeGrinTexture!=null)CapturedAnimation.Release(hordeGrinTexture);if(hordeGrinQuad!=null)CapturedAnimation.Release(hordeGrinQuad);
            hordeGrinMaterial=null;hordeGrinTexture=null;hordeGrinQuad=null;hordeGrinBlock=null;
        }
        /// <summary>The dying add's variation (the zombie kinds' models; white for the bank's), kept for its jaw.</summary>
        private Color hordeDeathBody=Color.white;
        /// <summary>
        /// The dying body on its rig (it falls on its kind's take; NativeHordeGorePresentation.cs keeps it as a corpse once it has
        /// settled). `burn`: the Cinderling's ember crumble (motion.ts dyingLook: each joint chars at its hashed time and shrinks by
        /// its rest height as the burn-away reaches it, gone at DEATH_SECONDS); every other body stays whole and uncharred.
        /// </summary>
        private void HordeDeathBody(CapturedAnimation.Player rig,string model,int id,int lost,float age,Func<int,bool> visible,bool burn=false)
        {
            hordeDeathRig=rig;hordeDeathSeed=NativeMinionFx.Seed(id);hordeDeathAge=age;hordeDeathBurn=burn;
            hordeDeathBone??=b=>
            {
                var m=hordeDeathRig.Bones[b].localToWorldMatrix;if(!hordeDeathBurn)return m;double v=NativeMinionFx.Vanish(b,hordeDeathAge);if(v<=0)return m;var rest=NativeMinionFx.JointRest;
                Vector3 c=new Vector3(-(float)rest[b*4+1],(float)rest[b*4+2],(float)rest[b*4+3]);float k=Mathf.Max(.001f,1-(float)v);
                return m*Matrix4x4.Translate(c)*Matrix4x4.Scale(new Vector3(k,k,k))*Matrix4x4.Translate(-c);
            };
            // The char darkens the joint (rgb) and, as 1 - alpha, moves its glow cells to the charred glow and flicker (NativeSourceLit).
            hordeDeathTint??=b=>{float c=(float)NativeMinionFx.Charred(hordeDeathSeed,b,hordeDeathAge),k=1-(1-(float)NativeMinionFx.CharScale)*c;return new Color(k,k,k,1-c);};
            hordeDeathBody=HordeVariation(model,id);
            sourceModels.Queue(model,rig.Root.transform.localToWorldMatrix,hordeDeathBone,visible,body:hordeDeathBody,partTint:burn?hordeDeathTint:null,glow:HordeGlow(id));
        }
        /// <summary>DevEffects: the frame's horde effects into the shared effect layers, after every rig is posed: a vent climb's
        /// dust and grit, and the possessed add's hint (its seam and halo, from level 2 its grin).</summary>
        private void DrawHordeFx(Camera camera)
        {
            if(fxDraw==null||camera==null){hordeFxCount=0;return;}
            var draw=fxDraw;float quality=Mathf.Clamp01(EffectsQuality);Vector3 eye=camera.transform.position,right=camera.transform.right,up=camera.transform.up;bool grin=hordeFxCount>0&&HordeGrinReady();
            for(int n=0;n<hordeFxCount;n++)
            {
                var job=hordeFx[n];var rig=job.Rig;if(rig==null)continue;var rootT=rig.Root.transform;double t=job.Time;bool alive=float.IsNaN(job.Death);
                // A vent climb: dust and grit kicked up round the rim as it claws out (NativeHordeGore.VentDust; Reduced effects halve it).
                if(!float.IsNaN(job.Emerge)&&vfx!=null&&Vector3.Distance(rootT.position,eye)<=NativeMinionFx.FarBand)
                {
                    var at=new Vector3(rootT.position.x,job.Floor,rootT.position.z);int puffs=quality<.5f?NativeHordeGore.VentPuffs/2:NativeHordeGore.VentPuffs;
                    for(int i=0;i<puffs;i++)if(NativeHordeGore.VentDust(job.Seed,i,job.Emerge,out var o,out float size,out float opacity,out float u))vfx.Smoke(at+new Vector3(o.X,o.Y,o.Z),VentDustTint,opacity,size,u,(job.Seed+(uint)i)%8191u,job.Floor);
                }
                if(!job.Body||!job.Possessed||!alive)continue;
                int level=NativeMinionHint.Level(true,job.PossessedAt,t);
                // hints.ts on the possessed add at every level: the crown seam (the crack on a cracked head) and the halo pulse every
                // ~4 s, brighter at level 3.
                if(HordeJointShown(job.Lost,CapturedRig.Head))if(level>0)
                {
                    var headBone=rig.Bones[CapturedRig.Head].localToWorldMatrix;bool cracked=HordeCracked(job.Variant,job.Look);
                    NativeMinionFx.Hint(cracked,NativeMinionFx.HintPulse(job.Phase,t),out double cx,out double cy,out double cz,out double sx,out double sy,out double sz,out double seam,out double halo,out double haloOpacity,NativeMinionHint.Brightness(level));
                    draw.Box(draw.Effects,headBone*Matrix4x4.TRS(new Vector3(-(float)cx,(float)cy,(float)cz),Quaternion.identity,new Vector3((float)sx,(float)sy,(float)sz)),Alpha(HordeViolet,seam),NativeFxDraw.Flat,NativeFxDraw.Authored+NativeFxDraw.Additive,Vector4.zero,Vector4.zero);
                    draw.Sprite(draw.Effects,headBone.MultiplyPoint3x4(new Vector3(0,.38f,0)),right,up,(float)halo,Alpha(HordeViolet,haloOpacity),NativeFxDraw.Authored+NativeFxDraw.Additive);
                }
                // hints.ts levels 2 and 3: the grin floats upright over the head joint in the rig group (shown while the body is),
                // facing the camera, within 25 m of the add's floor + 1 m (horde-view.ts dist); drawn over the seam and halo. Left out
                // until its bake is ready (HordeGrinReady).
                if(level>=2&&grin)
                {
                    Vector3 feet=rootT.position;feet.y=job.Ground+1;
                    if(NativeMinionHint.GlyphShown(level,Vector3.Distance(feet,eye)))
                    {
                        Vector3 c=rootT.TransformPoint(rootT.InverseTransformPoint(rig.Bones[CapturedRig.Head].position)+new Vector3(0,(float)NativeMinionHint.GlyphLift(t),0));
                        HordeGrin(camera,c,(float)NativeMinionHint.GlyphOpacity(level,t));
                    }
                }
            }
            hordeFxCount=0;
        }
    }

    public sealed partial class NativeSourceModels
    {
        private readonly Part[] hordeTongues=new Part[2];
        private static readonly int MinionGlowId=Shader.PropertyToID("_MinionGlow");
        // tools/export_models.mjs: the horde-* bodies flag their glow cells, steady since 2026-10-19 (no Siren flicker on the horde,
        // owner "standard zombies"); the horde-tongue pieces (model.ts TONGUE_MATERIAL) are lifted out of the bank and never drawn.
        private void HordeMaterials()
        {
            foreach(var pair in models)
            {
                if(!pair.Key.StartsWith("horde-",StringComparison.Ordinal))continue;
                if(pair.Key=="horde-tongue"){foreach(var p in pair.Value){int k=p.Name=="horde-tongue-1"?1:0;hordeTongues[k]=p;p.NoShadow=true;p.Material.SetFloat(InkRimId,.5f);p.Material.SetFloat(InkFlowId,.12f);pieceMeshes.Add(p.Mesh);}continue;}
                foreach(var p in pair.Value)p.Material.SetFloat(MinionGlowId,0);
            }
            if(hordeTongues[0]==null||hordeTongues[1]==null)throw new System.IO.InvalidDataException("Horde tongue pieces missing from the model bank");
            // Pieces leave the model list (nothing queues them as a model); DisposePieces releases their meshes.
            models.Remove("horde-tongue");
        }
    }
}
