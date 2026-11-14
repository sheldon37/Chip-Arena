using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Expedition mutators that change how the run looks rather than its rules (Claude, 2026-11-02; docs/EXPEDITION-DEPLOYMENT.md
    /// "Mutator looks"), read from the run's ticket (ExpeditionRunBundle.Mutators: host and guests alike):
    ///  - DARKNESS: the street, district and practical lights out (the look's light slots, lamp halos and bulbs, the kit's lit
    ///    windows and signs dimmed to <see cref="DarkGlow"/>), the ambient at <see cref="DarkAmbient"/> and the moon at
    ///    <see cref="DarkKey"/>; the fog darkened with it. Muzzle flashes, blasts, armour and weapon glows still light the streets:
    ///    the pilots' own light is all there is.
    ///  - WHITEOUT: a pale linear fog from <see cref="WhiteStart"/> to <see cref="WhiteEnd"/> m over everything, the sky included,
    ///    and the world's markers (pieces, quota marks, names and bars) hidden past <see cref="WhiteSight"/> m.
    ///  - BIG HEADS: every pilot's and zombie's head drawn <see cref="HeadScale"/> times its size (the rules double the head's hit
    ///    region).
    /// Pure.
    /// </summary>
    public static class NativeMutatorLook
    {
        public const float DarkAmbient=.4f,DarkKey=.35f,DarkFog=.35f,DarkGlow=.12f,DarkShadow=.22f,DarkLight=.6f,DarkSky=.25f;
        public const float WhiteStart=2,WhiteEnd=25,WhiteSight=25;public const int WhiteHex=0xC9D2DC;
        public const float HeadScale=2.2f;
        public static bool Dark(string[] ids)=>NativeExpeditionMutators.Has(ids,"darkness");
        public static bool White(string[] ids)=>NativeExpeditionMutators.Has(ids,"whiteout");
        public static bool BigHeads(string[] ids)=>NativeExpeditionMutators.Has(ids,"big-heads");
        /// <summary>A world marker's reach under WHITEOUT (`white`): no farther than the fog lets the eye see.</summary>
        public static float Sight(bool white,float range)=>white?Math.Min(range,WhiteSight):range;
        /// <summary>Whether a world marker at `distance2` (squared metres) shows under WHITEOUT (`white`).</summary>
        public static bool Within(float distance2,bool white)=>!white||distance2<=WhiteSight*WhiteSight;

        /// <summary>The station's payout line for a run's ids (NativeExpeditionMutators.Payout: 1 + .25 a Bane + .10 a Chaos), e.g.
        /// "RUN PAYOUT ×1.35"; each value's text is made once.</summary>
        public static string PayoutLabel(string[] ids)
        {
            lock(payoutLabels){if(ReferenceEquals(ids,payoutFor)&&payoutText!=null)return payoutText;}
            double x=NativeExpeditionMutators.Payout(ids);int key=(int)Math.Round(x*100);
            lock(payoutLabels){if(!payoutLabels.TryGetValue(key,out var s)){s="RUN PAYOUT ×"+(key/100.0).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture);payoutLabels[key]=s;}payoutFor=ids;payoutText=s;return s;}
        }
        private static readonly Dictionary<int,string> payoutLabels=new Dictionary<int,string>();private static string[] payoutFor;private static string payoutText;
        public const string PayoutNote="+25% A BANE · +10% A CHAOS";
    }
}
