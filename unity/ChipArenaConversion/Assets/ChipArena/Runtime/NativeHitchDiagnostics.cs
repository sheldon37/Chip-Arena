using System;
using UnityEngine;
using Stopwatch=System.Diagnostics.Stopwatch;
namespace ChipArena.UnityConversion
{
    public sealed partial class ArenaPresentation
    {public int DiagnosticCorpseCount=>corpses.Count;public int DiagnosticSlashCount=>slashes.Count;}
    public sealed partial class ConversionGame
    {
        private readonly NativeHitchWindow hitchWindow=new NativeHitchWindow();
        private long hitchLast;private double hitchUpdate,hitchFixed;private int hitchTicks;
        private int hitchGc0,hitchGc1,hitchGc2;private bool hitchReady;
        private double hitchReplay,hitchSnapshot,hitchEffects,hitchArenaStep,hitchPhysics,hitchNavigation,hitchDeathRecord,hitchHorde,hitchHordeSpawn;private int hitchNavSearches;private ArenaPresentation hitchDeathView;private double hitchDeathViewLast;
        private string lastHitchReport="No stall recorded",worstActiveHitchReport="No active frame recorded";
        private double worstActiveFrameMs;private NativeArena hitchActiveArena;
        private double hitchNavNearest,hitchNavLinks;private int hitchNavLinkChecks;
        private double hitchDeathBuildLast,hitchDeathStepLast,hitchDeathWoundLast;private int hitchDeathCreatesLast;
        private double sceneryLast,hordeViewLast,zombiePoseLast,sourceFlushLast;
        private double hitchReplayPlayback,hitchReplayPrepare,hitchReplayApply,hitchAbilities,hitchStatus,hitchGlow;
        private static double HitchMs(long since)=>(Stopwatch.GetTimestamp()-since)*1000d/Stopwatch.Frequency;
        [Serializable] private sealed class HitchReport
        {
            public string utc,map,weapon,phase,graphics,graphicsApi,pveStage;public int pvePhase,hordeCap;
            public bool focused,paused,replica,reducedEffects;
            public double arenaStepMs,physicsMs,navigationMs,deathRecordMs,deathPresentationMs,hordeMs,hordeSpawnMs;public int navigationSearches;
            public double frameMs,averageMs,worstMs,simulationMs,updateMs,presentationMs,animationMs,unattributedMs,replayMs,snapshotMs,effectsMs;
            public double navigationNearestMs,navigationLinkMs,deathBuildMs,deathStepMs,deathWoundMs;public int navigationLinkChecks,deathCreates;
            public double abilitiesMs,statusMs,glowMs,sceneryMs,hordePresentationMs,zombiePoseMs,sourceFlushMs;
            public double replayPlaybackMs,replayPrepareMs,replayApplyMs;public bool replayActive;
            public int fixedTicks,gc0,gc1,gc2,pilots,minions,projectiles,props,events,particles,drawnParts,replayFrames,width,height,hitches,corpses,slashes;
            public long managedBytes;
        }
        private void FinishHitchFrame(double viewMs)
        {
            long now=Stopwatch.GetTimestamp();int g0=GC.CollectionCount(0),g1=GC.CollectionCount(1),g2=GC.CollectionCount(2);
            double scenery=presentation?.DiagnosticSceneryMs??0,hordeView=presentation?.DiagnosticHordePresentationMs??0,zombiePose=presentation?.DiagnosticZombiePoseMs??0,sourceFlush=presentation?.DiagnosticSourceFlushMs??0;
            double deathView=presentation?.DiagnosticDeathPresentationMs??0;
            double deathBuild=presentation?.DiagnosticDeathBuildMs??0,deathStep=presentation?.DiagnosticDeathStepMs??0,deathWound=presentation?.DiagnosticDeathWoundMs??0;
            int deathCreates=presentation?.DiagnosticDeathCreates??0;bool sameDeathView=ReferenceEquals(hitchDeathView,presentation);
            double deathViewMs=ReferenceEquals(hitchDeathView,presentation)?Math.Max(0,deathView-hitchDeathViewLast):deathView;
            if(hitchReady&&!dedicated&&arena!=null)
            {
                double dt=(now-hitchLast)*1000d/Stopwatch.Frequency;
                if(!ReferenceEquals(hitchActiveArena,arena))
                {hitchActiveArena=arena;worstActiveFrameMs=0;worstActiveHitchReport="No active frame recorded";}
                bool report=hitchWindow.Observe(now/(double)Stopwatch.Frequency,dt);
                bool activeWorst=hitchArenaStep>0&&!replayRunning&&!(menu&&!Online)&&dt>worstActiveFrameMs;
                if(report||activeWorst)
                {
                    var r=new HitchReport{utc=DateTime.UtcNow.ToString("O"),map=arena.MapId,weapon=arena.Local?.Seat.spec,
                        phase=match?.Phase.ToString(),graphics=prefs?.graphics,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),
                        focused=Application.isFocused,paused=menu&&!Online,replica=arena.Replica,reducedEffects=prefs?.reducedEffects??false,
                        frameMs=dt,averageMs=hitchWindow.AverageMs,worstMs=hitchWindow.WorstMs,simulationMs=hitchFixed,
                        pveStage=arena.Pve?.Stage,pvePhase=arena.Pve?.Phase??0,hordeCap=arena.Pve?.Cap??0,hordeMs=hitchHorde,hordeSpawnMs=hitchHordeSpawn,
                        arenaStepMs=hitchArenaStep,physicsMs=hitchPhysics,navigationMs=hitchNavigation,navigationSearches=hitchNavSearches,deathRecordMs=hitchDeathRecord,deathPresentationMs=deathViewMs,
                        navigationNearestMs=hitchNavNearest,navigationLinkMs=hitchNavLinks,navigationLinkChecks=hitchNavLinkChecks,
                        deathBuildMs=Math.Max(0,deathBuild-(sameDeathView?hitchDeathBuildLast:0)),deathStepMs=Math.Max(0,deathStep-(sameDeathView?hitchDeathStepLast:0)),deathWoundMs=Math.Max(0,deathWound-(sameDeathView?hitchDeathWoundLast:0)),deathCreates=Math.Max(0,deathCreates-(sameDeathView?hitchDeathCreatesLast:0)),
                        replayMs=hitchReplay,snapshotMs=hitchSnapshot,effectsMs=hitchEffects,abilitiesMs=hitchAbilities,statusMs=hitchStatus,glowMs=hitchGlow,corpses=presentation?.DiagnosticCorpseCount??0,slashes=presentation?.DiagnosticSlashCount??0,
                        sceneryMs=Math.Max(0,scenery-(sameDeathView?sceneryLast:0)),hordePresentationMs=Math.Max(0,hordeView-(sameDeathView?hordeViewLast:0)),zombiePoseMs=Math.Max(0,zombiePose-(sameDeathView?zombiePoseLast:0)),sourceFlushMs=Math.Max(0,sourceFlush-(sameDeathView?sourceFlushLast:0)),
                        replayPlaybackMs=hitchReplayPlayback,replayPrepareMs=hitchReplayPrepare,replayApplyMs=hitchReplayApply,replayActive=replayRunning,
                        fixedTicks=hitchTicks,updateMs=hitchUpdate,presentationMs=viewMs,animationMs=presentation?.AnimationMs??0,
                        // Includes rendering/GPU waits, scheduling, other callbacks and uninstrumented work; NOT a GPU measurement.
                        unattributedMs=Math.Max(0,dt-hitchFixed-hitchUpdate-viewMs),gc0=g0-hitchGc0,gc1=g1-hitchGc1,gc2=g2-hitchGc2,
                        managedBytes=GC.GetTotalMemory(false),pilots=arena.Pilots.Count,minions=arena.Minions.Count,
                        projectiles=arena.Bullets.Count,props=arena.Props.Count,events=arena.Events.Count,
                        particles=presentation?.ParticleCount??0,drawnParts=presentation?.DrawnParts??0,
                        replayFrames=this.replayFrames.Count,width=Screen.width,height=Screen.height,hitches=hitchWindow.Hitches};
                    string json=JsonUtility.ToJson(r);
                    if(activeWorst){worstActiveFrameMs=dt;worstActiveHitchReport=json;}
                    if(report){lastHitchReport=json;Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"PERF_HITCH {0}",json);}
                }
            }
            sceneryLast=scenery;hordeViewLast=hordeView;zombiePoseLast=zombiePose;sourceFlushLast=sourceFlush;
            hitchDeathView=presentation;hitchDeathViewLast=deathView;hitchDeathBuildLast=deathBuild;hitchDeathStepLast=deathStep;hitchDeathWoundLast=deathWound;hitchDeathCreatesLast=deathCreates;
            hitchNavNearest=hitchNavLinks=0;hitchNavLinkChecks=0;
            hitchReplayPlayback=hitchReplayPrepare=hitchReplayApply=hitchAbilities=hitchStatus=hitchGlow=0;
            hitchLast=now;hitchGc0=g0;hitchGc1=g1;hitchGc2=g2;hitchReady=true;hitchUpdate=hitchFixed=hitchReplay=hitchSnapshot=hitchEffects=hitchArenaStep=hitchPhysics=hitchNavigation=hitchDeathRecord=hitchHorde=hitchHordeSpawn=0;hitchTicks=hitchNavSearches=0;
        }
    }
}
