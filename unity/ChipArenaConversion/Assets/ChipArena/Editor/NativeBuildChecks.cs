using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace ChipArena.UnityConversion.Editor
{
    public static partial class NativeBuildChecks
    {
        private static void CheckPresentation(NativeArena arena)
        {
            // Exercise the component creation/startup path that pure combat checks never instantiate.
            var errors=new System.Collections.Generic.List<string>();
            void Capture(string message,string trace,LogType type)
            {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+trace);}
            Application.logMessageReceived+=Capture;
            try
            {
                for(int cycle=0;cycle<2;cycle++)
                {
                    var host=new GameObject("Presentation startup check");host.transform.SetParent(arena.Root.transform);
                    try
                    {
                        var presentation=host.AddComponent<ArenaPresentation>();presentation.Initialize(false);
                        presentation.ResetPresentation();presentation.ResetPresentation();
                        if(host.GetComponentsInChildren<AudioSource>(true).Length!=25||host.GetComponentsInChildren<ParticleSystem>(true).Length!=1||host.GetComponentsInChildren<Light>(true).Length!=7+NativeBlastSim.LightCapacity||presentation.ParticleCount!=0)
                            throw new Exception("Presentation startup did not initialize/reset its audio, particles and light pool");
                    }
                    finally{CapturedAnimation.Release(host);}
                }
            }
            finally{Application.logMessageReceived-=Capture;}
            if(errors.Count>0)throw new Exception("Presentation startup logged Unity errors: "+string.Join("\n",errors));
            Debug.Log("PASS native presentation startup: component construction, initialization, repeated reset and teardown (two cycles; no rendered frame)");
        }
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
                arena.MeleeShots.Add(new NativeMeleeShot{Id=502,Original=arena.Local,Owner=arena.Local,Shield=true,Position=Vector3.up*3,Radius=.28f});
                void Sample()=>glow.Render(arena,camera,null,false,rigs,null,null,false);
                Sample();if(glow.LiveMarks!=200||glow.LiveTraces!=3)throw new Exception("Combat glow dropped a live burst or ability trace");
                if(host.GetComponentsInChildren<Light>().Length!=7)throw new Exception("Combat lights must use exactly seven pooled objects");
                arena.Bullets.Clear();arena.AbilityShots.Clear();arena.MeleeShots.Clear();match.Tick(.2);Sample();
                if(glow.LiveMarks!=200||glow.LiveTraces!=0)throw new Exception("Glow/trail lifetimes differ from the source");
                for(int i=0;i<4;i++)match.Tick(.16);Sample();if(glow.LiveMarks!=200)throw new Exception("Impact burst was evicted before natural expiry");
                match.Tick(.02);Sample();if(glow.LiveMarks!=0)throw new Exception("Expired impact marks leaked");
                glow.Quality=0;Sample();foreach(var light in host.GetComponentsInChildren<Light>())if(light.enabled)throw new Exception("Off leaves a combat light enabled");
                glow.Clear();if(glow.LiveMarks!=0||glow.LiveTraces!=0)throw new Exception("Glow reset leaked presentation state");
            }
            arena.Events.Clear();CapturedAnimation.Release(host);
            Debug.Log("PASS native glow lifecycle: 200 simultaneous impacts, primary/ability/thrown-shield traces, seven pooled lights, natural expiry and reset (draw submission skipped)");
        }
        public static void Run()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Run build checks after leaving Play mode.");
            foreach(var method in typeof(NativeAccount).GetMethods(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly))
                if(method.Name=="Start"&&(method.GetParameters().Length!=0||(method.ReturnType!=typeof(void)&&method.ReturnType!=typeof(System.Collections.IEnumerator))))
                    throw new InvalidOperationException("NativeAccount declares an invalid Unity Start callback");
            NativeSourceModels.ValidateTextureImports();
            var asset=Resources.Load<TextAsset>("GameContent");if(asset==null)throw new Exception("Missing native GameContent asset");
            var data=JsonUtility.FromJson<ConversionContent>(asset.text);
            var fixtures=JsonUtility.FromJson<ParityFixtures>(File.ReadAllText(Path.Combine(Application.dataPath,"../Tests/source-parity.json")));
            foreach(var test in NativeParityChecks.Run(data,fixtures))Debug.Log("PASS "+test);
            var presentation=JsonUtility.FromJson<PresentationFixtures>(File.ReadAllText(Path.Combine(Application.dataPath,"../Tests/presentation-parity.json")));
            var melee=JsonUtility.FromJson<MeleeFixtures>(File.ReadAllText(Path.Combine(Application.dataPath,"../Tests/melee-parity.json")));
            var specials=JsonUtility.FromJson<SpecialFixtures>(File.ReadAllText(Path.Combine(Application.dataPath,"../Tests/specials-parity.json")));
            foreach(var test in NativeSpecialChecks.Run(data,specials))Debug.Log("PASS "+test);
            foreach(var test in NativeMeleeChecks.Run(data,melee))Debug.Log("PASS "+test);
            foreach(var test in NativeMotionChecks.Run())Debug.Log("PASS "+test);
            foreach(var test in NativeVisualChecks.Run(data))Debug.Log("PASS "+test);
            foreach(var test in NativePresentationChecks.Run(data,presentation))Debug.Log("PASS "+test);
            if(data.chips.Any(c=>!NativeArena.ImplementedChip(c)))throw new Exception("A source Chip is absent from the native draft");
            if(NativeSceneMotion.Count!=31)throw new Exception("Source cinematic scenes are incomplete");
            if(NativeSourceMotion.ClipCount!=165)throw new Exception("Source motion bank is incomplete");
            using(var models=new NativeSourceModels()){foreach(var map in data.maps)if(!models.Has("map-"+map.id))throw new Exception("Missing map model: "+map.id);foreach(var weapon in data.weapons)if(!models.Has("weapon-"+weapon.id)&&weapon.id!="siren")throw new Exception("Missing weapon model: "+weapon.id);foreach(var part in new[]{"saw","saw-track","spikes","spikes-cones","spikes-surface","lava","lava-surface","lava-currents","lava-bubble-0","lava-bubble-1"})if(!models.Has("hazard-"+part))throw new Exception("Missing stage hazard model: "+part);}
            foreach(var name in new[]{"NativeSourceLit","NativeSourceTransparent","NativeWardPit","NativeNightSky","NativeSmoke","NativeHologram","NativeFieldGlow"}){var shader=Resources.Load<Shader>(name);if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Native source shader failed: "+name);}
            var audioDir=Path.Combine(Application.dataPath,"Conversion/Resources/Audio");int audioCount=0;foreach(var file in Directory.EnumerateFiles(audioDir,"*",SearchOption.AllDirectories).Where(p=>p.EndsWith(".mp3")||p.EndsWith(".wav"))){var path="Assets"+file.Substring(Application.dataPath.Length).Replace('\\','/');if(AssetDatabase.LoadAssetAtPath<AudioClip>(path)==null)throw new Exception("Audio import failed: "+path);audioCount++;}if(audioCount!=83)throw new Exception("Source audio bank is incomplete: "+audioCount);
            Debug.Log("PASS imported conversion assets: 165 source motion clips, 133 source models / 31 cosmetic scenes, 83 audio clips and full draft catalog");
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
                            CheckNetworkSerialization(arena);CheckCombatParity(data);CheckPresentation(arena);CheckGlow(arena,match);arena.ValidateMelee();arena.ValidateSpecials();arena.ValidateRangedAndMagic();
                        }
                        if(map.id=="siren")arena.ValidatePveEconomy(JsonUtility.FromJson<NativePveContent>(Resources.Load<TextAsset>("PveContent").text));
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
