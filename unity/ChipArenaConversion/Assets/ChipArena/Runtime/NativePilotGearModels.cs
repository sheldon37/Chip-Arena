using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The pilot gear in the source-model bank (Core/NativePilotGear.cs): the chest core and the wrist device's body, bay, lid and
    // chip, built when the first visual body is drawn (the device for its bake's sleeve radius, rounded to the millimetre; again
    // only if a bake's radius changes), uploaded as the Standard weapons' sections are (mirrored into Unity's frame), on one
    // instanced NativeSourceLit material. The core rides the chest joint and the device pieces the right hand, so a pilot's
    // per-joint reveal and tint (the spawn-in's) reach them as they reach the voxel parts there.
    public sealed partial class NativeSourceModels
    {
        public const string GearCore="pilot-gear-core",GearDevice="pilot-gear-device",GearBay="pilot-gear-bay",GearLid="pilot-gear-lid",GearChip="pilot-gear-chip";
        private Material gearMaterial;private float gearRadius=float.NaN;private bool gearFailed;
        /// <summary>The gear's models for a device on a sleeve of radius `radius` (m); false without the source shader.</summary>
        public bool PilotGear(float radius)
        {
            if(gearFailed)return false;radius=Mathf.Round(Mathf.Clamp(radius,.02f,.08f)*1000)/1000;
            if(gearMaterial!=null&&radius==gearRadius)return true;
            if(gearMaterial==null)
            {
                var shader=Resources.Load<Shader>("NativeSourceLit");if(shader==null){gearFailed=true;return false;}
                gearMaterial=new Material(shader){name="Native pilot gear",enableInstancing=true};gearMaterial.SetInt("_Cull",2);gearMaterial.SetFloat("_Ink",0);materials.Add(gearMaterial);
                GearModel(GearCore,NativePilotGear.Core(),CapturedRig.Chest);GearModel(GearLid,NativePilotGear.Lid(),CapturedRig.RHand);GearModel(GearChip,NativePilotGear.Chip(),CapturedRig.RHand);
            }
            GearModel(GearDevice,NativePilotGear.Device(radius),CapturedRig.RHand);GearModel(GearBay,NativePilotGear.Bay(radius),CapturedRig.RHand);
            gearRadius=radius;return true;
        }
        private void GearModel(string id,WeaponMesh mesh,int bone)
        {
            if(models.TryGetValue(id,out var old))foreach(var p in old)CapturedAnimation.Release(p.Mesh);
            var parts=new System.Collections.Generic.List<Part>();
            foreach(var s in mesh.Sections){var m=WeaponMeshOf(s,id);if(m!=null)parts.Add(new Part{Name=id,Bone=bone,Mesh=m,Material=gearMaterial});}
            models[id]=parts.ToArray();
        }
    }
}
