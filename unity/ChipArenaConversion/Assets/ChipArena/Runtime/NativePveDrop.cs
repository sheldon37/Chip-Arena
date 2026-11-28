using System;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // The drop entries' jump-down and landing slam were removed with the drop-in (owner 2026-11-21): a drop entry is an emergence
        // from the floor on the same clock (Core/NativeHordeEmerge, NativePveHorde.WardEntryPose). The smash's shove stays here.
        /// <summary>An add still under the floor (an emergence before its torso is out, ExposedAt) is no one's target or enemy yet.</summary>
        private bool EmergeHeld(NativeMinion m)=>m.Pve!=null&&NativeHordeEmerge.Emerges(m.Pve.Entry,m.Pve.Stats?.id)&&Time<m.Pve.ExposedAt;
        private void PveSlamShove(NativePilot pilot,Vector3 delta,Vector3 fallback,float impulse,bool blocked)
        {
            if(pilot.Hp<=0||SirenArmor(pilot)||KernelLocked(pilot))return;
            pilot.Knockback+=Vec(NativeHordeDropRules.Shove(Vec(delta),Vec(fallback),impulse,blocked))*(float)pilot.Stats["knockbackTaken"];
            pilot.Grounded=pilot.GroundAttached=false;
        }
    }
}
