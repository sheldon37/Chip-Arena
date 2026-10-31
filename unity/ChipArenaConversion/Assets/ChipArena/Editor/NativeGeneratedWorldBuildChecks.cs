using System;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ChipArena.UnityConversion.Editor
{
    public static partial class NativeBuildChecks
    {
        // Runs in the real builder, not the engine-free semantic compilation gate.
        private static void CheckGeneratedDeploymentWorld(ConversionContent content,ExpeditionDeploymentRoom room)
        {
            var world=NativeExpeditionGeneratedWorld.Build(room);
            var copy=JsonUtility.FromJson<ConversionContent>(JsonUtility.ToJson(content));
            copy.contentRevision=content.contentRevision;
            copy.maps=copy.maps.Concat(new[]{world.Map}).ToArray();foreach(var m in copy.maps)m.unity=true;
            using(var runtime=new NativeExpeditionAreaRuntime("generated-check",copy,world.Map.id,match=>
            {
                match.Cooperative=match.Teams=true;match.ShowIntros=false;
                match.Seats.Add(new NativeSeat{id=1,name="Generated A",spec="pistol",team=0});
                match.Seats.Add(new NativeSeat{id=2,name="Generated B",spec="pistol",team=0});
            },_=>EditorSceneManager.NewPreviewScene(),s=>EditorSceneManager.ClosePreviewScene(s)))
            {
                var arena=runtime.Arena;Physics.SyncTransforms();
                var points=world.Area.slots.Concat(world.Area.bossPoints).Concat(world.Area.doors.SelectMany(d=>d.returns.Concat(new[]{d.approach})))
                    .Concat(world.Area.interactions.Select(i=>new ExpeditionPoint{x=i.x,y=i.y,z=i.z}));
                foreach(var p in points)
                    if(!arena.ExpeditionSupported(p.Position,NativeHordeKinds.HulkRadius,NativeHordeKinds.HulkHeight)||
                       !arena.ExpeditionReachable(world.Area.slots[0].Position,p.Position,NativeHordeKinds.HulkRadius,NativeHordeKinds.HulkHeight))
                        throw new Exception("Generated world native placement/route: "+p.x+", "+p.z);
                foreach(var spawn in world.Area.spawns)
                    if(!arena.ExpeditionSupported(spawn.entry.@exit.Position(),.45f,1.8f))throw new Exception("Generated spawn native collision: "+spawn.entry.id);
                foreach(var door in world.Area.doors)for(int i=0;i<8;i++)
                {
                    double angle=i*Math.PI/4;var p=new V3(door.join.x+door.join.r*(float)Math.Cos(angle),0,door.join.z+door.join.r*(float)Math.Sin(angle));
                    if(!arena.ExpeditionSupported(p,.45f,1.8f))throw new Exception("Generated join circle native collision: "+door.anchor);
                }
            }
            Debug.Log("PASS generated deployment blockout: native collision, Hulk routes and join circles (not live deployment)");
        }
    }
}
