using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // Siren minion effects (Core/NativeMinionFx.cs) on the PvE horde adds, drawn only where an add itself is drawn (SourceWorld
    // for living and entering adds, SourceMinionDeath for the dying), so menus and the killcam show them exactly as they show
    // the adds. Everything comes from what host, guests and the killcam replay all hold: the add's id (bank variant, look seed
    // and glow phase), its vent entry (ActiveAt), whether it is the possessed add and since when (PossessedAt), its lost limbs,
    // its death record and the presentation clock (FieldClock: smooth on guests). Presentation only: no hit, collider, timing
    // or network state changes.
    //  - Glow cells (cracks, embers, dying strips) flicker in NativeSourceLit on _MinionClock, with the add's phase and this
    //    frame's power-drop bits in _GlowTint.w (NativeMinionFx.GlowW, the bits the crack haze drops on).
    //  - Tongues are the bank's horde-tongue pieces, swelling and swaying on their joints (instanced with the bodies).
    //  - Shed licks, the cracked head's haze, the possession hint's seam and halo, the vent emergence and the death flames,
    //    ash and scorch go into the shared Siren effect layers (NativeFxDraw) once the frame's rigs are posed (DevEffects).
    //  - The possession hint by its level (Core/NativeMinionHint.cs, from the replicated PossessedAt): the crown seam and halo
    //    pulse, from level 2 the Siren's grin floating over the head within 25 m and the black-flame trail dripped into the
    //    world flame pool while the add walks, at level 3 all brighter. The grin is hints.ts's canvas baked once into a texture
    //    (NativeMinionHint.Glyph, off the main thread from the first PvE frame or horde add) and drawn as one quad at its
    //    opacity after the effect layer (Resources/NativeMinionGrin.shader), so it fades as one image as the source's sprite
    //    does; it is one quad at every effects quality, as in the source. The trail emits in SourceWorld, before the pool
    //    ticks, as the stump drips do.
    //  - The dying body chars joint by joint (its char in _SceneTint.a keeps the embers flickering) and burns away by rest
    //    height instead of the old dithered fade.
    // Counts follow EffectsQuality (Reduced .3 halves the effect cubes and the licks, as fx.ts halves its cubes below .5);
    // reduced effects also draw the licks flat, as the Siren's flames. Every add is a full rig here, where the source gives
    // effects only to its 24 nearest full rigs (horde-view.ts), so the effects go out by NativeMinionFx.Rank: effect cubes to
    // the possessed and dying adds, then the vent climbers, each nearest first; licks to the possessed add, then the nearest.
    // Each add takes its effect cubes and its licks whole, reserving its most, so the cut never falls inside an add and only
    // moves when the ranks do; each budget is 24 adds' worth. Effect cubes are full to 24 m, halved to 40 m and left out
    // beyond (NativeMinionFx.Band); licks stop beyond LickRange, the source's full-rig radius. Allocation free in steady
    // state (the job arrays grow to the largest horde seen).
    public sealed partial class ArenaPresentation
    {
        private struct HordeFx{public CapturedAnimation.Player Rig;public int Id,Variant,Lost,Look;public uint Seed;public float Time,Emerge,Death,Floor,Ground,Phase,PossessedAt;public Vector3 Velocity;public bool Possessed,Body;}
        private const int HordeCubeBudget=NativeMinionFx.FullRigs*NativeMinionFx.EffectCubes;private const float LickRange=20;
        private static readonly int HordeLickBudget=NativeMinionFx.FullRigs*Math.Max(NativeMinionFx.MostTongues,NativeHordeModels.MostTongues)*NativeMinionFx.Licks;
        private HordeFx[] hordeFx=new HordeFx[64];private float[] hordeRank=new float[64],hordeDistance=new float[64];private int[] hordeOrder=new int[64];private int hordeFxCount;private float hordeClock;
        private readonly double[] hordeLocal=new double[16],hordePoints=new double[18];private readonly NativeMinionFx.Cube[] hordeCubes=new NativeMinionFx.Cube[NativeMinionFx.EffectCubes];
        private static readonly int MinionClockId=Shader.PropertyToID("_MinionClock");
        // fx.ts bodyPoints: head, chest, pelvis, left knee, right knee, left elbow.
        private static readonly int[] HordePointBones={5,3,1,15,19,8};
        private static readonly int[] HordeLimbBits={1,2,4,8,16,32,64,128,256},HordeLimbBones={5,7,11,8,12,14,18,15,19};
        // The dying body's joint matrices (burn-away) and tints (char), read by Queue while it runs; no per-frame closures.
        private CapturedAnimation.Player hordeDeathRig;private uint hordeDeathSeed;private float hordeDeathAge;private Func<int,Matrix4x4> hordeDeathBone;private Func<int,Color> hordeDeathTint;
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
        /// A posed horde add: queues its tongues (bank pieces on its joints) and keeps its effects for DevEffects. emerge: seconds
        /// into its vent climb (NaN without one); death: seconds since it died (NaN alive); floor: the world height of the floor
        /// its emergence pool lies on; ground: the floor under it now; possessedAt: its replicated possession start (NaN when it
        /// is not the possessed add); body: whether its body is still drawn (tongues, licks and the hint need it); look: a Siren
        /// minion kind's model (kind x 8 + look, Core/NativeHordeModels: its own tongue anchors and crack), -1 the bank variant's.
        /// Any add also starts the grin's bake (a guest or killcam that never saw the arena's PvE state).
        /// </summary>
        private void HordeAdd(CapturedAnimation.Player rig,int id,float emerge,float death,float floor,float ground,float possessedAt,Vector3 velocity,int lost,bool body,int look=-1)
        {
            if(rig==null||sourceModels==null)return;NativeMinionHint.Glyph.Prepare();int variant=NativeMinionFx.Variant(id);bool possessed=!float.IsNaN(possessedAt);
            if(possessed&&body&&float.IsNaN(death))HordeTrail(id,possessedAt,rig.Root.transform.position,ground,velocity);
            if(body&&!NativeMinionFx.Gone(death))
            {
                double flare=NativeMinionFx.Flare(float.IsNaN(emerge)?double.PositiveInfinity:emerge,float.IsNaN(death)?double.PositiveInfinity:death),extra=NativeMinionFx.TonguePhase(id);
                for(int k=0;k<HordeTongueCount(variant,look);k++)
                {if(!HordeTongue(variant,look,k,hordeClock,flare,extra,hordeLocal,out int joint,out int layout)||!HordeJointShown(lost,joint))continue;sourceModels.QueueHordeTongue(layout,rig.Bones[joint].localToWorldMatrix*FromSource(hordeLocal));}
            }
            if(hordeFxCount==hordeFx.Length){Array.Resize(ref hordeFx,hordeFx.Length*2);Array.Resize(ref hordeRank,hordeFx.Length);Array.Resize(ref hordeDistance,hordeFx.Length);Array.Resize(ref hordeOrder,hordeFx.Length);}
            hordeFx[hordeFxCount++]=new HordeFx{Rig=rig,Id=id,Variant=variant,Lost=lost,Seed=NativeMinionFx.Seed(id),Time=hordeClock,Emerge=emerge,Death=death,Floor=floor,Ground=ground,Phase=(float)NativeMinionFx.Phase(id),PossessedAt=possessedAt,Velocity=velocity,Possessed=possessed,Body=body,Look=look};
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
        // The tongues and the crack of a bank variant (look -1) or of a minion kind's look (kind x 8 + look, Core/NativeHordeModels).
        private static int HordeTongueCount(int variant,int look)=>look<0?NativeMinionFx.TongueCount(variant):NativeHordeModels.Tongues(look>>3,look&7).Length;
        private static bool HordeTongue(int variant,int look,int k,double t,double flare,double extra,double[] m,out int joint,out int layout)
            =>look<0?NativeMinionFx.TongueMatrix(variant,k,t,flare,extra,m,out joint,out layout):NativeHordeModels.TongueMatrix(look>>3,look&7,k,t,flare,extra,m,out joint,out layout);
        private static bool HordeCracked(int variant,int look)=>look<0?NativeMinionFx.Cracked[variant]:NativeHordeModels.Cracked(look>>3,look&7);
        private static void HordeCrack(int variant,int look,out double x,out double y,out double z)
        {if(look>=0){NativeHordeModels.Crack(look>>3,look&7,out x,out y,out z);return;}var c=NativeMinionFx.Crack;int o=variant*3;x=c[o];y=c[o+1];z=c[o+2];}
        private void DisposeHordeGrin()
        {
            if(hordeGrinMaterial!=null)CapturedAnimation.Release(hordeGrinMaterial);if(hordeGrinTexture!=null)CapturedAnimation.Release(hordeGrinTexture);if(hordeGrinQuad!=null)CapturedAnimation.Release(hordeGrinQuad);
            hordeGrinMaterial=null;hordeGrinTexture=null;hordeGrinQuad=null;hordeGrinBlock=null;
        }
        /// <summary>
        /// The horde death draw (motion.ts dyingLook): queues the posed body with each joint charred at its hashed time and shrunk
        /// about its parts' centre as the burn-away reaches its rest height; the body is gone at DEATH_SECONDS.
        /// </summary>
        private void HordeDeathBody(CapturedAnimation.Player rig,string model,int id,int lost,float age,Func<int,bool> visible)
        {
            hordeDeathRig=rig;hordeDeathSeed=NativeMinionFx.Seed(id);hordeDeathAge=age;
            hordeDeathBone??=b=>
            {
                var m=hordeDeathRig.Bones[b].localToWorldMatrix;double v=NativeMinionFx.Vanish(b,hordeDeathAge);if(v<=0)return m;var rest=NativeMinionFx.JointRest;
                Vector3 c=new Vector3(-(float)rest[b*4+1],(float)rest[b*4+2],(float)rest[b*4+3]);float k=Mathf.Max(.001f,1-(float)v);
                return m*Matrix4x4.Translate(c)*Matrix4x4.Scale(new Vector3(k,k,k))*Matrix4x4.Translate(-c);
            };
            // The char darkens the joint (rgb) and, as 1 - alpha, moves its glow cells to the charred glow and flicker (NativeSourceLit).
            hordeDeathTint??=b=>{float c=(float)NativeMinionFx.Charred(hordeDeathSeed,b,hordeDeathAge),k=1-(1-(float)NativeMinionFx.CharScale)*c;return new Color(k,k,k,1-c);};
            sourceModels.Queue(model,rig.Root.transform.localToWorldMatrix,hordeDeathBone,visible,partTint:hordeDeathTint,glow:HordeGlow(id));
        }
        /// <summary>DevEffects: the frame's horde effects into the shared effect layers, after every rig is posed.</summary>
        private void DrawHordeFx(Camera camera)
        {
            if(fxDraw==null||camera==null){hordeFxCount=0;return;}
            var draw=fxDraw;bool flat=HaloReduced;float quality=Mathf.Clamp01(EffectsQuality),rim=(float)NativeSirenLook.FlameRim;int cubes=HordeCubeBudget,lickBudget=HordeLickBudget,licks=NativeMinionFx.LickCount(quality);
            Vector3 eye=camera.transform.position,right=camera.transform.right,up=camera.transform.up;bool grin=hordeFxCount>0&&HordeGrinReady();
            // fx.ts MinionFx by rank (the possessed and dying adds, then vent climbers, nearest first): the vent emergence pool, rim
            // flames and sparks, or the burn-away's flames, ash and scorch, at the add's distance band, all of them or none.
            for(int n=0;n<hordeFxCount;n++){var job=hordeFx[n];float distance=hordeDistance[n]=job.Rig==null?float.MaxValue:Vector3.Distance(job.Rig.Root.transform.position,eye);hordeOrder[n]=n;hordeRank[n]=job.Rig==null?float.MaxValue:(float)NativeMinionFx.Rank(job.Possessed,job.Emerge,job.Death,distance);}
            Array.Sort(hordeRank,hordeOrder,0,hordeFxCount);
            for(int s=0;s<hordeFxCount;s++)
            {
                int n=hordeOrder[s];var job=hordeFx[n];var rig=job.Rig;if(rig==null)continue;var root=rig.Root.transform;bool alive=float.IsNaN(job.Death);
                double band=NativeMinionFx.Band(hordeDistance[n],quality);int most=NativeMinionFx.MostCubes(band);
                if(most>0&&most<=cubes&&NativeMinionFx.Showing(job.Emerge,job.Death))
                {
                    cubes-=most;int points=0;if(!alive)for(int i=0;i<HordePointBones.Length;i++){Vector3 p=root.InverseTransformPoint(rig.Bones[HordePointBones[i]].position);hordePoints[i*3]=-p.x;hordePoints[i*3+1]=p.y;hordePoints[i*3+2]=p.z;points++;}
                    float floor=root.InverseTransformPoint(new Vector3(root.position.x,job.Floor,root.position.z)).y;
                    int count=NativeMinionFx.Effects(job.Seed,job.Emerge,job.Death,floor,hordePoints,points,band,hordeCubes);
                    for(int i=0;i<count;i++)
                    {
                        var c=hordeCubes[i];Vector3 at=root.TransformPoint(new Vector3(-(float)c.X,(float)c.Y,(float)c.Z));
                        draw.Cube(draw.Solid,at,root.rotation*SourceTurn(c.Ex,c.Ey,c.Ez),new Vector3((float)c.Sx,(float)c.Sy,(float)c.Sz),new Color((float)c.R,(float)c.G,(float)c.B,1).gamma,NativeFxDraw.Authored);
                    }
                }
            }
            // The licks, haze, hint and grin of each drawn body, the possessed add first, then the nearest.
            for(int n=0;n<hordeFxCount;n++){var job=hordeFx[n];hordeOrder[n]=n;hordeRank[n]=job.Rig==null?float.MaxValue:(float)NativeMinionFx.Rank(job.Possessed,double.NaN,double.NaN,hordeDistance[n]);}
            Array.Sort(hordeRank,hordeOrder,0,hordeFxCount);
            for(int s=0;s<hordeFxCount;s++)
            {
                int n=hordeOrder[s];var job=hordeFx[n];var rig=job.Rig;if(rig==null||!job.Body||NativeMinionFx.Gone(job.Death))continue;double t=job.Time;bool alive=float.IsNaN(job.Death);
                bool head=HordeJointShown(job.Lost,CapturedRig.Head);int want=HordeTongueCount(job.Variant,job.Look)*licks;
                // Shed licks off each tongue's tip, trailing as if they stayed where they were shed: every tongue's or none.
                if(hordeDistance[n]<LickRange&&want>0&&want<=lickBudget)
                {
                    lickBudget-=want;
                    double flare=NativeMinionFx.Flare(float.IsNaN(job.Emerge)?double.PositiveInfinity:job.Emerge,alive?double.PositiveInfinity:job.Death),extra=NativeMinionFx.TonguePhase(job.Id);
                    for(int k=0;k<HordeTongueCount(job.Variant,job.Look);k++)
                    {
                        if(!HordeTongue(job.Variant,job.Look,k,t,flare,extra,hordeLocal,out int joint,out _)||!HordeJointShown(job.Lost,joint))continue;
                        Vector3 tip=(rig.Bones[joint].localToWorldMatrix*FromSource(hordeLocal)).MultiplyPoint3x4(new Vector3(0,(float)NativeMinionFx.TipY,0));
                        for(int j=0;j<licks;j++)
                        {
                            if(!NativeMinionFx.Lick(job.Seed,k,j,t,-job.Velocity.x,job.Velocity.z,out double dx,out double dy,out double dz,out double ex,out double ey,out double ez,out double edge,out double r,out double g,out double b))continue;
                            Vector3 at=tip+new Vector3(-(float)dx,(float)dy,(float)dz),size=new Vector3((float)edge*.8f,(float)edge*1.7f,(float)edge*.8f);var turn=SourceTurn(ex,ey,ez);var color=new Color((float)r,(float)g,(float)b,1);
                            if(flat)draw.Cube(draw.Solid,at,turn,size,color);else draw.FlameCube(draw.Solid,at,turn,size,color,AshRaw,rim);
                        }
                    }
                }
                int level=job.Possessed&&alive?NativeMinionHint.Level(true,job.PossessedAt,t):0;
                if(head)
                {
                    var headBone=rig.Bones[CapturedRig.Head].localToWorldMatrix;float headChar=alive?0:(float)NativeMinionFx.Charred(job.Seed,CapturedRig.Head,job.Death);
                    // The cracked head's violet haze, breathing and dropping out with the crack's light (the GlowW bits); it dies with the char.
                    if(HordeCracked(job.Variant,job.Look)&&headChar<1)
                    {
                        HordeCrack(job.Variant,job.Look,out double cx0,out double cy0,out double cz0);NativeMinionFx.Haze(job.Phase,t,out double size,out double opacity);
                        Vector3 at=headBone.MultiplyPoint3x4(new Vector3(-(float)cx0,(float)cy0,(float)cz0+.03f));
                        draw.Sprite(draw.Effects,at,right,up,(float)size,Alpha(HordeViolet,opacity*(1-headChar)),NativeFxDraw.Authored+NativeFxDraw.Additive);
                    }
                    // hints.ts on the possessed add at every level: the crown seam (the crack on a cracked head) and the halo pulse every
                    // ~4 s, brighter at level 3.
                    if(level>0)
                    {
                        bool cracked=HordeCracked(job.Variant,job.Look);NativeMinionFx.Hint(cracked,NativeMinionFx.HintPulse(job.Phase,t),out double cx,out double cy,out double cz,out double sx,out double sy,out double sz,out double seam,out double halo,out double haloOpacity,NativeMinionHint.Brightness(level));
                        draw.Box(draw.Effects,headBone*Matrix4x4.TRS(new Vector3(-(float)cx,(float)cy,(float)cz),Quaternion.identity,new Vector3((float)sx,(float)sy,(float)sz)),Alpha(HordeViolet,seam),NativeFxDraw.Flat,NativeFxDraw.Authored+NativeFxDraw.Additive,Vector4.zero,Vector4.zero);
                        draw.Sprite(draw.Effects,headBone.MultiplyPoint3x4(new Vector3(0,.38f,0)),right,up,(float)halo,Alpha(HordeViolet,haloOpacity),NativeFxDraw.Authored+NativeFxDraw.Additive);
                    }
                }
                // hints.ts levels 2 and 3: the grin floats upright over the head joint in the rig group (shown while the body is),
                // facing the camera, within 25 m of the add's floor + 1 m (horde-view.ts dist); drawn over the seam and halo, as three
                // sorts the higher sprite nearer a camera looking down. Left out until its bake is ready (HordeGrinReady).
                if(level>=2&&grin)
                {
                    var rootT=rig.Root.transform;Vector3 feet=rootT.position;feet.y=job.Ground+1;
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
        private readonly Part[] hordeTongues=new Part[2];private readonly Vector4 hordeTongueGlow=Vector4.one;
        private static readonly int MinionGlowId=Shader.PropertyToID("_MinionGlow");
        // tools/export_models.mjs: the horde-* bodies flag their glow cells (NativeSourceLit flickers them per add); the
        // horde-tongue pieces are model.ts TONGUE_MATERIAL, the Siren's ink at rim .5 and rising streaks .12, without shadows.
        private void HordeMaterials()
        {
            foreach(var pair in models)
            {
                if(!pair.Key.StartsWith("horde-",StringComparison.Ordinal))continue;
                if(pair.Key=="horde-tongue"){foreach(var p in pair.Value){int k=p.Name=="horde-tongue-1"?1:0;hordeTongues[k]=p;p.NoShadow=true;p.Material.SetFloat(InkRimId,.5f);p.Material.SetFloat(InkFlowId,.12f);pieceMeshes.Add(p.Mesh);}continue;}
                foreach(var p in pair.Value)p.Material.SetFloat(MinionGlowId,1);
            }
            if(hordeTongues[0]==null||hordeTongues[1]==null)throw new System.IO.InvalidDataException("Horde tongue pieces missing from the model bank");
            // Pieces leave the model list (nothing queues them as a model); DisposePieces releases their meshes.
            models.Remove("horde-tongue");
        }
        /// <summary>One horde tongue (layout 0/1) at a world matrix, drawn with this frame's source models.</summary>
        public void QueueHordeTongue(int layout,Matrix4x4 world,int layer=0,Camera camera=null)
        {
            var part=hordeTongues[layout&1];if(part==null)return;var key=(part,layer);if(!draws.TryGetValue(key,out var d)){d=new Draws{Part=part,Layer=layer,Camera=camera};draws.Add(key,d);}
            d.Matrices.Add(world);d.Bodies.Add(Vector4.one);d.Accents.Add(Vector4.one);d.Tints.Add(Vector4.one);d.Glows.Add(hordeTongueGlow);
        }
    }
}
