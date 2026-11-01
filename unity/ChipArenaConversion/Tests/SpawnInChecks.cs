using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The match-start spawn-in (Core/NativeSpawnIn.cs, Runtime/NativeSpawnInPresentation.cs, docs/SPAWN-IN.md). Checked: the
    // root cause it presents, from data (every authored spawn on every map sits 0.23 m over the ground motor's rest); the clock
    // (the countdown is NativeMatch's, the hold ends exactly at the fight start, the strike at the countdown's start, the body
    // whole well before GO, nothing jumps); every round of a real NativeMatch through results, a draft, a new set and the
    // intros; guests and the killcam from replicated frames at 20 Hz agree with the host's fight start, the guest's own pilot
    // GuestLead ahead; the drop is the authority's own fall from each spawn (re-derived here from the ground probe's rule) and
    // a grounded spawn is lowered by GO instead; the body forms from the feet up in rest and idle poses with the hologram
    // ahead; tiers (Full, Reduced, Low); geometry bounds and the beam's strike, release and landing; the camera kick and the
    // first-person screen stay gentle; no allocation; and the Runtime wiring reads only replicated state, restores what it
    // overrides and is referenced by presentation files only.
    internal static class SpawnInChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Spawn-in: "+what);count++;}
        private static void Near(double a,double b,double tolerance,string what){Check(Math.Abs(a-b)<=tolerance&&!double.IsNaN(a),what+": "+a+" != "+b);}
        private const float Dt=1f/60;
        public static void Run(string root,ConversionContent data)
        {
            var o=new JsonSerializerOptions{IncludeFields=true};
            var unity=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),o);
            RootCause(data,unity);Clock();Rounds(data);Guests(data);Fall(data,unity);Build();Tiers();Geometry();Camera();Allocation();Wiring(root);
            Console.WriteLine("PASS spawn-in ("+count+" checks: every authored spawn "+Lift.lo.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+" m over the motor's rest on all 18 maps ("+NativeSpawnIn.Mode(Lift.lo)+"); NativeMatch's 3 s countdown, hold ends exactly at the fight start, strike at its start, body whole 1.5 s before GO, no jump in lift; every round of a real match through results, a draft, a new set and intros; guests and the killcam from 20 Hz frames agree with the host, own pilot GuestLead ahead; the drop is the authority's own fall (4 ticks + 1) and a grounded spawn is lowered by GO; feet-up build in rest and idle poses, hologram ahead; Full/Reduced/Low tiers; geometry bounds, strike, release and landing; gentle kick and screen; zero allocation; wiring reads replicated state only, restores the flag, presentation files only)");
        }

        // ---- the float, from data: every authored spawn is a capsule centre 0.23 m above the ground motor's rest on its floor ----
        private static void RootCause(ConversionContent data,UnityMapContent unity)
        {
            Near(NativeSpawnIn.RestCentre,.87,1e-6,"the motor's rest is HalfHeight + Skin");Near(NativeSpawnIn.RestFeet,.005,1e-6,"its presented feet sit 5 mm over the floor");
            // Measured, not pinned: grounding the spawns (docs/SPAWN-IN.md, option 1) keeps this passing and switches the mode.
            int maps=0;float lo=float.MaxValue,hi=float.MinValue;
            void Spawns(ContentMap m,IEnumerable<V3> points)
            {
                maps++;
                foreach(var p in points)
                {
                    float floor=Floor(m,p);Check(!float.IsNaN(floor),m.id+": a floor under the spawn at "+p.x+","+p.z);
                    float lift=NativeSpawnIn.SpawnLift(p.y,floor);lo=Math.Min(lo,lift);hi=Math.Max(hi,lift);
                    Check(NativeSpawnIn.Mode(lift)==(lift>NativeSpawnIn.LiftMin?SpawnInMode.Drop:SpawnInMode.Lower),m.id+": the spawn's lift picks the release");
                    Check(NativeSpawnIn.AuthoredSpawn(data,m.spawns!=null?m:Legacy(m),p.x,p.z,out float found)&&Math.Abs(found-p.y)<1e-6f,m.id+": the presentation finds the authored spawn");
                    Check(NativeSpawnIn.AuthoredFloor(m,p.x,p.z,p.y,out float authoredFloor)&&Math.Abs(authoredFloor-floor)<1e-5f,m.id+": the authored floor fallback agrees ("+authoredFloor+")");
                }
            }
            foreach(var m in data.maps)Spawns(m,data.spawns);
            foreach(var m in unity.maps)Spawns(m,m.spawns.ffa.Select(s=>s.pos).Concat(m.spawns.teams.SelectMany(t=>t.points).Select(s=>s.pos)));
            Check(maps==data.maps.Length+unity.maps.Length&&maps>=18,"every map's spawns ("+maps+" maps)");Lift=(lo,hi);
            Check(hi-lo<1e-4f,"every spawn on every map has the same lift over the motor's rest ("+lo+".."+hi+" m)");
            // The presented hold is the spawn's own height: the feet the authority spawns at (1.1 - .865) are where the beam holds them.
            float hold=NativeSpawnIn.HoldFor(SpawnInMode.Drop,NativeSpawnIn.SpawnLift(1.1f,0));Near(hold+NativeSpawnIn.RestFeet,1.1-NativeSpawnIn.FeetOffset,1e-5,"the beam holds the feet where the authority spawned them");
        }
        private static (float lo,float hi) Lift;
        private static ContentMap Legacy(ContentMap m)=>new ContentMap{id=m.id,blocks=m.blocks};
        // The highest walkable top under (x, z) at or below the capsule centre: the floor the motor would rest on.
        private static float Floor(ContentMap m,V3 p)
        {
            float best=float.NaN;
            foreach(var b in m.blocks){if(Math.Abs(p.x-b.pos.x)>b.size.x/2||Math.Abs(p.z-b.pos.z)>b.size.z/2)continue;float top=b.pos.y+b.size.y/2;if(top<=p.y+1e-3f&&(float.IsNaN(best)||top>best))best=top;}
            return best;
        }

        // ---- the clock ----
        private static void Clock()
        {
            Near(NativeSpawnIn.End(NativePhase.Countdown,12,3),12,0,"the countdown ends at its PhaseEnd");Near(NativeSpawnIn.End(NativePhase.Fight,40,12.0167),12.0167,0,"the fight's recorded start");
            Near(NativeSpawnIn.End(NativePhase.Result,40,12),12,0,"a result keeps its fight's start");
            foreach(var p in new[]{NativePhase.Lobby,NativePhase.Draft,NativePhase.Intro,NativePhase.Match})Check(double.IsNaN(NativeSpawnIn.End(p,5,5)),"no spawn-in in "+p);
            Near(NativeSpawnIn.Age(NativePhase.Countdown,12,0,8.9),NativeSpawnIn.StrikeAt,1e-6,"a clock a tick behind the reset reads the strike, never a whole body");
            Check(float.IsNaN(NativeSpawnIn.Age(NativePhase.Draft,12,0,10)),"draft: none");
            Near(NativeSpawnIn.StrikeAt,-NativeSpawnIn.Countdown,0,"the strike at the countdown's start");Check(NativeSpawnIn.BuiltAt<=-1.5f+1e-4f,"the body is whole 1.5 s before GO ("+NativeSpawnIn.BuiltAt+")");
            foreach(var mode in new[]{SpawnInMode.Drop,SpawnInMode.Lower})foreach(bool reduced in new[]{false,true})
            {
                float hold=mode==SpawnInMode.Drop?.23f:NativeSpawnIn.Hover,drop=mode==SpawnInMode.Drop?NativeSpawnIn.DropFor(.23f):0,landAt=mode==SpawnInMode.Drop?drop:0;
                float lastLift=float.NaN,lastFront=-1,maxStep=0;bool heldOk=true,activeOk=true,frontOk=true;
                for(float age=NativeSpawnIn.StrikeAt;age<landAt+NativeSpawnIn.LandSeconds+.2f;age+=1f/240)
                {
                    var f=NativeSpawnIn.Evaluate(age,mode,hold,drop,7,reduced);
                    heldOk&=f.Held==(age<0)&&f.Falling==(mode==SpawnInMode.Drop&&age>=0&&age<drop);
                    activeOk&=f.Active==(age<landAt+NativeSpawnIn.LandSeconds);
                    if(f.Active){frontOk&=f.Front>=lastFront-1e-5f;lastFront=f.Front;if(!float.IsNaN(lastLift))maxStep=Math.Max(maxStep,Math.Abs(f.Lift-lastLift));lastLift=f.Lift;}
                    if(age<(mode==SpawnInMode.Drop?0:-NativeSpawnIn.LowerSeconds))Check(f.Lift>=hold*.8f,"the beam holds the pilot clear of the floor until its release ("+mode+" "+age+")");
                    if(age>=landAt)Check(f.Lift==0,"on the floor from the touchdown ("+mode+")");
                }
                Check(heldOk,"held exactly until the fight start, falling only for the drop ("+mode+")");Check(activeOk,"active from the strike to the landing's end ("+mode+")");Check(frontOk,"the build front never falls back");
                float speed=mode==SpawnInMode.Drop?2*hold/drop:1.5f*hold/NativeSpawnIn.LowerSeconds;Check(maxStep<=speed/240*1.05f+1e-4f,"no jump in the lift ("+mode+" "+maxStep+" per 1/240 s)");
                var before=NativeSpawnIn.Evaluate(-1e-4f,mode,hold,drop,7,reduced);
                if(mode==SpawnInMode.Drop)Near(before.Lift,hold,2e-4,"the bob has settled: the drop starts from the hold at GO");else Near(before.Lift,0,2e-3,"a lowered pilot is on the floor at GO");
                Check(NativeSpawnIn.Evaluate(NativeSpawnIn.BuiltAt+.01f,mode,hold,drop,7,reduced).Built==1,"whole after BuiltAt");
                Check(!NativeSpawnIn.Evaluate(NativeSpawnIn.StrikeAt-.01f,mode,hold,drop,7,reduced).Active,"nothing before the strike in the fight phase");
            }
            // Pure: the same inputs give the same frame; the seed is the pilot's id and the replicated fight start's tick.
            var a=NativeSpawnIn.Evaluate(-1.13f,SpawnInMode.Drop,.23f,.083f,NativeSpawnIn.Seed(3,42.5),false);var b=NativeSpawnIn.Evaluate(-1.13f,SpawnInMode.Drop,.23f,.083f,NativeSpawnIn.Seed(3,42.5),false);
            Check(a.Lift==b.Lift&&a.Split==b.Split&&a.Beam==b.Beam&&a.Front==b.Front,"pure");Check(NativeSpawnIn.Seed(3,42.5)==NativeSpawnIn.Seed(3,42.5+1e-9)&&NativeSpawnIn.Seed(3,42.5)!=NativeSpawnIn.Seed(4,42.5),"the seed: the id and the fight start's tick");
        }

        // ---- every round of a real match: results, a draft, a new set, the intros ----
        private static NativeMatch Match(ConversionContent data,bool intros)
        {
            var m=new NativeMatch(data,1234){RoundsPerSet=2,SetsToWin=3,ShowIntros=intros,SkipBotDrafts=false};
            m.Seats.Add(new NativeSeat{id=1,name="One",spec="sword",team=0});m.Seats.Add(new NativeSeat{id=2,name="Bot",spec="rifle",team=1,bot=true});
            return m;
        }
        private static void Rounds(ConversionContent data)
        {
            foreach(bool intros in new[]{false,true})
            {
                var m=Match(data,intros);Check(m.Start(),"the match starts");
                if(!intros)Near(m.PhaseEnd-m.Time,NativeSpawnIn.Countdown,1e-9,"NativeMatch's countdown is NativeSpawnIn.Countdown");
                int countdowns=0,fights=0,drafts=0,introTicks=0;double countdownEnd=double.NaN;bool sawStrike=false,sawGo=false;int round=0;
                for(int tick=0;tick<60*240&&m.Phase!=NativePhase.Match;tick++)
                {
                    var phase=m.Phase;double now=m.Time;float age=NativeSpawnIn.Age(m.Phase,m.PhaseEnd,m.FightAt,now);
                    if(phase==NativePhase.Countdown)
                    {
                        if(double.IsNaN(countdownEnd)||Math.Abs(countdownEnd-m.PhaseEnd)>1e-6){countdownEnd=m.PhaseEnd;countdowns++;Near(m.PhaseEnd-m.Time,NativeSpawnIn.Countdown,1.5*Dt,"every countdown is 3 s");Near(age,NativeSpawnIn.StrikeAt,1.5*Dt,"each countdown starts with the strike");sawStrike=true;}
                        var f=NativeSpawnIn.Evaluate(age,SpawnInMode.Drop,.23f,.083f,1,false);Check(f.Active&&f.Held&&age<0,"held through every countdown");
                    }
                    else if(phase==NativePhase.Fight)
                    {
                        if(!double.IsNaN(countdownEnd)){Near(m.FightAt,countdownEnd,Dt+1e-9,"the fight starts where the countdown ended");countdownEnd=double.NaN;fights++;sawGo=true;}
                        var f=NativeSpawnIn.Evaluate(age,SpawnInMode.Drop,.23f,.083f,1,false);Check(!f.Held&&f.Active==(age<.083f+NativeSpawnIn.LandSeconds),"the hold ends at GO and the landing ends");
                        if(m.Time-m.FightAt>1.5){m.Seat(round%3==2?1:2).alive=false;m.Resolve();round++;}
                    }
                    else if(phase==NativePhase.Result){Check(!NativeSpawnIn.Evaluate(age,SpawnInMode.Drop,.23f,.083f,1,false).Active,"nothing in the result");}
                    else if(phase==NativePhase.Draft){drafts++;Check(float.IsNaN(age),"nothing in the draft");}
                    else if(phase==NativePhase.Intro){introTicks++;Check(float.IsNaN(age),"nothing in an intro (the entrance owns the screen)");}
                    m.Tick(Dt);
                }
                Check(m.Phase==NativePhase.Match,"the match ran to its end");Check(countdowns>=6&&fights==countdowns,"a spawn-in on every round ("+countdowns+" countdowns, "+fights+" fights)");
                Check(drafts>0,"a draft between sets");Check(sawStrike&&sawGo,"strike and GO seen");Check(intros==(introTicks>0),"intros chain before the first countdown");
            }
        }

        // ---- guests and the killcam from replicated frames ----
        private static void Guests(ConversionContent data)
        {
            var clock=new NativeSnapshotClock();clock.Reset(5);Near(clock.Latest-clock.Time,NativeSpawnIn.GuestLead,1e-6,"GuestLead is the snapshot clock's delay");
            var host=Match(data,false);Check(host.Start(),"host starts");var guest=new NativeMatch(data);var killcam=new NativeMatch(data);var g=new NativeSnapshotClock();bool reset=true;
            var frames=new List<NativeMatchFrame>();var hostEnds=new List<(double time,double end)>();int compared=0,lagged=0;
            for(int tick=0;tick<60*30;tick++)
            {
                hostEnds.Add((host.Time,NativeSpawnIn.End(host.Phase,host.PhaseEnd,host.FightAt)));
                if(tick%3==0)
                {
                    var f=new NativeMatchFrame{phase=host.Phase,time=host.Time,phaseEnd=host.PhaseEnd,fightAt=host.FightAt,round=host.Round,set=host.Set};frames.Add(f);guest.ApplyNetwork(f);
                    if(reset){g.Reset(f.time);reset=false;}else g.Observe(f.time);
                }
                double render=g.Advance(Dt);
                // The host draws its world at the guest's render moment from its own state then: the same fight start, the same frame.
                double hostEnd=double.NaN,hostAt=double.NaN;foreach(var (time,end) in hostEnds)if(time<=render+1e-9){hostEnd=end;hostAt=time;}
                double guestEnd=NativeSpawnIn.End(guest.Phase,guest.PhaseEnd,guest.FightAt);
                float guestAge=NativeSpawnIn.Age(guest.Phase,guest.PhaseEnd,guest.FightAt,render),own=NativeSpawnIn.Age(guest.Phase,guest.PhaseEnd,guest.FightAt,render+NativeSpawnIn.GuestLead);
                if(!double.IsNaN(guestEnd)&&!double.IsNaN(hostEnd)&&Math.Abs(hostEnd-guestEnd)<1)
                {
                    compared++;Check(Math.Abs(hostEnd-guestEnd)<=Dt+1e-9,"guest and host agree on the fight start ("+hostEnd+" vs "+guestEnd+")");
                    var a=NativeSpawnIn.Evaluate((float)(render-hostEnd),SpawnInMode.Drop,.23f,.083f,9,false);var b=NativeSpawnIn.Evaluate(guestAge,SpawnInMode.Drop,.23f,.083f,9,false);
                    Check(a.Active==b.Active&&Math.Abs(a.Lift-b.Lift)<=.03f&&a.Built-b.Built<=Dt/NativeSpawnIn.BuildSeconds+1e-4f,"the guest draws the host's moment");
                    if(own>guestAge)lagged++;
                }
                if(host.Phase==NativePhase.Fight&&host.Time-host.FightAt>2){host.Seat(2).alive=false;host.Resolve();}
                host.Tick(Dt);
            }
            Check(compared>600&&lagged>0,"compared "+compared+" guest frames ("+lagged+" with the own pilot ahead)");
            // The killcam applies the recorded frames into a fresh match: each frame's fight start is the live one.
            int replayed=0;foreach(var f in frames){killcam.ApplyNetwork(f);double e=NativeSpawnIn.End(killcam.Phase,killcam.PhaseEnd,killcam.FightAt),l=NativeSpawnIn.End(f.phase,f.phaseEnd,f.fightAt);Check(double.IsNaN(e)==double.IsNaN(l)&&(double.IsNaN(e)||e==l),"the killcam's fight start");replayed++;}
            Check(replayed>100,"killcam frames replayed");
        }

        // ---- the drop is the authority's own fall ----
        private static void Fall(ConversionContent data,UnityMapContent unity)
        {
            // Re-derived from NativeGroundMotor.Probe: its sphere finds the floor when the capsule centre is within HalfHeight +
            // GroundReach of it; each fight tick adds a tick of gravity (the step zeroes nothing in the fight) before PhysX moves it.
            int Ticks(float centre){float y=centre,v=0;for(int n=0;n<240;n++){if(y-NativeLocomotionRules.HalfHeight<=NativeLocomotionRules.GroundReach+1e-6f)return n;v+=23f/60;y-=v/60;}return 240;}
            Near(NativeSpawnIn.Gravity,23,0,"the authority's gravity");
            int ticks=Ticks(1.1f);Check(ticks==4,"from a 1.1 m spawn the probe finds the floor on the 5th fight tick ("+ticks+")");
            Near(NativeSpawnIn.AuthorityFall(.23f),ticks/60.0,1e-5,"AuthorityFall is the authority's fall");Near(NativeSpawnIn.DropFor(.23f),(ticks+1)/60.0,1e-5,"the drop lands the tick after the authority's touchdown");
            foreach(float lift in new[]{.1f,.23f,.3f,.45f})Near(NativeSpawnIn.AuthorityFall(lift),Ticks(lift+NativeSpawnIn.RestCentre)/60.0,1e-5,"AuthorityFall from a "+lift+" m lift");
            // A grounded spawn (Astra's option 1): the beam lowers the pilot to the floor by GO, no drop, a soft touchdown.
            foreach(float centre in new[]{.87f,.9f,.92f})
            {
                float lift=NativeSpawnIn.SpawnLift(centre,0);Check(NativeSpawnIn.Mode(lift)==SpawnInMode.Lower,"a spawn "+lift+" m over its rest is lowered");
                var p=NativeSpawnIn.Place(Vector3.Zero,0,16,centre,1,Vector3.One);Check(p.Drop==0&&p.Hold==NativeSpawnIn.Hover&&NativeSpawnIn.LandAt(p)==0,"lowered: held at Hover, touchdown at GO");
                Check(NativeSpawnIn.Evaluate(0,p.Mode,p.Hold,p.Drop,1,false).Land==0,"its landing starts at GO");
            }
            Check(NativeSpawnIn.Mode(.07f)==SpawnInMode.Drop&&NativeSpawnIn.Mode(.05f)==SpawnInMode.Lower,"LiftMin splits drop and lower");
            var tall=NativeSpawnIn.Place(Vector3.Zero,0,16,3,1,Vector3.One);Check(tall.Hold==NativeSpawnIn.MaxHold,"a very high spawn's hold is capped (its real fall shows above it)");
            // The floor and ceiling the column spans.
            var low=NativeSpawnIn.Place(Vector3.Zero,0,1,1.1f,1,Vector3.One);Check(low.Top>=3,"the column always reaches above the pilot");
        }

        // ---- the body forms from the feet up ----
        private static void Build()
        {
            var poses=new List<(string name,CapturedRig.Pose pose)>();
            {var r=new MeleeRig();r.Reset();var p=new CapturedRig.Pose();r.WritePose(p);p.World();poses.Add(("rest",p));}
            foreach(var spec in new[]{"sword","greatsword","daggers","rifle","pistol","fire","bow"})
            {var r=new MeleeRig();r.Reset();NativeLobbyPoses.Sample(r,spec,NativeLobbyPoses.Weapon,1.3f,out _,out _);var p=new CapturedRig.Pose();r.WritePose(p);p.World();poses.Add((spec,p));}
            int[] chain={CapturedRig.LFoot,CapturedRig.LKnee,CapturedRig.LHip,CapturedRig.Pelvis,CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head};
            foreach(var (name,pose) in poses)
            {
                float H(int b){int c=NativeSpawnIn.PartChild(b);return NativeSpawnIn.PartHeight(b,pose.WorldPosition[b].Y,c>=0?pose.WorldPosition[c].Y:0)+NativeSpawnIn.FeetOffset;}
                float[] solidAt=new float[CapturedRig.Bones.Length],ghostAt=new float[CapturedRig.Bones.Length];
                for(int b=0;b<solidAt.Length;b++){solidAt[b]=ghostAt[b]=float.NaN;Check(H(b)>-.05f&&H(b)<2f,name+": part "+b+" within the build ("+H(b)+")");}
                for(float age=NativeSpawnIn.BuildAt;age<=NativeSpawnIn.BuiltAt+.01f;age+=1f/240)
                {
                    var f=NativeSpawnIn.Evaluate(age,SpawnInMode.Drop,.23f,.083f,1,false);
                    for(int b=0;b<solidAt.Length;b++)
                    {
                        float s=NativeSpawnIn.Solid(f.Front,H(b)),g=NativeSpawnIn.Ghost(f.Front,H(b));
                        if(float.IsNaN(solidAt[b])&&s>=.5f)solidAt[b]=age;if(float.IsNaN(ghostAt[b])&&g>.05f)ghostAt[b]=age;
                        Check(g<=1-s+1e-5f,"the hologram gives way to the solid part");
                    }
                }
                for(int b=0;b<solidAt.Length;b++){Check(!float.IsNaN(solidAt[b])&&solidAt[b]<=NativeSpawnIn.BuiltAt,name+": part "+b+" solid by the end of the build");Check(!float.IsNaN(ghostAt[b])&&ghostAt[b]<solidAt[b],name+": part "+b+"'s hologram leads it");}
                for(int i=1;i<chain.Length;i++)Check(solidAt[chain[i]]>=solidAt[chain[i-1]],name+": "+CapturedRig.Bones[chain[i]].Name+" forms after "+CapturedRig.Bones[chain[i-1]].Name);
                Check(solidAt[CapturedRig.LFoot]-NativeSpawnIn.BuildAt<.25f,name+": the feet first ("+(solidAt[CapturedRig.LFoot]-NativeSpawnIn.BuildAt)+" s)");
                Check(solidAt[CapturedRig.Head]-solidAt[CapturedRig.LFoot]>.6f,name+": a visible climb from the feet to the head");
                var whole=NativeSpawnIn.Evaluate(NativeSpawnIn.BuiltAt,SpawnInMode.Drop,.23f,.083f,1,false);for(int b=0;b<solidAt.Length;b++)Near(NativeSpawnIn.Solid(whole.Front,H(b)),1,1e-4,name+": part "+b+" whole at BuiltAt");
            }
            Check(Math.Abs(NativeSpawnIn.Heat(.5f+NativeSpawnIn.SolidLag+NativeSpawnIn.Band,.5f)-1)<1e-5f&&NativeSpawnIn.Heat(.5f,.5f)==1&&NativeSpawnIn.Heat(2.3f,.1f)==0,"the heat of a just-formed part, cooled once the front has passed");
        }

        // ---- tiers ----
        private static void Tiers()
        {
            var full=NativeSpawnIn.Tier(1,false);var red=NativeSpawnIn.Tier(.3f,false);var low=NativeSpawnIn.Tier(1,true);var both=NativeSpawnIn.Tier(.3f,true);
            Check(full.Motes>red.Motes&&full.Motes>low.Motes&&low.Motes>both.Motes&&full.Front>red.Front&&low.Front==0&&full.Dust>red.Dust&&low.Dust==0,"fewer motes, front sparks and dust down the tiers");
            Check(full.Ghost&&red.Ghost&&!low.Ghost&&full.Glitch&&!red.Glitch&&!low.Glitch&&full.Shimmer&&!red.Shimmer,"the hologram off on Low, the glitch and shimmer on Full only");
            Check(red.Gain<full.Gain&&low.Segments<full.Segments,"Reduced is calmer, Low rings coarser");
            float maxSplit=0,maxFlash=0,maxTint=0,maxBob=0;
            for(float age=NativeSpawnIn.StrikeAt;age<1;age+=1f/240){var f=NativeSpawnIn.Evaluate(age,SpawnInMode.Drop,.23f,.083f,5,true);maxSplit=Math.Max(maxSplit,f.Split);maxFlash=Math.Max(maxFlash,f.Flash);maxTint=Math.Max(maxTint,f.Tint);if(age>NativeSpawnIn.BuiltAt&&age<-NativeSpawnIn.Charge)maxBob=Math.Max(maxBob,Math.Abs(f.Lift-.23f));}
            Check(maxSplit==0&&maxFlash<=.35f+1e-5f&&maxTint<=.6f+1e-5f&&maxBob<=NativeSpawnIn.ReducedBob+1e-4f,"Reduced: no split, a soft flash and tint, a smaller bob");
            float fullSplit=0,fullBob=0;int blips=0;bool inBlip=false;
            for(float age=NativeSpawnIn.StrikeAt;age<1;age+=1f/240){var f=NativeSpawnIn.Evaluate(age,SpawnInMode.Drop,.23f,.083f,5,false);fullSplit=Math.Max(fullSplit,f.Split);if(age>NativeSpawnIn.BuiltAt&&age<-NativeSpawnIn.Charge)fullBob=Math.Max(fullBob,Math.Abs(f.Lift-.23f));bool b=age>NativeSpawnIn.BuiltAt+.2f&&f.Split>0;if(b&&!inBlip)blips++;inBlip=b;}
            Check(Math.Abs(fullSplit-NativeSpawnIn.SplitMax)<.005f&&blips==2&&fullBob>NativeSpawnIn.Bob*.8f&&fullBob<=NativeSpawnIn.Bob+1e-4f,"Full: the completion split, two hold blips, the bob ("+blips+" blips, "+fullBob+" m)");
            // Vertex counts per pilot by tier, at the busiest moments.
            var sink=new Sink(1<<16);int Peak(SpawnInTier t){int peak=0;foreach(float age in new[]{-2.85f,-2f,-1.9f,-.1f,.09f,.2f}){sink.Clear();var f=NativeSpawnIn.Evaluate(age,SpawnInMode.Drop,.23f,.083f,5,false);peak=Math.Max(peak,NativeSpawnIn.Draw(sink,f,Place(),new Vector3(0,.235f,0),new Vector3(0,3,-6),Vector3.UnitX,Vector3.UnitY,t,true));}return peak;}
            int pf=Peak(full),pr=Peak(red),pl=Peak(low);Check(pf>pr&&pr>=pl&&pf<=900,"vertices per pilot: Full "+pf+", Reduced "+pr+", Low "+pl);
        }
        private static SpawnInPlace Place()=>NativeSpawnIn.Place(new Vector3(4,0,-6),0,16,1.1f,NativeSpawnIn.Seed(2,30),new Vector3(.2f,.6f,1));
        private sealed class Sink:ISpawnInSink
        {
            public readonly Vector3[] P;public readonly Vector3[] Uv;public readonly Vector4[] C;public readonly int[] I;public int V,T;
            public Sink(int n){P=new Vector3[n];Uv=new Vector3[n];C=new Vector4[n];I=new int[n*3];}
            public int Vertex(Vector3 p,Vector3 uv,Vector4 c){P[V]=p;Uv[V]=uv;C[V]=c;return V++;}
            public void Triangle(int a,int b,int c){I[T++]=a;I[T++]=b;I[T++]=c;}
            public void Clear(){V=T=0;}
        }

        // ---- geometry ----
        private static void Geometry()
        {
            var p=Place();var full=NativeSpawnIn.Tier(1,false);var sink=new Sink(1<<16);var eye=new Vector3(0,3,-6);
            float lastBottom=2,lastTop=-1,lastRing=0;bool descend=true,retract=true,expand=true,floorOk=true,colOk=true,trisOk=true;
            for(float age=NativeSpawnIn.StrikeAt;age<.8f;age+=1f/120)
            {
                var f=NativeSpawnIn.Evaluate(age,p.Mode,p.Hold,p.Drop,p.Seed,false);sink.Clear();var feet=new Vector3(p.Centre.X,p.Floor+NativeSpawnIn.RestFeet+f.Lift,p.Centre.Z);
                NativeSpawnIn.Draw(sink,f,p,feet,eye,Vector3.UnitX,Vector3.UnitY,full,true);
                for(int i=0;i<sink.V;i++){var q=sink.P[i];colOk&=float.IsFinite(q.X)&&float.IsFinite(q.Y)&&float.IsFinite(q.Z)&&q.Y>=p.Floor-1f&&q.Y<=p.Top+1f&&(sink.Uv[i].Z==0||q.Y>=p.Floor-1e-3f)&&sink.C[i].W>=0&&sink.C[i].W<=1.4f&&Math.Abs(sink.Uv[i].X)<=1&&(sink.Uv[i].Z==0||sink.Uv[i].Z==1);}
                for(int i=0;i<sink.T;i++)trisOk&=sink.I[i]>=0&&sink.I[i]<sink.V;
                if(age<NativeSpawnIn.StrikeEnd){descend&=f.BeamFrom<=lastBottom+1e-6f;lastBottom=f.BeamFrom;}
                if(age>=0&&age<NativeSpawnIn.ReleaseSeconds){retract&=f.BeamFrom>=lastTop-1e-6f;lastTop=f.BeamFrom;}
                if(f.Land>=0&&f.Land<.4f){float r=Ring(sink,p);expand&=r>=lastRing-1e-4f;lastRing=r;}
                if(f.Ring>0)floorOk&=f.RingRadius>=NativeSpawnIn.RingRadiusCharge-1e-5f&&f.RingRadius<=NativeSpawnIn.RingRadiusHold+1e-5f;
            }
            Check(colOk&&trisOk,"every vertex finite, between the floor and the column's top, opacities and modes in range; indices valid");
            Check(descend&&NativeSpawnIn.Evaluate(NativeSpawnIn.StrikeEnd,p.Mode,p.Hold,p.Drop,p.Seed,false).BeamFrom<1e-4f,"the strike drives the beam's bottom down onto the floor");Check(retract,"GO retracts the beam upward");Check(expand,"the impact ring runs out");Check(floorOk,"the hold ring tightens in the charge");
            Check(NativeSpawnIn.Evaluate(-1,p.Mode,p.Hold,p.Drop,p.Seed,false).BeamFrom==0,"the held beam stands on the floor");
            // Nothing drawn when inactive; my own column skipped in first person (only the floor and the motes away from the eye).
            sink.Clear();Check(NativeSpawnIn.Draw(sink,NativeSpawnIn.Evaluate(5,p.Mode,p.Hold,p.Drop,p.Seed,false),p,Vector3.Zero,eye,Vector3.UnitX,Vector3.UnitY,full,true)==0&&sink.V==0,"inactive: nothing");
            var held=NativeSpawnIn.Evaluate(-1,p.Mode,p.Hold,p.Drop,p.Seed,false);var own=p.Centre+new Vector3(0,1.6f,0);sink.Clear();NativeSpawnIn.Draw(sink,held,p,p.Centre,own,Vector3.UnitX,Vector3.UnitY,full,false);
            bool clear=true;for(int i=0;i<sink.V;i++)clear&=Vector3.Distance(sink.P[i],own)>.5f||sink.P[i].Y<p.Floor+.05f;Check(clear&&sink.V>0,"first person: no column or mote at my own eye");
            // The landing's dust (a drop only) and sparks (both): on the floor, moving out and fading.
            int dust=0,sparks=0;for(float land=0;land<.8f;land+=.05f)for(int k=0;k<7;k++){if(NativeSpawnIn.Dust(k,7,p,land,out var at,out float size,out float op)){dust++;Check(at.Y>=p.Floor&&at.Y<p.Floor+.3f&&op<=.5f&&size>0,"dust on the floor");}if(NativeSpawnIn.Spark(k,7,p,land,out var head,out _,out float fade)){sparks++;Check(head.Y>=p.Floor&&fade<=1,"sparks off the floor");}}
            var lowered=NativeSpawnIn.Place(Vector3.Zero,0,16,.87f,1,Vector3.One);Check(dust>0&&sparks>0&&!NativeSpawnIn.Dust(0,7,lowered,.1f,out _,out _,out _),"dust and sparks from a drop; no dust from a lowered landing");
        }
        private static float Ring(Sink s,SpawnInPlace p){float r=0;for(int i=0;i<s.V;i++)if(s.P[i].Y<p.Floor+.05f&&s.Uv[i].Z==1&&s.Uv[i].X==1&&s.C[i].W>.05f)r=Math.Max(r,new Vector2(s.P[i].X-p.Centre.X,s.P[i].Z-p.Centre.Z).Length());return r;}

        // ---- the local camera and the first-person screen ----
        private static void Camera()
        {
            var p=Place();float peak=0,peakPitch=0;bool beforeOk=true,afterOk=true;
            for(float age=-3;age<1;age+=1f/240)
            {
                var f=NativeSpawnIn.Evaluate(age,p.Mode,p.Hold,p.Drop,p.Seed,false);float k=NativeSpawnIn.Kick(f,p.Mode,out float pitch);
                if(age<p.Drop)beforeOk&=k==0;if(age>p.Drop+.32f)afterOk&=k==0;peak=Math.Max(peak,k);peakPitch=Math.Max(peakPitch,pitch);
                var s=NativeSpawnIn.Screen(f,false);Check(s.Veil<=.32f+1e-5f&&s.Vignette<=.24f+1e-5f&&s.Flash<=.22f+1e-5f&&s.Split<=1+1e-5f&&s.Line>=0&&s.Line<=1,"a gentle first-person screen");
                var r=NativeSpawnIn.Screen(f,true);Check(r.Split==0&&r.Veil<=.2f+1e-5f&&r.Flash<=.1f+1e-5f,"Reduced: the softer screen");
            }
            Check(beforeOk&&afterOk&&peak>.04f&&peak<=.045f+1e-5f&&peakPitch<=.9f+1e-5f,"the kick: only at the touchdown, at most 4.5 cm and .9 deg, settled by .32 s");
            var lowered=NativeSpawnIn.Place(Vector3.Zero,0,16,.87f,1,Vector3.One);float soft=0;for(float age=0;age<.4f;age+=1f/240)soft=Math.Max(soft,NativeSpawnIn.Kick(NativeSpawnIn.Evaluate(age,lowered.Mode,lowered.Hold,lowered.Drop,1,false),lowered.Mode,out _));Check(soft<=.0151f&&soft>0,"a lowered pilot's touchdown kicks a third as much");
            var build=NativeSpawnIn.Screen(NativeSpawnIn.Evaluate(-2.1f,p.Mode,p.Hold,p.Drop,p.Seed,false),false);var hold=NativeSpawnIn.Screen(NativeSpawnIn.Evaluate(-.8f,p.Mode,p.Hold,p.Drop,p.Seed,false),false);
            Check(build.Line>0&&build.Line<1&&build.LineGlow>0&&build.Veil>.3f,"the scanline wipes up with the build under the veil");Check(hold.Veil<1e-4f&&hold.Vignette>.1f&&hold.LineGlow==0,"the hold: only the soft vignette");
            Check(!NativeSpawnIn.Screen(NativeSpawnIn.Evaluate(.5f,p.Mode,p.Hold,p.Drop,p.Seed,false),false).Any,"nothing on my screen once I have landed");
        }

        // ---- no allocation ----
        private static void Allocation()
        {
            var p=Place();var sink=new Sink(1<<16);var tiers=new[]{NativeSpawnIn.Tier(1,false),NativeSpawnIn.Tier(.3f,false),NativeSpawnIn.Tier(1,true)};var eye=new Vector3(1,3,-6);
            for(int i=0;i<200;i++){var f=NativeSpawnIn.Evaluate(-3+i*.02f,p.Mode,p.Hold,p.Drop,p.Seed,false);sink.Clear();NativeSpawnIn.Draw(sink,f,p,p.Centre,eye,Vector3.UnitX,Vector3.UnitY,tiers[i%3],true);}
            long before=GC.GetAllocatedBytesForCurrentThread();float sum=0;
            for(int i=0;i<12000;i++)
            {
                float t=-3.2f+(i%480)*Dt;float age=NativeSpawnIn.Age(i%5==0?NativePhase.Fight:NativePhase.Countdown,40,40,40+t);
                var f=NativeSpawnIn.Evaluate(age,i%7==0?SpawnInMode.Lower:SpawnInMode.Drop,p.Hold,p.Drop,p.Seed,i%3==1);sink.Clear();sum+=NativeSpawnIn.Draw(sink,f,p,p.Centre,eye,Vector3.UnitX,Vector3.UnitY,tiers[i%3],i%4!=0);
                for(int k=0;k<7;k++){if(NativeSpawnIn.Dust(k,7,p,f.Land,out var at,out float s,out float o))sum+=at.X+s+o;if(NativeSpawnIn.Spark(k,7,p,f.Land,out var h,out var v,out float fa))sum+=h.Y+v.X+fa;}
                for(int b=0;b<22;b++){int c=NativeSpawnIn.PartChild(b);float hh=NativeSpawnIn.PartHeight(b,.5f+b*.05f,c>=0?.4f:0);sum+=NativeSpawnIn.Solid(f.Front,hh)+NativeSpawnIn.Ghost(f.Front,hh)+NativeSpawnIn.Heat(f.Front,hh);}
                sum+=NativeSpawnIn.Kick(f,SpawnInMode.Drop,out float pitch)+pitch+NativeSpawnIn.Screen(f,i%2==0).Veil+(float)NativeSpawnIn.End(NativePhase.Countdown,3,0);
            }
            long bytes=GC.GetAllocatedBytesForCurrentThread()-before;Check(bytes==0&&sum!=0,"12000 frames of the clock, the frame, the geometry, dust, sparks, parts, kick and screen allocate nothing ("+bytes+" bytes)");
        }

        // ---- the Runtime wiring ----
        private static string Body(string text,string signature)
        {
            int i=text.IndexOf(signature,StringComparison.Ordinal);Check(i>=0,"declares "+signature);int open=text.IndexOf('{',i),depth=0,j=open;
            for(;j<text.Length;j++){if(text[j]=='{')depth++;else if(text[j]=='}'&&--depth==0)break;}return text.Substring(open,j-open+1);
        }
        private static void Wiring(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            string view=R("NativeSpawnInPresentation.cs"),arena=R("ArenaPresentation.cs"),source=R("NativeSourcePresentation.cs"),game=R("ConversionGame.cs"),status=R("NativeStatusPresentation.cs"),glow=R("NativeCombatGlow.cs"),hud=R("NativeCombatHud.cs"),fp=R("NativeViewPresentation.cs"),sim=R("NativeArena.cs");
            // Replicated state only: the match's phase, phase end and fight start; the lead only for a live guest's own pilot.
            var eval=Body(view,"private SpawnView SpawnEvaluate(");
            Check(eval.Contains("NativeSpawnIn.Age(m.Phase,m.PhaseEnd,m.FightAt,")&&eval.Contains("arena.Replica&&!arena.ReplicaScene.IsValid()&&id==arena.LocalId")&&eval.Contains("NativeSpawnIn.GuestLead"),"the clock is the replicated fight start; the lead only for a live guest's own pilot");
            Check(!Regex.IsMatch(view,@"\b(Body\.(linearVelocity|position\s*=)|Match\.Tick|ApplyNetwork|Send\w*\(|Physics\.Simulate)"),"the presentation never writes the simulation or the network");
            Check(Body(view,"private void SpawnPlaceView(").Contains("NativeSpawnIn.Seed(pilot.Seat.id,end)"),"the seed is the id and the replicated fight start");
            // Render: lift before storing the presented position; the contact flag overridden only around posing and restored.
            var render=Body(arena,"public void Render(Snapshot s,");
            Check(render.Contains("SpawnBegin();")&&render.IndexOf("p=SpawnPlace(a,p,renderTime);",StringComparison.Ordinal)<render.IndexOf("visualPositions[a.Id]=p;",StringComparison.Ordinal),"Render lifts the feet before storing them");
            // The pose block may also hold other posing steps (the BoZo visual body's restore and pose); the flag is set just before it and restored in its finally.
            Check(Regex.IsMatch(render,@"bool ground=a\.Grounded;a\.Grounded=SpawnContact\(a\.Id,ground\);try\{[^{}]*PrepareCapturedPose\(a,p,renderTime\);[^{}]*\}finally\{a\.Grounded=ground;\}SpawnBones\(a\);"),"the replicated flag is restored after posing");
            Check(arena.Contains("ClearSourceActors();ClearSpawnIn();")&&arena.Contains("DisposeSpawnIn();")&&Regex.IsMatch(arena,@"BloodFx\(arena,camera,fields\);SpawnInFx\(arena,camera\);"),"cleared with the presentation, disposed, drawn in RenderGlow");
            var character=Body(source,"private bool SourceCharacter(");
            foreach(var hook in new[]{"var spawn=SpawnFor(rig,a,first);spawnNow=spawn;reveal=SpawnReveal(spawn,reveal);","tint=SpawnTinted(spawn,tint);","&&SpawnShows(CapturedRig.RHand);","spawnNow=null;ClearRigTint();return true;"})
                Check(character.Contains(hook),"SourceCharacter: "+hook);
            // The voxel body's spawn-gated draws; a pilot drawn by the BoZo visual body (NativeVisualBodyPresentation.cs) skips them (!visual).
            foreach(var hook in new[]{@"if\(!first(&&!visual)?\)SpawnHologram\(rig,spawn,bodySkin,layer,camera\);",@"if\(dressed(&&!visual)?&&SpawnShows\(CapturedRig\.Chest\)\)BodyDressing\(",@"if\((!visual&&)?SpawnShows\(CapturedRig\.RHand\)\)CyberLordBody\("})
                Check(Regex.IsMatch(character,hook),"SourceCharacter: "+hook);
            Check(character.IndexOf("reveal=SpawnReveal(",StringComparison.Ordinal)<character.IndexOf("reveal=CyberReveal(",StringComparison.Ordinal)&&character.Contains("bool headShown=reveal==null||reveal(CapturedRig.Head)>.5f;"),"the hat waits for the head");
            Check(status.Contains("SpawnShowsFor(p.Seat.id,CapturedRig.Chest))Bands(")&&glow.Contains("Shown?.Invoke(pilot.Seat.id,bone)")&&glow.Contains("public Func<int,int,float> Shown;")&&arena.Contains("combatGlow.Shown=spawnJoint??=SpawnJointOf;"),"team bands and armour halos wait for their parts");
            Check(hud.Contains("||presentation.SpawnHidesHud(p.Seat.id))continue;"),"the overhead bar waits for the body");
            Check(fp.Contains("shown.Grounded=SpawnContact(shown.Id,shown.Grounded);"),"the first-person view copy is posed held too");
            Check(game.Contains("presentation.GunKitKick(")&&game.IndexOf("presentation.SpawnCamera(cameraView.transform,arena,renderTime,prefs.cameraShake,prefs.reducedEffects);",StringComparison.Ordinal)>game.IndexOf("presentation.GunKitKick(",StringComparison.Ordinal),"my camera after the other kicks");
            Check(game.IndexOf("if(!menu)SpawnInScreen();",StringComparison.Ordinal)>=0&&game.IndexOf("if(!menu)SpawnInScreen();",StringComparison.Ordinal)<game.IndexOf("FightHud(p);",StringComparison.Ordinal),"my screen under the HUD");
            Check(Body(view,"public void SpawnCamera(").Contains("||!shake)return;"),"no camera motion with camera shake off");
            // No allocation per frame: the per-frame methods construct only value types (a view once per pilot, the mesh lazily).
            foreach(var name in new[]{"private SpawnView SpawnEvaluate(","private Vector3 SpawnPlace(","private bool SpawnContact(","private void SpawnBones(","private Color SpawnTintOf(","private void SpawnHologram(","private Matrix4x4 SpawnBoneOf(","private void SpawnInFx(","public void SpawnCamera(","public bool SpawnScreenFor("})
            {
                var body=Body(view,name);foreach(Match m in Regex.Matches(body,@"\bnew\s+([A-Z]\w*)"))Check(new[]{"Vector3","Color","NVector","SpawnView","Rect"}.Contains(m.Groups[1].Value),name+" builds only values ("+m.Value+")");
                Check(!Regex.IsMatch(body,@"\.(Where|Select|Any|First|FirstOrDefault|ToArray|ToList|Find)\(|=>"),name+": no LINQ or lambdas");
            }
            Check(Body(view,"private void SpawnInScreen(").Contains("U.RadialStops(")&&view.Contains("private static readonly float[] SpawnVignetteAt="),"the screen's stops are static (RadialStops caches by them)");
            Check(view.Contains("Resources.Load<Shader>(\"NativeGlow\")")&&File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/NativeGlow.shader")),"the beam draws with NativeGlow");
            // The constants the drop is derived from are the authority's.
            Check(sim.Contains("Physics.gravity=new Vector3(0,-23,0)")&&sim.Contains("a.Position=Vec(p.Body.position-Vector3.up*.865f)"),"the authority's gravity and the presented feet offset");
            // Presentation only: the Core module and the hooks are referenced by presentation files alone.
            var names=new[]{"NativeSpawnIn","SpawnInFrame","SpawnPlace(","SpawnContact(","SpawnCamera(","SpawnHidesHud(","SpawnInScreen("};
            var allowed=new HashSet<string>{"Core/NativeSpawnIn.cs","Runtime/NativeSpawnInPresentation.cs","Runtime/ArenaPresentation.cs","Runtime/NativeSourcePresentation.cs","Runtime/NativeViewPresentation.cs","Runtime/NativeCombatHud.cs","Runtime/ConversionGame.cs"};
            foreach(var file in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena"),"*.cs",SearchOption.AllDirectories))
            {
                string rel=Path.GetRelativePath(Path.Combine(root,"Assets/ChipArena"),file).Replace('\\','/');string text=File.ReadAllText(file);
                if(names.Any(n=>text.Contains(n)))Check(allowed.Contains(rel),"only presentation files reference the spawn-in ("+rel+")");
            }
        }
    }
}
