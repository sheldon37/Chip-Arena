using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>A zombie's call, by what it is doing (NativeZombieVoice).</summary>
    public enum NativeZombieCall:byte{None,Moan,Groan,Snarl,Shriek,Attack,Grunt,Gurgle,Spit,Hurt,Death}

    /// <summary>
    /// The zombies' voices (owner 2026-10-15: "can we add sounds for footsteps and zombies?"; the clips are requested in
    /// docs/audio/SOUND-REQUESTS.md). Presentation only: every call is bound to state every peer already has, and nothing here is
    /// read by the authority.
    /// <list type="bullet">
    /// <item>Calm (the town's roaming record wandering, idling or searching): sparse moans (NativeHordeRoam.MoanAt).</item>
    /// <item>Hunting (the town's chase, every instance horde): a groan, a runner's snarl, in seeded slots (<see cref="CallAt"/>).</item>
    /// <item>The town's alert: the shriek with the scream's opening (NativeHordeRoam.ShriekAt).</item>
    /// <item>Each attack's wind-up from the replicated LastAttack: a lunge or a swipe snarls, a smash grunts, a spit or a belly
    /// gurgles; the glob leaves with the spit at LastAttack + the strike.</item>
    /// <item>A replicated hit on an add hurts it; its death (the replicated MinionDeaths) rattles.</item>
    /// </list>
    /// The seeded moments are pure functions of the add's seed and the clock (the same on every peer); which of them a listener
    /// hears is <see cref="NativeZombieVoices"/>: one call at a time per add, a global cap, and the nearest first.
    /// </summary>
    public static class NativeZombieVoice
    {
        public static string Key(NativeZombieCall c)
        {
            switch(c)
            {
                case NativeZombieCall.Moan:return "pve.zombie.moan";case NativeZombieCall.Groan:return "pve.zombie.groan";case NativeZombieCall.Snarl:return "pve.zombie.runner.snarl";
                case NativeZombieCall.Shriek:return "pve.zombie.shriek";case NativeZombieCall.Attack:return "pve.zombie.attack";case NativeZombieCall.Grunt:return "pve.zombie.brute.grunt";
                case NativeZombieCall.Gurgle:return "pve.zombie.spitter.gurgle";case NativeZombieCall.Spit:return "pve.zombie.spitter.spit";case NativeZombieCall.Hurt:return "pve.zombie.hurt";
                case NativeZombieCall.Death:return "pve.zombie.death";default:return null;
            }
        }
        /// <summary>How much a call matters when the cap is full: a death and a shriek most, the telegraphs (wind-ups, the spit) next,
        /// a hurt and a hunting groan, then the calm moans.</summary>
        public static int Rank(NativeZombieCall c)=>c==NativeZombieCall.Death||c==NativeZombieCall.Shriek?3:c==NativeZombieCall.Attack||c==NativeZombieCall.Grunt||c==NativeZombieCall.Gurgle||c==NativeZombieCall.Spit?2:c==NativeZombieCall.Moan?0:1;
        /// <summary>The call's gain (the bank's trim and the distance falloff apply on top).</summary>
        public static float Gain(NativeZombieCall c)
        {
            switch(c)
            {
                case NativeZombieCall.Moan:return NativeHordeRoam.MoanGain;case NativeZombieCall.Groan:return .4f;case NativeZombieCall.Snarl:return .45f;case NativeZombieCall.Shriek:return NativeHordeRoam.ShriekGain;
                case NativeZombieCall.Attack:return .62f;case NativeZombieCall.Grunt:return .7f;case NativeZombieCall.Gurgle:return .55f;case NativeZombieCall.Spit:return .65f;
                case NativeZombieCall.Hurt:return .5f;case NativeZombieCall.Death:return .7f;default:return 0;
            }
        }
        public static NativeSoundPriority Priority(NativeZombieCall c)=>Rank(c)>=2?NativeSoundPriority.Normal:NativeSoundPriority.Low;
        /// <summary>The add's voice pitch: the moan's per kind and seed, the Hulk lower, the crawler and the spitter higher.</summary>
        public static float Rate(uint seed,int kind)=>NativeHordeRoam.MoanRate(seed,kind)*(kind==NativeHordeKinds.Hulk?.72f:kind==NativeHordeKinds.Crawler?1.08f:kind==NativeHordeKinds.Spitter?1.04f:1);
        /// <summary>About how long a call holds the add's voice (s), for the per-add cooldown and the cap (the clips' own lengths vary).</summary>
        public static float Seconds(NativeZombieCall c)
        {
            switch(c)
            {
                case NativeZombieCall.Moan:return 2.2f;case NativeZombieCall.Shriek:return 1.2f;case NativeZombieCall.Death:return 1.3f;case NativeZombieCall.Gurgle:return .9f;
                case NativeZombieCall.Groan:case NativeZombieCall.Grunt:return .9f;case NativeZombieCall.Snarl:return .8f;case NativeZombieCall.Attack:return .6f;case NativeZombieCall.Spit:return .5f;
                default:return .4f;
            }
        }
        /// <summary>The existing bank key a call borrows while its own key has no clips (a LocalAssets bank from before these keys),
        /// and its pitch; null: none (the death's old grunt is the event's own extra, NativeSoundCues.Extras).</summary>
        public static string StandIn(NativeZombieCall c)=>c==NativeZombieCall.Shriek?"siren.rush":c==NativeZombieCall.Moan||c==NativeZombieCall.Groan||c==NativeZombieCall.Snarl||c==NativeZombieCall.Grunt||c==NativeZombieCall.Hurt?"pve.add.death":null;
        public static float StandInRate(NativeZombieCall c)=>c==NativeZombieCall.Shriek?1.08f:c==NativeZombieCall.Moan?.74f:c==NativeZombieCall.Grunt?.7f:c==NativeZombieCall.Snarl?1.12f:c==NativeZombieCall.Hurt?1.1f:.86f;
        /// <summary>The hunting call of a kind: a runner snarls, the brute and the bloater grunt low, the rest groan.</summary>
        public static NativeZombieCall Hunting(int kind)=>kind==NativeHordeKinds.Runner?NativeZombieCall.Snarl:NativeZombieCall.Groan;
        /// <summary>The call that opens an attack's wind-up (at LastAttack): a spit or a belly gurgles, a smash grunts, the rest snarl.</summary>
        public static NativeZombieCall Windup(NativeHordeKinds.Attack a)=>a==NativeHordeKinds.Attack.Spit||a==NativeHordeKinds.Attack.Belly?NativeZombieCall.Gurgle:a==NativeHordeKinds.Attack.Smash?NativeZombieCall.Grunt:NativeZombieCall.Attack;
        /// <summary>The call at the strike (LastAttack + the strike time): the spit leaving the mouth; none for the others (the strike's
        /// own cue carries it).</summary>
        public static NativeZombieCall Strike(NativeHordeKinds.Attack a)=>a==NativeHordeKinds.Attack.Spit?NativeZombieCall.Spit:NativeZombieCall.None;

        /// <summary>The hunting calls' slots: one in each 5-9 s slot (by the seed), six slots in ten voiced.</summary>
        public const float CallPeriodMin=5,CallPeriodSpan=4,CallVoiced=.6f;
        public static float CallPeriod(uint seed)=>CallPeriodMin+CallPeriodSpan*H(seed,50);
        /// <summary>When the hunting call of the slot holding `time` starts (NaN: this slot is silent). A pure function of the seed and
        /// the clock (every peer calls at the same moments).</summary>
        public static float CallAt(uint seed,float time)
        {
            float period=CallPeriod(seed),k=time/period+H(seed,51);int n=(int)Math.Floor(k);if(H(seed,n,52)>=CallVoiced)return float.NaN;
            return (n-H(seed,51))*period+H(seed,n,53)*(period-1.2f);
        }
        private static float H(uint seed,int k)=>NativeHordeKinds.Hash(seed,960+k);
        private static float H(uint seed,int n,int salt)=>(float)NativeGoreRules.Hash01(seed,n,0x7c0+salt);
    }

    /// <summary>
    /// Which zombie calls a listener hears (presentation only; no allocation once warm): one call at a time per add (its voice is
    /// busy for the call's <see cref="NativeZombieVoice.Seconds"/> plus <see cref="AddGap"/>; a hurt at most every
    /// <see cref="HurtGap"/>); at most <see cref="MaxCalm"/> moans and groans sounding at once, <see cref="MaxAll"/> calls of any
    /// kind, <see cref="MoanGap"/> between moans; nothing past <see cref="Range"/>. A death and a shriek always take a slot while
    /// fewer than <see cref="MaxAll"/> sound. A clock going back (a new round, a rewind) forgets the voices.
    /// </summary>
    public sealed class NativeZombieVoices
    {
        public const float Range=32,AddGap=.35f,HurtGap=.55f,MoanGap=NativeHordeRoam.MoanGap,Forget=3;
        public const int MaxCalm=3,MaxAll=6;
        /// <summary>The moments an add voices once each (<see cref="First"/>): its attack's wind-up, its strike, its seeded call (a moan
        /// or a hunting call), its alert's shriek, its death.</summary>
        public const int AttackSlot=0,StrikeSlot=1,CallSlot=2,AlertSlot=3,DeathSlot=4;
        private struct Voice{public float BusyUntil,HurtAt,Seen,S0,S1,S2,S3,S4;public int Kind;}
        private readonly Dictionary<int,Voice> voices=new Dictionary<int,Voice>();private readonly List<int> stale=new List<int>();
        private readonly float[] ends=new float[MaxAll];private readonly int[] ranks=new int[MaxAll];private float moanNext=-1,last=float.NaN,sweep;
        public int Tracked=>voices.Count;
        public void Clear(){voices.Clear();for(int i=0;i<ends.Length;i++)ends[i]=float.NegativeInfinity;moanNext=-1;last=float.NaN;sweep=0;}
        public NativeZombieVoices(){Clear();}
        /// <summary>Marks the add seen this frame (its kind for its death call) and sweeps the long-gone.</summary>
        public void Seen(int id,int kind,float now)
        {
            Clock(now);var v=Get(id);v.Seen=now;v.Kind=kind;voices[id]=v;
            if(now>=sweep){sweep=now+1;stale.Clear();foreach(var p in voices)if(now-p.Value.Seen>Forget)stale.Add(p.Key);for(int i=0;i<stale.Count;i++)voices.Remove(stale[i]);}
        }
        /// <summary>The kind last seen for `id` (its death call's pitch), -1 if unknown.</summary>
        public int KindOf(int id)=>voices.TryGetValue(id,out var v)?v.Kind:-1;
        /// <summary>Whether add `id`'s `call` at `distance` m plays now; true stamps its voice and a cap slot.</summary>
        public bool Admit(int id,NativeZombieCall call,float now,float distance)
        {
            Clock(now);if(call==NativeZombieCall.None||!(distance<=Range))return false;
            var v=Get(id);int rank=NativeZombieVoice.Rank(call);
            if(call!=NativeZombieCall.Death&&now<v.BusyUntil&&!(rank>=3))return false;
            if(call==NativeZombieCall.Hurt&&now-v.HurtAt<HurtGap)return false;
            bool calm=call==NativeZombieCall.Moan||call==NativeZombieCall.Groan||call==NativeZombieCall.Snarl;
            if(call==NativeZombieCall.Moan&&now<moanNext)return false;
            int sounding=0,calmSounding=0,free=-1,weakest=-1;
            for(int i=0;i<ends.Length;i++)
            {
                if(ends[i]>now){sounding++;if(ranks[i]<=1)calmSounding++;if(weakest<0||ranks[i]<ranks[weakest])weakest=i;}
                else if(free<0)free=i;
            }
            if(calm&&calmSounding>=MaxCalm)return false;
            int slot=free;if(slot<0){if(rank>=3&&weakest>=0&&ranks[weakest]<rank)slot=weakest;else return false;}
            float hold=NativeZombieVoice.Seconds(call);ends[slot]=now+hold;ranks[slot]=rank;
            v.BusyUntil=now+hold+AddGap;if(call==NativeZombieCall.Hurt)v.HurtAt=now;voices[id]=v;
            if(call==NativeZombieCall.Moan)moanNext=now+MoanGap;
            return true;
        }
        /// <summary>How many calls sound at `now` (the cap's view).</summary>
        public int Sounding(float now){int n=0;for(int i=0;i<ends.Length;i++)if(ends[i]>now)n++;return n;}
        private Voice Get(int id)=>voices.TryGetValue(id,out var v)?v:new Voice{BusyUntil=float.NegativeInfinity,HurtAt=float.NegativeInfinity,Seen=float.NegativeInfinity,S0=float.NaN,S1=float.NaN,S2=float.NaN,S3=float.NaN,S4=float.NaN,Kind=-1};
        /// <summary>Whether `moment` (an arena time) is new for add `id`'s `slot`: true once per moment, then false until another.</summary>
        public bool First(int id,int slot,float moment)
        {
            var v=Get(id);ref float m=ref slot==AttackSlot?ref v.S0:ref slot==StrikeSlot?ref v.S1:ref slot==CallSlot?ref v.S2:ref slot==AlertSlot?ref v.S3:ref v.S4;
            if(!float.IsNaN(m)&&Math.Abs(m-moment)<1e-3f)return false;m=moment;if(float.IsNegativeInfinity(v.Seen))v.Seen=float.IsNaN(last)?0:last;voices[id]=v;return true;
        }
        private void Clock(float now){if(!float.IsNaN(last)&&now<last-.5f){voices.Clear();for(int i=0;i<ends.Length;i++)ends[i]=float.NegativeInfinity;moanNext=-1;sweep=0;}last=now;}
    }
}
