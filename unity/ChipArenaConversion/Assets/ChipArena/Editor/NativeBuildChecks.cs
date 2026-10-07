using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace ChipArena.UnityConversion.Editor
{
    public static class NativeBuildChecks
    {
        private static void CheckGlow(NativeArena arena,NativeMatch match)
        {
            var host=new GameObject("Glow lifecycle check");host.transform.SetParent(arena.Root.transform);var camera=host.AddComponent<Camera>();camera.enabled=false;
            var rigs=new System.Collections.Generic.Dictionary<int,CapturedAnimation.Player>();
            using(var glow=new NativeCombatGlow(host.transform))
            {
                var prop=arena.Props[0];
                for(int n=1;n<=200;n++)arena.Events.Add(new ArenaEvent{Id=n,Kind=EventKind.Impact,Time=arena.Time,SurfaceId=prop.Definition.id,Position=NativeArena.Vec(prop.transform.position+Vector3.up),Normal=new V3(0,1,0),Color=0x58cfff});
                arena.Bullets.Add(new NativeBullet{Id=500,Spec="pistol",Position=Vector3.up,Radius=.045f});
                arena.AbilityShots.Add(new NativeAbilityShot{Id=501,Owner=arena.Local,Position=Vector3.up*2,Radius=.2f});
                void Sample()=>glow.Render(arena,camera,null,false,rigs,null,null,false);
                Sample();if(glow.LiveMarks!=200||glow.LiveTraces!=2)throw new Exception("Combat glow dropped a live burst or ability trace");
                if(host.GetComponentsInChildren<Light>().Length!=7)throw new Exception("Combat lights must use exactly seven pooled objects");
                arena.Bullets.Clear();arena.AbilityShots.Clear();match.Tick(.2);Sample();
                if(glow.LiveMarks!=200||glow.LiveTraces!=0)throw new Exception("Glow/trail lifetimes differ from the source");
                for(int i=0;i<4;i++)match.Tick(.16);Sample();if(glow.LiveMarks!=200)throw new Exception("Impact burst was evicted before natural expiry");
                match.Tick(.02);Sample();if(glow.LiveMarks!=0)throw new Exception("Expired impact marks leaked");
                glow.Quality=0;Sample();foreach(var light in host.GetComponentsInChildren<Light>())if(light.enabled)throw new Exception("Off leaves a combat light enabled");
                glow.Clear();if(glow.LiveMarks!=0||glow.LiveTraces!=0)throw new Exception("Glow reset leaked presentation state");
            }
            arena.Events.Clear();CapturedAnimation.Release(host);
            Debug.Log("PASS native glow lifecycle: 200 simultaneous impacts, primary/ability traces, seven pooled lights, natural expiry and reset (draw submission skipped)");
        }
        public static void Run()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Run build checks after leaving Play mode.");
            var asset=Resources.Load<TextAsset>("GameContent");if(asset==null)throw new Exception("Missing native GameContent asset");
            var data=JsonUtility.FromJson<ConversionContent>(asset.text);
            var fixtures=JsonUtility.FromJson<ParityFixtures>(File.ReadAllText(Path.Combine(Application.dataPath,"../Tests/source-parity.json")));
            foreach(var test in NativeParityChecks.Run(data,fixtures))Debug.Log("PASS "+test);
            var presentation=JsonUtility.FromJson<PresentationFixtures>(File.ReadAllText(Path.Combine(Application.dataPath,"../Tests/presentation-parity.json")));
            var melee=JsonUtility.FromJson<MeleeFixtures>(File.ReadAllText(Path.Combine(Application.dataPath,"../Tests/melee-parity.json")));
            foreach(var test in NativeMeleeChecks.Run(data,melee))Debug.Log("PASS "+test);
            foreach(var test in NativeMotionChecks.Run())Debug.Log("PASS "+test);
            foreach(var test in NativeVisualChecks.Run(data))Debug.Log("PASS "+test);
            foreach(var test in NativePresentationChecks.Run(data,presentation))Debug.Log("PASS "+test);
            var active=SceneManager.GetActiveScene();Vector3 gravity=Physics.gravity;int pads=0;
            try
            {
                foreach(var map in data.maps)
                {
                    // Batch builds run in Edit mode: SceneManager.CreateScene is Play-mode-only.
                    // A preview scene also avoids simulating the user's open scene.
                    var scene=EditorSceneManager.NewPreviewScene();
                    NativeArena arena=null;
                    try
                    {
                        var physics=scene.GetPhysicsScene();
                        if(!scene.IsValid()||!EditorSceneManager.IsPreviewScene(scene)||!physics.IsValid()||physics==Physics.defaultPhysicsScene)
                            throw new Exception("Build check requires a preview scene with independent 3D physics: "+map.id);
                        var match=new NativeMatch(data);match.Seats.Add(new NativeSeat{id=1,name="Check A",spec="pistol"});match.Seats.Add(new NativeSeat{id=2,name="Check B",spec="sword",team=1});
                        arena=new NativeArena(data,match,map.id);arena.Reset();SceneManager.MoveGameObjectToScene(arena.Root,scene);
                        // Prove Edit-mode simulation advances; a no-op must not pass settling checks.
                        var probe=new GameObject("Physics advance check");probe.transform.SetParent(arena.Root.transform);
                        probe.transform.position=new Vector3(1000,100,1000);var probeBody=probe.AddComponent<Rigidbody>();
                        Physics.SyncTransforms();
                        for(int i=0;i<90;i++)physics.Simulate(1f/60);
                        if(!MathUtil.Finite(probeBody.position.y)||probeBody.position.y>=99)
                            throw new Exception("Preview physics did not advance the falling probe: "+map.id);
                        foreach(var spawn in data.spawns)
                        {
                            if(!physics.Raycast(NativeArena.Vec(spawn),Vector3.down,out var hit,4,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))throw new Exception("Native spawn has no floor: "+map.id+" / "+spawn.x+","+spawn.z);
                            if(hit.normal.y<.5f)throw new Exception("Native spawn floor is too steep: "+map.id);pads++;
                        }
                        foreach(var pilot in arena.Pilots)if(!MathUtil.Finite(pilot.Body.position.y)||pilot.Body.position.y<-.5f)throw new Exception("Native capsule fell through the map: "+map.id);
                        if(arena.Props.Count!=map.blocks.Length)throw new Exception("Native map lost blocks: "+map.id);
                        if(map==data.maps[0])
                        {
                            var shown=arena.Snapshot(true);var pilot=arena.Local;
                            if(ReferenceEquals(shown.Actors[0],pilot.Pose)||Vector3.Distance(NativeArena.Vec(shown.Actors[0].Position),pilot.Body.transform.position-Vector3.up*.865f)>.00001f)
                                throw new Exception("Render snapshot must copy the interpolated transform");
                            float hp=pilot.Pose.Hp;shown.Actors[0].Hp=0;if(pilot.Pose.Hp!=hp)throw new Exception("Render snapshot changed authority");
                            CheckGlow(arena,match);arena.ValidateMelee();
                        }
                        Debug.Log("PASS native map "+map.id+": isolated Edit-mode physics, 8 spawn floors, capsule settling");
                    }
                    finally
                    {
                        try{arena?.Dispose();}
                        finally{if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);}
                    }
                }
                Debug.Log("PASS native Unity physics: 15 source layouts / "+pads+" spawn floors and dynamic capsule settling");
            }
            finally{Physics.gravity=gravity;if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);}
        }
    }
}
