using System;
using System.Collections.Generic;
using System.Globalization;

namespace ChipArena.UnityConversion
{
    // Pure presentation rules for the authoritative event stream (PR #66 kinds included): which events are the source's
    // ordinary `hit` (shared/simulation.ts:241 emits one only above .05 damage, burn/poison ticks among them), which still
    // draw the legacy hit burst, and the PvE "under attack" signal. Nothing here changes the match.
    public static class NativeEventFeedback
    {
        public const float HitFloor=.05f;
        // Kinds the source reports as a `hit` event: direct/secondary/stage damage (Hit, Headshot) and the DoT ticks.
        public static bool Damages(EventKind kind)=>kind==EventKind.Hit||kind==EventKind.Headshot||kind==EventKind.BurnTick||kind==EventKind.PoisonTick;
        // A Unity damage event the source would have emitted at all (`if(actual>.05)this.emit('hit',...)`).
        public static bool SourceHit(EventKind kind,float amount)=>Damages(kind)&&amount>HitFloor;
        // The kinds the legacy ParticleSystem burst was written for, plus collapse, which event-vfx.ts:29 bursts like a hit
        // (6 particles, 0xfbc98b). The other PR #66 kinds (blasts, fields, ticks, procs, PvE, DEV moves) are not ordinary
        // hits; C6/C10 own their visuals.
        public static bool HitBurst(EventKind kind)=>kind==EventKind.Hit||kind==EventKind.Headshot||kind==EventKind.Block||kind==EventKind.Death||kind==EventKind.Break||kind==EventKind.Impact||kind==EventKind.Collapse;
        // pve/tracks.ts AttackedTrack: any `hit` on me with damage, burn and poison ticks included.
        public static bool Attacked(in ArenaEvent e,int local,float now,float window)=>local>0&&e.Target==local&&SourceHit(e.Kind,e.Amount)&&now-e.Time>=0&&now-e.Time<window;
        static bool Near(V3 a,V3 b,float d)=>(a-b).Length<=d;
        // Unity's chip block pulse emits BlockPulse and then its ChipBlast's Explosion at the same spot and step
        // (NativeChips.cs:24); the source sends only `blockPulse` (simulation.ts:285 explode, kind 'blockPulse').
        public static bool PulseBlast(List<ArenaEvent> events,int index)
        {
            var e=events[index];if(e.Kind!=EventKind.Explosion)return false;
            for(int i=0;i<events.Count;i++){var q=events[i];if(q.Kind==EventKind.BlockPulse&&q.Source==e.Source&&q.Time==e.Time&&Near(q.Position,e.Position,.05f))return true;}
            return false;
        }
        // A teleport draws its beam through Fx(), which also emits Arc (NativeChips.cs:82, NativeAbilities.cs:168/179); the
        // source's teleport is a `teleport` event only (chaos.ts:51).
        public static bool TeleportBeam(List<ArenaEvent> events,int index)
        {
            var e=events[index];if(e.Kind!=EventKind.Arc)return false;
            for(int i=0;i<events.Count;i++){var q=events[i];if(q.Kind==EventKind.Teleport&&q.Time==e.Time&&Near(q.Position,e.Position,.05f))return true;}
            return false;
        }
        // Blink Strike blinks (Teleport) and blasts at its landing in the same step (NativeAbilities.cs:170-179); the source
        // tags that teleport 'blink-strike' (magic-abilities.ts:49).
        public static bool BlinkStrike(List<ArenaEvent> events,int index)
        {
            var e=events[index];if(e.Kind!=EventKind.Teleport)return false;if(e.Detail=="blink-strike")return true;
            for(int i=0;i<events.Count;i++){var q=events[i];if(q.Kind==EventKind.Explosion&&q.Detail=="ability"&&q.Source==e.Source&&q.Source>0&&q.Time==e.Time&&Near(q.Position,e.Position,.05f))return true;}
            return false;
        }
        // stage-hazards.ts:29 plays saw-cut only for the saw: HazardHit carries no hazard kind, so test the saw contact rule
        // (NativeArena.StepHazards) at the event's own time and position, with a little slack for the float clock.
        public static bool SawHit(string map,double time,V3 at)
        {
            if(map!="sawmill")return false;
            foreach(var h in NativeHazards.At(map,time))
                if(h.kind=="saw"&&Math.Abs(at.z-h.z)<.67f&&Math.Sqrt((at.x-h.x)*(at.x-h.x)+Math.Pow(Math.Max(0,Math.Abs(at.y-h.y)-.43),2))<1.67)return true;
            return false;
        }
    }

