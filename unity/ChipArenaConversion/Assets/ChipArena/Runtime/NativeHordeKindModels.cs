using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The Siren minion kinds' models in the source-model bank (Core/NativeHordeModels.cs, the zombies of NativeZombieModels.cs; the
    // spitter's and the Hulk's jaws as their own "-jaw" models, hinged by the presentation):
    // every kind, look and crowd tier ("minion-<kind>-<look>-<full|reduced|impostor>") is sculpted once when the bank loads, beside
    // the untouched bank horde-0..5 (the old death records and the classic look still name those). Each kind has one material on
    // ChipArena/ZombieLit (Resources/NativeZombieLit.shader: SourceLit's minion material with the zombie shading, docs/ZOMBIE-DESIGN.md;
    // SourceLit if the shader cannot run): the minion glow (its ink veins flicker cell by cell and drop with the add's power,
    // NativeMinionFx) and a faint touch of the Siren's ink rim and flow on the rotted skin, so the crowd keeps her lavender edge
    // against the night without washing the zombies out. The models carry their shading data in uv0 (BodyMesh) and each add's
    // variation rides the instanced body tint, so the batches are as before. The reduced and impostor tiers cast no shadows.
    public sealed partial class NativeSourceModels
    {
        /// <summary>The minion models built at load, and each one's triangles (the build checks hold them to the tier budgets).</summary>
        public int HordeKindModelCount{get;private set;}
        public readonly Dictionary<string,int> HordeKindTriangles=new Dictionary<string,int>();
        /// <summary>The Siren's ink shading on the zombies: a soft rim (the skin and torn suit keep their colours), a faint rising flow.</summary>
        public const float HordeInkRim=.16f,HordeInkFlow=.008f;
        private void HordeKindModels()
        {
            var shader=Resources.Load<Shader>("NativeSourceLit");if(shader==null)throw new System.InvalidOperationException("Source material shader is missing");
            var zombie=Resources.Load<Shader>("NativeZombieLit");if(zombie==null||!zombie.isSupported)zombie=shader;
            for(int kind=0;kind<NativeHordeKinds.All;kind++)
            {
                var material=new Material(zombie){name="Native Siren minion / "+NativeHordeKinds.Ids[kind],enableInstancing=true};material.SetInt("_Cull",2);
                material.SetFloat("_Ink",1);material.SetFloat(InkRimId,HordeInkRim);material.SetFloat(InkFlowId,HordeInkFlow);material.SetFloat(MinionGlowId,1);materials.Add(material);
                for(int look=0;look<NativeHordeKinds.Looks[kind];look++)for(int tier=0;tier<NativeHordeModels.Tiers;tier++)
                {
                    var built=NativeHordeModels.Build(kind,look,tier);string id=NativeHordeModels.Id(kind,look,tier);var parts=new List<Part>();
                    foreach(var s in built.Mesh.Sections){var mesh=BodyMesh(s,id+" / joint "+s.Bone);if(mesh!=null)parts.Add(new Part{Name=id,Bone=s.Bone,Mesh=mesh,Material=material,NoShadow=tier!=NativeHordeModels.Full});}
                    models[id]=parts.ToArray();HordeKindTriangles[id]=built.Triangles;HordeKindModelCount++;
                    // A hinged kind's jaw: its own model on the head joint (the presentation turns it about NativeHordeModels.JawHinge).
                    if(built.Jaw!=null&&built.Jaw.Triangles>0)
                    {
                        string jaw=NativeHordeModels.JawId(kind,look,tier);var jp=new List<Part>();
                        foreach(var s in built.Jaw.Sections){var mesh=BodyMesh(s,jaw+" / joint "+s.Bone);if(mesh!=null)jp.Add(new Part{Name=jaw,Bone=s.Bone,Mesh=mesh,Material=material,NoShadow=tier!=NativeHordeModels.Full});}
                        models[jaw]=jp.ToArray();
                    }
                }
            }
            IronworksModels(shader);StationPropModels(shader);
        }
    }
}
