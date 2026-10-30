using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The sculpted PvE machines and dispensed chip in the source-model bank (Core/NativeMachineModels.cs), built once at bank
    // load beside the exported source models they replace on screen (those stay in the bank: the checks and any fallback read
    // them). Authored in the Unity model frame, so the sections upload as they are (BodyMesh: no mirror). Bones keep the bank's
    // part convention: 0 the body, 1 the lights the draw tints; the see-through sections (bone -2 in Core: the chip display's
    // glass, the pod door's glass, the med pulse, the chip's glass domes) take NativeSourceTransparent, double sided,
    // unshadowed, as bone -1 (root). The chip is one cartridge per tier; its ring (bone 2, Epic and up) turns about its core.
    public sealed partial class NativeSourceModels
    {
        /// <summary>The machine and chip models built at load, for the build checks.</summary>
        public int MachineModels{get;private set;}
        private void MachineSkins()
        {
            var lit=Resources.Load<Shader>("NativeSourceLit");var clear=Resources.Load<Shader>("NativeSourceTransparent");
            if(lit==null||clear==null)throw new System.InvalidOperationException("Source material shader is missing");
            var opaque=new Material(lit){name="Native machines",enableInstancing=true};opaque.SetInt("_Cull",2);opaque.SetFloat("_Ink",0);materials.Add(opaque);
            var glass=new Material(clear){name="Native machine glass",enableInstancing=true};glass.SetInt("_Cull",0);glass.SetFloat("_Ink",0);glass.SetFloat("_Unlit",0);materials.Add(glass);
            void Add(string id,WeaponMesh built)
            {
                if(built==null||models.ContainsKey(id))return;var parts=new List<Part>();
                foreach(var s in built.Sections)
                {
                    var mesh=BodyMesh(s,id+" / "+s.Bone);if(mesh==null)continue;bool see=s.Bone==NativeMachineModels.Glass;
                    parts.Add(new Part{Name=id,Bone=see?-1:s.Bone,Mesh=mesh,Material=see?glass:opaque,NoShadow=see});
                }
                if(parts.Count>0){models[id]=parts.ToArray();MachineModels++;}
            }
            foreach(var kind in NativeMachineModels.Kinds){Add(NativeMachineModels.BodyId(kind),NativeMachineModels.Build(kind));Add(NativeMachineModels.MovingId(kind),NativeMachineModels.Moving(kind));}
            // The dispensed / held chip: a cartridge per tier (the common one under PickupId, the fallback), and each one's ring
            // pivot (its core's centre) for PickupOrbit.
            foreach(var tier in NativeMachineModels.PickupTiers){var built=NativeMachineModels.Pickup(tier,out var core);string id=NativeMachineModels.PickupModel(tier);Add(id,built);pickupAxes[id]=new Vector3(core.X,core.Y,core.Z);}
            // The directed entrances' stage: the sculpted pylon and dais (NativeIntros' EntranceProps.Model).
            Add(NativeEntranceStageModels.PylonId,NativeEntranceStageModels.Pylon());Add(NativeEntranceStageModels.DaisId,NativeEntranceStageModels.Dais());
            Add(NativeEntranceStageModels.PodId,NativeEntranceStageModels.Pod());Add(NativeEntranceStageModels.PodDoorId,NativeEntranceStageModels.PodDoor());
        }
        private readonly Dictionary<string,Vector3> pickupAxes=new Dictionary<string,Vector3>();
        /// <summary>The ring's turn (Epic and up, bone NativeMachineModels.Orbit) of a chip model at `t`: about its core's axis
        /// (model +y), as a model-space matrix; identity for a model without one.</summary>
        public Matrix4x4 PickupOrbit(string model,float t)
        {
            if(!pickupAxes.TryGetValue(model,out var c))return Matrix4x4.identity;
            return Matrix4x4.Translate(c)*Matrix4x4.Rotate(Quaternion.Euler(0,NativeChipCartridge.Orbit(t,false)*Mathf.Rad2Deg,0))*Matrix4x4.Translate(-c);
        }
        /// <summary>The sculpted model id when it was built, else the exported source id (the machine and chip draws).</summary>
        public string MachineModel(string sculpted,string source)=>models.ContainsKey(sculpted)?sculpted:source;
    }
}
