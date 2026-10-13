using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private readonly List<Mesh> cutExitMeshes=new List<Mesh>();
        private void BuildCutExitCollision()
        {
            foreach(var flight in NativeCyberCityStairs.Flights(Map))
            {
                // Keep every original prop/renderer/id. Only their tread collision is replaced.
                foreach(var step in flight.Steps)StaticBlocks.Single(p=>p.Definition.id==step.id).GetComponent<Collider>().enabled=false;
                var go=new GameObject(flight.Id+"-collision-ramp");go.layer=PropLayer;go.transform.SetParent(root.transform);
                var low=Vec(flight.Low);var high=Vec(flight.High);
                var across=Vector3.Cross(Vec(flight.Direction),Vector3.up)*flight.Width*.5f;
                var a=low-across;var b=high-across;var c=high+across;var d=low+across;
                Vector3 Bottom(Vector3 p)=>new Vector3(p.x,flight.Bottom,p.z);
                // Closed wedge: no vertical tread faces, no final street lip, no hollow collision underside.
                var mesh=new Mesh{name=go.name};cutExitMeshes.Add(mesh);
                mesh.vertices=new[]{a,b,c,d,Bottom(a),Bottom(b),Bottom(c),Bottom(d)};
                mesh.triangles=new[]{0,3,2,0,2,1,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
                mesh.RecalculateBounds();
                var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.sharedMaterial=surfaces[flight.Steps[0].material];
            }
        }
        private void ClearCutExitCollision()
        {foreach(var mesh in cutExitMeshes)CapturedAnimation.Release(mesh);cutExitMeshes.Clear();}
    }
}
