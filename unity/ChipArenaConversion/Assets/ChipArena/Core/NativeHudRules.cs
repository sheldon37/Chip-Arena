using System;
using System.Collections.Generic;
using System.Globalization;

namespace ChipArena.UnityConversion
{
    // client/hitmarker.ts HitmarkerTrack and Hitmarker.update: my fresh hit (under .4 s old) on an enemy pilot, or in PvE on a
    // Siren add, marks the crosshair. A proc kind (PROC_KINDS, the Hit's Detail) or a burn/poison tick takes the small dot
    // marker for .20 s (scaled .68, no corner tips); any other hit the crisp X for .34 s (popping from 1.168x, red for a
    // headshot). A crisp mark under .22 s old is not replaced by a dot, and a dot not by another dot. Ticks also list their
    // element (fire, poison) under the marker for .7 s, merging with the last .5 s. The opacity fades over the last .16 s.
    public sealed class NativeHitmarker
    {
        public const float CrispLife=.34f,DotLife=.20f,Fade=.16f,Hold=.22f,Fresh=.4f,DotScale=.68f,Pop=.08f,PopScale=2.1f,ElementMerge=.5f,ElementLife=.7f;
        // .combat-hitmarker (42 px, white, red headshot) with its 100-unit viewBox: four strokes (16% to 33% of the box from the
        // centre on each axis) and the corner tips (39% to 42%), 3 units wide with square caps; element icons (15 px, gap 4)
        // 3 px under the box, flame #ffab59 and poison #91e47a, 1.8-unit strokes; drop-shadow(0 1px 2px #0009).
        public const float Box=42,StrokeIn=.16f,StrokeOut=.33f,TipIn=.39f,TipOut=.42f,Stroke=.03f,Icon=15,IconGap=4,IconTop=3,IconStroke=1.8f;
        public const int Ink=0xffffff,Headshot=0xff394d,FireColor=0xffab59,PoisonColor=0x91e47a,ShadowColor=0x000000;public const float ShadowAlpha=.6f,ShadowY=1,ShadowBlur=2;
        // hitmarker.ts flame and poison icon paths (24-unit viewBox).
        public const string FlamePath="M12 2c1 6-6 6-3 11 2 1 3-2 3-4 5 4 6 7 4 10-4 5-12 2-12-3 0-5 6-7 8-14Z",PoisonPath="M8 3h8m-6 0v6l-5 9a2 2 0 0 0 2 3h10a2 2 0 0 0 2-3l-5-9V3M8 15h8M10 18h.01M14 17h.01";
        public const int NoElement=0,Fire=1,Poison=2;
        public enum Target{Other,Enemy,Ally,Add}
        // What the event's target id is: a pilot (enemy or on my side, me included) or a PvE add; anything else is Other.
        public delegate Target Classify(int target);
        public struct Mark{public bool Visible,Shown,Headshot,Dot;public float Age,Opacity,Scale;public int Elements,First,Second;}
        private int last,generation=-1,first,second,count;private float time=-1,at=-10,elementAt=-10;private bool headshot,dot;
        public void Clear(){last=0;time=-1;generation=-1;at=elementAt=-10;headshot=dot=false;count=first=second=0;}
        // hitmarker.ts PROC_KINDS (NativeEventSounds.Proc also takes Unity's hyphenated spellings).
        public static bool Proc(string kind)=>NativeEventSounds.Proc(kind);
        // enemyHit: my damage on a pilot that is not me nor on my side (a pilot's hit exists in the source only above .05), or
        // addHit: in PvE, my damage on a Siren add (any amount).
        public static bool EnemyHit(in ArenaEvent e,int local,bool pve,Classify classify)
        {
            if(local<=0||e.Source!=local||e.Target<=0||!NativeEventFeedback.Damages(e.Kind))return false;
            switch(classify(e.Target))
            {
                case Target.Enemy:return e.Target!=local&&NativeEventFeedback.SourceHit(e.Kind,e.Amount);
                case Target.Add:return pve&&e.Amount>0;
                default:return false;
            }
        }
        public Mark Update(List<ArenaEvent> events,float now,int gen,int local,bool pve,Classify classify)
        {
            if(now<time||generation!=gen)Clear();time=now;generation=gen;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];if(e.Id<=last)continue;last=e.Id;if(now-e.Time>Fresh||!EnemyHit(e,local,pve,classify))continue;
                bool tick=e.Kind==EventKind.BurnTick||e.Kind==EventKind.PoisonTick,isDot=tick||Proc(e.Detail);
                if(!isDot||now-at>Hold){at=e.Time;headshot=!isDot&&e.Kind==EventKind.Headshot;dot=isDot;}
                if(tick){int element=e.Kind==EventKind.BurnTick?Fire:Poison;if(!(now-elementAt<ElementMerge))count=0;if(count==0){first=element;count=1;}else if(first!=element&&!(count==2&&second==element)){second=element;count=2;}elementAt=e.Time;}
            }
            float age=now-at,life=dot?DotLife:CrispLife;var m=new Mark{Age=age,Dot=dot,Headshot=headshot,Visible=age<life};
            if(now-elementAt<ElementLife&&count>0){m.Elements=count;m.First=first;m.Second=count>1?second:NoElement;}
            m.Shown=m.Visible||m.Elements>0;m.Opacity=m.Visible?Math.Min(1,(life-age)/Fade):0;m.Scale=dot?DotScale:1+Math.Max(0,Pop-age)*PopScale;return m;
        }
    }

    // client/sprint-hud.ts sprintStatus beside the resources (in the fight, not paused, alive): SPRINTING with its
    // chevron, else SPRINT DEPLETED · RELEASE W once stamina ran out mid-sprint. style.css .sprint-status inside
    // .healthbox .status-tags (8 px above the box, .status-tags span's 2/5 px padding, dark fill and currentColor border).
    public static class NativeSprintTag
    {
        public const int None=0,Sprinting=1,Exhausted=2;
        public const string SprintingText="SPRINTING",ExhaustedText="SPRINT DEPLETED · RELEASE W",Chevron="»";
        public const int SprintColor=0xf8db94,ExhaustedColor=0xed9b80,Fill=0x0c1b22,GlowColor=0xe7af4b;public const float FillAlpha=0xdd/255f,GlowAlpha=0x55/255f,GlowBlur=10;
        public const float Size=10,ExhaustedSize=9,Tracking=.1f,ExhaustedTracking=.04f,LineHeight=1.4f,PadX=5,PadY=2,Border=1,Above=8,Gap=6,ChevronSize=17,ChevronLine=12;
        // sprint-chevron .65s ease-in-out infinite alternate: translateX(-2px) at .55 opacity to translateX(2px) at full.
        public const float ChevronPeriod=.65f,ChevronShift=2,ChevronDim=.55f;
        public static int State(bool active,float hp,bool sprinting,bool exhausted)=>!active||hp<=0?None:sprinting?Sprinting:exhausted?Exhausted:None;
        public static string Text(int state)=>state==Sprinting?SprintingText:state==Exhausted?ExhaustedText:"";
        // The chevron's eased phase (0 at translateX(-2px), 1 at +2px) at `time` seconds into the animation.
        public static float ChevronPhase(float time)
        {
            float cycle=time/ChevronPeriod,u=cycle-(float)Math.Floor(cycle);if(((long)Math.Floor(cycle)&1)==1)u=1-u;
            return NativeUiMotion.Bezier(.42f,0,.58f,1,u);
        }
    }

    // main.ts drawHud .leet-status (style.css): in the fight, a L33t H4x pilot controlling a body (CONTROLLING name, the
    // countdown and "Your body is vulnerable") or the pilot it possesses (POSSESSED BY name and the countdown); otherwise
    // the Admin Pause countdown while frozen. Centred 21% down, #001c12df with a #45ff87 edge and a 40 px #32ff7a33 glow.
    public static class NativeLeetStatus
    {
        public const float ControlSeconds=5,FreezeSeconds=4;
        public const string ControllingText="CONTROLLING",PossessedText="POSSESSED BY",PauseText="ADMIN PAUSE",VulnerableText=" · Your body is vulnerable",UnknownName="PILOT";
        public const int Fill=0x001c12,Edge=0x45ff87,Ink=0xa9ffc5,GlowColor=0x32ff7a;public const float FillAlpha=0xdf/255f,GlowAlpha=0x33/255f,GlowSpread=40;
        public const float Top=.21f,PadX=28,PadY=15,Gap=8,TitleSize=16,TitleTracking=.12f,SubSize=13,Border=1;
        // The lines' heights (line-height: normal of the monospace face, as Chromium lays them out at these sizes).
        public const float TitleLine=19,SubLine=15;
        public static string Title(bool controlling,string name)=>(controlling?ControllingText:PossessedText)+" "+(string.IsNullOrEmpty(name)?UnknownName:name);
    }

    // main.ts drawHud DEV fighter HUD (fight phase): the Ansatsuken arcade HUD (1 HADOUKEN and 2 UPPERCUT cooldowns, the
    // Shinku hold bar while charging) and the Emperor's Q ELECTRIC card, at right 24 / bottom 93 over the cooldown cards
    // (match-hud.css); and the combo meter (hits, damage and the draining bar) top 40% right 8%, while the last hit is under
    // 1.1 s old (1.35 s for the Emperor). The Emperor's count always reads HITS.
    public static class NativeArcadeHud
    {
        public const float ComboWindow=1.1f,EmperorComboWindow=1.35f;
        public const string Hadouken="HADOUKEN",Uppercut="UPPERCUT",Electric="ELECTRIC",Charging="HOLD 1 · CHARGING",FullCharge="FULL CHARGE · RELEASE 1",Ready="READY",Hit=" HIT",Hits=" HITS",HitWord="HIT",HitsWord="HITS",DamageText=" DAMAGE";
        // .arcade-hud and .arcade-special (match-hud.css over style.css; the background is !important, so the Emperor's
        // card keeps it and only takes its border colour).
        public const float Right=24,Bottom=93,ItemGap=5,SpecialPadX=8,SpecialPadY=6,SpecialRadius=7,SpecialSize=10,StateSize=11,StateGap=14,Border=1;
        // line-height: normal lines measured in Chromium: the card's (10 px text with an 11 px state), the charge text's
        // (15 px bold) and the combo damage line's (16 px); the 22 px HIT(S) line starts .557 of the count's size down.
        public const float SpecialLine=13,ChargeLine=18,DamageLine=19,SmallDrop=.557f;
        public const int Ink=0xb8f7ff,SpecialFill=0x101219,SpecialEdge=0x63dcf9,EmperorEdge=0x76394a,StateInk=0xffe09b;public const float SpecialFillAlpha=0xa6/255f;
        // .hadoken-charge: its own line (flex-basis 100%) 10 px under the cards, 9/14 px padding, a 2 px #89edff edge on
        // #03141eee, 15 px bold text over the #158fbd80 fill, at least 280 px wide.
        public const float ChargeTop=10,ChargePadX=14,ChargePadY=9,ChargeBorder=2,ChargeSize=15,ChargeMin=280;
        public const int ChargeFill=0x03141e,ChargeEdge=0x89edff,ChargeBar=0x158fbd;public const float ChargeFillAlpha=0xee/255f,ChargeBarAlpha=0x80/255f;
        // .arcade-combo: scale(.8) about its left centre; the count clamp(42px, 5vw, 70px) on one 70 px line with the 22 px
        // HITS, the 16 px damage line 6 px either side, the 3 px bar with a 9 px glow; 3 px hard text shadow.
        public const float ComboTop=.4f,ComboRight=.08f,ComboScale=.8f,CountMin=42,CountVw=.05f,CountMax=70,SmallSize=22,DamageSize=16,DamageMargin=6,BarHeight=3,BarGlow=9,ShadowOffset=3;
        public const int ComboInk=0xffdf91,ComboShadow=0x592c23,DamageInk=0xb2eaff,ComboBar=0x88dfff,ComboBarGlow=0x69cfff,EmperorInk=0xff4163,EmperorShadow=0x17020b,EmperorBar=0xff244f;
        public static float CountSize(float viewportWidth)=>Math.Max(CountMin,Math.Min(CountMax,viewportWidth*CountVw));
        public static bool ComboShown(int hits,float time,float lastHit,bool emperor)=>hits>0&&time-lastHit<(emperor?EmperorComboWindow:ComboWindow);
        public static float ComboFill(float time,float lastHit,bool emperor)=>Math.Max(0,1-(time-lastHit)/(emperor?EmperorComboWindow:ComboWindow));
        public static string HitsSuffix(int hits,bool emperor)=>emperor||hits!=1?Hits:Hit;
        // `${Math.round(a.charge*100)}%` and Math.round(comboDamage): JS rounds halves up.
        public static int ChargePercent(float charge)=>(int)Math.Floor(charge*100d+.5);
        public static long Round(double value)=>(long)Math.Floor(value+.5);
        public static string ChargeText(float charge)=>charge>=1?FullCharge:Charging;
    }

    // client/siren-hud.ts sirenHud: '<key> MELEE/RANGED MODE' then keys 1-4 with the mode's four moves (SIREN_SLOTS) by
    // SIREN_MOVE_NAMES and each one's cooldown or READY. style.css .siren-hud spans (6/9 px padding, #13101ded, #706780
    // edge, 5 px corners, 12 px #ece6ff, the state bold #b9e6bf 5 px after), in a min(860px, 80vw) row centred at
    // right 24 / bottom 93 (match-hud.css .arcade-hud wins the bottom over .siren-hud's 110 px).
    public static class NativeSirenHud
    {
        public const string MeleeMode="MELEE MODE",RangedMode="RANGED MODE";
        public const float Width=860,WidthVw=.8f,PadX=9,PadY=6,Radius=5,Size=12,StateGap=5,Border=1,Line=14;
        public const int Fill=0x13101d,Edge=0x706780,Ink=0xece6ff,StateInk=0xb9e6bf;public const float FillAlpha=0xed/255f;
        public static string[] Moves(bool ranged)=>ranged?NativeSirenRules.Ranged:NativeSirenRules.Melee;
        public static string Name(string move)
        {
            switch(move)
            {
                case "swipe":return "Claw Swipe";case "rend":return "Rending Claw";case "slam":return "Grave Slam";case "rush":return "Shadow Rush";case "laugh":return "Last Laugh";
                case "hellfire":return "Hellfire";case "damnation":return "Damnation";case "eruption":return "Eruption";case "rain":return "Black Rain";case "mark":return "Marked";
                default:return move??"";
            }
        }
        // The Siren mode key setting (Q, F or V; the source's KeyQ / KeyF / KeyV less the Key prefix).
        public static string Key(string setting)=>setting=="F"||setting=="V"?setting:"Q";
    }

    // A guest's view of its own Siren cooldowns: the host keeps the Ready times (NativeSirenState) and does not send
    // them, so a replica latches each of its moves' Siren events (Detail = move id, emitted as the cooldown starts) and
    // adds the move's rule cooldown. A player Siren's cooldown scale is 1 (only the PvE boss's final phase changes it).
    public sealed class NativeSirenCooldowns
    {
        private readonly string[] ids={"rend","slam","rush","laugh","damnation","eruption","rain","mark"};private readonly float[] at=new float[8];
        private int last,generation=-1;
        public NativeSirenCooldowns(){Clear();}
        public void Clear(){last=0;generation=-1;for(int i=0;i<at.Length;i++)at[i]=float.NegativeInfinity;}
        public void Update(List<ArenaEvent> events,int gen,int local)
        {
            if(generation!=gen){Clear();generation=gen;}
            for(int i=0;i<events.Count;i++){var e=events[i];if(e.Id<=last)continue;last=e.Id;if(e.Kind!=EventKind.Siren||e.Source!=local||local<=0)continue;int k=Array.IndexOf(ids,e.Detail);if(k>=0&&!(e.Time<at[k]))at[k]=e.Time;}
        }
        public float Ready(string move){int k=Array.IndexOf(ids,move);var r=NativeSirenRules.Rule(move);return k<0||r==null||float.IsNegativeInfinity(at[k])?0:at[k]+r.Cooldown;}
    }

    // Cached HUD strings, so the per-frame HUD allocates none after warm-up: Number.prototype.toFixed(1) + "s" for
    // cooldowns and countdowns (exact binary rounding, ties up), integers, and string pairs.
    public static class NativeHudText
    {
        private static readonly string[] seconds=new string[1201],vulnerable=new string[1201],ints=new string[1000];
        // The integer n nearest x * 10 from x's exact binary value, ties up (toFixed(1)), for 0 <= x < 2^50.
        public static long Tenths(double x)
        {
            if(!(x>0)||double.IsInfinity(x))return 0;double whole=Math.Floor(x),f=x-whole;long n=(long)whole*10;if(f==0)return n;
            long bits=BitConverter.DoubleToInt64Bits(f);int exponent=(int)((bits>>52)&0x7FF);long mantissa=bits&((1L<<52)-1);if(exponent==0)exponent=1;else mantissa|=1L<<52;int shift=1075-exponent;
            // f = mantissa / 2^shift (shift > 52 as f < 1): floor((2*mantissa*10 + 2^shift) / 2^(shift+1)), computed without overflow.
            if(shift>=62)return n+(long)Math.Floor(f*10+.5);
            return n+(long)((2*(decimal)mantissa*10+(decimal)(1L<<shift))/((decimal)(1L<<shift)*2));
        }
        public static string Fixed1(double x){long t=Tenths(x);return (t/10).ToString(CultureInfo.InvariantCulture)+"."+(t%10).ToString(CultureInfo.InvariantCulture);}
        // `${x.toFixed(1)}s` (negative and NaN read as 0.0s, as Math.max(0, ...) leaves them in the source).
        public static string Seconds(double x){long t=Tenths(x);if(t<seconds.Length)return seconds[t]??=Fixed1(x)+"s";return Fixed1(x)+"s";}
        public static string SecondsVulnerable(double x){long t=Tenths(x);if(t<vulnerable.Length)return vulnerable[t]??=Fixed1(x)+"s"+NativeLeetStatus.VulnerableText;return Fixed1(x)+"s"+NativeLeetStatus.VulnerableText;}
        public static string Int(long n)=>n>=0&&n<ints.Length?ints[n]??=n.ToString(CultureInfo.InvariantCulture):n.ToString(CultureInfo.InvariantCulture);
    }
}
