namespace ChipArena.UnityConversion
{
    // A stand-in until claude/pick-flourish lands: its per-weapon selection animations define Flourishes() (drawn after every
    // weapon card of a frame). The Blind Pick (N choices) screen already calls it. The integrator deletes this file (and its
    // .meta) when merging both branches.
    public static partial class NativeWeaponCard
    {
        public static void Flourishes(){}
    }
}
