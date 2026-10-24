#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Footsteps and the zombies' voices and footfalls (owner 2026-10-15: "can we add sounds for footsteps and zombies?"; Core
    // NativeFootfalls, NativeZombieVoice, NativeCreatureSynth; Runtime NativeFootfallPresentation, NativeFootstepAudio,
    // NativeZombieAudio): every key in the bank contract with its cap at least the request's longest (docs/audio/sound-requests.json);
    // the surfaces (map floors, blocks underfoot, every Expedition block material); a pilot's steps are its drawn gait's plants (the
    // gun and magic gaits at a walk, a run and a sprint, at 60 and 144 Hz), none in the air or a roll, one landing; the tracker's
    // gaps, the distance stride with no gait, teleports and rewinds; the crowd mix (mine always, four others a frame at most,
    // each family's rate, range, staleness, loudest first, the same on two peers); every zombie kind's footfalls from its gait (the
    // crawler's hands, the shambler's dragged foot); the voices' per-add cooldown, caps, moan gap, range, preemption, rewind and the
    // seeded calls identical on two peers; the built-in takes (deterministic, finite, within caps and their level); no allocation per
    // frame; the hooks.
    internal static class FootfallAudioChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Footfall audio: "+what);count++;}
        public static void Run(string root)
        {
            count=0;var log=new List<string>();
            Keys(root,log);Surfaces(root,log);PilotSteps(log);Tracker(log);Mix(log);ZombieSteps(log);Voices(log);Synth(log);Allocation(log);Source(root,log);
            Console.WriteLine("PASS footfall audio ("+count+" checks: "+string.Join("; ",log)+")");
        }

        // ---------- keys and the request manifest ----------
        private static readonly string[] Maps={"moonlit","foundry","freight","reactor","siren","outpost","citadel","canal","frozen","forge","sawmill","factory","cybercity","brickfall","expedition-city","expedition-ward-zero",null,"unknown"};
        private static readonly string[] Materials={null,"stone","metal","wood","timber","brick","foundation","glass","snow","gravel","dirt","concrete","steel"};
        private static void Keys(string root,List<string> log)
        {
            var keys=NativeSoundKeys.All.ToDictionary(k=>k.Key);bool Known(string k)=>k!=null&&keys.ContainsKey(k);
            for(var c=NativeZombieCall.Moan;c<=NativeZombieCall.Death;c++){Check(Known(NativeZombieVoice.Key(c)),c+" key");var s=NativeZombieVoice.StandIn(c);Check(s==null||Known(s),c+" stand-in "+s);Check(NativeCreatureSynth.Has(NativeZombieVoice.Key(c)),c+" has a built-in take");}
            Check(NativeZombieVoice.Key(NativeZombieCall.None)==null&&NativeZombieVoice.StandIn(NativeZombieCall.Death)==null,"no key for no call; the death's old grunt is its event's");
            foreach(var k in new[]{NativeZombieSteps.Shuffle,NativeZombieSteps.Running,NativeZombieSteps.Heavy,NativeZombieSteps.Crawling})
            {
                Check(Known(k)&&keys[k].Priority==NativeSoundPriority.Low&&keys[k].Space==NativeSoundSpace.Positional,k+" a low positional key");Check(NativeCreatureSynth.Has(k),k+" built-in take");
                foreach(var m in Maps)Check(Known(NativeZombieSteps.StandIn(k,NativeFootsteps.Surface(m))),k+" stand-in on "+m);
            }
            foreach(var m in Maps)foreach(var mat in Materials){var s=NativeFootsteps.Surface(m,mat);Check(Known(s)&&s.StartsWith("move.step.",StringComparison.Ordinal)&&NativeCreatureSynth.Has(s),m+"/"+mat+" step "+s);}
            Check(Known("move.land")&&NativeCreatureSynth.Has("move.land"),"the landing has a built-in take");
            Check(NativeCreatureSynth.Keys.All(Known)&&NativeCreatureSynth.Keys.Distinct().Count()==NativeCreatureSynth.Keys.Length&&keys.Keys.Where(k=>k.StartsWith("pve.zombie.",StringComparison.Ordinal)).All(NativeCreatureSynth.Has),"every zombie key has a built-in take ("+NativeCreatureSynth.Keys.Length+" keys)");
            string path=Path.Combine(root,"../../docs/audio/sound-requests.json");
            if(!File.Exists(path)){log.Add("request manifest not in this tree (skipped)");return;}
            using var doc=JsonDocument.Parse(File.ReadAllText(path));var top=doc.RootElement;Check(top.GetProperty("schema").GetString()=="chip-arena.sound-requests/1","request schema");
            int n=0,files=0;
            foreach(var s in top.GetProperty("sounds").EnumerateArray())
            {
                string key=s.GetProperty("key").GetString();Check(Known(key),"requested key "+key+" is in the bank contract");var d=keys[key];float max=s.GetProperty("targetSeconds").GetProperty("max").GetSingle(),min=s.GetProperty("targetSeconds").GetProperty("min").GetSingle();
                int countN=s.GetProperty("count").GetInt32();string cat=s.GetProperty("category").GetString();
                Check(d.Cap+1e-4f>=max&&min>0&&min<max,key+" cap "+d.Cap+" s holds the request's "+min+"-"+max+" s");Check(countN>=1&&countN<=NativeSoundKeys.MaxClips,key+" count "+countN);
                Check(d.Space==NativeSoundSpace.Positional&&(cat=="voice")==NativeCreatureSynth.Voice(key)&&(cat=="step"||cat=="voice"),key+" "+cat+" positional");n++;files+=countN;
            }
            Check(n==17&&files==88,"17 keys, 88 files requested ("+n+", "+files+")");
            log.Add("keys: 14 zombie keys and 6 steps and the landing with built-in takes, stand-ins on every map; the "+n+" requested keys ("+files+" files) fit their caps");
        }
        private static void Surfaces(string root,List<string> log)
        {
            Check(NativeFootsteps.Surface("expedition-city",null)=="move.step.concrete"&&NativeFootsteps.Surface("expedition-city","foundation")=="move.step.concrete","the town's asphalt is concrete");
            Check(NativeFootsteps.Surface("expedition-ward-zero","stone")=="move.step.concrete"&&NativeFootsteps.Surface("siren","stone")=="move.step.concrete"&&NativeFootsteps.Surface("expedition-ward-zero","metal")=="move.step.metal"&&NativeFootsteps.Surface("siren",null)=="move.step.concrete","Ward Zero's tile slab is concrete, its metal decks metal");
            Check(NativeFootsteps.Surface("foundry","stone")=="move.step.metal"&&NativeFootsteps.Surface("moonlit",null)=="move.step.snow"&&NativeFootsteps.Surface("moonlit","metal")=="move.step.metal"&&NativeFootsteps.Surface("citadel","stone")=="move.step.stone"&&NativeFootsteps.Surface("canal",null)=="move.step.wood"&&NativeFootsteps.Surface("expedition-city","wood")=="move.step.wood","map floors and the blocks' own materials");
            var maps=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),new JsonSerializerOptions{IncludeFields=true});
            var seen=new SortedSet<string>();
            foreach(var m in maps.maps)foreach(var b in m.blocks){var s=NativeFootsteps.Surface(m.id,b.material);Check(s.StartsWith("move.step.",StringComparison.Ordinal),m.id+" "+b.material);if(m.id.StartsWith("expedition",StringComparison.Ordinal))seen.Add(m.id+":"+b.material+"="+s.Substring(10));}
            log.Add("surfaces: "+string.Join(", ",seen));
        }

        // ---------- a pilot's steps are its gait's plants ----------
        private sealed class Gaited
        {
            public readonly Actor A;public readonly GunMotion G;public readonly MagicMotion M;public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();private readonly NativeJuggleTrack juggle=new NativeJuggleTrack();private readonly float[] channels=new float[13];public Vector3 Root;
            public Gaited(string spec)
            {
                bool gun=spec=="pistol";A=new Actor{Id=7,SourceSpec=spec,Weapon=gun?WeaponId.Pistol:WeaponId.Sword,MagicVisual=gun?0:1,Grounded=true,Hp=100,CastRate=1,RollDirection=new V3(0,0,1),Ammo=gun?GunRules.Get(WeaponId.Pistol).Magazine:0};
                if(gun)G=new GunMotion{JumpLayer=new NativeJumpMotion(),SprintLayer=new NativeSprintMotion(),Locomotion=new NativeRangedLocomotion()};else M=new MagicMotion{JumpLayer=new NativeJumpMotion(),SprintLayer=new NativeSprintMotion(),Locomotion=new NativeRangedLocomotion()};
            }
            public int Step(float t,float dt)
            {
                Root+=new Vector3(A.Velocity.x,0,A.Velocity.z)*dt;var rig=G!=null?G.Rig:M.Rig;rig.GroupPosition=new Vector3(-Root.X,.865f,Root.Z);rig.GroupYaw=0;
                if(G!=null){G.Pose(A,t,Pose,false,juggle,out _);G.Finish(Pose,t);return (G.Planted(true)?1:0)|(G.Planted(false)?2:0);}
                M.Pose(A,t,Pose,channels,0,false,juggle,out _);M.Finish(Pose,t);return (M.Planted(true)?1:0)|(M.Planted(false)?2:0);
            }
        }
        private static void PilotSteps(List<string> log)
        {
            var rates=new List<string>();
            foreach(var spec in new[]{"pistol","fire"})
            {
                var perSecond=new Dictionary<string,float>();
                foreach(int hz in new[]{60,144})foreach(var (name,speed,sprint) in new[]{("walk",3f,false),("run",6.8f,false),("sprint",9.18f,true)})
                {
                    var p=new Gaited(spec);var tracker=new NativeStepTracker();float dt=1f/hz;int prev=0,edges=0,steps=0,warm=(int)(1.5f*hz),frames=(int)(7.5f*hz);
                    for(int f=0;f<frames;f++)
                    {
                        float t=1+f*dt;p.A.Velocity=new V3(0,0,speed);p.A.Sprinting=sprint;int c=p.Step(t,dt);
                        int planted=tracker.Step(c,true,false,p.Root.X,p.Root.Z,speed,sprint,t,out bool land,out _);int edge=c&~prev;prev=c;
                        Check(!land,spec+" "+name+" never lands on the flat");Check((planted&~edge)==0,spec+" "+name+" @"+hz+" Hz: a step is a foot that just planted (frame "+f+")");
                        if(f<warm)continue;if(edge!=0)edges++;if(planted!=0)steps++;
                    }
                    float secs=(frames-warm)*dt,rate=steps/secs;
                    Check(edges>0&&steps>=edges*.9f-1&&steps<=edges,spec+" "+name+" @"+hz+" Hz: "+steps+" steps for "+edges+" plants");
                    Check(rate>1.5f&&rate<8f,spec+" "+name+" @"+hz+" Hz cadence "+rate.ToString("0.0")+"/s");
                    perSecond[name+hz]=rate;
                }
                foreach(var name in new[]{"walk","run","sprint"})Check(Math.Abs(perSecond[name+"60"]-perSecond[name+"144"])<=.12f*perSecond[name+"60"]+.15f,spec+" "+name+" cadence holds at 60 and 144 Hz ("+perSecond[name+"60"].ToString("0.0")+" / "+perSecond[name+"144"].ToString("0.0")+")");
                Check(perSecond["sprint60"]>=perSecond["walk60"],spec+" sprints at a quicker cadence than it walks");
                rates.Add(spec+" walk/run/sprint "+perSecond["walk60"].ToString("0.0")+"/"+perSecond["run60"].ToString("0.0")+"/"+perSecond["sprint60"].ToString("0.0")+" steps/s");
                // A jump: no step airborne, one landing, its plants silent; a roll is silent; standing still settles then stops.
                {
                    var p=new Gaited(spec);var tracker=new NativeStepTracker();float dt=1/60f;int lands=0,airSteps=0,quiet=0,after=0,rollSteps=0,standLate=0;float landAt=-1;
                    for(int f=0;f<900;f++)
                    {
                        float t=1+f*dt;bool air=t>=4&&t<4.6f,roll=t>=6&&t<6.5f,stand=t>=8;p.A.Grounded=!air;p.A.Velocity=new V3(0,air?(t<4.3f?4:-4):0,stand?0:6.8f);
                        if(roll&&p.A.RollAt<5.9f)p.A.RollAt=6;int c=p.Step(t,dt);
                        int planted=tracker.Step(c,!air,p.A.Rolling(t),p.Root.X,p.Root.Z,stand?0:6.8f,false,t,out bool land,out float airSeconds);
                        if(land){lands++;landAt=t;Check(Math.Abs(airSeconds-.6f)<.03f,spec+" airborne "+airSeconds);}
                        if(air&&planted!=0)airSteps++;if(landAt>0&&t-landAt<NativeStepTracker.LandQuiet&&planted!=0)quiet++;if(landAt>0&&t-landAt>.2f&&t<5.5f&&planted!=0)after++;
                        if(p.A.Rolling(t)&&planted!=0)rollSteps++;if(t>=9&&planted!=0)standLate++;
                    }
                    Check(lands==1&&airSteps==0&&quiet==0&&after>0,spec+": one landing, no step airborne or under it, steps resume ("+lands+", "+airSteps+", "+quiet+", "+after+")");
                    Check(rollSteps==0,spec+": a roll steps silently");Check(standLate==0,spec+": a pilot standing still stops stepping");
                }
            }
            log.Add("pilot steps from the drawn gait's plants: "+string.Join(", ",rates)+"; at 60 and 144 Hz; a jump lands once, a roll and a stand are silent");
        }
        private static void Tracker(List<string> log)
        {
            // No gait: the distance stride (today's cadence), nothing below the walk.
            foreach(var (speed,sprint) in new[]{(6.8f,false),(9.18f,true),(1f,false)})
            {
                var t0=new NativeStepTracker();int steps=0;float x=0;for(int f=0;f<600;f++){x+=speed/60;steps+=t0.Step(-1,true,false,0,x,speed,sprint,1+f/60f,out _,out _)!=0?1:0;}
                float expect=speed<NativeFootsteps.MinSpeed?0:10*speed/NativeFootsteps.StrideFor(sprint);Check(Math.Abs(steps-expect)<=1.5f,"no gait at "+speed+" m/s: "+steps+" steps ("+expect.ToString("0.0")+")");
            }
            // A flickering contact: each foot at most once a FootGap, any two steps AnyGap apart.
            var tf=new NativeStepTracker();int flick=0;float last=-1,minGap=1;
            for(int f=0;f<600;f++){float t=1+f/60f;int c=(f&1)!=0?3:0;int s=tf.Step(c,true,false,0,f*.1f/60,6,false,t,out _,out _);if(s!=0){flick++;if(last>0)minGap=Math.Min(minGap,t-last);last=t;}}
            Check(flick<=10/NativeStepTracker.FootGap*2&&minGap>=NativeStepTracker.AnyGap-1e-4f,"a flickering gait steps at most once a foot gap ("+flick+", gap "+minGap.ToString("0.000")+")");
            // A teleport and a rewind re-seat without a step.
            var tt=new NativeStepTracker();tt.Step(0,true,false,0,0,6,false,1,out _,out _);Check(tt.Step(1,true,false,0,5,6,false,1.02f,out _,out _)==0,"a teleport re-seats");
            tt.Step(0,true,false,0,5,6,false,1.04f,out _,out _);Check(tt.Step(1,true,false,0,5.1f,6,false,.5f,out _,out _)==0,"a rewind re-seats");
            var ts=new NativeStepTracker();ts.Step(0,true,false,0,0,6,false,1,out _,out _);Check(ts.Step(1,true,false,0,.1f,6,false,1.02f,out _,out _)==1&&ts.Step(3,true,false,0,.2f,6,false,1.2f,out _,out _)==2,"plants: the left, then the right");
            log.Add("tracker: the distance stride with no gait (today's cadence), a foot gap of "+NativeStepTracker.FootGap+" s, teleports and rewinds re-seat");
        }

        // ---------- the crowd mix ----------
        private static void Mix(List<string> log)
        {
            // Every step, a pilot's or a zombie's, sits under the step trim (owner 2026-10-19: quieter footsteps); landings keep theirs.
            var walk=new NativeFootfall{Speed=2f};var hop=new NativeFootfall{Land=true,Air=1f};var shamble=new NativeFootfall{Zombie=true,Kind=NativeHordeKinds.Shambler,Speed=1.4f};
            Check(NativeFootsteps.StepTrim>.5f&&NativeFootsteps.StepTrim<1&&Math.Abs(NativeStepMix.Loudness(walk,false)-NativeFootsteps.StepGain(NativeStepGait.Walk)*NativeFootsteps.StepTrim)<1e-5f
                &&Math.Abs(NativeStepMix.Loudness(hop,false)-NativeFootsteps.LandGain(1f))<1e-5f
                &&Math.Abs(NativeStepMix.Loudness(shamble,false)-NativeZombieSteps.Gain(NativeZombieSteps.Key(shamble.Kind,false,0),shamble.Kind,false)*NativeFootsteps.StepTrim)<1e-5f,
                "steps sit "+NativeFootsteps.StepTrim.ToString("0.00")+"x under the step trim (pilots and zombies); landings keep their gain");
            var q=new NativeFootfallQueue();for(int i=0;i<NativeFootfallQueue.Capacity+2;i++)q.Push(new NativeFootfall{Id=i});Check(q.Count==NativeFootfallQueue.Capacity&&q.Dropped==2,"a full queue drops");q.Clear();
            var mix=new NativeStepMix();var other=new NativeStepMix();int frames=600,mine=0,mineSteps=0,zombiePicks=0,pilotPicks=0,maxOthers=0,far=0;bool sorted=true,same=true;
            for(int f=0;f<frames;f++)
            {
                float t=1+f/60f;q.Clear();
                if(f%15==0){q.Push(new NativeFootfall{Id=3,Speed=6.8f,X=0,Y=0,Z=0,Time=t,View=true});mineSteps++;}
                for(int z=0;z<60;z++){float d=2+z*.65f;q.Push(new NativeFootfall{Id=10000+z,Zombie=true,Kind=z%7,X=d,Z=0,Speed=1.4f,Run=0,Time=t});}
                for(int p=0;p<6;p++)q.Push(new NativeFootfall{Id=20+p,Speed=6.8f,X=-5-p*10,Time=t});
                q.Push(new NativeFootfall{Id=99,Speed=6.8f,X=3,Time=t-1});
                int n=mix.Choose(q,0,0,0,3,1/60f),m=other.Choose(q,0,0,0,3,1/60f);same&=n==m;
                int others=0;float lastKey=float.MaxValue;
                for(int i=0;i<n;i++)
                {
                    var pk=mix[i];same&=i<m&&other[i].Index==pk.Index;var ff=q[pk.Index];if(pk.Mine){mine++;continue;}others++;
                    if(ff.Zombie)zombiePicks++;else pilotPicks++;if(ff.Id==99)far++;if(ff.Zombie&&ff.X>NativeStepMix.ZombieRange)far++;
                    float key=pk.Gain*NativeEventSounds.Falloff(Math.Abs(ff.X));if(key>lastKey+1e-5f)sorted=false;lastKey=key;
                }
                maxOthers=Math.Max(maxOthers,others);
            }
            float secs=frames/60f;
            Check(mine==mineSteps,"my steps always play ("+mine+" of "+mineSteps+")");Check(maxOthers<=NativeStepMix.MaxPerFrame,"at most four others a frame ("+maxOthers+")");
            Check(zombiePicks<=NativeStepMix.ZombieRate*secs+NativeStepMix.Burst+1&&zombiePicks>=NativeStepMix.ZombieRate*secs*.8f,"a 60-zombie crowd plays "+zombiePicks+" footfalls in "+secs+" s (rate "+NativeStepMix.ZombieRate+"/s)");
            Check(pilotPicks<=NativeStepMix.PilotRate*secs+NativeStepMix.Burst+1,"pilots within their rate ("+pilotPicks+")");Check(far==0,"nothing stale or out of range");Check(sorted,"loudest first");Check(same,"two peers' mixes pick the same");
            log.Add("mix: mine always, at most "+maxOthers+" others a frame; 60 zombies stepping every frame play "+(zombiePicks/secs).ToString("0.0")+" footfalls/s, 6 pilots "+(pilotPicks/secs).ToString("0.0")+"/s");
        }

        // ---------- every zombie kind steps from its gait ----------
        private static void ZombieSteps(List<string> log)
        {
            var info=new List<string>();var p=new CapturedRig.Pose();
            for(int kind=0;kind<NativeHordeKinds.Count;kind++)
            {
                var row=NativeHordeKinds.ProposedRow(kind);float sc=NativeHordeKinds.Scale[kind];
                foreach(var (name,speed) in new[]{("walk",row.walk),("run",row.run)})
                {
                    int id=10500+kind*10+(name=="run"?1:0);var g=new NativeHordeGait();int falls=0,hands=0,feet=0,dragged=0;var keys=new HashSet<string>();float z=0,v=speed/sc;
                    for(int f=0;f<480;f++)
                    {
                        float t=1+f/60f;z+=v/60;
                        g.Pose(new HordeMotionInput{Kind=kind,Id=id,Seed=NativeHordeKinds.Seed(id),Time=t,Root=new Vector3(0,NativeHordeKinds.DrawRoot(kind,false)/sc,z),Velocity=new Vector3(0,0,v),Health=1,AttackAge=float.NaN},p);
                        if(f<60||g.Landed==0)continue;
                        for(int i=0;i<4;i++){if((g.Landed&(1<<i))==0||!NativeZombieSteps.Sounds(g.Quad,i))continue;falls++;if(i>=2)hands++;else feet++;if(g.Dragged(i))dragged++;keys.Add(NativeZombieSteps.Key(kind,g.Quad,NativeHordeGait.RunBlend(kind,g.Speed)));}
                    }
                    float rate=falls/7f;Check(rate>.5f&&rate<10,NativeHordeKinds.Names[kind]+" "+name+": "+rate.ToString("0.0")+" footfalls/s");
                    if(kind==NativeHordeKinds.Crawler)Check(hands==falls&&g.Quad,"the crawler's footfalls are a hand's pull a cycle");else Check(feet==falls,NativeHordeKinds.Names[kind]+" steps on its feet");
                    if(kind!=NativeHordeKinds.Shambler)Check(dragged==0,NativeHordeKinds.Names[kind]+" drags no foot");
                    string want=kind==NativeHordeKinds.Crawler?NativeZombieSteps.Crawling:kind==NativeHordeKinds.Brute||kind==NativeHordeKinds.Bloater||kind==NativeHordeKinds.Hulk?NativeZombieSteps.Heavy:null;
                    if(want!=null)Check(keys.Count==1&&keys.Contains(want),NativeHordeKinds.Names[kind]+" "+name+" plays "+want+" ("+string.Join(",",keys)+")");
                    if(kind==NativeHordeKinds.Shambler&&name=="walk")Check(keys.SetEquals(new[]{NativeZombieSteps.Shuffle}),"the shambler shuffles at a walk");
                    if(kind==NativeHordeKinds.Runner&&name=="run")Check(keys.Contains(NativeZombieSteps.Running),"the runner slaps at a run");
                    if(name=="walk")info.Add(NativeHordeKinds.Names[kind]+" "+rate.ToString("0.0")+"/s");
                }
            }
            // The shambler's dragged foot is one of its two, by its seed.
            int draggers=0;for(int id=11000;id<11040;id++){var g=new NativeHordeGait();var p2=new CapturedRig.Pose();for(int f=0;f<5;f++)g.Pose(new HordeMotionInput{Kind=0,Id=id,Seed=NativeHordeKinds.Seed(id),Time=1+f/60f,Root=new Vector3(0,.85f,f*.02f),Velocity=new Vector3(0,0,1.2f),Health=1,AttackAge=float.NaN},p2);Check(!(g.Dragged(0)&&g.Dragged(1))&&!g.Dragged(2)&&!g.Dragged(3),"at most one dragged foot");if(g.Dragged(0)||g.Dragged(1))draggers++;}
            Check(draggers>5&&draggers<40,"some shamblers drag a foot ("+draggers+" of 40)");
            Check(NativeZombieSteps.Gain(NativeZombieSteps.Heavy,NativeHordeKinds.Hulk,false)>NativeZombieSteps.Gain(NativeZombieSteps.Heavy,NativeHordeKinds.Brute,false)&&NativeZombieSteps.Gain(NativeZombieSteps.Shuffle,0,true)>NativeZombieSteps.Gain(NativeZombieSteps.Shuffle,0,false),"the Hulk stomps loudest; a drag scrapes louder than a shuffle");
            Check(NativeZombieSteps.Rate(NativeHordeKinds.Hulk,1)<NativeZombieSteps.Rate(NativeHordeKinds.Brute,1)&&NativeZombieSteps.Rate(NativeHordeKinds.Brute,1)<NativeZombieSteps.Rate(NativeHordeKinds.Runner,1),"heavier kinds step lower");
            log.Add("zombie footfalls from every kind's gait at a walk: "+string.Join(", ",info));
        }

        // ---------- voices ----------
        private static void Voices(List<string> log)
        {
            // The seeded hunting calls: pure, sparse (6 slots in 10), inside their slot.
            int voiced=0,slots=0;
            for(uint seed=1;seed<60;seed++)
            {
                float period=NativeZombieVoice.CallPeriod(seed);Check(period>=NativeZombieVoice.CallPeriodMin&&period<=NativeZombieVoice.CallPeriodMin+NativeZombieVoice.CallPeriodSpan,"call period");float prev=float.NaN;
                for(float t=0;t<300;t+=period){slots++;float at=NativeZombieVoice.CallAt(seed,t+.01f);if(float.IsNaN(at))continue;voiced++;Check(at==NativeZombieVoice.CallAt(seed,t+.01f)&&!(at>t+.01f+period),"a call is a pure function of seed and clock");if(!float.IsNaN(prev))Check(at>=prev,"calls in order");prev=at;}
            }
            float share=voiced/(float)slots;Check(share>.5f&&share<.7f,"about six slots in ten call ("+share.ToString("0.00")+")");
            Check(NativeZombieVoice.Hunting(NativeHordeKinds.Runner)==NativeZombieCall.Snarl&&NativeZombieVoice.Hunting(NativeHordeKinds.Shambler)==NativeZombieCall.Groan,"a runner snarls, the rest groan");
            Check(NativeZombieVoice.Windup(NativeHordeKinds.Attack.Spit)==NativeZombieCall.Gurgle&&NativeZombieVoice.Strike(NativeHordeKinds.Attack.Spit)==NativeZombieCall.Spit&&NativeZombieVoice.Windup(NativeHordeKinds.Attack.Smash)==NativeZombieCall.Grunt&&NativeZombieVoice.Windup(NativeHordeKinds.Attack.Swipe)==NativeZombieCall.Attack&&NativeZombieVoice.Windup(NativeHordeKinds.Attack.Belly)==NativeZombieCall.Gurgle&&NativeZombieVoice.Strike(NativeHordeKinds.Attack.Swipe)==NativeZombieCall.None,"wind-ups: spits and bellies gurgle, smashes grunt, the rest snarl; the spit leaves at the strike");
            Check(NativeZombieVoice.Rate(5,NativeHordeKinds.Hulk)<NativeZombieVoice.Rate(5,NativeHordeKinds.Shambler)&&NativeZombieVoice.Rate(5,NativeHordeKinds.Shambler)!=NativeZombieVoice.Rate(6,NativeHordeKinds.Shambler),"voices pitched by kind and seed");
            // Per add: one call at a time, hurts spaced; the caps; the moan gap; range; preemption; rewind.
            var v=new NativeZombieVoices();
            Check(v.Admit(1,NativeZombieCall.Groan,10,5)&&!v.Admit(1,NativeZombieCall.Hurt,10.2f,5)&&v.Admit(1,NativeZombieCall.Hurt,10+NativeZombieVoice.Seconds(NativeZombieCall.Groan)+NativeZombieVoices.AddGap+.01f,5),"one call at a time per add");
            Check(v.Admit(2,NativeZombieCall.Hurt,20,5)&&!v.Admit(2,NativeZombieCall.Hurt,20.45f,5)&&v.Admit(2,NativeZombieCall.Hurt,20.8f,5),"hurts at most every "+NativeZombieVoices.HurtGap+" s");
            Check(v.Admit(2,NativeZombieCall.Death,20.81f,5),"a death speaks over its own busy voice");
            v.Clear();int calm=0;for(int i=0;i<6;i++)if(v.Admit(100+i,NativeZombieCall.Groan,30,5))calm++;Check(calm==NativeZombieVoices.MaxCalm,"at most three calm calls at once ("+calm+")");
            int all=calm;for(int i=0;i<6;i++)if(v.Admit(200+i,NativeZombieCall.Attack,30,5))all++;Check(all==NativeZombieVoices.MaxAll&&v.Sounding(30)==NativeZombieVoices.MaxAll,"at most six calls at once ("+all+")");
            Check(v.Admit(300,NativeZombieCall.Death,30.01f,5)&&v.Sounding(30.01f)==NativeZombieVoices.MaxAll,"a death takes a calm call's slot when full");
            Check(!v.Admit(301,NativeZombieCall.Hurt,30.02f,5),"a hurt waits when full");
            v.Clear();Check(v.Admit(400,NativeZombieCall.Moan,40,5)&&!v.Admit(401,NativeZombieCall.Moan,40.5f,5)&&v.Admit(401,NativeZombieCall.Moan,40+NativeZombieVoices.MoanGap+.01f,5),"moans a moan gap apart");
            Check(!v.Admit(402,NativeZombieCall.Shriek,41,NativeZombieVoices.Range+1)&&!v.Admit(403,NativeZombieCall.None,41,1),"nothing past range; no call");
            v.Admit(500,NativeZombieCall.Groan,50,5);Check(!v.Admit(500,NativeZombieCall.Groan,50.1f,5)&&v.Admit(500,NativeZombieCall.Groan,10,5),"a rewind forgets the voices");
            Check(v.First(7,NativeZombieVoices.AttackSlot,3)&&!v.First(7,NativeZombieVoices.AttackSlot,3)&&v.First(7,NativeZombieVoices.AttackSlot,4)&&v.First(7,NativeZombieVoices.StrikeSlot,4),"each moment once per slot");
            v.Clear();v.Seen(1,2,1);v.Seen(2,3,1);for(float t=1;t<6;t+=.5f)v.Seen(2,3,t);Check(v.Tracked==1&&v.KindOf(2)==3&&v.KindOf(1)==-1,"adds gone for "+NativeZombieVoices.Forget+" s are forgotten");
            // Two peers: the same horde, the same calls heard (a listener's own cap and cooldown are a function of the same inputs).
            string Run(){var z=new NativeZombieVoices();var heard=new List<string>();for(int f=0;f<60*90;f++){float t=1+f/60f;for(int a=0;a<40;a++){int id=10000+a;uint seed=NativeHordeKinds.Seed(id);z.Seen(id,a%7,t);float at=NativeZombieVoice.CallAt(seed,t);if(!float.IsNaN(at)&&t>=at&&t-at<.12f&&z.First(id,NativeZombieVoices.CallSlot,at)&&z.Admit(id,NativeZombieVoice.Hunting(a%7),t,2+a*.7f))heard.Add(id+"@"+at.ToString("0.000"));}}return string.Join(" ",heard);}
            string a1=Run(),a2=Run();int calls=a1.Split(' ').Length;Check(a1==a2&&calls>100,"two peers hear the same horde calls ("+calls+" in 90 s from 40 adds)");
            log.Add("voices: seeded calls in "+(share*100).ToString("0")+"% of 5-9 s slots, "+calls+" heard from a 40-add horde in 90 s on two peers alike; one call per add, 3 calm / 6 in all, moans "+NativeZombieVoices.MoanGap+" s apart, 32 m");
        }

        // ---------- the built-in takes ----------
        private static void Synth(List<string> log)
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();int takes=0;double worst=0;
            foreach(var key in NativeCreatureSynth.Keys)
            {
                Check(NativeSoundKeys.Find(key,out var def),key+" known");float[] first=null;
                for(int v=0;v<NativeCreatureSynth.Variants;v++)
                {
                    var a=NativeCreatureSynth.Samples(key,v);var b=NativeCreatureSynth.Samples(key,v);Check(a!=null&&a.SequenceEqual(b),key+"/"+v+" deterministic");takes++;
                    float seconds=a.Length/(float)NativeCreatureSynth.Rate;Check(seconds>.1f&&seconds<=def.Cap+.005f,key+"/"+v+" "+seconds.ToString("0.000")+" s within its "+def.Cap+" s cap");
                    Check(a.All(x=>!float.IsNaN(x)&&!float.IsInfinity(x)&&Math.Abs(x)<1)&&Math.Abs(a[0])<.02f&&Math.Abs(a[a.Length-1])<.02f,key+"/"+v+" finite, under full scale, quiet ends");
                    double level=NativeCreatureSynth.MomentaryMax(a),want=NativeCreatureSynth.Voice(key)?NativeCreatureSynth.VoiceLevel:key=="move.land"?NativeCreatureSynth.LandLevel:NativeCreatureSynth.StepLevel;worst=Math.Max(worst,Math.Abs(level-want));Check(Math.Abs(level-want)<1,key+"/"+v+" momentary max "+level.ToString("0.0")+" LUFS ("+want+")");
                    if(first==null)first=a;else Check(!first.SequenceEqual(a),key+" takes differ");
                }
            }
            Check(NativeCreatureSynth.Samples("ui.click",0)==null&&NativeCreatureSynth.Samples(null,0)==null,"other keys are not the synth's");
            log.Add("built-in takes: "+takes+" ("+NativeCreatureSynth.Keys.Length+" keys x "+NativeCreatureSynth.Variants+"), deterministic, within caps, within "+worst.ToString("0.0")+" LU of their family's level (the bank's measure), made in "+watch.ElapsedMilliseconds+" ms");
        }

        // ---------- no allocation per frame ----------
        private static void Allocation(List<string> log)
        {
            var q=new NativeFootfallQueue();var mix=new NativeStepMix();var voices=new NativeZombieVoices();var trackers=new NativeStepTracker[4];
            void Frame(int f)
            {
                float t=1+f/60f;q.Clear();
                for(int p=0;p<4;p++){int c=(f/12+p)%2==0?1:2;int s=trackers[p].Step(c,true,false,0,f*.11f,6.8f,false,t,out bool land,out float air);if(s!=0||land)q.Push(new NativeFootfall{Id=p+1,Speed=6.8f,X=p*3,Time=t,Land=land,Air=air});}
                for(int a=0;a<40;a++)
                {
                    int id=10000+a,kind=a%7;uint seed=NativeHordeKinds.Seed(id);if((f+a)%20==0)q.Push(new NativeFootfall{Id=id,Zombie=true,Kind=kind,X=a,Speed=1.3f,Time=t,Run=.2f});
                    voices.Seen(id,kind,t);float at=NativeZombieVoice.CallAt(seed,t),moan=NativeHordeRoam.MoanAt(seed,t);
                    if(!float.IsNaN(at)&&t>=at&&t-at<.12f&&voices.First(id,NativeZombieVoices.CallSlot,at))voices.Admit(id,NativeZombieVoice.Hunting(kind),t,a);
                    if(!float.IsNaN(moan)&&t>=moan&&t-moan<.12f)voices.Admit(id,NativeZombieCall.Moan,t,a);
                    if(f%90==a)voices.Admit(id,NativeZombieCall.Hurt,t,a);
                }
                int n=mix.Choose(q,0,0,0,1,1/60f);for(int i=0;i<n;i++){var pk=mix[i];var ff=q[pk.Index];if(ff.Zombie){var k=NativeZombieSteps.Key(ff.Kind,ff.Crawl,ff.Run);NativeZombieSteps.StandIn(k,NativeFootsteps.Surface("expedition-city"));NativeZombieSteps.Rate(ff.Kind,NativeHordeKinds.Seed(ff.Id));}else NativeFootsteps.Surface("expedition-ward-zero","metal");}
            }
            for(int f=0;f<600;f++)Frame(f);long before=GC.GetAllocatedBytesForCurrentThread();for(int f=600;f<3000;f++)Frame(f);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"no allocation over 2400 frames of 4 pilots and a 40-add horde ("+used+" bytes)");
            log.Add("no allocation per frame (2400 frames, 4 pilots and 40 adds)");
        }

        // ---------- the hooks ----------
        private static void Source(string root,List<string> log)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            var ap=R("Runtime/ArenaPresentation.cs");Check(ap.Contains("PilotFootfall(a,p,renderTime,posed);")&&ap.Contains("stepTrackers.Remove(id);"),"every pilot's footfalls after its pose");
            Check(R("Runtime/NativeHordeKindPresentation.cs").Contains("{HordeDust(s,kind,ground,now);HordeFootfalls(s,m,kind,ground,now);}"),"every add's footfalls from its gait's plants");
            Check(R("Runtime/ConversionGame.cs").Contains("nativeAudio.Footfalls=presentation.Footfalls;"),"the queue is wired");
            var ca=R("Runtime/CapturedAnimation.cs");Check(ca.Contains("switch(route=NativePoseRoute.For(a,firstPerson))")&&ca.Contains("public int FootContacts")&&ca.Contains("melee.Gait.Planted(true)")&&ca.Contains("gunMotion.Planted(true)")&&ca.Contains("magicMotion.Planted(true)")&&ca.Contains("bowMotion.Planted(true)"),"the rig reports its gait's planted feet for every gaited route");
            var fx=R("Runtime/NativeEffectsAudio.cs");Check(fx.Contains("StepCues(arena);")&&!fx.Contains("NativeFootsteps.StrideFor")&&!fx.Contains("BankPlay(\"move.land\""),"the steps and landings left the distance stride");
            var kp=R("Runtime/NativeHordeKindPresentation.cs");Check(!kp.Contains("hordeStrides")&&kp.Contains("bool calm=HordeRoamCue(arena,m,kind,pos,t);ZombieCues(m,kind,attack,strike,pos,t,calm);")&&kp.Contains("ZombieEvents(arena,t);"),"the horde's voices are hooked where its cues are");
            var fp=R("Runtime/NativeFootfallPresentation.cs");foreach(var bad in new[]{".Hp=","Damage(","Emit(","Body.position=","linearVelocity=","LastAttack=","AddComponent<"})Check(!fp.Contains(bad),"the footfall presentation never writes `"+bad+"`");
            foreach(var f in new[]{"Runtime/NativeFootstepAudio.cs","Runtime/NativeZombieAudio.cs"}){var t=R(f);Check(!t.Contains("AddComponent<AudioSource>")&&!t.Contains("Play(\""),f+": the existing voices, no direct source-clip play");}
            Check(R("Runtime/NativeFootstepAudio.cs").Contains("int v=BankVoice(false,priority);"),"the built-in takes take a voice by priority");
            foreach(var f in new[]{"NativeFootfalls.cs","NativeZombieVoice.cs","NativeCreatureSynth.cs"}){var t=R("Core/"+f);Check(!t.Contains("UnityEngine")&&!t.Contains("NativeArena")&&!t.Contains("NativePilot")&&!t.Contains("new Random("),f+" reads no engine, no authority state, no clock-free randomness");}
            log.Add("hooks: pilots after their pose, adds after their gait, the queue wired, the rig's contacts, the horde's voices, no writes");
        }
    }
}
#endif
