using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Katana's names and numbers (docs/KATANA.md): the owner's public melee class of 2026-11-10 ("Make very high quality designed
    /// katana ... Use the new animation pack"). A long curved blade held two-handed, on the Frank Slash Pack's katana set: four cuts that flow
    /// (the third steps in, the fourth cleaves), a rushing draw cut on a sprint, Q the iai (the blade sheathed, a dash along the aim, and the
    /// line it cut splits a moment later), E a storm of cuts all round that ends in a rising launch, and a counter after a timed parry: the
    /// next cut is a paired critical (the foe held through it). The rules (Runtime NativeKatanaAuthority.cs) and the presentation both read
    /// these, so a number lives in one place. Pure.
    /// </summary>
    public static class NativeKatana
    {
        public const string Spec="katana",Name="Katana";
        public const string Cut="katana-cut",Rush="katana-rush",Iai="katana-iai",Storm="katana-storm",Counter="katana-counter";
        /// <summary>The class's own entrance (Core NativeKatanaIntro).</summary>
        public const string Entrance="katana-crimson-moon";
        // Hit kinds (NativeBladeHit.Kind): a cut, the finisher, the rush, the iai's pass (marked) and its split, a storm cut, the storm's
        // launch, the counter, the draw (the iai's flash at the start of the dash) and the sheathe (the click as it splits).
        public const string HitCut="k-cut",HitFinisher="k-finish",HitRush="k-rush",HitMark="k-mark",HitSplit="k-split",HitStorm="k-storm",HitLaunch="k-launch",HitCounter="k-counter",HitDraw="k-draw",HitSheathe="k-sheathe";
        // The chips' (Core NativeKatanaChips): a bleed's drip, a crescent wave's cut, a petal's cut, a still-water readiness.
        public const string HitBleed="k-bleed",HitWave="k-wave",HitPetal="k-petal",HitFocus="k-focus";
        public const int Slots=2;
        public static readonly string[] SlotKeys={"Q","E"};
        /// <summary>Cooldowns (s) in slot order: Q (the iai) from its split, E (the storm) from its end.</summary>
        public static readonly float[] Cooldown={8,10};

        // ---- the combo ----
        /// <summary>The four cuts: the wind-up to the hit (s), the whole move (s), damage, reach (m), half arc (rad) and how far the cut steps in
        /// (m, along the facing through the wind-up: the third lunges, the fourth leans in).</summary>
        public static readonly float[] CutHit={.16f,.14f,.18f,.22f},CutLength={.42f,.4f,.5f,.66f},CutDamage={18,18,22,30},CutReach={2.7f,2.7f,2.8f,3f},CutArc={1.1f,1.1f,1.3f,.9f},CutLunge={0,0,1.2f,.6f};
        public const float ComboWindow=.45f,FinisherPush=3,FinisherStun=1.5f;
        public const int Steps=4;
        // ---- the rush (a sprint's attack) ----
        public const float RushWind=.08f,RushTime=.16f,RushReach=5,RushWidth=1.25f,RushDamage=22,RushPush=4,RushRecover=.3f;
        // ---- the iai (Q) ----
        /// <summary>Sheathed and gathered for IaiDraw s, a dash IaiReach m along the aim (flat, a wall stops it) in IaiDash s; everyone within
        /// IaiWidth of the line is marked, and IaiSplit s after the dash the line splits: each marked one takes IaiDamage and staggers.</summary>
        public const float IaiDraw=.32f,IaiDash=.12f,IaiReach=7.5f,IaiWidth=1.3f,IaiSplit=.45f,IaiDamage=46,IaiStagger=.6f,IaiRecover=.5f;
        // ---- the storm (E) ----
        /// <summary>A wind-up, then StormCuts cuts all round (StormRadius m) every StormEvery s, StormDamage each; the last throws them up
        /// (StormLift m/s). The wielder moves at StormSlow of its pace through it.</summary>
        public const float StormWind=.3f,StormEvery=.13f,StormRadius=2.9f,StormDamage=8,StormLift=6.5f,StormRecover=.35f,StormSlow=.45f;
        public const int StormCuts=6;
        // ---- the counter (after a timed parry) ----
        /// <summary>A timed parry landed (a blow inside the F parry's window; not one held on the raised guard) opens CounterWindow s; the next left click within it (the foe within CounterReach in front) is the
        /// counter: the paired critical, the foe held from the wind-up to CounterHold s, CounterDamage at the blow (CounterHit s in).</summary>
        public const float CounterWindow=1,CounterReach=3.2f,CounterHit=.55f,CounterHold=1.25f,CounterDamage=38,CounterStand=1.6f;

        public static string AbilityName(string id)
        {
            switch(id){case Cut:return "Flowing Cuts";case Rush:return "Rushing Draw";case Iai:return "Moonlit Draw";case Storm:return "Petal Storm";case Counter:return "Moon Reflection";default:return "";}
        }
        /// <summary>The icon an ability's card draws (the shared set's).</summary>
        public static string Icon(string id)=>id==Iai?"moon":id==Storm?"wind":id==Counter?"eclipse":id==Rush?"move-right":"sword";
        public static bool IsCast(string k)=>k==Cut||k==Rush||k==Iai||k==Storm||k==Counter;
        public static bool IsHit(string k)=>k==HitCut||k==HitFinisher||k==HitRush||k==HitMark||k==HitSplit||k==HitStorm||k==HitLaunch||k==HitCounter||k==HitDraw||k==HitSheathe||k==HitBleed||k==HitWave||k==HitPetal||k==HitFocus;
        /// <summary>The iai's dash direction from the aim (`x`, `z`): flat, along the look.</summary>
        public static Vector3 DashDir(float x,float z){var d=new Vector3(x,0,z);float l=d.Length();return l>1e-4f?d/l:Vector3.UnitZ;}
        /// <summary>Whether the blade is sheathed in the record at `time`, for the model: from the split's click (the sheathing seats it there,
        /// Core NativeKatanaSaya.SheatheClick) to the iai's end, the blade resting in the saya. Through the draw it is in the hand: the pack's
        /// iai takes it out of the saya the left hand carries before the clip's draw stance (NativeKatanaSaya.IaiDraw), so it slides out of the
        /// saya there; seated earlier it jumped up to 60 cm into it before the click.</summary>
        public static bool Sheathed(NativeBladeKit k,float time)
        {
            if(k==null||k.CastKind!=Iai||time<k.CastAt)return false;
            return k.SplitAt>-50&&time>=k.SplitAt-.01f&&time<Math.Max(k.CastUntil,k.SplitAt+.05f);
        }
    }
}
