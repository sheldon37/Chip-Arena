namespace ChipArena.UnityConversion
{
    /// <summary>
    /// What each half of a split corpse's model draws (Runtime NativeVisualCutPresentation.cs; owner 2026-11-13: "On the new gib, it
    /// appears the head/face appear for a moment when the player dies?"). The model's cut is worked out on a worker while the halves
    /// hold on the blade: until it is on, the first half draws the whole body and the second waits hidden; a cut that fails stays so.
    /// Once it is on, each half draws its own side. What lies on one side of the cut (the head with its face gear, hat and plates)
    /// is so drawn exactly once at every stage: on the whole body, or on the half the cut left it on; never on a body that lost it,
    /// never twice, never bare. (The cut used to put the head's gear and hat on the hidden half while the whole body drew its bare face.)
    /// Pure; presentation only.
    /// </summary>
    public static class NativeSplitShow
    {
        /// <summary>The model's cut: being made on the worker, on the halves, or failed.</summary>
        public enum Stage{Making,On,Failed}
        /// <summary>Whether half `half` (0 the first, 1 the second) is drawn.</summary>
        public static bool Shown(int half,Stage stage)=>half==0||stage==Stage.On;
        /// <summary>Whether half `half` draws the whole body: the first, until the cut is on (or for good once it failed).</summary>
        public static bool Whole(int half,Stage stage)=>half==0&&stage!=Stage.On;
        /// <summary>Whether half `half` wears what lies on side `side` of the cut (the head's face gear and hat, a plate): the whole
        /// body all of it, a cut half its own side, a hidden half nothing.</summary>
        public static bool Wears(int half,int side,Stage stage)=>Shown(half,stage)&&(Whole(half,stage)||stage==Stage.On&&half==side);
    }
}
