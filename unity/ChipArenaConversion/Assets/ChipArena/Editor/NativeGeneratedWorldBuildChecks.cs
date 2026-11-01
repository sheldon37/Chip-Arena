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
        // Explicit draft acceptance entry point: no player build, publisher, branch movement or live services.
        [MenuItem("Chip Arena/Check Expedition deployment acceptance")]
        public static void CheckDeploymentAcceptance()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Leave Play mode before checking deployment");
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException("Deployment shader acceptance requires a graphics device; omit -nographics.");
            var active=SceneManager.GetActiveScene();var gravity=Physics.gravity;var mode=Physics.simulationMode;
            float scale=Time.timeScale;var cursor=Cursor.lockState;bool visible=Cursor.visible;
            var errors=new System.Collections.Generic.List<string>();
            void Capture(string message,string trace,LogType type)
            {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
            Application.logMessageReceived+=Capture;
            try
            {
                CheckDeploymentShader(true);
                var asset=Resources.Load<TextAsset>("GameContent");
                if(asset==null)throw new Exception("Missing deployment GameContent");
                var content=JsonUtility.FromJson<ConversionContent>(asset.text);NativeUnityMapLoader.Load(content);content.Validate();
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
                Debug.Log("DEPLOYMENT_NATIVE_ACCEPTANCE_READY: eight generated layouts; four 1800-tick split runs; station shader, lobby transfers and loading guards. Automated fixture, not a human co-op or rendered acceptance test.");
            }
            finally
            {
                Application.logMessageReceived-=Capture;
                Physics.gravity=gravity;Physics.simulationMode=mode;Time.timeScale=scale;Cursor.lockState=cursor;Cursor.visible=visible;
                if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);
            }
        }
        private static void CheckDeploymentShader(bool exercisePass=false)
        {
            var shader=Resources.Load<Shader>("NativeDeployScreen");
            if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Native source shader failed: NativeDeployScreen");
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
                        throw new Exception("Deployment screen pass failed, linear fog="+fog);
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
                var arena=runtime.Arena;arena.GeneratedRun=bundle;Physics.SyncTransforms();
                var wire=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="expedition-state",epoch=7,
                    deployment=room.Ticket,frame=arena.NetworkFrame(),expeditionPublic=new ExpeditionView(),expeditionPrivate=new ExpeditionPrivateFrame()}));
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
