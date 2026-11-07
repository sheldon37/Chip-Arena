using System;
using System.Linq;
using ChipArena.UnityConversion;

// The Boss Battle's opening chips (Core NativeBossPicks; owner 2026-11-07): one shared 30 s countdown for everyone's five, and the waiting screen's words.
public static class BossPicksChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("boss-picks: "+what);count++;}
    public static void Run(string root)
    {
        count=0;
        Check(NativeBossPicks.Seconds==30&&NativeBossPicks.Count==5,"30 s for five chips");
        Check(NativeBossPicks.Clock(30)=="0:30"&&NativeBossPicks.Clock(22.2f)=="0:23"&&NativeBossPicks.Clock(.01f)=="0:01"&&NativeBossPicks.Clock(0)=="0:00"&&NativeBossPicks.Clock(-3)=="0:00"&&NativeBossPicks.Clock(float.NaN)=="0:00","the countdown as shown");
        Check(NativeBossPicks.Waiting(2,4)=="Waiting for 2/4 players to select their chips"&&NativeBossPicks.Waiting(1,1)=="Waiting for 1/1 player to select their chips"&&NativeBossPicks.Waiting(3,1)=="Waiting for 3/3 players to select their chips","the waiting line, as the owner wrote it");
        Check(NativeBossPicks.Still(new[]{"Ana","Bo"})=="Still selecting: Ana, Bo"&&NativeBossPicks.Still(new[]{"A","B","C","D","E","F"})=="Still selecting: A, B, C, D +2 more"&&NativeBossPicks.Still(new string[0])==""&&NativeBossPicks.Still(new[]{""})=="Still selecting: PILOT","who is still selecting");
        float left=NativeBossPicks.Seconds;int ticks=0;while(left>0&&ticks<10000){left=NativeBossPicks.Tick(left,1f/60);ticks++;}
        Check(Math.Abs(ticks-1800)<=1&&left==0&&NativeBossPicks.Tick(5,-1)==5,"it runs out after 30 s of authority ticks, never below 0");
        Check(NativeBossPicks.Fill(30)==1&&NativeBossPicks.Fill(15)==.5f&&NativeBossPicks.Fill(-1)==0,"the bar drains");
        var rule=System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets","ChipArena","Runtime","NativeBossBattle.cs"));
        Check(rule.Contains("Pve.PickLeft=NativeBossPicks.Seconds")&&rule.Contains("Pve.PickLeft=NativeBossPicks.Tick(Pve.PickLeft,1f/60)")&&rule.Contains("ChoosePve(p,row.Open.Id,row.Open.Offers[0])"),"the opening starts the countdown, ticks it, and picks for whoever runs out");
        var frame=System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets","ChipArena","Runtime","NativeNetworkFrame.cs"));
        Check(frame.Contains("pickLeft=Pve.PickLeft")&&frame.Contains("v.PickLeft=f.pickLeft"),"the countdown is replicated");
        Console.WriteLine("PASS boss-picks ("+count+" checks)");
    }
}
