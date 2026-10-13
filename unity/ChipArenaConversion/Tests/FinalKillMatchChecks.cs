using System;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class FinalKillMatchChecks
{
    public static void Run(ConversionContent data)
    {
        void Check(bool ok,string why){if(!ok)throw new Exception("Final kill match: "+why);}
        NativeMatch Setup(out List<FinalKillCandidate> deaths)
        {
            var list=new List<FinalKillCandidate>();deaths=list;
            var m=new NativeMatch(data,1){FinalKillcams=true,RoundDeaths=()=>list};
            for(int i=1;i<=3;i++)m.Seats.Add(new NativeSeat{id=i,name="Pilot "+i,team=i==1?0:1});
            Check(m.Start(),"start");for(int i=0;i<20;i++)m.Tick(.25);
            list.Add(new FinalKillCandidate{Victim=2,Killer=1,Seed=42,At=m.Time});m.Seat(2).alive=m.Seat(3).alive=false;return m;
        }
        void Result(NativeMatch m,double seconds,int victim=0,int seed=0)
        {m.Resolve();Check(m.Phase==NativePhase.Result&&m.PhaseEnd-m.Time==seconds&&m.FinalVictim==victim&&m.FinalSeed==seed&&m.ResultAt==m.Time&&m.HasFinalKillcam,"result "+seconds);}
        var host=Setup(out var deaths);Result(host,8.5,2,42);
        var wire=new NativeMatchFrame{hasFinalKillcam=host.HasFinalKillcam,finalVictim=host.FinalVictim,finalSeed=host.FinalSeed,resultAt=host.ResultAt,phaseEnd=host.PhaseEnd,time=host.Time,phase=host.Phase};
        var opts=new JsonSerializerOptions{IncludeFields=true};var guest=new NativeMatch(data);guest.ApplyNetwork(JsonSerializer.Deserialize<NativeMatchFrame>(JsonSerializer.Serialize(wire,opts),opts));
        Check(guest.FinalVictim==2&&guest.FinalSeed==42&&guest.ResultAt==host.ResultAt&&guest.PhaseEnd==host.PhaseEnd,"guest receives host marker");
        guest.ApplyNetwork(JsonSerializer.Deserialize<NativeMatchFrame>("{}",opts));Check(!guest.HasFinalKillcam&&guest.FinalVictim==0&&guest.FinalSeed==0&&guest.ResultAt==0,"old host defaults");
        for(int i=0;i<33;i++)host.Tick(.25);Check(host.Phase==NativePhase.Result,"holds beyond five seconds");host.Tick(.25);Check(host.Phase==NativePhase.Countdown&&host.FinalVictim==0&&host.FinalSeed==0&&host.ResultAt==0&&!host.HasFinalKillcam,"next round clears marker");
        var m=Setup(out deaths);deaths.Clear();Result(m,5);
        m=Setup(out deaths);m.FinalKillcams=false;Result(m,5);
        m=Setup(out deaths);m.IsSandbox=()=>true;Result(m,5);
        m=Setup(out deaths);m.Seat(1).alive=false;Result(m,5);
        foreach(int killer in new[]{0,2,99}){m=Setup(out deaths);var d=deaths[0];d.Killer=killer;deaths[0]=d;Result(m,5);}
        m=Setup(out deaths);m.Teams=true;var team=deaths[0];team.Killer=3;deaths[0]=team;Result(m,5);
        m=Setup(out deaths);var old=deaths[0];old.At=m.Time-.5001;deaths[0]=old;Result(m,5);
        m=Setup(out deaths);old=deaths[0];old.At=m.Time-.5;deaths[0]=old;Result(m,8.5,2,42);
        m=Setup(out deaths);deaths.Add(new FinalKillCandidate{Victim=3,Killer=0,Seed=43,At=m.Time});Result(m,5);
        m=Setup(out deaths);deaths.Add(new FinalKillCandidate{Victim=3,Killer=1,Seed=43,At=m.Time});Result(m,8.5,3,43);
        m=Setup(out deaths);m.Cooperative=true;m.Resolve();Check(m.Phase==NativePhase.Fight&&m.FinalVictim==0,"cooperative retains dedicated flow");
        m=Setup(out deaths);m.Seat(3).connected=false;m.Seat(3).disconnectedAt=m.Time;m.Resolve();Check(m.Phase==NativePhase.Fight,"disconnect grace prevents final kill");
        Check(!new NativeMatch(data).FinalKillcams,"practice opt-in defaults off");
        Console.WriteLine("PASS final kill match: host timing, exclusions, tie order, guest fields, old host defaults and round reset");
    }
}
