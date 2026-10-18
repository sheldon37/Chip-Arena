#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // THE 6:47's presentation (docs/EXPEDITION-BOSS-CENTRAL-STATION.md "Presentation build"): normal play is unchanged (the view's
    // binding returns None, so nothing is drawn, lit, heard or changed in the station, and the placeholder Siren keeps its name);
    // the DEV preview is a pure function of the clock; every move's contact lands on the record's hit within one frame; hands the
    // gait plants slip less than 1 cm; nothing pops across action starts, contacts, cancels, staggers, phase changes, the
    // arrival's hand-off and the death (the per-frame change halves when the frame rate doubles); nothing allocates per frame;
    // the three tiers are inside their budgets and the hit volumes; the HUD names the zone boss THE 6:47 and waits for its drop.
    internal static class StationBossPresentationChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Station boss presentation: "+what);count++;}
        private static readonly Vector3 Target=new Vector3(3.5f,1.6f,10.5f);
        public static void Run(string root)
        {
            NonePin(root);Determinism(out string det);Contact(out string contact);Feet(out string feet);Pops(out string pops);Allocation(out string alloc);Tiers(out string tiers);Names();Cues(root);Source(root);Song(root,out string song);Stage(root,out string stage);Double(out string twice);Watch(out string watch);
            Console.WriteLine("PASS station boss presentation ("+count+" checks: normal play draws, lights, sounds and changes nothing (absent records draw nothing; the Siren keeps its name); "+det+"; "+contact+"; "+feet+"; "+pops+"; "+alloc+"; "+tiers+"; the plate and banner read THE 6:47 for station-647, the banner waits for the drop; "+song+"; "+stage+"; "+twice+"; "+watch+")");
        }

        // ---- normal play: the view is None, and None draws, lights, sounds and changes nothing ----
        private static void NonePin(string root)
        {
            var none=StationBossView.None;Check(!none.live&&none.Move==-1&&!none.Dead(100)&&!none.Holding(100)&&!none.Live(100)&&!none.BarrierDown(100)&&!none.BoardDown(100),"the None view has no boss, move, hold, break or arena change");
            var rig=new StationBossRig();var fx=new NativeStationBossFx();var cues=new StationBossCue[32];
            for(int f=0;f<120;f++){float t=f/60f;rig.Update(t,1/60f,none,StationBossGround.Station,Vector3.Zero,false,true,Target);fx.Build(rig,none,t,0,2,10,1,0,0,0,1,0,1,false);
                Check(!rig.Visible&&fx.Quads.Count==0&&fx.LightCount==0&&NativeStationBossCues.Collect(none,rig,t-1/60f,t,cues)==0,"None draws no boss, no effect, no light and no cue");}
            var st=NativeStationBossStage.Evaluate(none,50,StationBossBeats.Fallback);
            Check(!st.On&&st.Board==NativeStationBossStage.BoardOwn&&st.Row0==null&&st.ClockHour==6&&st.ClockMinute==47&&st.Lights==1&&st.LightSurge==0&&st.BarrierFall==0&&st.BoardFall==0&&st.Shake==0&&!st.WestSignalGreen,
                "None leaves the station as it is (the board's own rows, the clocks at 6:47, the lights as authored, nothing falls)");
            Check(!NativeStationBossPreview.On,"the DEV preview is off by default");
            string rt=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeStationBossPresentation.cs"));
            Check(rt.Contains("internal static StationBossView StationBossViewOf(NativeArena arena)=>arena?.StationZoneBossView()??StationBossView.None;"),"the binding reads the authority adapter and falls back to None without an arena");
            string arena=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeExpeditionM1Arena.cs"));
            Check(NativeZoneBossRules.RuntimeEnabled&&arena.Contains("string archetype=NativeZoneBossRules.RuntimeEnabled?NativeZoneBossRules.Archetype(MapId)??\"siren\":\"siren\""),"enabled zones select their dedicated boss archetype");
            Check(NativeExpeditionStates.BossName("siren")=="SIREN"&&NativeZoneBossNames.BannerDelay("siren")==0&&NativeZoneBossNames.Name("siren")==null,"the Siren's plate name and banner timing are unchanged");
        }

        // ---- the DEV preview: a pure function of the clock ----
        private static void Determinism(out string summary)
        {
            var a=StationBossView.None;var b=StationBossView.None;int same=0;
            for(int i=0;i<4000;i++)
            {
                float p=i*NativeStationBossPreview.Loop/4000;NativeStationBossPreview.View(p,10,ref a,out var va,out var ta);NativeStationBossPreview.View(p,10,ref b,out var vb,out var tb);
                Check(a.x==b.x&&a.z==b.z&&a.yaw==b.yaw&&a.action==b.action&&a.actionId==b.actionId&&float.Equals(a.hitAt,b.hitAt)&&a.phase==b.phase&&float.Equals(a.diedAt,b.diedAt)&&va==vb,"the preview's record is a pure function of the clock");
                NativeStationBossPreview.View(p,10+NativeStationBossPreview.Loop,ref b,out _,out _);Check(Math.Abs(a.x-b.x)<1e-4f&&a.action==b.action&&a.phase==b.phase,"the preview loops with the same script");same++;
            }
            // Two peers drawing the preview at different frame rates see the same action at the same progress (the timeline is a
            // pure function of the clock; the gait's steps and the springs' lag may differ by frame rate, never pop: Pops), and two
            // runs at the same rate draw the same pose to the bit.
            var r60=new StationBossRig();var r120=new StationBossRig();var again=new StationBossRig();int compared=0;
            for(int f=0;f<=60*40;f++)
            {
                float t=f/60f;Step(r60,t,1/60f);Step(again,t,1/60f);
                for(int k=1;k<=2;k++)Step(r120,t-(f==0?0:(2-k)/120f),f==0&&k==1?0:1/120f);
                if(f%30==0){Check(r60.Act.Active==r120.Act.Active&&Math.Abs(r60.Act.Progress-r120.Act.Progress)<1e-4f&&r60.ActMove==r120.ActMove,"the action timeline matches at 60 and 120 fps ("+t+")");compared++;
                    for(int j=0;j<StationBossRig.Count;j++)Check(r60.Shown.Position[j]==again.Shown.Position[j]&&r60.Shown.Rotation[j]==again.Shown.Rotation[j],"two runs draw the same pose to the bit");}
            }
            summary="the DEV preview's record is a pure function of the clock ("+same+" samples) and two runs draw the same pose to the bit";
        }
        private static void Step(StationBossRig r,float t,float dt){var v=StationBossView.None;NativeStationBossPreview.View(t,0,ref v,out var vel,out var tg);r.Update(t,dt,v,StationBossGround.Station,vel,false,true,new Vector3(tg.X,1.6f,tg.Y));}

        // ---- every move's contact lands on the record's hit within one frame, also when the authority's hit moves ----
        private static void Contact(out string summary)
        {
            float worst=0;var moves=NativeStationBoss.Moves;
            for(int i=0;i<moves.Length;i++)foreach(float stretch in new[]{1f,1.15f,.9f})foreach(float fps in new[]{60f,144f})
            {
                var m=moves[i];var v=Standing();v.action=m.Id;v.actionId=100+i;v.actionAt=2;v.hitAt=2+m.Anticipation*stretch;v.duration=m.Duration-m.Anticipation+m.Anticipation*stretch;
                if(m.Id=="departure"){v.laneX0=0;v.laneZ0=6;v.laneX1=0;v.laneZ1=28;}if(m.Id=="doors"){v.holdSide=-1;v.holdTarget=1;v.holdUntil=v.hitAt+1.75f;}if(m.Id=="live-rail"){v.liveAt=v.hitAt;v.liveUntil=v.hitAt+m.Strike;}
                var rig=new StationBossRig();float struck=float.NaN;float handAt=float.NaN;
                for(int f=0;f<=(int)(fps*(2+v.duration+.5f));f++)
                {
                    float t=f/fps;rig.Update(t,f==0?0:1/fps,v,StationBossGround.Station,Vector3.Zero,false,true,Target);
                    if(float.IsNaN(struck)&&rig.Act.Active&&rig.Act.Struck)struck=t;
                    if(m.Id=="terminus"&&float.IsNaN(handAt)&&t>v.actionAt+.5f&&rig.Hand(0).Y<.6f)handAt=t;
                }
                Check(!float.IsNaN(struck),m.Id+" reaches its contact");float err=struck-v.hitAt;worst=Math.Max(worst,Math.Abs(err));
                Check(err>=-1e-4f&&err<=1/fps+1e-4f,m.Id+"'s contact lands on the record's hit within one frame ("+err.ToString("0.0000")+" s at "+fps+" fps, hit x"+stretch+")");
                if(m.Id=="terminus")Check(!float.IsNaN(handAt)&&Math.Abs(handAt-v.hitAt)<=.06f,"Terminus's hands meet the floor on the hit ("+(handAt-v.hitAt).ToString("0.000")+" s)");
            }
            summary="every move's contact lands on the record's hit within one frame at 60 and 144 fps, at the authored hit and moved 15% either way (worst "+(worst*1000).ToString("0.0")+" ms)";
        }
        private static StationBossView Standing(){var v=StationBossView.None;v.live=true;v.x=0;v.z=2;v.yaw=0;v.phase=1;v.maxHp=6000;v.hp=6000;v.spawned=-100;return v;}

        // ---- planted hands do not slip: walking a curve, stopping, turning on the spot, the charge's bound ----
        private static void Feet(out string summary)
        {
            var rig=new StationBossRig();var prev=new Vector3[6];var held=new bool[6];float worst=0;int plants=0;float fps=60;
            for(int f=0;f<=(int)(fps*22);f++)
            {
                float t=f/fps;var v=Standing();GaitPath(t,out var p,out var vel,out float yaw);v.x=p.X;v.z=p.Z;v.yaw=yaw;
                rig.Update(t,f==0?0:1/fps,v,StationBossGround.Station,vel,false,true,Target);
                for(int i=0;i<6;i++)
                {
                    bool now=rig.PlantedFree(i);var h=rig.Hand(i);
                    if(now&&held[i]){float s=Vector3.Distance(h,prev[i]);worst=Math.Max(worst,s);}
                    if(now&&!held[i])plants++;held[i]=now;prev[i]=h;
                }
            }
            Check(plants>60,"the gait planted its hands ("+plants+")");
            Check(worst<.01f,"a planted hand slips less than 1 cm ("+(worst*100).ToString("0.000")+" cm at worst) walking, stopping, turning on the spot and bounding");
            summary=plants+" plants walking, turning on the spot and bounding at 16 m/s: a planted hand slips "+(worst*1000).ToString("0.00")+" mm at worst (under 1 cm)";
        }
        // A walk on a curve (2.6 m/s), a stop, a 160 degree turn on the spot, a charge at 16 m/s, a stop.
        private static void GaitPath(float t,out Vector3 p,out Vector3 vel,out float yaw)
        {
            Vector3 P(float s){var c=new Vector3(0,0,4);float r=7;if(s<9){float a=s*.36f;return c+new Vector3((float)Math.Sin(a)*r,0,-(float)Math.Cos(a)*r);}float a9=9*.36f;var e=c+new Vector3((float)Math.Sin(a9)*r,0,-(float)Math.Cos(a9)*r);
                if(s<15)return e;float k=s-15;float d=k<.4f?10*k*k:k<1.9f?1.6f+16*(k-.4f):k<2.6f?25.6f+16*(k-1.9f)-11.4f*(k-1.9f)*(k-1.9f):25.6f+16*.7f-11.4f*.49f;var dir=BossMath.Forward(Y(15)*BossMath.Deg);return e+dir*d;}
            float Y(float s){float a=Math.Min(s,9)*.36f;float y=a/BossMath.Deg+90;if(s>9.5f)y+=BossMath.Smoother((s-9.5f)/4.5f)*160;return y;}
            p=P(t);vel=(P(t+1/240f)-P(t-1/240f))*120;yaw=Y(t);
        }

        // ---- no pops: the per-frame change halves when the frame rate doubles, across every transition the preview plays ----
        private static void Pops(out string summary)
        {
            // Windows around the transitions (preview seconds): the arrival's hand-off, an action's start and contact, a crossfade,
            // the crash cancelling Departure into a stagger, the express stagger, the breaks, both phase changes, the death.
            float[] at={NativeStationBossPreview.ArrivalEnd,NativeStationBossPreview.BufferAt,NativeStationBossPreview.BufferAt+.55f,NativeStationBossPreview.DoorsAt+.8f,NativeStationBossPreview.CrashAt+1.75f,
                NativeStationBossPreview.ExpressAt,NativeStationBossPreview.LampAt,NativeStationBossPreview.PantoAt,NativeStationBossPreview.Phase2At,NativeStationBossPreview.Phase3At+1.65f,NativeStationBossPreview.TerminusAt+1.4f,NativeStationBossPreview.DeathAt};
            float worstRatio=0;string worstAt="";
            foreach(float c in at)
            {
                float d60=MaxDelta(c,60),d120=MaxDelta(c,120);float ratio=d120/Math.Max(1e-5f,d60);
                Check(d60<1.6f,"no joint jumps more than 1.6 m in a 60 fps frame round "+c.ToString("0.00")+" s ("+d60.ToString("0.00")+")");
                Check(ratio<.7f,"the per-frame change halves when the frame rate doubles round "+c.ToString("0.00")+" s (ratio "+ratio.ToString("0.00")+": no pop)");
                if(ratio>worstRatio){worstRatio=ratio;worstAt=c.ToString("0.0");}
            }
            summary="across "+at.Length+" transitions (the arrival's hand-off, starts, contacts, a crossfade, a cancel into a stagger, the breaks, both phase changes, the death) the per-frame change at 120 fps is at most "+worstRatio.ToString("0.00")+" of 60 fps's (no pop)";
        }
        // The largest per-frame joint move (relative to the root) within +-0.6 s of `c` at `fps`, after simulating from the start.
        private static float MaxDelta(float c,float fps)
        {
            var rig=new StationBossRig();var last=new BossPose(StationBossRig.Count);float worst=0;bool has=false;
            for(int f=0;;f++)
            {
                float t=f/fps;if(t>c+.6f)break;Step(rig,t,f==0?0:1/fps);
                if(t>=c-.6f){if(has)for(int j=0;j<StationBossRig.Count;j++){if(j>=NativeStationBossModel.WheelFirst&&j<NativeStationBossModel.WheelFirst+6)continue;float d=Vector3.Distance(rig.Shown.Position[j]-rig.Shown.RootPosition,last.Position[j]-last.RootPosition);worst=Math.Max(worst,d);}last.CopyFrom(rig.Shown);has=true;}
            }
            return worst;
        }

        // ---- nothing allocates per frame: the rig, the effects, the stage hook and the cues over the whole preview ----
        private static void Allocation(out string summary)
        {
            var rig=new StationBossRig();var fx=new NativeStationBossFx();var cues=new StationBossCue[NativeStationBossCues.QueueSize];var v=StationBossView.None;
            void Frame(int f){float t=f/60f;NativeStationBossPreview.View(t,0,ref v,out var vel,out var tg);rig.Update(t,1/60f,v,StationBossGround.Station,vel,false,true,new Vector3(tg.X,1.6f,tg.Y));
                fx.GlassCount=NativeStationBossPreview.Glass(v,fx.GlassXZ);fx.Build(rig,v,t,4,3,14,1,0,0,0,1,0,1,false);NativeStationBossStage.Evaluate(v,t,NativeStationBossBeats.Current);NativeStationBossCues.Collect(v,rig,t-1/60f,t,cues);
                NativeStationBossCues.Queue(v,rig,t-1/60f,t);NativeStationBossCues.Drain(cues);}
            int frames=(int)(NativeStationBossPreview.Loop*60);for(int f=0;f<frames;f++)Frame(f);
            long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<frames;f++)Frame(f);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"the rig, the effects, the stage hook and the cues allocate nothing per frame ("+used+" bytes over "+frames+" frames)");
            summary="0 bytes per frame over the whole "+NativeStationBossPreview.Loop+" s preview (rig, effects, stage hook, cues)";
        }

        // ---- the three tiers: inside the budgets and the hit volumes, every joint drawn from the right parent ----
        private static void Tiers(out string summary)
        {
            var tris=new int[3];
            for(int tier=0;tier<3;tier++)
            {
                var m=NativeStationBossLook.Build(tier);var again=NativeStationBossLook.Build(tier);tris[tier]=m.Triangles;
                Check(m.Triangles<=NativeStationBossLook.Budget[tier],"tier "+tier+" is inside its budget ("+m.Triangles+" of "+NativeStationBossLook.Budget[tier]+")");
                Check(m.Triangles>=NativeStationBossModel.Build(tier).Triangles,"tier "+tier+" keeps every concept triangle (the split moves, never drops)");
                Check(m.Sections.Count==again.Sections.Count&&m.Sections.Zip(again.Sections,(a,b)=>a.Positions.SequenceEqual(b.Positions)).All(x=>x),"tier "+tier+" builds deterministically");
                foreach(var s in m.Sections)Check(s.Bone>=0&&s.Bone<NativeStationBossLook.Count,"every section rides a joint");
                for(int side=0;side<2;side++)for(int k=0;k<4;k++){var w=m.Sections.FirstOrDefault(s=>s.Bone==NativeStationBossLook.Window(side,k));Check(w!=null&&w.Emission.Any(e=>e.X+e.Y+e.Z>0),"window "+side+"/"+k+" is its own lit joint on tier "+tier);}
                Check(m.Sections.Any(s=>s.Bone==NativeStationBossLook.HornL)&&m.Sections.Any(s=>s.Bone==NativeStationBossLook.TailL+2)&&m.Sections.Any(s=>s.Bone==NativeStationBossLook.BreachR),"the horns, tails and torn flanks are built on tier "+tier);
                // The added parts at rest stay inside the carriage's box (in the body's frame) and the rider's capsule.
                foreach(var s in m.Sections)
                {
                    if(s.Bone<NativeStationBossModel.Count)continue;var off=Rest(s.Bone);
                    foreach(var p in s.Positions)
                    {
                        var q=p+off;bool box=q.X>=NativeStationBossModel.BodyBoxMin.X-.05f&&q.X<=NativeStationBossModel.BodyBoxMax.X+.05f&&q.Y>=NativeStationBossModel.BodyBoxMin.Y&&q.Y<=NativeStationBossModel.BodyBoxMax.Y+.05f&&q.Z>=NativeStationBossModel.BodyBoxMin.Z&&q.Z<=NativeStationBossModel.BodyBoxMax.Z;
                        if(!box)Check(false,NativeStationBossLook.Names[s.Bone]+" leaves the carriage's box at rest ("+q+")");
                    }
                }
            }
            Check(tris[0]>tris[1]&&tris[1]>tris[2],"each tier is lighter than the last");
            summary="tiers "+tris[0]+" / "+tris[1]+" / "+tris[2]+" triangles inside "+string.Join(" / ",NativeStationBossLook.Budget)+", the added parts inside the carriage's box";
        }
        // A presentation joint's rest offset in the body's frame (the tails hang from the Stationmaster's hips).
        private static Vector3 Rest(int j){var o=Vector3.Zero;int b=j;while(b>=0&&b!=NativeStationBossModel.Body){o+=NativeStationBossLook.Rest[b];b=NativeStationBossLook.Parent[b];}return o;}

        // ---- the HUD: the plate and the banner name the zone boss, the banner waits for the drop ----
        private static void Names()
        {
            Check(NativeExpeditionStates.BossName(NativeStationBoss.Archetype)==NativeStationBoss.Name&&NativeStationBoss.Name=="THE 6:47","the boss plate names station-647 THE 6:47");
            Check(Math.Abs(NativeZoneBossNames.BannerDelay(NativeStationBoss.Archetype)-NativeStationBossBeats.Current.Banner)<1e-6,"THE 6:47's banner waits for the drop");
            var cues=new ExpeditionStateCues();var v=new ExpeditionView{area=NativeStationBoss.Area,phase=ExpeditionPhase.Instance,
                bossArrivals=new[]{new ExpeditionBossArrivalView{id="b",area=NativeStationBoss.Area,archetype=NativeStationBoss.Archetype,spawned=10,x=0,y=0,z=1.5f,hp=6000,maxHp=6000}},instances=new[]{new ExpeditionViewInstance{id=NativeStationBoss.Area}}};
            cues.Observe(v,10.1);var model=new ExpeditionBossModel();
            model.From(v,cues,10.5,null);Check(model.Intro<0,"no banner before the drop");
            model.From(v,cues,10+NativeStationBossBeats.Current.Banner+.2,null);Check(model.Intro>=0&&model.Name=="THE 6:47"&&model.Area=="BOSS ARRIVAL · CENTRAL STATION","the banner shows THE 6:47 on the drop under BOSS ARRIVAL · CENTRAL STATION");
            Check(model.PlateCount==1&&model.PlateNames[0]=="THE 6:47","the plate reads THE 6:47");
            var sv=new ExpeditionView{area=NativeStationBoss.Area,phase=ExpeditionPhase.Instance,bossArrivals=new[]{new ExpeditionBossArrivalView{id="s",area=NativeStationBoss.Area,archetype="siren",spawned=10,hp=10,maxHp=10}},instances=new[]{new ExpeditionViewInstance{id=NativeStationBoss.Area}}};
            var sc=new ExpeditionStateCues();sc.Observe(sv,10.1);var sm=new ExpeditionBossModel();sm.From(sv,sc,10.5,null);Check(sm.Intro>=0&&sm.Name=="SIREN","the placeholder Siren's banner still shows at once as SIREN");
        }

        // ---- the cue keys: the doc's table, each reachable, the 2D ones flat ----
        private static void Cues(string root)
        {
            string doc=File.ReadAllText(Path.Combine(root,"../../docs/EXPEDITION-BOSS-CENTRAL-STATION.md"));
            var table=System.Text.RegularExpressions.Regex.Matches(doc,@"\| `(boss\.station\.[a-z-]+)` \| (P|F) \|").Cast<System.Text.RegularExpressions.Match>().ToDictionary(m=>m.Groups[1].Value,m=>m.Groups[2].Value);
            Check(table.Count==NativeStationBossCues.Keys.Length&&NativeStationBossCues.Keys.All(table.ContainsKey),"the cue keys are the doc's table ("+table.Count+")");
            for(int i=0;i<NativeStationBossCues.Keys.Length;i++)Check(NativeStationBossCues.Flat((byte)i)==(table[NativeStationBossCues.Keys[i]]=="F"),NativeStationBossCues.Keys[i]+" plays "+(table[NativeStationBossCues.Keys[i]]=="F"?"2D":"positional")+" as the doc says");
            // Over the whole preview every key but the idle groan's rate-limited cousin fires at least once.
            var heard=new bool[NativeStationBossCues.Keys.Length];var rig=new StationBossRig();var v=StationBossView.None;var buf=new StationBossCue[NativeStationBossCues.QueueSize];
            for(int f=0;f<(int)(NativeStationBossPreview.Loop*60);f++){float t=f/60f;NativeStationBossPreview.View(t,0,ref v,out var vel,out var tg);rig.Update(t,1/60f,v,StationBossGround.Station,vel,false,true,new Vector3(tg.X,1.6f,tg.Y));
                int blows=NativeStationBossPreview.Blows(t,0,blowAt,blowPush,blowSize);for(int i=0;i<blows;i++)if(blowAt[i]>t-1/60f&&blowAt[i]<=t)rig.Hit(blowAt[i],blowPush[i],blowSize[i]);
                int n=NativeStationBossCues.Collect(v,rig,t-1/60f,t,buf);for(int i=0;i<n;i++)heard[buf[i].Key]=true;}
            var missing=Enumerable.Range(0,heard.Length).Where(i=>!heard[i]).Select(i=>NativeStationBossCues.Keys[i]).ToArray();
            Check(missing.Length==0,"every boss.station key plays somewhere in the preview (missing: "+string.Join(", ",missing)+")");
            string rt=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeStationBossPresentation.cs"));Check(rt.Contains("Cue(key,null,place,c.Gain)"),"the cues play through the bank path with no stand-in (silent without clips)");
        }
        private static readonly float[] blowAt=new float[8],blowSize=new float[8];private static readonly Vector3[] blowPush=new Vector3[8];

        // ---- the boss song binds the arrival: its accents are the beats, its stage keeps the lights, the run meets the live root ----
        private static void Song(string root,out string summary)
        {
            var music=NativeZoneBossMusic.Parse(File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/ZoneBossMusic.json")));
            var song=music.ForMap(NativeExpeditionStation.MapId,_=>true);Check(song!=null,"the station has a boss song in the map");
            Check(StationBossBeats.FromSong(song,out var b)&&b.Song&&b.Valid,"the song's accents place the arrival's beats in order");
            double Boss(NativeMusicAccent a)=>song.FirstBoss(a.T);bool Played(double t)=>t>=0&&t<=song.Hold+.05;
            bool On(float t,byte kind)=>song.Accents.Any(a=>(kind==255||a.Kind==kind)&&Played(Boss(a))&&Math.Abs(Boss(a)-t)<1e-3);
            double firstCut=song.Accents.Where(a=>a.Kind==NativeZoneBossSong.AccentCut&&Played(Boss(a))).Min(a=>Boss(a));
            double firstChime=song.Accents.Where(a=>a.Kind==NativeZoneBossSong.AccentChime&&Played(Boss(a))).Min(a=>Boss(a));
            double lastChime=song.Accents.Where(a=>a.Kind==NativeZoneBossSong.AccentChime&&Played(Boss(a))&&Boss(a)<=firstCut-.5).Max(a=>Boss(a));
            Check(Math.Abs(b.ChimeHit-lastChime)<1e-3&&b.ChimeHit>firstChime&&Math.Abs(b.LightCut-firstCut)<1e-3,"the board flips to ARRIVING on the song's last intro chime (LAST CALL before it) and the headlight blooms on its first cut");
            Check(Math.Abs(b.Banner-song.Hold)<.01&&Math.Abs(b.Hold-song.Hold)<1e-4,"the banner lands on the song's drop at the hold's end");
            Check(On(b.Drop,NativeZoneBossSong.AccentSlam)&&b.Drop<b.Banner,"the burst through the gate is on a slam before the drop");
            Check(song.Track.beats.Any(c=>Math.Abs(song.FirstBoss(c)-b.Accent0)<1e-3),"the brake sparks fall on a beat of the song's grid");
            Check(On(b.Accent1,NativeZoneBossSong.AccentSlam)||On(b.Accent1,NativeZoneBossSong.AccentDrop),"it stops at the crossing on the slam where the song flares the lights");
            for(int i=2;i<4;i++)Check(On(b.Accent(i),255),"intro accent "+i+" (the unfold, the step up) is on a song hit");
            Check(NativeStationBossBeats.UseSong(song)&&NativeStationBossBeats.Current.Song&&NativeStationBossBeats.UseSong(song),"the presenter binds the song (again a no-op)");
            Check(Math.Abs(NativeZoneBossNames.BannerDelay(NativeStationBoss.Archetype)-b.Banner)<1e-6,"the banner waits for the song's drop");
            // The arrival on the song's beats: the song keeps the lights, the board still flips, no PA chime over the song's own,
            // the run reaches the live root by the hold's end, and nothing pops at the hand-off.
            var rig=new StationBossRig();var v=StationBossView.None;var buf=new StationBossCue[NativeStationBossCues.QueueSize];float minLight=1,maxSurge=0,maxStep=0,settled=float.NaN;int flipped=0,frames=0;bool chime=false;Vector3 lastRoot=Vector3.Zero;
            for(int f=0;f<(int)((NativeStationBossPreview.ArrivalEnd+2.5f)*120);f++)
            {
                float t=f/120f;NativeStationBossPreview.View(t,0,ref v,out var vel,out var tg);rig.Update(t,1/120f,v,StationBossGround.Station,vel,false,true,new Vector3(tg.X,1.6f,tg.Y));
                if(t>NativeStationBossPreview.ArrivalEnd-.5f){if(f>0)maxStep=Math.Max(maxStep,Vector3.Distance(rig.Root,lastRoot));if(float.IsNaN(settled)&&t>NativeStationBossPreview.ArrivalEnd&&Vector3.Distance(rig.Root,new Vector3(v.x,rig.Root.Y,v.z))<.3f)settled=t-NativeStationBossPreview.ArrivalEnd;}
                lastRoot=rig.Root;
                var st=NativeStationBossStage.Evaluate(v,t,NativeStationBossBeats.Current);minLight=Math.Min(minLight,st.Lights);maxSurge=Math.Max(maxSurge,st.LightSurge);
                if(rig.Arriving&&rig.ArrivalAge>b.ChimeHit+.01f){frames++;if(st.Board==NativeStationBossStage.BoardArriving)flipped++;}
                int n=NativeStationBossCues.Collect(v,rig,t-1/120f,t,buf);for(int i=0;i<n;i++)if(rig.Arriving&&buf[i].Key==NativeStationBossCues.Chime)chime=true;
            }
            Check(minLight==1&&maxSurge==0,"with the song bound the boss's hook leaves the house lights to the song's stage");
            Check(frames>0&&flipped==frames,"row 0 reads ARRIVING from the chime to the hold's end");
            Check(!chime,"no PA chime cue over the song's own chimes");
            StationBossArrival.Root(b.Hold-1e-3f,b,new Vector3(0,0,1.5f),0,out var ap,out _,out _);
            Check(ap.Z>NativeStationBossArena.BarrierZ,"the burst has carried it through the gate by the hold's end");
            Check(maxStep<16f/120f&&!float.IsNaN(settled)&&settled<2f,"the live follow strides it on to its root with no jump (largest step "+(maxStep*120).ToString("0.0")+" m/s, settled "+settled.ToString("0.00")+" s after the hold)");
            float d60=MaxDelta(NativeStationBossPreview.ArrivalEnd,60),d120=MaxDelta(NativeStationBossPreview.ArrivalEnd,120),ratio=d120/Math.Max(1e-5f,d60);
            float d60b=MaxDelta(NativeStationBossPreview.SpawnAt+b.Drop,60),d120b=MaxDelta(NativeStationBossPreview.SpawnAt+b.Drop,120),ratiob=d120b/Math.Max(1e-5f,d60b);
            Check(d60<1.6f&&ratio<.7f&&d60b<1.6f&&ratiob<.7f,"no pop at the song's burst or the hand-off ("+ratio.ToString("0.00")+", "+ratiob.ToString("0.00")+")");
            NativeStationBossBeats.Clear();Check(!NativeStationBossBeats.Current.Song,"cleared, the doc's beats return");
            summary="bound to the boss song: chime "+b.ChimeHit.ToString("0.00")+" s, cut "+b.LightCut.ToString("0.00")+", accents "+b.Accent0.ToString("0.00")+"/"+b.Accent1.ToString("0.00")+"/"+b.Accent2.ToString("0.00")+"/"+b.Accent3.ToString("0.00")+
                ", burst "+b.Drop.ToString("0.00")+", banner on the drop "+b.Banner.ToString("0.00")+"; the song keeps the lights, row 0 still flips, the live follow carries it on from the burst, no pop (ratio "+Math.Max(ratio,ratiob).ToString("0.00")+")";
        }

        // ---- the station reacting: row 0's order, the clocks, T1's signal, the falls, the shudder; the board and barrier poses; the
        // DEV preview's express and benches ----
        private static void Stage(string root,out string summary)
        {
            var music=NativeZoneBossMusic.Parse(File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/ZoneBossMusic.json")));var song=music.ForMap(NativeExpeditionStation.MapId,_=>true);
            Check(NativeStationBossBeats.UseSong(song),"the song binds the beats for the stage checks");var b=NativeStationBossBeats.Current;
            // Row 0 through the arrival and the fight on the preview's record, as the station draws it: the authority's (the boss flag
            // from the hold's end), the song's LAST CALL, the boss's stage.
            var st=new NativeStationState();var sv=new NativeStationView();var v=StationBossView.None;string order="";byte last=255;bool green=false,greenAfter=false;
            for(float p=0;p<NativeStationBossPreview.Loop-.01f;p+=1/30f)
            {
                NativeStationBossPreview.View(p,0,ref v,out _,out _);sv.Reset();sv.Boss=p>=NativeStationBossPreview.ArrivalEnd;
                st.Evaluate(sv,p);st.Score(song.Evaluate(p-v.spawned,NativeKitFlash.Full));var stage=NativeStationBossStage.Evaluate(v,p,b);NativeStationBossStage.Apply(st,stage);
                if(st.Row0!=last){last=st.Row0;order+=(order.Length>0?",":"")+NativeExpeditionStation.Row0Status[last];}
                float age=p-v.spawned;if(age>b.LightCut&&age<b.Drop)green|=st.Signal==NativeStationState.SignalGreen;if(age>b.Hold+2&&!v.Dead(p))greenAfter|=st.Signal==NativeStationState.SignalGreen;
                if(v.Dead(p)&&p-v.diedAt>NativeStationBossStage.DeathClocks+.05f)Check(st.ClockMinute==48,"every clock reads 6:48 after the death's last tick");
                if(!v.Dead(p)||p-v.diedAt<NativeStationBossStage.DeathClocks-.05f)Check(st.ClockMinute==47,"the clocks read 6:47 until the death's last tick");
                if(StationBossView.At(v.barrierDownAt)&&p>v.barrierDownAt+NativeStationBossStage.BarrierFallSeconds)Check(st.BarrierFall>.99f,"the barrier lies down once thrown");
                if(StationBossView.At(v.boardDownAt)&&p>v.boardDownAt+NativeStationBossStage.BoardFallSeconds)Check(st.BoardFall>.99f,"the board lies down once torn off");
            }
            Check(order=="DELAYED,LAST CALL,ARRIVING,CANCELLED,DEPARTED","row 0 reads DELAYED before the boss, LAST CALL through the song's intro chimes, ARRIVING from the boss's flip, CANCELLED in the fight, DEPARTED at the death ("+order+")");
            Check(green&&!greenAfter,"T1's signal shows green for the arrival only");
            var lit=new NativeStationState();lit.Evaluate(new NativeStationView{Levers=true,LeversAt=0,LeversUntil=100},1);var arriving=new StationBossStage{On=true,Board=NativeStationBossStage.BoardArriving,WestSignalGreen=true,ClockMinute=47};
            NativeStationBossStage.Apply(lit,arriving);Check(lit.Signal==NativeStationState.SignalDark,"the levers' dark signals stay dark");
            float shake=0;for(float p=NativeStationBossPreview.Phase2At;p<NativeStationBossPreview.Phase2At+4;p+=1/30f){NativeStationBossPreview.View(p,0,ref v,out _,out _);shake=Math.Max(shake,NativeStationBossStage.Evaluate(v,p,b).Shake);}
            Check(shake>.5f,"the barrier's smash shudders the station ("+shake.ToString("0.00")+")");
            // The kit draws them: row 0's ARRIVING and DEPARTED and platform 1, every clock's hands at 6:47 or 6:48.
            var o=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};var unity=System.Text.Json.JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),o);
            var area=NativeExpeditionLayout.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ExpeditionLayout.json"))).Area("exp-transit");var kit=NativeExpeditionStationKit.Build(unity.maps.Single(m=>m.id==NativeExpeditionStation.MapId).blocks,area);
            int Shown(NativeStationState state,Func<NativeStationPart,bool> which){int n=0;foreach(var part in kit.Parts)if(which(part)&&NativeExpeditionStationKit.Drive(part,state,0,out _,out _,out _,out _))n++;return n;}
            bool Hand(NativeStationPart p,int at)=>p.Drive==NativeExpeditionStationKit.DriveClock&&p.Tag==at;
            var s47=new NativeStationState();s47.Evaluate(new NativeStationView(),0);var s48=new NativeStationState();s48.Evaluate(new NativeStationView(),0);NativeStationBossStage.Apply(s48,new StationBossStage{On=true,ClockMinute=48});
            int hands=kit.Parts.Count(p=>Hand(p,0));Check(hands>=10&&kit.Parts.Count(p=>Hand(p,1))==hands,"every station clock has its hands at 6:47 and at 6:48 ("+hands+" each)");
            Check(Shown(s47,p=>Hand(p,0))==hands&&Shown(s47,p=>Hand(p,1))==0&&Shown(s48,p=>Hand(p,1))==hands&&Shown(s48,p=>Hand(p,0))==0,"the clocks show 6:47, then only 6:48");
            Check(Shown(null,p=>Hand(p,0))==hands,"the steady colours (no state) are 6:47");
            bool Row0(NativeStationPart p,int variant)=>p.Drive==NativeExpeditionStationKit.DriveBoard&&p.Tag==variant;bool Plat(NativeStationPart p,int variant)=>p.Drive==NativeExpeditionStationKit.DriveBoard&&p.Tag==7*16+variant;
            var sa=new NativeStationState();sa.Evaluate(new NativeStationView(),0);NativeStationBossStage.Apply(sa,arriving);var sd=new NativeStationState();sd.Evaluate(new NativeStationView{Boss=true},0);NativeStationBossStage.Apply(sd,new StationBossStage{On=true,Board=NativeStationBossStage.BoardDeparted,ClockMinute=48});
            Check(sa.Row0==NativeStationState.Row0Arriving&&Shown(sa,p=>Row0(p,NativeStationState.Row0Arriving))>0&&Shown(sa,p=>Plat(p,1))>0&&Shown(sa,p=>Plat(p,0))==0,"ARRIVING draws on row 0 with platform 1");
            Check(sd.Row0==NativeStationState.Row0Departed&&Shown(sd,p=>Row0(p,NativeStationState.Row0Departed))>0&&Shown(sd,p=>Row0(p,NativeStationState.Row0Cancelled))==0&&Shown(sd,p=>Plat(p,0))>0,"DEPARTED replaces CANCELLED on row 0 at the death");
            // The poses: the fallen board tips away (nothing south of z 8.9) and rests on the booth; the barrier lies on P1.
            NativeStationBossStage.BoardPose(1,out var bq,out var bm);var piv=NativeStationBossStage.BoardPivot;float south=float.MaxValue,low=float.MaxValue;
            Vector3 Board(Vector3 q)=>piv+Vector3.Transform(q-piv,bq)+bm;var c0=new Vector3(NativeStationBossArena.BoardX,NativeStationBossArena.BoardY,NativeStationBossArena.BoardZ);var h=new Vector3(NativeStationBossArena.BoardHalfX,NativeStationBossArena.BoardHalfY,NativeStationBossArena.BoardHalfZ);
            for(int k=0;k<8;k++){var q=Board(c0+new Vector3((k&1)==0?-h.X:h.X,(k&2)==0?-h.Y:h.Y,(k&4)==0?-h.Z:h.Z));south=Math.Min(south,q.Z);low=Math.Min(low,q.Y);}
            Check(south>8.9f&&low>2.6f&&low<3.3f,"the fallen board tips away from the boss and rests on the booth (south edge z "+south.ToString("0.00")+", lowest "+low.ToString("0.00")+" m)");
            for(int side=-1;side<=1;side+=2){NativeStationBossStage.BarrierPose(side,1,out var bp,out var br,out var bmv);var top=bp+Vector3.Transform(new Vector3(side*10.5f,NativeStationBossArena.BarrierHeight,NativeStationBossArena.BarrierZ)-bp,br)+bmv;Check(top.Z<NativeStationBossArena.BarrierZ-.6f&&top.Y>-.1f&&top.Y<.6f,"the "+(side<0?"west":"east")+" segment lies on P1");}
            // The boss stays clear of the fallen board from its landing to the loop's end (TERMINUS, the double charge, Next Stop, the death).
            var rig=new StationBossRig();float worstIn=float.MaxValue;var inv=Quaternion.Inverse(bq);
            for(int f=0;f<(int)(NativeStationBossPreview.Loop*30);f++)
            {
                float t=f/30f;NativeStationBossPreview.View(t,0,ref v,out var vel,out var tg);rig.Update(t,1/30f,v,StationBossGround.Station,vel,false,true,new Vector3(tg.X,1.6f,tg.Y));
                if(!StationBossView.At(v.boardDownAt)||t<v.boardDownAt+NativeStationBossStage.BoardFallSeconds)continue;
                for(int j=0;j<StationBossRig.Count;j++){var q=Vector3.Transform(rig.Shown.Position[j]-bm-piv,inv)+piv-c0;float d=Math.Max(Math.Abs(q.X)-h.X,Math.Max(Math.Abs(q.Y)-h.Y,Math.Abs(q.Z)-h.Z));worstIn=Math.Min(worstIn,d);}
            }
            Check(worstIn>.05f,"no part of the boss enters the fallen board (closest "+worstIn.ToString("0.00")+" m)");
            // The DEV preview: the express passes on T1 and meets the boss in Mind the Gap; the benches it walks through are cleared.
            NativeStationBossPreview.Start=0;var pv=new NativeStationView();var ps=new NativeStationState();Check(NativeStationBossPreview.Station(pv,NativeStationBossPreview.ExpressAt),"the preview fills the station's view");
            ps.Evaluate(pv,NativeStationBossPreview.ExpressAt);Check(ps.Express==NativeStationState.ExpressPass&&Math.Abs(ps.Nose-NativeStationBossPreview.ExpressMeetX)<.5f&&ps.Line==1,"the express's nose meets the boss on T1 in Mind the Gap ("+ps.Nose.ToString("0.0")+")");
            pv.Reset();NativeStationBossPreview.Station(pv,NativeStationBossPreview.ExpressAt-NativeExpeditionStation.WarnSeconds+.5);ps.Evaluate(pv,NativeStationBossPreview.ExpressAt-NativeExpeditionStation.WarnSeconds+.5);Check(ps.Express==NativeStationState.ExpressWarn&&ps.Danger>0,"its warning plays first");
            NativeStationBossPreview.Start=double.NaN;Check(!NativeStationBossPreview.Station(pv,1),"no view before the preview starts");
            string rt=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeStationBossPresentation.cs")),stp=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeExpeditionStationPresentation.cs"));
            Check(rt.Contains("NativeArenaDressing.Hidden.Add(stationBossBenches[i])")&&rt.Contains("-benches-e"),"the preview clears the concourse benches it walks through");
            Check(stp.Contains("stationState.Score(ZoneBossBeat(arena));NativeLighting.ZoneHouse(stationState.Music.House());StationBossStageApply();")&&stp.Contains("StationBossFallPose(i,ref m)"),"the station draws the hook after the song (the house light stays the song's)");
            NativeStationBossBeats.Clear();
            summary="the station draws the hook: row 0 "+order+", clocks to 6:48, T1 green for the arrival, the barrier thrown onto P1, the board tipped back across the booth (the boss "+worstIn.ToString("0.00")+" m clear at closest), the shudder; the preview's express meets the boss on T1";
        }
        // ---- TERMINUS's double charge: two contacts, each within a frame; the second lane and chime ----
        private static void Double(out string summary)
        {
            var m=NativeStationBoss.Move("departure");float worst=0;
            foreach(float fps in new[]{60f,144f})
            {
                var v=Standing();v.phase=3;v.action="departure";v.actionId=900;v.actionAt=2;v.hitAt=2+m.Anticipation;v.duration=StationBossMoves.DepartureDouble.Seconds;v.laneX0=0;v.laneZ0=6;v.laneX1=0;v.laneZ1=28;
                var rig=new StationBossRig();float first=float.NaN,second=float.NaN;bool twice=false;var cues=new StationBossCue[48];int chimes=0,charges=0;
                for(int f=0;f<=(int)(fps*(2+v.duration+.5f));f++)
                {
                    float t=f/fps;rig.Update(t,f==0?0:1/fps,v,StationBossGround.Station,Vector3.Zero,false,true,Target);twice|=rig.DoubleCharge;
                    if(float.IsNaN(first)&&rig.Act.Active&&rig.Act.Struck)first=t;if(float.IsNaN(second)&&rig.Act.Active&&rig.Act.PhaseIndex==4)second=t;
                    int n=NativeStationBossCues.Collect(v,rig,t-1/fps,t,cues);for(int i=0;i<n;i++){if(cues[i].Key==NativeStationBossCues.Chime)chimes++;if(cues[i].Key==NativeStationBossCues.Charge)charges++;}
                }
                float e1=first-v.hitAt,e2=second-(v.hitAt+StationBossMoves.SecondCharge);worst=Math.Max(worst,Math.Max(Math.Abs(e1),Math.Abs(e2)));
                Check(twice&&e1>=-1e-4f&&e1<=1/fps+1e-4f&&e2>=-1e-4f&&e2<=1/fps+1e-4f,"TERMINUS's Departure charges twice, each contact within a frame ("+e1.ToString("0.0000")+", "+e2.ToString("0.0000")+" s at "+fps+" fps)");
                Check(chimes==NativeStationBoss.ChimeNotes+2&&charges==2,"the second charge has its two-note chime and its charge cue ("+chimes+" chimes, "+charges+" charges)");
            }
            var one=Standing();one.action="departure";one.actionId=901;one.actionAt=2;one.hitAt=2+m.Anticipation;one.duration=m.Duration;var r1=new StationBossRig();bool single=true;
            for(int f=0;f<300;f++){float t=f/60f;r1.Update(t,f==0?0:1/60f,one,StationBossGround.Station,Vector3.Zero,false,true,Target);single&=!r1.DoubleCharge;}
            Check(single,"before TERMINUS Departure is one charge");
            summary="TERMINUS's Departure is two charges (second contact "+StationBossMoves.SecondCharge.ToString("0.0")+" s after the first, both within a frame, worst "+(worst*1000).ToString("0.0")+" ms) with a second two-note chime";
        }
        // ---- the pocket watch: its minute hand moves on one minute, clockwise, at the death's last tick ----
        private static void Watch(out string summary)
        {
            var mesh=NativeStationBossLook.Build(0);int a=0,b=0;foreach(var sec in mesh.Sections){if(sec.Bone==NativeStationBossLook.WatchAt47)a+=sec.Triangles;if(sec.Bone==NativeStationBossLook.WatchAt48)b+=sec.Triangles;}
            Check(a>=8&&a==b,"the watch has its minute hand at 6:47 and at 6:48 ("+a+" triangles each)");
            NativeStationBossLook.WatchPivot(out var n,out float turn);Check(Math.Abs(Math.Abs(turn)-6*Math.PI/180)<1e-4,"the hand turns one minute (6 degrees)");
            var fx=new NativeStationBossFx();var rig=new StationBossRig();var v=Standing();v.diedAt=10;v.hp=0;
            rig.Update(10,0,v,StationBossGround.Station,Vector3.Zero,false,true,Target);fx.Build(rig,v,12,0,2,10,1,0,0,0,1,0,1,false);bool before=!fx.Hidden[NativeStationBossLook.WatchAt47]&&fx.Hidden[NativeStationBossLook.WatchAt48];
            rig.Update(14,4,v,StationBossGround.Station,Vector3.Zero,false,true,Target);fx.Build(rig,v,10+NativeStationBossStage.DeathClocks+.1f,0,2,10,1,0,0,0,1,0,1,false);bool after=fx.Hidden[NativeStationBossLook.WatchAt47]&&!fx.Hidden[NativeStationBossLook.WatchAt48];
            Check(before&&after,"the watch reads 6:47 until the death's last tick, then 6:48");
            summary="the pocket watch's minute hand moves on to 6:48 with the station's clocks";
        }

        // ---- the source: presentation only ----
        private static void Source(string root)
        {
            foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Core"),"NativeStationBoss*.cs").Concat(new[]{Path.Combine(root,"Assets/ChipArena/Runtime/NativeStationBossPresentation.cs")}))
            {
                if(f.EndsWith("NativeStationBossModel.cs"))continue;string s=File.ReadAllText(f);
                foreach(var bad in new[]{"Rigidbody","Collider","NativeNetwork","Physics.","Damage(","BeginPveHold","RecordBossArrival"})Check(!s.Contains(bad),Path.GetFileName(f)+" stays presentation only (no "+bad+")");
            }
        }
    }
}
#endif
