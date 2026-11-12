using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The DEV Katana's animation (Core NativeDevMotions; owner 2026-11-11: "Great Sword and Katana", then "lets put them on DEV classes"):
    /// Grruzam's Powerful Sword Animation pack's katana set (its clips are named by their files, `M_katana_Blade@...`), baked into the local
    /// motion bank like the other packs' and played as the Katana's moves are (Core NativeBladeMotion: each move a clip window, its strike on
    /// the rules' hit): the stance, the stride, the jumps, the guard, the dodge roll, the reactions, the knockdown and its rolling get-up, the
    /// four cuts, the rushing cut, the storm and the plunging slam. The pack has no saya, so the iai and its sheathing (the blade drawn from
    /// and slid home into the saya the left hand carries) and the counter's paired critical (its victim's clip played with it) stay the Frank
    /// Slash Pack's. The class plays the Katana's rules, so the windows are fitted to the Katana's timing. Presentation only.
    /// </summary>
    public static class NativeDevKatanaMotion
    {
        private const string K="M_katana_Blade@";
        public const string Idle=K+"Idle_ver_B",Jump=K+"Jump_Start",Flip=K+"Double_Jump_Start",Fall=K+"Jump_Loop",Land=K+"Jump_End",
            Guard=K+"Revenge_Guard_Loop",GuardHit=K+"Revenge_Guard_Accept",Roll=K+"Dodge_Front",
            Knockdown=K+"KnockDown_Front_ver_A",RollBack=K+"KnockDown_Front_BW_Rolling_StandUp";
        /// <summary>The four cuts: the pack's own four-hit chain (each part takes up where the last left off): a diagonal cut down across the
        /// front, a rising backhand, a low spinning cut (the step in), the finisher's high cut.</summary>
        public static readonly string[] Cuts={K+"Attack_4Combo_1_Inplace",K+"Attack_4Combo_2_Inplace",K+"Attack_4Combo_3_Inplace",K+"Attack_4Combo_4_Inplace"};
        /// <summary>The rushing cut (a sprint's attack: the pack's lunging dash cut), the storm (cuts swept low all round, then a rising cut: the
        /// launch) and the plunging slam (the air attack: a downward chop that ends with the blade low in front; run to just short of its
        /// strike through the drop, its strike on the landing).</summary>
        public const string Rush=K+"Dash_Attack_ver_B",Storm=K+"Skill_D",Plunge=K+"Jump_Attack_Combo_1_ZeroHeight";
        /// <summary>Hit reactions from the front, back, left and right.</summary>
        public static readonly string[] Reactions={K+"Damage_Front_Small_ver_A",K+"Damage_Back_Small_ver_A",K+"Damage_Left_Small_ver_A",K+"Damage_Right_Small_ver_A"};
        /// <summary>The stride, the blade low in the right hand (the pack's B versions; its A versions carry the katana in the left hand, off
        /// the game's hold): the jog's eight directions for the walks and for the runs off the forward line, the run ahead, the fast run the
        /// sprint. The root-motion takes give each clip's travel for the stride's rate.</summary>
        public static readonly string[] Runs={K+"Run_Root_ver_B",K+"Jogging_8Way_verB_FR45_Root",K+"Jogging_8Way_verB_R90_Root",K+"Jogging_8Way_verB_BR45_Root",
            K+"Jogging_8Way_verB_B_Root",K+"Jogging_8Way_verB_BL45_Root",K+"Jogging_8Way_verB_L90_Root",K+"Jogging_8Way_verB_FL45_Root"};
        public static readonly string[] Walks={K+"Jogging_8Way_verB_F_Root",K+"Jogging_8Way_verB_FR45_Root",K+"Jogging_8Way_verB_R90_Root",K+"Jogging_8Way_verB_BR45_Root",
            K+"Jogging_8Way_verB_B_Root",K+"Jogging_8Way_verB_BL45_Root",K+"Jogging_8Way_verB_L90_Root",K+"Jogging_8Way_verB_FL45_Root"};
        public const string Sprint=K+"Run_Fast_Root_ver_B";
        /// <summary>Whether a clip is one of the pack's katana clips.</summary>
        public static bool Is(string clip)=>clip!=null&&clip.StartsWith(K,StringComparison.Ordinal);
        /// <summary>Each move's clip window (Core NativeBladeMotion.Window: where it starts, its strike and where it ends, s into the clip),
        /// measured on the body frame by frame at 30 a second with the game's blade in the hand and the pack's own beside it: a cut's strike is
        /// where its blade turns fastest with the tip crossing the front; the end is where the pose is back in its stance (or, the storm,
        /// just after its rising cut); the plunge's strike is where the blade reaches the floor in front.</summary>
        public static NativeBladeMotion.Window WindowOf(string clip)
        {
            switch(clip)
            {
                case K+"Attack_4Combo_1_Inplace":return new NativeBladeMotion.Window(0,.4f,1.1f);
                case K+"Attack_4Combo_2_Inplace":return new NativeBladeMotion.Window(0,.37f,1.13f);
                case K+"Attack_4Combo_3_Inplace":return new NativeBladeMotion.Window(0,.26f,.93f);
                case K+"Attack_4Combo_4_Inplace":return new NativeBladeMotion.Window(0,.1f,.83f);
                case Rush:return new NativeBladeMotion.Window(0,.43f,.83f);
                case Storm:return new NativeBladeMotion.Window(.2f,.53f,1.25f);
                case Plunge:return new NativeBladeMotion.Window(0,.33f,1.17f);
                default:return new NativeBladeMotion.Window(float.NaN,float.NaN,float.NaN);
            }
        }
        /// <summary>A clip's strike (its window's, s into it) for the bank as it is read (the pack's clips carry no Hit event): the plunging
        /// slam's is where the blade meets the floor. False for any other clip.</summary>
        public static bool Strike(string clip,out float at){at=Is(clip)?WindowOf(clip).Hit:float.NaN;if(float.IsNaN(at)){at=0;return false;}return true;}
        /// <summary>The clips the DEV Katana plays that its set's fields do not name (the bake's controller carries them): its own moves and the
        /// Frank Slash Pack's it keeps (the iai, the sheathing, the paired critical and its victim's clip).</summary>
        public static readonly string[] Extra={Rush,Storm,NativeBladeMotion.KatanaIai,NativeBladeMotion.KatanaSheathe,NativeBladeMotion.KatanaCritical,NativeBladeMotion.KatanaCriticalHit};
    }
}
