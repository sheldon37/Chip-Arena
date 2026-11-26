using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private void BuildMeridianRouteCollision()
        {
            var input = Map.routeCollision;
            if (input == null) return;
            var bank = NativePackBanks.CollisionBank(NativeMeridianRoute.Bank);
            if (bank == null) return;
            var landmarks = NativeMeridianLandmarks.Place(input.Layout, Map, input.Doors, input.Slots);
            var route = NativeMeridianRoute.Place(input.Layout, Map, landmarks);
            foreach (var piece in route.Art.Pieces)
            {
                if (!NativeMeridianRoute.HasCollision(piece.Key)) continue;
                // Always use the same authored surfaces, even when low-detail rails omit the individual bars.
                var prop = bank.Find(piece.Key + "/0");
                if (prop == null || prop.Parts.Length == 0) continue;
                var source = bank.Meshes[prop.Parts[0].Mesh];
                var indices = new List<int>();
                foreach (var sub in source.Submeshes)
                {
                    var faces = sub.Indices;
                    for (int i = 0; i < faces.Length; i += 3)
                    {
                        int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                        if (!NativeMeridianRoute.CollisionTriangle(piece.Key, source.Positions[a], source.Positions[b], source.Positions[c])) continue;
                        indices.Add(a);
                        indices.Add(b);
                        indices.Add(c);
                    }
                }
                if (indices.Count == 0) continue;
                var vertices = new Vector3[source.Positions.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    var v = source.Positions[i];
                    vertices[i] = Vec(NativeMeridianLandmarks.Apply(piece.Place, new V3(v.X, v.Y, v.Z)));
                }
                var mesh = new Mesh { name = piece.Key + "-collision" };
                if (vertices.Length > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                cutExitMeshes.Add(mesh);
                mesh.vertices = vertices;
                mesh.SetTriangles(indices, 0);
                var child = new GameObject(mesh.name) { layer = PropLayer };
                child.transform.SetParent(root.transform, false);
                var collider = child.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.sharedMaterial = surfaces[piece.Key == NativeMeridianRoute.Roofs ? "stone" : "metal"];
            }
        }
    }
}
