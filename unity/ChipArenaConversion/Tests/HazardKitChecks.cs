#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The reusable hazard kit (docs/HAZARD-KIT.md; Core NativeHazardKit*, Runtime NativeHazardKit*): the placements file's schema
    // and validation, deterministic state timelines (event, schedule, always, hazard follow, re-fire, idempotent fires), the
    // photosensitivity caps (at most 3 flashes a second, 2 in Reduced, none in Calm; free-running and beat-locked), the music
    // tie-in (beat-locked blasts and turns, the klaxon variant fit, the trip reaction), every audio key in the bank contract with
    // a built-in fallback, the light budget and merge, turret and saw timelines, the gallery, the Ward Zero placement, no
    // per-frame allocation, and that the existing stage hazards are untouched (the kit only reads them).
    internal static class HazardKitChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Hazard kit: "+what);count++;}
        private static readonly JsonSerializerOptions Options=new JsonSerializerOptions{IncludeFields=true};
        public static void Run(string root)
        {
            count=0;var R=new Func<string,string>(f=>File.ReadAllText(Path.Combine(root,f)));
            var set=JsonSerializer.Deserialize<KitSet>(R("Assets/Conversion/Resources/HazardKit.json"),Options);NativeHazardKit.Validate(set);Check(true,"the placements validate");
            var maps=JsonSerializer.Deserialize<UnityMapContent>(R("Assets/Conversion/Resources/UnityMaps.json"),Options);var siren=maps.maps.First(m=>m.id=="siren");var instance=maps.maps.First(m=>m.id=="expedition-ward-zero");
            var ward=NativeHazardKit.For(set,siren.id,siren.look);var inst=NativeHazardKit.For(set,instance.id,instance.look);
            Check(ward.Length==30&&inst.Length==30&&siren.look=="quarantine"&&instance.look=="quarantine","Ward Zero and its Expedition instance each place the 30 alarms ("+ward.Length+", "+inst.Length+")");
            Check(NativeHazardKit.For(set,"factory","factory-first-shift").Length==0&&NativeHazardKit.For(set,"cybercity","neon-rain").Length==0,"other maps place nothing");
            Check(ward.All(r=>NativeHazardKit.Alarm(r.kind)),"the Ward set is alarms only (no turret or saw authority exists yet)");
            Placement(ward,siren,R);
            Schema();Timelines();Flashes(R);Music(R);Audio(root,R);Lights();Turret();Saws();Gallery();Allocation(ward);Untouched(root,R,maps);Hooks(R);
            Console.WriteLine("PASS hazard kit ("+count+" checks: placements schema and validation, Ward Zero and its instance 30 alarms tied to shutters, breaches, waves, the reveal and lockdowns; deterministic timelines; flashes <=3 Hz, <=2 Hz reduced, none calm, free and beat-locked; beat-locked blasts and turns, klaxon fit, trip reaction; 19 keys with fallbacks; light budget; turret and saw timelines; gallery; no per-frame allocation; stage hazards untouched)");
        }
        private static void Placement(KitRecord[] ward,ContentMap siren,Func<string,string> R)
        {
            var b=siren.bounds;var shutters=siren.hazards.Where(h=>h.kind=="shutter").Select(h=>h.id).ToHashSet();
            foreach(var r in ward)Check(r.pos.x>=b.minX-.01f&&r.pos.x<=b.maxX+.01f&&r.pos.z>=b.minZ-.01f&&r.pos.z<=b.maxZ+.01f&&r.pos.y>0&&r.pos.y<b.ceiling,r.id+" inside the map");
            var lock_=ward.Where(r=>r.look=="lockdown").ToArray();Check(lock_.Length==8&&lock_.All(r=>shutters.Contains(r.trigger.hazard)&&r.trigger.events.Contains("door-seal")&&r.trigger.events.Contains("dev-trigger")),"lockdown pieces follow the four shutters and answer a door seal and a DEV trigger");
            Check(shutters.Count==4&&shutters.All(id=>lock_.Count(r=>r.trigger.hazard==id)==2),"a beacon and a sign per shutter");
            using var pve=JsonDocument.Parse(R("Assets/Conversion/Resources/PveContent.json"));var breaches=pve.RootElement.GetProperty("entries").EnumerateArray().Where(e=>e.GetProperty("kind").GetString()=="breach").Select(e=>{var c=e.GetProperty("center");return new V3((float)c.GetProperty("x").GetDouble(),(float)c.GetProperty("y").GetDouble(),(float)c.GetProperty("z").GetDouble());}).ToArray();
            Check(breaches.Length==8,"eight breach entries");
            foreach(var e in breaches){var near=ward.Where(r=>r.kind=="alarm-strobe"&&r.trigger.events.Contains("breach")&&(r.pos-e).Length<=r.trigger.radius).ToArray();Check(near.Length==1,"exactly one breach strobe answers the breach at "+e.x+","+e.y+","+e.z);}
            Check(ward.Count(r=>r.kind=="alarm-klaxon"&&r.trigger.events.Contains("wave-start")&&r.trigger.events.Contains("boss-reveal")&&r.music=="beat")==4,"four beat-locked klaxons sound a wave, a breach, the reveal and the round start");
            Check(ward.Count(r=>r.look=="evacuation"&&r.trigger.events.Contains("boss-reveal"))==6,"six evacuation pieces light at the boss's reveal");
            Check(ward.Count(r=>r.react)>=3&&ward.Count(r=>r.music=="beat")>=15,"the music reacts to the big trips; most alarms keep the boss song's beat");
        }
        private static void Schema()
        {
            KitRecord Good()=>new KitRecord{id="a",kind="alarm-beacon",look="lockdown",mount="wall",trigger=new KitTrigger{mode="event",events=new[]{"breach"}}};
            void Reject(Action<KitRecord> f,string why){var r=Good();f(r);bool threw=false;try{NativeHazardKit.Validate(new[]{r});}catch(InvalidOperationException){threw=true;}Check(threw,"rejects "+why);}
            NativeHazardKit.Validate(new[]{Good()});
            Reject(r=>r.kind="trapdoor","an unknown kind");Reject(r=>r.look="disco","an unknown alarm look");Reject(r=>r.mount="sky","an unknown mount");Reject(r=>r.music="loud","an unknown music mode");
            Reject(r=>r.trigger.events=new[]{"party"},"an unknown event");Reject(r=>r.trigger.mode="sometimes","an unknown trigger mode");Reject(r=>{r.trigger.events=null;},"an event trigger with no events or hazard");
            Reject(r=>{r.trigger=new KitTrigger{mode="schedule",period=3};},"a schedule shorter than its warn + active + cooldown");Reject(r=>{r.pos=new V3(float.NaN,0,0);},"a non-finite place");
            Reject(r=>{r.kind="alarm-sign";r.text="EXIT 9";},"sign text beyond A-Z");Reject(r=>{r.kind="saw";r.variant="spinning-top";},"an unknown saw variant");Reject(r=>{r.kind="turret";r.range=500;},"a 500 m turret");
            bool dup=false;try{NativeHazardKit.Validate(new[]{Good(),Good()});}catch(InvalidOperationException){dup=true;}Check(dup,"rejects duplicate ids");
            bool schema=false;try{NativeHazardKit.Validate(new KitSet{schema=2,placements=new KitPlacement[0]});}catch(InvalidOperationException){schema=true;}Check(schema,"rejects another schema");
        }
        private static void Timelines()
        {
            var r=new KitRecord{id="t",kind="alarm-beacon",trigger=new KitTrigger{mode="event",events=new[]{"wave-start"},warn=1,active=4,cooldown=2}};
            NativeKitState S(double now,double fired)=>NativeHazardKitClock.Phase(r,now,fired).State;
            Check(S(5,double.NaN)==NativeKitState.Idle&&S(9.9,10)==NativeKitState.Idle&&S(10,10)==NativeKitState.Arming&&S(10.99,10)==NativeKitState.Arming&&S(11.01,10)==NativeKitState.Active&&S(14.99,10)==NativeKitState.Active&&S(15.01,10)==NativeKitState.Cooldown&&S(17.01,10)==NativeKitState.Idle,"event: idle, arming 1 s, active 4 s, cooldown 2 s, idle");
            var a=NativeHazardKitClock.Phase(r,12.5,10);var b=NativeHazardKitClock.Phase(r,12.5,10);Check(a.State==b.State&&a.Since==b.Since&&a.Until==b.Until&&a.Level==b.Level&&Math.Abs(a.Since-1.5)<1e-9&&Math.Abs(a.Until-2.5)<1e-9,"the same inputs give the same phase");
            var sch=new KitRecord{id="s",kind="saw",variant="rail",trigger=new KitTrigger{mode="schedule",first=2,period=10,offset=1,warn=1,active=3,cooldown=1}};
            Check(NativeHazardKitClock.Phase(sch,2.9,double.NaN).State==NativeKitState.Idle&&NativeHazardKitClock.Phase(sch,3.5,double.NaN).State==NativeKitState.Arming&&NativeHazardKitClock.Phase(sch,13.5,double.NaN).State==NativeKitState.Arming&&NativeHazardKitClock.Phase(sch,13.5,double.NaN).Cycle==1&&NativeHazardKitClock.Phase(sch,15,double.NaN).State==NativeKitState.Active,"schedule: cycles from first + offset every period");
            Check(NativeHazardKitClock.Phase(new KitRecord{id="x",kind="alarm-sign",trigger=new KitTrigger{mode="always"}},123,double.NaN).State==NativeKitState.Active,"always: active");
            var follow=new NativeHazardState{Phase=NativeHazardPhase.Danger,Level=.4f,Since=.3,Until=.9};Check(NativeHazardKitClock.Phase(r,1,double.NaN,true,follow).State==NativeKitState.Active&&NativeHazardKitClock.Phase(r,1,double.NaN,true,new NativeHazardState{Phase=NativeHazardPhase.Warn}).State==NativeKitState.Arming&&NativeHazardKitClock.Phase(r,1,double.NaN,true,new NativeHazardState{Phase=NativeHazardPhase.Clear}).State==NativeKitState.Cooldown,"following a stage hazard: warn arming, closing and shut active, opening cooldown");
            Check(NativeHazardKitClock.Phase(r,11.5,10,true,new NativeHazardState{Phase=NativeHazardPhase.Idle}).State==NativeKitState.Active,"the stronger of an event and a follow wins");
            // The ledger: fires by name, radius and hazard; idempotent; a re-fire while active holds active.
            var kit=new[]{r,new KitRecord{id="near",kind="alarm-strobe",pos=new V3(0,0,0),trigger=new KitTrigger{mode="event",events=new[]{"breach"},radius=5}},new KitRecord{id="shut",kind="alarm-beacon",trigger=new KitTrigger{mode="event",events=new[]{"dev-trigger"},hazard="h1"}}};
            var scene=new NativeKitScene(kit);
            Check(scene.Fire("wave-start",10)==1&&scene.Fire("wave-start",10)==0&&scene.Fire("wave-start",9)==0,"the same or an older event changes nothing");
            Check(scene.Fire("breach",3,true,new V3(20,0,0))==0&&scene.Fire("breach",3,true,new V3(3,0,0))==1,"a radius keeps only near events");
            Check(scene.Fire("dev-trigger",4,false,default,"h2")==0&&scene.Fire("dev-trigger",4,false,default,"h1")==1,"a hazard filter keeps only its hazard's events");
            scene.Fire("wave-start",13);scene.Step(14,NativeKitFlash.Full,NativeKitBeat.Off,false);Check(scene.Phases[0].State==NativeKitState.Active&&Math.Abs(scene.Phases[0].Until-3)<1e-6,"a re-fire while active holds it active for its full time from the re-fire ("+scene.Phases[0].Until+" s left)");
            scene.Step(3,NativeKitFlash.Full,NativeKitBeat.Off,false);Check(scene.Phases.All(p=>p.State==NativeKitState.Idle),"a clock that ran back forgets the fires");
        }
        // Flashes (WCAG 2.3.1): rises of at least a quarter of the lamp's peak within a third of a second (a slow fade or ramp is
        // not one), re-armed by an equal fall; the most in any 1 s window, sampled at 1 kHz.
        private static int MaxPerSecond(Func<double,float> lamp,double from,double to)
        {
            int n=(int)((to-from)*1000);var v=new double[n];double peak=0;for(int i=0;i<n;i++){v[i]=lamp(from+i*.001);peak=Math.Max(peak,v[i]);}if(peak<=0)return 0;
            var times=new List<double>();bool armed=true;const int W=330;int since=0;double top=0;
            for(int i=0;i<n;i++)
            {
                if(armed)
                {
                    double lo=double.MaxValue;for(int k=Math.Max(since,i-W);k<=i;k++)lo=Math.Min(lo,v[k]);
                    if(v[i]-lo>=.25*peak){armed=false;top=v[i];times.Add(from+i*.001);}
                }
                else{top=Math.Max(top,v[i]);if(top-v[i]>=.25*peak){armed=true;since=i;}}
            }
            int most=0;for(int i=0;i<times.Count;i++){int c=times.Count(x=>x>=times[i]&&x<times[i]+1);most=Math.Max(most,c);}return most;
        }
        private static void Flashes(Func<string,string> R)
        {
            using var pve=JsonDocument.Parse(R("Assets/Conversion/Resources/PveContent.json"));var track=JsonSerializer.Deserialize<NativeBossTrack>(pve.RootElement.GetProperty("bossMusic").GetRawText(),Options);
            var r=new KitRecord{id="f",kind="alarm-strobe",music="beat",trigger=new KitTrigger{mode="event",events=new[]{"breach"},warn=1,active=30,cooldown=2}};
            foreach(NativeKitFlash mode in Enum.GetValues(typeof(NativeKitFlash)))
                foreach(var (name,beat) in new[]{("free",(Func<double,NativeKitBeat>)(t=>NativeKitBeat.Off)),("Black Siren Protocol",t=>NativeKitBeat.Of(track,60+t)),("a 220 BPM grid",t=>NativeKitBeat.Grid(220,0,t)),("a 90 BPM grid",t=>NativeKitBeat.Grid(90,.1,t))})
                {
                    int cap=mode==NativeKitFlash.Full?3:mode==NativeKitFlash.Reduced?2:0;
                    int n=MaxPerSecond(t=>NativeHazardKitClock.Strobe(NativeHazardKitClock.Phase(r,t,0),t,mode,beat(t),true),.2,20);
                    Check(n<=cap,mode+" strobe "+name+": "+n+" flashes in a second (cap "+cap+")");
                    if(mode==NativeKitFlash.Full)Check(n>=1,"a full strobe does flash ("+name+")");
                    int arming=MaxPerSecond(t=>NativeHazardKitClock.Strobe(NativeHazardKitClock.Phase(r,t,0),t,mode,beat(t),true),0,1);Check(arming<=cap,mode+" arming "+name+" within the cap");
                }
            // Reduced also lowers the contrast; Calm never drops below its glow by more than its breath.
            double lo=1e9,hi=0;for(double t=1.2;t<10;t+=.002){float v=NativeHazardKitClock.Strobe(NativeHazardKitClock.Phase(r,t,0),t,NativeKitFlash.Reduced,NativeKitBeat.Off,false);lo=Math.Min(lo,v);hi=Math.Max(hi,v);}Check(hi<=.81&&lo>=.24,"reduced pulses stay between .25 and .8 ("+lo+".."+hi+")");
            lo=1e9;hi=0;for(double t=1.2;t<10;t+=.002){float v=NativeHazardKitClock.Strobe(NativeHazardKitClock.Phase(r,t,0),t,NativeKitFlash.Calm,NativeKitBeat.Off,false);lo=Math.Min(lo,v);hi=Math.Max(hi,v);}Check(hi-lo<=.25,"calm glows (range "+(hi-lo)+")");
            Check(NativeHazardKitClock.FlashHz(NativeKitFlash.Full,143.6/60,out int k1)<=3&&k1==1&&NativeHazardKitClock.FlashHz(NativeKitFlash.Reduced,143.6/60,out int k2)<=2&&k2==2&&NativeHazardKitClock.FlashHz(NativeKitFlash.Full,220/60.0,out int k3)<=3&&k3==2,"beat-locked flashes skip beats to stay under the cap");
            // The sign blinks at 1 Hz arming (Full) and holds steady otherwise; beacons turn at most 1.1 a second.
            var sign=new KitRecord{id="s",kind="alarm-sign",trigger=new KitTrigger{mode="event",events=new[]{"breach"},warn=8,active=10}};
            Check(MaxPerSecond(t=>NativeHazardKitClock.SignLight(NativeHazardKitClock.Phase(sign,t,0),t,NativeKitFlash.Full),0,8)<=1&&MaxPerSecond(t=>NativeHazardKitClock.SignLight(NativeHazardKitClock.Phase(sign,t,0),t,NativeKitFlash.Reduced),0,8)==0,"signs blink at most once a second, never in Reduced");
            var beacon=new KitRecord{id="b",kind="alarm-beacon",trigger=new KitTrigger{mode="event",events=new[]{"breach"},warn=1,active=20}};
            foreach(NativeKitFlash mode in Enum.GetValues(typeof(NativeKitFlash))){double a0=NativeHazardKitClock.BeaconAngle(NativeHazardKitClock.Phase(beacon,5,0),5,mode,NativeKitBeat.Off,false),a1=NativeHazardKitClock.BeaconAngle(NativeHazardKitClock.Phase(beacon,6,0),6,mode,NativeKitBeat.Off,false);Check((a1-a0)/(Math.PI*2)<=1.1+1e-9,mode+" beacon turns at most 1.1 a second");}
        }
        private static void Music(Func<string,string> R)
        {
            using var pve=JsonDocument.Parse(R("Assets/Conversion/Resources/PveContent.json"));var track=JsonSerializer.Deserialize<NativeBossTrack>(pve.RootElement.GetProperty("bossMusic").GetRawText(),Options);
            var r=new KitRecord{id="k",kind="alarm-klaxon",music="beat",trigger=new KitTrigger{mode="event",events=new[]{"breach"},warn=.3f,active=20}};
            // Beat-locked blasts, heard frame by frame (each once), land on every second beat of the song.
            double songAt=40;var times=new double[64];var p=NativeHazardKitClock.Phase(r,10,0);var all=new List<double>();
            for(double t=10;t<15;t+=1/60.0){int m=NativeHazardKitClock.Blasts(r,NativeHazardKitClock.Phase(r,t+1/60.0,0),t+1/60.0,t,t+1/60.0,NativeKitBeat.Of(track,songAt+t+1/60.0),true,2,times);for(int i=0;i<m;i++)all.Add(times[i]);}
            Check(all.Count>=5&&all.Distinct().Count()==all.Count,"beat-locked blasts over 5 s, each heard once ("+all.Count+")");
            for(int i=0;i<all.Count;i++){double song=songAt+all[i];int idx=track.Beat(song+.002);Check(Math.Abs(track.beats[idx]-song)<.003,"blast "+i+" on a beat ("+(track.beats[idx]-song)+")");if(i>0)Check(Math.Abs(all[i]-all[i-1]-2*(track.beats[idx]-track.beats[idx-1]))<.02,"every second beat");}
            int free=NativeHazardKitClock.Blasts(r,p,10,10,15,NativeKitBeat.Off,false,2,times);Check(free>=3&&Enumerable.Range(1,free-1).All(i=>Math.Abs(times[i]-times[i-1]-1.6)<1e-6),"free-running blasts every 1.6 s");
            // The klaxon variant fit (the licensed bank's three: 1.161 s natural, .587 s short, .761 s stretched to two beats).
            float[] lengths={1.161f,.587f,.761f};double bp=60/143.6;
            Check(NativeHazardKitClock.FitVariant(lengths,bp,2,out float rate)==2&&rate==1,"at 143.6 BPM the stretched blast fits two beats");
            Check(NativeHazardKitClock.FitVariant(lengths,60/100.0,2,out rate)==0&&rate==1,"at 100 BPM the natural blast rings inside two beats");
            Check(NativeHazardKitClock.FitVariant(lengths,.55,2,out rate)==0&&rate>1&&rate<=1.06f,"a blast up to 6% long is sped up to fit");
            Check(NativeHazardKitClock.FitVariant(lengths,.2,2,out rate)==-1,"nothing fits half a second: free-run");
            // The trip reaction.
            float min=1,max=0;for(double t=0;t<3;t+=.005){float g=NativeHazardKitClock.MusicReact(t,NativeKitFlash.Full);min=Math.Min(min,g);max=Math.Max(max,g);}Check(Math.Abs(min-.55f)<1e-4&&max<=1.1001f&&NativeHazardKitClock.MusicReact(2.5,NativeKitFlash.Full)==1&&NativeHazardKitClock.MusicReact(-1,NativeKitFlash.Full)==1,"the trip dips the music to .55 and swells to 1.1, then lets go");
            Check(NativeHazardKitClock.MusicReact(.2,NativeKitFlash.Calm)>=.79f&&NativeHazardKitClock.MusicReact(new[]{0.0,1.0},2,1.2,NativeKitFlash.Full)<=NativeHazardKitClock.MusicReact(.2,NativeKitFlash.Full),"calm is gentle; the deepest of several trips wins");
            // Beacons keep the bar: beat-locked one turn every two beats.
            var bc=new KitRecord{id="b",kind="alarm-beacon",music="beat",trigger=new KitTrigger{mode="event",events=new[]{"breach"},warn=.1f,active=60}};var ph=NativeHazardKitClock.Phase(bc,10,0);
            int i0=track.Beat(80);double turn=NativeHazardKitClock.BeaconAngle(ph,10,NativeKitFlash.Full,NativeKitBeat.Of(track,track.beats[i0]),true)/(Math.PI*2);Check(Math.Abs(turn*2-Math.Round(turn*2))<1e-6,"on a beat the beam points forward or back");
        }
        private static void Audio(string root,Func<string,string> R)
        {
            var named=new HashSet<string>(StringComparer.Ordinal);foreach(var f in new[]{"Assets/ChipArena/Core/NativeHazardKitSound.cs","Assets/ChipArena/Core/NativeHazardKit.cs","Assets/ChipArena/Runtime/NativeHazardKitAudio.cs"})foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(R(f),"\"((alarm|turret|saw)\\.[a-z-]+)\""))named.Add(m.Groups[1].Value);
            Check(named.Count==19&&named.SetEquals(NativeKitSynth.Keys),"the kit names exactly its 19 keys ("+named.Count+")");
            foreach(var k in NativeKitSynth.Keys)
            {
                Check(NativeSoundKeys.Find(k,out var d),k+" is in the bank contract");Check(NativeKitSynth.Loop(k)==(d.Space==NativeSoundSpace.Loop),k+" loops exactly when the contract says so");
                var a=NativeKitSynth.Samples(k);var b=NativeKitSynth.Samples(k);Check(a!=null&&a.Length>NativeKitSynth.Rate/20&&a.Length<=d.Cap*NativeKitSynth.Rate+1&&a.SequenceEqual(b)&&a.All(x=>!float.IsNaN(x)&&Math.Abs(x)<=1)&&a.Any(x=>Math.Abs(x)>.02f),k+" has a deterministic built-in fallback within its cap");
            }
            Check(NativeKitSynth.Samples("ui.click")==null&&NativeHazardKit.AlarmLooks.All(l=>NativeKitSynth.Has(NativeHazardKit.Look(l).Loop)),"each alarm look has its loop key; other keys are not the kit's");
            // The plan: falloff, duck and cap.
            Check(NativeKitSoundPlan.Falloff(3,5,40)==1&&NativeKitSoundPlan.Falloff(40,5,40)==0&&NativeKitSoundPlan.Falloff(22.5f,5,40)>.49f&&NativeKitSoundPlan.Falloff(22.5f,5,40)<.51f,"falloff: full inside, silent outside, half way between");
            Check(NativeKitSoundPlan.Duck(0)==1&&Math.Abs(NativeKitSoundPlan.Duck(1)-.4f)<1e-6&&NativeKitSoundPlan.Cap(.5f)==1&&Math.Abs(NativeKitSoundPlan.Cap(4)-.5f)<1e-6,"the combat duck keeps 40%; the cap holds two full alarms at one");
            var kit=new[]{new KitRecord{id="a",kind="alarm-beacon",look="biohazard",pos=new V3(0,0,0),react=true,trigger=new KitTrigger{mode="event",events=new[]{"breach"}}},new KitRecord{id="b",kind="alarm-beacon",look="lockdown",pos=new V3(1,0,0),trigger=new KitTrigger{mode="event",events=new[]{"breach"}}},new KitRecord{id="far",kind="alarm-beacon",pos=new V3(200,0,0),trigger=new KitTrigger{mode="event",events=new[]{"breach"}}}};
            var scene=new NativeKitScene(kit);scene.Fire("breach",1);var plan=new NativeKitSoundPlan();var heard=new List<string>();int trips=0;
            for(double t=.9;t<4;t+=1/60.0){scene.Step(t,NativeKitFlash.Full,NativeKitBeat.Off,false);plan.Build(scene,new V3(0,0,0),t-1/60.0,t,0);for(int i=0;i<plan.CueCount;i++)heard.Add(plan.Cues[i].Key+"@"+plan.Cues[i].Piece);trips+=plan.TripCount;}
            Check(heard.Count(h=>h=="alarm.trip@0")==1&&heard.Count(h=>h=="alarm.trip@1")==1&&!heard.Any(h=>h.EndsWith("@2")),"each near alarm trips once; the far one is silent");Check(trips==1,"only the react alarm moves the music");
            Check(plan.LoopCount>=2&&plan.Loops.Take(plan.LoopCount).Where(l=>l.Key.StartsWith("alarm.")&&l.Key!="alarm.beacon").Sum(l=>l.Gain*l.Gain)<=1.0001f,"two alarms together stay within the cap");
            plan.Build(scene,new V3(0,0,0),3,3+1/60.0,1);float ducked=plan.Loops.Take(plan.LoopCount).Where(l=>l.Key=="alarm.biohazard").Sum(l=>l.Gain);plan.Build(scene,new V3(0,0,0),3,3+1/60.0,0);float clear=plan.Loops.Take(plan.LoopCount).Where(l=>l.Key=="alarm.biohazard").Sum(l=>l.Gain);
            Check(ducked<clear*.75f&&ducked>clear*.3f,"combat ducks the alarms ("+ducked+" < "+clear+")");
            // The licensed clips are outside the repository; the installer knows the new keys (it reads the contract).
            Check(R("tools/install_local_assets.py").Contains("BudgetBytes=(\\d+)")&&NativeSoundKeys.BudgetBytes==13000000,"the installer reads the 13 MB budget from the contract");
            var stray=Directory.EnumerateDirectories(Path.Combine(root,"Assets"),"*",SearchOption.AllDirectories).Select(d=>d.Replace('\\','/')).Where(d=>!d.Contains("/LocalAssets/")&&NativeKitSynth.Keys.Any(k=>d.EndsWith("/"+k,StringComparison.Ordinal))).ToList();
            Check(stray.Count==0,"no kit clip folder outside the ignored LocalAssets: "+string.Join(", ",stray));
        }
        private static void Lights()
        {
            var kit=NativeHazardKit.Gallery(new V3(0,0,0),0);var scene=new NativeKitScene(kit){Bays=true};for(int i=0;i<kit.Length;i++)scene.FireAt(i,0);
            scene.Step(3,NativeKitFlash.Full,NativeKitBeat.Off,true);scene.Build(0,1.7,0,1,0,0,0,1,0,1,false);Check(scene.LightCount>=4,"the gallery's active alarms offer lights ("+scene.LightCount+")");
            var pool=new NativeKitLightPool();pool.Update(scene,0,1.7,0,0);Check(pool.Count==3,"three kit lights at full effects");pool.Budget=1;pool.Clear();pool.Update(scene,0,1.7,0,0);Check(pool.Count==1,"one on the low tier");
            var pos=new float[32];var rgb=new float[24];for(int i=0;i<8;i++){pos[i*4]=i*3;pos[i*4+1]=i<6?3:-10000;pos[i*4+2]=0;pos[i*4+3]=8;rgb[i*3]=i<6?(i==2?.01f:1):0;}
            float[] kp={0,2,0,6,1,2,0,6},kr={2,0,0,2,0,0};int took=NativeKitLightPool.Merge(pos,rgb,8,kp,kr,2,0,2,0);Check(took==2&&pos[6*4+1]==2&&pos[7*4+1]==2,"kit lights take the free slots first");
            for(int i=0;i<8;i++){pos[i*4+1]=3;rgb[i*3]=i==2?.01f:1;}took=NativeKitLightPool.Merge(pos,rgb,8,kp,kr,1,0,2,0);Check(took==1&&pos[2*4]==0&&pos[2*4+1]==2,"then the slot worth least at the eye");
        }
        private static void Turret()
        {
            var r=new KitRecord{id="t",kind="turret",mount="floor",range=18,arc=110,rate=10};var seen=new HashSet<NativeTurretState>();double lockStart=double.NaN,lockEnd=double.NaN,fire=double.NaN;
            for(double t=0;t<NativeTurretPreview.Loop;t+=.01){var v=NativeTurretPreview.At(r,t);seen.Add(v.State);if(v.State==NativeTurretState.Lock){if(double.IsNaN(lockStart))lockStart=t;lockEnd=t;}if(v.State==NativeTurretState.Fire&&double.IsNaN(fire))fire=t;Check(v.Health>=0&&v.Health<=1&&v.Heat>=0&&v.Heat<=1,"view in range at "+t);}
            Check(seen.Count==Enum.GetValues(typeof(NativeTurretState)).Length,"the preview visits every state ("+seen.Count+")");
            Check(Math.Abs(lockEnd-lockStart-NativeTurretPreview.LockTime)<.02&&fire>=lockStart+NativeTurretPreview.LockTime-.011,"the lock tell runs its full .8 s before the first round");
            var a=NativeTurretPreview.At(r,5.0);var b=NativeTurretPreview.At(r,5.0);Check(a.LastShot==b.LastShot&&a.Shots==b.Shots&&a.Yaw==b.Yaw,"the preview is a pure function of the clock");
            var beeps=new double[16];int n=NativeTurretPreview.LockBeeps(4,3.9,5,beeps);Check(n>=5&&Enumerable.Range(2,n-2).All(i=>beeps[i]-beeps[i-1]<=beeps[i-1]-beeps[i-2]+1e-9)&&beeps[n-1]<=4+NativeTurretPreview.LockTime+1e-6,"the lock beeps quicken inside the lock ("+n+")");
            Check(Math.Abs(NativeTurretPreview.SpinRate(NativeTurretState.Lock,NativeTurretPreview.LockTime)-NativeTurretPreview.SpinRate(NativeTurretState.Fire,0))<1e-9&&NativeTurretPreview.SpinRate(NativeTurretState.Scan,3)==0,"the barrels reach full speed as the lock ends");
            // The look: every state builds within budget, the laser is red only locked or firing.
            var scene=new NativeKitScene(new[]{r});foreach(double t in new[]{1.0,3.4,4.2,5.2,6.5,10,12,13,14,15.7}){scene.Step(t,NativeKitFlash.Full,NativeKitBeat.Off,true);scene.Build(3,1.6,3,1,0,0,0,1,0,1,false);Check(scene.PartCount>20&&scene.PartCount<NativeKitScene.PartCapacity,"turret parts at "+t);}
            scene.Step(5.2,NativeKitFlash.Full,NativeKitBeat.Off,true);scene.Build(3,1.6,3,1,0,0,0,1,0,1,false);Check(scene.ShotCount>0,"firing gives the renderer its rounds");
        }
        private static void Saws()
        {
            foreach(var v in NativeHazardKit.SawVariants)
            {
                var r=new KitRecord{id=v,kind="saw",variant=v,radius=.5f,speed=2,arc=60,length=2,a=new V3(-1,0,0),b=new V3(1,0,0),trigger=new KitTrigger{mode="event",events=new[]{"breach"},warn=1,active=4,cooldown=1.5f}};
                if(v=="ceiling-drop"){r.a=new V3(0,0,0);r.b=new V3(0,-2,0);}
                var idle=NativeKitSaw.Pose(r,NativeHazardKitClock.Phase(r,0,double.NaN),0,new V3(0,1,0),FactoryQ.Identity);Check(!idle.Live&&idle.Rate==0,v+": idle is still and harmless");
                bool live=false;double maxSwing=0;
                for(double t=0;t<7;t+=.02)
                {
                    var p=NativeHazardKitClock.Phase(r,t,0);var s=NativeKitSaw.Pose(r,p,t,new V3(0,1,0),FactoryQ.Identity);live|=s.Live;maxSwing=Math.Max(maxSwing,Math.Abs(s.Swing));
                    Check(!s.Live||p.State==NativeKitState.Active,v+": live only while active");Check(p.State!=NativeKitState.Arming||s.Exposure==0||v=="pendulum",v+": hidden while it spins up");
                    if(v=="floor-slot"||v=="wall"||v=="rail")Check(s.Hub.x>=-1.001f&&s.Hub.x<=1.001f,v+": stays on its path");
                }
                Check(live,v+": comes out while active");if(v=="pendulum")Check(maxSwing<=30*Math.PI/180+1e-6&&maxSwing>.3,"the pendulum swings within its arc ("+maxSwing+")");
            }
            {
                // A hit (the authority's hit event; the gallery's scripted one) sounds the wet chop once, at the blade.
                var r=new KitRecord{id="hit",kind="saw",variant="floor-slot",radius=.5f,a=new V3(-1,0,0),b=new V3(1,0,0),trigger=new KitTrigger{mode="manual"}};var scene=new NativeKitScene(new[]{r});scene.FireAt(0,0);scene.Hit(0,2.5);scene.Hit(0,2.5);
                var plan=new NativeKitSoundPlan();int chops=0;for(double t=2;t<3.2;t+=1/60.0){scene.Step(t,NativeKitFlash.Full,NativeKitBeat.Off,false);plan.Build(scene,new V3(0,1,1),t-1/60.0,t,0);for(int i=0;i<plan.CueCount;i++)if(plan.Cues[i].Key=="saw.hit"){chops++;Check(Math.Abs(plan.Cues[i].Time-2.5)<1e-9,"the chop sounds at the hit");}}
                Check(chops==1,"a saw hit sounds once ("+chops+")");Check(scene.Blood[0]>.5f,"and soaks the blade");
            }
            Check(NativeKitSaw.Blood(new double[0],0,5)==0&&NativeKitSaw.Blood(new[]{1.0},1,1.1)>.5f&&NativeKitSaw.Blood(new[]{1.0,2,3,4},4,500)<=.6f&&NativeKitSaw.Blood(new[]{1.0,2,3,4},4,500)>0,"blood: a hit soaks the blade and fades to a stain");
        }
        private static void Gallery()
        {
            var g=NativeHazardKit.Gallery(new V3(4,0,-2),Math.PI/2);NativeHazardKit.Validate(g);
            Check(NativeHazardKit.Kinds.All(k=>g.Any(r=>r.kind==k))&&NativeHazardKit.SawVariants.All(v=>g.Any(r=>r.variant==v))&&NativeHazardKit.AlarmLooks.All(l=>g.Any(r=>r.look==l))&&NativeHazardKit.Mounts.All(m=>g.Any(r=>r.kind=="turret"&&r.mount==m)),"the gallery shows every kind, look, saw variant and turret mount");
            Check(g.All(r=>r.pos.x>4+3),"the gallery stands ahead of the viewer (facing +x)");Check(g.Any(r=>r.music=="beat"&&r.react),"its music lock has a reacting alarm");
        }
        private static void Allocation(KitRecord[] ward)
        {
            var scenes=new[]{new NativeKitScene(ward),new NativeKitScene(NativeHazardKit.Gallery(new V3(0,0,0),0)){Bays=true}};var plan=new NativeKitSoundPlan();
            void Frame(NativeKitScene s,double t){if(Math.Floor(t*.25)!=Math.Floor((t-1/60.0)*.25))for(int i=0;i<s.Records.Length;i++)s.FireAt(i,t);s.Fire("breach",Math.Floor(t),true,new V3(10,2,24.8f));
                var b=NativeKitBeat.Grid(143.6,0,t);s.Step(t,NativeKitFlash.Full,b,true);s.Build(0,1.7,0,1,0,0,0,1,0,1,false);plan.Build(s,new V3(0,1.7f,0),t-1/60.0,t,.3f);}
            foreach(var s in scenes)for(double t=0;t<20;t+=1/60.0)Frame(s,t);
            long before=GC.GetAllocatedBytesForCurrentThread();foreach(var s in scenes)for(double t=20;t<40;t+=1/60.0)Frame(s,t);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"no per-frame allocation (fire, step, build, sound plan over 2400 frames: "+used+" bytes)");
        }
        private static void Untouched(string root,Func<string,string> R,UnityMapContent maps)
        {
            var dumpOptions=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};string Dump(ContentMap m)=>JsonSerializer.Serialize(m.hazards,dumpOptions);var before=maps.maps.Select(Dump).ToArray();
            var set=JsonSerializer.Deserialize<KitSet>(R("Assets/Conversion/Resources/HazardKit.json"),Options);
            foreach(var m in maps.maps){var kit=NativeHazardKit.For(set,m.id,m.look);if(kit.Length==0)continue;var scene=new NativeKitScene(kit);for(double t=0;t<60;t+=.25){foreach(var h in m.hazards??new MapHazard[0])if(h.kind=="shutter"){var s=NativeWardZeroHazards.At(h,NativePhase.Fight,0,0,t);for(int i=0;i<kit.Length;i++)if(kit[i].trigger?.hazard==h.id)scene.Follow(i,s);}scene.Step(t,NativeKitFlash.Full,NativeKitBeat.Off,false);}}
            Check(maps.maps.Select(Dump).SequenceEqual(before),"the kit only reads the stage hazards (every map's hazards are byte-identical after a minute of kit steps)");
            foreach(var m in maps.maps)if(m.hazards!=null&&m.hazards.Length>0)NativeStageHazards.Validate(m);Check(true,"the stage hazards still validate");
            foreach(var f in new[]{"NativeHazardKit.cs","NativeHazardKitClock.cs","NativeHazardKitBuild.cs","NativeHazardKitSound.cs"}){var s=R("Assets/ChipArena/Core/"+f);Check(!s.Contains("Damage(")&&!s.Contains("Hp-=")&&!s.Contains(".Hp=")&&!s.Contains("Emit("),f+" decides no hit");}
            var world=R("Assets/ChipArena/Runtime/NativeHazardKitWorld.cs");Check(!world.Contains("Damage(")&&!world.Contains("Emit(")&&!world.Contains("collider")&&!world.Contains("NetworkFrame"),"the runtime adapter only reads replicated state");
        }
        private static void Hooks(Func<string,string> R)
        {
            var src=R("Assets/ChipArena/Runtime/NativeSourcePresentation.cs");Check(src.Contains("if(revamp)WardZeroScenery(arena);")&&src.IndexOf("HazardKitWorld(arena);",StringComparison.Ordinal)>src.IndexOf("if(revamp)WardZeroScenery(arena);",StringComparison.Ordinal),"the kit draws after Ward Zero's own pick of the practical lights, in the arena and the killcam");
            var audio=R("Assets/ChipArena/Runtime/NativeAudio.cs");Check(audio.Contains("EffectsFrame(arena,menu);KitAudio(arena,menu);")&&audio.Contains("*kitMusic);"),"the kit's sound runs each frame and its trip reaction multiplies the music's own volume");
            var lighting=R("Assets/ChipArena/Runtime/NativeLighting.cs");Check(lighting.Contains("if(kitCount==0||Time.frameCount-kitFrame>1){Shader.SetGlobalVectorArray(ArenaPosId,arenaPos);Shader.SetGlobalVectorArray(ArenaColorId,arenaColor);Shader.SetGlobalInt(ArenaCountId,arenaCount);return;}"),"without kit lights the practical slots upload exactly as before");
            var game=R("Assets/ChipArena/Runtime/ConversionGame.cs");Check(game.Contains("alarmLights=\"Full\"")&&game.Contains("prefs.alarmLights==\"Reduced\"||prefs.reducedEffects?NativeKitFlash.Reduced"),"Settings · Alarm lights, with Reduced effects forcing at least Reduced");
            var sandbox=R("Assets/ChipArena/Runtime/NativeSandbox.cs");Check(sandbox.Contains("\"Preview Hazard Kit\"")&&sandbox.Contains("\"Lock Hazard Kit to music\""),"the DEV panel's Previews list the hazard kit gallery and its music lock");
            Check(R("Assets/ChipArena/Runtime/NativeHazardKitPresentation.cs").Contains("bool want=HazardKitGallery&&arena.Sandbox;"),"the gallery runs only in the Dev Sandbox");
        }
    }
}
#endif
