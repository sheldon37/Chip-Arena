using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // Siren minion effects (Core/NativeMinionFx.cs) on the PvE horde adds, drawn only where an add itself is drawn (SourceWorld
    // for living and entering adds, SourceMinionDeath for the dying), so menus and the killcam show them exactly as they show
    // the adds. Everything comes from what host, guests and the killcam replay all hold: the add's id (bank variant, look seed
    // and glow phase), its vent entry (ActiveAt), whether it is the possessed add, its lost limbs, its death record and the
    // presentation clock (FieldClock: smooth on guests). Presentation only: no hit, collider, timing or network state changes.
    //  - Glow cells (cracks, embers, dying strips) flicker in NativeSourceLit on _MinionClock and the add's phase (_GlowTint.w).
    //  - Tongues are the bank's horde-tongue pieces, swelling and swaying on their joints (instanced with the bodies).
    //  - Shed licks, the cracked head's haze, the possession hint, the vent emergence and the death flames, ash and scorch go
    //    into the shared Siren effect layers (NativeFxDraw) once the frame's rigs are posed (DevEffects).
    //  - The dying body chars joint by joint and burns away by rest height instead of the old dithered fade.
    // Counts follow EffectsQuality (Reduced .3 halves the effect cubes and the licks, as fx.ts halves its cubes below .5);
    // reduced effects also draw the licks flat, as the Siren's flames. Licks stop beyond LickRange; at most HordeCubeBudget
    // effect cubes a frame. Allocation free in steady state.
    public sealed partial class ArenaPresentation
    {
        private struct HordeFx{public CapturedAnimation.Player Rig;public int Id,Variant,Lost;public uint Seed;public float Time,Emerge,Death,Floor,Phase;public Vector3 Velocity;public bool Possessed,Body;}
        private const int HordeFxMax=128,HordeCubeBudget=1600;private const float LickRange=24;
        private readonly HordeFx[] hordeFx=new HordeFx[HordeFxMax];private int hordeFxCount;private float hordeClock;
        private readonly double[] hordeLocal=new double[16],hordePoints=new double[18];private readonly NativeMinionFx.Cube[] hordeCubes=new NativeMinionFx.Cube[NativeMinionFx.EffectCubes];
        private static readonly int MinionClockId=Shader.PropertyToID("_MinionClock");
        // fx.ts bodyPoints: head, chest, pelvis, left knee, right knee, left elbow.
        private static readonly int[] HordePointBones={5,3,1,15,19,8};
        private static readonly int[] HordeLimbBits={1,2,4,8,16,32,64,128,256},HordeLimbBones={5,7,11,8,12,14,18,15,19};
        // The dying body's joint matrices (burn-away) and tints (char), read by Queue while it runs; no per-frame closures.
        private CapturedAnimation.Player hordeDeathRig;private uint hordeDeathSeed;private float hordeDeathAge;private Func<int,Matrix4x4> hordeDeathBone;private Func<int,Color> hordeDeathTint;
        private static readonly Color HordeViolet=NativeArena.ColorOf(NativeMinionFx.Violet);

        private static bool HordeJointShown(int lost,int joint){for(int i=0;i<HordeLimbBits.Length;i++)if((lost&HordeLimbBits[i])!=0&&BoneWithin(joint,HordeLimbBones[i]))return false;return true;}
        // A source-frame row-major matrix as a Unity matrix (x mirrored: S·M·S).
        private static Matrix4x4 FromSource(double[] s){var m=new Matrix4x4();for(int r=0;r<4;r++)for(int c=0;c<4;c++)m[r,c]=(float)(((r==0)^(c==0))?-s[r*4+c]:s[r*4+c]);return m;}
        private static Quaternion SourceTurn(double x,double y,double z){NativeFxMath.EulerQuaternion(x,y,z,out double qx,out double qy,out double qz,out double qw);return new Quaternion((float)qx,-(float)qy,-(float)qz,(float)qw);}
        /// <summary>The clock every minion effect runs on this frame; SourceWorld calls it before it poses the adds.</summary>
        private void BeginHordeFx(NativeArena arena){hordeFxCount=0;hordeClock=FieldClock(arena);Shader.SetGlobalFloat(MinionClockId,hordeClock);}
        /// <summary>The glow tint a horde add draws with: white emission, its phase in w (NativeSourceLit MinionFlicker).</summary>
        private static Vector4 HordeGlow(int id)=>new Vector4(1,1,1,(float)NativeMinionFx.Phase(id));
        /// <summary>
        /// A posed horde add: queues its tongues (bank pieces on its joints) and keeps its effects for DevEffects. emerge: seconds
        /// into its vent climb (NaN without one); death: seconds since it died (NaN alive); floor: the world height of the floor
        /// under it; body: whether its body is still drawn (tongues and licks need it).
        /// </summary>
        private void HordeAdd(CapturedAnimation.Player rig,int id,float emerge,float death,float floor,bool possessed,Vector3 velocity,int lost,bool body)
        {
            if(rig==null||sourceModels==null)return;int variant=NativeMinionFx.Variant(id);
            if(body&&!NativeMinionFx.Gone(death))
            {
                double flare=NativeMinionFx.Flare(float.IsNaN(emerge)?double.PositiveInfinity:emerge,float.IsNaN(death)?double.PositiveInfinity:death),extra=NativeMinionFx.TonguePhase(id);
                for(int k=0;k<NativeMinionFx.TongueCount(variant);k++)
                {if(!NativeMinionFx.TongueMatrix(variant,k,hordeClock,flare,extra,hordeLocal,out int joint,out int layout)||!HordeJointShown(lost,joint))continue;sourceModels.QueueHordeTongue(layout,rig.Bones[joint].localToWorldMatrix*FromSource(hordeLocal));}
            }
            if(hordeFxCount>=HordeFxMax)return;
            hordeFx[hordeFxCount++]=new HordeFx{Rig=rig,Id=id,Variant=variant,Lost=lost,Seed=NativeMinionFx.Seed(id),Time=hordeClock,Emerge=emerge,Death=death,Floor=floor,Phase=(float)NativeMinionFx.Phase(id),Velocity=velocity,Possessed=possessed,Body=body};
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
            hordeDeathTint??=b=>{float k=1-(1-(float)NativeMinionFx.CharScale)*(float)NativeMinionFx.Charred(hordeDeathSeed,b,hordeDeathAge);return new Color(k,k,k,1);};
            sourceModels.Queue(model,rig.Root.transform.localToWorldMatrix,hordeDeathBone,visible,partTint:hordeDeathTint,glow:HordeGlow(id));
        }
        /// <summary>DevEffects: the frame's horde effects into the shared effect layers, after every rig is posed.</summary>
        private void DrawHordeFx(Camera camera)
        {
            if(fxDraw==null||camera==null){hordeFxCount=0;return;}
            var draw=fxDraw;bool flat=HaloReduced;float quality=Mathf.Clamp01(EffectsQuality),rim=(float)NativeSirenLook.FlameRim;int budget=HordeCubeBudget,licks=NativeMinionFx.LickCount(quality);
            Vector3 eye=camera.transform.position,right=camera.transform.right,up=camera.transform.up;
            for(int n=0;n<hordeFxCount;n++)
            {
                var job=hordeFx[n];var rig=job.Rig;if(rig==null)continue;var root=rig.Root.transform;double t=job.Time;bool alive=float.IsNaN(job.Death),gone=NativeMinionFx.Gone(job.Death);
                // fx.ts MinionFx: the vent emergence pool, rim flames and sparks, or the burn-away's flames, ash and scorch.
                int points=0;if(!alive)for(int i=0;i<HordePointBones.Length;i++){Vector3 p=root.InverseTransformPoint(rig.Bones[HordePointBones[i]].position);hordePoints[i*3]=-p.x;hordePoints[i*3+1]=p.y;hordePoints[i*3+2]=p.z;points++;}
                float floor=root.InverseTransformPoint(new Vector3(root.position.x,job.Floor,root.position.z)).y;
                int count=NativeMinionFx.Effects(job.Seed,job.Emerge,job.Death,floor,hordePoints,points,quality,hordeCubes);
                for(int i=0;i<count&&budget>0;i++,budget--)
                {
                    var c=hordeCubes[i];Vector3 at=root.TransformPoint(new Vector3(-(float)c.X,(float)c.Y,(float)c.Z));
                    draw.Cube(draw.Solid,at,root.rotation*SourceTurn(c.Ex,c.Ey,c.Ez),new Vector3((float)c.Sx,(float)c.Sy,(float)c.Sz),new Color((float)c.R,(float)c.G,(float)c.B,1).gamma,NativeFxDraw.Authored);
                }
                if(!job.Body||gone)continue;
                bool near=(root.position-eye).sqrMagnitude<LickRange*LickRange,head=HordeJointShown(job.Lost,CapturedRig.Head);
                // Shed licks off each tongue's tip, trailing as if they stayed where they were shed.
                if(near&&licks>0)
                {
                    double flare=NativeMinionFx.Flare(float.IsNaN(job.Emerge)?double.PositiveInfinity:job.Emerge,alive?double.PositiveInfinity:job.Death),extra=NativeMinionFx.TonguePhase(job.Id);
                    for(int k=0;k<NativeMinionFx.TongueCount(job.Variant);k++)
                    {
                        if(!NativeMinionFx.TongueMatrix(job.Variant,k,t,flare,extra,hordeLocal,out int joint,out _)||!HordeJointShown(job.Lost,joint))continue;
                        Vector3 tip=(rig.Bones[joint].localToWorldMatrix*FromSource(hordeLocal)).MultiplyPoint3x4(new Vector3(0,(float)NativeMinionFx.TipY,0));
                        for(int j=0;j<licks&&budget>0;j++)
                        {
                            if(!NativeMinionFx.Lick(job.Seed,k,j,t,-job.Velocity.x,job.Velocity.z,out double dx,out double dy,out double dz,out double ex,out double ey,out double ez,out double edge,out double r,out double g,out double b))continue;budget--;
                            Vector3 at=tip+new Vector3(-(float)dx,(float)dy,(float)dz),size=new Vector3((float)edge*.8f,(float)edge*1.7f,(float)edge*.8f);var turn=SourceTurn(ex,ey,ez);var color=new Color((float)r,(float)g,(float)b,1);
                            if(flat)draw.Cube(draw.Solid,at,turn,size,color);else draw.FlameCube(draw.Solid,at,turn,size,color,AshRaw,rim);
                        }
                    }
                }
                if(!head)continue;var headBone=rig.Bones[CapturedRig.Head].localToWorldMatrix;float headChar=alive?0:(float)NativeMinionFx.Charred(job.Seed,CapturedRig.Head,job.Death);
                // The cracked head's violet haze, breathing and dropping out with the crack's light; it dies with the char.
                if(NativeMinionFx.Cracked[job.Variant]&&headChar<1)
                {
                    var crack=NativeMinionFx.Crack;int o=job.Variant*3;NativeMinionFx.Haze(job.Phase,t,out double size,out double opacity);
                    Vector3 at=headBone.MultiplyPoint3x4(new Vector3(-(float)crack[o],(float)crack[o+1],(float)crack[o+2]+.03f));
                    draw.Sprite(draw.Effects,at,right,up,(float)size,Alpha(HordeViolet,opacity*(1-headChar)),NativeFxDraw.Authored+NativeFxDraw.Additive);
                }
                // hints.ts level 1 on the possessed add: the crown seam (the crack on a cracked head) and the halo pulse every ~4 s.
                if(job.Possessed&&alive)
                {
                    bool cracked=NativeMinionFx.Cracked[job.Variant];NativeMinionFx.Hint(cracked,NativeMinionFx.HintPulse(job.Phase,t),out double cx,out double cy,out double cz,out double sx,out double sy,out double sz,out double seam,out double halo,out double haloOpacity);
                    draw.Box(draw.Effects,headBone*Matrix4x4.TRS(new Vector3(-(float)cx,(float)cy,(float)cz),Quaternion.identity,new Vector3((float)sx,(float)sy,(float)sz)),Alpha(HordeViolet,seam),NativeFxDraw.Flat,NativeFxDraw.Authored+NativeFxDraw.Additive,Vector4.zero,Vector4.zero);
                    draw.Sprite(draw.Effects,headBone.MultiplyPoint3x4(new Vector3(0,.38f,0)),right,up,(float)halo,Alpha(HordeViolet,haloOpacity),NativeFxDraw.Authored+NativeFxDraw.Additive);
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
