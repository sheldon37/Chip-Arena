using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChipArena.UnityConversion
{
    // PhysicsScene has buffer overloads rather than the global allocating All/Check APIs.
    public static class NativeSceneQueries
    {
        public static bool Linecast(PhysicsScene scene,Vector3 from,Vector3 to,int mask)
        {var delta=to-from;return scene.Raycast(from,delta.normalized,out _,delta.magnitude,mask,QueryTriggerInteraction.Ignore);}
        public static RaycastHit[] RaycastAll(PhysicsScene scene,Ray ray,float distance,int mask,QueryTriggerInteraction triggers)
        {
            var hits=new RaycastHit[32];int count;
            while((count=scene.Raycast(ray.origin,ray.direction,hits,distance,mask,triggers))==hits.Length)Array.Resize(ref hits,checked(hits.Length*2));
            Array.Resize(ref hits,count);return hits;
        }
    }
    public sealed partial class NativeArena
    {
        private readonly Collider[] sceneCheck=new Collider[1];
        private UnityEngine.SceneManagement.Scene authorityScene;
        public PhysicsScene PhysicsWorld=>root!=null?root.scene.GetPhysicsScene():throw new InvalidOperationException("Arena scene is not loaded.");
        public bool CheckWorldSphere(Vector3 at,float radius)=>PhysicsWorld.OverlapSphere(at,radius,sceneCheck,WorldMask,QueryTriggerInteraction.Ignore)>0;
        private bool presentationEnabled=true;
        public void SetPresentationEnabled(bool enabled)
        {
            presentationEnabled=enabled;
            if(root!=null)foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.forceRenderingOff=!enabled;
        }
        private void ApplyAreaVisibility(GameObject obj)
        {if(!presentationEnabled)foreach(var renderer in obj.GetComponentsInChildren<Renderer>(true))renderer.forceRenderingOff=true;}
    }
}
