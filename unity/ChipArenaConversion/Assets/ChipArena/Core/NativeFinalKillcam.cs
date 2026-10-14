using System;

namespace ChipArena.UnityConversion
{
    // The final killcam (owner 2026-10-13: "a final killcam ... plays for everyone on final kill (If you are the last player
    // dead on the round, instead of seeing your normal kill cam, you will see this one). It will replicate the slow motion
    // on death like call of duty."; follow-up: "its supposed to be at the end of every round (per set)"). Presentation only,
    // engine free: which recorded death ended the round (from the death records and the Fight -> Result step every client
    // already replicates), the slow-motion clock over the recorded frames, fitting it into the time the Result phase
    // leaves, and the order rules that keep the last victim from seeing both killcams. Runtime/NativeFinalKillcamPresentation.cs plays it; docs/FINAL-KILLCAM.md has the contract.

    /// <summary>One replicated pilot death record. Side: the team in a teams match, else the seat id.</summary>
    public struct FinalKillCandidate{public int Victim,Killer,Seed,VictimSide,KillerSide;public double At;}

    /// <summary>What the client knows when it sees the round end. Witnessed: this client saw the Fight -> Result step itself
    /// (a late joiner's first phase is Result). ResultAt: the match time of the first Result frame it saw (the host's step
    /// time; a guest's first Result frame carries the host time it was sent at).</summary>
    public struct FinalKillContext{public bool Teams,Cooperative,Pve,Sandbox,Practice,Witnessed;public int Winner,WinnerSide;public double FightAt,ResultAt;}

    /// <summary>Why there is no final killcam (logs and checks).</summary>
    public enum FinalKillSkip{None,Mode,Unwitnessed,NoDeath,NotRoundEnding,NoHistory,NoTime}

