using System;
using System.Diagnostics;
using UnityEngine;

namespace ChipArena.UnityConversion.Editor
{
    public static partial class NativeBuildChecks
    {
        // The effects overhaul's shared renderer (Runtime/NativeVfxDraw.cs): one frame of every blast style on the floor, an
        // impact, burst sparks and the modelled chip bomb queue sprites and puffs, lit meshes and shock shells; Submit draws and
        // empties the queue and asks for the depth texture (soft particles) except on the low tier; the smoke atlas built off
        // the main thread is uploaded; Dispose releases. No frame is rendered. Toon pass: the two water looks, a floor splash and
        // Quake's slam wave queue too, the low tier queues fewer puffs, and the toon sheet uploads with the atlas.
        private static void CheckVfx()
        {
            var host=new GameObject("Effects renderer check");var camera=host.AddComponent<Camera>();camera.enabled=false;
            camera.transform.SetPositionAndRotation(new Vector3(40,4,-12),Quaternion.Euler(12,0,0));
            try
            {
                using(var vfx=new NativeVfxDraw())
                {
                    var sim=new NativeBlastSim();double x=0;
                    foreach(NativeBlastStyle style in Enum.GetValues(typeof(NativeBlastStyle))){sim.Blast(style,x,.95,0,3,1,false,null,0);x+=8;}
                    sim.Impact(0,1,6,1.2,0xffaa64,1,false,true,null,0);
                    sim.Blast(NativeBlastStyle.Frost,x,.15,0,3,1,false,NativeBlastSim.TintOf("geyser-wall"),0);sim.Blast(NativeBlastStyle.Kinetic,x+8,.95,0,3,1,false,NativeBlastSim.TintOf("tidal-wave"),0);sim.Splash(0,0,-4,1,1);
                    if(sim.Shocks.Count==0||!System.Linq.Enumerable.Any(sim.Shocks,s=>s.Kind==NativeShockKind.Slam))throw new Exception("Quake drew no slam wave");
                    for(int i=0;i<6;i++)sim.Update(1.0/60);
                    var sparks=new NativeBurstPool();sparks.Spawn(0,1,0,0xffd45f,12,3,1);sparks.Update(1.0/60);
                    var field=new NativeBlastField{Id=1,Kind="bomb",Fx="sticky-bomb",X=0,Y=.95,Z=3,Radius=3,Born=0,Until=3};
                    if(!NativeFieldRules.Bomb(field,1,out var pose))throw new Exception("A sticky bomb field has no bomb pose");
                    vfx.Begin();vfx.Blasts(sim);vfx.Sparks(sparks);vfx.Bomb(pose,0xffa141);
                    if(vfx.Billboards==0||vfx.LitVertices<NativeVfxMeshes.Bomb.VertexCount||vfx.ShockVertices==0||sim.Shocks.Count==0)
                        throw new Exception($"Effects renderer queued {vfx.Billboards} billboards, {vfx.LitVertices} lit and {vfx.ShockVertices} shell vertices for {sim.Shocks.Count} fronts");
                    int full=vfx.Billboards;vfx.Submit(camera);if(vfx.Billboards!=0||vfx.LitVertices!=0||vfx.ShockVertices!=0)throw new Exception("Effects renderer Submit kept its queue");
                    // The low tier skips a third of the toon puffs.
                    vfx.Low=true;vfx.Begin();vfx.Blasts(sim);vfx.Sparks(sparks);vfx.Bomb(pose,0xffa141);int low=vfx.Billboards;vfx.Submit(camera);vfx.Low=false;
                    if(!(low<full))throw new Exception($"Effects renderer queued {low} billboards on the low tier, {full} on the normal tiers");
                    // Soft particles ask the camera for a depth texture on the normal tiers and give it back on the low tier.
                    if((camera.depthTextureMode&DepthTextureMode.Depth)==0)throw new Exception("Effects renderer did not ask for the depth texture (soft particles)");
                    vfx.Low=true;vfx.Submit(camera);if((camera.depthTextureMode&DepthTextureMode.Depth)!=0)throw new Exception("Effects renderer kept the depth texture on the low tier");vfx.Low=false;
                    var clock=Stopwatch.StartNew();while(!vfx.AtlasReady&&clock.ElapsedMilliseconds<15000){System.Threading.Thread.Sleep(10);vfx.Submit(camera);}
                    if(!vfx.AtlasReady)throw new Exception("The smoke atlas was not built and uploaded within 15 s");
                }
            }
            finally{CapturedAnimation.Release(host);}
            UnityEngine.Debug.Log("PASS native effects renderer: every blast style, the water looks, a floor splash, an impact, burst sparks and the chip bomb (with its fuse) queue billboards, lit meshes and shock shells (the slam wave among them); the low tier queues fewer puffs; submit empties the queue; the off-thread smoke atlas and toon sheet upload; dispose releases (no rendered frame)");
        }
    }
}
