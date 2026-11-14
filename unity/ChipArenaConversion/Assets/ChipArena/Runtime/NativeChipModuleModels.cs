using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The Standard skins' Chip modules in the source-model bank (Core/NativeChipModules*.cs): every part mesh the layouts can
    // place, per weapon (shared parts once), variant and detail tier, and the left dagger's mirrored copies, built once at bank
    // load and converted from the source weapon frame like the Standard weapons (WeaponMeshOf: x mirrored, each triangle turned
    // over). One plain NativeSourceLit material; the tag-1 zones take the module's Chip colour from the draw's body tint.
    public sealed partial class NativeSourceModels
    {
        /// <summary>The chip-module meshes built at load, for the build checks.</summary>
        public int ChipModuleModels{get;private set;}
        private void ChipModules()
        {
            var shader=Resources.Load<Shader>("NativeSourceLit");if(shader==null)throw new System.InvalidOperationException("Source material shader is missing");
            var material=new Material(shader){name="Native chip modules",enableInstancing=true};material.SetInt("_Cull",2);material.SetFloat("_Ink",0);materials.Add(material);
            foreach(var m in NativeChipModules.Meshes())
            {
                var id=NativeChipModules.ModelId(m.spec,m.part,m.variant,m.tier,m.left);if(id==null||models.ContainsKey(id))continue;var built=NativeChipModules.Build(m.spec,m.part,m.variant,m.tier,m.left);if(built==null)continue;
                var parts=new List<Part>();foreach(var s in built.Sections){var mesh=WeaponMeshOf(s,id);if(mesh!=null)parts.Add(new Part{Name=id,Bone=-1,Mesh=mesh,Material=material,NoShadow=NativeChipModules.Light(m.part)});}
                if(parts.Count>0){models[id]=parts.ToArray();ChipModuleModels++;}
            }
        }
    }
}
