using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The v2 name card's legibility (Core NativeIntroCardInk, drawn by Runtime NativeIntroCardScreen): on white snow and on a sunlit
    /// mid-tan floor, every registered intro's name has NameMinimum contrast against what is right behind its glyphs (the backing and
    /// the outline), and its kicker, the team lines and the intro line LineMinimum. A dark set keeps its look: the backing darkens it by
    /// a few hundredths at most, and no light line is lifted. The backing has no ring (its alpha only falls from the middle).
    /// </summary>
    internal static class IntroCardChecks
    {
        private static int count;private static void Check(bool ok,string what){if(!ok)throw new Exception("Intro card: "+what);count++;}
        public static void Run()
        {
            count=0;float worstName=float.MaxValue,worstLine=float.MaxValue;string worstNameAt="",worstLineAt="";int lifted=0;
            void Line(string what,Vector3 srgb)
            {
                var shown=NativeIntroCardInk.Legible(srgb);float c=NativeIntroCardInk.Worst(shown);if(shown!=srgb)lifted++;
                Check(c>=NativeIntroCardInk.LineMinimum-1e-3f,what+" reads on a bright set (contrast "+c.ToString("0.00")+", wants "+NativeIntroCardInk.LineMinimum+")");
                if(c<worstLine){worstLine=c;worstLineAt=what;}
            }
            int intros=0;
            foreach(var e in NativeIntroCatalog.Entries)
            {
                if(!(e.Make() is INativeIntroV2 v2))continue;
                // the card a second into its reveal (the first time it is shown, plus one second)
                var card=default(NativeIntroCardState);float at=-1;var s=(NativeEntranceScript)v2;
                for(int i=0;i<=e.Duration*30;i++){float t=s.Clock(i/30f);v2.Card(t,ref card);if(card.Shown&&card.Age>=1){at=t;break;}}
                if(at<0)continue;intros++;
                float name=NativeIntroCardInk.Worst(NativeIntroCardInk.Srgb(card.Ink));
                Check(name>=NativeIntroCardInk.NameMinimum,e.Id+": its name reads on white snow and a sunlit tan floor (contrast "+name.ToString("0.00")+", wants "+NativeIntroCardInk.NameMinimum+")");
                if(name<worstName){worstName=name;worstNameAt=e.Id;}
                Line(e.Id+": its kicker",NativeIntroCardInk.Srgb(card.Glow));
            }
            Check(intros>0,"a registered intro shows its card");
            foreach(int hex in NativeStatusVisuals.TeamColors)Line("the team line in #"+hex.ToString("x6"),NativeIntroCardInk.Hex(hex));
            Line("the intro line",NativeIntroCardInk.Hex(NativeIntroCardInk.LineHex));
            // the dark set keeps its look; the light lines are never lifted
            var dark=NativeIntroCardInk.Dark;var backed=NativeIntroCardInk.Backed(dark);float change=Math.Max(Math.Abs(dark.X-backed.X),Math.Max(Math.Abs(dark.Y-backed.Y),Math.Abs(dark.Z-backed.Z)));
            Check(change<=.03f,"the backing darkens a dark set by at most .03 (by "+change.ToString("0.000")+")");
            foreach(var light in new[]{new Vector3(.96f,.98f,1),NativeIntroCardInk.Hex(0x9fd8ff),NativeIntroCardInk.Hex(0xf5db70)})Check(NativeIntroCardInk.Legible(light)==light,"a light line keeps its colour");
            // and the backing is a soft disc: its alpha only falls from the middle (no ring)
            var a=NativeIntroCardInk.BackingAlpha;var r=NativeIntroCardInk.BackingAt;
            for(int i=1;i<a.Length;i++)Check(a[i]<=a[i-1]&&r[i]>r[i-1],"the backing's alpha only falls from its middle");
            Check(a[a.Length-1]==0&&a[0]==1,"the backing fades out to nothing at its edge");
            Console.WriteLine("PASS intro card: "+count+" checks ("+intros+" intro card"+(intros==1?"":"s")+" on white snow and a sunlit tan floor: the worst name contrast "+worstName.ToString("0.0")+" ("+worstNameAt+"), the worst small line "
                +worstLine.ToString("0.0")+" ("+worstLineAt+"), "+lifted+" line colour"+(lifted==1?"":"s")+" lifted; a dark set darkened by "+change.ToString("0.000")+")");
        }
    }
}
