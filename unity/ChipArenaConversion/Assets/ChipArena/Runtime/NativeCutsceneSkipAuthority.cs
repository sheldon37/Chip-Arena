namespace ChipArena.UnityConversion
{
    // Vote to skip a cutscene, on the host (Core NativeCutsceneSkip). Each tick, before the cutscene's own hold ticks, the arena
    // names the cutscene playing (a skippable PvE hold, or the pilots' intros) and the humans who must agree; once they all have,
    // the hold's time is spent at once (PveHoldTick then releases it exactly as its natural end does: the stage, the boss's phase
    // clock, NativeBossSongClock's lead so the song plays on from the fight's start) or the intros end (NativeMatch.SkipIntros).
    // Replicas only show the ballot they are sent.
    public sealed partial class NativeGameFrame {public NativeSkipFrame cutsceneSkip;}
    public sealed partial class NativeArena
    {
        public readonly NativeCutsceneSkip CutsceneSkip=new NativeCutsceneSkip();
        private readonly int[] skipHumans=new int[NativeCutsceneSkip.MaxVoters];
        private string skipKey,skipCinematic,skipIntros,skipIntrosOf;private int skipReveal,skipHolds;private bool skipHeld;
        /// <summary>A pilot's vote to skip the cutscene playing (the host's own, or a guest's "skip-vote" message).</summary>
        public bool VoteSkip(int seat)=>!Replica&&CutsceneSkip.Vote(seat);
        private string CutsceneKey()
        {
            var v=Pve;bool held=v!=null&&v.Holding;
            // A new hold (or one cinematic straight after another, the first reveal's intro) is a new cutscene.
            if(held&&(!skipHeld||v.Cinematic!=skipCinematic||v.Reveal!=skipReveal)){skipHolds++;skipCinematic=v.Cinematic;skipReveal=v.Reveal;skipKey="hold:"+skipHolds+":"+v.Cinematic;}
            skipHeld=held;
            if(held)return NativeCutsceneSkip.Skippable(v.Cinematic)&&v.HoldLeft>NativeCutsceneSkip.Late?skipKey:null;
            if(Match.Phase!=NativePhase.Intro)return null;
            if(skipIntros==null||skipIntrosOf!=Match.CompletionId){skipIntrosOf=Match.CompletionId;skipIntros="intros:"+skipIntrosOf;}
            return skipIntros;
        }
        private bool SkipRecruit(NativePilot p){if(Pve!=null)foreach(var r in Pve.Recruits)if(r.Pilot==p)return true;return false;}
        private void StepCutsceneSkip()
        {
            if(Replica)return;
            int n=0;foreach(var p in Pilots){var s=p.Seat;if(s.AuthorityBoss||s.devDummy||s.bot||!s.connected||n>=skipHumans.Length||SkipRecruit(p))continue;skipHumans[n++]=s.id;}
            CutsceneSkip.Track(CutsceneKey(),skipHumans,n);
            if(!CutsceneSkip.Due)return;
            if(Pve!=null&&Pve.Holding)Pve.HoldLeft=0;else Match.SkipIntros();
            CutsceneSkip.Skipped();
        }
    }
}
