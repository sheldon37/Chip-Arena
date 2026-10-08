using System;
namespace ChipArena.UnityConversion
{
    public static class NativeIntroRules
    {
        public static string Style(NativeSeat seat)
        {
            var access=seat.access??NativeAccess.Guest;double now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();if(seat.spec=="siren"&&(seat.AuthorityBoss||access.Allows("weapon:siren",now)))return "dev-siren";
            if(seat.overrideIntro&&!string.IsNullOrEmpty(seat.intro)&&(!seat.intro.StartsWith("dev-")||access.Allows("intro:"+seat.intro,now)))return seat.intro;
            if(seat.spec=="emperor")return "dev-emperor";if(seat.spec=="ansatsuken")return "dev-ansatsuken";if(seat.spec=="leet")return "dev-matrix";return "weapon-"+seat.spec;
        }
        public static double Duration(string style)=>style=="dev-siren"?9.0678:style=="dev-emperor"?7.8:style=="dev-matrix"?8:style=="dev-ansatsuken"?7.2:4.8;
        // Presentation only (the HUD's name card): shared/intros.ts introNameAt, SIREN_INTRO.nameAt 6.4261 for the Siren.
        public static double NameAt(string style)=>style=="dev-siren"?6.4261:style=="dev-emperor"?5.95:style=="dev-matrix"?6.25:style=="dev-ansatsuken"?5.45:3.2;
        // The overlay's own clock length (shared/intros.ts introDuration). The Siren's source intro is 7.2678 s; Duration keeps the
        // native phase length unchanged (9.0678 s for a PvP Siren intro), which is a gameplay-timing difference left for review.
        public static double OverlayDuration(string style)=>style=="dev-siren"?7.2678:Duration(style);
        // shared/intros.ts INTRO_STYLES display names.
        public static string StyleName(string style)
        {
            switch(style)
            {
                case "dev-siren":return "DEV · Black Siren Protocol";case "dev-emperor":return "DEV · Emperor’s Oath";case "dev-ansatsuken":return "DEV · Rain Warrior";case "dev-matrix":return "DEV · L33T Throne";
                case "weapon-sword":return "Flash Draw";case "weapon-daggers":return "Twin Phantom";case "weapon-greatsword":return "Colossus Oath";case "weapon-shield":return "Bastion Drop";
                case "weapon-pistol":return "Deadeye";case "weapon-rifle":return "Longwatch";case "weapon-machinegun":return "Belt Rattle";case "weapon-fire":return "Cinder Crown";
                case "weapon-water":return "Tidal Serpent";case "weapon-lightning":return "Thunderstep";case "weapon-darkness":return "Eventide Gate";
                case "vector-slide":return "Vector Slide";case "signal-spin":return "Signal Spin";case "arc-salute":return "Arc Salute";case "pulse-wave":return "Pulse Wave";
                default:return "Weapon Signature";
            }
        }
    }
}
