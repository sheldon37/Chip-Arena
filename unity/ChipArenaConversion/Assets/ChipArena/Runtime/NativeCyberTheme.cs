using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The cyber UI palette (owner 2026-10-12: "We will start going a cyber theme~ So cyan instead of orange"). One source of
    // truth for the restyled menu, panel screens and HUD: cyan on deep navy/indigo with a magenta counter-accent. Hex values are
    // 0xRRGGBB for NativeUi.Hex; Color properties take an alpha.
    public static class NativeCyberTheme
    {
        public const int CyanHex=0x22e6ff,CyanSoftHex=0x7ff3ff,CyanDeepHex=0x0a8fb3,MagentaHex=0xff3dcf,VioletHex=0x8b5cff;
        public const int VoidHex=0x05070f,NavyHex=0x0a1024,IndigoHex=0x131a3d,PanelHex=0x0d1430,EdgeHex=0x2a3a6b;
        public const int InkHex=0xe8fbff,MutedHex=0x8aa6c4,DimHex=0x4f6687,DangerHex=0xff4d6d,WarnHex=0xffc24d,GoodHex=0x4dffa6;
        // The anime style's night (docs/MAP-STYLE-ANIME.md section 6; Core NativeMenuStyle.NightHex, DuskHex): the full-screen
        // covers (the transition shutters, the loading screen's base, the Expeditions screen's veil) in the cool shade family of
        // the maps and the rooftop instead of near-black; Void and Navy again under the `--ca-map-style legacy` command line.
        public static Color Night(float a=1)=>NativeUi.Hex(NativeLighting.LegacyStyle?VoidHex:NativeMenuStyle.NightHex,a);
        public static Color Dusk(float a=1)=>NativeUi.Hex(NativeLighting.LegacyStyle?NavyHex:NativeMenuStyle.DuskHex,a);
        public static Color Cyan(float a=1)=>NativeUi.Hex(CyanHex,a);
        public static Color CyanSoft(float a=1)=>NativeUi.Hex(CyanSoftHex,a);
        public static Color CyanDeep(float a=1)=>NativeUi.Hex(CyanDeepHex,a);
        public static Color Magenta(float a=1)=>NativeUi.Hex(MagentaHex,a);
        public static Color Violet(float a=1)=>NativeUi.Hex(VioletHex,a);
        public static Color Void(float a=1)=>NativeUi.Hex(VoidHex,a);
        public static Color Navy(float a=1)=>NativeUi.Hex(NavyHex,a);
        public static Color Indigo(float a=1)=>NativeUi.Hex(IndigoHex,a);
        public static Color Panel(float a=1)=>NativeUi.Hex(PanelHex,a);
        public static Color Edge(float a=1)=>NativeUi.Hex(EdgeHex,a);
        public static Color Ink(float a=1)=>NativeUi.Hex(InkHex,a);
        public static Color Muted(float a=1)=>NativeUi.Hex(MutedHex,a);
        public static Color Dim(float a=1)=>NativeUi.Hex(DimHex,a);
        public static Color Danger(float a=1)=>NativeUi.Hex(DangerHex,a);
        public static Color Warn(float a=1)=>NativeUi.Hex(WarnHex,a);
        public static Color Good(float a=1)=>NativeUi.Hex(GoodHex,a);
    }
}
