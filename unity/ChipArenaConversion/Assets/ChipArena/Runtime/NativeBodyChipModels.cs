using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The body chips' model parts in the source-model bank (Core/NativeBodyChipGear.cs): each part built once at bank load and
    // converted from the source frame like the weapons (WeaponMeshOf: x mirrored, each triangle turned over). One plain
    // NativeSourceLit material; the tag-1 zones take the piece's Chip colour from the draw's body tint. With them, the Toy
    // Soldier's sculpted figure (Core/NativeToySoldier.cs): one part per joint, drawn on the summon's rig as its blocks were.
    public sealed partial class NativeSourceModels
    {
        /// <summary>The body-chip part meshes built at load, for the build checks.</summary>
        public int BodyChipModels{get;private set;}
        private void BodyChipParts()
        {
            var shader=Resources.Load<Shader>("NativeSourceLit");if(shader==null)throw new System.InvalidOperationException("Source material shader is missing");
            var material=new Material(shader){name="Native body chips",enableInstancing=true};material.SetInt("_Cull",2);material.SetFloat("_Ink",0);materials.Add(material);
            foreach(var id in NativeBodyChipGear.Ids)
            {
                if(models.ContainsKey(id))continue;var built=NativeBodyChipGear.Build(id);if(built==null)continue;
                var parts=new List<Part>();foreach(var s in built.Sections){var mesh=WeaponMeshOf(s,id);if(mesh!=null)parts.Add(new Part{Name=id,Bone=-1,Mesh=mesh,Material=material,NoShadow=NativeBodyChipGear.Light(id)});}
                if(parts.Count>0){models[id]=parts.ToArray();BodyChipModels++;}
            }
            if(!models.ContainsKey(NativeToySoldier.Model))
            {
                var toy=new Material(shader){name="Native toy soldier",enableInstancing=true};toy.SetInt("_Cull",2);toy.SetFloat("_Ink",0);materials.Add(toy);var figure=new List<Part>();
                foreach(var s in NativeToySoldier.Build().Sections){var mesh=WeaponMeshOf(s,NativeToySoldier.Model+" / joint "+s.Bone);if(mesh!=null)figure.Add(new Part{Name=NativeToySoldier.Model,Bone=s.Bone,Mesh=mesh,Material=toy});}
                if(figure.Count>0)models[NativeToySoldier.Model]=figure.ToArray();
            }
        }
    }
}
