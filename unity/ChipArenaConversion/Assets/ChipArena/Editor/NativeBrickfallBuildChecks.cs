using System;
using System.Linq;
using UnityEngine;
using UnityEditor.SceneManagement;
namespace ChipArena.UnityConversion.Editor
{
    public static partial class NativeBuildChecks
    {
        private static void CheckLegacyMapDefaults(ConversionContent data)
        {
            // This runs after the actual JsonUtility + supplemental-map loader, before any physics scene.
            var seats=Enumerable.Range(0,8).Select(i=>new NativeSeat{id=i+1,team=i%2}).ToArray();int count=0;
            foreach(var map in data.maps.Where(m=>!m.unity))
            {
                if(map.spawns!=null||map.bounds!=null||map.nav!=null||map.segments!=null||map.pilotBlocks!=null||map.hazards!=null)
                    throw new Exception("Legacy map retained supplemental inline defaults: "+map.id);
                foreach(bool teams in new[]{false,true})for(int i=0;i<seats.Length;i++)
                {
                    var spawn=NativeMapRules.Spawn(data,map,seats,i,teams);var expected=data.spawns[i];
                    if((spawn.pos-expected).Length>.001||Math.Abs(spawn.yaw-Math.Atan2(-expected.x,-expected.z)*180/Math.PI)>.001)
                        throw new Exception("Legacy JsonUtility spawn/yaw changed: "+map.id+" seat "+i);count++;
                }
                if((NativeMapRules.Clamp(map,new V3(100,4,-100))-new V3(31,4,-27)).Length>.001||(NativeMapRules.Clamp(map,new V3(-100,4,100),true)-new V3(-28,4,24)).Length>.001)
                    throw new Exception("Legacy JsonUtility player/bot bounds changed: "+map.id);
            }
            Debug.Log("PASS legacy JsonUtility map defaults: "+count+" FFA/team spawns, yaw, bounds and legacy navigation");
        }
        private static void CheckBrickfallReplica(NativeArena host)
        {
            var scene=EditorSceneManager.NewPreviewScene();NativeArena guest=null;
            try
            {
                guest=new NativeArena(host.Content,new NativeMatch(host.Content),host.MapId){Replica=true,ReplicaScene=scene};
                var intact=NativeFrameCodec.Clone(host.NetworkFrame());guest.ApplyFrame(intact,host.LocalId);int statics=guest.StaticBlocks.Count;
                var falling=NativeFrameCodec.Clone(intact);falling.time=12;foreach(var s in falling.segments.Take(3)){s.state=2;s.t0=11;}
                if(!falling.Valid(host.Content))throw new Exception("Brickfall fall frame refused");guest.ApplyFrame(falling,host.LocalId);
                foreach(var s in host.Map.segments.Where(s=>falling.segments.Any(f=>f.id==s.id&&f.state==2)))foreach(var id in s.blocks)
                {var body=guest.Props.FirstOrDefault(p=>p.Definition.id==id);if(body==null)continue;var expected=NativeStructureRules.Pose(s,body.Definition,11,12);if(Vector3.Distance(body.transform.position,NativeArena.Vec(expected.position))>.001)throw new Exception("Brickfall guest pose mismatch "+id);}
                var rubble=NativeFrameCodec.Clone(falling);rubble.time=20;foreach(var s in rubble.segments){s.state=3;s.t0=11;}rubble.props=rubble.props.Where(p=>!host.Map.blocks.Any(b=>b.id==p.id&&(!string.IsNullOrEmpty(b.segment)||host.Map.segments.Any(s=>s.supports.Any(x=>x.member==b.id))))).ToArray();guest.ApplyFrame(rubble,host.LocalId);
                if(guest.StaticBlocks.Count!=statics+host.Map.segments.Sum(s=>s.rubble.Length))throw new Exception("Brickfall rubble static swap");
                guest.ApplyFrame(NativeFrameCodec.Clone(intact),host.LocalId);if(guest.StaticBlocks.Count!=statics||guest.Props.Count(p=>p.Definition.role=="carried")!=host.Map.blocks.Count(b=>b.role=="carried"))throw new Exception("Brickfall replay rewind lost static/carried blocks");
                guest.ApplyFrame(NativeFrameCodec.Clone(falling),host.LocalId);guest.PoseStructures(12.25f);
                foreach(var s in host.Map.segments.Where(s=>falling.segments.Any(f=>f.id==s.id&&f.state==2)))foreach(var id in s.blocks)
                {var body=guest.Props.FirstOrDefault(p=>p.Definition.id==id);if(body==null)continue;var expected=NativeStructureRules.Pose(s,body.Definition,11,12.25f);if(Vector3.Distance(body.transform.position,NativeArena.Vec(expected.position))>.001)throw new Exception("Brickfall killcam between-frame pose mismatch "+id);}
                Debug.Log("PASS Brickfall real JsonUtility guest/killcam pose parity, static split, rubble swap and rewind");
            }
            finally{guest?.Dispose();EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
