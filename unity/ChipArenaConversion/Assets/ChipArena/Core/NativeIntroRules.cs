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
        // Presentation only below: what the client stages for the replicated style; match timing keeps that style.
        // The entrances the client can stage (player-intros.ts; every one has a baked stage and intro clip).
        public static readonly string[] Entrances={"neon-drop","vector-slide","signal-spin","arc-salute","pulse-wave","weapon-sword","weapon-daggers","weapon-shield","weapon-greatsword","weapon-pistol","weapon-rifle","weapon-machinegun","weapon-fire","weapon-water","weapon-lightning","weapon-darkness","dev-emperor","dev-ansatsuken","dev-matrix","dev-siren"};
        // A style with no entrance (a Siren seat without its DEV grant gets "weapon-siren") is staged as the source's own
        // fallback, 'neon-drop' (shared/intros.ts effectiveIntro), instead of failing the intro frame.
        public static string Entrance(string style)=>style!=null&&Array.IndexOf(Entrances,style)>=0?style:"neon-drop";
        // The weapon model the intro rig holds, or null. player-intros.ts builds a weapon into the bare intro rig for a weapon
        // intro (WEAPON_INTROS[style].spec: the model "weapon-"+spec is the style itself; daggers fill both hands) and for the
        // Emperor's Oath (buildWeapon(weapon/offWeapon,'emperor'), shown by poseEmperor; weapon-emperor holds both gauntlets).
        // Every other entrance shows empty hands: the poses, the Rain Warrior and the L33T Throne hide r.weapon and the Siren
        // hides it. The L33T Throne's staff is the stage's own prop, grabbed at 4 s.
        public static string IntroWeapon(string style)=>style=="dev-emperor"?"weapon-emperor":style!=null&&style.StartsWith("weapon-",StringComparison.Ordinal)&&Array.IndexOf(Entrances,style)>=0?style:null;
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
    /// <summary>
    /// Presentation only: an entrance-stage light from the baked scene bank as a Unity built-in light. The bank holds three's
    /// physically based lights (linear colour, intensity; Lambert diffuse is albedo/PI in three r180, albedo alone in Unity's
    /// built-in Lambert), so the light Unity applies must be the linear colour x intensity / PI. The source's exposure .92 and
    /// ACES toe roughly cancel at the stage's levels and are not applied. Unity applies a light as colour x intensity in Gamma
    /// space, linear(colour) x intensity with linear intensity, and linear(colour x intensity) with the built-in default in
    /// Linear space: the raw values made the Siren cell's bulb (24) about 700 once Linear was pinned, a flat white room.
    /// </summary>
    public static class NativeEntranceLight
    {
        public const double Diffuse=1/Math.PI;
        // Unity's sRGB transfer (Mathf.GammaToLinearSpace), with its power-2.2 branch above 1 for light colour x intensity.
        public static double ToLinear(double c)=>c<=.04045?c/12.92:c<1?Math.Pow((c+.055)/1.055,2.4):Math.Pow(c,2.2);
        public static double ToGamma(double c)=>c<=.0031308?c*12.92:c<1?1.055*Math.Pow(c,1/2.4)-.055:Math.Pow(c,1/2.2);
        /// <summary>The linear light Unity applies for a Light colour channel c and intensity.</summary>
        public static double Applied(double c,double intensity,bool linearSpace,bool linearIntensity)=>!linearSpace?c*intensity:linearIntensity?ToLinear(c)*intensity:ToLinear(c*intensity);
        /// <summary>Light.color (channels 0..1) and Light.intensity for a bank light (linear rgb, intensity).</summary>
        public static void Light(double r,double g,double b,double intensity,bool linearSpace,bool linearIntensity,out double cr,out double cg,out double cb,out double unity)
        {
            double k=Math.Max(0,intensity)*Diffuse,tr=Math.Max(0,r)*k,tg=Math.Max(0,g)*k,tb=Math.Max(0,b)*k;
            if(linearSpace&&!linearIntensity){tr=ToGamma(tr);tg=ToGamma(tg);tb=ToGamma(tb);}
            unity=Math.Max(tr,Math.Max(tg,tb));if(!(unity>0)){cr=cg=cb=unity=0;return;}
            cr=tr/unity;cg=tg/unity;cb=tb/unity;
            if(linearSpace&&linearIntensity){cr=ToGamma(cr);cg=ToGamma(cg);cb=ToGamma(cb);}
        }
        /// <summary>A RenderSettings ambient channel (authored in sRGB, linearised by Unity in Linear space) for a bank ambient sum.</summary>
        public static double Ambient(double linear,bool linearSpace){double t=Math.Max(0,linear)*Diffuse;return linearSpace?ToGamma(t):t;}
    }
}
