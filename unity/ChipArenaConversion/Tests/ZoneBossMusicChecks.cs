#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using ChipArena.UnityConversion;
// The zone boss songs' maps and the stages' reaction (Core/NativeZoneBossMusic.cs, NativeExpeditionStationMusic.cs,
// NativeIronworksMusic.cs; owner 2026-10-17: "sync their intro with the stage ... sync the stages to be affected by the music"):
// the maps are valid and belong to the committed MP3s, the intro edit lands each drop on the end of the simulation's boss intro
// hold and plays once before the bar-aligned loop, a missing file keeps the Siren's song and a quiet stage, and every stage
// reaction is a pure, allocation-free function of the shared boss clock that never touches a hazard and stays under the flash caps.
internal static class ZoneBossMusicChecks
{
    static int count;
    static void Check(bool ok,string what){if(!ok)throw new Exception("Zone boss music: "+what);count++;}
    static bool Near(double a,double b,double tol=1e-3)=>Math.Abs(a-b)<=tol;
    public static void Run(string root)
    {
        Root=root;string json=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/ZoneBossMusic.json"));var music=NativeZoneBossMusic.Parse(json);
        Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/ZoneBossMusic.json.meta")),"the map has its meta");
        // The simulation's boss intro hold (Runtime/NativePve.cs) is the hold every drop lands on.
        string pve=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativePve.cs"));
        Check(pve.Contains("cinematic==\"intro\"?9.0678f")&&Near(music.Hold,9.0678,1e-6),"the map's hold is the simulation's intro hold (9.0678 s); regenerate the map if it changes");
        Check(music.Songs.Length==2,"two zone songs");
        var station=music.Song("music/central-station-boss");var iron=music.Song("city-ironworks-boss");
        Check(station!=null&&iron!=null&&station.Map=="expedition-transit"&&iron.Map=="expedition-foundry","Station Chime of Doom on Central Station, Hydraulic Hell on City Ironworks");
        foreach(var song in music.Songs)Map(root,json,song);
        Routing(root,music);
        foreach(var song in music.Songs){Clock(song);Stage(song);}
        StationStage(station);IronworksStage(iron);
        Console.WriteLine("PASS zone boss music ("+count+" checks: beat maps, intro edit on the hold, bar-aligned loops, routing and fallback, pure stage reactions, hazards untouched, flash caps)");
    }

