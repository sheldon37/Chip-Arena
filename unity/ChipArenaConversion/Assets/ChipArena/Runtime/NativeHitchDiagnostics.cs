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
        private double hitchReplay,hitchSnapshot,hitchEffects;
        private string lastHitchReport="No stall recorded";
        private static double HitchMs(long since)=>(Stopwatch.GetTimestamp()-since)*1000d/Stopwatch.Frequency;
        [Serializable] private sealed class HitchReport
        {
            public string utc,map,weapon,phase,graphics,graphicsApi;
            public bool focused,paused,replica,reducedEffects;
            public double frameMs,averageMs,worstMs,simulationMs,updateMs,presentationMs,animationMs,unattributedMs,replayMs,snapshotMs,effectsMs;
            public int fixedTicks,gc0,gc1,gc2,pilots,minions,projectiles,props,events,particles,drawnParts,replayFrames,width,height,hitches,corpses,slashes;
            public long managedBytes;
        }
        private void FinishHitchFrame(double viewMs)
        {
            long now=Stopwatch.GetTimestamp();int g0=GC.CollectionCount(0),g1=GC.CollectionCount(1),g2=GC.CollectionCount(2);
            if(hitchReady&&!dedicated&&arena!=null)
            {
                double dt=(now-hitchLast)*1000d/Stopwatch.Frequency;
                if(hitchWindow.Observe(now/(double)Stopwatch.Frequency,dt))
                {
                    var r=new HitchReport{utc=DateTime.UtcNow.ToString("O"),map=arena.MapId,weapon=arena.Local?.Seat.spec,
                        phase=match?.Phase.ToString(),graphics=prefs?.graphics,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),
                        focused=Application.isFocused,paused=menu,replica=arena.Replica,reducedEffects=prefs?.reducedEffects??false,
                        frameMs=dt,averageMs=hitchWindow.AverageMs,worstMs=hitchWindow.WorstMs,simulationMs=hitchFixed,
                        replayMs=hitchReplay,snapshotMs=hitchSnapshot,effectsMs=hitchEffects,corpses=presentation?.DiagnosticCorpseCount??0,slashes=presentation?.DiagnosticSlashCount??0,
                        fixedTicks=hitchTicks,updateMs=hitchUpdate,presentationMs=viewMs,animationMs=presentation?.AnimationMs??0,
                        // Includes rendering/GPU waits, scheduling, other callbacks and uninstrumented work; NOT a GPU measurement.
                        unattributedMs=Math.Max(0,dt-hitchFixed-hitchUpdate-viewMs),gc0=g0-hitchGc0,gc1=g1-hitchGc1,gc2=g2-hitchGc2,
                        managedBytes=GC.GetTotalMemory(false),pilots=arena.Pilots.Count,minions=arena.Minions.Count,
                        projectiles=arena.Bullets.Count,props=arena.Props.Count,events=arena.Events.Count,
                        particles=presentation?.ParticleCount??0,drawnParts=presentation?.DrawnParts??0,
                        replayFrames=this.replayFrames.Count,width=Screen.width,height=Screen.height,hitches=hitchWindow.Hitches};
                    lastHitchReport=JsonUtility.ToJson(r);
                    Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"PERF_HITCH {0}",lastHitchReport);
                }
            }
            hitchLast=now;hitchGc0=g0;hitchGc1=g1;hitchGc2=g2;hitchReady=true;hitchUpdate=hitchFixed=hitchReplay=hitchSnapshot=hitchEffects=0;hitchTicks=0;
        }
    }
}
