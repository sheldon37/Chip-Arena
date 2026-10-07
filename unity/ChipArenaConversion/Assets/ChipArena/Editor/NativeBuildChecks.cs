using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace ChipArena.UnityConversion.Editor
{
    public static class NativeBuildChecks
    {
        public static void Run()
        {
            var asset=Resources.Load<TextAsset>("GameContent");if(asset==null)throw new Exception("Missing native GameContent asset");
            var data=JsonUtility.FromJson<ConversionContent>(asset.text);
            var fixtures=JsonUtility.FromJson<ParityFixtures>(File.ReadAllText(Path.Combine(Application.dataPath,"../Tests/source-parity.json")));
            foreach(var test in NativeParityChecks.Run(data,fixtures))Debug.Log("PASS "+test);
            var active=SceneManager.GetActiveScene();Vector3 gravity=Physics.gravity;int pads=0;
            try
            {
                foreach(var map in data.maps)
                {
                    var scene=SceneManager.CreateScene("Conversion geometry check "+map.id,new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                    NativeArena arena=null;
                    try
                    {
                        var match=new NativeMatch(data);match.Seats.Add(new NativeSeat{id=1,name="Check A",spec="pistol"});match.Seats.Add(new NativeSeat{id=2,name="Check B",spec="sword",team=1});
                        arena=new NativeArena(data,match,map.id);arena.Reset();SceneManager.MoveGameObjectToScene(arena.Root,scene);
                        var physics=scene.GetPhysicsScene();
                        for(int i=0;i<90;i++)physics.Simulate(1f/60);
                        foreach(var spawn in data.spawns)
                        {
                            if(!physics.Raycast(NativeArena.Vec(spawn),Vector3.down,out var hit,4,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))throw new Exception("Native spawn has no floor: "+map.id+" / "+spawn.x+","+spawn.z);
                            if(hit.normal.y<.5f)throw new Exception("Native spawn floor is too steep: "+map.id);pads++;
                        }
                        foreach(var pilot in arena.Pilots)if(!MathUtil.Finite(pilot.Body.position.y)||pilot.Body.position.y<-.5f)throw new Exception("Native capsule fell through the map: "+map.id);
                        if(arena.Props.Count!=map.blocks.Length)throw new Exception("Native map lost blocks: "+map.id);
                    }
                    finally{arena?.Dispose();EditorSceneManager.CloseScene(scene,true);}
                }
                Debug.Log("PASS native Unity physics: 15 source layouts / "+pads+" spawn floors and dynamic capsule settling");
            }
            finally{Physics.gravity=gravity;if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);}
        }
    }
}
