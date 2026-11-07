using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Kit v2's ranged cards on the cyber HUD card drawer (Runtime/NativeCyberLordScreen.cs, NativeCyberLordHud.Drawer): while
    /// the pilot is in ranged mode (key 5), the 1-4 cards are Daisy Chain, Singularity, Packet Storm and Orbital Uplink (with no
    /// record yet they read the view's empty one: READY or LOW MANA; Orbital Uplink still waits for the ultimate clock), each with
    /// its key, line icon (CyberKit2Spec.Icons, ClassIcons.json), cooldown from its replicated ready time (else the cast plus the
    /// spec cooldown; Orbital Uplink also waits for the shared ultimate clock, Kernel Panic's), mana, and state: CHARGE through a
    /// windup, ACTIVE through the cast and its lasting effect (the hole until it implodes, the storm until its last drop), CALL
    /// and STEER for the orbital column, then the countdown, LOW MANA or READY. Presentation only; allocation-free.
    /// </summary>
    public static class CyberKit2RangedHud
    {
        /// <summary>The ranged 1-4 cards in drawer order (CyberKit2Ranged slots).</summary>
        public static readonly int[] Order={CyberKit2Ranged.Chain,CyberKit2Ranged.Singularity,CyberKit2Ranged.Storm,CyberKit2Ranged.Orbital};
        /// <summary>"KEY · NAME" per ranged slot (CyberKit2Ranged order), built once.</summary>
        public static readonly string[] Labels=Build();
        private static string[] Build(){var l=new string[CyberKit2Ranged.Slots];for(int i=0;i<l.Length;i++){int s=CyberKit2Ranged.Spec(i);l[i]=CyberKit2Spec.Keys[s]+" · "+CyberKit2Spec.Names[s].ToUpperInvariant();}return l;}
        /// <summary>Whether the v2 ranged record is live for this view (the drawer shows the v2 ranged set either way).</summary>
        public static bool Shows(CyberLordView v)=>v!=null&&v.Kit2R.Live;
        /// <summary>A slot's ready time (a cast the record holds after `time`, on a rewound clock, has not happened yet).</summary>
        public static float ReadyAt(CyberLordView v,int slot,float kernelReady=float.NaN,float time=float.PositiveInfinity)
        {
            var k=v.Kit2R;int s=CyberKit2Ranged.Spec(slot);
            float ready=k.At[slot]>-90&&time<k.At[slot]?-100:k.Ready[slot]>-90?k.Ready[slot]:k.At[slot]>-90?k.At[slot]+CyberKit2Spec.Cooldowns[s]:-100;
            if(slot==CyberKit2Ranged.Orbital)ready=Math.Max(ready,kernelReady>0?kernelReady:NativeCyberLordHud.ReadyAt(v,CyberLordSpec.Four));
            return ready;
        }
        /// <summary>One card at `time` for a pilot with `mana`.</summary>
        public static NativeCyberLordHud.Card Of(CyberLordView v,int slot,float time,float mana,float kernelReady=float.NaN)
        {
            int s=CyberKit2Ranged.Spec(slot);var k=v.Kit2R;var c=new NativeCyberLordHud.Card{Key=CyberKit2Spec.Keys[s],Name=CyberKit2Spec.Names[s],Icon=CyberKit2Spec.Icons[s]};
            float cost=CyberKit2Spec.Costs[s],span=CyberKit2Spec.Cooldowns[s],ready=ReadyAt(v,slot,kernelReady,time),left=ready-time;var p=k.Phase(slot,time);float dial=span>0?1-Clamp01(left/span):1;
            if(p.Live&&p.Part==0){c.State=slot==CyberKit2Ranged.Orbital?"CALL":"CHARGE";c.Active=true;c.Dial=p.U;return c;}
            bool lasting=slot==CyberKit2Ranged.Singularity&&k.OrbLand>-90&&time>=k.At[slot]&&time<k.Implode||slot==CyberKit2Ranged.Storm&&k.Released(slot)&&time>=k.At[slot]&&time<k.StormEnd;
            if(p.Live&&p.Part==1||lasting){c.State=slot==CyberKit2Ranged.Orbital?"STEER":"ACTIVE";c.Active=true;c.Dial=dial;return c;}
            if(left>0){c.State=NativeCyberLordHud.Time(left);c.Dial=dial;c.Low=mana<cost;return c;}
            if(mana<cost){c.State="LOW MANA";c.Low=true;c.Dial=1;return c;}
            c.State="READY";c.Ready=true;c.Dial=1;return c;
        }
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
    }

    /// <summary>
    /// Kit v2's ranged audio routing onto EXISTING NativeSoundBank keys only (no new key, no new file), each with a clip that
    /// already ships as its fallback (fire, lightning, darkness, explosion, electric-hook, dash, metal, burn-tick): the view's
    /// moments (each throw's release, the chain's charge, strike and hops, the orb's throw, the hole opening and imploding, the
    /// storm's call and each drop, the orbital mark, column and end) and the ranged events by their Detail. A cut cast is silent
    /// past its cut. Presentation only.
    /// </summary>
    public static class CyberKit2RangedSounds
    {
        public const int Throw=0,ChainCharge=1,ChainFire=2,ChainHop=3,OrbThrow=3+CyberKit2Spec.Hops,HoleOpen=OrbThrow+1,Implode=HoleOpen+1,StormCall=Implode+1,StormDrop=StormCall+1,
            OrbitalMark=StormDrop+CyberKit2Spec.Drops,OrbitalBeam=OrbitalMark+1,OrbitalEnd=OrbitalBeam+1,Slots=OrbitalEnd+1;
        private static int S(NativeKitSound[] o,int n,string key,string clip,float gain){if(n<o.Length)o[n]=new NativeKitSound{Key=key,Clip=clip,Gain=gain};return Math.Min(o.Length,n+1);}
        /// <summary>When state cue `slot` is due for this view (NaN: never), and its sounds into `o` (returns the count).</summary>
        public static int State(CyberLordView v,int slot,NativeKitSound[] o,out float at)
        {
            at=float.NaN;int n=0;var k=v.Kit2R;if(!k.Live)return 0;
            if(slot==Throw){if(k.Released(CyberKit2Ranged.Plasma)){at=k.Release[CyberKit2Ranged.Plasma];n=S(o,n,"magic.fire.shot","fire",k.Overflow?.42f:.3f);if(k.Overflow)n=S(o,n,"blast.fire","explosion",.3f);n=S(o,n,"dev.leet.glitch","darkness",.07f);}return n;}
            if(slot==ChainCharge){if(k.At[CyberKit2Ranged.Chain]>-90){at=k.At[CyberKit2Ranged.Chain];n=S(o,n,"chip.delayed.charge","electric-hook",.28f);}return n;}
            if(slot==ChainFire){if(k.Released(CyberKit2Ranged.Chain)&&k.ChainCount>0){at=k.HopAt(0);n=S(o,n,"magic.lightning.shot","lightning",.55f);n=S(o,n,"status.shock-zap","lightning",.3f);}return n;}
            if(slot>=ChainHop&&slot<OrbThrow){int hop=slot-ChainHop+1;if(k.Released(CyberKit2Ranged.Chain)&&hop<k.ChainCount){at=k.HopAt(hop);n=S(o,n,"status.shock-zap","lightning",.32f*(1-.1f*hop));}return n;}
            if(slot==OrbThrow){if(k.Released(CyberKit2Ranged.Singularity)){at=k.Release[CyberKit2Ranged.Singularity];n=S(o,n,"gun.frag.throw","dash",.35f);}return n;}
            if(slot==HoleOpen){if(k.OrbLand>-90){at=k.OrbLand;n=S(o,n,"magic.singularity","darkness",.55f);n=S(o,n,"field.black-hole","darkness",.35f);}return n;}
            if(slot==Implode){if(k.Implode>-90){at=k.Implode;n=S(o,n,"blast.implosion","darkness",.7f);n=S(o,n,"blast.nova","explosion",.4f);}return n;}
            if(slot==StormCall){if(k.Released(CyberKit2Ranged.Storm)){at=k.Release[CyberKit2Ranged.Storm];n=S(o,n,"magic.meteor.incoming","lightning",.5f);}return n;}
            if(slot>=StormDrop&&slot<OrbitalMark){if(k.Released(CyberKit2Ranged.Storm)){k.Drop(slot-StormDrop,out at,out _);n=S(o,n,"blast.meteor","explosion",.3f);}return n;}
            if(slot==OrbitalMark){if(k.At[CyberKit2Ranged.Orbital]>-90){at=k.At[CyberKit2Ranged.Orbital];n=S(o,n,"gun.orbital.mark","electric-hook",.5f);}return n;}
            if(slot==OrbitalBeam){if(k.Released(CyberKit2Ranged.Orbital)){at=k.Release[CyberKit2Ranged.Orbital];n=S(o,n,"blast.shock","lightning",.75f);n=S(o,n,"field.storm","lightning",.4f);}return n;}
            if(slot==OrbitalEnd){if(k.Released(CyberKit2Ranged.Orbital)){at=k.Until[CyberKit2Ranged.Orbital];n=S(o,n,"dev.leet.glitch","metal",.3f);}return n;}
            return 0;
        }
        /// <summary>A v2 ranged event's cues (its Detail through CyberKit2RangedFeed.Hit). targetMe: the local pilot is its target.</summary>
        public static int Event(EventKind kind,CyberHit hit,bool targetMe,NativeKitSound[] o)
        {
            int n=0;bool strike=kind==EventKind.Hit||kind==EventKind.AbilityHit;
            if(hit==CyberKit2RangedFeed.PlasmaHit){if(kind==EventKind.Absorb)n=S(o,n,"melee.shield.block","parry",.3f);else if(strike&&!targetMe)n=S(o,n,"magic.fire.hit","fire",.4f);else if(kind==EventKind.Impact)n=S(o,n,"magic.fire.hit","fire",.2f);}
            else if(hit==CyberKit2RangedFeed.OverflowHit){if(kind==EventKind.Absorb)n=S(o,n,"melee.shield.block","parry",.4f);else if(strike&&!targetMe||kind==EventKind.Impact)n=S(o,n,"blast.fire","explosion",.45f);}
            else if(hit==CyberKit2RangedFeed.SingularityTick){if(strike&&!targetMe)n=S(o,n,"impact.energy","darkness",.2f);}
            else if(hit==CyberKit2RangedFeed.OrbitalTick){if(strike)n=S(o,n,"status.burn-tick","burn-tick",.22f);}
            return n;
        }
        /// <summary>The bank keys this routing may play (every one an existing bank key) and their fallback clips.</summary>
        public static readonly string[] Keys={"magic.fire.shot","blast.fire","dev.leet.glitch","chip.delayed.charge","magic.lightning.shot","status.shock-zap","gun.frag.throw","magic.singularity","field.black-hole",
            "blast.implosion","blast.nova","magic.meteor.incoming","blast.meteor","gun.orbital.mark","blast.shock","field.storm","melee.shield.block","magic.fire.hit","impact.energy","status.burn-tick"};
        public static readonly string[] Clips={"fire","explosion","darkness","electric-hook","lightning","dash","metal","parry","burn-tick"};
    }
}
