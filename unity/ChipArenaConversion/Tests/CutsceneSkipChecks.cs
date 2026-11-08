using System;
using System.IO;
using System.Linq;
using ChipArena.UnityConversion;

// Vote to skip a cutscene (Core NativeCutsceneSkip; owner 2026-11-08: "lets add a Vote to skip option on cutscenes like boss
// intros. Once all players agree, the scene transitions to the next thing."). The ballot itself, then where the host, the wire,
// the guests and the screen meet it.
internal static class CutsceneSkipChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Cutscene skip: "+what);count++;}
    public static void Run(string root)
    {
        Check(NativeCutsceneSkip.Skippable("intro")&&NativeCutsceneSkip.Skippable("reveal")&&!NativeCutsceneSkip.Skippable("victory")&&!NativeCutsceneSkip.Skippable("defeat")&&!NativeCutsceneSkip.Skippable(null),
            "a boss's arrival and its reveals may be skipped; the victory and the defeat may not");
        var s=new NativeCutsceneSkip();int[] three={1,2,3};
        Check(!s.Vote(1)&&s.Key==null&&!s.Due,"no cutscene, no ballot");
        s.Track("hold:1:intro",three,3);
        Check(s.Required==3&&!s.Vote(9)&&s.Vote(1)&&!s.Vote(1)&&s.Votes==1&&!s.Due,"only the humans waited on vote, each once");
        s.Vote(2);Check(!s.Due,"one short keeps the cutscene");
        s.Track("hold:1:intro",new[]{1,2},2);Check(s.Due&&s.Required==2,"a pilot who leaves no longer holds the others");
        s.Track("hold:1:intro",three,3);Check(!s.Due&&s.Votes==2,"one who comes back must vote too");
        s.Vote(3);Check(s.Due,"all agree: due");
        s.Skipped();Check(s.Serial==1&&s.Key==null&&s.Votes==0&&!s.Due&&!s.Vote(1),"a skip closes the ballot and counts");
        s.Track("hold:2:reveal",three,3);s.Vote(1);s.Track("hold:3:intro",three,3);
        Check(s.Votes==0&&s.Key=="hold:3:intro","a new cutscene starts a new ballot");
        s.Track("hold:3:intro",new[]{4,4,4},3);Check(s.Required==1&&s.Vote(4)&&s.Due,"a lone pilot skips with one press");
        s.Track(null,three,3);Check(!s.Due&&s.Required==0&&!s.Vote(1),"none playing: nothing to vote on");
        var many=Enumerable.Range(1,40).ToArray();s.Track("hold:4:intro",many,many.Length);Check(s.Required==NativeCutsceneSkip.MaxVoters,"bounded");
        // The wire: a replica shows the host's ballot; an older host's frame shows none.
        var host=new NativeCutsceneSkip();host.Track("intros:abc",three,3);host.Vote(3);host.Vote(1);
        var replica=new NativeCutsceneSkip();replica.Apply(host.Frame());
        Check(replica.Key=="intros:abc"&&replica.Required==3&&replica.Votes==2&&replica.Voted(1)&&replica.Voted(3)&&!replica.Voted(2)&&!replica.Due,"the ballot survives the frame");
        Check(host.Frame().voters.SequenceEqual(new[]{1,3}),"voters sorted on the wire");
        host.Skipped();replica.Apply(host.Frame());Check(replica.Key==null&&replica.Serial==1&&replica.Required==0,"the skip reaches the guests");
        replica.Apply(null);Check(replica.Key==null&&replica.Serial==1,"an older host's frame keeps the serial and shows no prompt");
        Check(Math.Abs(NativeCutsceneSkip.Late-.6f)<1e-6&&Math.Abs(NativeCutsceneSkip.Dip-.55f)<1e-6,"pinned timings");

        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
        string auth=R("Runtime/NativeCutsceneSkipAuthority.cs"),ui=R("Runtime/NativeCutsceneSkipUI.cs"),core=R("Core/NativeCutsceneSkip.cs");
        Check(R("Runtime/NativeArena.cs").Contains("CancelInvalidKernel();StepCutsceneSkip();if(BossOpeningTick()||PveHoldTick(1f/60))return;"),"the host weighs the ballot each tick, before the hold ticks");
        Check(auth.Contains("if(Pve!=null&&Pve.Holding)Pve.HoldLeft=0;else Match.SkipIntros();")&&auth.Contains("CutsceneSkip.Skipped();")&&auth.Contains("if(Replica)return;"),
            "a skip spends the hold (released as at its natural end) or ends the intros, on the host only");
        Check(auth.Contains("s.AuthorityBoss||s.devDummy||s.bot||!s.connected")&&auth.Contains("SkipRecruit(p)"),"bots, the boss's seat, recruits and the disconnected never hold a cutscene");
        Check(auth.Contains("NativeCutsceneSkip.Skippable(v.Cinematic)&&v.HoldLeft>NativeCutsceneSkip.Late"),"skippable holds only, until just before their end");
        Check(R("Core/NativeMatch.cs").Contains("public bool SkipIntros(){if(Phase!=NativePhase.Intro)return false;Phase=NativePhase.Countdown;PhaseEnd=Time+3;return true;}"),"the pilots' intros end into the countdown");
        string net=R("Runtime/NativeNetwork.cs"),split=R("Runtime/NativeExpeditionSplitRuntime.cs"),frame=R("Runtime/NativeNetworkFrame.cs");
        Check(net.Contains("else if(message.type==\"skip-vote\")arena.VoteSkip(seat.id);")&&net.Contains("private void VoteSkipCutscene(){if(server!=null)SendServer(new NativeNetMessage{type=\"skip-vote\"});else if(arena?.Local!=null)arena.VoteSkip(arena.Local.Seat.id);}"),
            "a guest's vote reaches the host under its own seat; the host and a solo pilot vote at once");
        int at=split.IndexOf("if(message.type==\"skip-vote\")",StringComparison.Ordinal);
        Check(at>0&&at<split.IndexOf("return message.type==\"dev-command\"",StringComparison.Ordinal)&&split.Contains("splitCohorts.Accepts(who,message.areaIdentity))SplitPilotArena(who)?.VoteSkip(peer.Seat.id);"),
            "in a split Expedition a vote counts in the voter's own area only");
        Check(frame.Contains("f.cutsceneSkip=CutsceneSkip.Frame();CaptureExpansion(f);")&&frame.Contains("ApplyPve(f.pve);CutsceneSkip.Apply(f.cutsceneSkip);")&&auth.Contains("public sealed partial class NativeGameFrame {public NativeSkipFrame cutsceneSkip;}"),
            "the ballot rides every frame, in and out of PvE");
        string game=R("Runtime/ConversionGame.cs");
        Check(game.Contains("CutsceneSkipKeys(typing);")&&game.Contains("CutsceneSkipGui();SocialGui();"),"the key and the prompt are wired");
        Check(ui.Contains("Input.GetKeyDown(KeyCode.F)||PadDown(PadAction.Interact)")&&ui.Contains("arena.CutsceneSkip.Voted(arena.Local.Seat.id)")&&ui.Contains("!SkipPromptOn||typing||BoardOpen||StageInputHeld"),
            "F or the pad's Interact votes once, never while typing or with the board open");
        Check(ui.Contains("if(skipSerialSeen>=0)skipDipAt=now;")&&ui.Contains("new Color(0,0,0,a)"),"a skip dips every screen through black, never on joining");
        var body=ui.Substring(ui.IndexOf("private void SkipPrompt(",StringComparison.Ordinal));
        Check(!System.Text.RegularExpressions.Regex.IsMatch(body,"\\+\\s*\"|\"\\s*\\+|\\$\"|ToString\\(|string\\.Format|new List|\\.Select\\(|\\.Where\\("),"the prompt builds no strings or queries per frame");
        Check(body.Contains("reduced?.6f:.5f+.5f*Mathf.Sin(now*5)"),"its pulse rests with reduced effects");
        Check(!core.Contains("UnityEngine")&&!core.Contains("new List")&&!core.Contains("Linq"),"the ballot is pure and allocation free per tick");
        Console.WriteLine("PASS cutscene skip vote: "+count+" checks");
    }
}
