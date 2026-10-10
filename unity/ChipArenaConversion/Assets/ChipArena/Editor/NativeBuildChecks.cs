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
                        var audio=host.GetComponentsInChildren<AudioSource>(true);
                        var particles=host.GetComponentsInChildren<ParticleSystem>(true);
                        var lights=host.GetComponentsInChildren<Light>(true);
                        var combatPool=host.transform.Find("Native combat light pool");
                        var blastPool=host.transform.Find("Native blast light pool");
                        int combatLights=combatPool==null?0:combatPool.GetComponentsInChildren<Light>(true).Length;
                        int blastLights=blastPool==null?0:blastPool.GetComponentsInChildren<Light>(true).Length;
                        if(audio.Length!=25||particles.Length!=1||combatLights!=7||blastLights!=NativeBlastSim.LightCapacity||lights.Length!=7+NativeBlastSim.LightCapacity)
                            throw new Exception($"Presentation startup inventory: audio={audio.Length}/25, particles={particles.Length}/1, combat lights={combatLights}/7, blast lights={blastLights}/{NativeBlastSim.LightCapacity}, total lights={lights.Length}/{7+NativeBlastSim.LightCapacity}");
                        // Seed both pools so omitting either Clear() is caught, even without rendering a frame.
                        foreach(var light in lights)light.enabled=true;
                        particles[0].Emit(3);
                        presentation.ResetPresentation();presentation.ResetPresentation();
                        if(presentation.ParticleCount!=0||lights.Any(light=>light.enabled)||audio.Any(source=>source.isPlaying))
                            throw new Exception("Presentation reset left live particles, audio or combat/blast lights");
                        if(!audio.SequenceEqual(host.GetComponentsInChildren<AudioSource>(true))||!particles.SequenceEqual(host.GetComponentsInChildren<ParticleSystem>(true))||!lights.SequenceEqual(host.GetComponentsInChildren<Light>(true)))
                            throw new Exception("Presentation reset replaced or leaked pooled components");
                    }
                    finally{CapturedAnimation.Release(host);}
                }
            }
            finally{Application.logMessageReceived-=Capture;}
            if(errors.Count>0)throw new Exception("Presentation startup logged Unity errors: "+string.Join("\n",errors));
            Debug.Log("PASS native presentation startup: audio, particles, seven combat lights and blast pool, seeded repeated reset and teardown (two cycles; no rendered frame)");
        }
        private static void CheckGlow(NativeArena arena,NativeMatch match)
        {
            var host=new GameObject("Glow lifecycle check");host.transform.SetParent(arena.Root.transform);var camera=host.AddComponent<Camera>();camera.enabled=false;
            var rigs=new System.Collections.Generic.Dictionary<int,CapturedAnimation.Player>();
            using(var glow=new NativeCombatGlow(host.transform))
            {
                const int impactCount=200;var prop=arena.Props[0];float firstImpactAt=arena.Time;
                arena.Events.Clear();arena.Bullets.Clear();arena.AbilityShots.Clear();arena.MeleeShots.Clear();
                // Exercise 200 overlapping marks without violating the authority's per-tick event budget.
                // This is a glow-lifetime test, not a request to bypass or disable network/event limits.
                for(int n=1;n<=impactCount;n++)
                {
                    if(n>1&&(n-1)%NativeEventBuffer.PerTickEffects==0)match.Tick(1d/60);
                    arena.Events.Add(new ArenaEvent{Id=n,Kind=EventKind.Impact,Time=arena.Time,SurfaceId=prop.Definition.id,Position=NativeArena.Vec(prop.transform.position+Vector3.up),Normal=new V3(0,1,0),Color=0x58cfff});
                }
                float lastImpactAt=arena.Time;
                if(arena.Events.Count!=impactCount)throw new Exception($"Glow fixture exceeded the event budget: accepted {arena.Events.Count}/{impactCount} impacts");
                arena.Bullets.Add(new NativeBullet{Id=500,Spec="pistol",Position=Vector3.up,Radius=.045f});
                arena.AbilityShots.Add(new NativeAbilityShot{Id=501,Owner=arena.Local,Position=Vector3.up*2,Radius=.2f});
                arena.MeleeShots.Add(new NativeMeleeShot{Id=502,Original=arena.Local,Owner=arena.Local,Shield=true,Position=Vector3.up*3,Radius=.28f});
                void Sample()=>glow.Render(arena,camera,null,false,rigs,null,null,false);
                void Expect(int marks,int traces,string stage)
                {if(glow.LiveMarks!=marks||glow.LiveTraces!=traces)throw new Exception($"Combat glow {stage}: marks={glow.LiveMarks}/{marks}, traces={glow.LiveTraces}/{traces}, accepted events={arena.Events.Count}");}
                void AdvanceTo(double time){while(match.Time<time)match.Tick(Math.Min(.2,time-match.Time));Sample();}
                Sample();Expect(impactCount,3,"initial burst");
                if(host.GetComponentsInChildren<Light>().Length!=7)throw new Exception("Combat lights must use exactly seven pooled objects");
                arena.Bullets.Clear();arena.AbilityShots.Clear();arena.MeleeShots.Clear();match.Tick(.2);Sample();
                Expect(impactCount,0,"trail expiry");
                AdvanceTo(firstImpactAt+NativeGlowRules.ImpactLife-.01);Expect(impactCount,0,"before first impact expiry");
                AdvanceTo(firstImpactAt+NativeGlowRules.ImpactLife+.005);Expect(impactCount-NativeEventBuffer.PerTickEffects,0,"first tick expiry");
                AdvanceTo(lastImpactAt+NativeGlowRules.ImpactLife+.01);Expect(0,0,"last impact expiry");
                glow.Quality=0;Sample();foreach(var light in host.GetComponentsInChildren<Light>())if(light.enabled)throw new Exception("Off leaves a combat light enabled");
                glow.Clear();if(glow.LiveMarks!=0||glow.LiveTraces!=0)throw new Exception("Glow reset leaked presentation state");
            }
            arena.Events.Clear();CapturedAnimation.Release(host);
            Debug.Log("PASS native glow lifecycle: 200 overlapping impacts across budgeted ticks, primary/ability/thrown-shield traces, seven pooled lights, staggered expiry and reset (draw submission skipped)");
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
            using(var models=new NativeSourceModels()){foreach(var map in data.maps)if(!models.Has("map-"+map.id))throw new Exception("Missing map model: "+map.id);foreach(var weapon in data.weapons)if(!models.Has("weapon-"+weapon.id)&&weapon.id!="siren")throw new Exception("Missing weapon model: "+weapon.id);foreach(var part in new[]{"saw","saw-track","spikes","spikes-cones","spikes-surface","lava","lava-surface","lava-currents","lava-bubble-0","lava-bubble-1"})if(!models.Has("hazard-"+part))throw new Exception("Missing stage hazard model: "+part);
                // The Siren's animated pieces split from its joint batches, and a face track for every Siren clip.
                if(models.PortraitPieceCount("siren")!=NativeSirenLook.Count||models.SirenHaloTexture==null||NativeSirenLookTracks.Count!=23)throw new Exception("Siren look pieces or face tracks are incomplete");
                // The dev-siren cell's bulb glow track (NativeSirenCell drives its glow with the bulb's level; the cell's lights go to the stage block).
                var cell=NativeSceneMotion.Get("stage-dev-siren");int bulb=-1;if(cell?.values!=null)for(int t=0;t<cell.tracks&&bulb<0;t++)if(NativeSirenIntroLight.AtBulb(cell.values,cell.frames,t)&&models.OpaqueBone("stage-dev-siren",t))bulb=t;
                if(bulb<0)throw new Exception("The Siren cell's bulb glow track is missing from the scene bank");}
            foreach(var name in new[]{"NativeSourceLit","NativeSourceTransparent","NativeWardPit","NativeNightSky","NativeSmoke","NativeHologram","NativeRepair","NativeStatusUnlit","NativeBeacon","NativeFieldGlow","NativeBlast","NativePortrait","NativePortraitResolve","NativeKillPoints","NativeKillBody","NativeKillGhost","NativeSirenFx","NativeDevGhost","NativeUiBlur","NativeAuditorHalo"}){var shader=Resources.Load<Shader>(name);if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Native source shader failed: "+name);}
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
                            CheckNetworkSerialization(arena);CheckCombatParity(data);CheckInteractionFeedback(data);CheckPresentation(arena);CheckGlow(arena,match);CheckShots(arena);arena.ValidateMelee();arena.ValidateSpecials();arena.ValidateRangedAndMagic();
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
            // The death presentation's primitives (sky broadcast lines, stars and nameplate; the vent splat's SPLAT!).
            {var shader=Resources.Load<Shader>("NativeDeathFx");if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Native source shader failed: NativeDeathFx");}
        }
    }
}
