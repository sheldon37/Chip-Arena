namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Card icons for the native-only chips that the pinned source icon map (Resources/Icons.json "chips", exported from the
    /// source client) does not list, so they no longer all fall back to the cpu icon. Each maps to a line icon already in the
    /// pinned set (Icons.json "icons"); the chip's content data, its content hash and revision are untouched (presentation
    /// only). Runtime/NativeIcons.Chip resolves the source map first, then this, then cpu.
    /// The five movement chips (Core/NativeMovementContent, Astra, PR #282): Stride Circuit footprints (speed), Momentum Cell
    /// gauge (speed and stamina regeneration), Air Step move-up (one more air jump), Acrobat rotate-cw (an air jump and a longer
    /// dodge), Skybound feather (two more air jumps and lighter gravity). Then the ROUNDS packs' chips (NativeRoundsLooks.Icon).
    /// </summary>
    public static class NativeChipIcons
    {
        public static readonly string[] Ids={"stride","momentum-cell","air-step","acrobat","skybound"};
        public static readonly string[] Icons={"footprints","gauge","move-up","rotate-cw","feather"};
        /// <summary>The icon for chip `id`, or null when it has none here (the source map or the cpu fallback applies).</summary>
        public static string Native(string id)
        {
            if(id==null)return null;
            for(int i=0;i<Ids.Length;i++)if(Ids[i]==id)return Icons[i];
            // The ROUNDS adaptation packs' 138 chips (Core/NativeRoundsLooks): one pinned icon each by its main effect.
            return NativeRoundsLooks.Icon(id);
        }
    }
}
