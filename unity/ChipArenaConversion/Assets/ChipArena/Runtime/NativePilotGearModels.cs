using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The pilot gear in the source-model bank (Core/NativePilotGear.cs): the chest core, the wrist device's body, bay, lid and
    // chip, the face's mask and visor, and the back emblem, built when the first visual body is drawn (the device and bay once
    // per sleeve radius, rounded to the millimetre), uploaded as the Standard weapons' sections are (mirrored into Unity's frame), on one
    // instanced NativeSourceLit material. The core rides the chest joint and the device pieces the right hand, so a pilot's
    // per-joint reveal and tint (the spawn-in's) reach them as they reach the voxel parts there.
    public sealed partial class NativeSourceModels
    {
        public const string GearCore="pilot-gear-core",GearDevice="pilot-gear-device",GearBay="pilot-gear-bay",GearLid="pilot-gear-lid",GearChip="pilot-gear-chip",GearMask="pilot-gear-mask",GearVisor="pilot-gear-visor",GearEmblem="pilot-gear-emblem";
        private Material gearMaterial;private bool gearFailed;
        // The device and its bay per sleeve radius, to the millimetre (an outfit's top and hands set it: Runtime/
        // NativeVisualBodyPresentation.cs VisualDress), each built once.
        private readonly System.Collections.Generic.Dictionary<int,string> gearDevices=new System.Collections.Generic.Dictionary<int,string>(),gearBays=new System.Collections.Generic.Dictionary<int,string>();
        private static int GearMillimetres(float radius)=>Mathf.RoundToInt(Mathf.Clamp(radius,.02f,.08f)*1000);
        /// <summary>The gear's models, with a device for a sleeve of radius `radius` (m); false without the source shader.</summary>
        public bool PilotGear(float radius)
        {
            if(gearFailed)return false;int mm=GearMillimetres(radius);
            if(gearMaterial!=null&&gearDevices.ContainsKey(mm))return true;
            if(gearMaterial==null)
            {
                var shader=Resources.Load<Shader>("NativeSourceLit");if(shader==null){gearFailed=true;return false;}
                gearMaterial=new Material(shader){name="Native pilot gear",enableInstancing=true};gearMaterial.SetInt("_Cull",2);gearMaterial.SetFloat("_Ink",0);materials.Add(gearMaterial);
                GearModel(GearCore,NativePilotGear.Core(),CapturedRig.Chest);GearModel(GearLid,NativePilotGear.Lid(),CapturedRig.RHand);GearModel(GearChip,NativePilotGear.Chip(),CapturedRig.RHand);
                GearModel(GearMask,NativePilotGear.Mask(),CapturedRig.Head);GearModel(GearVisor,NativePilotGear.Visor(),CapturedRig.Head);
                GearModel(GearEmblem,NativePilotGear.Emblem(),CapturedRig.Chest);
            }
            string device=GearDevice+"-"+mm,bay=GearBay+"-"+mm;
            GearModel(device,NativePilotGear.Device(mm/1000f),CapturedRig.RHand);GearModel(bay,NativePilotGear.Bay(mm/1000f),CapturedRig.RHand);
            gearDevices[mm]=device;gearBays[mm]=bay;return true;
        }
        /// <summary>The device's and bay's models for a sleeve of radius `radius` (built by PilotGear; null before).</summary>
        public string GearDeviceFor(float radius)=>gearDevices.TryGetValue(GearMillimetres(radius),out var id)?id:null;
        public string GearBayFor(float radius)=>gearBays.TryGetValue(GearMillimetres(radius),out var id)?id:null;
        /// <summary>The face gear's models (built with the gear; a device for the default sleeve until a body's bake sets its own).</summary>
        public bool PilotFace()=>gearMaterial!=null||PilotGear(.04f);
        private void GearModel(string id,WeaponMesh mesh,int bone)
        {
            if(models.TryGetValue(id,out var old))foreach(var p in old)CapturedAnimation.Release(p.Mesh);
            var parts=new System.Collections.Generic.List<Part>();
            foreach(var s in mesh.Sections){var m=WeaponMeshOf(s,id);if(m!=null)parts.Add(new Part{Name=id,Bone=bone,Mesh=m,Material=gearMaterial});}
            models[id]=parts.ToArray();
        }
    }
}
