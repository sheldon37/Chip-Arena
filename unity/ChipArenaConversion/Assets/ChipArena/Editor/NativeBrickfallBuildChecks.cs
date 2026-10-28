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
            if(data.maps.Any(m=>!m.unity)||data.maps.Length!=8)throw new Exception("Legacy map survived Unity catalog filtering");
            if(!data.maps.Where(m=>m.rotation).Select(m=>m.id).OrderBy(x=>x).SequenceEqual(new[]{"brickfall","cybercity","factory"}))throw new Exception("Unity rotation differs");
            foreach(string id in new[]{"moonlit","foundry","unknown",null})
            {var prefs=JsonUtility.FromJson<ConversionPreferences>(JsonUtility.ToJson(new ConversionPreferences{map=id}));if(NativeMapRules.Selection(data,prefs.map)!=NativeMapRules.DefaultArena)throw new Exception("Saved map migration failed");}
            foreach(var map in data.maps)NativeMapRules.Validate(map);
            Debug.Log("PASS real JsonUtility Unity catalog, modern rotation, saved-map migration and authored spawn tables");
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
