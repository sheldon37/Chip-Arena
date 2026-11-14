namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The killcam's cause line (killcam-hud.ts CAUSES; the replay's card and the final killcam's lower third). A blade's kill names what
    /// it did to the body (owner 2026-11-12: "Yes, fix the label", for kills that read "Blunt force"): from the kill's own slice plan
    /// (NativeMeleeSlice, the cut the body shows), "Beheaded" for the head, "Cut in two" through the body, "Dismembered" for a limb, and
    /// "Slashed" when the cut took no part off (a slit throat, a kill shown whole). The blades: the sword, the greatsword and the daggers
    /// (their DEV classes too) and the blade classes (the Katana, the Fangs, the Glaive, thrown or not). Blunt melee (the shield, the
    /// fighters, the mallet) keeps "Blunt force", as does a blade class's blow that is no cut (the DEV Katana's kick, owner 2026-11-13:
    /// its kill crushes as the shield's); a melee class's kill by a move that was no blow (a tick, a burst) "Special attack"; the
    /// guns "Direct hit"; no killer "Environment". Pure.
    /// </summary>
    public static class NativeKillCause
    {
        public const string Beheaded="Beheaded",CutInTwo="Cut in two",Dismembered="Dismembered",Slashed="Slashed",Blunt="Blunt force",Special="Special attack",Direct="Direct hit",Environment="Environment";
        /// <summary>Whether a weapon's melee kills are a blade's.</summary>
        public static bool Blade(string spec)=>spec=="sword"||spec=="greatsword"||spec=="daggers"||NativeBladeCuts.Is(spec);
        /// <summary>The cause: `killer` whether there is one, its weapon's class and spec, whether the death was a melee one, and the kill's
        /// slice plan (null: none was planned).</summary>
        public static string Of(bool killer,string weaponClass,string spec,bool melee,SlicePlan plan)
        {
            if(!killer)return Environment;
            bool blade=Blade(spec);
            if(weaponClass!="melee"&&!blade)return Direct;
            if(!blade)return melee?Blunt:Special;
            // A blade's cut: what the plan takes (as NativeMeleeBisect cuts it, or the joints it detaches with gore off).
            if(plan!=null&&plan.Any)
            {
                // A blade class's blow that is no cut (the DEV Katana's kick) crushes as a blunt weapon's: a blunt kill.
                if(plan.Kind==SliceKind.Crush)return Blunt;
                if(plan.Count==0)return Slashed;
                if(plan.Kind==SliceKind.Cleave)return CutInTwo;
                if(plan.Kind==SliceKind.Chop&&spec!="daggers"&&spec!=NativeFangs.Spec)return CutInTwo;
                int j=plan.Joints[0];if(plan.Kind==SliceKind.Chop){for(int i=0;i<plan.Count;i++)if(Limb(plan.Joints[i]))return Dismembered;return Beheaded;}
                return j==CapturedRig.Head?Beheaded:Limb(j)?Dismembered:CutInTwo;
            }
            // A blade's kill that cut nothing (no blow seen, a kill effect showing its own ending): the blow's, or a move that was none.
            return melee?Slashed:Special;
        }
        private static bool Limb(int j)=>j==CapturedRig.LArm||j==CapturedRig.RArm||j==CapturedRig.LElbow||j==CapturedRig.RElbow;
    }
}
