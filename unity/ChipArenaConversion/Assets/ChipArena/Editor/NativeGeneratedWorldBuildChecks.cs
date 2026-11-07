using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
namespace ChipArena.UnityConversion.Editor
{
    public static partial class NativeBuildChecks
    {
        // Standalone graphics acceptance; the headless Windows builder runs the same simulation checks below.
        [MenuItem("Chip Arena/Check Expedition deployment acceptance")]
        public static void CheckDeploymentAcceptance()=>RunDeploymentAcceptance(true);
        // -nographics builders still run native geometry, PhysX, codecs and loopback checks.
        // A null device cannot prove shader passes or rendered quality; never report it as such.
        public static void CheckDeploymentBuildAcceptance()=>RunDeploymentAcceptance(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null);
        private static void RunDeploymentAcceptance(bool exerciseShaderPass)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Leave Play mode before checking deployment");
            if(exerciseShaderPass&&SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException("Deployment shader acceptance requires a graphics device; omit -nographics.");
            var active=SceneManager.GetActiveScene();var gravity=Physics.gravity;var mode=Physics.simulationMode;
            float scale=Time.timeScale;var cursor=Cursor.lockState;bool visible=Cursor.visible;
            var errors=new System.Collections.Generic.List<string>();
            void Capture(string message,string trace,LogType type)
            {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
            Application.logMessageReceived+=Capture;
            try
            {
                CheckDeploymentShader(exerciseShaderPass);
                var asset=Resources.Load<TextAsset>("GameContent");
                if(asset==null)throw new Exception("Missing deployment GameContent");
                var content=JsonUtility.FromJson<ConversionContent>(asset.text);NativeUnityMapLoader.Load(content);NativeArena.ValidateMutatorRules(content);content.Validate();
                string authored=Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName).text;
                var layout=NativeExpeditionLayout.Parse(authored);layout.compatibility=NativeExpeditionLayout.Compatibility(authored,content);
                ConversionGame.ValidateLobbyPromotion(content);
                ConversionGame.ValidateDeploymentLoadBarrier();
                foreach(uint seed in new uint[]{1,17,0x80000000u,0xffffffffu})for(int roll=1;roll<=2;roll++)
                {
                    var timer=System.Diagnostics.Stopwatch.StartNew();
                    var room=new ExpeditionDeploymentRoom("native-seed-"+seed+"-"+roll,"p1",seed,"black-siren",layout,content);
                    for(int i=0;i<roll;i++)if(!room.Execute("p1",i,ExpeditionDeploymentIntent.RollInstances))throw new Exception("Native seed roll refused");
                    var bundle=CheckGeneratedDeploymentWorld(content,room);
                    if(!room.Execute("p1",roll,ExpeditionDeploymentIntent.Deploy,true,bundle.Identity))throw new Exception("Native seed deploy refused");
                    var locked=ExpeditionRunBundle.Build(room.Ticket,content,authored);
                    if(locked.Identity!=bundle.Identity)throw new Exception("Deploy changed the native generated identity");
                    var wire=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="deployment-state",epoch=7,deployment=room.Ticket}));
                    var reconnect=new ExpeditionDeploymentReceiver();
                    if(!reconnect.Accept(wire.deployment,layout,content)||!reconnect.Snapshot.deployed||
                       ExpeditionRunBundle.Build(wire.deployment,content,authored).Identity!=locked.Identity||
                       reconnect.Accept(bundle.Ticket,layout,content))
                        throw new Exception("Native late/reconnecting guest lost the locked generated identity");
                    // All eight layouts get geometry/startup checks. Four first rolls also run the
                    // existing 1,800-tick four-area combat/transfer/replica/performance fixture.
                    if(roll==1)CheckExpeditionActivation(content,locked);
                    Debug.Log("PASS deployment seed acceptance: seed="+seed+", roll="+roll+", identity="+locked.Identity+", totalMs="+timer.Elapsed.TotalMilliseconds.ToString("F1")+", simulatedTicks="+(roll==1?1800:0));
                }
                if(errors.Count>0)throw new Exception("Deployment acceptance logged native errors: "+string.Join(" | ",errors));
                if(exerciseShaderPass)Debug.Log("DEPLOYMENT_NATIVE_ACCEPTANCE_READY: eight generated layouts; four 1800-tick split runs; station shader, lobby transfers and loading guards. Automated fixture, not a human co-op or rendered acceptance test.");
                else Debug.Log("DEPLOYMENT_BUILD_ACCEPTANCE_READY: eight generated layouts; four 1800-tick split runs; shader import, lobby transfers and loading guards. GPU passes, rendered performance and human co-op NOT exercised.");
            }
            finally
            {
                Application.logMessageReceived-=Capture;
                Physics.gravity=gravity;Physics.simulationMode=mode;Time.timeScale=scale;Cursor.lockState=cursor;Cursor.visible=visible;
                if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);
            }
        }
        private static Exception DeploymentShaderFailure(Shader shader,string stage)
        {
            var messages=ShaderUtil.GetShaderMessages(shader);
            return new Exception("NativeDeployScreen "+stage+" failed: "+(messages.Length==0?"Unity reported no compiler details.":
                string.Join(" | ",messages.Select(m=>m.message+" ("+m.file+":"+m.line+")"))));
        }
        private static void CheckDeploymentShader(bool exercisePass=false)
        {
            var shader=Resources.Load<Shader>("NativeDeployScreen");
            if(shader==null)throw new Exception("NativeDeployScreen shader resource is missing.");
            if(ShaderUtil.ShaderHasError(shader))throw DeploymentShaderFailure(shader,"import");
            if(!exercisePass)return;
            bool async=ShaderUtil.allowAsyncCompilation;Material material=null;
            try
            {
                ShaderUtil.allowAsyncCompilation=false;
                material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
                foreach(bool fog in new[]{false,true})
                {
                    material.shaderKeywords=fog?new[]{"FOG_LINEAR"}:Array.Empty<string>();
                    if(!shader.isSupported||!material.SetPass(0)||ShaderUtil.ShaderHasError(shader))
                        throw DeploymentShaderFailure(shader,"pass, linear fog="+fog);
                }
                Debug.Log("PASS deployment screen shader: no-fog and linear-fog pass on "+SystemInfo.graphicsDeviceType);
            }
            finally{ShaderUtil.allowAsyncCompilation=async;if(material!=null)UnityEngine.Object.DestroyImmediate(material);}
        }

        // Runs in the real builder, not the engine-free semantic compilation gate.
        private static ExpeditionRunBundle CheckGeneratedDeploymentWorld(ConversionContent content,ExpeditionDeploymentRoom room)
        {
            string authored=Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName).text;
            var bundle=ExpeditionRunBundle.Build(room.Ticket,content,authored);
            var world=bundle.World;
            var copy=JsonUtility.FromJson<ConversionContent>(JsonUtility.ToJson(bundle.Content));
            copy.contentRevision=bundle.Content.contentRevision;
            foreach(var m in copy.maps)m.unity=true;
            using(var runtime=new NativeExpeditionAreaRuntime("generated-check",copy,world.Map.id,match=>
            {
                match.Cooperative=match.Teams=true;match.ShowIntros=false;
                match.Seats.Add(new NativeSeat{id=1,name="Generated A",spec="pistol",team=0});
                match.Seats.Add(new NativeSeat{id=2,name="Generated B",spec="pistol",team=0});
            },_=>EditorSceneManager.NewPreviewScene(),s=>EditorSceneManager.ClosePreviewScene(s)))
            {
                var arena=runtime.Arena;arena.GeneratedRun=bundle;Physics.SyncTransforms();
                // Real Unity codec acceptance: clocks, remote pod looks and persistent late-join launch state.
                arena.Match.Seat(1).pod="paint:112233,trim:aabbcc,light:123456";
                var podRules=new NativePodRun(new[]{new NativePodRecord{seat=1,order=0,name="Generated A",bodyColor="#123456",accentColor="#abcdef",look=arena.Match.Seat(1).pod}});
                if(!podRules.Start(arena.Match))throw new Exception("Generated pod countdown did not start.");
                arena.PodRecords=podRules.Snapshot();
                var landing=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",frame=arena.NetworkFrame()})).frame;
                if(landing==null||landing.pods==null||landing.seats==null||landing.seats.Length<1||landing.phase!=NativePhase.Countdown||landing.phaseEnd!=3||landing.fightAt!=3||landing.pods.Length!=1||landing.seats[0].pod!=arena.Match.Seat(1).pod)
                    throw new Exception("Pod countdown or seat look lost in Unity codec.");
                arena.Match.SynchronizeExpeditionClock(4);
                if(!podRules.Board(1,new System.Numerics.Vector3(),4,true))throw new Exception("Pod boarding refused.");
                arena.PodRecords=podRules.Snapshot();
                var launch=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="frame",frame=arena.NetworkFrame()})).frame;
                if(launch==null||launch.pods==null||launch.pods.Length!=1||launch.seats==null||launch.seats.Length<1||launch.seats[0].pod!=arena.Match.Seat(1).pod||launch.pods[0].launchAt!=4||launch.pods[0].look!=arena.Match.Seat(1).pod||!NativePodRun.Valid(launch.pods,launch.time))
                    throw new Exception("Late-join pod launch lost in Unity codec.");

                var wire=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="expedition-state",epoch=7,
                    deployment=room.Ticket,frame=arena.NetworkFrame(),expeditionPublic=new ExpeditionView(),expeditionPrivate=new ExpeditionPrivateFrame()}));
                if(wire.frame?.pods==null||wire.frame.pods.Length!=1||wire.frame.pods[0].launchAt!=4||wire.frame.pods[0].look!=arena.Match.Seat(1).pod||wire.frame.seats==null||wire.frame.seats.Length<1||wire.frame.seats[0].pod!=arena.Match.Seat(1).pod)
                    throw new Exception("Pod launch or seat look lost in Expedition Unity snapshot codec.");
                if(wire.deployment?.station!=room.Ticket.station||wire.deployment.instanceRolls!=room.Ticket.instanceRolls)
                    throw new Exception("Generated run ticket lost in Unity snapshot codec.");
                var lobby=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="lobby",deployment=room.Ticket}));
                if(lobby.deployment?.station!=room.Ticket.station)throw new Exception("Station ticket lost in lobby codec.");
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
                var loaded=arena.LoadExpeditionM1Layout(authored,JsonUtility.FromJson<NativePveContent>(Resources.Load<TextAsset>("PveContent").text));
                if(loaded.compatibility!=bundle.Identity||loaded.Area(loaded.world).map!=world.Map.id)
                    throw new Exception("Generated startup validated a different layout.");
            }
            Debug.Log("PASS generated deployment blockout: seed="+room.Ticket.seed+", roll="+room.Ticket.instanceRolls+", identity="+bundle.Identity+"; native collision, Hulk routes, join circles and run/lobby ticket codec");
            return bundle;
        }
    }
}