    // ---------------- the maps ----------------
    static void Map(string root,string json,NativeZoneBossSong s)
    {
        var t=s.Track;var b=t.beats;string id=s.Id;
        Check(b.Length>=200&&b[0]>=0&&b[b.Length-1]<=t.duration,id+": beats inside the file");
        bool rising=true;double minGap=9,maxGap=0;for(int i=1;i<b.Length;i++){double d=b[i]-b[i-1];rising&=d>0;minGap=Math.Min(minGap,d);maxGap=Math.Max(maxGap,d);}
        Check(rising&&maxGap<minGap*1.25,id+": beats strictly increasing with a steady (drifting, never jumping) period");
        double bpm=60*(b.Length-1)/(b[b.Length-1]-b[0]);
        Check(id=="central-station-boss"?bpm>170&&bpm<190:bpm>130&&bpm<150,id+": tempo as measured ("+bpm.ToString("F1")+" BPM)");
        Check(t.firstDownbeat>=0&&t.firstDownbeat<4,id+": the first downbeat is in the first bar");
        // Sections: contiguous from bar 1, unique, covering the loop.
        var seen=new HashSet<string>();bool contiguous=t.sections[0].fromBar==1;for(int i=0;i<t.sections.Length;i++){contiguous&=t.sections[i].toBar>=t.sections[i].fromBar&&(i==0||t.sections[i].fromBar==t.sections[i-1].toBar+1);seen.Add(t.sections[i].id);}
        Check(contiguous&&seen.Count==t.sections.Length,id+": sections contiguous from bar 1 with unique names");
        Check(t.loop.fromBar>=t.sections[0].fromBar&&t.loop.toBar<=t.sections[t.sections.Length-1].toBar,id+": the sections cover the loop");
        for(int bar=t.loop.fromBar;bar<=t.loop.toBar;bar++){int k=s.SectionAt(bar);Check(k>=0&&bar>=t.sections[k].fromBar&&bar<=t.sections[k].toBar,id+": bar "+bar+" of the loop has a section");}
        // The loop: bar-aligned, inside the file, long, starting at the drop (the intro never replays).
        Check(Near(t.loop.start,s.Downbeat(t.loop.fromBar),1e-4)&&Near(t.loop.end,s.Downbeat(t.loop.toBar+1),1e-4)&&t.loop.end<=t.duration&&t.loop.end-t.loop.start>45,id+": the loop runs from a downbeat to a downbeat");
        Check(t.introEnd<=t.loop.start&&Near(t.introEnd,s.Drop)&&Near(s.Drop,t.loop.start)&&Near(s.Downbeat(s.DropBar),s.Drop),id+": introEnd (the drop, a downbeat) is the loop's start: the intro is outside the loop");
        Check(t.loop.end<t.duration-1,id+": the outro stays outside the loop");
        // The intro edit lands the drop on the hold, splicing on bar lines after a short preroll.
        Check(Near(s.Preroll+s.Cut+s.Drop-s.Resume,s.Hold,2e-3)&&s.Preroll>=0&&s.Preroll<1.5,id+": preroll + opening + resumed run = the hold");
        bool cutOnBar=false,resumeOnBar=false;for(int bar=1;bar<60;bar++){if(Near(s.Downbeat(bar),s.Cut,2e-4))cutOnBar=true;if(Near(s.Downbeat(bar),s.Resume,2e-4))resumeOnBar=true;}
        Check(cutOnBar&&resumeOnBar&&s.Cut<s.Resume&&s.Resume<s.Drop,id+": the splice cuts and resumes on downbeats before the drop");
        // Accents.
        bool sorted=true,valid=true,dropAtDrop=false;for(int i=0;i<s.Accents.Length;i++){var a=s.Accents[i];sorted&=i==0||a.T>=s.Accents[i-1].T;valid&=a.T>=0&&a.T<=t.duration&&a.S>=0&&a.S<=1&&a.Kind<=NativeZoneBossSong.AccentDrop;if(a.Kind==NativeZoneBossSong.AccentDrop&&Near(a.T,s.Drop,.03))dropAtDrop=true;}
        Check(s.Accents.Length>0&&sorted&&valid&&dropAtDrop,id+": accents sorted and valid, a drop accent on the drop");
        // The committed file is the one the map was made from (owner's bytes, unchanged).
        var mp3=Path.Combine(root,"Assets/ChipArena/Resources/Audio/music/"+id+".mp3");Check(File.Exists(mp3)&&File.Exists(mp3+".meta"),id+": the MP3 and its meta are installed");
        var bytes=File.ReadAllBytes(mp3);string sha=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Check(json.Contains("\"sha256\": \""+sha+"\"")&&json.Contains("\"bytes\": "+bytes.Length),id+": the map names the committed MP3's SHA-256 and size");
    }

