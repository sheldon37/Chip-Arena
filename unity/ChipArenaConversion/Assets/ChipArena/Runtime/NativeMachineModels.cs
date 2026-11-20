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
                    // (The cartridge's core module glass rides its core's bone: NativeMachineModels.CoreGlass on CoreBody.)
                    var mesh=BodyMesh(s,id+" / "+s.Bone);if(mesh==null)continue;bool core=s.Bone==NativeMachineModels.CoreGlass,see=s.Bone==NativeMachineModels.Glass||core;
                    parts.Add(new Part{Name=id,Bone=core?NativeMachineModels.CoreBody:see?-1:s.Bone,Mesh=mesh,Material=see?glass:opaque,NoShadow=see});
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
            // The hub's REVIVE-O-MATIC (Core/NativeReviveMachineModels.cs).
            ReviveMachineSkins(Add);
            // The Civil Defence caches' CD-647 lockers, a body, a lid and a latch per tier (Core/NativeCacheLockerModels.cs; drawn by
            // NativeCachePresentation.cs only where A2's solid chest block stands, or in the DEV preview).
            foreach(var (id,mesh) in NativeCacheLockerModels.All())Add(id,mesh);
            // The dressing round the machines in the generated instances (Core/NativeInstanceMachineDress.cs).
            foreach(var (id,build,_) in NativeInstanceMachineDress.All)Add(id,build());
            // The Expedition drop pod and its door (Core/NativePodModels.cs; NativePodPresentation.cs tints its parts).
            Add(NativePodModels.PodId,NativePodModels.Pod());Add(NativePodModels.DoorId,NativePodModels.Door());
            // the cabin's jump seat and its harness bar (the hub's pod draws them; the station's dock has its own)
            Add(NativePodModels.SeatId,NativePodModels.Seat());Add(NativePodModels.HarnessId,NativePodModels.HarnessBarModel());
            // and their see-through copies (every part in the glass, its bone kept for the pod's tints, unshadowed)
            void Ghost(string id,WeaponMesh built)
            {
                if(built==null||models.ContainsKey(id))return;var parts=new List<Part>();
                foreach(var s in built.Sections){var mesh=BodyMesh(s,id+" / "+s.Bone);if(mesh!=null)parts.Add(new Part{Name=id,Bone=s.Bone,Mesh=mesh,Material=glass,NoShadow=true});}
                if(parts.Count>0){models[id]=parts.ToArray();MachineModels++;}
            }
            Ghost(NativePodModels.GhostId,NativePodModels.Pod());Ghost(NativePodModels.DoorGhostId,NativePodModels.Door());
            // the crash's lasting mark: flat paint over the floor, lit but casting no shadow (a crater per shape, its furrow in it;
            // its see-through part, the soot fading into the plaza and the cracks, in the glass by its vertex alpha)
            void Flat(string id,WeaponMesh built)
            {
                if(built==null||models.ContainsKey(id))return;var parts=new List<Part>();
                foreach(var s in built.Sections){var mesh=BodyMesh(s,id+" / "+s.Bone);bool see=s.Bone==NativeMachineModels.Glass;if(mesh!=null)parts.Add(new Part{Name=id,Bone=see?-1:s.Bone,Mesh=mesh,Material=see?glass:opaque,NoShadow=true});}
                if(parts.Count>0){models[id]=parts.ToArray();MachineModels++;}
            }
            for(int k=0;k<NativePodModels.CraterShapes;k++)Flat(NativePodModels.CraterIds[k],NativePodModels.Crater(k));
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
        private readonly Dictionary<string,bool> pickupCores=new Dictionary<string,bool>();
        /// <summary>Whether a chip model carries the cartridge's core module (NativeMachineModels.CoreBody), which the draft stage's install
        /// seats alone in the bay (the exported generic chip has none); remembered per model.</summary>
        public bool PickupCore(string model)
        {
            if(model==null)return false;if(pickupCores.TryGetValue(model,out bool core))return core;
            core=false;if(models.TryGetValue(model,out var parts))foreach(var p in parts)if(p.Bone==NativeMachineModels.CoreBody){core=true;break;}
            return pickupCores[model]=core;
        }
    }
}
