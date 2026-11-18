namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The generated hub's plan (docs/EXPEDITION-OVERHAUL.md, the shared contract between the pod crash and the rules): Astra's
    /// A0 creates the table with the landing field and owns every field of it after A1; this is the one constant the crash reads
    /// before A0 lands. ExitReach: how far in front of its pod centre a pilot's capsule (its slot, the pod record) waits; 0 until
    /// A1 moves the slots to the ramp foot (2.4 m), so today the slot is the pod centre. The drawn pod centre is the record minus
    /// forward(yaw in degrees) times ExitReach (NativePodDrop.Centre). Presentation reads it; no rule here.
    /// </summary>
    public static partial class NativeExpeditionHubPlan
    {
        public const float ExitReach=0;
    }
}
