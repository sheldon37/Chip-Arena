using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The greatsword on the model (owner 2026-11-11: "Great Sword and Katana"): Grruzam's Powerful Sword Animation pack's great sword set
    /// (its clips are named by their files, `M_Big_Sword@...`), baked into the local motion bank like the other packs' and played by the
    /// generic set (Core NativeVisualMotion): the stance, the shoulder carry's stride, the jumps, the guard, the dodge roll, the reactions,
    /// the knockdown and its rolling get-up, the combo, the leaping cleave, the jump attack, both kit moves and the techniques. The pack's
    /// clips carry no Hit event (the bake's Hit is a clip's middle), so each strike is measured on the body here (Strike) and the bank takes
    /// it as it is read. Presentation only: the rules' timing, reach, steps and arcs are unchanged; the clips are fitted to them.
    /// </summary>
    public static class NativeGreatswordMotion
    {
        private const string G="M_Big_Sword@";
        public const string Idle=G+"Idle",Jump=G+"Jump_Start",Flip=G+"Double_Jump_Start",Fall=G+"Jump_Loop",Land=G+"Jump_End",
            Guard=G+"Revenge_Guard_Loop",GuardHit=G+"Revenge_Guard_Accept",Roll=G+"Dodge_Front",
            Knockdown=G+"KnockDown_Front_ver_A",RollBack=G+"KnockDown_Front_BW_Rolling_StandUp";
        /// <summary>The combo by attack index from 1. The rules sweep the first cut right to left, the next left to right and so on (each cut's
        /// blade crosses the facing that way at its strike): a diagonal cleave from high on the right, a level backhand, a wide forehand
        /// sweep, a spinning low sweep. Each starts from the stance and settles back to it.</summary>
        public static readonly string[] Cuts={G+"Attack_7Combo_1_Inplace",G+"Attack_4Combo_2_Inplace",G+"Skill_B",G+"Attack_4Combo_1B_Inplace"};
        /// <summary>The leaping cleave (the sprint attack: the game's hop, the slam on its landing), the jump attack's downward chop, Earthsplitter's
        /// lunging slam onto the floor, the Whirlwind's level spinning sweep (held through the spin, WhirlHold), Sunder's leaping overhead,
        /// Reaping Sweep's spin, and the vow's crouch (into it, then held).</summary>
        public const string Cleave=G+"Attack_4Combo_4_Inplace",JumpCut=G+"Jump_Attack_Combo_3_ZeroHeight",Slam=G+"Skill_H",Spin=G+"Skill_E",
            Overhead=G+"Skill_J_Inplace",Reap=G+"Attack_4Combo_1B_Inplace",Crouch=G+"Idle_To_Crouch",Crouched=G+"Crouch_Idle";
        /// <summary>Hit reactions from the front, back, left and right.</summary>
        public static readonly string[] Reactions={G+"Damage_Front_Small_ver_A",G+"Damage_Back_Small_ver_A",G+"Damage_Left_Small_ver_A",G+"Damage_Right_Small_ver_A"};
        /// <summary>The stride: the shoulder carry (the pack's B versions, whose blade lies within about 15 degrees of where the game holds it; the
        /// A versions drag it 40-50 degrees off): the jog its walks in eight directions and its runs off the forward line, the run ahead, the
        /// fast run the sprint. The root-motion takes give each clip's travel for the stride's rate.</summary>
        public static readonly string[] Runs={G+"Run_ver_B_Root",G+"Jogging_8Way_verB_FR45_Root",G+"Jogging_8Way_verB_R90_Root",G+"Jogging_8Way_verB_BR45_Root",
            G+"Jogging_8Way_verB_B_Root",G+"Jogging_8Way_verB_BL45_Root",G+"Jogging_8Way_verB_L90_Root",G+"Jogging_8Way_verB_FL45_Root"};
        public static readonly string[] Walks={G+"Jogging_8Way_verB_F_Root",G+"Jogging_8Way_verB_FR45_Root",G+"Jogging_8Way_verB_R90_Root",G+"Jogging_8Way_verB_BR45_Root",
            G+"Jogging_8Way_verB_B_Root",G+"Jogging_8Way_verB_BL45_Root",G+"Jogging_8Way_verB_L90_Root",G+"Jogging_8Way_verB_FL45_Root"};
        public const string Sprint=G+"Run_Fast_ver_B_Root";
        /// <summary>Whether a clip is one of the pack's.</summary>
        public static bool Is(string clip)=>clip!=null&&clip.StartsWith(G,StringComparison.Ordinal);
        /// <summary>A clip's strike (s into it), measured on the body frame by frame at 30 a second with the game's own blade in the hand: a
        /// cut's where its tip crosses the pilot's facing sweeping as the rules' arc does (the hit window's middle points there); a slam's where
        /// the blade stops on the floor. False for any other clip.</summary>
        public static bool Strike(string clip,out float at)
        {
            switch(clip)
            {
                case G+"Attack_7Combo_1_Inplace":at=.565f;return true;
                case G+"Attack_4Combo_2_Inplace":at=.696f;return true;
                case G+"Skill_B":at=.393f;return true;
                case G+"Attack_4Combo_1B_Inplace":at=.586f;return true;
                case Cleave:at=.34f;return true;
                case JumpCut:at=.62f;return true;
                case Slam:at=.61f;return true;
                case Overhead:at=.95f;return true;
                default:at=0;return false;
            }
        }
    }
}