    /// <summary>
    /// The replay clock: wall seconds since the clip started -> replay seconds since its first recorded moment. Normal speed
    /// for the pre-roll, a smoothstep ramp of the playback rate down to <see cref="Rate"/> that ends <see cref="Lead"/> replay
    /// seconds before the fatal hit, a hold at that rate through the hit and <see cref="Hold"/> replay seconds of the death
    /// reaction, a smoothstep ramp back up to normal speed, then <see cref="Tail"/> replay seconds of aftermath. The rate is
    /// continuous with zero slope at both ends of each ramp, so the replay clock is smooth (C2) and strictly increasing.
    /// </summary>
    public struct FinalKillcamCurve
    {
        /// <summary>Slow-motion playback rate; replay seconds before the hit; replay seconds before it where the hold starts;
        /// replay seconds after it that stay slow; wall seconds of each ramp; replay seconds of aftermath at normal speed.</summary>
        public float Rate,Preroll,Lead,Hold,RampIn,RampOut,Tail;
        /// <summary>Wall marks: the normal pre-roll ends, the ramp down ends (hold starts), the hold ends, the ramp up ends.</summary>
        public float W1=>Math.Max(0,Preroll-Lead-RampIn*(1+Rate)/2);
        public float W2=>W1+RampIn;
        public float W3=>W2+(Lead+Hold)/Rate;
        public float W4=>W3+RampOut;
        /// <summary>The clip's wall length.</summary>
        public float Wall=>W4+Tail;
        /// <summary>Replay marks at those wall marks.</summary>
        public float R2=>Preroll-Lead;
        public float R3=>Preroll+Hold;
        public float R4=>R3+RampOut*(1+Rate)/2;
        /// <summary>The clip's replay length; the fatal hit is <see cref="Preroll"/> into it.</summary>
        public float Span=>R4+Tail;
        /// <summary>Wall second the fatal hit plays at.</summary>
        public float HitWall=>W2+Lead/Rate;
        /// <summary>How far the clip falls behind the live clock by its end (it never gains it back).</summary>
        public float Behind=>Wall-Span;
        private static float Ease(float u)=>u<=0?0:u>=1?1:u*u*(3-2*u);
        // The integral of Ease from 0 to u.
        private static float EaseArea(float u){u=u<=0?0:u>=1?1:u;float u3=u*u*u;return u3-u3*u/2;}
        /// <summary>Playback rate at wall second w.</summary>
        public float Speed(float w)
        {
            if(w<W1)return 1;if(w<W2)return 1+(Rate-1)*Ease((w-W1)/Math.Max(1e-4f,RampIn));
            if(w<W3)return Rate;if(w<W4)return Rate+(1-Rate)*Ease((w-W3)/Math.Max(1e-4f,RampOut));return 1;
        }
        /// <summary>Replay seconds since the clip's start at wall second w (past the end it keeps normal speed, so the
        /// runtime's end test fires).</summary>
        public float Replay(float w)
        {
            if(w<=0)return Math.Max(-1,w);float w1=W1,w2=W2,w3=W3,w4=W4;
            if(w<w1)return w;
            if(w<w2){float u=(w-w1)/Math.Max(1e-4f,RampIn);return w1+RampIn*(u-(1-Rate)*EaseArea(u));}
            if(w<w3)return R2+(w-w2)*Rate;
            if(w<w4){float u=(w-w3)/Math.Max(1e-4f,RampOut);return R3+RampOut*(Rate*u+(1-Rate)*EaseArea(u));}
            return R4+(w-w4);
        }
        /// <summary>0 at normal speed, 1 at the full slow-motion rate (the camera's wide shot, the audio pitch, the HUD).</summary>
        public float Slow(float w)=>Rate>=.999f?0:Math.Max(0,Math.Min(1,(1-Speed(w))/(1-Rate)));
        /// <summary>The ideal clip (CoD-style): 2.4 s of pre-roll, quarter speed from .15 s before the hit to .45 s after it.</summary>
        public static FinalKillcamCurve Ideal=>new FinalKillcamCurve{Rate=.25f,Preroll=2.4f,Lead=.15f,Hold=.45f,RampIn=.5f,RampOut=.7f,Tail=.9f};
        internal static FinalKillcamCurve Mix(FinalKillcamCurve a,FinalKillcamCurve b,float t)=>new FinalKillcamCurve{Rate=a.Rate+(b.Rate-a.Rate)*t,Preroll=a.Preroll+(b.Preroll-a.Preroll)*t,Lead=a.Lead+(b.Lead-a.Lead)*t,
            Hold=a.Hold+(b.Hold-a.Hold)*t,RampIn=a.RampIn+(b.RampIn-a.RampIn)*t,RampOut=a.RampOut+(b.RampOut-a.RampOut)*t,Tail=a.Tail+(b.Tail-a.Tail)*t};
    }

    public static class NativeFinalKillcam
    {
        /// <summary>The round-ending death is at most this long before the first Result frame a client saw (the host
        /// resolves in the same step as the death; a guest's first Result frame is at most one 20 Hz send later).</summary>
        public const double Window=.5;
        /// <summary>Seconds to leave the final death on the live camera before replay begins.</summary>
        public const float StartDelay=2;
        /// <summary>Practice (offline Play vs Bots) shows the final killcam too: the owner wants it at the end of every round.</summary>
        public const bool InPractice=true;
        /// <summary>Wall seconds the round result keeps after the clip (its banner arrives over the hand-off flash).</summary>
        public const float Handoff=.9f;
        /// <summary>Replay seconds the clip needs before the hit; less history skips the final killcam.</summary>
        public const float MinPreroll=.5f;
        /// <summary>Live history the runtime keeps (Runtime/NativeReplay.cs ObserveReplay: 6 s) and the margin kept from it.</summary>
        public const float HistorySeconds=6,HistoryMargin=.4f;
        /// <summary>The clip's longest wall length when no next phase bounds it (a match-ending kill: the Match phase waits).</summary>
        public static float Longest=>FinalKillcamCurve.Ideal.Wall;

