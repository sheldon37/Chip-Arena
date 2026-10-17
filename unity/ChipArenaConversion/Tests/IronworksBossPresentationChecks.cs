#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // THE CONVERTER's presentation (docs/EXPEDITION-BOSS-CITY-IRONWORKS.md "Presentation build"): normal play is unchanged (the
    // view's binding returns None, so nothing is drawn, lit, heard or changed in the hall, and the placeholder Siren keeps its
    // name); the DEV preview is a pure function of the clock; every action's contact lands on the record's hit within one frame;
    // the gait's planted feet slip less than 1 cm; nothing pops across action starts, contacts, a choke, the moments, both phase
    // changes, the arrival's hand-off and the death (the per-frame change halves when the frame rate doubles); nothing allocates
    // per frame; the three tiers are inside their budgets; the HUD names the zone boss THE CONVERTER; the arrival's beats come from
    // Hydraulic Hell's map; the cue keys are the doc's table; the source stays presentation only.
    internal static class IronworksBossPresentationChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Ironworks boss presentation: "+what);count++;}
        private static readonly Vector3 Target=new Vector3(-28f,1.6f,2.6f);
        public static void Run(string root)
        {
            NonePin(root);Determinism(out string det);Contact(out string contact);Feet(out string feet);Pops(out string pops);Allocation(out string alloc);Tiers(out string tiers);
            Names();Song(root,out string song);Cues(root);Death();Source(root);
            Console.WriteLine("PASS ironworks boss presentation ("+count+" checks: normal play draws, lights, sounds and changes nothing (the view's binding is None; the Siren keeps its name); "+det+"; "+contact+"; "+feet+"; "+pops+"; "+alloc+"; "+tiers+"; "+song
                +"; the plate and banner read THE CONVERTER for ironworks-converter; the cue keys are the doc's table; the whistle and the foundry's stop fall inside the 4 s victory hold)");
        }

        // ---- normal play: the view is None, and None draws, lights, sounds and changes nothing ----
        private static void NonePin(string root)
        {
            var none=IronworksBossView.None;
            Check(!none.live&&none.Action==-1&&!none.Dead(100)&&none.parts==IronworksBossView.AllParts&&none.Phase==1&&none.targetSeat==-1&&float.IsNaN(none.actionAt)&&float.IsNaN(none.deathAt)&&float.IsNaN(none.phaseAt),
                "the None view has no boss, action, phase change, broken part or death");
            Check(float.IsNaN(IronworksBossView.Time(-100))&&IronworksBossView.Time(12.5)==12.5f,"the wire's -100 reads as no time");
            var rig=new IronworksBossRig();var fx=new NativeIronworksBossFx();var cues=new IronworksBossCue[NativeIronworksBossCues.QueueSize];var level=new float[5];
            for(int f=0;f<120;f++)
            {
                float t=f/60f;rig.Update(t,1/60f,none,IronworksBossGround.Hall,Vector3.Zero,false,true,Target,double.NaN,double.NaN);fx.Build(rig,none,t,-26,3,-6,1,0,0,0,1,0,1,false);
                NativeIronworksBossCues.Loops(none,rig,t,level);
                Check(!rig.Visible&&fx.Quads.Count==0&&fx.LightCount==0&&fx.MoltenCount==0&&fx.HeatCount==0&&NativeIronworksBossCues.Collect(none,rig,fx,t-1/60f,t,t-1/60f,t,t-1/60f,t,cues)==0&&level.All(x=>x==0),
                    "None draws no boss, no effect, no light, no heat haze, no cue and no loop");
            }
            var st=NativeIronworksBossStage.Evaluate(none,50,float.NaN,rig);
            Check(!st.On&&st.Heat==1&&st.Lamps==1&&st.MusicDuck==1&&st.Whistle==0&&st.WakeA==0&&st.WakeB==0&&st.Shake==0,"None leaves the hall as it is (the furnaces' heat, the lamps, the music, the vessels)");
            var beat=new NativeZoneBossBeat{On=true,Mood=new NativeZoneBossMood{Heat=1.3f,Light=.8f}};var same=NativeIronworksBossStage.Apply(beat,st);
            Check(same.Mood.Heat==beat.Mood.Heat&&same.Mood.Light==beat.Mood.Light,"the music hook passes the song's beat through untouched without a boss");
            Check(!NativeIronworksBossPreview.On,"the DEV preview is off by default");
            string rt=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeIronworksBossPresentation.cs"));
            Check(rt.Contains("internal static IronworksBossView IronworksBossViewOf(NativeArena arena)=>IronworksBossView.None;"),"the one binding point returns None until Astra's record exists (update this pin when it is bound)");
            Check(NativeExpeditionStates.BossName("siren")=="SIREN"&&NativeZoneBossNames.BannerDelay("siren")==0&&NativeZoneBossNames.Name("siren")==null,"the Siren's plate name and banner timing are unchanged");
        }

        // ---- the DEV preview: a pure function of the clock ----
        private static void Determinism(out string summary)
        {
            var a=IronworksBossView.None;var b=IronworksBossView.None;int same=0;float loop=NativeIronworksBossPreview.Loop;
            Check(loop>NativeIronworksBossPreview.DeathAt+NativeIronworksBoss.Find("die").Total,"the preview's loop holds the whole death");
            for(int i=0;i<4000;i++)
            {
                float p=i*loop/4000;NativeIronworksBossPreview.View(p,10,ref a,out var va,out var ta,out var ra,out var da);NativeIronworksBossPreview.View(p,10,ref b,out var vb,out var tb,out var rb,out var db);
                Check(a.x==b.x&&a.z==b.z&&a.yaw==b.yaw&&a.action==b.action&&float.Equals(a.actionAt,b.actionAt)&&a.variant==b.variant&&a.phase==b.phase&&a.parts==b.parts&&float.Equals(a.deathAt,b.deathAt)&&va==vb&&ta==tb&&ra.Equals(rb)&&da.Equals(db),
                    "the preview's record is a pure function of the clock");
                NativeIronworksBossPreview.Start=0;NativeIronworksBossPreview.Fill(p+loop,ref b,out _,out _,out _,out _);NativeIronworksBossPreview.Start=double.NaN;
                Check(Math.Abs(a.x-b.x)<1e-3f&&a.action==b.action&&a.phase==b.phase&&a.parts==b.parts,"the preview loops with the same script");same++;
            }
            // Two runs at the same rate draw the same pose to the bit; at 60 and 120 fps the action timeline matches.
            var r60=new IronworksBossRig();var r120=new IronworksBossRig();var again=new IronworksBossRig();int compared=0;
            for(int f=0;f<=60*70;f++)
            {
                float t=f/60f;Step(r60,t,f==0?0:1/60f);Step(again,t,f==0?0:1/60f);
                if(f==0)Step(r120,t,0);else{Step(r120,t-1/120f,1/120f);Step(r120,t,1/120f);}
                if(f%30==0)
                {
                    Check(r60.Act.Active==r120.Act.Active&&Math.Abs(r60.Act.Age-r120.Act.Age)<1e-3f&&r60.ActIndex==r120.ActIndex&&r60.Arriving==r120.Arriving,"the action timeline matches at 60 and 120 fps ("+t+")");compared++;
                    for(int j=0;j<IronworksBossRig.Count;j++)Check(r60.Shown.Position[j]==again.Shown.Position[j]&&r60.Shown.Rotation[j]==again.Shown.Rotation[j],"two runs draw the same pose to the bit");
                }
            }
            summary="the DEV preview's record is a pure function of the clock ("+same+" samples, looping), two runs draw the same pose to the bit and the timeline matches at 60 and 120 fps ("+compared+" comparisons)";
        }
        private static readonly float[] blowAt=new float[8],blowSize=new float[8];private static readonly Vector3[] blowPush=new Vector3[8];
        private static void Step(IronworksBossRig r,float t,float dt)
        {
            var v=IronworksBossView.None;NativeIronworksBossPreview.View(t,0,ref v,out var vel,out var tg,out var arr,out var death);
            r.Update(t,dt,v,IronworksBossGround.Hall,vel,false,true,new Vector3(tg.X,1.6f,tg.Y),arr,death);
        }

        // ---- every action's contact lands on the record's hit within one frame (at a start between frames, at 60 and 144 fps) ----
        private static void Contact(out string summary)
        {
            float worst=0;var bodies=new System.Collections.Generic.List<string>();
            for(int i=0;i<NativeIronworksBoss.Actions.Length;i++)
            {
                var a=NativeIronworksBoss.Actions[i];if(a.Code>=NativeIronworksBoss.Arrive)continue;
                foreach(float fps in new[]{60f,144f})foreach(int variant in a.Id=="stomp"?new[]{0,1,2,3}:new[]{0})
                {
                    var v=Standing();v.action=a.Id;v.actionAt=2.0137f;v.variant=variant;v.targetX=v.x+(a.Id=="grab"?3.4f:5f);v.targetZ=v.z+(a.Id=="grab"?3.0f:-1.5f);if(a.Id=="call")v.variant=3;
                    float hit=v.actionAt+a.Windup;var rig=new IronworksBossRig();float struck=float.NaN,footAtHit=float.NaN,footBefore=float.NaN,tilt=float.NaN,tongAtHit=float.NaN;int leg=-1;
                    float rest=float.NaN;
                    for(int f=0;f<=(int)(fps*(v.actionAt+a.Total+.5f));f++)
                    {
                        float t=f/fps;rig.Update(t,f==0?0:1/fps,v,IronworksBossGround.Hall,Vector3.Zero,false,true,Target,double.NaN,double.NaN);
                        if(f==(int)fps){leg=a.Id=="stomp"?variant:0;rest=rig.Foot(leg).Y;}
                        if(leg>=0&&t>=hit-.25f&&float.IsNaN(footBefore))footBefore=rig.Foot(leg).Y;
                        if(float.IsNaN(struck)&&rig.Act.Active&&rig.Act.Struck)
                        {
                            struck=t;if(a.Id=="stomp")footAtHit=rig.Foot(rig.ActLeg).Y;if(a.Id=="slam")footAtHit=Math.Max(rig.Foot(0).Y,rig.Foot(1).Y);if(a.Id=="pour")tilt=rig.Tilt;
                            if(a.Id=="grab"){int side=rig.ActSide>0?1:0;tongAtHit=Vector2.Distance(new Vector2(rig.TongTip[side].X,rig.TongTip[side].Z),new Vector2(v.targetX,v.targetZ));}
                        }
                    }
                    Check(!float.IsNaN(struck),a.Id+" reaches its contact");float err=struck-hit;worst=Math.Max(worst,Math.Abs(err));
                    Check(err>=-1e-4f&&err<=1/fps+1e-4f,a.Id+"'s contact lands on the record's hit within one frame ("+err.ToString("0.0000")+" s at "+fps+" fps)");
                    if(a.Id=="stomp"){Check(footBefore>rest+.35f&&footAtHit<rest+.12f,"stomp "+variant+": the foot is up a quarter second before the hit and down on it ("+(footBefore-rest).ToString("0.00")+" m, "+(footAtHit-rest).ToString("0.00")+" m)");if(fps==60&&variant==1)bodies.Add("the stomping foot "+(footAtHit-rest).ToString("0.00")+" m off the floor on the hit");}
                    if(a.Id=="slam"){Check(footAtHit<rest+.15f,"slam: both front feet come down on the hit ("+(footAtHit-rest).ToString("0.00")+" m)");if(fps==60)bodies.Add("the slam's feet "+(footAtHit-rest).ToString("0.00")+" m");}
                    if(a.Id=="pour"){Check(tilt>55,"pour: the vessel is over past its lip on the hit ("+tilt.ToString("0")+" deg)");if(fps==60)bodies.Add("the pour tipped "+tilt.ToString("0")+" degrees");}
                    if(a.Id=="grab"){Check(tongAtHit<1.6f,"grab: the tongs close on the target on the hit ("+tongAtHit.ToString("0.00")+" m)");if(fps==60)bodies.Add("the grab's tongs "+tongAtHit.ToString("0.00")+" m from the target");}
                }
            }
            summary="every action's contact lands on the record's hit within one frame at 60 and 144 fps (worst "+(worst*1000).ToString("0.0")+" ms; "+string.Join(", ",bodies)+")";
        }
        private static IronworksBossView Standing(){var v=IronworksBossView.None;v.live=true;v.x=-35;v.z=0;v.yaw=90;v.phase=1;v.maxHp=6000;v.hp=6000;v.spawned=-100;return v;}

        // ---- planted feet do not slip: walking a curve, stopping, turning on the spot, the phase-3 charge's run ----
        private static void Feet(out string summary)
        {
            var rig=new IronworksBossRig();var prev=new Vector3[4];var held=new bool[4];float worst=0;int plants=0;const float fps=60;
            for(int f=0;f<=(int)(fps*24);f++)
            {
                float t=f/fps;var v=Standing();GaitPath(t,out var p,out var vel,out float yaw,out int phase);v.x=p.X;v.z=p.Z;v.yaw=yaw;v.phase=phase;
                rig.Update(t,f==0?0:1/fps,v,IronworksBossGround.Hall,vel,false,true,Target,double.NaN,double.NaN);
                for(int i=0;i<4;i++)
                {
                    bool now=rig.PlantedFree(i);var h=rig.Foot(i);
                    if(now&&held[i])worst=Math.Max(worst,Vector3.Distance(h,prev[i]));
                    if(now&&!held[i])plants++;held[i]=now;prev[i]=h;
                }
            }
            Check(plants>40,"the gait planted its feet ("+plants+")");
            Check(worst<.01f,"a planted foot slips less than 1 cm ("+(worst*100).ToString("0.000")+" cm at worst) walking, stopping, turning on the spot and running");
            summary=plants+" plants walking a curve, turning on the spot and running at 11 m/s: a planted foot slips "+(worst*1000).ToString("0.00")+" mm at worst (under 1 cm)";
        }
        // A walk on a curve (1.6 m/s), a stop, a 150 degree turn on the spot, a phase-3 run across the hall at 11 m/s, a stop.
        private static void GaitPath(float t,out Vector3 p,out Vector3 vel,out float yaw,out int phase)
        {
            Vector3 P(float s)
            {
                var c=new Vector3(-37,0,0);const float r=6;float a=Math.Min(s,8)*.27f;var e=c+new Vector3((float)Math.Sin(a)*r,0,-(float)Math.Cos(a)*r);if(s<15)return e;
                float k=s-15;float d=k<.6f?9.2f*k*k:k<1.6f?3.3f+11f*(k-.6f):k<2.4f?14.3f+11f*(k-1.6f)-6.9f*(k-1.6f)*(k-1.6f):14.3f+8.8f-4.4f;
                float y0=Y(15)*BossMath.Deg;return e+BossMath.Forward(y0)*d;
            }
            float Y(float s){float a=Math.Min(s,8)*.27f;float y=a/BossMath.Deg+90;if(s>8.5f)y+=BossMath.Smoother((s-8.5f)/5f)*150;return y;}
            p=P(t);vel=(P(t+1/240f)-P(t-1/240f))*120;yaw=Y(t);phase=t>14?3:1;
        }

        // ---- no pops: the per-frame change halves when the frame rate doubles, across every transition the preview plays ----
        private static void Pops(out string summary)
        {
            float Wu(string id)=>NativeIronworksBoss.Find(id).Windup;
            float[] at={NativeIronworksBossPreview.SpawnAt+NativeIronworksBossBeats.Current.Hit,NativeIronworksBossPreview.SpawnAt+NativeIronworksBossBeats.Current.Climb,NativeIronworksBossPreview.ArrivalEnd-.4f,
                NativeIronworksBossPreview.PourAt,NativeIronworksBossPreview.PourAt+Wu("pour"),NativeIronworksBossPreview.BlowAt+Wu("blow"),NativeIronworksBossPreview.StompAt+Wu("stomp"),NativeIronworksBossPreview.GrabAt+Wu("grab"),
                NativeIronworksBossPreview.ChokeAt+.9f,NativeIronworksBossPreview.KneelAt,NativeIronworksBossPreview.Phase2At,NativeIronworksBossPreview.SlamAt+Wu("slam"),NativeIronworksBossPreview.DeslagAt+Wu("deslag"),
                NativeIronworksBossPreview.FreedGrabAt+Wu("grab")+NativeIronworksBoss.Find("grab").Strike+.55f,NativeIronworksBossPreview.Phase3At,NativeIronworksBossPreview.ChargeAt+Wu("charge"),NativeIronworksBossPreview.CrashAt,NativeIronworksBossPreview.DeathAt,NativeIronworksBossPreview.DeathAt+1.2f};
            var d60=MaxDeltas(at,60);var d120=MaxDeltas(at,120);float worstRatio=0;string worstAt="";
            for(int i=0;i<at.Length;i++)
            {
                float ratio=d120[i]/Math.Max(1e-5f,d60[i]);
                Check(d60[i]<1.6f,"no joint jumps more than 1.6 m in a 60 fps frame round "+at[i].ToString("0.00")+" s ("+d60[i].ToString("0.00")+")");
                Check(ratio<.7f,"the per-frame change halves when the frame rate doubles round "+at[i].ToString("0.00")+" s (ratio "+ratio.ToString("0.00")+": no pop)");
                if(ratio>worstRatio){worstRatio=ratio;worstAt=at[i].ToString("0.0");}
            }
            summary="across "+at.Length+" transitions (the arrival's grip, climb and hand-off, action starts and contacts, a choked blow, the kneel, both phase changes, a freed grab, the charge, the crash, the death) the per-frame change at 120 fps is at most "+worstRatio.ToString("0.00")+" of 60 fps's (no pop)";
        }
        // The largest per-frame joint move (relative to the root; the flung crust's free flight and hidden joints aside) within
        // +-0.5 s of each moment at `fps`, simulating the preview once from its start.
        private static float[] MaxDeltas(float[] at,float fps)
        {
            var rig=new IronworksBossRig();var last=new BossPose(IronworksBossRig.Count);var worst=new float[at.Length];bool has=false;float end=at.Max()+.6f;
            for(int f=0;;f++)
            {
                float t=f/fps;if(t>end)break;Step(rig,t,f==0?0:1/fps);
                bool near=false;for(int i=0;i<at.Length;i++)if(Math.Abs(t-at[i])<=.5f)near=true;
                if(near&&has)
                {
                    float d=0;
                    for(int j=0;j<IronworksBossRig.Count;j++)
                    {
                        if(rig.Hidden[j]||j>=NativeIronworksBossLook.ChunkBase&&j<NativeIronworksBossLook.ChunkBase+8)continue;
                        d=Math.Max(d,Vector3.Distance(rig.Shown.Position[j]-rig.Shown.RootPosition,last.Position[j]-last.RootPosition));
                    }
                    for(int i=0;i<at.Length;i++)if(Math.Abs(t-at[i])<=.5f)worst[i]=Math.Max(worst[i],d);
                }
                last.CopyFrom(rig.Shown);has=true;
            }
            return worst;
        }

        // ---- nothing allocates per frame: the rig, the effects, the stage hook, the cues and the loops over the whole preview ----
        private static void Allocation(out string summary)
        {
            var rig=new IronworksBossRig();var fx=new NativeIronworksBossFx();var cues=new IronworksBossCue[NativeIronworksBossCues.QueueSize];var level=new float[5];var v=IronworksBossView.None;double lastArr=double.NaN,lastDeath=double.NaN;
            void Frame(int f)
            {
                float t=f/60f;NativeIronworksBossPreview.View(t,0,ref v,out var vel,out var tg,out var arr,out var death);rig.Update(t,1/60f,v,IronworksBossGround.Hall,vel,false,true,new Vector3(tg.X,1.6f,tg.Y),arr,death);
                int nb=NativeIronworksBossPreview.Blows(t,0,blowAt,blowPush,blowSize);for(int i=0;i<nb;i++)if(blowAt[i]>t-1/60f&&blowAt[i]<=t)rig.Hit(blowAt[i],blowPush[i],blowSize[i]);
                fx.Build(rig,v,t,-26,3,-6,1,0,0,0,1,0,1,false);var st=NativeIronworksBossStage.Evaluate(v,t,rig.DeathAge,rig);NativeIronworksBossStage.Apply(default,st);
                float af=(float)(double.IsNaN(lastArr)?-1:lastArr),at=(float)(double.IsNaN(arr)?-1:arr),df=(float)(double.IsNaN(lastDeath)?-1:lastDeath),de=(float)(double.IsNaN(death)?-1:death);
                NativeIronworksBossCues.Queue(v,rig,fx,t-1/60f,t,af,at,df,de);NativeIronworksBossCues.Drain(cues);NativeIronworksBossCues.Loops(v,rig,t,level);lastArr=arr;lastDeath=death;
            }
            int frames=(int)(NativeIronworksBossPreview.Loop*60);for(int f=0;f<frames;f++)Frame(f);
            long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<frames;f++)Frame(f);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"the rig, the effects, the stage hook, the cues and the loops allocate nothing per frame ("+used+" bytes over "+frames+" frames)");
            summary="0 bytes per frame over the whole "+NativeIronworksBossPreview.Loop.ToString("0")+" s preview (rig, effects, stage hook, cues, loops)";
        }

        // ---- the three tiers: inside the budgets with the chains drawn, every joint ridden by a valid section ----
        private static void Tiers(out string summary)
        {
            var drawn=new int[3];
            for(int tier=0;tier<3;tier++)
            {
                var m=NativeIronworksBossLook.Build(tier);var again=NativeIronworksBossLook.Build(tier);drawn[tier]=NativeIronworksBossLook.Drawn(m,IronworksBossRig.LinksDrawn(tier));
                Check(drawn[tier]<=NativeIronworksBossLook.Budget[tier],"tier "+tier+" with its chains is inside its budget ("+drawn[tier]+" of "+NativeIronworksBossLook.Budget[tier]+")");
                Check(m.Body.Sections.Count==again.Body.Sections.Count&&m.Body.Sections.Zip(again.Body.Sections,(x,y)=>x.Positions.SequenceEqual(y.Positions)).All(x=>x),"tier "+tier+" builds deterministically");
                foreach(var s in m.Body.Sections)Check(s.Bone>=0&&s.Bone<NativeIronworksBossLook.Count,"every section rides a joint");
                foreach(int j in new[]{NativeIronworksBossModel.Vessel,NativeIronworksBossModel.Yoke,NativeIronworksBossLook.LampL,NativeIronworksBossLook.LampR,NativeIronworksBossLook.Head,NativeIronworksBossLook.Visor,NativeIronworksBossLook.Metal})
                    Check(m.Body.Sections.Any(s=>s.Bone==j),NativeIronworksBossLook.Names[j]+" is built on tier "+tier);
                for(int g=0;g<4;g++)Check(m.Body.Sections.Any(s=>s.Bone==NativeIronworksBossModel.CrustFront+g),"crust group "+g+" is built on tier "+tier);
                Check(m.Body.Sections.Any(s=>s.Bone==NativeIronworksBossLook.Visor&&s.Emission.Any(e=>e.X+e.Y+e.Z>0)),"the foreman's visor glows on tier "+tier);
            }
            Check(drawn[0]>drawn[1]&&drawn[1]>drawn[2],"each tier is lighter than the last");
            summary="tiers "+drawn[0]+" / "+drawn[1]+" / "+drawn[2]+" triangles with their chains, inside "+string.Join(" / ",NativeIronworksBossLook.Budget);
        }

        // ---- the HUD: the plate and the banner name the zone boss ----
        private static void Names()
        {
            Check(NativeExpeditionStates.BossName(NativeIronworksBoss.Archetype)==NativeIronworksBoss.PlateName&&NativeIronworksBoss.PlateName=="THE CONVERTER","the boss plate names ironworks-converter THE CONVERTER");
            Check(NativeZoneBossNames.Name(NativeIronworksBoss.Archetype)=="THE CONVERTER"&&NativeZoneBossNames.BannerDelay(NativeIronworksBoss.Archetype)==0,"its banner shows from the arrival's spawned (doc 7.1)");
            Check(NativeZoneBossNames.Name(NativeStationBoss.Archetype)==NativeStationBoss.Name,"THE 6:47 keeps its name");
            const string area="expedition-ironworks";
            var cues=new ExpeditionStateCues();var v=new ExpeditionView{area=area,phase=ExpeditionPhase.Instance,
                bossArrivals=new[]{new ExpeditionBossArrivalView{id="b",area=area,archetype=NativeIronworksBoss.Archetype,spawned=10,x=-35,y=0,z=0,hp=6000,maxHp=6000}},instances=new[]{new ExpeditionViewInstance{id=area}}};
            cues.Observe(v,10.1);var model=new ExpeditionBossModel();model.From(v,cues,10.5,null);
            Check(model.Intro>=0&&model.Name=="THE CONVERTER","the arrival banner reads THE CONVERTER");
            Check(model.PlateCount==1&&model.PlateNames[0]=="THE CONVERTER","the plate reads THE CONVERTER");
            Check(NativeIronworksBossPreview.BannerArea.EndsWith("CITY IRONWORKS",StringComparison.Ordinal),"the DEV preview's banner names the area");
        }

        // ---- the arrival's beats are Hydraulic Hell's (its map's slams and downbeats), the fallback matches them ----
        private static void Song(string root,out string summary)
        {
            var music=NativeZoneBossMusic.Parse(File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/ZoneBossMusic.json")));
            var song=music.Song(NativeZoneMusic.IronworksBoss);Check(song!=null,"the Ironworks boss song is in the map");
            var b=IronworksBossBeats.From(song);Check(b.Valid,"the song's accents place the arrival's beats in order");
            double Boss(NativeMusicAccent a)=>song.FirstBoss(a.T);
            var slams=song.Accents.Where(a=>a.Kind==NativeZoneBossSong.AccentSlam).Select(Boss).Where(x=>!double.IsNaN(x)&&x>.5&&x<song.Hold).OrderBy(x=>x).ToArray();
            Check(slams.Length>=2&&Math.Abs(b.Hit-slams[0])<1e-3&&Math.Abs(b.Climb-slams[1])<1e-3,"the tongs grip on the first hydraulic hit and the rear legs climb on the second");
            Check(Math.Abs(b.Drop-song.Hold)<1e-3&&b.Hold==b.Drop,"the first blow is on the drop at the hold's end");
            Check(song.Track.beats.Any(c=>Math.Abs(song.FirstBoss(c)-b.Flare)<1e-3)&&song.Track.beats.Any(c=>Math.Abs(song.FirstBoss(c)-b.Heave)<1e-3),"the heave and the furnace flare fall on the song's beats");
            var f=IronworksBossBeats.Fallback;
            Check(Math.Abs(f.Hit-b.Hit)<2e-3&&Math.Abs(f.Heave-b.Heave)<2e-3&&Math.Abs(f.Climb-b.Climb)<2e-3&&Math.Abs(f.Flare-b.Flare)<2e-3&&Math.Abs(f.Drop-b.Drop)<2e-3,"the fallback beats are the map's (the arrival looks the same without it)");
            Check(NativeIronworksBossBeats.UseSong(song)&&NativeIronworksBossBeats.Bound&&NativeIronworksBossBeats.UseSong(song),"the presenter binds the song (again a no-op)");
            // On the song's beats: the arrival's timeline (grip, climb, the lamps on the flare, the hand-off by the drop's tail) and no pop.
            var rig=new IronworksBossRig();var v=IronworksBossView.None;bool lampsBefore=false,lampsAfter=false,arrivedAfter=false;
            for(int k=0;k<(int)((NativeIronworksBossPreview.ArrivalEnd+1)*60);k++)
            {
                float t=k/60f;NativeIronworksBossPreview.View(t,0,ref v,out var vel,out var tg,out var arr,out var death);rig.Update(t,1/60f,v,IronworksBossGround.Hall,vel,false,true,new Vector3(tg.X,1.6f,tg.Y),arr,death);
                if(rig.Arriving&&rig.ArrivalAge<b.Flare-.05f&&rig.LampLevel>.05f)lampsBefore=true;if(rig.Arriving&&rig.ArrivalAge>b.Flare+.3f&&rig.LampLevel>.5f)lampsAfter=true;
                if(t>NativeIronworksBossPreview.SpawnAt+b.Drop+IronworksBossRig.ArrivalTail+.05f&&rig.Arriving)arrivedAfter=true;
            }
            Check(!lampsBefore&&lampsAfter,"the lamps click on with the furnace flare");Check(!arrivedAfter,"the fight takes the boss over after the drop's first blow");
            NativeIronworksBossBeats.Clear();Check(!NativeIronworksBossBeats.Bound,"cleared, the fallback returns");
            summary="the arrival on Hydraulic Hell's map: grip "+b.Hit.ToString("0.00")+" s, heave "+b.Heave.ToString("0.00")+", climb "+b.Climb.ToString("0.00")+", flare "+b.Flare.ToString("0.00")+", first blow on the drop "+b.Drop.ToString("0.00")+" (the fallback matches)";
        }

        // ---- the cue keys: the doc's table, each reachable in the preview, played through the bank path ----
        private static void Cues(string root)
        {
            string doc=File.ReadAllText(Path.Combine(root,"../../docs/EXPEDITION-BOSS-CITY-IRONWORKS.md"));
            var table=Regex.Matches(doc,@"^\| `(boss\.ironworks\.[a-z.-]+)` \| \d \|",RegexOptions.Multiline).Cast<Match>().Select(m=>m.Groups[1].Value).ToArray();
            Check(table.Length==NativeIronworksBossCues.Keys.Length&&table.SequenceEqual(NativeIronworksBossCues.Keys),"the cue keys are the doc's table, in its order ("+table.Length+")");
            var heard=new bool[NativeIronworksBossCues.Keys.Length];var rig=new IronworksBossRig();var fx=new NativeIronworksBossFx();var v=IronworksBossView.None;var buf=new IronworksBossCue[NativeIronworksBossCues.QueueSize];var level=new float[5];
            double lastArr=double.NaN,lastDeath=double.NaN;
            for(int f=0;f<(int)(NativeIronworksBossPreview.Loop*60);f++)
            {
                float t=f/60f;NativeIronworksBossPreview.View(t,0,ref v,out var vel,out var tg,out var arr,out var death);rig.Update(t,1/60f,v,IronworksBossGround.Hall,vel,false,true,new Vector3(tg.X,1.6f,tg.Y),arr,death);
                int nb=NativeIronworksBossPreview.Blows(t,0,blowAt,blowPush,blowSize);for(int i=0;i<nb;i++)if(blowAt[i]>t-1/60f&&blowAt[i]<=t)rig.Hit(blowAt[i],blowPush[i],blowSize[i]);
                fx.Build(rig,v,t,-26,3,-6,1,0,0,0,1,0,1,false);
                float af=(float)(double.IsNaN(lastArr)?-1:lastArr),at=(float)(double.IsNaN(arr)?-1:arr),df=(float)(double.IsNaN(lastDeath)?-1:lastDeath),de=(float)(double.IsNaN(death)?-1:death);
                int n=NativeIronworksBossCues.Collect(v,rig,fx,t-1/60f,t,af,at,df,de,buf);for(int i=0;i<n;i++)heard[buf[i].Key]=true;
                NativeIronworksBossCues.Loops(v,rig,t,level);for(int i=0;i<level.Length;i++)if(level[i]>0)heard[NativeIronworksBossCues.LoopKeys[i]]=true;lastArr=arr;lastDeath=death;
            }
            var missing=Enumerable.Range(0,heard.Length).Where(i=>!heard[i]).Select(i=>NativeIronworksBossCues.Keys[i]).ToArray();
            Check(missing.Length==0,"every boss.ironworks key plays somewhere in the preview (missing: "+string.Join(", ",missing)+")");
            string au=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeIronworksBossAudio.cs"));
            Check(au.Contains("Cue(NativeIronworksBossCues.Keys[")&&!au.Contains("Fallback"),"the cues play through the bank path with no stand-in (silent without clips)");
            string na=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeAudio.cs"));Check(na.Contains("DoorHumFrame(arena,menu);IronworksBossLoopFrame(arena,menu);"),"NativeAudio calls the boss's audio frame once, beside the door hum (not in the music branch)");
        }

        // ---- the death: the whistle and the foundry's stop fall inside the 4 s victory hold ----
        private static void Death()
        {
            Check(NativeIronworksBossStage.WhistleAt<4&&NativeIronworksBossStage.StopAt+NativeIronworksBossStage.StopSeconds<=4,"the shift whistle blows and the foundry stops inside the 4 s victory hold");
            var v=Standing();v.deathAt=1;v.hp=0;var rig=new IronworksBossRig();float heatAt4=float.NaN,whistle=0;
            for(int f=0;f<=60*6;f++){float t=f/60f;rig.Update(t,f==0?0:1/60f,v,IronworksBossGround.Hall,Vector3.Zero,false,true,Target,double.NaN,t>=1?t-1:double.NaN);var st=NativeIronworksBossStage.Evaluate(v,t,rig.DeathAge,rig);
                if(t<=5)whistle=Math.Max(whistle,st.Whistle);if(Math.Abs(t-5)<1e-4f)heatAt4=st.Heat;}
            Check(whistle>.99f&&heatAt4<.4f,"by the hold's end the whistle has blown and the furnaces are down ("+heatAt4.ToString("0.00")+")");
        }

        // ---- the source: presentation only ----
        private static void Source(string root)
        {
            var files=Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Core"),"NativeIronworksBoss*.cs").Where(f=>!f.EndsWith("NativeIronworksBossModel.cs",StringComparison.Ordinal))
                .Concat(Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"NativeIronworksBoss*.cs")).ToArray();
            Check(files.Length>=11,"the presentation's files ("+files.Length+")");
            foreach(var f in files)
            {
                string s=File.ReadAllText(f);
                foreach(var bad in new[]{"Rigidbody","Collider","NativeNetwork","Physics.","Damage(","BeginPveHold","RecordBossArrival","NativeExpeditionM1Arena","Cooldown="})Check(!s.Contains(bad),Path.GetFileName(f)+" stays presentation only (no "+bad+")");
            }
        }
    }
}
#endif
