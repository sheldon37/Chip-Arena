using System;
using System.IO;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;

// The final killcam (Core/NativeFinalKillcam.cs, Runtime/NativeFinalKillcamPresentation.cs, docs/FINAL-KILLCAM.md): which
// death ended the round (every client the same), the slow-motion replay clock (monotonic, smooth, slowed around the fatal
// hit, its length), fitting it into the Result time, the order that keeps the last victim from seeing both killcams, and
// source checks that the normal killcam is unchanged for every other death and that nothing touches the match rules.
internal static class FinalKillcamChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Final killcam: "+what);count++;}
    private static FinalKillCandidate D(int victim,int killer,int seed,double at,int victimSide=-2,int killerSide=-2)
        =>new FinalKillCandidate{Victim=victim,Killer=killer,Seed=seed,At=at,VictimSide=victimSide==-2?victim:victimSide,KillerSide=killerSide==-2?killer:killerSide};
    private static FinalKillContext Ffa(int winner,double resultAt)=>new FinalKillContext{Witnessed=true,Winner=winner,WinnerSide=winner,FightAt=3,ResultAt=resultAt};
    public static void Run(string root)
    {
        Selection();Curve();Fitting();Order();Sources(root);
        Console.WriteLine("PASS final killcam ("+count+" checks)");
    }
    private static void Selection()
    {
        // FFA: A (1) kills C (3) at 10 s, then B (2) at 20 s; the round resolves in the same step.
        var deaths=new[]{D(3,1,40,10),D(2,1,55,20)};
        Check(NativeFinalKillcam.Select(deaths,2,Ffa(1,20),out var why)==1&&why==FinalKillSkip.None,"FFA: the last survivor's kill that ended the round");
        // Host (ResultAt = the death step) and a guest (its first Result frame one 20 Hz send later) pick the same death,
        // whatever order the pilots are listed in.
        var swapped=new[]{deaths[1],deaths[0]};
        Check(NativeFinalKillcam.Select(deaths,2,Ffa(1,20.05),out _)==1&&NativeFinalKillcam.Select(swapped,2,Ffa(1,20.05),out _)==0,"host and guest agree on the same record");
        // Two deaths in one step: the later seed (the authority's event order) ends the round.
        var both=new[]{D(2,1,61,20),D(3,1,60,20)};
        Check(NativeFinalKillcam.Select(both,2,Ffa(1,20),out _)==0&&NativeFinalKillcam.Select(new[]{both[1],both[0]},2,Ffa(1,20),out _)==1,"same-step deaths: the highest seed");
        // Teams: Red (0) wins; Blue's (1) last pilot dies at 30 s. A Red death in the same step is not the round-ending one.
        var teams=new[]{D(4,1,70,29,1,0),D(5,2,71,30,1,0),D(1,5,72,30,0,1)};
        var tc=new FinalKillContext{Teams=true,Witnessed=true,Winner=1,WinnerSide=0,FightAt=3,ResultAt=30};
        Check(NativeFinalKillcam.Select(teams,3,tc,out _)==1,"teams: the last enemy on the losing team");
        // No final killcam: a draw, an environmental or self death, a team kill, a death well before the round resolved
        // (the round ended on a disconnect after the grace), an unwitnessed Result (late joiner), and the excluded modes.
        Check(NativeFinalKillcam.Select(deaths,2,Ffa(0,20),out why)<0&&why==FinalKillSkip.Draw,"simultaneous knockout");
        Check(NativeFinalKillcam.Select(new[]{D(2,0,9,20)},1,Ffa(1,20),out why)<0&&why==FinalKillSkip.NoKiller,"environmental death");
        Check(NativeFinalKillcam.Select(new[]{D(2,2,9,20)},1,Ffa(1,20),out why)<0&&why==FinalKillSkip.NoKiller,"self elimination");
        Check(NativeFinalKillcam.Select(new[]{D(5,6,9,30,1,1)},1,tc,out why)<0&&why==FinalKillSkip.TeamKill,"team kill");
        Check(NativeFinalKillcam.Select(deaths,2,Ffa(1,32),out why)<0&&why==FinalKillSkip.NotRoundEnding,"round resolved 12 s after the last death");
        Check(NativeFinalKillcam.Select(deaths,2,Ffa(1,20.49),out _)==1&&NativeFinalKillcam.Select(deaths,2,Ffa(1,20.51),out _)<0,"the .5 s window");
        var late=Ffa(1,20);late.Witnessed=false;Check(NativeFinalKillcam.Select(deaths,2,late,out why)<0&&why==FinalKillSkip.Unwitnessed,"late joiner");
        foreach(int mode in new[]{0,1,2,3})
        {
            var m=Ffa(1,20);if(mode==0)m.Cooperative=true;if(mode==1)m.Pve=true;if(mode==2)m.Sandbox=true;if(mode==3)m.Practice=true;
            Check(NativeFinalKillcam.Select(deaths,2,m,out why)<0&&why==FinalKillSkip.Mode,"mode "+mode+" (PvE, Siren, sandbox, practice) shows none");
        }
        // A death record from before this fight (FightAt) never counts; no deaths at all is a skip.
        Check(NativeFinalKillcam.Select(new[]{D(2,1,5,2)},1,Ffa(1,2.2),out why)<0&&why==FinalKillSkip.NoDeath,"previous fight's record");
        Check(NativeFinalKillcam.Select(Array.Empty<FinalKillCandidate>(),0,Ffa(1,20),out why)<0&&why==FinalKillSkip.NoDeath,"no deaths");
    }
    private static void Curve()
    {
        var c=FinalKillcamCurve.Ideal;
        Check(c.Rate>=.2f&&c.Rate<=.3f,"slow motion between .2 and .3");
        CurveShape(c,"ideal");
        // Its total: pre-roll 2.4 s, hit at 3.04 s wall, about 6.4 s.
        Check(Math.Abs(c.Wall-6.4375f)<1e-3f&&Math.Abs(c.Span-4.1875f)<1e-3f&&Math.Abs(c.HitWall-3.0375f)<1e-3f,"ideal durations "+c.Wall+" / "+c.Span+" / "+c.HitWall);
    }
    // Shape checks for any curve: monotonic and continuous replay time, a smooth rate, the slowed stretch around the hit,
    // and the end marks.
    private static void CurveShape(FinalKillcamCurve c,string name)
    {
        const float dt=.0005f;float last=c.Replay(0),lastSpeed=c.Speed(0);double area=0;
        Check(Math.Abs(last)<1e-6f,name+": starts at the clip's start");
        // Float clocks: each step is compared with the wall step actually taken, with a few ulps of slack at ~6 s.
        float before=0;
        for(int i=1;i*dt<=c.Wall+.2f;i++)
        {
            float w=i*dt,step=w-before,r=c.Replay(w),s=c.Speed(w);before=w;
            if(!(r>last))throw new Exception("Final killcam: "+name+": replay clock not increasing at "+w);
            if(r-last>step+4e-6f||r-last<step*c.Rate-4e-6f)throw new Exception("Final killcam: "+name+": replay step out of range at "+w+": "+(r-last)+" for "+step);
            if(Math.Abs(s-lastSpeed)>.02f)throw new Exception("Final killcam: "+name+": rate jumps at "+w);
            area+=(s+lastSpeed)/2*step;last=r;lastSpeed=s;
        }
        count+=3;
        // The replay clock is the integral of the rate.
        Check(Math.Abs(area-(c.Replay(c.Wall+.2f)))<.002,name+": replay clock is the integral of the rate");
        Check(Math.Abs(c.Replay(c.W2)-(c.Preroll-c.Lead))<1e-3f&&Math.Abs(c.Replay(c.W3)-(c.Preroll+c.Hold))<1e-3f,name+": hold spans the hit's lead and death reaction");
        Check(Math.Abs(c.Replay(c.HitWall)-c.Preroll)<1e-3f&&c.HitWall>c.W2&&c.HitWall<c.W3,name+": the fatal hit plays inside the hold");
        Check(Math.Abs(c.Speed((c.W2+c.W3)/2)-c.Rate)<1e-5f&&Math.Abs(c.Speed(c.HitWall)-c.Rate)<1e-5f,name+": at the slow rate through the hit");
        Check(c.Speed(c.W1*.5f)==1&&c.Speed(c.Wall-1e-3f)==1&&c.Slow(c.HitWall)==1&&c.Slow(0)==0,name+": normal speed before and after");
        Check(Math.Abs(c.Replay(c.Wall)-c.Span)<1e-3f&&c.Replay(c.Wall+1)>c.Span+.99f,name+": ends at its span, normal speed past it");
    }
    private static void Fitting()
    {
        // The Result phase today: 5 s from the death step, a guest seeing it .05 s late, .9 s left for the hand-off.
        float available=NativeFinalKillcam.Available(25,20.05,false);Check(Math.Abs(available-4.05f)<1e-4f,"available in a 5 s result");
        Check(NativeFinalKillcam.Fit(available,5.5f,NativeFinalKillcam.Behind(20.05,20),out var c,out var why)&&why==FinalKillSkip.None,"fits a 5 s result");
        Check(c.Wall<=available+1e-3f&&c.Rate<=.3f&&c.Preroll>=1&&c.Hold>=.3f,"5 s result keeps a quarter-speed hit: "+c.Wall+" s at x"+c.Rate+", preroll "+c.Preroll+", hold "+c.Hold);
        CurveShape(c,"fitted 5 s result");
        // Never overlaps the next phase: the clip plus the hand-off ends by the phase end, for every start delay.
        for(double seen=20;seen<21.5;seen+=.05)
        {
            float a=NativeFinalKillcam.Available(25,seen,false);
            if(NativeFinalKillcam.Fit(a,5.5f,NativeFinalKillcam.Behind(seen,20),out var f,out _)&&seen+f.Wall+NativeFinalKillcam.Handoff>25+1e-3)throw new Exception("Final killcam: overlaps the next phase from "+seen);
        }
        count++;
        // A match-ending kill (the Match phase waits for the replay) and a longer Result (the Astra request) play it whole.
        Check(NativeFinalKillcam.Fit(NativeFinalKillcam.Available(0,20,true),5.5f,NativeFinalKillcam.Behind(20,20),out c,out _)&&Math.Abs(c.Wall-FinalKillcamCurve.Ideal.Wall)<1e-4f,"match end: the ideal clip");
        Check(NativeFinalKillcam.Fit(NativeFinalKillcam.Available(28.5,20,false),5.5f,NativeFinalKillcam.Behind(20,20),out c,out _)&&Math.Abs(c.Wall-FinalKillcamCurve.Ideal.Wall)<1e-4f,"8.5 s result: the ideal clip");
        // Longer time never gives a shorter clip; too little time or history skips it.
        float previous=0;
        for(float a=1.7f;a<7;a+=.05f){if(NativeFinalKillcam.Fit(a,5.5f,5.6f,out var f,out _)){if(f.Wall+1e-4f<previous||f.Wall>a+1e-3f)throw new Exception("Final killcam: fit not monotonic at "+a);previous=f.Wall;}}
        count++;
        Check(!NativeFinalKillcam.Fit(NativeFinalKillcam.Shortest-.05f,5.5f,5.6f,out _,out why)&&why==FinalKillSkip.NoTime,"too little time: none");
        Check(NativeFinalKillcam.Fit(NativeFinalKillcam.Shortest+.01f,5.5f,5.6f,out c,out _)&&c.Rate<.5f,"shortest still slows");
        Check(!NativeFinalKillcam.Fit(4,.3f,5.6f,out _,out why)&&why==FinalKillSkip.NoHistory,"missing history: none");
        Check(NativeFinalKillcam.Fit(4.05f,1.1f,5.6f,out c,out _)&&c.Preroll<=1.1f+1e-4f&&c.W1>=0,"short history: shorter pre-roll");
        CurveShape(c,"short history");
        // The clip never falls further behind the live clock than the kept history (so trimming never takes its frame).
        Check(NativeFinalKillcam.Fit(NativeFinalKillcam.Longest,5.5f,3.2f,out c,out _)&&c.Preroll+c.Behind<=3.2f+1e-3f,"stays within the kept history");
        Check(Math.Abs(NativeFinalKillcam.Behind(21,20)-(NativeFinalKillcam.HistorySeconds-NativeFinalKillcam.HistoryMargin-1))<1e-4f,"history budget");
        Check(!NativeFinalKillcam.Fit(float.NaN,5,5,out _,out _),"NaN rejected");
    }
    private static void Order()
    {
        // The last victim: their death and the Result arrive in one frame. The runtime decides the final kill first, so the
        // normal killcam is never armed; if it was armed (resolution a step later) or another pilot's is playing, it is cut
        // over; a final killcam shown this round is never shown again.
        Check(NativeFinalKillcam.ArmNormal(0)&&!NativeFinalKillcam.ArmNormal(55),"no normal killcam once the final kill is decided");
        Check(NativeFinalKillcam.Begin(false,false,false)==NativeFinalKillcam.Start.Begin,"nothing playing: begin");
        Check(NativeFinalKillcam.Begin(true,true,false)==NativeFinalKillcam.Start.CutOver&&NativeFinalKillcam.Begin(false,true,false)==NativeFinalKillcam.Start.CutOver,"normal killcam playing or waiting: cut over");
        Check(NativeFinalKillcam.Begin(false,false,true)==NativeFinalKillcam.Start.Already&&NativeFinalKillcam.Begin(true,true,true)==NativeFinalKillcam.Start.Already,"never twice");
        // Model of one frame for the victim: decide (sets the seed), then the fresh death asks to arm.
        int finalSeed=0;bool normalArmed=false;var start=NativeFinalKillcam.Begin(false,normalArmed,false);if(start!=NativeFinalKillcam.Start.Already)finalSeed=55;
        if(NativeFinalKillcam.ArmNormal(finalSeed))normalArmed=true;Check(!normalArmed&&start==NativeFinalKillcam.Start.Begin,"victim sees only the final killcam");
        // Audio and camera helpers.
        Check(NativeFinalKillcam.Pitch(1)==1&&Math.Abs(NativeFinalKillcam.Pitch(.25f)-.5f)<1e-5f&&NativeFinalKillcam.Pitch(.6f)>.7f,"pitch follows the rate, an octave down at most");
        Check(NativeFinalKillcam.Duck(0)==1&&NativeFinalKillcam.Duck(1)<.5f,"music ducks while slowed");
        NativeFinalKillcam.Wide(1,false,out float boom,out float lift,out float fov,out float look);NativeFinalKillcam.Wide(1,true,out float rb,out _,out float rf,out _);NativeFinalKillcam.Wide(0,false,out float b0,out _,out float f0,out float l0);
        Check(boom>1&&lift>0&&fov>0&&look>0&&rf==0&&rb<boom&&b0==0&&f0==0&&l0==0,"wider shot while slowed (no FOV change with Reduced effects)");
    }
    private static string Body(string source,string signature)
    {
        int i=source.IndexOf(signature,StringComparison.Ordinal);if(i<0)throw new Exception("Final killcam: missing "+signature);
        int open=source.IndexOf('{',i),depth=0;for(int k=open;k<source.Length;k++){if(source[k]=='{')depth++;else if(source[k]=='}'&&--depth==0)return source.Substring(open,k-open+1);}
        throw new Exception("Final killcam: unbalanced "+signature);
    }
    private static void Sources(string root)
    {
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/"+f));
        string replay=R("Runtime/NativeReplay.cs"),final=R("Runtime/NativeFinalKillcamPresentation.cs"),match=R("Core/NativeMatch.cs"),overlays=R("Runtime/NativeMatchOverlays.cs");
        // The normal killcam for every other death: the same arming (and only while no final kill is decided), the same
        // .6 s / 1.6 s start, the same 3 s + 1.25 s window at normal speed, the same KILL CAM frame.
        Check(replay.Contains("if(fresh){replaySeen=d.seed;if(NativeFinalKillcam.ArmNormal(finalSeed)&&replayWasAlive&&replayFrames.Count>1&&arena.Time-replayFrames[0].time>=.2f){replayDeath=d.at;replayWait=Time.unscaledTime+1.6f;replayView=d.owner>0?d.owner:p.Seat.id;}}"),"normal arming unchanged");
        Check(replay.Contains("if(!replayRunning&&replayDeath>=0&&(arena.Time>=replayDeath+.6f||Time.unscaledTime>=replayWait))")&&replay.Contains("replayStart=Math.Max((float)replayFrames[0].time,replayDeath-3);replayEnd=replayDeath+1.25f;"),"normal start and window unchanged");
        Check(final.Contains("private float ReplayClock(float wall)=>finalOn?finalCurve.Replay(wall):wall;")&&replay.Contains("float time=replayStart+ReplayClock(Time.unscaledTime-replayStarted);"),"normal killcam keeps normal speed");
        Check(replay.Contains("if(menu||arena.Local.Hp>0&&!finalOn){EndReplay();return false;}")&&replay.Contains("if(p.Hp>0){if(!finalOn)EndReplay();spectatorId=0;}"),"normal killcam still ends for a living pilot");
        Check(Body(final,"private NativePilot ReplayVictim()").Contains("if(!finalOn)return replayArena.Local;"),"normal camera still falls back to my pilot");
        Check(replay.Contains("if(finalOn)FinalShot(ref offset);")&&replay.Contains("finalOn?FinalLook(focus+offset,rotation,target,victim):rotation")&&replay.Contains("NativeViewRules.ThirdFov+(finalOn?finalFov:0)"),"camera changes only in the final killcam");
        Check(replay.Contains("if(replayRunning&&finalOn){FinalKillcamHud();return;}")&&replay.IndexOf("if(replayRunning&&finalOn)",StringComparison.Ordinal)<replay.IndexOf("NativeHudChrome.Glitch(camLine,\"KILL CAM\"",StringComparison.Ordinal),"normal KILL CAM frame unchanged");
        Check(Body(final,"private void FinalFrame()").Contains("if(finalOn)")&&Body(final,"private void FinalAudioBefore()").Contains("if(finalOn)")&&Body(final,"private void FinalAudioAfter()").Contains("if(finalOn)"),"clock and audio slowed only in the final killcam");
        Check(replay.IndexOf("FinalKillWatch();",StringComparison.Ordinal)<replay.IndexOf("if(fresh){replaySeen=d.seed;",StringComparison.Ordinal),"final kill decided before the normal one can arm");
        Check(replay.Contains("CreateScene(finalOn?\"Final killcam presentation\":\"Killcam presentation\""),"own scene name (a cut-over never collides with the unloading one)");
        // Presentation clocks default to today's behaviour.
        string slow=R("Runtime/NativeSlowMotionPresentation.cs"),blast=R("Runtime/NativeBlastPresentation.cs"),shots=R("Runtime/NativeShotPresentation.cs"),corpse=R("Runtime/NativeCorpseSmoothing.cs"),death=R("Runtime/NativeDeathPresentation.cs");
        Check(slow.Contains("private float clockRate=1;")&&slow.Contains("public bool SlowBodies;")&&blast.Contains("public float Rate=1;")&&shots.Contains("public float Rate=1;")&&blast.Contains("Time.unscaledDeltaTime*Rate")&&shots.Contains("Time.unscaledDeltaTime*Rate"),"particle sims follow the replay rate, 1 by default");
        Check(Body(corpse,"private Matrix4x4 Posed(").Contains("if(!Smooth||smoothT>=1")&&death.Contains("float age=(SlowBodies?KillClock(arena):arena.Time)-death.at;")&&death.Contains("corpse.Smooth=SlowBodies;"),"bodies blend only in the final killcam");
        Check(Body(final,"private void FinalStop()").Contains("presentation.ClockRate=1;presentation.SlowBodies=false;")&&Body(final,"private void FinalStop()").Contains("ReplayRestore()"),"clocks and pitch restored when it ends");
        // The broadcast frame draws without per-frame allocation: no string building, no array or delegate creation.
        string hud=Body(final,"private void FinalKillcamHud()");
        Check(!hud.Contains("ToString(")&&!Regex.IsMatch(hud,"\"\\s*\\+|\\+\\s*\"")&&!hud.Contains("new[]")&&!hud.Contains("=>")&&!hud.Contains("string."),"broadcast frame allocation free");
        Check(hud.Contains("\"FINAL KILLCAM\"")&&hud.Contains("finalKillerName")&&hud.Contains("finalVictimName")&&hud.Contains("finalHow")&&hud.Contains("finalWinnerName")&&hud.Contains("\"Space\""),"title, killer and victim, cause, outcome and skip");
        // Presentation only: the round result still lasts 5 s and resolves as before; no network message added.
        Check(match.Contains("Phase=NativePhase.Result;PhaseEnd=Time+5;")&&!match.Contains("finalKill"),"match rules unchanged");
        Check(overlays.Contains("age=FinalResultAge(5-left)")&&final.Contains("private float FinalResultAge(float age)=>finalShown&&!finalOn&&finalEndedAt>0?Mathf.Min(age,Time.unscaledTime-finalEndedAt):age;"),"result banner arrives at the hand-off");
        foreach(var f in new[]{"Core/NativeFinalKillcam.cs","Runtime/NativeFinalKillcamPresentation.cs","Runtime/NativeReplayAudio.cs","Runtime/NativeSlowMotionPresentation.cs","Runtime/NativeCorpseSmoothing.cs"})
            Check(File.Exists(Path.Combine(root,"Assets/ChipArena/"+f+".meta")),f+" has its Unity meta");
    }
}