        /// <summary>
        /// The final kill: the latest (time, then seed) death of a pilot not on the winner's side (any pilot on a simultaneous
        /// knockout) between the fight's start and the round's end, if the round ended on it (no more than <see cref="Window"/>
        /// before the client's first Result frame). Every round that ends on a death has one, whoever or whatever caused it:
        /// an environmental or self death, or a team kill, plays from the victim's view (<see cref="Credited"/>). Returns its
        /// index, or -1 with the reason. Every client holds the same death records and the same Result step, so all pick the same.
        /// </summary>
        public static int Select(FinalKillCandidate[] deaths,int count,in FinalKillContext c,out FinalKillSkip why)
        {
            why=FinalKillSkip.None;
            if(c.Cooperative||c.Pve||c.Sandbox||c.Practice&&!InPractice){why=FinalKillSkip.Mode;return -1;}
            if(!c.Witnessed){why=FinalKillSkip.Unwitnessed;return -1;}
            int best=-1;
            for(int i=0;i<count&&deaths!=null&&i<deaths.Length;i++)
            {
                var d=deaths[i];if(d.At<c.FightAt-1e-4||d.At>c.ResultAt+1e-4||c.Winner!=0&&d.VictimSide==c.WinnerSide)continue;
                if(best<0||d.At>deaths[best].At||d.At==deaths[best].At&&d.Seed>deaths[best].Seed)best=i;
            }
            if(best<0){why=FinalKillSkip.NoDeath;return -1;}
            var f=deaths[best];
            if(c.ResultAt-f.At>Window){why=FinalKillSkip.NotRoundEnding;return -1;}
            return best;
        }
        /// <summary>The host's decided final kill (NativeMatch.FinalVictim / FinalSeed): the record with that victim and death
        /// seed exactly, or -1 when this client does not hold it (then the local <see cref="Select"/> decides).</summary>
        public static int Decided(FinalKillCandidate[] deaths,int count,int victim,int seed)
        {
            for(int i=0;i<count&&deaths!=null&&i<deaths.Length;i++)if(deaths[i].Victim==victim&&deaths[i].Seed==seed)return i;
            return -1;
        }
        /// <summary>The death has a killer to credit: a pilot other than the victim, not on the victim's team in a teams
        /// match. Otherwise (environment, self, team kill) the labels describe an elimination. The camera always follows the victim.</summary>
        public static bool Credited(in FinalKillCandidate f,bool teams)=>f.Killer>0&&f.Killer!=f.Victim&&!(teams&&f.KillerSide==f.VictimSide);
        /// <summary>The broadcast frame's cause line for an uncredited final death (constant strings: the HUD allocates nothing).</summary>
        public static string SoloCause(bool self,bool teamKill,string damageKind)=>self?"Self-inflicted":teamKill?"Friendly fire":damageKind=="collapse"?"Structural collapse":!string.IsNullOrEmpty(damageKind)?"Stage hazard":"Environment";

