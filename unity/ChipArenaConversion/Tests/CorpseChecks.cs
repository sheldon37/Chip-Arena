#if ARENA_TESTS
using System;
namespace ChipArena.UnityConversion
{
    internal static class CorpseChecks
    {
        public static void Run(ConversionContent content)
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Corpse lifetime: "+why);}
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
            match.Seats[1].alive=false;match.Tick(.25);
            Check(match.Phase==NativePhase.Result&&NativeCorpseRules.RoundEnded(match.Cooperative,match.Phase),"winning kill wipes bodies immediately at results");
            for(int i=0;i<20;i++)match.Tick(.25);
            Check(match.Phase==NativePhase.Countdown&&NativeCorpseRules.RoundEnded(match.Cooperative,match.Phase),"next round cannot recreate the previous deaths");
            Check(!NativeCorpseRules.Retain(false,false)&&!NativeCorpseRules.Retain(true,true),"summons and PvE use existing lifetimes");
            Check(!NativeCorpseRules.Expired(5,false,false)&&NativeCorpseRules.Expired(5.01f,false,false),"minion lifetime boundary");
            Check(!NativeCorpseRules.Expired(600,true,false),"PvE downed body still revivable after a long wait");
            Check(!NativeCorpseRules.EffectsFinished(4.99f)&&NativeCorpseRules.EffectsFinished(5)&&NativeCorpseRules.EffectsFinished(600),"kill effects end independently of body retention");
            foreach(NativePhase phase in Enum.GetValues(typeof(NativePhase)))
            {
                Check(NativeCorpseRules.RoundEnded(false,phase)==(phase!=NativePhase.Fight),"PvP clears at results and cannot recreate bodies in draft/next-round intro");
                Check(!NativeCorpseRules.RoundEnded(true,phase),"PvE body lifecycle remains independent");
            }
            Console.WriteLine("PASS corpse lifetime: PvP retained across 5/60/600 seconds, round-end wipe, five-second minions, unchanged PvE downed bodies and finite kill effects");
        }
    }
}
#endif
