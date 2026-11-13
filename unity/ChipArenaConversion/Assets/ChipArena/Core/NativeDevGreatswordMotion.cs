using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The DEV Greatsword's animation (Core NativeDevMotions; owner 2026-11-11: "Great Sword and Katana", then "lets put them on DEV classes"):
    /// Grruzam's Powerful Sword Animation pack's great sword set (its clips are named by their files, `M_Big_Sword@...`), baked into the local
    /// motion bank like the other packs' and played by the generic set (Core NativeVisualMotion): the stance, the shoulder carry's stride, the
    /// jumps, the guard, the dodge roll, the reactions, the knockdown and its rolling get-up, the combo, the leaping cleave, the jump attack,
    /// both kit moves and the techniques. The pack's clips carry no Hit event (the bake's Hit is a clip's middle), so each strike is measured on
    /// the body here (Strike) and the bank takes it as it is read. The class plays the Greatsword's rules, so the clips are fitted to the
    /// Greatsword's timing, reach, steps and arcs. Presentation only.
    /// </summary>
    public static class NativeDevGreatswordMotion
    {
        private const string G="M_Big_Sword@";
        public const string Idle=G+"Idle",Jump=G+"Jump_Start",Flip=G+"Double_Jump_Start",Fall=G+"Jump_Loop",Land=G+"Jump_End",
            Guard=G+"Revenge_Guard_Loop",GuardHit=G+"Revenge_Guard_Accept",Roll=G+"Dodge_Front",
            Knockdown=G+"KnockDown_Front_ver_A",RollBack=G+"KnockDown_Front_BW_Rolling_StandUp";
        /// <summary>The combo by attack index from 1, the owner's pick (2026-11-12): the pack's seven-hit chain's first four parts, in place
        /// (`Attack_7Combo_1..4_Inplace`): a diagonal cleave from high on the right, a great circling swing up the left and over (crossing the
        /// front left to right), an overhead from behind the right shoulder down to the left, and an overhead circle up the right and down the
        /// left. The rules sweep the first cut right to left, the next left to right and so on: the first three cross the front that way at
        /// their strikes; the fourth's overhead comes over right to left against the rules' left to right (the owner's pick: its strike is
        /// the overhead's top, where it is fastest). The picker's `Attack_7Combo_4` (6.9 s) is this same swing followed by five seconds
        /// standing; the in-place take is the swing alone.</summary>
        public static readonly string[] Cuts={G+"Attack_7Combo_1_Inplace",G+"Attack_7Combo_2_Inplace",G+"Attack_7Combo_3_Inplace",G+"Attack_7Combo_4_Inplace"};
        /// <summary>The sprint attack (the owner's pick 2026-11-12: `Dash_Attack_ver_B`, a lunging diagonal cut down from the left, played in
        /// place: the rules move the pilot, the bake leaves the clip's root travel out), the jump attack's downward chop, Earthsplitter's
        /// lunging slam onto the floor, the Whirlwind's level spinning sweep (held through the spin, WhirlHold), Sunder (the owner's pick:
        /// `Skill_F`, a low swing round behind and a great slam down to the front right, in place), Reaping Sweep's spin (the low sweep the
        /// old combo's fourth cut was) and the vow's crouch (into it, then held).</summary>
        public const string Cleave=G+"Dash_Attack_ver_B",JumpCut=G+"Jump_Attack_Combo_3_ZeroHeight",Slam=G+"Skill_H",Spin=G+"Skill_E",
            Overhead=G+"Skill_F",Reap=G+"Attack_4Combo_1B_Inplace",Crouch=G+"Idle_To_Crouch",Crouched=G+"Crouch_Idle";
        /// <summary>The class's own entrance's clip (Core NativeDevIntros; owner 2026-11-12: "entrance → Intro").</summary>
        public const string Intro=G+"Intro";
        /// <summary>Where the Whirlwind holds the spinning sweep through the spin (a share of it, as NativeVisualMotion.WhirlHold for the old
        /// set): 0.66 s of its 1.83, the arms out and the blade level at the waist, 1.8 m out.</summary>
        public const float SpinHold=.36f;
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
        /// cut's where its tip crosses the pilot's facing sweeping as the rules' arc does (the hit window's middle points there; an overhead's,
        /// where it is fastest); a slam's where the blade stops on the floor. The old combo's clips keep theirs (the bank keeps them). False
        /// for any other clip.</summary>
        public static bool Strike(string clip,out float at)
        {
            switch(clip)
            {
                case G+"Attack_7Combo_1_Inplace":at=.565f;return true;
                case G+"Attack_7Combo_2_Inplace":at=.485f;return true;
                case G+"Attack_7Combo_3_Inplace":at=.6f;return true;
                case G+"Attack_7Combo_4_Inplace":at=.4f;return true;
                case G+"Attack_4Combo_2_Inplace":at=.696f;return true;
                case G+"Skill_B":at=.393f;return true;
                case G+"Attack_4Combo_1B_Inplace":at=.586f;return true;
                case Cleave:at=.39f;return true;
                case JumpCut:at=.62f;return true;
                case Slam:at=.61f;return true;
                case Overhead:at=.63f;return true;
                default:at=0;return false;
            }
        }
    }
}
