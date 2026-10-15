using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The DEV Cyber Lord's ranged mode (owner 2026-10-13: "Give cyber lord a 2nd set of abilities for a "Ranged" mode. So,
    /// new 1-4 if they switch to ranged :)[We can set this to the 5 hotkey to switch)"): Claude's proposed kit and its numbers
    /// (docs/CYBER-LORD.md "Ranged mode"; Astra builds the rules). Key 5 reconfigures the hardlight fists into emitters and
    /// back. In ranged mode LMB fires Bit Bolts and 1-4 are Ping Lance, Lag Field, Proxy Swap and Root Access; RMB, SHIFT, Q
    /// and E are unchanged. The presentation keys on these to size its effects and drive its fixtures; once the rules land,
    /// the replicated record always wins (CyberLordFeed).
    /// </summary>
    public static class CyberRangedSpec
    {
        /// <summary>The ranged set's slots (the view's R* arrays): the LMB bolt, then 1-4.</summary>
        public const int Bolt=0,Lance=1,Field=2,Swap=3,Root=4,Slots=5;
        public static readonly string[] Keys={"LMB","1","2","3","4"};
        public static readonly string[] Names={"Bit Bolt","Ping Lance","Lag Field","Proxy Swap","Root Access"};
        public static readonly string[] Ids={"bit-bolt","ping-lance","lag-field","proxy-swap","root-access"};
        /// <summary>Roles (the design table): sustained poke, long poke, zone, escape, finisher.</summary>
        public static readonly string[] Roles={"sustained poke","long poke","zone","escape","finisher"};
        /// <summary>Cooldowns (s; the bolt's is its fire interval) and mana costs. Root Access shares Kernel Panic's 20 s clock.</summary>
        public static readonly float[] Cooldowns={.26f,5,13,11,NativeKernelPanicRules.Cooldown},Costs={0,15,30,25,NativeKernelPanicRules.Mana};
        /// <summary>Each cast's windup (Release = At + windup) and length (Until = At + length) at attack rate 1 (the bolt scales
        /// with the attack rate, as the combo does).</summary>
        public static readonly float[] Windup={.05f,.18f,.16f,.04f,1f},Seconds={.26f,.32f,.3f,.12f,1.3f};
        /// <summary>The presentation's settle after a cast's end (not a rules window; a newer cast takes over at once).</summary>
        public static readonly float[] Settle={.12f,.25f,.3f,.25f,.45f};
        /// <summary>Line icons in the class icon style (ClassIcons.json, tools/export_class_icons.mjs).</summary>
        public static readonly string[] Icons={"ability-bit-bolt","ability-ping-lance","ability-lag-field","ability-proxy-swap","ability-root-access"};
        public const string ModeIcon="ability-cyber-ranged",MeleeIcon="ability-cyber-melee",ModeKey="5";
        public static int Slot(string id){for(int i=0;i<Ids.Length;i++)if(Ids[i]==id)return i;return -1;}

        // ---- the switch (key 5) ----
        /// <summary>The reconfiguration (s): the pilot moves but cannot attack, cast, block or dash; the next switch after
        /// SwitchCooldown from its start. Refused while any cast, swing, dash, link, Kernel Panic or Root charge is live, while
        /// stunned, rolling, juggled or dead; mid-cast it is refused, never queued.</summary>
        public const float SwitchSeconds=.3f,SwitchCooldown=1;
        // ---- LMB Bit Bolt ----
        /// <summary>Alternate-hand hardlight bolts: damage, speed (m/s), radius and range (m), mana back per pilot hit.</summary>
        public const float BoltDamage=9,BoltSpeed=64,BoltRadius=.12f,BoltRange=40,BoltMana=3;
        // ---- 1 Ping Lance ----
        /// <summary>A thin instant lance along the aim at the release: damage per pilot, range, radius, pilots pierced.</summary>
        public const float LanceDamage=22,LanceRange=45,LanceRadius=.15f;public const int LancePierce=2;
        // ---- 2 Lag Field ----
        /// <summary>A lobbed data cube to the aimed floor point (clamped to FieldRange): flight time from the distance (FieldFlight),
        /// the field's radius, its forming, its life, its ticks and their damage, the slow on enemies inside.</summary>
        public const float FieldRange=22,FieldRadius=4.5f,FieldForm=.35f,FieldSeconds=4,FieldTick=.5f,FieldDamage=7,FieldSlow=.35f;
        public static float FieldFlight(float distance)=>MeleeMath.Clamp(distance/16,.35f,1.1f);
        // ---- 3 Proxy Swap ----
        /// <summary>A glitch blink up to SwapDistance toward the move input (straight back without one), path-constrained like
        /// Data Dash; a hardlight proxy mine left at the start bursts after MineFuse: radius, damage, shove (m/s).</summary>
        public const float SwapDistance=9,MineFuse=.8f,MineRadius=3,MineDamage=18,MineShove=3;
        // ---- 4 Root Access ----
        /// <summary>A charged railbeam: rooted through the 1 s charge (aim still turns), the telegraph line shown from TellFrom,
        /// then an instant beam: range, radius, damage plus a share of the target's missing health, capped; a block halves it,
        /// a Firewall or a wall stops it. Its cooldown is Kernel Panic's (one ultimate clock for both 4s).</summary>
        public const float RootRange=45,RootRadius=.4f,RootDamage=45,RootMissing=.3f,RootCap=75,TellFrom=.2f;
    }

    /// <summary>
    /// The ranged mode's replicated record as the contract proposes it (docs/CYBER-LORD.md "Ranged mode"): the mode on the
    /// actor (Actor.SourceRanged / SourceModeAt, the Siren's mode fields, already on the wire) and the casts on the kit. Until
    /// the rules write it, only the Dev Sandbox preview and the fixtures do (CyberRangedPreview); the presentation reads it
    /// through CyberLordFeed only, so Astra's real names change one mapping. Points are world metres (beams from the emitter
    /// height; the field and the swap at the feet); times are the match clock; -100 is never.
    /// </summary>
    [Serializable] public sealed class CyberRangedRecord
    {
        public bool Ranged;public float ModeAt=-100,ModeReady=-100;
        /// <summary>Each slot's window (CyberRangedSpec order: Bolt, Lance, Field, Swap, Root) and its ready time.</summary>
        public float[] At=New(),Release=New(),Until=New(),Ready=New();
        /// <summary>The bolt's hand (0 right, 1 left: they alternate), the lance's line, the field's throw, landing and life, the
        /// swap's path and its mine's burst, the beam's line.</summary>
        public int BoltHand;public Vector3 LanceFrom,LanceTo,FieldFrom,FieldPoint,SwapFrom,SwapTo,RootFrom,RootTo;public float FieldLand=-100,FieldUntil=-100,MineAt=-100;
        private static float[] New(){var a=new float[CyberRangedSpec.Slots];for(int i=0;i<a.Length;i++)a[i]=-100;return a;}
        public CyberRangedRecord ViewCopy(){var c=(CyberRangedRecord)MemberwiseClone();c.At=(float[])At.Clone();c.Release=(float[])Release.Clone();c.Until=(float[])Until.Clone();c.Ready=(float[])Ready.Clone();return c;}
    }

    /// <summary>A Cyber Lord hardlight projectile the effects draw (a replicated shot of Spec cyberlord, or the preview's).</summary>
    public struct CyberShot{public int Id,Owner,Slot;public Vector3 Position,Velocity;public float Born;
        /// <summary>The fireball's kind (CyberKit2RangedFeed.ShotKind): 0 a v1 Bit Bolt, 1 a Plasma Byte, 2 an Overflow.</summary>
        public int Kind;}
    /// <summary>Where a ranged cast ended (the preview's stand-in for the contract's Hit / Impact events with the ranged Details):
    /// its owner and slot, the point and the surface's normal toward the caster, when, and whether a pilot was hit.</summary>
    public struct CyberImpact{public int Owner,Slot;public Vector3 Position,Normal;public float At;public bool Pilot;public int Kind;}
    /// <summary>Where the ranged record comes from before the rules replicate it (the runtime's preview; the checks' fixture).</summary>
    public interface ICyberRangedSource{CyberRangedRecord Of(int actor);}
    /// <summary>The scene's hardlight shots (optional on an ICyberFxScene: the runtime's replicated and previewed bolts).</summary>
    public interface ICyberShots{int CyberShots{get;}CyberShot CyberShotAt(int i);int CyberImpacts{get;}CyberImpact CyberImpactAt(int i);}

    /// <summary>
    /// The ranged mode's stand-in source: the Dev Sandbox preview (presentation only, the local pilot's own screen) and the
    /// checks' and render harness's fixture write records the way the proposed host would (CyberLordFeed reads them for the
    /// pilots it holds). Its bolts fly straight at BoltSpeed to their end (a pilot hit, a wall the caller found, or the
    /// range), and every end leaves an impact the effects draw, as the proposed Hit / Impact events will. Nothing reaches
    /// the authority, the actor or the network frame.
    /// </summary>
    public sealed class CyberRangedPreview:ICyberRangedSource,ICyberShots
    {
        private readonly Dictionary<int,CyberRangedRecord> records=new Dictionary<int,CyberRangedRecord>();
        public CyberRangedRecord Of(int actor)=>records.TryGetValue(actor,out var r)?r:null;
        public CyberRangedRecord For(int actor){if(!records.TryGetValue(actor,out var r)){r=new CyberRangedRecord();records[actor]=r;}return r;}
        public void Clear(){records.Clear();boltCount=0;impactCount=0;}
        public bool Any=>records.Count>0;

        // ---- the bolts in flight and the impacts (fixed rings; no allocation) ----
        public struct Flight{public int Id,Owner;public Vector3 From,Dir;public float At,EndAt;}
        public const int MaxBolts=16,MaxImpacts=24;
        private readonly Flight[] bolts=new Flight[MaxBolts];private int boltCount,boltId;
        private readonly CyberImpact[] impacts=new CyberImpact[MaxImpacts];private int impactCount;private float clock;
        public int CyberImpacts=>impactCount;public CyberImpact CyberImpactAt(int i)=>impacts[i];
        private void AddImpact(int owner,int slot,Vector3 at,Vector3 normal,float time,bool pilot)
        {if(impactCount>=MaxImpacts){Array.Copy(impacts,1,impacts,0,MaxImpacts-1);impactCount--;}impacts[impactCount++]=new CyberImpact{Owner=owner,Slot=slot,Position=at,Normal=normal,At=time,Pilot=pilot};}
        /// <summary>The bolts alive at `time` (the effects read them through ICyberShots).</summary>
        public void Advance(float time)
        {
            if(time<clock-.25f){boltCount=0;impactCount=0;}clock=time;
            for(int i=boltCount-1;i>=0;i--)if(time>=bolts[i].EndAt+.05f||time<bolts[i].At-.25f){Array.Copy(bolts,i+1,bolts,i,boltCount-i-1);boltCount--;}
            for(int i=impactCount-1;i>=0;i--)if(time>impacts[i].At+2||time<impacts[i].At-.25f){Array.Copy(impacts,i+1,impacts,i,impactCount-i-1);impactCount--;}
            shown=0;for(int i=0;i<boltCount;i++)if(time>=bolts[i].At&&time<bolts[i].EndAt)shown++;
        }
        private int shown;
        public int CyberShots=>shown;
        public CyberShot CyberShotAt(int i)
        {
            int k=-1;for(int j=0;j<boltCount;j++)if(clock>=bolts[j].At&&clock<bolts[j].EndAt&&++k==i){var b=bolts[j];return new CyberShot{Id=b.Id,Owner=b.Owner,Slot=CyberRangedSpec.Bolt,Position=b.From+b.Dir*(CyberRangedSpec.BoltSpeed*(clock-b.At)),Velocity=b.Dir*CyberRangedSpec.BoltSpeed,Born=b.At};}
            return default;
        }

        // ---- the writers (the proposed host's casts) ----
        /// <summary>Key 5 at `time`: refused (false) while the record or `busy` says a cast is live or the switch is cooling down.</summary>
        public bool Switch(int actor,float time,bool busy=false)
        {
            var r=For(actor);if(busy||time<r.ModeReady||Live(r,time))return false;
            r.Ranged=!r.Ranged;r.ModeAt=time;r.ModeReady=time+CyberRangedSpec.SwitchCooldown;return true;
        }
        /// <summary>Whether any ranged cast of the record is live at `time` (a switch is refused through it).</summary>
        public static bool Live(CyberRangedRecord r,float time){for(int s=0;s<CyberRangedSpec.Slots;s++)if(r.At[s]>-90&&time>=r.At[s]&&time<r.Until[s])return true;return time<r.ModeAt+CyberRangedSpec.SwitchSeconds;}
        private static void Window(CyberRangedRecord r,int slot,float at,float rate=1)
        {r.At[slot]=at;r.Release[slot]=at+CyberRangedSpec.Windup[slot]/rate;r.Until[slot]=at+CyberRangedSpec.Seconds[slot]/rate;r.Ready[slot]=at+(slot==CyberRangedSpec.Bolt?CyberRangedSpec.Seconds[slot]/rate:CyberRangedSpec.Cooldowns[slot]);}
        /// <summary>A bolt from `from` along `dir` (unit), ending at `end` metres (a pilot or a wall the caller found; the range
        /// otherwise); `pilot` marks a pilot hit.</summary>
        public void Bolt(int actor,float time,Vector3 from,Vector3 dir,float end=CyberRangedSpec.BoltRange,bool pilot=false)
        {
            var r=For(actor);Window(r,CyberRangedSpec.Bolt,time);r.BoltHand=r.BoltHand==0?1:0;float rel=r.Release[CyberRangedSpec.Bolt];
            end=MeleeMath.Clamp(end,.2f,CyberRangedSpec.BoltRange);float fly=end/CyberRangedSpec.BoltSpeed;
            if(boltCount>=MaxBolts){Array.Copy(bolts,1,bolts,0,MaxBolts-1);boltCount--;}
            bolts[boltCount++]=new Flight{Id=++boltId,Owner=actor,From=from,Dir=dir,At=rel,EndAt=rel+fly};
            if(end<CyberRangedSpec.BoltRange-1e-3f||pilot)AddImpact(actor,CyberRangedSpec.Bolt,from+dir*end,-dir,rel+fly,pilot);
        }
        public void Lance(int actor,float time,Vector3 from,Vector3 to,Vector3 hit=default,bool pilot=false)
        {
            var r=For(actor);Window(r,CyberRangedSpec.Lance,time);r.LanceFrom=from;r.LanceTo=to;float rel=r.Release[CyberRangedSpec.Lance];
            AddImpact(actor,CyberRangedSpec.Lance,to,Vector3.Normalize(from-to),rel,false);if(pilot)AddImpact(actor,CyberRangedSpec.Lance,hit,Vector3.Normalize(from-hit),rel,true);
        }
        public void Field(int actor,float time,Vector3 from,Vector3 point)
        {
            var r=For(actor);Window(r,CyberRangedSpec.Field,time);float rel=r.Release[CyberRangedSpec.Field];var d=point-from;d.Y=0;
            r.FieldFrom=from;r.FieldPoint=point;r.FieldLand=rel+CyberRangedSpec.FieldFlight(d.Length());r.FieldUntil=r.FieldLand+CyberRangedSpec.FieldSeconds;
        }
        public void Swap(int actor,float time,Vector3 from,Vector3 to)
        {
            var r=For(actor);Window(r,CyberRangedSpec.Swap,time);r.SwapFrom=from;r.SwapTo=to;r.MineAt=r.Release[CyberRangedSpec.Swap]+CyberRangedSpec.MineFuse;
        }
        public void Root(int actor,float time,Vector3 from,Vector3 to,Vector3 hit=default,bool pilot=false)
        {
            var r=For(actor);Window(r,CyberRangedSpec.Root,time);r.RootFrom=from;r.RootTo=to;float rel=r.Release[CyberRangedSpec.Root];
            AddImpact(actor,CyberRangedSpec.Root,to,Vector3.Normalize(from-to),rel,false);if(pilot)AddImpact(actor,CyberRangedSpec.Root,hit,Vector3.Normalize(from-hit),rel,true);
        }
        /// <summary>An interrupt at `time` (a block, roll or stun): the live cast ends there, as StepCyberWorld cuts the kit.</summary>
        public void Interrupt(int actor,float time)
        {
            var r=Of(actor);if(r==null)return;
            for(int s=0;s<CyberRangedSpec.Slots;s++)if(r.At[s]>-90&&time>=r.At[s]&&time<r.Until[s])
            {
                bool before=r.Release[s]>time;r.Until[s]=time;if(!before)continue;r.Release[s]=time;
                // Cut before its release: nothing left the hands.
                if(s==CyberRangedSpec.Field)r.FieldLand=r.FieldUntil=-100;else if(s==CyberRangedSpec.Swap)r.MineAt=-100;
            }
        }
    }
}
