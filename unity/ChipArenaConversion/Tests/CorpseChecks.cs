#if ARENA_TESTS
using System;
namespace ChipArena.UnityConversion
{
    internal static class CorpseChecks
    {
        public static void Run(ConversionContent content)
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Corpse lifetime: "+why);}
            void Results(NativeMatch round,NativePhase next,string why)
            {
                double diedAt=round.Time;int resets=0;round.ResetRound=()=>resets++;
                // NativeArena resolves after damage in the same tick: the very first death frame is Result.
                round.Resolve();
                Check(round.Phase==NativePhase.Result&&round.Time==diedAt&&round.PhaseEnd==diedAt+5,why+": same-tick result");
                var viewer=new NativeMatch(content);
                for(int i=0;i<20;i++)
                {
                    Check(round.Phase==NativePhase.Result&&!NativeCorpseRules.RoundEnded(false,round.Phase),why+": death animation/body stays visible throughout results");
                    Check(!NativeCorpseRules.Expired((float)(round.Time-diedAt),false,true)&&resets==0,why+": death is not expired or reset during results");
                    // Guests and killcam can first observe a death after the phase has already changed.
                    viewer.ApplyNetwork(new NativeMatchFrame{phase=round.Phase,time=round.Time,phaseEnd=round.PhaseEnd});
                    Check(!NativeCorpseRules.RoundEnded(viewer.Cooperative,viewer.Phase),why+": replicated result still presents the death");
                    round.Tick(.25);
                }
                Check(round.Phase==next&&NativeCorpseRules.RoundEnded(false,round.Phase),why+": cleanup follows the full results period");
                Check(resets==(next==NativePhase.Countdown?1:0),why+": existing round-reset lifecycle preserved");
            }
            var match=new NativeMatch(content);
            for(int id=1;id<=3;id++)match.Seats.Add(new NativeSeat{id=id,name="Corpse check "+id,spec="pistol",connected=true,alive=true});
            match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10,fightAt=10,round=1,set=1});
            match.Seats[0].alive=false;bool retain=NativeCorpseRules.Retain(true,match.Cooperative);
            Check(retain,"PvP pilot retained");
            for(int i=0;i<2400;i++)
            {
                match.Tick(.25);float age=(float)match.Time-10;
                Check(!NativeCorpseRules.RoundEnded(match.Cooperative,match.Phase)&&!NativeCorpseRules.Expired(age,false,retain),"body stays for a ten-minute unresolved fight");
            }
            match.Seats[1].alive=false;Results(match,NativePhase.Countdown,"FFA winning kill after an earlier retained body");
            foreach(string outcome in new[]{"round","set","match","draw"})
            {
                var duel=new NativeMatch(content,1){RoundsPerSet=outcome=="round"?2:1,SetsToWin=outcome=="match"?1:5};
                duel.Seats.Add(new NativeSeat{id=1,name="Victim",alive=false});
                duel.Seats.Add(new NativeSeat{id=2,name="Winner",alive=outcome!="draw"});
                duel.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10,fightAt=10,round=1,set=1});
                Results(duel,outcome=="match"?NativePhase.Match:outcome=="set"?NativePhase.Draft:NativePhase.Countdown,"1v1 "+outcome);
            }
            Check(!NativeCorpseRules.Retain(false,false)&&!NativeCorpseRules.Retain(true,true),"summons and PvE use existing lifetimes");
            Check(!NativeCorpseRules.Expired(5,false,false)&&NativeCorpseRules.Expired(5.01f,false,false),"minion lifetime boundary");
            Check(!NativeCorpseRules.Expired(600,true,false),"PvE downed body still revivable after a long wait");
            Check(!NativeCorpseRules.EffectsFinished(4.99f)&&NativeCorpseRules.EffectsFinished(5)&&NativeCorpseRules.EffectsFinished(600),"kill effects end independently of body retention");
            foreach(NativePhase phase in Enum.GetValues(typeof(NativePhase)))
            {
                Check(NativeCorpseRules.RoundEnded(false,phase)==(phase!=NativePhase.Fight&&phase!=NativePhase.Result),"PvP presents combat/results and cannot recreate bodies in draft/next-round intro");
                Check(!NativeCorpseRules.RoundEnded(true,phase),"PvE body lifecycle remains independent");
            }
            Console.WriteLine("PASS corpse lifetime: 600-second retention, same-tick winning kills, full results on host/replica, round/set/match/draw cleanup, unchanged minions and PvE");
        }
    }
}
#endif
