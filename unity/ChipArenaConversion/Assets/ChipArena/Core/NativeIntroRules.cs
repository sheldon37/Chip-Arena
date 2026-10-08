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
        public static double NameAt(string style)=>style=="dev-siren"?7.2678:style=="dev-emperor"?5.95:style=="dev-matrix"?6.25:style=="dev-ansatsuken"?5.45:3.2;
    }
}
