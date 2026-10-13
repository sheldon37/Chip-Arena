using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // The pilot body skins in the source-model bank (Core/NativeBodySkins.cs, Core/NativeBodyModels.cs). The Standard
    // and Sheldon Clothes bodies are built once when the bank loads, three tiers each ("pilot-std", "-reduced", "-low";
    // "pilot-sheldon", ...), next to the untouched Prototype "pilot". Every model has exactly the exported pilot's parts
    // (names and joints), so every per-part consumer (wounds, corpses, portraits, ghosts, holograms, repair boxes, the
    // armour lights' sever masks) treats it as the pilot. The Core arrays are already in the bank frame (no mirror, no
    // winding turn). Each part gets one NativeSourceLit material, shared by its three tiers, with its armour-light table
    // (NativeArmorLight.Build over the body's own trims: an emitter part's glowing voxels, and the strips set into the
    // receivers, such as the visor), so the trims light the plates round them as on the Prototype. Each tier's Core mesh is
    // kept (pilotBodies) for the wounds, which cut that body's own parts (NativeWoundGeometry.cs, Core NativeWoundSkin).
    public sealed partial class NativeSourceModels
    {
        /// <summary>The body models built at load (two skins, three tiers each), for the build checks.</summary>
        public int PilotBodyModels{get;private set;}
        /// <summary>Triangles of each built body model, by bank id (the build checks compare them with the budgets).</summary>
        public readonly Dictionary<string,int> PilotBodyTriangles=new Dictionary<string,int>();
        private void PilotBodies()
        {
            var shader=Resources.Load<Shader>("NativeSourceLit");if(shader==null)throw new System.InvalidOperationException("Source material shader is missing");
            foreach(int skin in new[]{NativeBodySkins.StandardIndex,NativeBodySkins.SheldonIndex})
            {
                var full=NativeBodyModels.Build(skin,NativeBodySkins.Full);var materials=new Material[full.Parts.Length];
                for(int i=0;i<materials.Length;i++)
                {
                    var m=new Material(shader){name="Native "+NativeBodySkins.Labels[skin]+" body / "+full.Parts[i].Name,enableInstancing=true};m.SetInt("_Cull",2);m.SetFloat("_Ink",0);
                    this.materials.Add(m);materials[i]=m;
                }
                BodyArmorLights(full,materials);
                for(int tier=0;tier<NativeBodySkins.Tiers;tier++)
                {
                    var built=tier==NativeBodySkins.Full?full:NativeBodyModels.Build(skin,tier);var parts=new List<Part>();string id=NativeBodySkins.Model(skin,tier);
                    for(int i=0;i<built.Parts.Length;i++){var p=built.Parts[i];var mesh=BodyMesh(p.Mesh,id+" / "+p.Name+" / joint "+p.Bone);if(mesh!=null)parts.Add(new Part{Name=p.Name,Bone=p.Bone,Mesh=mesh,Material=materials[i],Core=p.Mesh});}
                    models[id]=parts.ToArray();pilotBodies[id]=built;PilotBodyTriangles[id]=built.Triangles;PilotBodyModels++;
                }
            }
        }
        private static Mesh BodyMesh(WeaponSection s,string name)
        {
            int count=s.Positions.Count;if(count==0)return null;
            var vertices=new Vector3[count];var normals=new Vector3[count];var colors=new Color[count];var uv=new Vector2[count];var emission=new List<Vector4>(count);
            for(int i=0;i<count;i++){var p=s.Positions[i];var n=s.Normals[i];var c=s.Colors[i];var e=s.Emission[i];vertices[i]=new Vector3(p.X,p.Y,p.Z);normals[i]=new Vector3(n.X,n.Y,n.Z);colors[i]=new Color(c.X,c.Y,c.Z,c.W);emission.Add(new Vector4(e.X,e.Y,e.Z,e.W));}
            var mesh=new Mesh{name="Pilot body "+name,indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.uv=uv;mesh.SetUVs(1,emission);mesh.SetTriangles(s.Indices,0);mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
        }
        /// <summary>Body part materials given an armour-light role (the Prototype's ArmorEmitters / ArmorReceivers stay its own).</summary>
        public int BodyArmorRoles{get;private set;}
        private void BodyArmorLights(PilotBodyMesh full,Material[] materials)
        {
            var plans=NativeArmorLight.Build(NativeBodyModels.LightSections(full));int e=ArmorEmitters,r=ArmorReceivers,h=ArmorHeld;
            for(int i=0;i<materials.Length;i++)ArmorMaterial(materials[i],plans[i]);
            BodyArmorRoles+=ArmorEmitters-e+ArmorReceivers-r;ArmorEmitters=e;ArmorReceivers=r;ArmorHeld=h;
        }
    }
}
