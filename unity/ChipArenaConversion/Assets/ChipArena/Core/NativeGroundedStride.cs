using System;

namespace ChipArena.UnityConversion
{
    // The guns', casters' and Bombadier's grounded stride (owner 2026-11-23: "I want to fix the running/sprinting animations for weapons.
    // Currently, its very 'hoppy' looking. Lets make it look more realistic (Mostly on guns and magic) think melee is fine?";
    // docs/RUN-REALISTIC.md). Measured on the model (the motion bank's records posed on the body, and every frame of a run in play): the
    // animation pack's gun, bow, staff and unarmed runs ahead bound like a cartoon (the pelvis rising and falling 19-31 cm a stride, both
    // feet off the floor 56-75% of it and up to 37-54 cm high), played 1.3-2 times their pace to keep up with the rules' 6.8 m/s run, and
    // the sprint (9.18 m/s) was the same run capped at twice its pace, so its feet slid. Its own side and back runs are low already (7-8 cm;
    // the staff's and the unarmed set's are not: 11-24 cm). The covered sets now stride on mocap: a real fast run ahead and a sprint (the
    // Frank gunslinger's Run01 and Run03_Faster, 4.8-5.4 cm a stride), the Frank 8-way run's diagonals ahead (6.8 cm), the gun set's own
    // low side, back and back-diagonal runs and its walks. Each clip's travel is its planted feet's (measured on the body), so the rate the
    // game plays it at keeps the feet planted at the rules' speeds. Over the legs: the guns keep the Tactical Shooter Pack's carry (Core
    // NativeTacticalMotion), the casters carry the staff steady (the Frank mage's run, mirrored into the staff's left fist) and the
    // Bombadier keeps its own pumping fists (RunUpper). A bank without these clips (the builder before the bank lands, CoreTests' synthetic
    // banks) keeps each set's own stride (NativeVisualMotion.Stride). Melee, the bow, the fighters, the Top Dog and the DEV classes keep
    // theirs. Presentation only: no move speed, sprint rule, hitbox, timing or wire is read differently or written.
    public static class NativeGroundedStride
    {
        /// <summary>The run ahead (6.0 m/s on its planted feet: the rules' 6.8 m/s at 1.13 times its pace, four steps a second) and the
        /// sprint (6.28 m/s: the rules' 9.18 at 1.46).</summary>
        public const string RunAhead="Frank_RPG_Gunslinger_Run01_Velocity", Sprint="Frank_RPG_Gunslinger_Run03_Faster_Velocity";
        /// <summary>The runs ahead to the right and the left (the Frank 8-way run, already the Katana's and the Fangs').</summary>
        public const string RunAheadRight="Frank_RPG_Katana@Velocity_8Way_Run_FR", RunAheadLeft="Frank_RPG_Katana@Velocity_8Way_Run_FL";
        /// <summary>A caster's staff carried over the stride: the Frank mage's run, mirrored (its staff in the right fist, the game's in
        /// the left), its upper body at the stride's phase.</summary>
        public const string StaffCarry="Frank_RPG_Mage_8Way_Run_F"+NativeVisualMotion.Mirror;
        /// <summary>The Bombadier's fists over the stride: its own unarmed run's upper body (a bundle in each fist, pumping).</summary>
        public const string FistsCarry="Unarmed-Run-Forward";
        /// <summary>The grounded stride in NativeVisualStride's order: the runs (ahead, ahead-right, right, back-right, back, back-left,
        /// left, ahead-left), the walks likewise, the sprint.</summary>
        public static readonly string[] Stride={RunAhead,RunAheadRight,"2Hand-Shooting-Run-Right","2Hand-Shooting-Run-Backward-Right","2Hand-Shooting-Run-Backward","2Hand-Shooting-Run-Backward-Left","2Hand-Shooting-Run-Left",RunAheadLeft,
            "2Hand-Shooting-Strafe-Forward","2Hand-Shooting-Strafe-Forward-Right","2Hand-Shooting-Strafe-Right","2Hand-Shooting-Strafe-Backward-Right","2Hand-Shooting-Strafe-Backward","2Hand-Shooting-Strafe-Backward-Left","2Hand-Shooting-Strafe-Left","2Hand-Shooting-Strafe-Forward-Left",
            Sprint};
        /// <summary>The clips the grounded stride adds to the motion bank (the rest are already in it): their names, and their travel on
        /// the body (x right, z ahead, m/s) as their planted feet show it.</summary>
        public static readonly string[] Added={RunAhead,Sprint,StaffCarry};
        /// <summary>The sets that stride on it (by the set's spec: the guns' "rifle" covers the machine gun, "staff" every school).</summary>
        public static bool Covers(string setSpec)=>setSpec=="pistol"||setSpec=="rifle"||setSpec=="staff"||setSpec==NativeBombs.Spec;
        /// <summary>Every clip it plays (the stride and both carries).</summary>
        public static readonly string[] Clips=All();
        private static string[] All(){var l=new System.Collections.Generic.List<string>(Stride);l.Add(StaffCarry);l.Add(FistsCarry);return l.ToArray();}
    }
}