    // ---------------- routing and fallback ----------------
    static void Routing(string root,NativeZoneBossMusic music)
    {
        Func<string,bool> all=id=>true,none=id=>false;
        Check(music.ForMap("expedition-transit",all)?.Id=="central-station-boss","Central Station's boss plays Station Chime of Doom");
        Check(music.ForMap("expedition-foundry",all)?.Id=="city-ironworks-boss"&&music.ForMap("expedition-ironworks",all)?.Id=="city-ironworks-boss","City Ironworks' boss plays Hydraulic Hell (live id and draft name)");
        foreach(var map in new[]{"expedition-ward-zero","expedition-city","siren","factory",null,""})Check(music.ForMap(map,all)==null,"no zone song (the Siren's) on "+(map??"null"));
        Check(music.ForMap("expedition-transit",none)==null&&music.ForMap("expedition-foundry",none)==null&&NativeZoneMusic.BossTrack("expedition-transit",none)==NativeZoneMusic.SirenSong,"a missing file keeps the Siren's song and a quiet stage");
        Check(music.Song("music/black-siren-protocol")==null&&music.Song(null)==null,"the Siren's song has no zone map");
        string audio=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeAudio.cs"));
        Check(audio.Contains("var song=NativeZoneBossSongs.Music?.Song(boss);double clip=song!=null?song.Clip(arena.BossSongTime):NativeZoneMusic.LoopClock(arena.BossSongTime,Clip(boss).length);"),"NativeAudio plays a zone track on its intro edit and loop map, the plain loop only without a map");
        Check(audio.Contains("Track(boss,Music*.55f,hold||clip<0,Math.Max(0,clip),arena,arena.BossSongAt);"),"the preroll holds the track silent; the shared boss clock and epoch drive it");
        string stage=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeZoneBossStage.cs"));
        Check(stage.Contains("double song=arena.BossSongTime;if(!(song>=0))return default;")&&stage.Contains("s.Evaluate(song,KitFlash)"),"the stage reads the same boss clock and the settings' flash mode");
        string st=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeExpeditionStationPresentation.cs")),iw=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeIronworksPresentation.cs"));
        Check(st.Contains("stationState.Evaluate(view,now);stationState.Score(ZoneBossBeat(arena));NativeLighting.ZoneHouse(stationState.Music.House());")&&iw.Contains("iwMusic=ZoneBossBeat(arena);NativeLighting.ZoneHouse(iwMusic.House());")&&iw.Contains("iwFx.Music=iwMusic;"),"both stages take the song each frame (and give the house back when it stops)");
        string lighting=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeLighting.cs"));
        Check(lighting.Contains("public static void ZoneHouse(float scale)")&&lighting.Contains("arenaPicked=false;WardFieldOff();zoneHouse=1;")&&lighting.Contains("if(introActive||current==null||"),"the house scale leaves entrances alone and restarts from the look on Apply");
    }

    // ---------------- the clock ----------------
    static void Clock(NativeZoneBossSong s)
    {
        string id=s.Id;double l=s.LoopLength,start=s.Track.loop.start,end=s.Track.loop.end;
        Check(s.Clip(-1)==-1&&s.Clip(double.NaN)==-1&&s.Clip(double.PositiveInfinity)==-1&&(s.Preroll<=0||s.Clip(s.Preroll*.5)==-1),id+": silent before the arrival and through the preroll");
        Check(Near(s.Clip(s.Preroll),0,1e-9)&&Near(s.Clip(s.Preroll+s.Cut-1e-4),s.Cut-1e-4,1e-6),id+": the opening plays from the top");
        Check(Near(s.Clip(s.Preroll+s.Cut),s.Resume,1e-9),id+": the splice resumes on its bar");
        Check(Near(s.Clip(s.Hold),s.Drop,1e-6)&&Near(s.Clip(s.Hold-1e-3),s.Drop-1e-3,1e-6),id+": the drop sounds exactly when the hold ends");
        Check(Near(s.Clip(s.Hold+l-1e-3),end-1e-3,1e-6)&&Near(s.Clip(s.Hold+l+1e-4),start+1e-4,1e-6)&&Near(s.Clip(s.Hold+3*l+2.5),start+2.5,1e-6),id+": the loop wraps on its bars, on the shared clock");
        // The intro plays once: after the hold the clip never leaves the loop.
        bool inLoop=true;int openings=0;double last=-1;for(double t=0;t<s.Hold+4*l;t+=.05){double c=s.Clip(t);if(t>=s.Hold)inLoop&=c>=start&&c<end;if(c>=0&&c<.05&&last<0)openings++;last=c;}
        Check(inLoop&&openings==1,id+": the intro plays once, then only the loop");
        Check(Near(s.FirstBoss(.5),s.Preroll+.5,1e-9)&&Near(s.FirstBoss(s.Drop+3),s.Hold+3,1e-9)&&double.IsNaN(s.FirstBoss(s.Cut+.01))==(s.Cut+.01<s.Resume),id+": first-play times invert the clock");
    }

    // ---------------- the stage (both songs) ----------------
    static void Stage(NativeZoneBossSong s)
    {
        string id=s.Id;double l=s.LoopLength,until=s.Hold+3*l;
        var off=s.Evaluate(-1,NativeKitFlash.Full);var nan=s.Evaluate(double.NaN,NativeKitFlash.Full);
        Check(!off.On&&!nan.On&&off.Light()==1&&off.House()==1&&NativeIronworksKit.MusicHeat(off)==1&&NativeIronworksKit.MusicLamp(off)==1&&NativeIronworksKit.MusicField(off)==1&&NativeIronworksKit.MusicSway(off,true)==0,id+": before the boss arrives nothing changes");
        // Pure and deterministic: the same clock and mode give the same answer, also from a freshly parsed map.
        var again=NativeZoneBossMusic.Parse(File.ReadAllText(Path.Combine(Root,"Assets/ChipArena/Resources/ZoneBossMusic.json"))).Song(id);bool same=true;
        foreach(var mode in new[]{NativeKitFlash.Full,NativeKitFlash.Reduced,NativeKitFlash.Calm})for(double t=0;t<s.Hold+l+8;t+=.0731){var a=s.Evaluate(t,mode);var b=s.Evaluate(t,mode);var c=again.Evaluate(t,mode);same&=Same(a,b)&&Same(a,c);}
        Check(same,id+": the stage is a pure function of the boss clock and the flash mode");
        // Every peer at the same boss time sees the same bar: the beat position follows the clip.
        var at=s.Evaluate(s.Hold+.01,NativeKitFlash.Full);Check(at.On&&!at.Intro&&at.Bar==s.DropBar&&at.BeatInBar==1&&at.Flare>.5&&at.Cut==0,id+": the fight opens on the drop's downbeat with the flare");
        Check(s.Evaluate(s.Hold-.01,NativeKitFlash.Full).Intro&&s.Evaluate(s.Hold-.01,NativeKitFlash.Full).Steps==1,id+": the intro's steps are complete before the drop");
        // Flash caps: the kept events, and the channels themselves sampled at 240 Hz.
        foreach(var mode in new[]{NativeKitFlash.Full,NativeKitFlash.Reduced,NativeKitFlash.Calm})
        {
            int cap=mode==NativeKitFlash.Full?3:mode==NativeKitFlash.Reduced?2:0;var f=s.FlashTimes(mode,until);int worst=0;for(int i=0;i<f.Count;i++){int n=0;for(int j=i;j<f.Count&&f[j]<f[i]+1;j++)n++;worst=Math.Max(worst,n);}
            Check(worst<=cap&&(mode==NativeKitFlash.Calm?f.Count==0:f.Count>until/2),id+": "+mode+" keeps at most "+cap+" flash events a second ("+worst+")");
            // A flash onset: the house light 10% (Calm: 12%) above its lowest of the last 100 ms, counted once per rising edge.
            var rises=new List<double>();var lamp=new List<double>();var house=new List<double>();double prevP=0;var window=new Queue<(double t,double v,double h)>();bool rising=false,risingH=false;double jump=mode==NativeKitFlash.Calm?1.12:1.1;
            for(double t=0;t<until;t+=1/240.0)
            {
                var b=s.Evaluate(t,mode);if(b.Pulse-prevP>.15)rises.Add(t);prevP=b.Pulse;double light=b.Light(.25f),h=b.House();
                window.Enqueue((t,light,h));while(window.Peek().t<t-.1)window.Dequeue();double lo=9,loH=9;foreach(var w in window){lo=Math.Min(lo,w.v);loH=Math.Min(loH,w.h);}
                bool up=light>lo*jump;if(up&&!rising)lamp.Add(t);rising=up;bool upH=h>loH*jump;if(upH&&!risingH)house.Add(t);risingH=upH;
            }
            Check(MaxPerSecond(house)<=Math.Max(cap,0)&&(mode!=NativeKitFlash.Calm||house.Count==0)&&MaxPerSecond(house)<=1,id+": "+mode+" the house (ambient and key) never flashes on the beat: at most one jump a second, none in Calm ("+MaxPerSecond(house)+")");
            Check(MaxPerSecond(rises)<=cap,id+": "+mode+" pulse onsets stay under "+cap+" a second ("+MaxPerSecond(rises)+")");
            Check(MaxPerSecond(lamp)<=Math.Max(cap,0)&&(mode!=NativeKitFlash.Calm||lamp.Count==0),id+": "+mode+" house light flashes (a "+(mode==NativeKitFlash.Calm?"12":"10")+"% rise within 100 ms) stay at most "+cap+" a second ("+MaxPerSecond(lamp)+")");
        }
        // Allocation free per frame.
        for(int i=0;i<200;i++)s.Evaluate(i*.37,NativeKitFlash.Full);long before=GC.GetAllocatedBytesForCurrentThread();double sum=0;
        for(int i=0;i<4000;i++){var b=s.Evaluate(i*.0517,(NativeKitFlash)(i%3));sum+=b.Pulse+b.Light();}
        Check(GC.GetAllocatedBytesForCurrentThread()-before==0&&sum>0,id+": evaluating the stage allocates nothing");
    }
    static string Root;
    static int MaxPerSecond(List<double> t){int worst=0;for(int i=0;i<t.Count;i++){int n=0;for(int j=i;j<t.Count&&t[j]<t[i]+1;j++)n++;worst=Math.Max(worst,n);}return worst;}
    static bool Same(in NativeZoneBossBeat a,in NativeZoneBossBeat b)=>a.On==b.On&&a.Intro==b.Intro&&a.Signals==b.Signals&&a.Bar==b.Bar&&a.BeatInBar==b.BeatInBar&&a.Section==b.Section&&a.Phase==b.Phase&&a.BarPhase==b.BarPhase&&a.Pulse==b.Pulse&&a.Down==b.Down&&a.Cut==b.Cut&&a.Flare==b.Flare&&a.Steps==b.Steps&&a.PulseKind==b.PulseKind&&a.Mood.Light==b.Mood.Light&&a.Mood.Tint==b.Mood.Tint&&a.Mood.Heat==b.Mood.Heat&&a.Song.Equals(b.Song)&&a.Since.Equals(b.Since)&&a.SinceDown.Equals(b.SinceDown)&&a.SinceDrop.Equals(b.SinceDrop);

    // ---------------- Central Station ----------------
    static void StationStage(NativeZoneBossSong s)
    {
        var steps=s.StepTimes;Check(steps.Length==8&&steps[0]>=s.Preroll,"Station: the opening's eight PA chimes step the lamps on");
        float west=-NativeExpeditionStation.ShellX+1,east=NativeExpeditionStation.ShellX-1;
        var arrive=s.Evaluate(s.Preroll*.8,NativeKitFlash.Full);
        Check(arrive.Cut>.3f&&NativeExpeditionStationKit.LampGate(arrive,west)==NativeExpeditionStationKit.LampWaiting&&!arrive.Signals,"Station: the power sags at the arrival, lamps waiting, signals dark");
        var first=s.Evaluate(steps[0]+.02,NativeKitFlash.Full);Check(NativeExpeditionStationKit.LampGate(first,west)==1&&NativeExpeditionStationKit.LampGate(first,east)==NativeExpeditionStationKit.LampWaiting,"Station: the first chime lights the west lamps only");
        var all=s.Evaluate(steps[7]+.02,NativeKitFlash.Full);Check(NativeExpeditionStationKit.LampGate(all,east)==1,"Station: the eighth chime lights the east lamps");
        Check(!s.Evaluate(s.FirstSlam-.02,NativeKitFlash.Full).Signals&&s.Evaluate(s.FirstSlam+.01,NativeKitFlash.Full).Signals,"Station: the signals' red lamps come on at the intro's first slam");
        // The cuts: the bass drops out and the house goes dark until the slam or the drop.
        double cut1=double.NaN,cut2=double.NaN;foreach(var a in s.Accents)if(a.Kind==NativeZoneBossSong.AccentCut){double b=s.FirstBoss(a.T);if(b<s.Hold){if(double.IsNaN(cut1))cut1=b;else cut2=b;}}
        Check(!double.IsNaN(cut1)&&!double.IsNaN(cut2)&&s.Evaluate(cut1+.2,NativeKitFlash.Full).Cut>.95f&&s.Evaluate(cut2+.2,NativeKitFlash.Full).Cut>.95f&&s.Evaluate(cut1+.2,NativeKitFlash.Full).Light()<.35f,"Station: the lights cut twice in the intro where the bass cuts");
        Check(s.Evaluate(s.Hold+.05,NativeKitFlash.Full).Light()>1&&s.Evaluate(s.Hold+.05,NativeKitFlash.Full).Light()>4*s.Evaluate(s.Hold-.05,NativeKitFlash.Full).Light(),"Station: the drop flares the lights back from the cut as the fight begins");
        // The board: LAST CALL through the intro, its row lit on the song's pulses; the boss flag cancels it at the drop.
        var st=new NativeStationState();st.Evaluate(new NativeStationView(),10);Check(!st.Music.On,"Station: Evaluate clears the song");
        st.Score(s.Evaluate(steps[2]+.01,NativeKitFlash.Full));Check(st.Row0==NativeStationState.Row0LastCall&&st.BoardRowLit(0),"Station: the board reads LAST CALL on a chime");
        st.Evaluate(new NativeStationView(),10);st.Score(s.Evaluate(steps[2]+.3,NativeKitFlash.Full));Check(st.Row0==NativeStationState.Row0LastCall&&!st.BoardRowLit(0),"Station: LAST CALL goes dark between the chimes");
        var bossView=new NativeStationView{Boss=true};st.Evaluate(bossView,10);st.Score(s.Evaluate(s.Hold+1,NativeKitFlash.Full));Check(st.Row0==NativeStationState.Row0Cancelled&&st.Title==NativeStationState.TitleCancelled,"Station: the boss flag still cancels every service in the fight");
        st.Evaluate(new NativeStationView(),10);st.Score(s.Evaluate(s.Hold+1,NativeKitFlash.Full));Check(st.Row0==NativeStationState.Row0Delayed,"Station: the board's LAST CALL is the intro's only");
        Check(NativeExpeditionStation.TickerAtBar(1)==0&&NativeExpeditionStation.TickerAtBar(3)==1&&NativeExpeditionStation.TickerAtBar(2)==0,"Station: the ticker pages every two bars with the song");
        // Hazards are never touched: their parts and lights read the same with the song on.
        var beat=s.Evaluate(s.Hold+20.3,NativeKitFlash.Full);Check(beat.On,"Station: a fight-time beat");
        foreach(byte d in new[]{NativeExpeditionStationKit.DriveDanger,NativeExpeditionStationKit.DriveBeacon,NativeExpeditionStationKit.DriveRail,NativeExpeditionStationKit.DriveFlood,NativeExpeditionStationKit.DriveCabinet,NativeExpeditionStationKit.DriveLever,NativeExpeditionStationKit.DriveExpress,NativeExpeditionStationKit.DriveMouth,NativeExpeditionStationKit.DriveDiagram,NativeExpeditionStationKit.DriveFire,NativeExpeditionStationKit.DriveDead})
        {var p=new NativeStationPart{Drive=d,Gain=2,Role=NativeExpeditionStationKit.RoleLamp};Check(!NativeExpeditionStationKit.MusicReactive(p)&&NativeExpeditionStationKit.MusicGain(p,beat,0)==1,"Station: hazard drive "+d+" ignores the song");}
        Check(NativeExpeditionStationKit.MusicGain(new NativeStationPart{Drive=NativeExpeditionStationKit.DriveSignal,Tag=1,Gain=3},beat,0,NativeStationState.SignalGreen)==1,"Station: the green aspect (the express's warning) ignores the song");
        Check(NativeExpeditionStationKit.MusicGain(new NativeStationPart{Drive=NativeExpeditionStationKit.DriveSignal,Tag=0,Gain=3},beat,0,NativeStationState.SignalGreen)==1,"Station: a red lamp only kicks while the signal shows red");
        foreach(byte k in new[]{NativeExpeditionStationKit.GlowBeacon,NativeExpeditionStationKit.GlowCabinet,NativeExpeditionStationKit.GlowWater,NativeExpeditionStationKit.GlowFire,NativeExpeditionStationKit.GlowGenerator,NativeExpeditionStationLights.KindHeadlight,NativeExpeditionStationLights.KindBand})
        {float r=1,g=.5f,b=.25f;NativeExpeditionStationKit.MusicLight(k,null,0,beat,NativeStationState.SignalRed,ref r,ref g,ref b);Check(r==1&&g==.5f&&b==.25f,"Station: light kind "+k+" ignores the song");}
        {float r=1,g=1,b=1;NativeExpeditionStationKit.MusicLight(NativeExpeditionStationKit.GlowSignal,null,0,beat,NativeStationState.SignalGreen,ref r,ref g,ref b);float r2=1,g2=1,b2=1;NativeExpeditionStationKit.MusicLight(NativeExpeditionStationLights.KindLook,"tunnel-w-green",0,beat,NativeStationState.SignalGreen,ref r2,ref g2,ref b2);Check(r==1&&g==1&&b==1&&r2==1&&g2==1&&b2==1,"Station: the green signal light and the tunnel's green ignore the song");}
        var lamp=new NativeStationPart{Drive=NativeExpeditionStationKit.DriveSteady,Gain=2.4f,Role=NativeExpeditionStationKit.RoleLamp};Check(NativeExpeditionStationKit.MusicReactive(lamp)&&NativeExpeditionStationKit.MusicGain(lamp,default,0)==1,"Station: a lamp follows the song, and nothing without it");
        // The kit's own parts: every hazard part reads the same with or without the song.
        var stHaz=new NativeStationState();stHaz.Evaluate(new NativeStationView{TrainAt=10.5,FloodLive=true},10);
        foreach(byte d in new[]{NativeExpeditionStationKit.DriveDanger,NativeExpeditionStationKit.DriveBeacon,NativeExpeditionStationKit.DriveRail,NativeExpeditionStationKit.DriveFlood,NativeExpeditionStationKit.DriveMouth})
        {var p=new NativeStationPart{Drive=d,Gain=2,Color=0xFF8040};NativeExpeditionStationKit.Drive(p,stHaz,10,out float r0,out _,out _,out _);stHaz.Score(beat);NativeExpeditionStationKit.Drive(p,stHaz,10,out float r1,out _,out _,out _);Check(r0==r1,"Station: hazard part "+d+" draws the same with the song");stHaz.Score(default);}
        Check(stHaz.Express==NativeStationState.ExpressWarn&&stHaz.Row4==NativeStationState.Row4Approaching,"Station: the express's warning shows as before");
        // The strips chase a quarter a beat; Calm keeps them steady.
        var strip=new NativeStationPart{Drive=NativeExpeditionStationKit.DriveStrip,Gain=1.7f};var chase=s.Evaluate(s.Hold+20+.01,NativeKitFlash.Full);float lit=0,dark=9;
        for(int q=0;q<16;q++){float g=NativeExpeditionStationKit.MusicGain(strip,chase,-NativeExpeditionStation.ShellX+q*6+3);lit=Math.Max(lit,g);dark=Math.Min(dark,g);}
        Check(lit>dark*1.5f&&NativeExpeditionStationKit.MusicGain(strip,s.Evaluate(s.Hold+20+.01,NativeKitFlash.Calm),3)==1,"Station: the edge strips chase on the beat (steady in Calm)");
    }

    // ---------------- City Ironworks ----------------
    static void IronworksStage(NativeZoneBossSong s)
    {
        var steps=s.StepTimes;Check(steps.Length>=3,"Ironworks: the opening's bars stoke the furnaces");
        float h0=NativeIronworksKit.MusicHeat(s.Evaluate(steps[0]+.3,NativeKitFlash.Calm)),h1=NativeIronworksKit.MusicHeat(s.Evaluate(steps[steps.Length-1]+.3,NativeKitFlash.Calm));
        Check(h1>h0*1.15f,"Ironworks: the furnaces stoke up bar by bar through the intro");
        var drop=s.Evaluate(s.Hold+.05,NativeKitFlash.Full);Check(NativeIronworksKit.MusicHeat(drop)>1.3f&&NativeIronworksKit.MusicLamp(drop)>1.15f&&drop.SinceDrop<.1,"Ironworks: the drop surges the furnaces and lamps");
        // The drop's shower and the downbeat steam (effects), none before the boss.
        var fx=new NativeIronworksSceneFx();var scene=new NativeIronFxScene{Pipes=new[]{-40f,13.3f,27.45f},Hoods=new[]{0f,17.4f,27.2f}};var views=new IronworksHazardView[NativeIronworksHazards.Count];
        fx.Music=default;fx.Build(scene,views,100,0,6,-20,1,0,0,0,1,0,1,false);int sparks0=fx.Sparks,steam0=fx.Steam;
        fx.Music=s.Evaluate(s.Hold+.6,NativeKitFlash.Full);fx.Build(scene,views,100,0,6,-20,1,0,0,0,1,0,1,false);int sparks1=fx.Sparks,steam1=fx.Steam;
        fx.Music=s.Evaluate(s.Hold+.6,NativeKitFlash.Calm);fx.Build(scene,views,100,0,6,-20,1,0,0,0,1,0,1,false);int calmSparks=fx.Sparks,calmSteam=fx.Steam;
        Check(sparks1>sparks0+60&&steam1>steam0&&calmSparks<sparks0+30&&calmSteam==steam0,"Ironworks: the drop rains sparks and puffs steam (no shower or puffs in Calm) ("+sparks0+"/"+sparks1+"/"+calmSparks+" sparks, "+steam0+"/"+steam1+" steam)");
        int telegraphs(NativeZoneBossBeat m){var v=new IronworksHazardView[NativeIronworksHazards.Count];NativeIronworksHazards.Evaluate(new IronworksHazardInput{Running=true,Boss=true,Wave=3,Clock=3.5},v);fx.Music=m;fx.Build(scene,v,100,0,6,-20,1,0,0,0,1,0,1,false);return fx.Telegraphs;}
        Check(telegraphs(default)>0&&telegraphs(drop)==telegraphs(default),"Ironworks: the hazard telegraphs are the same with the song");
        // Hazards never move or light to the music.
        var hv=new IronworksHazardView[NativeIronworksHazards.Count];NativeIronworksHazards.Evaluate(new IronworksHazardInput{Running=true,Boss=true,Wave=3,Clock=17.2},hv);var beat=s.Evaluate(s.Hold+30.2,NativeKitFlash.Full);
        foreach(int f in new[]{NativeIronworksKit.FrameRam,NativeIronworksKit.FrameSlab,NativeIronworksKit.FrameShear,NativeIronworksKit.FramePusher,NativeIronworksKit.FrameSlagPot})
        {NativeIronworksKit.FramePose(f,hv,40,out float a,out float b,out float c,out float d,out float e,out float g,out float h);NativeIronworksKit.FramePose(f,hv,40,out float a2,out float b2,out float c2,out float d2,out float e2,out float g2,out float h2,beat);Check(a==a2&&b==b2&&c==c2&&d==d2&&e==e2&&g==g2&&h==h2,"Ironworks: hazard frame "+f+" never moves to the music");}
        Check(hv[NativeIronworksHazards.Ladle].Parked,"Ironworks: the ladle parks in the boss encounter");
        var unparked=new IronworksHazardView[NativeIronworksHazards.Count];NativeIronworksHazards.Evaluate(new IronworksHazardInput{Running=true,Wave=3,Clock=17.2},unparked);
        NativeIronworksKit.FramePose(NativeIronworksKit.FrameLadle,unparked,40,out _,out _,out _,out float lx,out float ly,out float lz,out float lw);NativeIronworksKit.FramePose(NativeIronworksKit.FrameLadle,unparked,40,out _,out _,out _,out float lx2,out float ly2,out float lz2,out float lw2,beat);
        Check(lx==lx2&&ly==ly2&&lz==lz2&&lw==lw2,"Ironworks: a ladle that can pour never rocks to the music");
        bool rocks=false;for(double t=0;t<2;t+=.1){NativeIronworksKit.FramePose(NativeIronworksKit.FrameLadle,hv,40,out _,out _,out _,out float x0,out _,out _,out _);NativeIronworksKit.FramePose(NativeIronworksKit.FrameLadle,hv,40,out _,out _,out _,out float x1,out _,out _,out _,s.Evaluate(s.Hold+30+t,NativeKitFlash.Full));if(Math.Abs(x0-x1)>1e-4)rocks=true;}
        Check(rocks,"Ironworks: the parked ladle rocks on its hook with the bar");
        foreach(byte d in new[]{NativeIronworksKit.DriveBeacon,NativeIronworksKit.DriveWarnLamp,NativeIronworksKit.DriveHazardGlow,NativeIronworksKit.DriveTap,NativeIronworksKit.DriveLed})
        {var p=new NativeIronPart{Drive=d,Gain=2,Color=0xFF8040,Hazard=(short)NativeIronworksHazards.R2,Role=NativeIronworksKit.RoleLamp};NativeIronworksKit.Drive(p,hv,40,out float r0,out _,out _,out _);NativeIronworksKit.Drive(p,hv,40,out float r1,out _,out _,out _,beat);Check(!NativeIronworksKit.MusicReactive(p)&&r0==r1,"Ironworks: hazard drive "+d+" ignores the song");}
        foreach(byte k in new[]{NativeIronworksKit.GlowTaphole,NativeIronworksKit.GlowRunner,NativeIronworksKit.GlowPour,NativeIronworksKit.GlowMold,NativeIronworksKit.GlowReheat,NativeIronworksKit.GlowSlab,NativeIronworksKit.GlowBillet,NativeIronworksKit.GlowSlag,NativeIronworksKit.GlowBeacon})
        {var c=new NativeIronLight{Kind=k,Hazard=(short)NativeIronworksHazards.R2,R=1,G=.5f,B=.2f,Range=8};Check(NativeIronworksKit.MusicLight(k,beat)==1&&NativeIronworksLights.Level(c,hv,40)==NativeIronworksLights.Level(c,hv,40,beat),"Ironworks: hazard light kind "+k+" ignores the song");}
        var lv0=new float[NativeIronworksField.Channels];var lv1=new float[NativeIronworksField.Channels];NativeIronworksField.Levels(hv,40,lv0);NativeIronworksField.Levels(hv,40,lv1,beat);
        Check(lv0[1]==lv1[1]&&lv0[2]==lv1[2]&&lv0[3]==lv1[3]&&lv0[0]!=lv1[0],"Ironworks: the runners' light channels ignore the song; the steady glow follows it");
        // Molten hazard metal never follows the music; the furnace pools do.
        var runner=new NativeMoltenPiece{Drive=NativeIronworksKit.MDriveRunner,Hazard=(short)NativeIronworksHazards.R2,HeatA=.9f,HeatB=.7f,Crust=.3f};
        NativeIronworksKit.MoltenState(runner,hv,40,out float ra,out _,out _,out _,out _,out _);NativeIronworksKit.MoltenState(runner,hv,40,out float rb,out _,out _,out _,out _,out _,beat);Check(ra==rb,"Ironworks: a runner's metal ignores the song");
        var pool=new NativeMoltenPiece{Drive=NativeIronworksKit.MDriveFurnace,Hazard=-1,HeatA=.6f,HeatB=.5f};NativeIronworksKit.MoltenState(pool,hv,40,out float pa,out _,out _,out _,out _,out _);NativeIronworksKit.MoltenState(pool,hv,40,out float pb,out _,out _,out _,out _,out _,drop);
        Check(pb>pa&&pb<=1,"Ironworks: a furnace pool flares with the drop (capped at white heat)");
    }
}
#endif
