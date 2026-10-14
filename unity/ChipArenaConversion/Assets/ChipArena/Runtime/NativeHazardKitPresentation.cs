using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The hazard kit's presentation (docs/HAZARD-KIT.md): every piece of the arena's kit scene (NativeHazardKitWorld: the
    /// map's presentation-only placements, or the DEV preview gallery in the Dev Sandbox) drawn in SourceWorld, so the live
    /// arena and the killcam replay both show it:
    ///  - the pieces' boxes as NativeBlockLit instances (one material, batches of 500, shadowless detail);
    ///  - their effects (beams, flashes, halos, laser sights, steam, sparks, smoke, blur, blood) through one NativeFxDraw;
    ///  - the turret rounds through the guns' own renderer (NativeWeaponFxShapes: muzzle star, tracer, impact);
    ///  - their light on the walls: the strongest kit lights at the camera (Core NativeKitLightPool, 3 on full effects, 2
    ///    reduced, 1 on the low tier) merged into the eight practical slots (NativeLighting.KitLights).
    /// Settings · Alarm lights (Full, Reduced, Calm; Reduced effects forces at least Reduced) sets how hard lamps flash.
    /// Nothing here writes sim data, collides, damages or enters the network frame. Allocation free after the first frame.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        /// <summary>DEV preview (Sandbox only): the hazard kit gallery in front of you, and its music lock (the boss song plays
        /// locally and the alarms keep its beat).</summary>
        public bool HazardKitGallery,HazardKitMusic;
        /// <summary>How hard kit lamps flash (ConversionGame from Settings · Alarm lights and Reduced effects).</summary>
        public NativeKitFlash KitFlash=NativeKitFlash.Full;
        private Material kitMaterial;private Mesh kitMesh;private bool kitFailed;private NativeFxDraw kitDraw;private NativeWeaponFxDraw kitWeapon;private MaterialPropertyBlock kitOne;
        private readonly List<MetroBatch> kitBatches=new List<MetroBatch>();private int kitUsed;private readonly NativeKitLightPool kitLights=new NativeKitLightPool();private bool kitLightsSent;private float kitLast=-1;

        // SourceWorld: the arena or the killcam replay arena.
        private void HazardKitWorld(NativeArena arena)
        {
            HazardKitGalleryState(arena);NativeHazardKitWorld.Flash=KitFlash;
            float now=PresentationClock(arena);var scene=NativeHazardKitWorld.For(arena,now);
            if(scene==null||!EnsureHazardKit()){if(kitLightsSent){NativeLighting.KitLights(null,null,0,Vector3.zero);kitLightsSent=false;kitLights.Clear();}return;}
            var eye=WorldEye;Vector3 e=eye!=null?eye.transform.position:Vector3.zero,r=eye!=null?eye.transform.right:Vector3.right,u=eye!=null?eye.transform.up:Vector3.up;
            scene.Build(e.x,e.y,e.z,r.x,r.y,r.z,u.x,u.y,u.z,Mathf.Clamp01(EffectsQuality),LowGraphics);
            DrawKitParts(scene);DrawKitFx(scene,now,eye);DrawKitRounds(scene,now,eye);
            float dt=kitLast<0?0:Mathf.Clamp(now-kitLast,0,.1f);kitLast=now;kitLights.Budget=LowGraphics?1:EffectsQuality<.5f?2:NativeKitLightPool.MaxBudget;
            kitLights.Update(scene,e.x,e.y,e.z,dt);NativeLighting.KitLights(kitLights.Pos,kitLights.Rgb,kitLights.Count,e);kitLightsSent=true;
        }
        // The DEV gallery opens where you stand, facing where you face, and stays put until it is turned off.
        private void HazardKitGalleryState(NativeArena arena)
        {
            if(arena.ReplicaScene.IsValid())return;bool want=HazardKitGallery&&arena.Sandbox;
            if(want&&double.IsNaN(NativeHazardKitWorld.GalleryAt)&&arena.Local?.Body!=null)
            {NativeHazardKitWorld.GalleryOrigin=arena.Local.Body.position-Vector3.up*.85f;NativeHazardKitWorld.GalleryYaw=arena.Local.Yaw;NativeHazardKitWorld.GalleryAt=arena.Time;}
            if(!want){NativeHazardKitWorld.GalleryAt=double.NaN;NativeHazardKitWorld.GalleryMusicAt=double.NaN;}
            bool music=want&&HazardKitMusic;if(music&&double.IsNaN(NativeHazardKitWorld.GalleryMusicAt))NativeHazardKitWorld.GalleryMusicAt=arena.Time;if(!music)NativeHazardKitWorld.GalleryMusicAt=double.NaN;
            NativeHazardKitWorld.Gallery=want;NativeHazardKitWorld.GalleryMusic=music;
        }
        private bool EnsureHazardKit()
        {
            if(kitMaterial!=null)return true;if(kitFailed)return false;
            try
            {
                var shader=Resources.Load<Shader>("NativeBlockLit");if(shader==null)throw new InvalidOperationException("NativeBlockLit shader is missing");
                kitMaterial=new Material(shader){name="Hazard kit",enableInstancing=true};kitMesh=MetroMeshOf(NativeBlockLooks.Build(0));kitDraw=new NativeFxDraw();kitWeapon=new NativeWeaponFxDraw();return true;
            }
            catch(Exception x){Debug.LogWarning("Hazard kit presentation disabled: "+x.Message);DisposeHazardKit();kitFailed=true;return false;}
        }
        private void DrawKitParts(NativeKitScene scene)
        {
            kitUsed=0;var parts=scene.Parts;bool low=LowGraphics;
            for(int i=0;i<scene.PartCount;i++)
            {
                ref var p=ref parts[i];if(p.Detail&&low)continue;
                if(kitUsed==0||kitBatches[kitUsed-1].Count==NativeBlockLooks.Chunk){if(kitUsed==kitBatches.Count)kitBatches.Add(new MetroBatch());kitBatches[kitUsed].Count=0;kitUsed++;}
                Put(kitBatches[kitUsed-1],Matrix4x4.TRS(new Vector3(p.X,p.Y,p.Z),new Quaternion(p.Qx,p.Qy,p.Qz,p.Qw),Vector3.one),new Vector4(p.Hx,p.Hy,p.Hz,NativeFactoryKit.Bevel(p.Hx,p.Hy,p.Hz)),
                    new Vector4(p.X,p.Y,p.Z,0),MetroColor(p.R,p.G,p.B,p.Level),new Vector4(p.Look,1,.5f,p.Flags));
            }
            for(int i=0;i<kitUsed;i++)
            {
                var b=kitBatches[i];if(b.Count==0)continue;MetroUpload(b);
                if(UsesInstancing){Graphics.DrawMeshInstanced(kitMesh,0,kitMaterial,b.M,b.Count,b.Block,ShadowCastingMode.Off,true,0,null);continue;}
                var one=kitOne??=new MaterialPropertyBlock();
                for(int k=0;k<b.Count;k++){one.Clear();one.SetVector(MetroSizeId,b.Size[k]);one.SetVector(MetroRestId,b.Rest[k]);one.SetVector(MetroColorId,b.Color[k]);one.SetVector(MetroLookId,b.Look[k]);Graphics.DrawMesh(kitMesh,b.M[k],kitMaterial,0,null,0,one,ShadowCastingMode.Off,true);}
            }
        }
        private void DrawKitFx(NativeKitScene scene,float time,Camera camera)
        {
            if(camera==null)return;var q=scene.Quads;kitDraw.Clear();
            for(int i=0;i<q.Count;i++)
            {
                var layer=q.Layers[i]==0?kitDraw.Solid:kitDraw.Effects;int o=i*12,k=i*4;float flags=NativeFxDraw.Authored+(q.Additive[i]!=0?NativeFxDraw.Additive:0),mode=q.Modes[i];
                // Linear colours go in as authored sRGB (NativeSirenFx linearises them back), as the Ward Zero effects do.
                var color=new Color(q.Colors[k],q.Colors[k+1],q.Colors[k+2],q.Colors[k+3]).gamma;int a=layer.Count;
                for(int v=0;v<4;v++)layer.Vertex(new Vector3(q.Corners[o+v*3],q.Corners[o+v*3+1],q.Corners[o+v*3+2]),Vector3.up,color,new Vector4(v==1||v==2?1:-1,v>=2?1:-1,mode,flags),Vector4.zero,Vector4.zero);
                layer.Triangle(a,a+1,a+2);layer.Triangle(a,a+2,a+3);
            }
            kitDraw.Draw(camera,null,time);
        }
        // Turret rounds as the guns draw theirs: the machine gun's muzzle star, a tracer flying at 90 m/s, the impact at its end.
        private void DrawKitRounds(NativeKitScene scene,float now,Camera camera)
        {
            if(camera==null||scene.ShotCount==0)return;kitWeapon.Low=LowGraphics;kitWeapon.Begin();NativeMuzzleFx.Look("machinegun",out var look);float q=Mathf.Clamp01(EffectsQuality);bool low=LowGraphics;
            int shards=NativeImpactFx.ShardCount(q,low),sparks=NativeImpactFx.SparkCount(q,low);var tint=Color.Lerp(NativeWeaponFxShapes.Lin(0xffedb8),Color.white,.55f);
            for(int i=0;i<scene.ShotCount;i++)
            {
                var s=scene.Shots[i];double age=now-s.At;long seed=NativeWeaponFx.Seed(s.At,9000+s.Piece);
                Vector3 m=NativeArena.Vec(s.Muzzle),f=NativeArena.Vec(s.Forward),up=NativeArena.Vec(s.Up),side=NativeArena.Vec(s.Side),hit=NativeArena.Vec(s.Hit);
                NativeWeaponFxShapes.Muzzle(kitWeapon,false,m,side,up,f,1.2f,look,seed,age,q,low,1);
                var path=hit-m;float dist=path.magnitude;if(dist<.05f)continue;var dir=path/dist;const float Speed=90;float travelled=(float)age*Speed;
                if(travelled<dist)NativeWeaponFxShapes.Tracer(kitWeapon,m+dir*travelled,dir*Speed,travelled,"machinegun");
                else NativeWeaponFxShapes.Impact(kitWeapon,hit,-dir,seed,tint,age-dist/Speed,shards,sparks);
            }
            kitWeapon.Submit(camera,null);
        }
        private void DisposeHazardKit()
        {
            CapturedAnimation.Release(kitMesh);CapturedAnimation.Release(kitMaterial);kitMesh=null;kitMaterial=null;kitDraw?.Dispose();kitDraw=null;kitWeapon?.Dispose();kitWeapon=null;kitBatches.Clear();kitLights.Clear();kitLast=-1;
            if(kitLightsSent){NativeLighting.KitLights(null,null,0,Vector3.zero);kitLightsSent=false;}
        }
    }
}
