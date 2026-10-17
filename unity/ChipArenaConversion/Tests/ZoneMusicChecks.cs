#if ARENA_TESTS
using System;
using System.IO;
using ChipArena.UnityConversion;
// Expedition zone boss music (Core/NativeZoneMusic.cs, owner 2026-10-17): Central Station and City Ironworks name their own boss
// tracks, each played only when its file is installed, else the Siren's song on its own loop map as before; Ward Zero, the hub
// and every other map keep the Siren's song; a zone track loops on the shared boss clock; Runtime/NativeAudio.cs routes the boss
// branch through it and keeps Astra's per-map normal-play tracks (PR #263).
internal static class ZoneMusicChecks
{
    static int count;
    static void Check(bool ok,string what){if(!ok)throw new Exception("Zone music: "+what);count++;}
    public static void Run(string root)
    {
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
        Check(audio.Contains("else Track(boss,Music*.55f,hold,NativeZoneMusic.LoopClock(arena.BossSongTime,Clip(boss).length),arena,arena.BossSongAt);"),"a zone track plays on the shared boss clock");
        int boss=audio.IndexOf("else if(arena.BossSongTime>=0)",StringComparison.Ordinal),foundry=audio.IndexOf("Track(\"music/foundry-of-the-dead\"",StringComparison.Ordinal),transit=audio.IndexOf("Track(\"music/subway-arrival-chime\"",StringComparison.Ordinal);
        Check(boss>0&&foundry>boss&&transit>boss,"the boss branch still comes before the zones' normal-play tracks (PR #263)");
        Console.WriteLine("PASS zone music ("+count+" checks: Central Station and City Ironworks boss tracks when installed, Siren's song otherwise, shared-clock loop, NativeAudio routing)");
    }
}
#endif