    // stage-hazards.ts:21 StageHazards.step: in the fight, from 2 s after it starts, each spikevault bed's rise cycle
    // floor((time + i*1.5 + j*.65 - 3.6) / 5) is tracked from its first sample, and a higher cycle emits stage-spikes-rise
    // at (x, .1, z); the cycles clear with the round (resetArena). Unity's hazards emit no rise event, so the presentation
    // derives it from the same NativeHazards clock.
    public sealed class NativeSpikeRises
    {
        public const int Beds=4;
        private readonly double[] cycles=new double[Beds];private readonly bool[] seen=new bool[Beds];
        public void Clear(){for(int k=0;k<Beds;k++)seen[k]=false;}
        // `live`: the fight phase and at least 2 s into it. Returns a mask of the beds (bit i*2+j) that rose at `time`.
        public int Step(string map,bool live,double time)
        {
            if(map!="spikevault"||!live){Clear();return 0;}
            int rose=0;
            for(int i=0;i<2;i++)for(int j=0;j<2;j++){int k=i*2+j;double cycle=Math.Floor((time+i*1.5+j*.65-3.6)/5);if(seen[k]&&cycle>cycles[k])rose|=1<<k;cycles[k]=cycle;seen[k]=true;}
            return rose;
        }
        public static V3 Position(int bed)=>new V3(bed/2==0?-8:8,.1f,bed%2==0?-5:5);
    }

    // renderer.ts:136/:149 with event-vfx.ts:29: a fresh impact, break, hit (not burn/poison), debrisHit, parry, blockHit,
    // reflect, blockAbsorb or collapse whose target is the local pilot sets the shake to .12 (assignment, only with the
    // Camera shake setting on). Each live local-view frame then moves the camera's world X by (random - .5) x amount and
    // the amount falls by the frame time (capped at .05 s, main.ts frame()), so a shake lasts .12 s at up to +-.06 m.
    // Unity has no parry (no weapon parries in the pinned content), debrisHit (debris hits are Hit), reflect or
    // blockAbsorb kind; Block is the source's blockHit (Source = blocker, Target = attacker).
    public static class NativeShakeRules
    {
        public const float Amount=.12f,MaxAge=.4f,MaxStep=.05f;
        public static bool Fresh(float now,float at)=>now-at<=MaxAge;
        public static bool Triggers(EventKind kind,int target,float amount,int local)
        {
            if(local<=0||target!=local)return false;
            switch(kind)
            {
                case EventKind.Hit:case EventKind.Headshot:return NativeEventFeedback.SourceHit(kind,amount);
                case EventKind.Impact:case EventKind.Break:case EventKind.Block:case EventKind.Collapse:return true;
                default:return false;
            }
        }
        public static float Offset(float amount,float random01)=>(random01-.5f)*amount;
        public static float Step(float frame)=>Math.Min(MaxStep,Math.Max(0,frame));
        public static float Decay(float amount,float frame)=>Math.Max(0,amount-Step(frame));
    }

