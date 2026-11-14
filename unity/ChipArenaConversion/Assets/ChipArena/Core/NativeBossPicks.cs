using System;
using System.Collections.Generic;
using System.Globalization;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Boss Battle's opening chips (the owner, 2026-11-07: "Everyone gets their own selection screen. There is a 30 second countdown for all players
    /// to get their 5 chips. If a player finishes selecting, it will bring them to a 'Waiting for x/x players to select their chips' / 'Still selecting:
    /// (NAME)'"). Every pilot picks their five on their own screen at once; one shared countdown of <see cref="Seconds"/> s runs on the authority's
    /// ticks (the arena's clock stands still while they choose) and is replicated. When it runs out the fight starts, and any chip a pilot hasn't
    /// chosen waits for them: C opens it in the fight, as in an Expedition (the owner, again: "If all chips aren't selected, players can select rest
    /// when pressing C in boss fight"). The clock is shown big at the top centre with a glow, urgent in its last <see cref="Urgent"/> s ("make the text
    /// a lot more visible and well designed with glow. Like top center of screen, and show urgency when down to last 10 seconds"). Pure words and
    /// numbers; Runtime NativeBossBattle holds the rule, NativeBossPicksScreen and NativeBossPicksClock draw it.
    /// </summary>
    public static class NativeBossPicks
    {
        public const float Seconds=30;public const int Count=5;
        /// <summary>The opening's picks carry this source: they outlive the countdown and say "BOSS BATTLE · CHIP n / 5" wherever they're chosen.</summary>
        public const string Source="opening";
        /// <summary>The last seconds the clock counts down urgently (red, a beat and a tick each second).</summary>
        public const float Urgent=10;
        public static bool IsUrgent(float left)=>left>0&&left<=Urgent;
        /// <summary>How long the number shown has been up (0 just as it changes, toward 1 as the next one comes): the urgent beat's clock.</summary>
        public static float SinceTick(float left)
        {
            if(float.IsNaN(left)||left<=0)return 1;double s=Math.Ceiling(left);return (float)Math.Max(0,Math.Min(1,s-left));
        }
        /// <summary>The beat on each new second while urgent: 1 as the number changes, dying away (0 when calm).</summary>
        public static float Beat(float left)=>IsUrgent(left)?(float)Math.Exp(-SinceTick(left)*7):0;
        /// <summary>The shown second (the clock's number, rounded up; 0 at the end), for the tick that marks each one.</summary>
        public static int Second(float left)=>float.IsNaN(left)||left<=0?0:(int)Math.Ceiling(Math.Min(left,599));
        /// <summary>The prompt for chips still to choose in the fight: "2 CHIPS LEFT TO CHOOSE" ("1 CHIP ...").</summary>
        public static string Leftover(int n)=>Math.Max(0,n).ToString(CultureInfo.InvariantCulture)+(n==1?" CHIP":" CHIPS")+" LEFT TO CHOOSE";
        /// <summary>The countdown as the screens show it: whole seconds left, rounded up ("0:23"; "0:00" at the end).</summary>
        public static string Clock(float left)
        {
            int s=float.IsNaN(left)||left<=0?0:(int)Math.Ceiling(Math.Min(left,599));
            return (s/60).ToString(CultureInfo.InvariantCulture)+":"+(s%60).ToString("00",CultureInfo.InvariantCulture);
        }
        /// <summary>The waiting screen's line: "Waiting for 2/4 players to select their chips" (still choosing / everyone choosing).</summary>
        public static string Waiting(int still,int total)
        {
            still=Math.Max(0,still);total=Math.Max(still,total);
            return "Waiting for "+still.ToString(CultureInfo.InvariantCulture)+"/"+total.ToString(CultureInfo.InvariantCulture)+(total==1?" player":" players")+" to select their chips";
        }
        /// <summary>Who is still choosing: "Still selecting: Ana, Bo"; the list is cut at `max` names with "+n more".</summary>
        public static string Still(IReadOnlyList<string> names,int max=4)
        {
            if(names==null||names.Count==0)return "";var shown=new List<string>();for(int i=0;i<names.Count&&i<max;i++)shown.Add(string.IsNullOrEmpty(names[i])?"PILOT":names[i]);
            string s="Still selecting: "+string.Join(", ",shown);if(names.Count>max)s+=" +"+(names.Count-max).ToString(CultureInfo.InvariantCulture)+" more";return s;
        }
        /// <summary>The countdown after one authority tick of `dt` while anyone still chooses; never below 0.</summary>
        public static float Tick(float left,float dt)=>Math.Max(0,left-Math.Max(0,dt));
        /// <summary>The countdown's fill (1 full, 0 out).</summary>
        public static float Fill(float left)=>Math.Max(0,Math.Min(1,left/Seconds));
    }
}
