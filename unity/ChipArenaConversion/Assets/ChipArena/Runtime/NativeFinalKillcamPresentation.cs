using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The final killcam (Core/NativeFinalKillcam.cs, docs/FINAL-KILLCAM.md): when a round ends on a death (every round of
    // every set, practice included), every client (the killer, the survivors, the dead and spectators) replays it from its
    // own recorded frames in the killcam's presentation scene (Runtime/NativeReplay.cs), with a CoD-style slow motion around
    // the fatal hit, its own broadcast frame, a wider slow-motion shot and a local skip, then hands off to the round result. Presentation only: the match,
    // its phases and the live simulation are untouched.
    public sealed partial class ConversionGame
    {
        private bool finalOn,finalShown,finalSolo;private int finalSeed,finalVictim,finalKiller;private NativePhase finalPhase=NativePhase.Lobby;
        private FinalKillcamCurve finalCurve;private FinalKillSkip finalSkip;private float finalEndedAt=-10,finalFov,finalLook;
        private int finalProjectile,finalCameraSide;private bool finalFlew;private float finalImpactWall;private Vector3 finalCameraAim,finalFlightEye,finalFlightAim;
        private readonly FinalKillCandidate[] finalDeaths=new FinalKillCandidate[16];
        // The broadcast frame's text and colours, fixed when the clip starts (no per-frame strings).
        private string finalKillerName="",finalVictimName="",finalHow="",finalWinnerLabel="",finalWinnerName="",finalOutcome="",finalRound="",finalTag="FINAL KILL";
        private Color finalKillerColor,finalVictimColor,finalWinnerColor;private bool finalKillerTeam;private string finalKillerTeamName="";private Color finalKillerTeamColor;
        private int FinalSide(NativeSeat s)=>match.Teams?s.team:s.id;
        /// <summary>
        /// ObserveReplay, every frame: at the Fight -> Result step this client sees, picks the round's final kill from the
        /// replicated death records, fits the slow-motion clip into the Result time left, and starts it (cutting over a
        /// normal killcam that is playing or waiting). finalSeed then keeps the normal killcam from arming this round. A death
        /// always plays from the victim's view; kill credit only controls the broadcast labels.
        /// </summary>
        private void FinalKillWatch()
        {
            var phase=match.Phase;bool step=finalPhase==NativePhase.Fight&&phase==NativePhase.Result;finalPhase=phase;
            if(phase!=NativePhase.Result&&phase!=NativePhase.Match){finalSeed=0;finalShown=false;}
            if(!step)return;
            int n=0;
            foreach(var q in arena.Pilots)
            {
                var d=q.Death;if(n>=finalDeaths.Length||d==null||q.Hp>0)continue;var killer=d.owner>0?match.Seat(d.owner):null;
                finalDeaths[n++]=new FinalKillCandidate{Victim=q.Seat.id,Killer=d.owner,Seed=d.seed,At=d.at,VictimSide=FinalSide(q.Seat),KillerSide=killer!=null?FinalSide(killer):-1};
            }
            var winner=match.Seat(match.Winner);
            // The host's decision (NativeMatch.HasFinalKillcam, PR #178) when it sent one: its Result step time, and its
            // victim and seed when it found a qualifying kill (that Result is held for the whole clip). With no qualifying
            // kill (practice, a death with no enemy killer, a draw) or an old host, the local pick still gives every round
            // that ends on a death its final killcam (owner), fitted into the Result time.
            var context=new FinalKillContext{Teams=match.Teams,Cooperative=match.Cooperative,Pve=arena.Pve!=null,Sandbox=arena.Sandbox,Practice=!Online,Witnessed=true,Winner=match.Winner,WinnerSide=winner!=null?FinalSide(winner):-1,FightAt=match.FightAt,ResultAt=match.HasFinalKillcam?match.ResultAt:match.Time};
            int pick=match.HasFinalKillcam&&match.FinalVictim!=0?NativeFinalKillcam.Decided(finalDeaths,n,match.FinalVictim,match.FinalSeed):-1;finalSkip=FinalKillSkip.None;
            if(pick<0)pick=NativeFinalKillcam.Select(finalDeaths,n,context,out finalSkip);
            if(pick<0){Debug.Log("Final killcam skipped: "+finalSkip);return;}
            // Only the authority knows the room's sets to win (a guest's match holds its own prefs), so a guest always fits
            // the clip into the Result time.
            var f=finalDeaths[pick];bool credited=NativeFinalKillcam.Credited(f,match.Teams),matchEnds=(!Online||Hosting)&&match.Seats.Any(s=>s.score>=match.SetsToWin);
            float history=replayFrames.Count>1?(float)(f.At-replayFrames[0].time):0;
            if(!NativeFinalKillcam.Fit(NativeFinalKillcam.Available(match.PhaseEnd,match.Time+NativeFinalKillcam.StartDelay,matchEnds),history,NativeFinalKillcam.Behind(match.Time,f.At),out var curve,out finalSkip)){Debug.Log("Final killcam skipped: "+finalSkip);return;}
            var start=NativeFinalKillcam.Begin(replayRunning,replayDeath>=0,finalShown);if(start==NativeFinalKillcam.Start.Already)return;
            if(start==NativeFinalKillcam.Start.CutOver)EndReplay();
            finalSeed=f.Seed;finalShown=true;finalOn=true;finalCurve=curve;finalVictim=f.Victim;finalKiller=credited?f.Killer:0;finalSolo=!credited;
            // The final victim anchors side/aftermath framing. Keep credit for
            // the labels separate from the camera; the normal killcam still selects its own view in ObserveReplay.
            replayDeath=(float)f.At;replayWait=Time.unscaledTime+NativeFinalKillcam.StartDelay;replayView=f.Victim;FinalLabels(matchEnds,f.Killer);
            Debug.Log("Final killcam: "+(credited?"seat "+f.Killer+" on seat ":"final moments of seat ")+f.Victim+" · "+curve.Wall.ToString("0.00")+" s at x"+curve.Rate.ToString("0.00"));
        }
        private void FinalLabels(bool matchEnds,int source)
        {
            var killer=arena.Pilots.Find(p=>p.Seat.id==finalKiller);var victim=arena.Pilots.Find(p=>p.Seat.id==finalVictim);var winner=match.Seat(match.Winner);
            finalVictimName=victim?.Seat.name??"Unknown";finalVictimColor=SeatColor(victim?.Seat);finalTag=finalSolo?"FINAL ELIMINATION":"FINAL KILL";
            if(finalSolo)
            {
                // No killer to name: the lower third carries the victim and the cause (a team kill names its teammate).
                bool self=source==finalVictim;var mate=!self&&source>0?match.Seat(source):null;
                finalKillerName=finalVictimName;finalKillerColor=finalVictimColor;finalKillerTeam=false;
                finalHow=NativeFinalKillcam.SoloCause(self,mate!=null,victim?.Death?.damageKind);if(mate!=null)finalHow+="  ·  "+mate.name;
            }
            else
            {
                finalKillerName=killer?.Seat.name??match.Seat(finalKiller)?.name??"Unknown";finalKillerColor=SeatColor(killer?.Seat??match.Seat(finalKiller));
                finalHow=killer!=null&&victim?.Death!=null?killer.Weapon.name+"  ·  "+KillCause(victim.Death,killer):"Final elimination";
                finalKillerTeam=match.Teams&&killer!=null;if(finalKillerTeam){finalKillerTeamColor=TeamColor(killer.Seat.team);finalKillerTeamName=NativeMatch.TeamNames[TeamIndex(killer.Seat.team)].ToUpperInvariant();}
            }
            // A simultaneous knockout has no winner: the outcome card says so with the round's result line.
            finalWinnerLabel=winner==null?"NO WINNER":matchEnds?"MATCH WINNER":match.SetWinner!=0?"SET WINNER":"ROUND WINNER";
            finalWinnerName=winner==null?"Draw":match.Teams?NativeMatch.TeamNames[TeamIndex(winner.team)]+" team":winner.name;finalWinnerColor=winner==null?NativeCyberTheme.Ink():match.Teams?TeamColor(winner.team):SeatColor(winner);
            finalOutcome=match.Result??"";finalRound="ROUND "+match.Round+"  ·  SET "+match.Set;
        }
        /// <summary>RenderReplayFrame's start block, final killcam: the clip window from the fitted curve and the slowed
        /// presentation. If history was trimmed since the pick (normally not: FinalKeeps pins it during the delay), refit to it.</summary>
        private void FinalBegin()
        {
            float history=replayDeath-(float)replayFrames[0].time;
            if(history<finalCurve.Preroll-1e-3f&&!NativeFinalKillcam.Fit(finalCurve.Wall,history,NativeFinalKillcam.Behind(match.Time,replayDeath),out finalCurve,out finalSkip)){EndReplay();return;}
            PrepareFinalCamera();
            replayStart=replayDeath-finalCurve.Preroll;replayEnd=replayStart+finalCurve.Span;presentation.SlowBodies=true;presentation.ClockRate=1;
        }
        /// <summary>The replay clock: wall seconds since the clip started -> replay seconds since its start (the normal
        /// killcam plays at normal speed).</summary>
        private float ReplayClock(float wall)=>finalOn?finalCurve.Replay(wall):wall;
        private float FinalWall=>Time.unscaledTime-replayStarted;
        /// <summary>The pilot the replay's camera falls back to: the final kill's victim, else (normal killcam) my own pilot.</summary>
        private NativePilot ReplayVictim()
        {
            if(!finalOn)return replayArena.Local;
            foreach(var p in replayArena.Pilots)if(p.Seat.id==finalVictim)return p;return replayArena.Local;
        }
        /// <summary>Each replay frame before the presentation renders: wall-time effect sims follow the playback rate.</summary>
        private void FinalFrame(){if(finalOn)presentation.ClockRate=finalCurve.Speed(FinalWall);}
        /// <summary>The slow-motion shot: the boom pulls back and up (Core NativeFinalKillcam.Wide), before the collision cast.</summary>
        private void FinalShot(ref Vector3 offset)
        {
            NativeFinalKillcam.Wide(finalCurve.Slow(FinalWall),prefs.reducedEffects,out float boom,out float lift,out finalFov,out finalLook);
            float length=offset.magnitude;if(length>1e-4f)offset=offset/length*(length+boom);offset+=Vector3.up*lift;
        }
        private void PrepareFinalCamera()
        {
            finalProjectile=0;finalCameraSide=0;finalFlew=false;finalImpactWall=-1;
            var death=arena.Pilots.Find(p=>p.Seat.id==finalVictim)?.Death;if(death==null||death.projectile<=0)return;
            int samples=0;float distance=0;
            foreach(var frame in replayFrames)
            {
                if(frame.time>replayDeath)break;
                foreach(var shot in frame.snapshot.Shots)if(shot.Id==death.projectile)
                {samples++;distance=Math.Max(distance,Vector3.Distance(NativeArena.Vec(shot.Position),death.position));}
            }
            if(NativeFinalKillcam.FlightCamera(distance,samples))finalProjectile=death.projectile;
        }
        // Stable left/right choice avoids cuts as the actors turn. Collision is tested again every replay frame.
        private void FinalCamera(float time,NativePilot victim,ref Vector3 focus,ref Vector3 offset)
        {
            if(victim==null)return;
            var killer=replayArena.Pilots.Find(p=>p.Seat.id==finalKiller);
            Vector3 body=victim.Hp<=0&&victim.Death!=null?victim.Death.position:victim.Body.position;
            Vector3 axis=killer!=null?body-killer.Body.position:Quaternion.Euler(0,victim.Yaw,0)*Vector3.forward;
            float separation=axis.magnitude;axis.y=0;if(axis.sqrMagnitude<.01f)axis=Vector3.forward;axis.Normalize();
            Vector3 side=Vector3.Cross(Vector3.up,axis);
            bool near=killer!=null&&separation<18;
            float aftermath=NativeFinalKillcam.DeathPullback(Math.Max(0,time-replayDeath));
            Vector3 aftermathFocus=body+Vector3.up*.35f;
            focus=near?Vector3.Lerp(Vector3.Lerp(killer.Body.position,body,.6f)+Vector3.up*.5f,aftermathFocus,aftermath):aftermathFocus;
            float boom=Mathf.Lerp(near?Mathf.Clamp(separation*.65f+3,4,12):4.5f,6,aftermath);
            Vector3 left=side*boom+Vector3.up*2-axis*1.5f,right=-side*boom+Vector3.up*2-axis*1.5f;
            if(finalCameraSide==0)finalCameraSide=FinalClearance(focus,left)>=FinalClearance(focus,right)?1:-1;
            offset=finalCameraSide>0?left:right;finalCameraAim=focus;
            if(finalProjectile>0&&FinalWall>=.35f&&time<replayDeath&&replayShots.Sample(finalProjectile,time,out var at,out var velocity)&&velocity.Length>.01f)
            {
                Vector3 direction=NativeArena.Vec(velocity).normalized;focus=NativeArena.Vec(at);offset=-direction*.65f+Vector3.up*.12f;
                finalCameraAim=focus+direction*6;finalFlightEye=focus+offset;finalFlightAim=finalCameraAim;finalFlew=true;finalFov=0;return;
            }
            if(finalProjectile>0&&!finalFlew&&killer!=null&&time<replayDeath)
            {
                // Open on the killer's recorded aim before cutting to the actual shot's flight.
                // Even when the shot predates our preroll, give the shooter a brief establishing view.
                Quaternion aim=Quaternion.Euler(-killer.Pitch,killer.Yaw,0);
                focus=killer.Body.position+Vector3.up*(NativeViewRules.EyeHeight+.22f)+Quaternion.Euler(0,killer.Yaw,0)*Vector3.right*.85f;
                offset=aim*Vector3.back*NativeViewRules.ThirdDistance+Vector3.up*NativeViewRules.ThirdLift;
                finalCameraAim=focus+aim*Vector3.forward*20;finalFov=0;return;
            }
            if(finalFlew)
            {
                if(finalImpactWall<0)finalImpactWall=FinalWall;
                float blend=NativeFinalKillcam.DeathPullback(FinalWall-finalImpactWall);
                Vector3 eye=Vector3.Lerp(finalFlightEye,focus+offset,blend);
                // The bullet view holds the slow-motion widening at 0; ease it back in with the pull-back, not as a jump at the hit.
                finalCameraAim=Vector3.Lerp(finalFlightAim,focus,blend);offset=eye-focus;finalFov*=blend;
            }
        }
        private float FinalClearance(Vector3 focus,Vector3 offset)
        {
            return replayScene.GetPhysicsScene().SphereCast(focus,.18f,offset.normalized,out var hit,offset.magnitude,NativeArena.WorldMask,QueryTriggerInteraction.Ignore)?hit.distance:offset.magnitude;
        }
        private Quaternion FinalLook(Vector3 eye,Quaternion rotation,NativePilot target,NativePilot victim)
        {
            Vector3 to=finalCameraAim-eye;return to.sqrMagnitude<.001f?rotation:Quaternion.LookRotation(to);
        }
        private void FinalAudioBefore(){if(finalOn)nativeAudio.ReplayRestore();}
        private void FinalAudioAfter(){if(finalOn){float w=FinalWall;nativeAudio.ReplaySlow(NativeFinalKillcam.Pitch(finalCurve.Speed(w)),NativeFinalKillcam.Duck(finalCurve.Slow(w)));}}
        /// <summary>EndReplay: the final killcam's clocks and audio back to normal, and the hand-off time when it played.</summary>
        private void FinalStop()
        {
            if(finalOn&&replayRunning)finalEndedAt=Time.unscaledTime;finalOn=false;
            if(presentation!=null){presentation.ClockRate=1;presentation.SlowBodies=false;}nativeAudio?.ReplayRestore();
        }
        private void FinalClear(){finalOn=false;finalShown=false;finalSolo=false;finalSeed=0;finalPhase=NativePhase.Lobby;finalEndedAt=-10;}
        /// <summary>ObserveReplay's history trim: while the final killcam plays, the frame its clip starts from is kept (the
        /// entity cap can hold less than the clip falls behind the live clock on a large map).</summary>
        private bool FinalKeeps()=>finalOn&&replayDeath>=0&&replayFrames.Count>1&&replayFrames[1].time>replayDeath-finalCurve.Preroll;
        /// <summary>Space skips the final killcam for a living pilot too (only on this screen; the dead already skip with it).</summary>
        private bool FinalSkipKey()
        {
            if(!finalOn||!replayRunning||menu||BoardOpen||arena?.Local==null||arena.Local.Hp<=0||!Input.GetKeyDown(KeyCode.Space))return false;EndReplay();return true;
        }
        /// <summary>The Result phase's length: the host's held Result when it sent its decision (phaseEnd - resultAt), else 5 s.</summary>
        private float ResultSeconds()=>match.HasFinalKillcam&&match.PhaseEnd>match.ResultAt?(float)(match.PhaseEnd-match.ResultAt):5;
        /// <summary>The round result's age after a final killcam: its banner arrives with the hand-off, not under the replay.</summary>
        private float FinalResultAge(float age)=>finalShown&&!finalOn&&finalEndedAt>0?Mathf.Min(age,Time.unscaledTime-finalEndedAt):age;
        /// <summary>The three seconds before a final clip (NativeFinalKillcam.StartDelay) keep the live view, but not the round
        /// result: it waits for the hand-off above and plays once, after the replay, instead of starting under the wait.</summary>
        private bool FinalWaiting=>finalOn&&!replayRunning&&Time.unscaledTime<replayWait;

        // ---- FINAL KILLCAM broadcast frame (killcam-hud.ts style: everything on the edges, the centre left to the kill) ----
        private static readonly float[] finalVignetteAt={0,.55f,1,1.05f},finalVignetteAlpha={0,0,1,1},finalFlashAt={0,.75f,1},finalFlashAlpha={.5f,.125f,.125f},finalHitAt={0,.68f,1},finalHitAlpha={0,.2f,1},finalHandAt={0,.7f,1},finalHandAlpha={.33f,0,0};
        private static string[] finalClock,finalRates;
        // "−1.2s" / "0.0s" / "+0.8s" and "×0.25" for every tenth / twentieth, built once.
        private static string FinalClockText(float rel)
        {
            if(finalClock==null){finalClock=new string[199];for(int i=0;i<199;i++){int t=i-99;finalClock[i]=t<0?"−"+(-t/10f).ToString("0.0")+"s":t>0?"+"+(t/10f).ToString("0.0")+"s":"0.0s";}}
            int k=Mathf.Clamp(Mathf.RoundToInt(rel*10),-99,99);return finalClock[k+99];
        }
        private static string FinalRateText(float rate)
        {
            if(finalRates==null){finalRates=new string[21];for(int i=0;i<=20;i++)finalRates[i]="×"+(i/20f).ToString("0.00");}
            return finalRates[Mathf.Clamp(Mathf.RoundToInt(rate*20),0,20)];
        }
        private static float FinalPop(float t)=>NativeUiMotion.Bezier(.2f,.8f,.2f,1,t);
        private static float FinalIn(float t)=>NativeUiMotion.Bezier(.42f,0,1,1,t);
        private static float FinalBar(float t)=>NativeUiMotion.Bezier(.5f,0,.8f,.4f,t);
        private void FinalKillcamHud()
        {
            float W=NativeUi.W,H=NativeUi.H,wall=FinalWall,g=24;var c=finalCurve;bool reduced=prefs.reducedEffects;
            float slow=c.Slow(wall),rate=c.Speed(wall),rel=replayTime-replayDeath,close=c.Wall-.3f,shut=FinalIn(NativeUiMotion.Progress(wall,close,.26f)),shutBar=FinalBar(NativeUiMotion.Progress(wall,close,.26f));
            Color accent=finalKillerColor,gold=NativeCyberTheme.Warn();
            // Letterbox: taller than the killcam's, squeezing in a little more while slowed (not with Reduced effects).
            float kb=Mathf.Clamp(H*.085f,40,76)*(1+(reduced?0:.16f*slow)),lb=FinalPop(NativeUiMotion.Progress(wall,0,.34f))*(1-shutBar);
            NativeUi.Fill(new Rect(0,0,W,kb*lb),NativeCyberTheme.Void(.92f));NativeUi.Fill(new Rect(0,H-kb*lb,W,kb*lb),NativeCyberTheme.Void(.92f));
            if(lb>0){float rule=NativeHudChrome.Snap(1);NativeUi.Fill(new Rect(0,kb*lb-rule,W,rule),NativeUi.A(gold,.45f*lb));NativeUi.Fill(new Rect(0,H-kb*lb,W,rule),NativeUi.A(gold,.45f*lb));}
            // Vignette in the past's tone, deeper while slowed; the killer's colour on the frame edge.
            float vig=NativeUiMotion.EaseOut(NativeUiMotion.Progress(wall,0,.4f))*(1-shut);
            NativeUi.RadialStops(new Rect(-W*.25f,-H*.2f,W*1.5f,H*1.4f),NativeCyberTheme.Void((.62f+.18f*slow)*vig),finalVignetteAt,finalVignetteAlpha);
            NativeUi.Round(new Rect(0,0,W,H),Color.clear,4,NativeUi.A(accent,(.4f+.3f*slow)*vig),2);
            // The cut-in flash, and the fatal hit's pulse (white to danger; a single glitch slice unless Reduced effects).
            float flash=.7f*(1-NativeUiMotion.EaseOut(NativeUiMotion.Progress(wall,0,.26f)));NativeUi.RadialStops(new Rect(-W*.2f,-H*.2f,W*1.4f,H*1.4f),new Color(1,1,1,flash),finalFlashAt,finalFlashAlpha);
            float hitWall=c.HitWall,hit=NativeUiMotion.Progress(wall,hitWall,reduced?.35f:.7f);
            if(wall>=hitWall&&hit<1)
            {
                float k=1-NativeUiMotion.EaseOut(hit);NativeUi.RadialStops(new Rect(-W*.1f,-H*.1f,W*1.2f,H*1.2f),NativeCyberTheme.Danger((reduced?.14f:.3f)*k),finalHitAt,finalHitAlpha);
                NativeHudChrome.Slice(new Rect(0,H*.5f-60,W,120),wall-hitWall,.55f*k,reduced,NativeCyberTheme.Danger());
            }
            // Top-left tag: blinking REC dot, REPLAY and the clock relative to the fatal hit.
            float tag=FinalPop(NativeUiMotion.Progress(wall,0,.36f)),ty=kb/2,tx=g-14*(1-tag),ta=(.45f+.55f*tag)*(1-shut);bool blink=Mathf.Repeat(Time.unscaledTime,1.1f)<.55f;
            var dot=new Rect(tx,ty-4.5f,9,9);NativeUi.Glow(dot,NativeCyberTheme.Danger(.5f*ta),6);NativeUi.Round(dot,NativeCyberTheme.Danger((blink?1:.4f)*ta),6);
            NativeUi.Text(new Rect(tx+19,ty-8,90,16),"REPLAY",13,NativeCyberTheme.Ink(ta),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.2f);
            var ck=new Rect(tx+19+NativeUi.Measure("REPLAY",13,true,true,.2f).x+10,ty-11,66,22);NativeHudChrome.Tag(ck,NativeCyberTheme.Cyan(),ta,false);
            NativeUi.Text(new Rect(ck.x,ck.y,ck.width-7,ck.height),FinalClockText(rel),13,NativeCyberTheme.Ink(ta),TextAnchor.MiddleRight,mono:true,tracking:.06f);
            // Top centre: FINAL KILLCAM docked in the letterbox once the title card has gone.
            float titleOut=Mathf.Clamp(c.W1-.15f,.55f,1.15f),dock=NativeUiMotion.EaseOut(NativeUiMotion.Progress(wall,titleOut,.35f))*(1-shut);
            if(dock>0){var top=new Rect(0,ty-11,W,22);NativeUi.Text(top,"FINAL KILLCAM",17,NativeUi.A(gold,dock),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.32f,glow:NativeUi.A(gold,.25f*dock));}
            // Title card: FINAL KILLCAM across the upper third, tracking closing in as it arrives (the result banner's word
            // motion), the round under it; it gives way before the slow motion begins.
            float card=NativeUiMotion.Progress(wall,.04f,.55f),cardOut=NativeUiMotion.Progress(wall,titleOut,.32f),ca=FinalPop(card)*(1-FinalIn(cardOut));
            if(ca>0)
            {
                float F=Mathf.Clamp(W*.052f,38,74),bw=Mathf.Min(760,W*.86f),bx=(W-bw)/2,by=H*.2f-10*(1-FinalPop(card));float word=NativeUiMotion.Bezier(.16f,.8f,.2f,1,card);
                float bh=14+F+10+16+14;var band=NativeCyberTheme.Navy(.82f*ca);
                NativeUi.HGradient(new Rect(bx,by,bw*.22f,bh),Color.clear,band);NativeUi.Fill(new Rect(bx+bw*.22f,by,bw*.56f,bh),band);NativeUi.HGradient(new Rect(bx+bw*.78f,by,bw*.22f,bh),band,Color.clear);
                float rule=NativeHudChrome.Snap(1);NativeUi.HGradient(new Rect(bx,by,bw*.5f,rule),Color.clear,NativeUi.A(gold,.8f*ca));NativeUi.HGradient(new Rect(bx+bw*.5f,by,bw*.5f,rule),NativeUi.A(gold,.8f*ca),Color.clear);
                NativeUi.HGradient(new Rect(bx,by+bh-rule,bw*.5f,rule),Color.clear,NativeUi.A(gold,.8f*ca));NativeUi.HGradient(new Rect(bx+bw*.5f,by+bh-rule,bw*.5f,rule),NativeUi.A(gold,.8f*ca),Color.clear);
                var line=new Rect(bx,by+14,bw,F);NativeHudChrome.Glitch(line,"FINAL KILLCAM",F,wall-.1f,word*ca,TextAnchor.MiddleCenter,reduced,true,false,.45f-.35f*word);
                NativeUi.Text(line,"FINAL KILLCAM",F,NativeUi.A(gold,word*ca),TextAnchor.MiddleCenter,bold:true,tracking:.45f-.35f*word,glow:NativeUi.A(gold,.3f*word*ca),shadow:new Color(0,0,0,.66f*ca),blur:reduced?0:5*(1-word));
                NativeUi.Text(new Rect(bx,by+14+F+8,bw,16),finalRound,12,NativeCyberTheme.Muted(ca),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.22f);
            }
            // Slow-motion chip, top right, while the playback is slowed: the live rate and a sweep.
            float sc=NativeUiMotion.EaseOut(Mathf.Clamp01(slow*1.6f))*(1-shut);
            if(sc>.01f)
            {
                var sr=new Rect(W-g-176,ty-12,176,24);NativeHudChrome.Tag(sr,gold,sc,false);float pulse=reduced?1:.75f+.25f*Mathf.Sin(Time.unscaledTime*7);
                NativeUi.Text(new Rect(sr.x+10,sr.y,110,sr.height),"SLOW MOTION",11,NativeUi.A(gold,sc*pulse),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.16f);
                NativeUi.Text(new Rect(sr.x,sr.y,sr.width-9,sr.height),FinalRateText(rate),12,NativeCyberTheme.Ink(sc),TextAnchor.MiddleRight,mono:true);
            }
            // Lower third: FINAL KILL, killer ▸ victim in their colours, the weapon and cause; it slides away for the outcome.
            float outcomeAt=Mathf.Max(hitWall+.55f,c.W3-.35f),lower=FinalPop(NativeUiMotion.Progress(wall,.12f,.4f))*(1-FinalIn(NativeUiMotion.Progress(wall,outcomeAt,.3f)))*(1-shut);
            if(lower>0)
            {
                float fs=Mathf.Clamp(W*.022f,19,27),cw=Mathf.Min(460,W-2*g),ch=10+14+fs*1.15f+5+(finalKillerTeam?20:0)+18+11,cx=g-22*(1-lower),cy=H-kb-16-ch;
                var r=new Rect(cx,cy,cw,ch);float beat=wall>=hitWall?1-NativeUiMotion.EaseOut(NativeUiMotion.Progress(wall,hitWall,.6f)):0;
                NativeHudChrome.Panel(r,lower,accent);NativeUi.Fill(new Rect(cx,cy+6,3,ch-12),NativeUi.A(accent,lower));if(beat>0&&!reduced)NativeUi.Glow(new Rect(cx,cy+6,3,ch-12),NativeUi.A(accent,.8f*beat*lower),10);
                float ly=cy+10;NativeUi.Text(new Rect(cx+16,ly,cw-34,14),finalTag,10,NativeUi.A(gold,lower),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.22f);ly+=16;
                // Killer ▸ victim; with no killer to name (environment, self, team kill), the victim alone.
                if(finalSolo)NativeUi.Text(new Rect(cx+16,ly,cw-34,fs*1.15f),finalVictimName,fs,NativeUi.A(finalVictimColor,lower),TextAnchor.MiddleLeft,bold:true,tracking:-.02f,shadow:new Color(0,0,0,lower));
                else
                {
                    float kw=Mathf.Min(cw*.46f,NativeUi.Measure(finalKillerName,fs,true,false,-.02f).x);
                    NativeUi.Text(new Rect(cx+16,ly,kw+4,fs*1.15f),finalKillerName,fs,NativeUi.A(accent,lower),TextAnchor.MiddleLeft,bold:true,tracking:-.02f,shadow:new Color(0,0,0,lower));
                    NativeUi.Text(new Rect(cx+16+kw+10,ly,22,fs*1.15f),"▸",fs*.8f,NativeCyberTheme.Muted(lower),TextAnchor.MiddleCenter,bold:true);
                    NativeUi.Text(new Rect(cx+16+kw+36,ly,cw-kw-56,fs*1.15f),finalVictimName,fs,NativeUi.A(finalVictimColor,lower),TextAnchor.MiddleLeft,bold:true,tracking:-.02f,shadow:new Color(0,0,0,lower));
                }
                ly+=fs*1.15f+3;
                if(finalKillerTeam){var tr=new Rect(cx+16,ly,NativeUi.Measure(finalKillerTeamName,10,true,true,.12f).x+12,16);NativeUi.Round(tr,Color.clear,4,NativeUi.A(finalKillerTeamColor,lower));NativeUi.Text(tr,finalKillerTeamName,10,NativeUi.A(finalKillerTeamColor,lower),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);ly+=20;}
                NativeUi.Text(new Rect(cx+16,ly,cw-34,18),finalHow,13,NativeCyberTheme.Ink(lower),TextAnchor.MiddleLeft);
            }
            // Outcome: who took the round (set, match), centred above the bottom letterbox, after the slow motion.
            float oc=FinalPop(NativeUiMotion.Progress(wall,outcomeAt+.12f,.42f))*(1-shut);
            if(oc>0&&finalWinnerName.Length>0)
            {
                float F=Mathf.Clamp(W*.03f,24,40),ow=Mathf.Min(680,W-2*g),oh=12+14+F*1.1f+6+18+12,ox=(W-ow)/2,oy=H-kb-18-oh+12*(1-oc);var band=NativeCyberTheme.Navy(.86f*oc);
                NativeUi.HGradient(new Rect(ox,oy,ow*.2f,oh),Color.clear,band);NativeUi.Fill(new Rect(ox+ow*.2f,oy,ow*.6f,oh),band);NativeUi.HGradient(new Rect(ox+ow*.8f,oy,ow*.2f,oh),band,Color.clear);
                float rule=NativeHudChrome.Snap(1);NativeUi.HGradient(new Rect(ox,oy,ow*.5f,rule),Color.clear,NativeUi.A(finalWinnerColor,.8f*oc));NativeUi.HGradient(new Rect(ox+ow*.5f,oy,ow*.5f,rule),NativeUi.A(finalWinnerColor,.8f*oc),Color.clear);
                NativeUi.Text(new Rect(ox,oy+12,ow,14),finalWinnerLabel,11,NativeUi.A(gold,oc),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.24f);
                NativeUi.Text(new Rect(ox,oy+28,ow,F*1.1f),finalWinnerName,F,NativeUi.A(finalWinnerColor,oc),TextAnchor.MiddleCenter,bold:true,tracking:.02f,glow:NativeUi.A(finalWinnerColor,.28f*oc),shadow:new Color(0,0,0,.7f*oc));
                NativeUi.Text(new Rect(ox+20,oy+28+F*1.1f+6,ow-40,18),finalOutcome,13,NativeCyberTheme.Ink(oc),TextAnchor.MiddleCenter,bold:true);
            }
            // Timeline in the bottom letterbox: ink to the hit, danger after it, the slowed stretch shaded gold.
            float tl=(.45f+.55f*NativeUiMotion.Progress(wall,.1f,.4f))*(1-shut),tw=Mathf.Min(520,W*.44f),tlx=(W-tw)/2,tly=H-kb/2-8,span=Mathf.Max(.001f,c.Span);
            float progress=Mathf.Clamp01((replayTime-replayStart)/span),mark=Mathf.Clamp01(c.Preroll/span),s0=Mathf.Clamp01(c.W1/span),s1=Mathf.Clamp01(c.R4/span);
            NativeUi.Text(new Rect(tlx,tly-14,tw,12),"FINAL MOMENTS",10,NativeCyberTheme.Muted(tl),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.18f);
            var track=new Rect(tlx,tly+4,tw,4);NativeUi.Fill(new Rect(track.x,track.y,tw*mark,4),NativeCyberTheme.Edge(.9f*tl));NativeUi.Fill(new Rect(track.x+tw*mark,track.y,tw*(1-mark),4),NativeCyberTheme.Danger(.22f*tl));
            NativeUi.Fill(new Rect(track.x+tw*s0,track.y-4,tw*(s1-s0),12),NativeUi.A(gold,.26f*tl));NativeUi.Fill(new Rect(track.x+tw*s0,track.y+9,tw*(s1-s0),NativeHudChrome.Snap(1)),NativeUi.A(gold,.85f*tl));
            NativeUi.Fill(new Rect(track.x,track.y,tw*Mathf.Min(progress,mark),4),NativeCyberTheme.Ink(tl));if(progress>mark)NativeUi.Fill(new Rect(track.x+tw*mark,track.y,tw*(progress-mark),4),NativeCyberTheme.Danger(tl));
            var mk=new Vector2(track.x+tw*mark,track.y+2);float glow=wall>=hitWall?NativeUiMotion.EaseOut(NativeUiMotion.Progress(wall,hitWall,.8f)):1;
            if(wall>=hitWall&&glow<1)NativeUi.Glow(new Rect(mk.x-5,mk.y-5,10,10),NativeCyberTheme.Danger(.8f*(1-glow)*tl),12*glow);
            NativeUi.Push(mk,Vector2.zero,1,45);NativeUi.Fill(new Rect(mk.x-7,mk.y-7,14,14),NativeCyberTheme.Void(tl));NativeUi.Fill(new Rect(mk.x-5,mk.y-5,10,10),NativeCyberTheme.Danger(tl));if(rel<0)NativeUi.Fill(new Rect(mk.x-3,mk.y-3,6,6),NativeCyberTheme.Navy(tl));NativeUi.Pop();
            // Skip pill (Space), bottom right: this screen only; the round carries on for everyone else.
            float sk=(.45f+.55f*NativeUiMotion.Progress(wall,0,.3f))*(1-shut);var skip=new Rect(W-g-150,H-kb/2-17,150,34);float sh=NativeUi.HoverAmount(9138,skip);
            NativeHudChrome.Cut(skip,NativeUi.Mix(NativeCyberTheme.Navy(.9f*sk),NativeCyberTheme.Indigo(sk),sh),NativeHudCyber.TagCut);NativeHudChrome.Outline(skip,NativeUi.Mix(NativeCyberTheme.Edge(sk),NativeCyberTheme.Cyan(sk),sh),NativeHudCyber.TagCut);
            NativeUi.Text(new Rect(skip.x+14,skip.y,90,skip.height),"Skip replay",13,NativeCyberTheme.Ink(sk),TextAnchor.MiddleLeft,bold:true);NativeHudChrome.Key(new Rect(skip.xMax-8-48,skip.y+8,48,18),"Space",sk);
            if(NativeUi.Button(skip)&&sk>.5f)EndReplay();
        }
        /// <summary>After the final killcam: a .45 s flash over the cut back, under which the round result arrives.</summary>
        private void FinalHandoff()
        {
            float back=Time.unscaledTime-finalEndedAt;if(!finalShown||back<0||back>=.45f)return;float W=NativeUi.W,H=NativeUi.H;
            NativeUi.RadialStops(new Rect(-W*.2f,-H*.2f,W*1.4f,H*1.4f),new Color(1,1,1,(prefs.reducedEffects?.45f:.85f)*(1-NativeUiMotion.EaseOut(back/.45f))),finalHandAt,finalHandAlpha);
        }
    }
}