    // combat-numbers.ts CombatNumberTrack and numberLabel, with lobby.css/match-hud.css .combat-number styling: every
    // authoritative hit (burn/poison ticks included) and heal once; a target's numbers of one kind within .16 s merge; three
    // lanes per target; a number lives 1.15 s (at most 64). In PvE the local pilot's hits on adds count too.
    public sealed class NativeCombatNumbers
    {
        public const float Life=1.15f,Merge=.16f,Fade=.48f,Rise=.9f,Drift=.2f,Head=1.15f,Ground=.35f,Pop=.16f,PopScale=1.5f;
        public const int Cap=64,Lanes=3;
        // match-hud.css:3 .combat-number.damage and lobby.css:92 .combat-number.healing: colour, the text-shadow drop and
        // the dark outline (the healing glow's 12px #38dd6655 halo is folded into its outline).
        public const int DamageColor=0xff535f,DamageShadow=0x24060a,DamageOutline=0x200308,HealColor=0x82f2a0,HealShadow=0x0c2714,HealOutline=0x102b18;
        public sealed class Number{public int Id,Target,Lane;public bool Heal;public float Amount,At;public V3 Position;public string Label;}
        public delegate bool Anchor(int target,out V3 position);
        public readonly List<Number> Numbers=new List<Number>();
        private int lastEvent,generation=-1;private float lastTime=-1;
        public void Clear(){Numbers.Clear();lastEvent=0;generation=-1;lastTime=-1;}
        // `pilot(target, out position)`: the target's body position when it is a pilot (the source's state.players).
        public List<Number> Update(List<ArenaEvent> events,float time,int gen,int local,bool pve,Anchor pilot)
        {
            if(time<lastTime||generation!=gen)Clear();generation=gen;lastTime=time;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];if(e.Id<=lastEvent)continue;lastEvent=e.Id;bool heal=e.Kind==EventKind.Heal;
                if(!heal&&!NativeEventFeedback.Damages(e.Kind)||e.Target<=0||!(e.Amount>0)||float.IsInfinity(e.Amount)||time-e.Time>Life)continue;
                bool player=pilot(e.Target,out var body);
                // A pilot hit exists in the source only above .05; a hit on a non-pilot is the source's `minionHit`, shown
                // only for my own hits in PvE (any amount).
                if(player?!heal&&!NativeEventFeedback.SourceHit(e.Kind,e.Amount):heal||!pve||local<=0||e.Source!=local)continue;
                Number old=null;for(int n=Numbers.Count-1;n>=0;n--){var q=Numbers[n];if(q.Target==e.Target&&q.Heal==heal&&e.Time-q.At>=0&&e.Time-q.At<Merge){old=q;break;}}
                if(old!=null){old.Amount+=e.Amount;old.Label=Label(old.Amount,heal);continue;}
                int lane=0;for(int n=0;n<Numbers.Count;n++)if(Numbers[n].Target==e.Target)lane++;
                Numbers.Add(new Number{Id=e.Id,Target=e.Target,Heal=heal,Amount=e.Amount,At=e.Time,Lane=lane%Lanes,Position=player?body+new V3(0,Head,0):e.Position+new V3(0,Ground,0),Label=Label(e.Amount,heal)});
            }
            int kept=0;for(int n=0;n<Numbers.Count;n++)if(time-Numbers[n].At<Life)Numbers[kept++]=Numbers[n];Numbers.RemoveRange(kept,Numbers.Count-kept);
            if(Numbers.Count>Cap)Numbers.RemoveRange(0,Numbers.Count-Cap);return Numbers;
        }
        public static float Alpha(float age)=>Math.Min(1,(Life-age)/Fade);
        public static float Scale(float age)=>1+Math.Max(0,Pop-age)*PopScale;
        // FloatingCombatNumbers.update: the world point drifts by lane and rises; a first-person pilot's own numbers sit
        // under the crosshair (x 50% +- 3% per lane, y 62% - 7%/s).
        public static V3 World(Number n,float age)=>new V3(n.Position.x+(n.Lane-1)*Drift*age,n.Position.y+age*Rise,n.Position.z);
        public static float OwnX(int lane)=>.5f+(lane-1)*.03f;
        public static float OwnY(float age)=>.62f-age*.07f;
        // numberLabel: integers at 1 and above (JS Math.round), else toFixed(1), or toFixed(2) under .1; '+' heals, U+2212.
        public static string Label(double amount,bool heal)
        {
            string value=amount>=1?((long)Math.Floor(amount+.5)).ToString(CultureInfo.InvariantCulture):Fixed(amount,amount<.1?2:1);
            return (heal?"+":"−")+value;
        }
        // Number.prototype.toFixed for 0 <= x < 1: the integer n nearest x * 10^digits from the exact binary value, ties up.
        public static string Fixed(double x,int digits)
        {
            long scale=digits==2?100:10,n=0;
            if(x>0&&!double.IsInfinity(x))
            {
                long bits=BitConverter.DoubleToInt64Bits(x);int exponent=(int)((bits>>52)&0x7FF);long mantissa=bits&((1L<<52)-1);
                if(exponent==0)exponent=1;else mantissa|=1L<<52;int shift=1075-exponent;
                // x = mantissa / 2^shift; n = floor((2*mantissa*scale + 2^shift) / 2^(shift+1)).
                if(shift<=0)n=(long)Math.Floor(x*scale+.5);else if(shift<62)n=(2*mantissa*scale+(1L<<shift))>>(shift+1);
            }
            string text=n.ToString(CultureInfo.InvariantCulture).PadLeft(digits+1,'0');
            return text.Substring(0,text.Length-digits)+"."+text.Substring(text.Length-digits);
        }
    }

    // audio.ts SoundLimiter / HIT_TRIM / cue(): per key, plays closer than the key's gap are skipped and folded into the next
    // play (louder, capped at 1.5x or at the untrimmed gain), and repeated plays duck (activity decays over .4 s, floor .35x).
    public sealed class NativeSoundLimiter
    {
        public const double Trim=.8;
        public static double Gap(string key)=>key=="hit"?.06:key=="add"||key=="proc"?.085:key=="tick"?.1:key=="hurt"?.08:key=="impact"?.05:key=="explosion"?.07:key=="arc"?.09:.06;
        private struct State{public double At,Pending,Activity;}
        private readonly Dictionary<string,State> keys=new Dictionary<string,State>();
        public void Clear()=>keys.Clear();
        public double Gate(string key,double now,double gain,double cap)
        {
            if(!keys.TryGetValue(key,out var k))k=new State{At=-9};double dt=now-k.At;
            if(dt<Gap(key)){k.Pending=Math.Min(2,k.Pending+gain);keys[key]=k;return 0;}
            k.Activity=k.Activity*Math.Exp(-dt/.4)+1;double output=Math.Min(cap,Math.Min(gain*1.5,gain+k.Pending*.3))/(1+.15*(k.Activity-1));k.At=now;k.Pending=0;keys[key]=k;
            return Math.Max(gain*.35,output);
        }
        // ArenaAudio.cue: the gain is trimmed about -2 dB and a folded burst never exceeds the untrimmed level.
        public double Cue(string key,double now,double gain)=>Gate(key,now,gain*Trim,gain);
    }

    // audio.ts ArenaAudio.update's event branch, mapped onto Unity's EventKinds and the clips the build ships. Each route is
    // a play (Gate null) or a limited cue (Gate = limiter key, at most 8 such voices); Positional cues are panned and fall
    // off from the listener. Kinds the source plays from state (fighter hits, rolls, casts, machine denials, waves) stay on
    // NativeAudio's state cues and route nothing here.
    public static class NativeEventSounds
    {
        public struct Sound{public string Clip,Gate;public float Gain,Rate;public bool Positional,Tone;}
        // What the presentation resolves about one fresh event before routing it.
        public struct Context
        {
            public EventKind Kind;public string Detail,SourceSpec,Material;public float Amount,Radius;
            public bool TargetMe,SourceMe,TargetPilot,Ally,Pve,Supernova,Saw,Shadowed,BlinkStrike;
        }
        public const int MaxSounds=3;
        // audio.ts: `stage-spikes-rise` plays spike-rise at the bed (NativeSpikeRises derives the event).
        public static readonly Sound SpikeRise=new Sound{Clip="spike-rise",Gain=1,Positional=true};
        // ArenaAudio.play: a fresh rate of .97-1.03 per play, except these clips (and explicit rates).
        public static float Rate(string clip,float random01)=>clip=="hit"||clip=="ui"||clip=="countdown"||clip=="demon-step"||clip=="electric-hook"||clip=="electric-hook-hit"?1:.97f+random01*.06f;
        public static float Falloff(float distance)=>1/(1+Math.Max(0,distance-2)*.13f);
        // ArenaAudio.play pans by the listener's right vector. The source world is right-handed (right = (-cos, 0, sin) of
        // yaw); Unity reuses the same coordinates in its left-handed world, where the camera's right is (cos, 0, -sin), so the
        // sign flips here to keep a sound on the side the player sees it.
        public static float Pan(V3 delta,float yawRadians)
        {
            float d=delta.Length;return Math.Max(-.85f,Math.Min(.85f,((float)Math.Cos(yawRadians)*delta.x-(float)Math.Sin(yawRadians)*delta.z)/Math.Max(2,d)));
        }
        static int Add(Sound[] o,int n,string clip,float gain,bool positional,string gate=null,float rate=0){if(n<o.Length)o[n]=new Sound{Clip=clip,Gain=gain,Positional=positional,Gate=gate,Rate=rate};return n+1;}
        public static int Route(in Context e,Sound[] o)
        {
            int n=0;
            switch(e.Kind)
            {
                case EventKind.Shot:if(!string.IsNullOrEmpty(e.SourceSpec)&&e.SourceSpec!="siren")n=Add(o,n,e.SourceSpec=="leet"||e.SourceSpec=="emperor"?"lightning":e.SourceSpec=="ansatsuken"?"fire":e.SourceSpec,.65f,true);break;
                // blockHit (Source = blocker): parry, except an Ansatsuken blocker, whose arcade parry plays instead.
                case EventKind.Block:n=e.SourceSpec=="ansatsuken"?Add(o,n,"arcade-parry",.95f,true):Add(o,n,"parry",.65f,true);break;
                case EventKind.ReloadStart:case EventKind.ReloadEnd:if(e.SourceMe)n=Add(o,n,e.Kind==EventKind.ReloadStart?"reload-start":"reload-end",.6f,false);break;
                case EventKind.BurnTick:case EventKind.PoisonTick:if(e.Amount>NativeEventFeedback.HitFloor)n=Add(o,n,e.Kind==EventKind.BurnTick?"burn-tick":"poison-tick",.24f,true,"tick");break;
                case EventKind.Hit:case EventKind.Headshot:
                    // A hit on a non-pilot is the source's minionHit: only my hits on PvE adds sound (pveCue, any amount).
                    if(!e.TargetPilot){if(e.Pve&&e.SourceMe&&e.Amount>0)n=Add(o,n,"hit",.55f,false,"add");break;}
                    if(!(e.Amount>NativeEventFeedback.HitFloor))break;
                    if(e.TargetMe)n=Add(o,n,"hurt",.6f,false,"hurt");else if(e.SourceMe&&!e.Ally)n=Add(o,n,"hit",.9f,false,"hit");break;
                case EventKind.Impact:n=Add(o,n,e.Material=="metal"?"metal":e.Material=="wood"?"wood":"stone",.22f,true,"impact");break;
                case EventKind.Break:n=Add(o,n,e.Material=="metal"?"metal":e.Material=="wood"?"wood":"stone",.65f,true);break;
                // `death` is a pilot's only: a PvE add's kill (pve-kill, horde.ts:445) and a summon's (minionDeath, chaos.ts:113)
                // are silent, also when KillPveHorde clears the remaining adds.
                case EventKind.Death:if(e.TargetPilot){n=Add(o,n,"stone",.65f,true);n=Add(o,n,"explosion",.3f,true);}break;
                // BLAST_CUE is keyed by the ability fx tag; Unity's Explosion Detail is only chip/ability, so every blast and
                // collapse takes the default layer. A block pulse's own blast is the pulse.
                case EventKind.Explosion:if(!e.Shadowed)n=Add(o,n,"explosion",.65f,true,"explosion");break;
                case EventKind.Collapse:n=Add(o,n,"explosion",.65f,true,"explosion");break;
                case EventKind.BlockPulse:n=Add(o,n,"explosion",e.Supernova?.8f:.35f,true);break;
                // Only ball-lightning's chain (radius .15, NativeAbilities.cs:193) is a source `arc`; the Leet orb lock (.25,
                // NativeLeet.cs:54) is `leet-lock`; teleport beams and the other Fx beams (Leet possession and Slender hops,
                // summon shots) have no source cue. An Arc that carries a Detail is taken as a real arc.
                case EventKind.Arc:
                    if(e.Shadowed)break;
                    if(e.Detail=="ball-lightning"||string.IsNullOrEmpty(e.Detail)&&Math.Abs(e.Radius-.15f)<.01f)n=Add(o,n,"lightning",.5f,true,"arc");else if(e.Detail=="leet-lock"||string.IsNullOrEmpty(e.Detail)&&Math.Abs(e.Radius-.25f)<.01f)n=Add(o,n,"lightning",.35f,true);
                    break;
                case EventKind.Teleport:if(e.BlinkStrike)n=Add(o,n,"lightning",.6f,true);break;
                // The panic-button field is the source's `nova` (chaos.ts:72), which shares the arc cue, and a Thunder Step bolt
                // locked on a pilot comes with its `arc` (chaos.ts:100; the no-target fallback bomb has none).
                case EventKind.Field:if(e.Detail=="panic-button"||e.Detail=="thunderstep"&&e.TargetPilot)n=Add(o,n,"lightning",.5f,true,"arc");break;
                case EventKind.CeilingHit:n=Add(o,n,"parry",.45f,true);break;
                case EventKind.HazardHit:if(e.Saw)n=Add(o,n,"saw-cut",1.1f,true);break;
                case EventKind.Install:
                    if(e.Detail!=null&&e.Detail.StartsWith("mishima-",StringComparison.Ordinal)){n=Add(o,n,"lightning",.9f,false);n=Add(o,n,"arcade-heavy",.8f,false);}
                    else if(e.Detail!=null&&e.Detail.StartsWith("arcade-",StringComparison.Ordinal)){if(n<o.Length)o[n]=new Sound{Clip="arcade-tone",Tone=true,Gain=1};n++;n=Add(o,n,"arcade-heavy",.7f,false);}
                    break;
                // pveCue: buy (my own), gib, the reveal cinematic's phase, and my revive.
                case EventKind.PveBuy:if(e.SourceMe)n=Add(o,n,"ui",.6f,false);break;
                case EventKind.PveGib:n=Add(o,n,"hit",.35f,true,"impact");break;
                case EventKind.PvePhase:if(e.Detail=="reveal"){n=Add(o,n,"darkness",.7f,false);n=Add(o,n,"metal",.35f,false);}break;
                case EventKind.PveRevive:if(e.TargetMe)n=Add(o,n,"go",.6f,false);break;
                // No source cue: vacuum, freeze, critical, the chip catch, split, ricochet, pong, Leet impacts, fields,
                // summons, heals and PvE cash. Emperor/Arcade hits and Siren moves play from NativeAudio's fighter state.
                // Source cues with no Unity event yet (Astra): the lightning weapon's chain arc (magic-abilities.ts:82), Blink
                // Strike's per-target arcs (:51), leet-freeze/leet-undo (darkness .6) and leet-brain (lightning .35), and the
                // shield-throw catch (shield .45). NativeAudio derives the spike rise and the PvP draft install from state.
                case EventKind.Vacuum:case EventKind.Freeze:case EventKind.Critical:case EventKind.Catch:case EventKind.Split:case EventKind.Ricochet:case EventKind.Pong:
                case EventKind.Leet:case EventKind.Emperor:case EventKind.Arcade:case EventKind.Siren:case EventKind.PveMoney:case EventKind.Summon:case EventKind.Heal:case EventKind.Dodge:
                default:break;
            }
            return Math.Min(n,o.Length);
        }
    }
}
