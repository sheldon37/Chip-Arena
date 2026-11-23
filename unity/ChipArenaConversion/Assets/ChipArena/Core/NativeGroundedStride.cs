using System;

namespace ChipArena.UnityConversion
{
    // The guns', casters' and Bombadier's grounded stride (owner 2026-11-23: "I want to fix the running/sprinting animations for weapons.
    // Currently, its very 'hoppy' looking. Lets make it look more realistic (Mostly on guns and magic) think melee is fine?";
    // docs/RUN-REALISTIC.md). Measured on the model (the motion bank's records posed on the body, and every frame of a run in play): the
    // animation pack's gun, bow, staff and unarmed runs ahead bound like a cartoon (the pelvis rising and falling 19-31 cm a stride, both
    // feet off the floor 58-75% of it and up to 38-54 cm high), played 1.3-1.6 times their pace to keep up with the rules' 6.8 m/s run, and
    // the sprint (9.18 m/s) was the same run capped at twice its pace, so its feet slid. The gun set's own side, back and back-diagonal runs
    // are low already (7-8 cm; the staff's and the unarmed set's are not: 11-24 cm). The covered sets now stride on mocap ahead: the Frank
    // 8-way run ahead and its diagonals ahead (7 cm a stride, a low flight, 1.9 m steps on its planted feet), which also carries the sprint
    // (the rules' 9.18 m/s at 1.6 times its pace: 4.8 steps a second, a sprinter's), over the gun set's own low side, back and back-diagonal
    // runs and its walks. Each added clip's travel is its planted feet's (measured on the body: the pack's root motion is 25-40% off it) and
    // its planted feet sit on the floor, so the game's foot locks take them and the rate keeps them planted at the rules' speeds. Over the
    // legs: the guns keep the Tactical Shooter Pack's carry (Core NativeTacticalMotion), the casters carry the staff steady (the Frank mage's
    // run, mirrored into the staff's left fist) and the Bombadier keeps its own pumping fists (RunUpper). A bank without these clips (the
    // builder before the bank lands, CoreTests' synthetic banks) keeps each set's own stride (NativeVisualMotion.Stride). Melee, the bow, the
    // fighters, the Top Dog and the DEV classes keep theirs. Presentation only: no move speed, sprint rule, hitbox, timing or wire is read
    // differently or written.
    public static class NativeGroundedStride
    {
        /// <summary>The run ahead, also the sprint (5.70 m/s on its planted feet: the rules' 6.8 m/s run at 1.19 times its pace, 3.6 steps a
        /// second; their 9.18 sprint at 1.61, 4.8 steps a second); the runs ahead to the right and the left (their feet's travel, (2.79, 4.83)
        /// and (-2.44, 4.99) m/s). The Frank gunslinger's 8-way run (the Katana's and the Fangs' legs, recorded again for the guns with their
        /// feet on the floor and their own travel: the Katana's records are melee's and stay as released).</summary>
        public const string RunAhead="Frank_RPG_Gunslinger_8Way_Run_F", Sprint=RunAhead, RunAheadRight="Frank_RPG_Gunslinger_8Way_Run_FR", RunAheadLeft="Frank_RPG_Gunslinger_8Way_Run_FL";
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
        public static readonly string[] Added={RunAhead,RunAheadRight,RunAheadLeft,StaffCarry};
        /// <summary>The sets that stride on it (by the set's spec: the guns' "rifle" covers the machine gun, "staff" every school).</summary>
        public static bool Covers(string setSpec)=>setSpec=="pistol"||setSpec=="rifle"||setSpec=="staff"||setSpec==NativeBombs.Spec;
        /// <summary>Whether every run, walk and the sprint of `stride` travels in `bank` (at least MinTravel m/s on the body): the rate
        /// that keeps the feet planted needs it.</summary>
        public static bool Travels(string[] stride,NativeVisualMotionClips.Library bank)
        {
            if(stride==null||bank==null)return false;
            foreach(var c in stride){if(!bank.Clips.TryGetValue(c,out var k)||Math.Sqrt(k.SpeedX*k.SpeedX+k.SpeedZ*k.SpeedZ)<MinTravel)return false;}
            return true;
        }
        public const float MinTravel=.5f;
        /// <summary>Every clip it plays (the stride and both carries), once each.</summary>
        public static readonly string[] Clips=All();
        private static string[] All(){var l=new System.Collections.Generic.List<string>();foreach(var c in Stride)if(!l.Contains(c))l.Add(c);l.Add(StaffCarry);l.Add(FistsCarry);return l.ToArray();}
    }
}
