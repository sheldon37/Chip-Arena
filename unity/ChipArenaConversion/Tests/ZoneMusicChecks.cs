#if ARENA_TESTS
using System;
using System.IO;
using ChipArena.UnityConversion;
// Expedition zone boss music (Core/NativeZoneMusic.cs, owner 2026-10-17): Central Station and City Ironworks name their own boss
// tracks, each played only when its file is installed, else the Siren's song on its own loop map as before; Ward Zero, the hub
// and every other map keep the Siren's song; a zone track plays its intro edit and loop map on the shared boss clock
// (ZoneBossMusicChecks), a plain loop only without a map; Runtime/NativeAudio.cs routes the boss branch through it and keeps
// Astra's per-map normal-play tracks (PR #263).
internal static class ZoneMusicChecks
{
    static int count;
    static void Check(bool ok,string what){if(!ok)throw new Exception("Zone music: "+what);count++;}
    public static void Run(string root)
    {
        Check(NativeStageMusic.Theme("breakwater","old")=="breakwater-arena-duel","owner stage song: breakwater");
        Check(NativeStageMusic.Theme("harbor","old")=="harbor-container-terminal","owner stage song: harbor");
        Check(NativeStageMusic.Theme("skyframe","old")=="skyframe-rising-motif","owner stage song: skyframe");
        Check(NativeStageMusic.Theme("rig","old")=="rig-offshore","owner stage song: rig");
        Check(NativeStageMusic.Theme("polar","old")=="polar-pulse-de-la-mort","owner stage song: polar");
        Check(NativeStageMusic.Theme("backrooms-level-0","old")=="backrooms-fluorescent-loop","owner stage song: backrooms-level-0");
        Check(NativeStageMusic.Theme("training","old")=="training-steady-groove","owner stage song: training");
        Check(NativeStageMusic.Theme("expedition-backrooms-level-0","old")=="backrooms-fluorescent-loop","Expedition Level 0 uses its own track");
        Check(NativeStageMusic.PoolroomsTrack=="poolrooms-untitled","Poolrooms resource reserved without inventing a playable map");
        foreach(string map in new[]{"brickfall","siren","factory","cybercity","expedition-city","expedition-transit","expedition-foundry","unknown",null})Check(NativeStageMusic.Theme(map,"existing")=="existing","existing theme retained: "+map);
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/breakwater-arena-duel.mp3")),"packaged owner song: breakwater-arena-duel");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/harbor-container-terminal.mp3")),"packaged owner song: harbor-container-terminal");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/skyframe-rising-motif.mp3")),"packaged owner song: skyframe-rising-motif");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/rig-offshore.mp3")),"packaged owner song: rig-offshore");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/polar-pulse-de-la-mort.mp3")),"packaged owner song: polar-pulse-de-la-mort");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/backrooms-fluorescent-loop.mp3")),"packaged owner song: backrooms-fluorescent-loop");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/poolrooms-untitled.mp3")),"packaged owner song: poolrooms-untitled");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/training-steady-groove.mp3")),"packaged owner song: training-steady-groove");
        Func<string,bool> all=id=>true,none=id=>false;
        Check(NativeZoneMusic.ZoneBoss("expedition-transit")=="music/central-station-boss","Central Station names its boss track");
        Check(NativeZoneMusic.ZoneBoss("expedition-foundry")=="music/city-ironworks-boss"&&NativeZoneMusic.ZoneBoss("expedition-ironworks")=="music/city-ironworks-boss","City Ironworks names its boss track under the live id and the draft name");
        foreach(var map in new[]{"expedition-ward-zero","expedition-city","siren","factory","cybercity",null,""})
            Check(NativeZoneMusic.ZoneBoss(map)==null&&NativeZoneMusic.BossTrack(map,all)==NativeZoneMusic.SirenSong,"other maps keep the Siren's song: "+(map??"null"));
        Check(NativeZoneMusic.BossTrack("expedition-transit",all)=="music/central-station-boss"&&NativeZoneMusic.BossTrack("expedition-foundry",all)=="music/city-ironworks-boss","an installed zone track plays");
        Check(NativeZoneMusic.BossTrack("expedition-transit",none)==NativeZoneMusic.SirenSong&&NativeZoneMusic.BossTrack("expedition-foundry",null)==NativeZoneMusic.SirenSong,"a missing zone track falls back to the Siren's song");
        Check(NativeZoneMusic.BossTrack("expedition-transit",id=>id=="music/city-ironworks-boss")==NativeZoneMusic.SirenSong,"a zone never borrows the other zone's track");
        Check(NativeZoneMusic.LoopClock(0,90)==0&&NativeZoneMusic.LoopClock(-1,90)==0&&Math.Abs(NativeZoneMusic.LoopClock(12.5,90)-12.5)<1e-9,"the clock starts at the arrival");
        Check(Math.Abs(NativeZoneMusic.LoopClock(95,90)-5)<1e-9&&Math.Abs(NativeZoneMusic.LoopClock(270.25,90)-.25)<1e-9,"the track loops on the shared clock");
        Check(Math.Abs(NativeZoneMusic.LoopClock(37,0)-37)<1e-9,"an unknown length plays straight through");
        string audio=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeAudio.cs"));
        Check(audio.Contains("string boss=NativeZoneMusic.BossTrack(arena.MapId,id=>Clip(id)!=null);"),"the boss branch routes through NativeZoneMusic");
        Check(audio.Contains("if(boss==NativeZoneMusic.SirenSong)Track(boss,Music*.55f,hold,arena.BossTrack.Looped(arena.BossSongTime),arena,arena.BossSongAt);"),"the Siren's song keeps its intro/loop map and clock");
        Check(audio.Contains("double clip=song!=null?song.Clip(arena.BossSongTime):NativeZoneMusic.LoopClock(arena.BossSongTime,Clip(boss).length);")&&audio.Contains("Track(boss,Music*.55f,hold||clip<0,Math.Max(0,clip),arena,arena.BossSongAt);"),"a zone track plays its intro edit and loop map (a plain loop without one) on the shared boss clock");
        Check(audio.Contains("NativeStageMusic.Theme(arena.MapId,\"restricted-zone-pulse\")")&&audio.Contains("string theme=NativeStageMusic.Theme(arena.MapId,"),"PvE and PvP route owner music through the shared resolver");
        int boss=audio.IndexOf("else if(arena.BossSongTime>=0)",StringComparison.Ordinal),foundry=audio.IndexOf("Track(\"music/foundry-of-the-dead\"",StringComparison.Ordinal),transit=audio.IndexOf("Track(\"music/subway-arrival-chime\"",StringComparison.Ordinal);
        Check(boss>0&&foundry>boss&&transit>boss,"the boss branch still comes before the zones' normal-play tracks (PR #263)");
        Console.WriteLine("PASS zone music ("+count+" checks: Central Station and City Ironworks boss tracks when installed, Siren's song otherwise, shared-clock loop, NativeAudio routing)");
    }
}
#endif
