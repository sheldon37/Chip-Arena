using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Kit v2's Q, E and 1-3 cards on the cyber card drawer (always: v2 is the shipped kit, NativeCyberLordHud.Drawer; with no
    /// record yet they read the view's empty one, READY or LOW MANA; Kernel Panic's 4 card is unchanged): Uplink, Overclock,
    /// Fork Bomb, Glitch Cut and Meteor Byte with their art icons
    /// (ClassIcons.json, tools/export_class_icons.mjs), ACTIVE through what the ability keeps doing after its cast (the cable
    /// and the zip, the 6 s power state, the clones' 5 s, the dash to the marks' detonation, the leap to the landing), the
    /// cooldown from the record's ready time (Overclock's own recovery is the rules' business: the record carries it), LOW
    /// MANA once ready, else READY. Presentation only.
    /// </summary>
    public static class CyberKit2MeleeHud
    {
        /// <summary>The v1 card slots (CyberLordSpec Q, E, One, Two, Three) to v2's (CyberKit2Spec Q, E, Fork, Cut, Meteor).</summary>
        public static int Kit2Slot(int v1Slot)=>v1Slot==CyberLordSpec.Q?CyberKit2Spec.Q:v1Slot==CyberLordSpec.E?CyberKit2Spec.E:v1Slot==CyberLordSpec.One?CyberKit2Spec.Fork:v1Slot==CyberLordSpec.Two?CyberKit2Spec.Cut:v1Slot==CyberLordSpec.Three?CyberKit2Spec.Meteor:-1;
        /// <summary>The cards' "KEY · NAME" labels by v1 card slot (Q, E, 1, 2, 3; the 4 card stays Kernel Panic's).</summary>
        public static readonly string[] Labels=Build();
        private static string[] Build(){var l=new string[CyberLordSpec.Four];for(int i=0;i<l.Length;i++){int s=Kit2Slot(i);l[i]=CyberKit2Spec.Keys[s]+" · "+CyberKit2Spec.Names[s].ToUpperInvariant();}return l;}
        /// <summary>The icon of a v2 ability id (CyberKit2Spec.Ids), null for any other.</summary>
        public static string Icon(string id){int s=CyberKit2Spec.Slot(id);return s<0?null:CyberKit2Spec.Icons[s];}
        /// <summary>Whether the v2 record drives the card in `v1Slot` for this view (the drawer shows the v2 card either way).</summary>
        public static bool Replaces(CyberLordView v,int v1Slot)=>v.Kit2.Live&&v1Slot>=CyberLordSpec.Q&&v1Slot<CyberLordSpec.Four;
        /// <summary>Whether the ability keeps going at `time` past its cast (the card shows ACTIVE).</summary>
        public static bool Busy(CyberKit2MeleeView k,int s,float time)
        {
            var p=k.Phase(s,time);if(p.Live&&p.Part<2)return true;
            switch(s)
            {
                case CyberKit2Spec.Q:return k.At[s]>-90&&time>=k.At[s]&&!k.Cut[s]&&time<(k.UplinkOutcome==CyberKit2Melee.Miss?k.Release[s]+CyberKit2Melee.MissSeconds:Math.Max(k.UplinkArriveAt,k.UplinkLatchAt));
                case CyberKit2Spec.E:return time>=k.OverclockAt&&time<k.OverclockUntil;
                case CyberKit2Spec.Fork:return time>=k.ForkAt&&time<k.ForkUntil;
                case CyberKit2Spec.Cut:return k.At[s]>-90&&!k.Cut[s]&&time>=k.At[s]&&time<k.DetonateAt;
                case CyberKit2Spec.Meteor:return k.At[s]>-90&&!k.Cut[s]&&time>=k.At[s]&&time<k.LandAt;
            }
            return false;
        }
        /// <summary>The v2 card for v1 card slot `v1Slot` at `time` for a pilot with `mana` (NativeCyberLordHud.Card).</summary>
        public static NativeCyberLordHud.Card Of(CyberLordView v,int v1Slot,float time,float mana)
        {
            var k=v.Kit2;int s=Kit2Slot(v1Slot);var c=new NativeCyberLordHud.Card{Key=CyberKit2Spec.Keys[s],Name=CyberKit2Spec.Names[s],Icon=CyberKit2Spec.Icons[s]};
            float cost=CyberKit2Spec.Costs[s],span=CyberKit2Spec.Cooldowns[s];
            // (A cast the record holds after `time`, on a rewound clock, has not happened yet.)
            float ready=k.At[s]>-90&&time<k.At[s]?-100:k.Ready[s]>-90?k.Ready[s]:k.At[s]>-90?k.At[s]+span:-100,left=ready-time;
            if(Busy(k,s,time)){c.State="ACTIVE";c.Active=true;c.Dial=span>0?1-Clamp01(left/span):1;return c;}
            if(left>0){c.State=NativeCyberLordHud.Time(left);c.Dial=span>0?1-Clamp01(left/span):1;c.Low=mana<cost;return c;}
            if(mana<cost){c.State="LOW MANA";c.Low=true;c.Dial=1;return c;}
            c.State="READY";c.Ready=true;c.Dial=1;return c;
        }
        /// <summary>The Overclock's edge glitch on the local pilot's screen (0..1): the power state's strength, pulsing; Reduced a
        /// steady low glow. The HUD draws RGB-split scan bars along the screen's edges with it.</summary>
        public static float EdgeGlitch(CyberLordView v,float time,bool reduced)
        {
            float oc=v!=null&&v.Kit2.Live?v.Kit2.Overclock(time):0;if(oc<=0)return 0;return reduced?.35f*oc:oc*(.6f+.4f*(float)Math.Abs(Math.Sin(time*7.3f)));
        }
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
    }
}
