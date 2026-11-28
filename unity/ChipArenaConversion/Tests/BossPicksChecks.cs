using System;
using System.Linq;
using ChipArena.UnityConversion;

// The Boss Battle's opening chips (Core NativeBossPicks; owner 2026-11-07): one shared 30 s countdown for everyone's five, the waiting screen's words,
// the big clock's urgency, and chips left after it chosen with C in the fight.
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
        // The clock's urgency (the owner, 2026-11-07: "show urgency when down to last 10 seconds").
        Check(NativeBossPicks.Urgent==10&&!NativeBossPicks.IsUrgent(10.01f)&&NativeBossPicks.IsUrgent(10)&&NativeBossPicks.IsUrgent(.01f)&&!NativeBossPicks.IsUrgent(0)&&!NativeBossPicks.IsUrgent(float.NaN),"urgent for the last ten seconds, not at 0");
        Check(Math.Abs(NativeBossPicks.SinceTick(8)-0)<1e-5f&&Math.Abs(NativeBossPicks.SinceTick(7.75f)-.25f)<1e-5f&&NativeBossPicks.SinceTick(0)==1,"the time since the shown number changed");
        Check(NativeBossPicks.Beat(20)==0&&Math.Abs(NativeBossPicks.Beat(8)-1)<1e-5f&&NativeBossPicks.Beat(7.5f)<.05f&&NativeBossPicks.Beat(7.9f)>NativeBossPicks.Beat(7.6f),"a beat on each urgent second, dying away, none while calm");
        Check(NativeBossPicks.Second(9.2f)==10&&NativeBossPicks.Second(.01f)==1&&NativeBossPicks.Second(0)==0&&NativeBossPicks.Second(float.NaN)==0,"the shown second, for the tick");
        Check(NativeBossPicks.Leftover(2)=="2 CHIPS LEFT TO CHOOSE"&&NativeBossPicks.Leftover(1)=="1 CHIP LEFT TO CHOOSE","the prompt for chips left");
        var rule=System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets","ChipArena","Runtime","NativeBossBattle.cs"));
        Check(rule.Contains("Pve.PickLeft=NativeBossPicks.Seconds")&&rule.Contains("Pve.PickLeft=NativeBossPicks.Tick(Pve.PickLeft,1f/60)")&&rule.Contains("GrantPvePick(p,\"common\",false,NativeBossPicks.Source)"),"the opening grants its five as the opening's, starts the countdown and ticks it");
        // The owner, 2026-11-07: "If all chips aren't selected, players can select rest when pressing C in boss fight".
        Check(rule.Contains("foreach(var row in connected)foreach(var pick in row.Picks)if(pick.Offers==null)RollPveOffers(row.Pilot,pick);")&&rule.Contains("foreach(var row in Pve.Rows.Values){row.Open=null;if(row.Pilot.Seat.bot)row.Picks.Clear();}")&&!rule.Contains("while(row.Picks.Count>0&&guard++<16)"),"out of time, nothing is chosen for them: the chips left wait, their offers rolled under the opening's rules, the screen closed");
        var pve=System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets","ChipArena","Runtime","NativePve.cs"));
        Check(pve.Contains("!BossOpening&&pick.Source!=NativeBossPicks.Source&&Pve.EconomyRandom.Next()<.3"),"an opening chip chosen in the fight never offers a money chip");
        var fallen=System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets","ChipArena","Runtime","NativeFallenAuthority.cs"));
        Check(fallen.Contains("if(row.Open?.Source==\"boss\"&&row.Open.Offers?.Count>0)ChoosePve(row.Pilot,row.Open.Id,row.Open.Offers[0]);if(row.Open!=null&&row.Open.Source!=\"boss\")row.Open=null;"),"NOCTIEL's resume makes only its own draft; a leftover open after it closes");
        var game=System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets","ChipArena","Runtime","ConversionGame.cs"));var hud=System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets","ChipArena","Runtime","NativeFallenHudBinding.cs"));var ui=System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets","ChipArena","Runtime","NativePveUI.cs"));
        Check(game.Contains("{PveChipScreen();BossPicksClock();return;}")&&game.Contains("{BossPicksWaitScreen(width,height);BossPicksClock();return;}"),"the big clock over both opening screens");
        Check(hud.Contains("BossLeftoverHud(W,NativeUi.H);")&&hud.Contains("PveChipsTag(W,NativeUi.H,pending.Picks.Count")&&ui.Contains("BossLeftoverHud(W,H);"),"chips left show with C in every boss's fight, NOCTIEL's too");
        var frame=System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets","ChipArena","Runtime","NativeNetworkFrame.cs"));
        Check(frame.Contains("pickLeft=Pve.PickLeft")&&frame.Contains("v.PickLeft=f.pickLeft"),"the countdown is replicated");
        Console.WriteLine("PASS boss-picks ("+count+" checks)");
    }
}
