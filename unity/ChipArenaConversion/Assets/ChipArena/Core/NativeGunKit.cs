using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The pistol and machine-gun Q kit's presentation clocks (owner 2026-10-12: Q aim-down-sights replaced by new abilities;
    /// Astra's simulation, PR #134: Core/NativeRangedRules, Runtime/NativeRangedAbilities; Claude's presentation, see
    /// docs/RANGED-ABILITIES.md). Every pose, effect, sound and HUD card reads this one view of the replicated actor:
    /// Overdrive from Actor.OverdriveAt/OverdriveUntil/VentUntil (0 = never cast); Fan the Hammer from the cast the authority
    /// stamps on the pose (Actor.AbilityId "fan-hammer", AbilityAt = the cast and its first round), the rounds fired so far
    /// from Actor.LastShot inside the volley's .075 s schedule, and the volley's length from the magazine it started with
    /// (Ammo + rounds fired, at most four): a round that does not arrive on schedule (a roll, block, stun or reload cut the burst)
    /// ends it, an emptied magazine's reload ends it. Each fanned round is also a normal Shot event (Detail "fan"; Overdrive's
    /// "overdrive"). Rules and timings are the simulation's (NativeRangedRules); nothing here reaches the authority, the
    /// network frame, hit timing, hitboxes, damage, cooldowns or movement.
    /// </summary>
    public static class NativeGunKit
    {
        public const string Fan="fan-hammer",Overdrive="overdrive",FanTag="fan",OverdriveTag="overdrive";
        public const float FanGap=NativeRangedRules.FanInterval;public const int FanMaxShots=NativeRangedRules.FanRounds;
        /// <summary>How long after its schedule a fanned round may still arrive (the host's fixed tick, a guest's 20 Hz frames).</summary>
        public const float FanLate=.09f;
        // Presentation spans: the fan's pose blend in, the last slap's follow-through and the return to the grip, the smoking
        // barrel; Overdrive's barrel spin-up, spin-down and the cooling after the vent.
        public const float FanIn=.05f,FanFollow=.1f,FanRecover=.3f,FanSmoke=1.8f,SpinUp=.35f,SpinDown=.6f,Cool=1.4f;

        /// <summary>One pilot's kit as the presentation reads it, sanitised (a malformed frame reads as no kit).</summary>
        public struct State
        {
            public float FanAt,OverdriveAt,OverdriveUntil,VentUntil;public int FanShots,FanFired;
            public bool HasFan=>FanShots>0;public bool HasOverdrive=>OverdriveUntil>OverdriveAt;
            /// <summary>Round i of the fan on its schedule (0 .. FanShots-1).</summary>
            public float FanShot(int i)=>FanAt+i*FanGap;
            public float FanLast=>FanAt+Math.Max(0,FanShots-1)*FanGap;
        }
        private static bool F(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v)&&Math.Abs(v)<10000000;
        /// <summary>The kit of `a` at presentation `time` (FanShots: the volley's rounds, those still due included; FanFired: the
        /// rounds the snapshot has fired).</summary>
        public static State Of(Actor a,float time)
        {
            var s=new State{FanAt=-100,OverdriveAt=-100,OverdriveUntil=-100,VentUntil=-100};if(a==null)return s;
            if(a.Weapon==WeaponId.Pistol&&a.AbilityId==Fan&&F(a.AbilityAt)&&a.AbilityAt>-90&&time>=a.AbilityAt-.001f&&time-a.AbilityAt<FanSmoke+1)
            {
                float at=a.AbilityAt,rel=a.LastShot-at;int fired=0;
                if(F(a.LastShot)&&rel>=-.001f&&rel<=(FanMaxShots-1)*FanGap+FanLate)fired=Math.Min(FanMaxShots,(int)Math.Floor(rel/FanGap+.3f)+1);
                // The cast's magazine bounds the volley; a reload started since the cast (the emptied magazine, or R) ends it.
                int limit=Math.Min(FanMaxShots,Math.Max(fired,Math.Max(0,a.Ammo)+fired));if(a.ReloadUntil>time&&a.ReloadAt>=at-.001f)limit=fired;
                int shots=fired<limit&&time>at+fired*FanGap+FanLate?fired:limit;
                if(shots>0){s.FanAt=at;s.FanShots=shots;s.FanFired=fired;}
            }
            if(a.Weapon==WeaponId.MachineGun&&F(a.OverdriveAt)&&F(a.OverdriveUntil)&&F(a.VentUntil)&&a.OverdriveAt>0&&a.OverdriveUntil>a.OverdriveAt&&a.OverdriveUntil-a.OverdriveAt<=30&&a.VentUntil>=a.OverdriveUntil)
            {s.OverdriveAt=a.OverdriveAt;s.OverdriveUntil=a.OverdriveUntil;s.VentUntil=Math.Min(a.VentUntil,a.OverdriveUntil+5);}
            return s;
        }

        public enum FanPhase{None,Fan,Recover,Smoke}
        /// <summary>
        /// The fan's phase at `time`: Fan from the cast through the last slap's follow-through, Recover while the off hand returns
        /// to the grip, Smoke while the barrel smokes. `shot` is the newest fired round (-1 before the first), `since` the time
        /// since it.
        /// </summary>
        public static FanPhase FanAt(in State s,float time,out int shot,out float since)
        {
            shot=-1;since=0;if(!s.HasFan||time<s.FanAt)return FanPhase.None;
            shot=Math.Min(s.FanShots-1,(int)Math.Floor((time-s.FanAt)/FanGap+1e-4f));since=time-s.FanShot(shot);
            float last=s.FanLast;
            if(time<last+FanFollow)return FanPhase.Fan;
            if(time<last+FanFollow+FanRecover)return FanPhase.Recover;
            return time<last+FanSmoke?FanPhase.Smoke:FanPhase.None;
        }
        /// <summary>The fan pose's weight: in over FanIn from the cast, held through the follow-through, out over FanRecover.</summary>
        public static float FanWeight(in State s,float time)
        {
            if(!s.HasFan||time<s.FanAt)return 0;float end=s.FanLast+FanFollow;
            if(time<end)return Smooth((time-s.FanAt)/FanIn);
            return 1-Smooth((time-end)/FanRecover);
        }

        public enum DrivePhase{None,SpinUp,Sustain,Vent,Cool}
        /// <summary>Overdrive's phase at `time`: the barrels spin up, sustained fire, the overheat vent, then the cooling glow.</summary>
        public static DrivePhase DriveAt(in State s,float time)
        {
            if(!s.HasOverdrive||time<s.OverdriveAt)return DrivePhase.None;
            if(time<s.OverdriveUntil)return time<s.OverdriveAt+SpinUp?DrivePhase.SpinUp:DrivePhase.Sustain;
            if(time<s.VentUntil)return DrivePhase.Vent;
            return time<s.VentUntil+Cool?DrivePhase.Cool:DrivePhase.None;
        }
        /// <summary>The barrels' heat 0..1: it climbs over the active time (orange, then white-hot at its end), holds through the
        /// vent's first moment and falls to the red glow, which cools out after the vent.</summary>
        public static float Heat(in State s,float time)
        {
            if(!s.HasOverdrive||time<s.OverdriveAt)return 0;
            if(time<s.OverdriveUntil){float u=(time-s.OverdriveAt)/Math.Max(.05f,s.OverdriveUntil-s.OverdriveAt);return .18f*Smooth((time-s.OverdriveAt)/SpinUp)+.82f*(float)Math.Pow(Clamp01(u),1.35f);}
            float vent=Math.Max(.05f,s.VentUntil-s.OverdriveUntil),hot=1;
            if(time<s.VentUntil)return hot-(1-.5f)*Smooth((time-s.OverdriveUntil)/vent);
            return .5f*(1-Smooth((time-s.VentUntil)/Cool));
        }
        /// <summary>The red shift of the glow 0..1 (0 while firing; the vent turns it red as it cools).</summary>
        public static float Redden(in State s,float time)
        {
            if(!s.HasOverdrive||time<s.OverdriveUntil)return 0;float vent=Math.Max(.05f,s.VentUntil-s.OverdriveUntil);
            return time<s.VentUntil?Smooth((time-s.OverdriveUntil)/(vent*.6f)):1;
        }
        /// <summary>The barrel spin rate 0..1 (spin-up over SpinUp, full while firing, spin-down over SpinDown after it).</summary>
        public static float Spin(in State s,float time)
        {
            if(!s.HasOverdrive||time<s.OverdriveAt)return 0;
            if(time<s.OverdriveUntil)return Smooth((time-s.OverdriveAt)/SpinUp);
            return 1-Smooth((time-s.OverdriveUntil)/SpinDown);
        }
        /// <summary>The barrel cluster's turn (radians) at `time`: the integral of Spin at SpinRate turns a second (closed form, so
        /// every peer and the killcam agree), held once spun down.</summary>
        public const float SpinRate=4.5f;
        public static float SpinAngle(in State s,float time)
        {
            if(!s.HasOverdrive||time<s.OverdriveAt)return 0;const float W=SpinRate*2*(float)Math.PI;
            float up=SpinUp,t=Math.Min(time,s.OverdriveUntil)-s.OverdriveAt;
            // The smoothstep ramp's integral: up x (x^3 - x^4/2) at x = t/up, half the ramp's length at full rate once spun up.
            float x=up>1e-4f?t/up:1,a=t<up?W*up*(x*x*x-.5f*x*x*x*x):W*(up*.5f+(t-up));
            if(time>s.OverdriveUntil){float d=Math.Min(SpinDown,time-s.OverdriveUntil)/SpinDown;a+=W*SpinDown*(d-(d*d*d-.5f*d*d*d*d));}
            return a;
        }
        /// <summary>Whether a round born at `born` by this pilot belongs to the fan / to Overdrive (its tracer's look).</summary>
        public static bool FanRound(in State s,float born)=>s.HasFan&&born>=s.FanAt-.02f&&born<=s.FanLast+.02f;
        public static bool DriveRound(in State s,float born)=>s.HasOverdrive&&born>=s.OverdriveAt-.02f&&born<=s.OverdriveUntil+.02f;

        /// <summary>The kit's abilities (the Q cards and icons).</summary>
        public static bool Covers(string id)=>NativeRangedRules.Q(id);
        public static string Icon(string id)=>id==Fan?"hammer":id==Overdrive?"gauge":null;

        // ---- audio: existing bank keys with built-in fallbacks (NativeAudio) ----
        /// <summary>Each fanned round's hammer slap: the dry-fire click (bank only; the round itself is the normal shot cue).</summary>
        public static readonly NativeKitSound Slap=new NativeKitSound{Key="gun.dryfire",Clip=null,Gain=.32f};
        /// <summary>Overdrive's spin-up whine (the charge cue; built-in: the electric hook, raised).</summary>
        public static readonly NativeKitSound Whine=new NativeKitSound{Key="chip.delayed.charge",Clip="electric-hook",Gain=.38f};
        public const float WhineRate=1.45f;
        /// <summary>The overheat vent's hiss (the stage-vent steam cue; built-in: the fire whoosh).</summary>
        public static readonly NativeKitSound Hiss=new NativeKitSound{Key="siren.vents",Clip="fire",Gain=.5f};

        public static float Clamp01(float v)=>v<0?0:v>1?1:v;
        public static float Smooth(float v){v=Clamp01(v);return v*v*(3-2*v);}
    }

    /// <summary>
    /// The Q card of the pistol's Fan the Hammer and the machine gun's Overdrive (FightHud.Cooldowns, AbilityStatus) from the
    /// replicated kit, the slot's ready time and the magazine: Fan shows FANNING while the volley runs and NO AMMO / RELOADING
    /// when it cannot cast; Overdrive shows its active time left (a draining dial) and VENTING through the overheat vent; both
    /// show the cooldown with its dial once those end. Icons: hammer and gauge. The rules are the simulation's.
    /// </summary>
    public static class NativeGunKitHud
    {
        public struct Card{public string State,Icon;public bool Ready,Active,Venting,Low;public float Dial;}
        /// <summary>One card. remaining: the slot's ready time less the clock; span: the cooldown the dial counts down.</summary>
        public static Card Of(string id,in NativeGunKit.State s,float time,float remaining,float span,int ammo,bool reloading)
        {
            var c=new Card{Icon=NativeGunKit.Icon(id)};
            if(id==NativeGunKit.Overdrive)
            {
                var phase=NativeGunKit.DriveAt(s,time);
                if(phase==NativeGunKit.DrivePhase.SpinUp||phase==NativeGunKit.DrivePhase.Sustain)
                {float left=s.OverdriveUntil-time;c.State="ACTIVE "+left.ToString("0.0")+"s";c.Active=true;c.Dial=NativeGunKit.Clamp01(left/Math.Max(.05f,s.OverdriveUntil-s.OverdriveAt));return c;}
                if(phase==NativeGunKit.DrivePhase.Vent)
                {float left=s.VentUntil-time;c.State="VENTING "+left.ToString("0.0")+"s";c.Venting=true;c.Dial=NativeGunKit.Clamp01(left/Math.Max(.05f,s.VentUntil-s.OverdriveUntil));return c;}
            }
            else if(id==NativeGunKit.Fan&&NativeGunKit.FanAt(s,time,out _,out _)==NativeGunKit.FanPhase.Fan){c.State="FANNING";c.Active=true;c.Dial=1;return c;}
            if(remaining>0){c.State=remaining.ToString("0.0")+"s";c.Dial=span>0?1-NativeGunKit.Clamp01(remaining/span):0;return c;}
            if(id==NativeGunKit.Fan&&(reloading||ammo<1)){c.State=reloading?"RELOADING":"NO AMMO";c.Low=true;c.Dial=1;return c;}
            c.State="READY";c.Ready=true;c.Dial=1;return c;
        }
        /// <summary>The AbilityStatus line ("Q  Overdrive · venting 0.4s"): the card's state, lower case.</summary>
        public static string Status(string name,in Card c)=>"Q  "+name+" · "+c.State.ToLowerInvariant();
    }
}
