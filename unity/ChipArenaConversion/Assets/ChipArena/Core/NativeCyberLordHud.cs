using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Cyber Lord's ability cards on the cyber HUD card drawer (ConversionGame.CardAt: Q, E and 1-4 beside RMB and SHIFT)
    /// and its mana, from the replicated view (CyberLordFeed) and the pilot's mana: the cast while its window runs (Kernel
    /// Panic's cut-in and sequence as PANIC), the cooldown counting down from the slot's replicated ready time (the proposal's
    /// Ready; else the cast plus the spec cooldown, Kernel Panic's whiff refunding half), low mana once ready, else READY.
    /// Shown only for a Cyber Lord pilot: a DEV-only class (NativeLoadoutText / content devOnly), so DEV access gates it with
    /// the class. The drawer draws Kit v2's cards (Drawer, below); the v1 card functions stay for v1's checks. Presentation only.
    /// </summary>
    public static class NativeCyberLordHud
    {
        public struct Card{public string Key,Name,State,Icon;public bool Ready,Active,Low;public float Dial;}
        /// <summary>The cards in drawer order (right to left from the corner the drawer draws them in): Q, E, 1, 2, 3, 4.</summary>
        public static readonly int[] Order={CyberLordSpec.Q,CyberLordSpec.E,CyberLordSpec.One,CyberLordSpec.Two,CyberLordSpec.Three,CyberLordSpec.Four};
        /// <summary>The cards' keys and names as the drawer splits them ("KEY · NAME"), built once.</summary>
        public static readonly string[] Labels=Build();
        private static string[] Build(){var l=new string[CyberLordSpec.Slots];for(int i=0;i<l.Length;i++)l[i]=CyberLordSpec.Keys[i]+" · "+CyberLordSpec.Names[i].ToUpperInvariant();return l;}
        /// <summary>Line icons (Lucide names, NativeIcons) per slot; the Locker's ability rows share them (Icon).</summary>
        public static readonly string[] Icons={"zap","brick-wall","boxes","network","radio","cpu"};
        /// <summary>The icon of a Cyber Lord ability id (Astra's content ids, CyberLordSpec.Ids), null for any other.</summary>
        public static string Icon(string id){int s=CyberLordSpec.Slot(id);return s<0?null:Icons[s];}
        private static readonly string[] seconds=Seconds();
        private static string[] Seconds(){var s=new string[301];for(int i=0;i<s.Length;i++)s[i]=(i/10f).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"s";return s;}
        /// <summary>"12.3s" without allocating (cached to .1 s up to 30 s).</summary>
        public static string Time(float left){int k=(int)Math.Ceiling(Math.Max(0,left)*10-1e-4f);return k<seconds.Length?seconds[k]:Math.Ceiling(left).ToString(System.Globalization.CultureInfo.InvariantCulture)+"s";}
        /// <summary>The slot's ready time: `ready` when given (the local pilot's replicated Kernel Panic cooldown, the pilot
        /// frame's kernelReady), else the view's replicated one, else the cast plus its cooldown (a whiff refunds half).</summary>
        public static float ReadyAt(CyberLordView v,int slot,float ready=float.NaN)
        {
            if(ready>0)return ready;if(v.Ready[slot]>-90)return v.Ready[slot];if(!(v.At[slot]>-90))return -100;
            float cd=CyberLordSpec.Cooldowns[slot];if(slot==CyberLordSpec.Four&&v.PanicOutcome==CyberLordSpec.Whiff)cd*=.5f;return v.At[slot]+cd;
        }
        /// <summary>One card at `time` for a pilot with `mana`.</summary>
        public static Card Of(CyberLordView v,int slot,float time,float mana,float readyAt=float.NaN)
        {
            var c=new Card{Key=CyberLordSpec.Keys[slot],Name=CyberLordSpec.Names[slot],Icon=Icons[slot]};float cost=CyberLordSpec.Costs[slot];
            var p=v.Phase(slot,time);float ready=ReadyAt(v,slot,readyAt),left=ready-time,span=slot==CyberLordSpec.Four&&v.PanicOutcome==CyberLordSpec.Whiff?CyberLordSpec.Cooldowns[slot]*.5f:CyberLordSpec.Cooldowns[slot];
            if(slot==CyberLordSpec.Four&&v.PanicLive(time)){c.State="PANIC";c.Active=true;c.Dial=span>0?1-Clamp01(left/span):1;return c;}
            if(p.Live&&p.Part<2||slot==CyberLordSpec.Two&&time>=v.LinkAt&&time<v.LinkUntil||slot==CyberLordSpec.E&&time>=v.WallBorn&&time<v.WallUntil){c.State="ACTIVE";c.Active=true;c.Dial=span>0?1-Clamp01(left/span):1;return c;}
            if(left>0){c.State=Time(left);c.Dial=span>0?1-Clamp01(left/span):1;c.Low=mana<cost;return c;}
            if(mana<cost){c.State="LOW MANA";c.Low=true;c.Dial=1;return c;}
            c.State="READY";c.Ready=true;c.Dial=1;return c;
        }
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;

        // ---- the ranged mode (Core/CyberLordRanged.cs): the 1-4 cards swap sets, and a key-5 card shows the live set ----
        /// <summary>Whether the drawer shows the ranged set (the mode's weight past half: the cards swap mid-switch).</summary>
        public static bool ShowsRanged(CyberLordView v,float time)=>v.ModeWeight(time)>=.5f;
        /// <summary>The ranged 1-4 cards in drawer order (CyberRangedSpec slots), their labels, and the key-5 card's labels.</summary>
        public static readonly int[] RangedOrder={CyberRangedSpec.Lance,CyberRangedSpec.Field,CyberRangedSpec.Swap,CyberRangedSpec.Root};
        public static readonly string[] RangedLabels=BuildRanged();
        private static string[] BuildRanged(){var l=new string[CyberRangedSpec.Slots];for(int i=0;i<l.Length;i++)l[i]=CyberRangedSpec.Keys[i]+" · "+CyberRangedSpec.Names[i].ToUpperInvariant();return l;}
        public const string RangedMode="5 · RANGED",MeleeMode="5 · MELEE";
        /// <summary>A ranged slot's ready time: the view's replicated one, else the cast plus its cooldown; Root Access also waits
        /// for the shared ultimate clock (Kernel Panic's: `kernelReady`, the pilot frame's, else the view's).</summary>
        public static float RangedReadyAt(CyberLordView v,int slot,float kernelReady=float.NaN,float time=float.PositiveInfinity)
        {
            // (A cast the record holds after `time`, on a rewound clock, has not happened yet.)
            float ready=v.RAt[slot]>-90&&time<v.RAt[slot]?-100:v.RReady[slot]>-90?v.RReady[slot]:v.RAt[slot]>-90?v.RAt[slot]+CyberRangedSpec.Cooldowns[slot]:-100;
            if(slot==CyberRangedSpec.Root)ready=Math.Max(ready,kernelReady>0?kernelReady:ReadyAt(v,CyberLordSpec.Four));
            return ready;
        }
        /// <summary>One ranged card at `time` for a pilot with `mana` (the states as Of: ACTIVE through the cast, the field's life
        /// and the mine's fuse; CHARGE through Root Access's charge).</summary>
        public static Card RangedOf(CyberLordView v,int slot,float time,float mana,float kernelReady=float.NaN)
        {
            var c=new Card{Key=CyberRangedSpec.Keys[slot],Name=CyberRangedSpec.Names[slot],Icon=CyberRangedSpec.Icons[slot]};float cost=CyberRangedSpec.Costs[slot];
            var p=v.RPhase(slot,time);float ready=RangedReadyAt(v,slot,kernelReady,time),left=ready-time,span=CyberRangedSpec.Cooldowns[slot];
            if(slot==CyberRangedSpec.Root&&p.Live&&p.Part==0){c.State="CHARGE";c.Active=true;c.Dial=p.U;return c;}
            if(p.Live&&p.Part<2||slot==CyberRangedSpec.Field&&time>=v.FieldLand&&time<v.FieldUntil||slot==CyberRangedSpec.Swap&&time>=v.RRelease[slot]&&time<v.MineAt){c.State="ACTIVE";c.Active=true;c.Dial=span>0?1-Clamp01(left/span):1;return c;}
            if(left>0){c.State=Time(left);c.Dial=span>0?1-Clamp01(left/span):1;c.Low=mana<cost;return c;}
            if(mana<cost){c.State="LOW MANA";c.Low=true;c.Dial=1;return c;}
            c.State="READY";c.Ready=true;c.Dial=1;return c;
        }
        /// <summary>The key-5 card: the live set (RANGED or MELEE, its icon), SWITCHING through the reconfiguration, then the
        /// switch's cooldown, then READY.</summary>
        public static Card ModeOf(CyberLordView v,float time)
        {
            bool ranged=ShowsRanged(v,time);var c=new Card{Key=CyberRangedSpec.ModeKey,Name=ranged?"Ranged":"Melee",Icon=ranged?CyberRangedSpec.ModeIcon:CyberRangedSpec.MeleeIcon};
            float sw=v.SwitchU(time);if(sw>=0){c.State="SWITCHING";c.Active=true;c.Dial=sw;return c;}
            float left=v.ModeAt>-90&&time<v.ModeAt?0:v.ModeReady-time;if(left>0){c.State=Time(left);c.Dial=1-Clamp01(left/CyberRangedSpec.SwitchCooldown);return c;}
            c.State="READY";c.Ready=true;c.Dial=1;return c;
        }
        /// <summary>The mana bar's gain flash: a combo hit's +6 lands in the bar (0..1 over .35 s from the flow's arrival).</summary>
        public static float GainFlash(float time,float chargeAt)=>chargeAt>-90&&time>=chargeAt&&time<chargeAt+.35f?1-(time-chargeAt)/.35f:0;

        // ---- the drawer (Runtime/NativeCyberLordScreen.cs): Kit v2, the shipped kit ----
        /// <summary>The most cards the drawer shows for a Cyber Lord: Q, E, the live set's 1-4 and the key-5 card.</summary>
        public const int DrawerCards=7;
        /// <summary>A view with no replicated state (all windows and ready times unset): what the drawer reads for a Cyber Lord
        /// pilot the presentation has no view of yet.</summary>
        public static CyberLordView Fresh(){var v=new CyberLordView();v.Clear();return v;}
        /// <summary>
        /// The Cyber Lord's drawer cards at `time` for a pilot with `mana` (`kernelReady`: the pilot frame's Kernel Panic
        /// cooldown), in drawer order into `cards` and their "KEY · NAME" labels into `labels` (both DrawerCards long); returns
        /// the count. Kit v2 is the shipped kit (Astra's authority, PR #231), so its cards always show: Q Uplink, E Overclock,
        /// then melee 1 Fork Bomb, 2 Glitch Cut, 3 Meteor Byte, 4 Kernel Panic, or ranged 1 Daisy Chain, 2 Singularity, 3 Packet
        /// Storm, 4 Orbital Uplink, then the key-5 card. With the replicated v2 record (Kit2, Kit2R live) they read its windows
        /// and ready times (CyberKit2MeleeHud.Of, CyberKit2RangedHud.Of, unchanged). Before the authority first writes it (the
        /// record is made at the pilot's first Cyber input: the stage load, the intro, the countdown and draft, a reset round, a
        /// guest before a snapshot carries it, a spectated or killcam pilot whose frame has none) the same functions read the
        /// view's empty record: READY, or LOW MANA under the v2 cost, with v2's names and icons. Kernel Panic's 4 card (the
        /// ultimate clock both 4s share) and the key-5 card are the same in both kits. The v1 card functions above (Of for Q to 3,
        /// RangedOf, Labels, RangedLabels) are no longer drawn. Allocation-free.
        /// </summary>
        public static int Drawer(CyberLordView v,float time,float mana,float kernelReady,Card[] cards,string[] labels)
        {
            if(v==null||cards==null||labels==null||cards.Length<DrawerCards||labels.Length<DrawerCards)return 0;
            bool ranged=ShowsRanged(v,time);int n=0;
            for(int slot=CyberLordSpec.Q;slot<=CyberLordSpec.E;slot++){cards[n]=CyberKit2MeleeHud.Of(v,slot,time,mana);labels[n++]=CyberKit2MeleeHud.Labels[slot];}
            if(ranged)foreach(int slot in CyberKit2RangedHud.Order){cards[n]=CyberKit2RangedHud.Of(v,slot,time,mana,kernelReady);labels[n++]=CyberKit2RangedHud.Labels[slot];}
            else
            {
                for(int slot=CyberLordSpec.One;slot<CyberLordSpec.Four;slot++){cards[n]=CyberKit2MeleeHud.Of(v,slot,time,mana);labels[n++]=CyberKit2MeleeHud.Labels[slot];}
                cards[n]=Of(v,CyberLordSpec.Four,time,mana,kernelReady);labels[n++]=Labels[CyberLordSpec.Four];
            }
            cards[n]=ModeOf(v,time);labels[n++]=ranged?RangedMode:MeleeMode;
            return n;
        }
    }
}
