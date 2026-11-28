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
        Check(NativeStageMusic.Theme("breakwater","old")=="breakwater-theme","owner stage song: breakwater (2026-11-11, the remodel's own)");
        Check(NativeStageMusic.Theme("harbor","old")=="harbor-theme","owner stage song: harbor (2026-11-11, the remodel's own)");
        Check(NativeStageMusic.Theme("skyframe","old")=="skyframe-theme","owner stage song: skyframe (2026-11-11, the remodel's own)");
        Check(NativeStageMusic.Theme("rig","old")=="rig-theme","owner stage song: rig (2026-11-11, the remodel's own)");
        Check(NativeStageMusic.Theme("polar","old")=="polar-theme","owner stage song: polar (2026-11-11, the remodel's own)");
        Check(NativeStageMusic.Theme("backrooms-level-0","old")=="backrooms-fluorescent-loop","owner stage song: backrooms-level-0");
        Check(NativeStageMusic.Theme("training","old")=="training-steady-groove","owner stage song: training");
        Check(NativeStageMusic.Theme("causeway","placeholder")=="causeway-theme","owner stage song: causeway");
        Check(NativeStageMusic.Theme("club","placeholder")=="club-theme","owner stage song: club");
        Check(NativeStageMusic.Theme("emporium","placeholder")=="emporium-theme","owner stage song: emporium");
        Check(NativeStageMusic.Theme("sanctum","placeholder")=="sanctum-theme","owner stage song: sanctum");
        Check(NativeStageMusic.Theme("brickfall","old")=="brickfall-theme"&&NativeStageMusic.Theme("factory","old")=="factory-theme"&&NativeStageMusic.Theme("cybercity","old")=="cybercity-theme","owner stage songs: Brickfall, the Factory, Cyber City (2026-11-11)");
        Check(NativeStageMusic.Theme("siren","restricted-zone-pulse")=="wardzero-theme"&&NativeStageMusic.Theme("expedition-ward-zero","restricted-zone-pulse")=="wardzero-theme","owner stage song: Ward Zero on both records, PvP and PvE (2026-11-11)");
        Check(NativeStageMusic.Theme("expedition-backrooms-level-0","old")=="backrooms-fluorescent-loop","Expedition Level 0 uses its own track");
        Check(NativeStageMusic.PoolroomsTrack=="poolrooms-untitled","Poolrooms resource reserved without inventing a playable map");
        foreach(string map in new[]{"expedition-city","expedition-transit","expedition-foundry","unknown",null})Check(NativeStageMusic.Theme(map,"existing")=="existing","existing theme retained: "+map);
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/breakwater-arena-duel.mp3")),"packaged owner song: breakwater-arena-duel");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/harbor-container-terminal.mp3")),"packaged owner song: harbor-container-terminal");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/skyframe-rising-motif.mp3")),"packaged owner song: skyframe-rising-motif");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/rig-offshore.mp3")),"packaged owner song: rig-offshore");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/polar-pulse-de-la-mort.mp3")),"packaged owner song: polar-pulse-de-la-mort");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/backrooms-fluorescent-loop.mp3")),"packaged owner song: backrooms-fluorescent-loop");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/poolrooms-untitled.mp3")),"packaged owner song: poolrooms-untitled");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/training-steady-groove.mp3")),"packaged owner song: training-steady-groove");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/causeway-theme.mp3")),"packaged owner song: causeway-theme");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/club-theme.mp3")),"packaged owner song: club-theme");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/emporium-theme.mp3")),"packaged owner song: emporium-theme");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/sanctum-theme.mp3")),"packaged owner song: sanctum-theme");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/harbor-theme.mp3")),"packaged owner song: harbor-theme");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/rig-theme.mp3")),"packaged owner song: rig-theme");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/polar-theme.mp3")),"packaged owner song: polar-theme");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/skyframe-theme.mp3")),"packaged owner song: skyframe-theme");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/breakwater-theme.mp3")),"packaged owner song: breakwater-theme");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/brickfall-theme.mp3")),"packaged owner song: brickfall-theme");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/wardzero-theme.mp3")),"packaged owner song: wardzero-theme");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/factory-theme.mp3")),"packaged owner song: factory-theme");
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/cybercity-theme.mp3")),"packaged owner song: cybercity-theme");
        Func<string,bool> all=id=>true,none=id=>false;
        Check(NativeZoneMusic.ZoneBoss("expedition-transit")=="music/central-station-boss","Central Station names its boss track");
        Check(NativeZoneMusic.ZoneBoss("expedition-foundry")=="music/city-ironworks-boss"&&NativeZoneMusic.ZoneBoss("expedition-ironworks")=="music/city-ironworks-boss","City Ironworks names its boss track under the live id and the draft name");
        Check(NativeZoneMusic.ZoneBoss("expedition-backrooms-level-0")=="music/level0-boss"&&NativeZoneMusic.ZoneBoss("expedition-backrooms-poolrooms")=="music/poolrooms-boss","the Backrooms' bosses each have their own boss song, not the stage's (owner 2026-11-05)");
        Check(NativeZoneMusic.BossTrack("expedition-backrooms-level-0",none)==NativeZoneMusic.SirenSong&&NativeZoneMusic.BossTrack("expedition-backrooms-poolrooms",none)==NativeZoneMusic.SirenSong,"until its file is installed, a Backrooms boss plays the Siren's song");
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
