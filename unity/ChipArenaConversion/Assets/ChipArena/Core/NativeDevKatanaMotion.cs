using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The DEV Katana's animation (Core NativeDevMotions; owner 2026-11-11: "Great Sword and Katana", then "lets put them on DEV classes"):
    /// Grruzam's Powerful Sword Animation pack's katana set (its clips are named by their files, `M_katana_Blade@...`), baked into the local
    /// motion bank like the other packs' and played as the Katana's moves are (Core NativeBladeMotion: each move a clip window, its strike on
    /// the rules' hit): the stance, the stride, the jumps, the dodge roll, the reactions, the knockdown and its rolling get-up, the four cuts,
    /// the rushing cut, the storm and the plunging slam; the guard is the same pack's great sword guard (Guard). The pack has no saya, so the iai and its sheathing (the blade drawn from
    /// and slid home into the saya the left hand carries) and the counter's paired critical (its victim's clip played with it) stay the Frank
    /// Slash Pack's. The class plays the Katana's rules, so the windows are fitted to the Katana's timing. Presentation only.
    /// </summary>
    public static class NativeDevKatanaMotion
    {
        private const string K="M_katana_Blade@";
        public const string Idle=K+"Idle_ver_B",Jump=K+"Jump_Start",Flip=K+"Double_Jump_Start",Fall=K+"Jump_Loop",Land=K+"Jump_End",
            Roll=K+"Dodge_Front",Knockdown=K+"KnockDown_Front_ver_A",RollBack=K+"KnockDown_Front_BW_Rolling_StandUp";
        /// <summary>The guard and its blocked hit: the same pack's great sword guard (owner 2026-11-11: "Dev Katana block holds katana behind
        /// them?"). The katana set's own guard (`Revenge_Guard_Loop`, `_Accept`) is a back-turned block: its start turns the body about 172
        /// degrees away and holds the blade down behind it (the pack's own katana lies there too, about 125 degrees off the torso's facing),
        /// so in the game the pilot turned its back with the blade behind. The great sword guard faces ahead (the torso 26 degrees off the
        /// clip's forward; the katana stance's is 58) with the hands raised by the head and the blade hanging down across the front to the
        /// left (the game holds the katana within 5 degrees of the great sword); both clips are the DEV Greatsword's, already in the bank.</summary>
        public const string Guard=NativeDevGreatswordMotion.Guard,GuardHit=NativeDevGreatswordMotion.GuardHit;
        /// <summary>The katana set's back-turned guard, no longer played (kept named for the clip checks).</summary>
        public const string BackGuard=K+"Revenge_Guard_Loop",BackGuardHit=K+"Revenge_Guard_Accept";
        /// <summary>The four cuts: the pack's own four-hit chain (each part takes up where the last left off): a diagonal cut down across the
        /// front, a rising backhand, a low spinning cut (the step in), the finisher's high cut.</summary>
        /// The owner's pick (2026-11-12): the pack's five-hit chain's first four parts, in place (`Attack_5Combo_1..4_Inplace`).
        public static readonly string[] Cuts={K+"Attack_5Combo_1_Inplace",K+"Attack_5Combo_2_Inplace",K+"Attack_5Combo_3_Inplace",K+"Attack_5Combo_4_Inplace"};
        /// <summary>The rushing cut (a sprint's attack: the pack's lunging dash cut), the storm (cuts swept low all round, then a rising cut: the
        /// launch) and the plunging slam (the air attack: a downward chop that ends with the blade low in front; run to just short of its
        /// strike through the drop, its strike on the landing).</summary>
        public const string Rush=K+"Dash_Attack_ver_B",Storm=K+"Skill_D",Plunge=K+"Jump_Attack_Combo_4_Attach_ZeroHeight";
        /// <summary>The counter (owner 2026-11-12: "counter → Revenge_Guard_Attack_ver_A", in place of the Frank Slash Pack's paired critical):
        /// the pack's counter-cut out of its guard, its blow on the pair's; and its foe's clip (the pack has no partner clip): the katana set's
        /// big hit reaction from the front, from the blow on (the rules hold the foe and throw it back 1.2 m, as for the Katana).</summary>
        public const string Counter=K+"Revenge_Guard_Attack_ver_A",CounterHit=K+"Damage_Front_Big_ver_A";
        /// <summary>The class's own entrance's clip (Core NativeDevIntros; owner 2026-11-12: "entrance → Intro").</summary>
        public const string Intro=K+"Intro";
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
                // The owner's picked cuts: a forehand across the front, a backhand, a falling diagonal; the fourth a flurry of cuts and kicks,
                // its last high sweep (right to left at head height) the finisher's strike, so the move shows its close.
                case K+"Attack_5Combo_1_Inplace":return new NativeBladeMotion.Window(0,.31f,1f);
                case K+"Attack_5Combo_2_Inplace":return new NativeBladeMotion.Window(0,.3f,.83f);
                case K+"Attack_5Combo_3_Inplace":return new NativeBladeMotion.Window(0,.43f,1.03f);
                case K+"Attack_5Combo_4_Inplace":return new NativeBladeMotion.Window(1.2f,1.97f,2.33f);
                // The released chain's cuts (the bank keeps them; no longer played).
                case K+"Attack_4Combo_1_Inplace":return new NativeBladeMotion.Window(0,.4f,1.1f);
                case K+"Attack_4Combo_2_Inplace":return new NativeBladeMotion.Window(0,.37f,1.13f);
                case K+"Attack_4Combo_3_Inplace":return new NativeBladeMotion.Window(0,.26f,.93f);
                case K+"Attack_4Combo_4_Inplace":return new NativeBladeMotion.Window(0,.1f,.83f);
                case K+"Jump_Attack_Combo_1_ZeroHeight":return new NativeBladeMotion.Window(0,.33f,1.17f);
                // The counter: out of its back-turned guard it spins round into a rising cut (its strike, on the pair's blow), lunges on through
                // and settles. Its foe's big hit reaction from the blow on (the window's start before the clip's: its first frame held to the blow),
                // at its own pace to the hold's end.
                case Counter:return new NativeBladeMotion.Window(0,.4f,1.47f);
                case CounterHit:return new NativeBladeMotion.Window(-NativeKatana.CounterHit,0,NativeKatana.CounterHold-NativeKatana.CounterHit);
                case Rush:return new NativeBladeMotion.Window(0,.43f,.83f);
                case Storm:return new NativeBladeMotion.Window(.2f,.53f,1.25f);
                // The owner's picked plunge: a cut down and a turn in the air, then the landing that drives the blade into the floor at the
                // right (its strike, on the impact: the cut and the turn play through the drop).
                case Plunge:return new NativeBladeMotion.Window(0,.74f,.77f);
                default:return new NativeBladeMotion.Window(float.NaN,float.NaN,float.NaN);
            }
        }
        /// <summary>A clip's strike (its window's, s into it) for the bank as it is read (the pack's clips carry no Hit event): the plunging
        /// slam's is where the blade meets the floor. False for any other clip.</summary>
        public static bool Strike(string clip,out float at){at=Is(clip)?WindowOf(clip).Hit:float.NaN;if(float.IsNaN(at)){at=0;return false;}return true;}
        /// <summary>The clips the DEV Katana plays that its set's fields do not name (the bake's controller carries them): its own moves and the
        /// Frank Slash Pack's it keeps (the iai and the sheathing; the Katana's paired critical, which its rules still name).</summary>
        public static readonly string[] Extra={Rush,Storm,Counter,CounterHit,Intro,NativeBladeMotion.KatanaIai,NativeBladeMotion.KatanaSheathe,NativeBladeMotion.KatanaCritical,NativeBladeMotion.KatanaCriticalHit};
    }
}
