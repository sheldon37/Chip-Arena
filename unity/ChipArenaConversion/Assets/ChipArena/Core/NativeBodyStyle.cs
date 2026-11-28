using System;

namespace ChipArena.UnityConversion
{
    // The one body predicate (docs/VOXEL-RETIREMENT.md 2.2, V1; owner 2026-11-18: "remove the voxel dependency and move forward
    // with a more detailed framing"): the only question a draw path asks about a pilot's body is whether it draws on the
    // detailed model (Runtime ArenaPresentation.ModelBody), and what it wears there. Its inputs: the bake (the licensed body on
    // this install, which --ca-voxel-bodies turns off), Settings · Player model's Classic (this PC only; it stays until the
    // owner's low-end check, O3), a map's bodies key (none yet: the voxel style's switchboard is V12's), the pilot's class and
    // its look. Pure and engine-free. Presentation only: no rule, hit volume, timing, damage, movement or network reads it.
    public static class NativeBodyStyle
    {
        /// <summary>A map's bodies key (2.4's switchboard; every map reads None until a map or mode asks for the voxel style).</summary>
        public enum MapBodies { None, Costume, Classic }
        /// <summary>What a pilot draws: the model in its outfit, the model wearing the voxel costume, its class's own body on the
        /// model (the Top Dog's Hot Dog), or the voxel style on the rig (no bake, the DEV switch, Classic, a classic map, the Siren).</summary>
        public enum Draw { Model, Costume, ClassBody, Voxel }
        /// <summary>The DEV Siren class draws its own (voxel) body until her model (O5: V11).</summary>
        public const string Siren="siren";

        /// <summary>What a pilot of class `spec` in look `look` draws (2.2's table).</summary>
        public static Draw Pilot(bool bake,bool classic,MapBodies map,string spec,string look)
        {
            // A class with its own body wins over the costume and the map: the Siren's is voxel until V11.
            if(spec==Siren)return Draw.Voxel;
            if(!bake||classic||map==MapBodies.Classic)return Draw.Voxel;
            if(spec==NativeTopDog.Spec)return Draw.ClassBody;
            if(map==MapBodies.Costume||NativeVoxelCostume.Worn(look))return Draw.Costume;
            return Draw.Model;
        }
        /// <summary>Whether a pilot of class `spec` draws on the model here (in any look).</summary>
        public static bool ModelBody(bool bake,bool classic,string spec)=>Pilot(bake,classic,MapBodies.None,spec,"")!=Draw.Voxel;
        /// <summary>Whether zombies and the zone kinds draw on the model (the bake, not Classic, not --ca-voxel-zombies or a map's
        /// voxel zombies key); a pool miss hides an add for a frame, never voxel (V7).</summary>
        public static bool ZombieModel(bool bake,bool classic,bool voxelZombies)=>bake&&!classic&&!voxelZombies;
    }
}
