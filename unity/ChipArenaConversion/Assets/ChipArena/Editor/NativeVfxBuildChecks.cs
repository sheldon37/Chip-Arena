using System;
using System.Diagnostics;
using UnityEngine;

namespace ChipArena.UnityConversion.Editor
{
    public static partial class NativeBuildChecks
    {
        // The effects overhaul's shared renderer (Runtime/NativeVfxDraw.cs): one frame of every blast style on the floor, an
        // impact, burst sparks and the modelled chip bomb queue sprites and puffs, lit meshes and shock shells; Submit draws and
        // empties the queue; the smoke atlas built off the main thread is uploaded; Dispose releases. No frame is rendered.
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
                    for(int i=0;i<6;i++)sim.Update(1.0/60);
                    var sparks=new NativeBurstPool();sparks.Spawn(0,1,0,0xffd45f,12,3,1);sparks.Update(1.0/60);
                    var field=new NativeBlastField{Id=1,Kind="bomb",Fx="sticky-bomb",X=0,Y=.95,Z=3,Radius=3,Born=0,Until=3};
                    if(!NativeFieldRules.Bomb(field,1,out var pose))throw new Exception("A sticky bomb field has no bomb pose");
                    vfx.Begin();vfx.Blasts(sim);vfx.Sparks(sparks);vfx.Bomb(pose,0xffa141);
                    if(vfx.Billboards==0||vfx.LitVertices<NativeVfxMeshes.Bomb.VertexCount||vfx.ShockVertices==0||sim.Shocks.Count==0)
                        throw new Exception($"Effects renderer queued {vfx.Billboards} billboards, {vfx.LitVertices} lit and {vfx.ShockVertices} shell vertices for {sim.Shocks.Count} fronts");
                    vfx.Submit(camera);if(vfx.Billboards!=0||vfx.LitVertices!=0||vfx.ShockVertices!=0)throw new Exception("Effects renderer Submit kept its queue");
                    var clock=Stopwatch.StartNew();while(!vfx.AtlasReady&&clock.ElapsedMilliseconds<15000){System.Threading.Thread.Sleep(10);vfx.Submit(camera);}
                    if(!vfx.AtlasReady)throw new Exception("The smoke atlas was not built and uploaded within 15 s");
                }
            }
            finally{CapturedAnimation.Release(host);}
            UnityEngine.Debug.Log("PASS native effects renderer: every blast style, an impact, burst sparks and the chip bomb queue billboards, lit meshes and shock shells; submit empties the queue; the off-thread smoke atlas uploads; dispose releases (no rendered frame)");
        }
    }
}
