#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Tests/event-feedback-parity.json is written by tools/export_event_feedback.mjs from the pinned event-vfx.ts shake flag,
    // combat-numbers.ts CombatNumberTrack/numberLabel and audio.ts SoundLimiter/ArenaAudio.update. Each scenario names the
    // Unity events (or the routing context) that stand for its source events; the Core rules must give the same result.
    internal static class EventFeedbackChecks
    {
        public static IEnumerable<string> Run(string root)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/event-feedback-parity.json")));var json=document.RootElement;var passed=new List<string>();
            void Check(bool ok,string what){if(!ok)throw new Exception("Event feedback: "+what);}
            bool Near(double a,double b,double tolerance)=>Math.Abs(a-b)<=tolerance;
            V3 Vec(JsonElement e)=>new V3(e.GetProperty("x").GetSingle(),e.GetProperty("y").GetSingle(),e.GetProperty("z").GetSingle());
            EventKind Kind(string name)=>(EventKind)Enum.Parse(typeof(EventKind),name);

            // numberLabel
            int labels=0;foreach(var l in json.GetProperty("labels").EnumerateArray()){double value=l.GetProperty("amount").GetDouble();bool heal=l.GetProperty("heal").GetBoolean();string want=l.GetProperty("label").GetString(),got=NativeCombatNumbers.Label(value,heal);Check(got==want,"label "+value+" "+heal+": "+got+" != "+want);labels++;}
            passed.Add("event feedback: numberLabel matches the source for "+labels+" amounts (JS Math.round and toFixed)");

            // SoundLimiter through ArenaAudio.cue
            var limiter=new NativeSoundLimiter();int gates=0;Check(Near(NativeSoundLimiter.Trim,json.GetProperty("hitTrim").GetDouble(),1e-12),"HIT_TRIM");
            foreach(var g in json.GetProperty("limiter").EnumerateArray()){string key=g.GetProperty("key").GetString();double now=g.GetProperty("now").GetDouble(),gain=g.GetProperty("gain").GetDouble(),want=g.GetProperty("out").GetDouble(),got=limiter.Cue(key,now,gain);Check(Near(got,want,1e-12),"limiter "+key+" @"+now+": "+got+" != "+want);gates++;}
            passed.Add("event feedback: SoundLimiter gaps, folding, ducking and trim match the source over "+gates+" cues");

            // Camera shake trigger: event-vfx.ts:29 per source type and target, on the Unity kind that carries it.
            int shakes=0;foreach(var s in json.GetProperty("shake").EnumerateArray())
            {
                var kind=Kind(s.GetProperty("kind").GetString());int target=s.GetProperty("unityTarget").GetInt32();bool want=s.GetProperty("shake").GetBoolean();
                Check(NativeShakeRules.Triggers(kind,target,s.GetProperty("amount").GetSingle(),1)==want,"shake "+s.GetProperty("type").GetString()+" "+s.GetProperty("message")+" -> "+kind+" target "+target);shakes++;
            }
            foreach(EventKind kind in Enum.GetValues(typeof(EventKind)))
            {
                bool may=kind==EventKind.Hit||kind==EventKind.Headshot||kind==EventKind.Impact||kind==EventKind.Break||kind==EventKind.Block||kind==EventKind.Collapse;
                Check(NativeShakeRules.Triggers(kind,1,5,1)==may&&!NativeShakeRules.Triggers(kind,2,5,1)&&!NativeShakeRules.Triggers(kind,0,5,0),"shake kind "+kind);
            }
            // simulation.ts:241 emits a hit only above .05: a smaller Unity hit has no source event to shake on.
            Check(!NativeShakeRules.Triggers(EventKind.Hit,1,.05f,1)&&NativeShakeRules.Triggers(EventKind.Hit,1,.051f,1)&&!NativeShakeRules.Triggers(EventKind.BurnTick,1,40,1),"hit floor and tick exclusion");
            // renderer.ts:136 age gate and :149 envelope: .12, linear by the frame time (main.ts caps it at .05 s), +-.06 m.
            Check(NativeShakeRules.Fresh(10,9.6f)&&!NativeShakeRules.Fresh(10,9.59f)&&NativeShakeRules.Fresh(10,10.2f),"shake age gate");
            float amount=NativeShakeRules.Amount;int frames=0;while(amount>0&&frames<100){amount=NativeShakeRules.Decay(amount,1/60f);frames++;}Check(frames==8,"shake lasts .12 s at 60 Hz ("+frames+" frames)");
            amount=NativeShakeRules.Decay(NativeShakeRules.Amount,.2f);Check(Near(amount,.07,1e-6)&&NativeShakeRules.Decay(amount,-1)==amount,"shake frame cap");
            Check(Near(NativeShakeRules.Offset(.12f,0),-.06,1e-7)&&Near(NativeShakeRules.Offset(.12f,1),.06,1e-7)&&NativeShakeRules.Offset(.12f,.5f)==0,"shake offset range");
            passed.Add("event feedback: camera shake triggers match event-vfx.ts for "+shakes+" source events, burn/poison ticks excluded; .12 s envelope");

            // CombatNumberTrack
            int frameCount=0,numberCount=0;
            foreach(var scenario in json.GetProperty("numbers").EnumerateArray())
            {
                string name=scenario.GetProperty("name").GetString();int local=scenario.GetProperty("local").GetInt32();bool pve=scenario.GetProperty("pve").GetBoolean();
                var bodies=new Dictionary<int,V3>();foreach(var p in scenario.GetProperty("players").EnumerateArray())bodies[p.GetProperty("id").GetInt32()]=Vec(p.GetProperty("pos"));
                var track=new NativeCombatNumbers();NativeCombatNumbers.Anchor anchor=(int id,out V3 body)=>bodies.TryGetValue(id,out body);
                foreach(var frame in scenario.GetProperty("frames").EnumerateArray())
                {
                    var events=new List<ArenaEvent>();foreach(var e in frame.GetProperty("events").EnumerateArray())events.Add(new ArenaEvent{Id=e.GetProperty("id").GetInt32(),Time=e.GetProperty("time").GetSingle(),Kind=Kind(e.GetProperty("kind").GetString()),Source=e.GetProperty("source").GetInt32(),Target=e.GetProperty("target").GetInt32(),Amount=e.GetProperty("amount").GetSingle(),Position=Vec(e.GetProperty("pos"))});
                    float time=frame.GetProperty("time").GetSingle();var got=track.Update(events,time,frame.GetProperty("generation").GetInt32(),local,pve,anchor);var want=frame.GetProperty("numbers");
                    Check(got.Count==want.GetArrayLength(),name+" @"+time+": "+got.Count+" numbers != "+want.GetArrayLength());
                    for(int i=0;i<got.Count;i++)
                    {
                        var n=got[i];var w=want[i];string where=name+" @"+time+" #"+i;var pos=Vec(w.GetProperty("pos"));
                        Check(n.Id==w.GetProperty("id").GetInt32()&&n.Target==w.GetProperty("target").GetInt32()&&n.Heal==w.GetProperty("heal").GetBoolean()&&n.Lane==w.GetProperty("lane").GetInt32(),where+" id/target/heal/lane");
                        Check(Near(n.Amount,w.GetProperty("amount").GetDouble(),1e-5)&&Near(n.At,w.GetProperty("at").GetDouble(),1e-6)&&(n.Position-pos).Length<1e-5,where+" amount/time/position: "+n.Amount+"/"+n.At+"/"+n.Position.x+","+n.Position.y+","+n.Position.z+" != "+w.GetProperty("amount")+"/"+w.GetProperty("at")+"/"+w.GetProperty("pos"));
                        Check(n.Label==w.GetProperty("label").GetString(),where+" label "+n.Label+" != "+w.GetProperty("label").GetString());numberCount++;
                    }
                    frameCount++;
                }
            }
            Check(NativeCombatNumbers.Alpha(0)==1&&Near(NativeCombatNumbers.Alpha(.91f),.5,1e-5)&&Near(NativeCombatNumbers.Scale(0),1.24,1e-6)&&NativeCombatNumbers.Scale(.2f)==1&&Near(NativeCombatNumbers.OwnX(0),.47,1e-7)&&Near(NativeCombatNumbers.OwnY(1),.55,1e-7),"number envelope");
            passed.Add("event feedback: CombatNumberTrack merges, lanes, heals, ticks, PvE add hits, cap, expiry and rewind match the source ("+frameCount+" frames, "+numberCount+" numbers)");

            // ArenaAudio.update's event routing
            int scenarios=0;var buffer=new NativeEventSounds.Sound[NativeEventSounds.MaxSounds];
            string Describe(string fn,string clip,string key,double gain,bool positional)=>fn+":"+clip+":"+(key??"-")+":"+gain.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+":"+(positional?"3d":"2d");
            foreach(var scenario in json.GetProperty("sounds").EnumerateArray())
            {
                string name=scenario.GetProperty("name").GetString();var got=new List<string>();
                foreach(var u in scenario.GetProperty("unity").EnumerateArray())
                {
                    string Text(string field)=>u.TryGetProperty(field,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
                    bool Flag(string field)=>u.TryGetProperty(field,out var v)&&v.ValueKind==JsonValueKind.True;
                    float Number(string field)=>u.TryGetProperty(field,out var v)&&v.ValueKind==JsonValueKind.Number?v.GetSingle():0;
                    var context=new NativeEventSounds.Context{Kind=Kind(Text("Kind")),Detail=Text("Detail"),SourceSpec=Text("SourceSpec"),Material=Text("Material"),Amount=Number("Amount"),Radius=Number("Radius"),
                        TargetMe=Flag("TargetMe"),SourceMe=Flag("SourceMe"),TargetPilot=Flag("TargetPilot"),Ally=Flag("Ally"),Pve=Flag("Pve"),Supernova=Flag("Supernova"),Saw=Flag("Saw"),Shadowed=Flag("Shadowed"),BlinkStrike=Flag("BlinkStrike")};
                    int count=NativeEventSounds.Route(context,buffer);
                    for(int i=0;i<count;i++){var s=buffer[i];Check(s.Rate==0,name+" rate");got.Add(Describe(s.Tone?"tone":s.Gate!=null?"cue":"play",s.Clip,s.Gate,s.Gain,s.Positional));}
                }
                // A source event Unity derives from state (NativeAudio): its fixed sound.
                if(scenario.TryGetProperty("derived",out var derived)){Check(derived.GetString()=="SpikeRise",name+" derived");var s=NativeEventSounds.SpikeRise;Check(s.Rate==0&&!s.Tone,name+" rate");got.Add(Describe(s.Gate!=null?"cue":"play",s.Clip,s.Gate,s.Gain,s.Positional));}
                var want=scenario.GetProperty("expected").EnumerateArray().Select(c=>{Check(c.GetProperty("rate").GetDouble()==0,name+" source rate");if(c.GetProperty("fn").GetString()=="tone")Check(c.GetProperty("hit").GetInt32()==5&&c.GetProperty("move").GetBoolean()&&c.GetProperty("superMove").GetBoolean(),name+" tone");
                    return Describe(c.GetProperty("fn").GetString(),c.GetProperty("name").GetString(),c.GetProperty("key").ValueKind==JsonValueKind.String?c.GetProperty("key").GetString():null,c.GetProperty("gain").GetDouble(),c.GetProperty("positional").GetBoolean());}).OrderBy(x=>x,StringComparer.Ordinal).ToList();
                got.Sort(StringComparer.Ordinal);Check(got.SequenceEqual(want),name+": ["+string.Join(", ",got)+"] != ["+string.Join(", ",want)+"]");scenarios++;
            }
            // Every kind routes within the buffer; the PR #66 kinds without a source cue stay silent by default.
            foreach(EventKind kind in Enum.GetValues(typeof(EventKind)))Check(NativeEventSounds.Route(new NativeEventSounds.Context{Kind=kind},buffer)<=NativeEventSounds.MaxSounds,"route "+kind);
            passed.Add("event feedback: ArenaAudio.update's event cues match the source for "+scenarios+" scenarios across the PR #66 kinds");

            // stage-spikes-rise (stage-hazards.ts:21) derived from the NativeHazards clock: same ticks, beds and positions.
            {
                var spikes=json.GetProperty("spikes");int hz=spikes.GetProperty("hz").GetInt32();var rises=new NativeSpikeRises();var got=new List<(int tick,int bed)>();
                foreach(var seg in spikes.GetProperty("segments").EnumerateArray())
                {
                    bool fight=seg.GetProperty("fight").GetBoolean();double fightStart=seg.GetProperty("fightStart").GetDouble();
                    for(int tick=seg.GetProperty("from").GetInt32();tick<seg.GetProperty("to").GetInt32();tick++){double time=tick/(double)hz;int rose=rises.Step("spikevault",fight&&time-fightStart>=2,time);for(int bed=0;bed<NativeSpikeRises.Beds;bed++)if((rose&1<<bed)!=0)got.Add((tick,bed));}
                }
                var want=spikes.GetProperty("rises").EnumerateArray().ToList();Check(got.Count==want.Count&&want.Count>0,"spike rises: "+got.Count+" != "+want.Count);
                for(int i=0;i<got.Count;i++){var w=want[i];var bedAt=NativeSpikeRises.Position(got[i].bed);Check(got[i].tick==w.GetProperty("tick").GetInt32()&&(bedAt-Vec(w.GetProperty("pos"))).Length<1e-6,"spike rise #"+i+": tick "+got[i].tick+" bed "+got[i].bed+" != tick "+w.GetProperty("tick").GetInt32());}
                Check(new NativeSpikeRises().Step("sawmill",true,10)==0,"spike rises off spikevault");
                passed.Add("event feedback: spike rises match StageHazards.step over "+want.Count+" rises (fight gate, round reset, bed positions)");
            }

            // Unity-only correlations: a block pulse's own blast, a teleport's beam, Blink Strike's teleport, the saw.
            V3 at=new V3(2,1,3),far=new V3(9,1,3);
            ArenaEvent E(int id,EventKind kind,int source,float time,V3 position,string detail=null,float radius=0,float amount=0,int target=0)=>new ArenaEvent{Id=id,Kind=kind,Source=source,Time=time,Position=position,From=position,Detail=detail,Radius=radius,Amount=amount,Target=target};
            var pulse=new List<ArenaEvent>{E(1,EventKind.BlockPulse,3,1,at,radius:3),E(2,EventKind.Explosion,3,1,at,"chip",3),E(3,EventKind.Explosion,4,1,at,"chip",3),E(4,EventKind.Explosion,3,1.0167f,at,"chip",3),E(5,EventKind.Explosion,3,1,far,"chip",3)};
            Check(NativeEventFeedback.PulseBlast(pulse,1)&&!NativeEventFeedback.PulseBlast(pulse,2)&&!NativeEventFeedback.PulseBlast(pulse,3)&&!NativeEventFeedback.PulseBlast(pulse,4)&&!NativeEventFeedback.PulseBlast(pulse,0),"block pulse blast");
            var blink=new List<ArenaEvent>{E(1,EventKind.Teleport,2,1,at),E(2,EventKind.Arc,0,1,at,radius:1),E(3,EventKind.Arc,0,1,new V3(2,1.02f,3),radius:1),E(4,EventKind.Explosion,2,1,at,"ability",3),E(5,EventKind.Arc,0,1,far,radius:.15f),E(6,EventKind.Teleport,5,1,far),E(7,EventKind.Explosion,5,1,far,"chip",3),E(8,EventKind.Teleport,6,2,far,"blink-strike")};
            Check(NativeEventFeedback.TeleportBeam(blink,1)&&NativeEventFeedback.TeleportBeam(blink,2)&&NativeEventFeedback.TeleportBeam(blink,4)==true&&!NativeEventFeedback.TeleportBeam(blink,0),"teleport beam");
            var lone=new List<ArenaEvent>{E(1,EventKind.Arc,0,1,far,radius:.15f)};Check(!NativeEventFeedback.TeleportBeam(lone,0),"a lone arc is no teleport beam");
            Check(NativeEventFeedback.BlinkStrike(blink,0)&&!NativeEventFeedback.BlinkStrike(blink,5)&&NativeEventFeedback.BlinkStrike(blink,7)&&!NativeEventFeedback.BlinkStrike(blink,1),"blink strike teleport");
            double t=12.3;float sawX=(float)(Math.Sin(t*.65)*17);
            Check(NativeEventFeedback.SawHit("sawmill",t,new V3(sawX+1,1,-8.3f))&&NativeEventFeedback.SawHit("sawmill",t,new V3(-(float)(Math.Sin(t*.65)*17),1.2f,8))&&!NativeEventFeedback.SawHit("sawmill",t,new V3(sawX+3,1,-8))&&!NativeEventFeedback.SawHit("lavaworks",t,new V3(sawX,1,-8)),"saw contact");
            // PvE "under attack" (pve/tracks.ts AttackedTrack): any hit on me with damage, burn and poison ticks included.
            Check(NativeEventFeedback.Attacked(E(1,EventKind.BurnTick,2,1,at,amount:.5f,target:1),1,1.5f,1.2f)&&NativeEventFeedback.Attacked(E(1,EventKind.Hit,2,1,at,amount:5,target:1),1,2.1f,1.2f)&&!NativeEventFeedback.Attacked(E(1,EventKind.Hit,2,1,at,amount:5,target:1),1,2.2f,1.2f)
                &&!NativeEventFeedback.Attacked(E(1,EventKind.Heal,1,1,at,amount:5,target:1),1,1.1f,1.2f)&&!NativeEventFeedback.Attacked(E(1,EventKind.Hit,2,1,at,amount:.04f,target:1),1,1.1f,1.2f)&&!NativeEventFeedback.Attacked(E(1,EventKind.Hit,2,1,at,amount:5,target:3),1,1.1f,1.2f),"under attack");
            // The legacy hit burst keeps exactly the kinds it was written for, plus collapse (event-vfx.ts:29 bursts it like a hit).
            foreach(EventKind kind in Enum.GetValues(typeof(EventKind)))Check(NativeEventFeedback.HitBurst(kind)==(kind==EventKind.Hit||kind==EventKind.Headshot||kind==EventKind.Block||kind==EventKind.Death||kind==EventKind.Break||kind==EventKind.Impact||kind==EventKind.Collapse),"burst "+kind);
            // Pan on the Unity camera's right (left-handed), falloff and rate as ArenaAudio.play.
            Check(Near(NativeEventSounds.Pan(new V3(5,0,0),0),.85,1e-6)&&Near(NativeEventSounds.Pan(new V3(-5,0,0),0),-.85,1e-6)&&Near(NativeEventSounds.Pan(new V3(0,0,-5),(float)(Math.PI/2)),.85,1e-6)&&Near(NativeEventSounds.Pan(new V3(1,0,0),0),.5,1e-6)&&NativeEventSounds.Pan(new V3(0,0,6),0)==0,"pan");
            Check(Near(NativeEventSounds.Falloff(1),1,1e-7)&&Near(NativeEventSounds.Falloff(12),1/2.3,1e-6)&&NativeEventSounds.Rate("hit",.9f)==1&&NativeEventSounds.Rate("ui",0)==1&&Near(NativeEventSounds.Rate("stone",0),.97,1e-7)&&Near(NativeEventSounds.Rate("stone",1),1.03,1e-6),"falloff and rate");
            passed.Add("event feedback: block-pulse blast, teleport beam, Blink Strike and saw correlations; under-attack ticks; legacy burst kinds; pan, falloff and rate");
            return passed;
        }
    }
}
#endif
