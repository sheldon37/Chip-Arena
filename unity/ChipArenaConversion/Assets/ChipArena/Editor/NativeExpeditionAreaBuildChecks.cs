using System;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChipArena.UnityConversion.Editor
{
    public static partial class NativeBuildChecks
    {
        private static void CheckExpeditionAreaRuntimes(ConversionContent content)
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Two-area foundation: "+why);}
            var gravity=Physics.gravity;
            try
            {
                Physics.gravity=new Vector3(0,-17,0);
                using(var registry=new ExpeditionAreaRegistry<NativeExpeditionAreaRuntime>("exp-city","exp-ward-zero",area=>
                    new NativeExpeditionAreaRuntime(area,content,area=="exp-city"?"expedition-city":"expedition-ward-zero",match=>
                    {
                        match.Cooperative=true;match.ShowIntros=false;
                        int first=area=="exp-city"?1:2,last=area=="exp-city"?1:4;
                        for(int id=first;id<=last;id++)match.Seats.Add(new NativeSeat{id=id,name="Area test "+id,spec="pistol"});
                    },_=>EditorSceneManager.NewPreviewScene(),scene=>EditorSceneManager.ClosePreviewScene(scene))))
                {
                    var hub=registry.Acquire("exp-city");var ward=registry.Acquire("exp-ward-zero");
                    Check(hub.Scene!=ward.Scene&&hub.Arena.PhysicsWorld!=ward.Arena.PhysicsWorld,"distinct local scenes");
                    Check(!ReferenceEquals(hub.Arena.Match,ward.Arena.Match),"independent match callbacks");
                    Check(hub.Arena.Root.scene==hub.Scene&&ward.Arena.Root.scene==ward.Scene,"roots start in their own scene");
                    Check(Physics.gravity==new Vector3(0,-17,0),"area constructors preserve configured physics");
                    Physics.gravity=new Vector3(0,-23,0);
                    void Ready(NativeExpeditionAreaRuntime runtime)
                    {runtime.Ready(a=>{a.Match.HoldResolution=()=>true;a.Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10,fightAt=0});});}
                    Ready(hub);hub.SetOccupants(1);ward.SetOccupants(3);
                    GameObject Block(NativeExpeditionAreaRuntime runtime)
                    {
                        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=runtime.Area+" query probe";go.layer=NativeArena.PropLayer;
                        go.transform.SetParent(runtime.Arena.Root.transform);go.transform.position=new Vector3(1000,0,1000);return go;
                    }
                    var hubBlock=Block(hub);var wardBlock=Block(ward);Physics.SyncTransforms();
                    var origin=new Vector3(1000,5,1000);var hits=new Collider[8];
                    foreach(var area in new[]{hub,ward})
                    {
                        Check(area.Arena.PhysicsWorld.Raycast(origin,Vector3.down,out var ray,10,NativeArena.WorldMask)&&ray.collider.gameObject==(area==hub?hubBlock:wardBlock),"raycast stays in area");
                        int count=area.Arena.PhysicsWorld.OverlapSphere(new Vector3(1000,0,1000),1,hits,NativeArena.WorldMask,QueryTriggerInteraction.Ignore);
                        Check(count==1&&hits[0].gameObject==(area==hub?hubBlock:wardBlock),"overlap stays in area");
                        Check(area.Arena.PhysicsWorld.SphereCast(origin,.1f,Vector3.down,out var cast,10,NativeArena.WorldMask)&&cast.collider.gameObject==(area==hub?hubBlock:wardBlock),"swept query stays in area");
                    }
                    Rigidbody Probe(NativeExpeditionAreaRuntime runtime)
                    {var go=new GameObject("clock probe");go.transform.SetParent(runtime.Arena.Root.transform);go.transform.position=new Vector3(1000,100,1000);return go.AddComponent<Rigidbody>();}
                    var aBody=Probe(hub);var bBody=Probe(ward);Physics.SyncTransforms();
                    int clock=0;Action<double> shared=_=>clock++;
                    registry.Step(0,shared);Check(aBody.position.y<100&&bBody.position.y==100&&clock==1,"loading Ward does not advance its physics or freeze hub");
                    Ready(ward);registry.Present("exp-city");
                    Check(hub.Arena.Root.GetComponentsInChildren<Renderer>().All(r=>!r.forceRenderingOff)&&ward.Arena.Root.GetComponentsInChildren<Renderer>().All(r=>r.forceRenderingOff),"one area rendered with other colliders active");
                    for(int i=1;i<=3;i++)registry.Step(i,shared);
                    Check(aBody.position.y<100&&bBody.position.y<100&&clock==4,"both independent physics scenes advance and shared clock ticks once");
                    float ay=aBody.position.y,by=bBody.position.y;Check(!registry.Step(3,shared)&&aBody.position.y==ay&&bBody.position.y==by&&clock==4,"duplicate tick never resimulates physics");
                    ward.SetOccupants(0);registry.Step(4,shared);Check(bBody.position.y==by&&aBody.position.y<ay,"empty encounter retains frozen physics");
                    registry.Present("exp-ward-zero");Check(hub.Arena.Root.GetComponentsInChildren<Renderer>().All(r=>r.forceRenderingOff),"former area hidden after rebind");
                    var oldWardRoot=ward.Arena.Root;int hubGeneration=hub.Arena.Generation;hub.Arena.Match.ResetRound();
                    Check(ward.Arena.Root==oldWardRoot&&hub.Arena.Generation==hubGeneration+1&&hub.Arena.Root.scene==hub.Scene,"reset/callback and scene isolation");
                    Debug.Log("PASS two-area runtime foundation: independent scenes, queries, callbacks, clock, occupancy, load and rendering. Not a split-party gameplay or performance gate.");
                }
            }
            finally{Physics.gravity=gravity;}
        }
    }
}
