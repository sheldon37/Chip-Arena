using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The Siren minion kinds' models in the source-model bank (Core/NativeHordeModels.cs, the zombies of NativeZombieModels.cs; the
    // spitter's and the Hulk's jaws as their own "-jaw" models, hinged by the presentation):
    // every kind, look and crowd tier ("minion-<kind>-<look>-<full|reduced|impostor>") is sculpted once when the bank loads, beside
    // the untouched bank horde-0..5 (the old death records and the classic look still name those). Each kind has one material on
    // ChipArena/ZombieLit (Resources/NativeZombieLit.shader: SourceLit's minion material with the zombie shading, docs/ZOMBIE-DESIGN.md;
    // SourceLit if the shader cannot run). Since 2026-10-19 (owner: "more standard zombies", docs/ZOMBIE-GORE.md) no kind draws
    // the Siren's animated lines: no rising ink flow on any kind, and the Siren's seven no longer flicker (their veins are dead
    // and dark; their eyes, acid and boils glow steady). The zone kinds keep the glow cells' flicker for their own heat and
    // lights. Every kind keeps a faint neutral rim (ZombieLit) so the crowd still reads against the night. The models carry
    // their shading data in uv0 (BodyMesh) and each add's variation rides the instanced body tint, so the batches are as before.
    // The reduced and impostor tiers cast no shadows.
    public sealed partial class NativeSourceModels
    {
        /// <summary>The minion models built at load, and each one's triangles (the build checks hold them to the tier budgets).</summary>
        public int HordeKindModelCount{get;private set;}
        public readonly Dictionary<string,int> HordeKindTriangles=new Dictionary<string,int>();
        /// <summary>The zombies' rim (a faint neutral edge, ZombieLit) and the rising ink flow, off on every kind (no animated lines).</summary>
        public const float HordeInkRim=.14f,HordeInkFlow=0;
        /// <summary>Whether a kind's material flickers its glow cells: the zone kinds' own heat and lights only, never the Siren's seven.</summary>
        public static bool HordeFlickers(int kind)=>kind>=NativeHordeKinds.Count;
        private void HordeKindModels()
        {
            var shader=Resources.Load<Shader>("NativeSourceLit");if(shader==null)throw new System.InvalidOperationException("Source material shader is missing");
            var zombie=Resources.Load<Shader>("NativeZombieLit");if(zombie==null||!zombie.isSupported)zombie=shader;
            for(int kind=0;kind<NativeHordeKinds.All;kind++)
            {
                var material=new Material(zombie){name="Native Siren minion / "+NativeHordeKinds.Ids[kind],enableInstancing=true};material.SetInt("_Cull",2);
                material.SetFloat("_Ink",1);material.SetFloat(InkRimId,HordeInkRim);material.SetFloat(InkFlowId,HordeInkFlow);material.SetFloat(MinionGlowId,HordeFlickers(kind)?1:0);materials.Add(material);
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