        // Shrinking the clip, in this order: pre-roll and aftermath, then hold, ramps and lead, then a lighter slow motion,
        // then everything to the minimum. Every step only shortens it, so the fit is a search along these keyframes.
        private static readonly FinalKillcamCurve[] Keys=
        {
            FinalKillcamCurve.Ideal,
            new FinalKillcamCurve{Rate=.25f,Preroll=1.3f,Lead=.15f,Hold=.45f,RampIn=.5f,RampOut=.7f,Tail=.5f},
            new FinalKillcamCurve{Rate=.25f,Preroll=1.3f,Lead=.12f,Hold=.3f,RampIn=.4f,RampOut=.45f,Tail=.5f},
            new FinalKillcamCurve{Rate=.35f,Preroll=.9f,Lead=.1f,Hold=.25f,RampIn=.35f,RampOut=.35f,Tail=.35f},
            new FinalKillcamCurve{Rate=.45f,Preroll=MinPreroll,Lead=.08f,Hold=.2f,RampIn=.3f,RampOut=.3f,Tail=.25f},
        };
        /// <summary>The clip's shortest wall length (the last keyframe); less time skips the final killcam.</summary>
        public static float Shortest=>Keys[Keys.Length-1].Wall;
        private static FinalKillcamCurve At(float t,float history)
        {
            int i=Math.Max(0,Math.Min(Keys.Length-2,(int)Math.Floor(t)));var c=FinalKillcamCurve.Mix(Keys[i],Keys[i+1],Math.Max(0,Math.Min(1,t-i)));
            c.Preroll=Math.Min(c.Preroll,history);c.RampIn=Math.Min(c.RampIn,2*Math.Max(0,c.Preroll-c.Lead)/(1+c.Rate));return c;
        }
        private static bool Fits(in FinalKillcamCurve c,float available,float behind)=>c.Wall<=available+1e-4f&&c.Preroll+c.Behind<=behind+1e-4f;
        /// <summary>
        /// The longest clip (closest to <see cref="FinalKillcamCurve.Ideal"/>) that lasts at most <paramref name="available"/>
        /// wall seconds (infinity: unbounded), starts no earlier than <paramref name="history"/> replay seconds before the hit
        /// (what the client recorded), and never falls more than <paramref name="behind"/> seconds behind the live clock
        /// before it ends (so live history trimming never removes the frame it plays). False when even the shortest clip
        /// does not fit.
        /// </summary>
        public static bool Fit(float available,float history,float behind,out FinalKillcamCurve curve,out FinalKillSkip why)
        {
            curve=default;why=FinalKillSkip.None;
            if(float.IsNaN(available)||float.IsNaN(history)||float.IsNaN(behind)||history<MinPreroll){why=FinalKillSkip.NoHistory;return false;}
            float last=Keys.Length-1;
            if(!Fits(At(last,history),available,behind)){why=FinalKillSkip.NoTime;return false;}
            if(Fits(At(0,history),available,behind)){curve=At(0,history);return true;}
            float lo=0,hi=last;for(int i=0;i<40;i++){float mid=(lo+hi)/2;if(Fits(At(mid,history),available,behind))hi=mid;else lo=mid;}
            curve=At(hi,history);return true;
        }
        /// <summary>Wall seconds the clip may run in the Result phase: what is left of it minus the hand-off; unbounded
        /// (<see cref="Longest"/>) when the Match phase follows, which waits for the replay.</summary>
        public static float Available(double phaseEnd,double now,bool matchEnds)=>matchEnds?Longest:(float)Math.Max(0,phaseEnd-now-Handoff);
        /// <summary>How far behind the live clock the clip may fall: the kept history minus the time already past the hit.</summary>
        public static float Behind(double now,double hitAt)=>(float)Math.Max(0,HistorySeconds-HistoryMargin-Math.Max(0,now-hitAt));

        // ---- Order: the last victim never sees both killcams ----
        /// <summary>A fresh local death arms the normal killcam only while no final kill is decided for the round (finalSeed
        /// 0): the final kill's victim, and anyone dying in the same step, watch the final killcam instead.</summary>
        public static bool ArmNormal(int finalSeed)=>finalSeed==0;
        public enum Start{Begin,CutOver,Already}
        /// <summary>When the final kill is decided: a normal killcam playing or waiting to start is cut over (stopped first);
        /// a final killcam already shown this round is never shown again.</summary>
        public static Start Begin(bool normalPlaying,bool normalArmed,bool finalShown)=>finalShown?Start.Already:normalPlaying||normalArmed?Start.CutOver:Start.Begin;

        // ---- Presentation helpers ----
        /// <summary>Effects voice pitch at playback rate `rate` (Unity pitch also slows the sound): an octave down at most.</summary>
        public static float Pitch(float rate)=>Math.Max(.5f,Math.Min(1,1-(1-rate)*.7f));
        /// <summary>Music level while slowed (`slow` 0..1): ducked under the slowed effects.</summary>
        public static float Duck(float slow)=>1-.55f*Math.Max(0,Math.Min(1,slow));
        /// <summary>The camera's slow-motion shot at `slow` (0..1): boom length and lift added, FOV added (reduced effects
        /// keep the FOV and half the move), and how far the look turns toward the killer-victim midpoint.</summary>
        public static void Wide(float slow,bool reduced,out float boom,out float lift,out float fov,out float look)
        {
            float s=Math.Max(0,Math.Min(1,slow));s=s*s*(3-2*s);float k=reduced?.5f:1;
            boom=1.9f*s*k;lift=.45f*s*k;fov=reduced?0:7*s;look=.45f*s*k;
        }
    }
}
