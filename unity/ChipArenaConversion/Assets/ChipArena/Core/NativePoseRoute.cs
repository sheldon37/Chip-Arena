namespace ChipArena.UnityConversion
{
    /// <summary>Which pose path a presented pilot takes (CapturedAnimation.Player.ActorPose). Presentation only.</summary>
    public enum PoseRoute{Generic,CapturedMelee,Magic,Fighter}
    /// <summary>
    /// character-animation.ts sampleCharacter by weapon class: captured melee (sword, daggers, greatsword, shield) runs the
    /// melee pipeline; every magic-class pilot, the four schools and L33t H4x (MagicPose.School), walks on the magic gait
    /// with the staff; the DEV fighting styles run their own layers; guns, the Siren and first-person view models take the
    /// generic path. Only the four sword sets ever sample the sword set's idle, walk, run, cut, block or stagger.
    /// </summary>
    public static class NativePoseRoute
    {
        public static PoseRoute For(Actor a,bool firstPerson)
        {
            if(a==null||firstPerson)return PoseRoute.Generic;
            if(a.MagicVisual==0&&a.Weapon==WeaponId.Sword&&MeleeMotion.Handles(a.SourceSpec))return PoseRoute.CapturedMelee;
            if(MagicPose.School(a)>0)return PoseRoute.Magic;
            if(NativeFighterPose.Handles(a.SourceSpec))return PoseRoute.Fighter;
            return PoseRoute.Generic;
        }
        /// <summary>
        /// The generic path's sword-set layers (captured cuts, block, stagger and the blade floor guard): never for a
        /// magic-class pilot, who carries the staff (a first-person L33t H4x casts with MagicPose.Apply like the schools), nor
        /// for the Siren, who carries no blade and whose motion writes every joint (motion.ts): the floor guard bent her right
        /// claw up through the Slam and the Rush while the left one reached the floor.
        /// </summary>
        public static bool SwordLayers(Actor a)=>a!=null&&a.Weapon==WeaponId.Sword&&MagicPose.School(a)==0&&a.SourceSpec!="siren";
    }
}
