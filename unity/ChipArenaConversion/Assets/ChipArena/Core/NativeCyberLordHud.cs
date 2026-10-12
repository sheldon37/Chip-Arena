using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Cyber Lord's ability cards on the cyber HUD card drawer (ConversionGame.CardAt: Q, E and 1-4 beside RMB and SHIFT)
    /// and its mana, from the replicated view (CyberLordFeed) and the pilot's mana: the cast while its window runs (Kernel
    /// Panic's cut-in and sequence as PANIC), the cooldown counting down from the slot's replicated ready time (the proposal's
    /// Ready; else the cast plus the spec cooldown, Kernel Panic's whiff refunding half), low mana once ready, else READY.
    /// Shown only for a Cyber Lord pilot: a DEV-only class (NativeLoadoutText / content devOnly), so DEV access gates it with
    /// the class. Presentation only.
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
            if(slot==CyberLordSpec.Four&&v.PanicLive(time)){c.State="PANIC";c.Active=true;c.Dial=1-Clamp01(left/span);return c;}
            if(p.Live&&p.Part<2||slot==CyberLordSpec.Two&&time>=v.LinkAt&&time<v.LinkUntil||slot==CyberLordSpec.E&&time>=v.WallBorn&&time<v.WallUntil){c.State="ACTIVE";c.Active=true;c.Dial=1-Clamp01(left/span);return c;}
            if(left>0){c.State=Time(left);c.Dial=1-Clamp01(left/span);c.Low=mana<cost;return c;}
            if(mana<cost){c.State="LOW MANA";c.Low=true;c.Dial=1;return c;}
            c.State="READY";c.Ready=true;c.Dial=1;return c;
        }
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        /// <summary>The mana bar's gain flash: a combo hit's +6 lands in the bar (0..1 over .35 s from the flow's arrival).</summary>
        public static float GainFlash(float time,float chargeAt)=>chargeAt>-90&&time>=chargeAt&&time<chargeAt+.35f?1-(time-chargeAt)/.35f:0;
    }
}
