using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // Vote to skip a cutscene (owner 2026-11-08: "lets add a Vote to skip option on cutscenes like boss intros. Once all players
    // agree, the scene transitions to the next thing."). The host keeps one ballot for the cutscene now playing in an arena: a
    // boss's arrival or one of its phase reveals (the PvE holds, Runtime NativePve.cs), or the pilots' intros before a match
    // (NativeMatch.SkipIntros). Each cutscene has its own key, so a new one starts a new ballot. Every connected human in the
    // arena must agree; bots and the boss's own seat never count, and a lone pilot skips with one press. A vote travels as a
    // "skip-vote" message (Runtime NativeNetwork.cs, NativeExpeditionSplitRuntime.cs), and the ballot rides every frame
    // (NativeSkipFrame) so each screen shows the count (Runtime NativeCutsceneSkipUI.cs). Skipping releases the hold exactly as
    // its natural end would (NativeArena.StepCutsceneSkip), so the fight and the boss song pick up where the cutscene ends.
    // No hit, damage, cooldown or movement rule changes. Pure, allocation free per tick.
    public sealed class NativeCutsceneSkip
    {
        /// <summary>The PvE holds that may be skipped: a boss's arrival and its phase reveals, never the victory or the defeat.</summary>
        public static bool Skippable(string cinematic)=>cinematic=="intro"||cinematic=="reveal";
        /// <summary>How long before its end a cutscene stops taking votes (s): too close to the end to be worth a cut.</summary>
        public const float Late=.6f;
        /// <summary>The black dip every screen cuts through when a cutscene is skipped (s).</summary>
        public const float Dip=.55f;
        /// <summary>The most voters a ballot holds (a room's seats).</summary>
        public const int MaxVoters=16;

        private readonly int[] members=new int[MaxVoters],votes=new int[MaxVoters];private int memberCount,voteCount,need;
        /// <summary>The cutscene the ballot is for, null when none is playing.</summary>
        public string Key{get;private set;}
        /// <summary>How many cutscenes have been skipped here: a screen dips to black when it rises.</summary>
        public int Serial{get;private set;}
        public int Votes=>voteCount;
        /// <summary>How many must agree: every connected human in the arena.</summary>
        public int Required=>need;
        public bool Due=>Key!=null&&need>0&&voteCount>=need;
        public bool Voted(int id){for(int i=0;i<voteCount;i++)if(votes[i]==id)return true;return false;}
        public int Voter(int i)=>votes[i];

        /// <summary>
        /// The host, every tick: the cutscene now playing (`key`, null for none) and the humans who must agree (`ids`, the first
        /// `count`). A new key starts a new ballot; a pilot who leaves takes their vote with them.
        /// </summary>
        public void Track(string key,int[] ids,int count)
        {
            if(key!=Key){Key=key;voteCount=0;}
            memberCount=0;if(key!=null)for(int i=0;i<count&&memberCount<MaxVoters;i++)if(!Member(ids[i]))members[memberCount++]=ids[i];
            need=memberCount;
            for(int i=0;i<voteCount;)if(Member(votes[i]))i++;else votes[i]=votes[--voteCount];
        }
        private bool Member(int id){for(int i=0;i<memberCount;i++)if(members[i]==id)return true;return false;}
        /// <summary>A pilot's vote: counted once, and only from a human the ballot is waiting on.</summary>
        public bool Vote(int id)
        {
            if(Key==null||!Member(id)||Voted(id)||voteCount>=MaxVoters)return false;
            votes[voteCount++]=id;return true;
        }
        /// <summary>The host skipped the cutscene: the ballot closes and the serial rises.</summary>
        public void Skipped(){Serial++;Key=null;voteCount=memberCount=need=0;}

        public NativeSkipFrame Frame()
        {
            var voters=new int[voteCount];Array.Copy(votes,voters,voteCount);Array.Sort(voters);
            return new NativeSkipFrame{key=Key,voters=voters,need=need,serial=Serial};
        }
        /// <summary>A replica's copy of the host's ballot (a frame without one, from an older host, shows none).</summary>
        public void Apply(NativeSkipFrame f)
        {
            Key=string.IsNullOrEmpty(f?.key)?null:f.key;need=Key==null?0:Math.Max(0,Math.Min(MaxVoters,f.need));Serial=f?.serial??Serial;
            voteCount=0;memberCount=0;if(Key!=null&&f.voters!=null)foreach(int id in f.voters)if(voteCount<MaxVoters&&!Voted(id))votes[voteCount++]=id;
        }
    }

    /// <summary>The ballot on the wire (NativeGameFrame.cutsceneSkip): the cutscene's key, who has voted, how many must, the skips so far.</summary>
    [Serializable] public sealed class NativeSkipFrame {public string key;public int[] voters;public int need,serial;}
}
