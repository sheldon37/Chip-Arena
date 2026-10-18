#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ChipArena.UnityConversion
{
    // The last enemies of a kill quota, marked (Claude, 2026-10-18; Core/NativeQuotaMarks, Runtime/NativeQuotaMarksPresentation):
    // the trigger at 6 / 5 / 1 / 0 kills left, the boss phase, a boss arrival, leaving the area, loading and a host transfer, on
    // hand-built views and on the real split-area run for all three live instances (Ward Zero, Central Station, City Ironworks);
    // which adds (live hostile adds of the presented area only, the 8 nearest); the marker's height per kind (the Hulk too), its
    // size bounds, the close-range and crosshair fades, the pulse and the reduced tier; the edge arrows for off-screen and
    // behind-camera targets at 16:9 and 21:9, spread apart; the one-shot cue; the shapes and the ping; no allocation per frame;
    // and source scans (the Runtime hooks exist, the feature is read by presentation files only, no authority file touches it).
    internal static class QuotaMarkChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Quota marks: "+what);count++;}
        private static bool Near(float a,float b,float e=1e-4f)=>Math.Abs(a-b)<=e;

        public static void Run(string root)
        {
            count=0;var o=new JsonSerializerOptions{IncludeFields=true};
            var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
            var layout=NativeExpeditionLayout.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ExpeditionLayout.json")));
            Trigger();Live(content,layout);Selection();Heights();Marker();Arrows();Cue();Shapes();NoAllocation();Sources(root);
            Console.WriteLine("PASS quota marks ("+count+" checks: on at 5..1 kills left, off at 6 and 0, in the boss phase, after a boss arrival, outside the instance, loading or frozen; the same rule on Ward Zero, Central Station and City Ironworks' live quotas; live hostile adds of the presented area only, the 8 nearest; 0.6 m over each kind's head, the Hulk's 3.06 m; 15..28 px; hidden within 4 m in plain sight; edge arrows behind and off screen at 16:9 and 21:9, spread apart; lined-up markers stacked clear; one ping per quota; steady when reduced; 0 bytes per frame; presentation only)");
        }

        // ---- views ----
        private static ExpeditionView View(int progress,int target,bool current=true,ExpeditionPhase phase=ExpeditionPhase.Instance,string area="exp-ward-zero",string[] bosses=null,bool cleared=false)
            =>new ExpeditionView{run="run-1",loop=0,area=area,phase=phase,viewer="host",
                instances=new[]{new ExpeditionViewInstance{id="exp-ward-zero",progress=progress,target=target,entered=true,current=current,cleared=cleared,bosses=bosses??Array.Empty<string>()},
                    new ExpeditionViewInstance{id="exp-transit",progress=0,target=36}}};

        // 1. The trigger from hand-built views.
        private static void Trigger()
        {
            Check(NativeQuotaMarks.Threshold==5&&NativeQuotaMarks.Cap==8,"the threshold (5) and the cap (8) are single constants");
            var q6=NativeQuotaMarks.Of(View(30,36));Check(!q6.Active&&q6.Remaining==6,"6 kills left: off");
            var q5=NativeQuotaMarks.Of(View(31,36));Check(q5.Active&&q5.Remaining==5&&q5.Area=="exp-ward-zero"&&q5.Run=="run-1"&&q5.Loop==0,"5 kills left: on, with the quota's identity");
            var q1=NativeQuotaMarks.Of(View(35,36));Check(q1.Active&&q1.Remaining==1,"1 kill left: on");
            var q0=NativeQuotaMarks.Of(View(36,36));Check(!q0.Active&&q0.Remaining==0,"0 left (quota met): off");
            Check(!NativeQuotaMarks.Of(View(36,36,bosses:new[]{"m1-ward-siren"})).Active&&!NativeQuotaMarks.Of(View(33,36,bosses:new[]{"m1-ward-siren"})).Active,"the boss phase: off");
            var arrived=View(33,36);arrived.bossArrivals=new[]{new ExpeditionBossArrivalView{id="m1-ward-siren",area="exp-ward-zero"}};Check(!NativeQuotaMarks.Of(arrived).Active,"a boss arrival in the area: off");
            var elsewhere=View(33,36);elsewhere.bossArrivals=new[]{new ExpeditionBossArrivalView{id="x",area="exp-transit"}};Check(NativeQuotaMarks.Of(elsewhere).Active,"another area's boss arrival does not turn it off");
            Check(!NativeQuotaMarks.Of(View(33,36,current:false,phase:ExpeditionPhase.Town,area:"exp-city")).Active,"left for the hub (Town phase): off");
            Check(!NativeQuotaMarks.Of(View(33,36,current:false,area:"exp-transit")).Active,"in another instance (not this quota's area): off");
            Check(!NativeQuotaMarks.Of(View(33,36,cleared:true)).Active,"a cleared building: off");
            var loading=View(33,36);loading.loading=true;var frozen=View(33,36);frozen.migrating=true;Check(!NativeQuotaMarks.Of(loading).Active&&!NativeQuotaMarks.Of(frozen).Active,"loading or a host transfer: off");
            Check(!NativeQuotaMarks.Of(View(0,0)).Active&&!NativeQuotaMarks.Of(null).Active&&!NativeQuotaMarks.Of(new ExpeditionView()).Active,"no target, no view, an empty view: off");
            Check(!NativeQuotaMarks.Of(View(33,36,phase:ExpeditionPhase.FinalBoss)).Active,"the world boss: off");
            for(int left=0;left<=8;left++)Check(NativeQuotaMarks.Of(View(36-left,36)).Active==(left>=1&&left<=5),"on exactly for 1..5 left ("+left+")");
            Check(NativeQuotaMarks.Tag(5)=="LAST 5 · MARKED"&&NativeQuotaMarks.Tag(1)=="LAST 1 · MARKED"&&NativeQuotaMarks.Tag(0)==""&&NativeQuotaMarks.Tag(6)==""&&ReferenceEquals(NativeQuotaMarks.Tag(3),NativeQuotaMarks.Tag(3)),"the HUD line, cached");
        }

        // 2. The same rule on the real split-area run: each live instance's quota (Ward Zero, Central Station, City Ironworks).
        private static void Live(ConversionContent content,ExpeditionLayout layout)
        {
            Check(layout.instances.Length==3,"three live instances");
            var run=NativeExpedition.Create("quota-marks",layout.world,"host","pistol",7,layout.instances.Select(id=>new ExpeditionInstance{id=id,target=36}).ToArray());
            Check(run.Join("guest","pistol"),"a guest joins");run.EnableAreas();
            var sessions=layout.areas.ToDictionary(a=>a.id,a=>new ExpeditionSession(run.ForArea(a.id),content));
            ExpeditionView V(string area,string who)=>NativeExpeditionView.Build(sessions[area].Run,who,sessions[area].Sources);
            foreach(var id in layout.instances)
            {
                Check(run.MoveMembers(layout.world,id,new[]{"host"}),id+": the host enters");
                var area=sessions[id].Run;int kill=0;
                bool At(int left){while(area.Snapshot().instances.First(i=>i.id==id).progress<36-left)Check(area.Progress("m1-"+id+"-"+(kill++),1),id+": a kill counts");return NativeQuotaMarks.Of(V(id,"host")).Active;}
                Check(!At(6),id+": 6 left, off");Check(At(5),id+": 5 left, on");var q=NativeQuotaMarks.Of(V(id,"host"));Check(q.Area==id&&q.Remaining==5&&q.Target==36,id+": the live view's quota fields");
                Check(At(1),id+": 1 left, on");Check(!NativeQuotaMarks.Of(V(layout.world,"guest")).Active,id+": the guest in the hub sees none");
                Check(!At(0),id+": met, off");Check(area.StartBosses(new[]{"m1-ward-siren"}),id+": the boss starts");Check(!NativeQuotaMarks.Of(V(id,"host")).Active,id+": the boss phase, off");
                Check(run.MoveMembers(id,layout.world,new[]{"host"}),id+": back to the hub");Check(!NativeQuotaMarks.Of(V(layout.world,"host")).Active,id+": left the area, off");
            }
            // An unfinished quota: leaving turns it off, coming back turns it on again (the cue does not repeat: Cue()).
            var run2=NativeExpedition.Create("quota-marks-2",layout.world,"host","pistol",9,layout.instances.Select(id=>new ExpeditionInstance{id=id,target=36}).ToArray());run2.EnableAreas();
            var s2=layout.areas.ToDictionary(a=>a.id,a=>new ExpeditionSession(run2.ForArea(a.id),content));string ward=layout.instances[0];
            Check(run2.MoveMembers(layout.world,ward,new[]{"host"}),"re-entry fixture");for(int k=0;k<33;k++)s2[ward].Run.Progress("m1-x-"+k,1);
            Check(NativeQuotaMarks.Of(NativeExpeditionView.Build(s2[ward].Run,"host",s2[ward].Sources)).Active,"3 left: on");
            Check(run2.MoveMembers(ward,layout.world,new[]{"host"})&&!NativeQuotaMarks.Of(NativeExpeditionView.Build(s2[layout.world].Run,"host",s2[layout.world].Sources)).Active,"left with 3 to go: off");
            Check(run2.MoveMembers(layout.world,ward,new[]{"host"})&&NativeQuotaMarks.Of(NativeExpeditionView.Build(s2[ward].Run,"host",s2[ward].Sources)).Active,"back in: on again");
        }

        // 3. Which adds: live hostile adds of the presented area, the 8 nearest, nearest first (ties by id).
        private static QuotaCandidate Add(int id,float x,float z,int area=0,int kind=0)=>new QuotaCandidate{Id=id,Kind=kind,Root=new Vector3(x,.85f,z),Area=area,Alive=true,Hostile=true,Spawned=true};
        private static void Selection()
        {
            var list=new List<QuotaCandidate>{Add(1,0,5)};
            var dead=Add(2,0,1);dead.Alive=false;var ally=Add(3,0,1);ally.Hostile=false;var hub=Add(4,0,1);hub.Hub=true;var boss=Add(5,0,1);boss.Boss=true;var queued=Add(6,0,1);queued.Spawned=false;var other=Add(7,0,1,area:1);
            list.AddRange(new[]{dead,ally,hub,boss,queued,other});
            var dist=new[]{36f,3,30,9,12,27,6,21,18,15,24,33};for(int i=0;i<dist.Length;i++)list.Add(Add(10+i,0,dist[i]));
            var set=new QuotaMarkSet();var arr=list.ToArray();set.Select(arr,arr.Length,0,Vector3.Zero);
            Check(set.Eligible==13,"13 eligible: corpses, allies, hub zombies, the boss, queued adds and another area's adds excluded ("+set.Eligible+")");
            Check(set.Count==8,"capped at 8");
            var want=new[]{11,1,16,13,14,19,18,17};Check(Enumerable.Range(0,8).Select(i=>set.Marks[i].Id).SequenceEqual(want),"the 8 nearest, nearest first: "+string.Join(",",Enumerable.Range(0,8).Select(i=>set.Marks[i].Id)));
            Check(Enumerable.Range(0,8).All(i=>i==0||set.Marks[i].Distance>=set.Marks[i-1].Distance),"sorted by distance");
            foreach(var excluded in new[]{2,3,4,5,6,7})Check(Enumerable.Range(0,set.Count).All(i=>set.Marks[i].Id!=excluded),"never marked: "+excluded);
            set.Select(arr,arr.Length,1,Vector3.Zero);Check(set.Count==1&&set.Marks[0].Id==7,"the area filter: only the presented area's adds");
            var tie=new[]{Add(40,3,0),Add(30,-3,0),Add(35,0,3)};set.Select(tie,3,0,Vector3.Zero);Check(set.Marks[0].Id==30&&set.Marks[1].Id==35&&set.Marks[2].Id==40,"equal distances: by id (stable)");
            set.Select(arr,0,0,Vector3.Zero);Check(set.Count==0,"no candidates, no marks");set.Select(null,4,0,Vector3.Zero);Check(set.Count==0,"null list");
            Check(Near(set.Fade(true,.1f),1/3f,1e-3f)&&Near(set.Fade(true,.25f),1)&&set.Fade(false,.1f)==0&&set.Level==0,"fades in over .3 s, out at once");
        }

        // 4. The marker over each kind's head.
        private static void Heights()
        {
            for(int k=0;k<NativeHordeKinds.All;k++)
            {
                var root=new Vector3(2,NativeHordeKinds.RootHeight(k,false),-1);var a=NativeQuotaMarks.Anchor(root,k,false);float floor=0;
                float top=NativeHordeKinds.Axis[k]==2?NativeHordeKinds.RootHeight(k,false)+NativeHordeKinds.WorldRadius(k):NativeHordeKinds.WorldHeight(k);
                Check(Near(a.Y-floor,top+.6f,1e-4f)&&a.X==2&&a.Z==-1,NativeHordeKinds.Ids[k]+": 0.6 m over its head ("+(a.Y-floor)+" m)");
                var c=NativeQuotaMarks.Anchor(new Vector3(0,NativeHordeKinds.CrawlHeight*.5f,0),k,true);Check(Near(c.Y,NativeHordeKinds.CrawlHeight+.6f),NativeHordeKinds.Ids[k]+": crawling, over its crawl capsule");
            }
            var hulk=NativeQuotaMarks.Anchor(new Vector3(0,NativeHordeKinds.RootHeight(NativeHordeKinds.Hulk,false),0),NativeHordeKinds.Hulk,false);
            Check(Near(hulk.Y,3.06f+.6f,1e-3f),"the Hulk: over its 3.06 m head ("+hulk.Y+")");
            var shambler=NativeQuotaMarks.Anchor(new Vector3(0,.85f,0),NativeHordeKinds.Shambler,false);Check(Near(shambler.Y,2.3f,1e-4f),"a shambler: 2.3 m (1.7 + .6)");
            Check(Near(NativeQuotaMarks.Anchor(new Vector3(0,.85f,0),-3,false).Y,2.3f)&&Near(NativeQuotaMarks.Anchor(new Vector3(0,.85f,0),999,false).Y,2.3f),"an unknown kind: as the shambler");
            int ladle=NativeHordeKinds.Resolve("ladleman"),pile=NativeHordeKinds.Resolve("pileup");
            Check(ladle>=NativeHordeKinds.Count&&pile>=NativeHordeKinds.Count,"zone kinds resolve");
            foreach(int z in new[]{ladle,pile}){var a=NativeQuotaMarks.Anchor(new Vector3(0,NativeHordeKinds.RootHeight(z,false),0),z,false);Check(Near(a.Y,NativeHordeKinds.WorldHeight(z)+.6f,1e-4f),NativeHordeKinds.Ids[z]+": over its own capsule");}
        }

        // 5. The marker: size bounds, the close-range fade, the crosshair, the pulse, the reduced tier.
        private static void Marker()
        {
            float last=float.MaxValue;
            for(float d=0;d<=240;d+=.25f){float s=NativeQuotaMarks.Size(d);Check(s>=NativeQuotaMarks.SizeFar-1e-4f&&s<=NativeQuotaMarks.SizeNear+1e-4f&&s<=last+1e-5f,"size within 15..28 px and never growing with distance ("+d+" m: "+s+")");last=s;}
            Check(NativeQuotaMarks.Size(2)==28&&NativeQuotaMarks.Size(6)==28&&NativeQuotaMarks.Size(48)==15&&NativeQuotaMarks.Size(400)==15&&NativeQuotaMarks.Size(float.NaN)==28,"28 px up close, 15 px far (never huge up close, readable far)");
            Check(NativeQuotaMarks.Size(20)<26&&NativeQuotaMarks.Size(20)>17,"mid range in between");
            // Close fights: hidden within 4 m in plain sight, back by 6 m; behind a wall always shown.
            Check(NativeQuotaMarks.CloseAlpha(3.9f,true)==0&&NativeQuotaMarks.CloseAlpha(1,true)==0,"in plain sight within 4 m: hidden");
            Check(NativeQuotaMarks.CloseAlpha(5,true)>0&&NativeQuotaMarks.CloseAlpha(5,true)<1&&NativeQuotaMarks.CloseAlpha(6,true)==1&&NativeQuotaMarks.CloseAlpha(30,true)==1,"fades back in 4..6 m");
            Check(NativeQuotaMarks.CloseAlpha(1,false)==1&&NativeQuotaMarks.CloseAlpha(3,false)==1,"behind a wall: shown at any range");
            var close=NativeQuotaMarks.Marker(.7f,.6f,3,16/9f,3,true,0,false,1);Check(close.Alpha==0&&!close.Label,"a close visible add's marker draws nothing");
            var wall=NativeQuotaMarks.Marker(.7f,.6f,3,16/9f,3,false,0,false,1);Check(wall.Alpha>.5f&&wall.Label,"a close add behind a wall keeps its marker");
            // The crosshair: faint over the centre, full away from it.
            Check(Near(NativeQuotaMarks.AimAlpha(.5f,.5f,16/9f),NativeQuotaMarks.AimFaint)&&NativeQuotaMarks.AimAlpha(.7f,.5f,16/9f)==1&&NativeQuotaMarks.AimAlpha(.5f,.54f,16/9f)>NativeQuotaMarks.AimFaint,"faint over the crosshair, full clear of it");
            // The pulse: 1.2 s, gentle; none with Reduced effects.
            float a0=NativeQuotaMarks.Marker(.7f,.6f,10,16/9f,20,false,0,false,1).Alpha,a6=NativeQuotaMarks.Marker(.7f,.6f,10,16/9f,20,false,.6,false,1).Alpha,a12=NativeQuotaMarks.Marker(.7f,.6f,10,16/9f,20,false,1.2,false,1).Alpha;
            Check(a6>a0&&Near(a0,a12,1e-4f)&&a0>=NativeQuotaMarks.PulseLow-1e-4f&&a6<=1+1e-4f,"a 1.2 s pulse ("+a0+" .. "+a6+")");
            float s0=NativeQuotaMarks.Marker(.7f,.6f,10,16/9f,20,false,0,false,1).Size,s6=NativeQuotaMarks.Marker(.7f,.6f,10,16/9f,20,false,.6,false,1).Size;Check(s6>s0&&s6<=s0*(1+NativeQuotaMarks.PulseGrow)+1e-3f,"the pulse grows it gently (at most 12%)");
            for(double t=0;t<3;t+=.05)
            {
                var r=NativeQuotaMarks.Marker(.7f,.6f,10,16/9f,20,false,t,true,1);var r0=NativeQuotaMarks.Marker(.7f,.6f,10,16/9f,20,false,0,true,1);
                Check(r.Alpha==r0.Alpha&&r.Size==r0.Size&&r.Pulse==0&&NativeQuotaMarks.Pulse(t,true)==0,"reduced effects: steady, no pulse (t "+t.ToString("0.00")+")");
                var ar=NativeQuotaMarks.Arrow(1.4f,.5f,10,16/9f,20,t,true,1,0);Check(ar.Alpha==NativeQuotaMarks.SteadyAlpha,"reduced effects: steady arrows");
            }
            Check(NativeQuotaMarks.Marker(.7f,.6f,10,16/9f,20,false,0,false,.5f).Alpha<NativeQuotaMarks.Marker(.7f,.6f,10,16/9f,20,false,0,false,1).Alpha,"the set's fade level scales it");
            // Lined up behind each other: the farther marker is lifted clear of the nearer one (its column kept); apart ones stay.
            {
                const float W=1600,H=900;var ms=new[]{new QuotaMarker{OnScreen=true,X=.6f,Y=.55f,Size=26,Alpha=1},new QuotaMarker{OnScreen=true,X=.605f,Y=.552f,Size=18,Alpha=1},
                    new QuotaMarker{OnScreen=true,X=.6f,Y=.553f,Size=16,Alpha=1},new QuotaMarker{OnScreen=true,X=.2f,Y=.55f,Size=20,Alpha=1},new QuotaMarker{OnScreen=false,X=.6f,Y=.55f,Size=20,Alpha=1}};
                var before=(QuotaMarker[])ms.Clone();NativeQuotaMarks.Declutter(ms,ms.Length,W,H);
                (float l,float r,float t,float b) Box(in QuotaMarker m){float x=m.X*W,bot=(1-m.Y)*H+m.Size*.5f,w=Math.Max(m.Size,NativeQuotaMarks.FootprintWidth);return (x-w/2,x+w/2,bot-m.Size-NativeQuotaMarks.LabelRise,bot);}
                bool Overlap(in QuotaMarker a,in QuotaMarker b){var p=Box(a);var q=Box(b);return p.l<q.r&&q.l<p.r&&p.t<q.b&&q.t<p.b;}
                Check(ms[0].X==before[0].X&&ms[0].Y==before[0].Y&&!ms[0].Lifted,"the nearest marker stays put");
                Check(ms[1].Lifted&&ms[2].Lifted&&ms[1].Y>before[1].Y&&ms[2].Y>ms[1].Y&&ms[1].X==before[1].X,"farther ones lined up behind are lifted straight up, stacked");
                Check(!Overlap(ms[0],ms[1])&&!Overlap(ms[0],ms[2])&&!Overlap(ms[1],ms[2]),"no two kites or labels overlap after declutter");
                Check(!ms[3].Lifted&&ms[3].Y==before[3].Y&&!ms[4].Lifted&&ms[4].Y==before[4].Y,"apart and off-screen markers are left alone");
            }
            Check(NativeQuotaMarks.Metres(23.4f)=="23 m"&&NativeQuotaMarks.Metres(23.5f)=="24 m"&&NativeQuotaMarks.Metres(-2)=="0 m"&&NativeQuotaMarks.Metres(5000)=="999 m"&&NativeQuotaMarks.Metres(float.NaN)=="0 m"&&ReferenceEquals(NativeQuotaMarks.Metres(7),NativeQuotaMarks.Metres(7.2f)),"distance labels in whole metres, cached");
        }

        // 6. The edge arrows at 16:9 and 21:9: behind the camera, off each side and above, inside the inset rectangle, spread apart.
        private static void Arrows()
        {
            foreach(var (w,h) in new[]{(1920,1080),(3440,1440),(2560,1080)})
            {
                var ui=NativeUiMotion.Measure(w,h);float W=ui.Width,H=ui.Height,aspect=W/H;string at=w+"x"+h;
                var eye=new Vector3(0,1.6f,0);var fwd=new Vector3(0,0,1);var up=Vector3.UnitY;const float fov=65;
                QuotaArrow Of(Vector3 p,out bool on){var v=NativeQuotaMarks.Viewport(eye,fwd,up,fov,aspect,p);on=NativeQuotaMarks.OnScreen(v.X,v.Y,v.Z);return NativeQuotaMarks.Arrow(v.X,v.Y,v.Z,aspect,Vector3.Distance(eye,p),0,false,1,0);}
                void Inside(in QuotaArrow a,string what)
                {
                    float x=a.X*W,y=(1-a.Y)*H;float side=NativeQuotaMarks.InsetSide*H,top=NativeQuotaMarks.InsetTop*H,bottom=NativeQuotaMarks.InsetBottom*H;
                    Check(x>=side-.5f&&x<=W-side+.5f&&y>=top-.5f&&y<=H-bottom+.5f,at+" "+what+": inside the inset edge ("+x.ToString("0")+","+y.ToString("0")+")");
                    Check(y>=54+22+20,at+" "+what+": clear of the objective and LAST tags");
                }
                var ahead=Of(new Vector3(1,1.6f,12),out bool aheadOn);Check(aheadOn,at+": a target ahead is on screen (its kite, no arrow)");
                var right=Of(new Vector3(14,1.6f,4),out bool rOn);Check(!rOn&&!right.Behind&&Math.Abs(right.Angle)<.5f&&right.X*W>W-NativeQuotaMarks.InsetSide*H-6,at+": off to the right: on the right edge, pointing right");Inside(right,"right");
                var left=Of(new Vector3(-14,1.6f,4),out bool lOn);Check(!lOn&&Math.Abs(Math.Abs(left.Angle)-Math.PI)<.5f&&left.X*W<NativeQuotaMarks.InsetSide*H+6,at+": off to the left: on the left edge, pointing left");Inside(left,"left");
                var above=Of(new Vector3(0,40,6),out bool aOn);Check(!aOn&&above.Angle>1&&above.Angle<2.2f&&(1-above.Y)*H<=NativeQuotaMarks.InsetTop*H+.5f,at+": above (a balcony): on the top edge under the tags, pointing up");Inside(above,"above");
                var behindR=Of(new Vector3(3,1.6f,-9),out bool bOn);Check(!bOn&&behindR.Behind&&behindR.Angle<0&&behindR.Angle>-Math.PI/2&&behindR.X>.5f&&behindR.Y<.5f,at+": behind, to the right: lower right, pointing down (turn round) ("+behindR.Angle+")");Inside(behindR,"behind right");
                var behindL=Of(new Vector3(-3,1.6f,-9),out _);Check(behindL.Behind&&behindL.Angle<-Math.PI/2&&behindL.X<.5f&&behindL.Y<.5f,at+": behind, to the left: lower left");Inside(behindL,"behind left");
                var behind=Of(new Vector3(0,1.6f,-9),out _);Check(behind.Behind&&Near(behind.Angle,-(float)Math.PI/2,.05f)&&Math.Abs(behind.X-.5f)<.02f&&Near(behind.Y,NativeQuotaMarks.InsetBottom,1e-3f),at+": straight behind: bottom centre, pointing down");Inside(behind,"behind");
                var behindAbove=Of(new Vector3(0,20,-6),out _);Check(behindAbove.Behind&&behindAbove.Y<.5f,at+": behind even when above: points down (turn round)");
                // The arrows hug the wider screen's edges at 21:9 (the rounded rectangle stretches with the aspect).
                if(aspect>2)Check(right.X*W>W*.9f,at+": the right arrow sits at the wide screen's edge");
                // Five adds behind to the right at nearly the same bearing: spread apart, none on another.
                var set=new QuotaArrowSet();for(int i=0;i<5;i++){var p=new Vector3(3+i*.05f,1.6f,-9-i*.02f);var v=NativeQuotaMarks.Viewport(eye,fwd,up,fov,aspect,p);set.Add(NativeQuotaMarks.Arrow(v.X,v.Y,v.Z,aspect,9,0,false,1,i));}
                float mean=Enumerable.Range(0,5).Average(i=>set.Arrows[i].Angle);set.Spread(aspect);
                var angles=Enumerable.Range(0,5).Select(i=>set.Arrows[i].Angle).OrderBy(x=>x).ToArray();
                Check(Enumerable.Range(1,4).All(i=>angles[i]-angles[i-1]>=NativeQuotaMarks.MinGap-1e-3f),at+": a cluster is spread apart ("+string.Join(",",angles.Select(x=>x.ToString("0.000")))+")");
                Check(Math.Abs(angles.Average()-mean)<.02f,at+": the cluster keeps its mean bearing");
                for(int i=0;i<5;i++){var a=set.Arrows[i];float x,y;NativeQuotaMarks.Place(a.Angle,aspect,out x,out y);Check(Near(a.X,x)&&Near(a.Y,y),at+": spread arrows are placed on the edge");Inside(a,"spread "+i);}
                // Wrap-around: two arrows either side of the left (+/- pi) are spread across it.
                var wrap=new QuotaArrowSet();wrap.Add(new QuotaArrow{Angle=(float)Math.PI-.02f});wrap.Add(new QuotaArrow{Angle=-(float)Math.PI+.02f});wrap.Spread(aspect);
                float gap=(float)(Math.PI*2-Math.Abs(wrap.Arrows[0].Angle-wrap.Arrows[1].Angle));Check(gap>=NativeQuotaMarks.MinGap-1e-3f,at+": spread across the wrap ("+gap+")");
            }
            var many=new QuotaArrowSet();for(int i=0;i<8;i++)many.Add(new QuotaArrow{Angle=i*.785f});many.Spread(16/9f);Check(many.Count==8&&Enumerable.Range(0,8).All(i=>Near(many.Arrows[i].Angle,i*.785f>Math.PI?i*.785f-(float)(Math.PI*2):i*.785f,1e-3f)),"well-spread arrows stay where they are");
            var full=new QuotaArrowSet();for(int i=0;i<12;i++)full.Add(new QuotaArrow());Check(full.Count==8,"at most 8 arrows");
        }

        // 7. The cue: once per quota, never on a count change or a return to the same quota.
        private static void Cue()
        {
            NativeQuotaMarks.Quota Q(int left,string area="exp-ward-zero",int loop=0,bool on=true)=>new NativeQuotaMarks.Quota{Active=on&&left>=1&&left<=5,Remaining=left,Run="run-1",Loop=loop,Area=area};
            var cue=new QuotaMarkCue();
            Check(!cue.Step(Q(8))&&!cue.Step(Q(6)),"no ping before the markers turn on");
            Check(cue.Step(Q(5)),"one ping when they first turn on");
            Check(!cue.Step(Q(4))&&!cue.Step(Q(3))&&!cue.Step(Q(1)),"no ping on a count change");
            Check(!cue.Step(Q(0))&&!cue.Step(Q(3,on:false))&&!cue.Step(Q(3)),"none on leaving and coming back to the same quota");
            Check(cue.Step(Q(5,"exp-transit")),"a ping for another instance's quota");Check(cue.Step(Q(4,loop:1)),"a ping for the next loop's quota");
            // Strings rebuilt each frame (a guest's decoded view) still compare by value.
            Check(!cue.Step(new NativeQuotaMarks.Quota{Active=true,Remaining=2,Run=new string("run-1".ToCharArray()),Loop=0,Area=new string("exp-transit".ToCharArray())}),"the same quota from a fresh view: no ping");
            cue.Reset();Check(cue.Step(Q(5)),"reset: pings again");
        }

        // 8. The shapes (a downward kite, a right-pointing dart) and the built-in ping.
        private static void Shapes()
        {
            const int px=48;float[] Cover(string svg)=>NativeIconRaster.Coverage(NativeIconRaster.Parse(svg,1,NativeIconRaster.Mode.Markup),px,24);
            float At(float[] c,int x,int yTop)=>c[(px-1-yTop)*px+x];   // coverage rows are bottom first
            var body=Cover(NativeQuotaMarks.MarkerBody);
            int Width(float[] c,int y){int n=0;for(int x=0;x<px;x++)if(At(c,x,y)>.5f)n++;return n;}
            int widest=Enumerable.Range(0,px).OrderByDescending(y=>Width(body,y)).First();
            Check(widest<px/2,"the kite is widest in its upper half ("+widest+")");Check(Width(body,0)==0&&Width(body,px-1)==0,"clear top and bottom rows");
            Check(Width(body,(int)(px*.8f))>0&&Width(body,(int)(px*.8f))<Width(body,widest)/2,"it narrows to a tip pointing down (at the head)");
            for(int y=0;y<px;y++)for(int x=0;x<px/2;x++)Check(Math.Abs(At(body,x,y)-At(body,px-1-x,y))<.05f,"symmetric");
            var core=Cover(NativeQuotaMarks.MarkerCore);var edge=Cover(NativeQuotaMarks.MarkerEdge);Check(core.Sum()>0&&core.Sum()<body.Sum()*.5f&&edge.Sum()>0,"a core inside the body, an edge round it");
            var dart=Cover(NativeQuotaMarks.ArrowDart);
            Check(At(dart,(int)(px*.85f),px/2)>.5f&&At(dart,(int)(px*.85f),4)<.1f,"the dart points right: solid at its tip on the axis");
            var ping=NativeQuotaMarks.PingSamples();float peak=ping.Max(Math.Abs);
            Check(ping.Length==(int)(NativeQuotaMarks.PingRate*1.1)&&peak>.2f&&peak<.6f&&Math.Abs(ping[0])<.01f&&Math.Abs(ping[ping.Length-1])<1e-3f,"the built-in ping: 1.1 s, soft (peak "+peak.ToString("0.00")+"), click-free ends");
            Check(ping.Take(NativeQuotaMarks.PingRate/10).Max(Math.Abs)>ping.Skip(ping.Length-NativeQuotaMarks.PingRate/10).Max(Math.Abs)*20,"it decays (one ping, not a tone)");
            Check(NativeQuotaMarks.CueKey=="expedition.quota.mark","the cue's bank key");
        }

        // 9. No allocation per frame once warm: the trigger, selection, markers, arrows, spread, cue and labels.
        private static void NoAllocation()
        {
            var views=new[]{View(31,36),View(33,36),View(35,36),View(30,36)};var cands=new QuotaCandidate[40];
            for(int i=0;i<cands.Length;i++){cands[i]=Add(100+i,(float)Math.Sin(i)*20,(float)Math.Cos(i*1.3)*25,i%7==0?1:0,i%NativeHordeKinds.All);cands[i].Crawl=i%5==0;}
            var set=new QuotaMarkSet();var arrows=new QuotaArrowSet();var cue=new QuotaMarkCue();var markers=new QuotaMarker[NativeQuotaMarks.Cap];var eye=new Vector3(0,1.6f,0);float sink=0;NativeQuotaMarks.WarmLabels();
            void Frame(int f)
            {
                var v=views[(f/60)%views.Length];var q=NativeQuotaMarks.Of(v);if(cue.Step(q))sink+=1;float level=set.Fade(q.Active,1/60f);
                double t=f/60.0;var fwd=new Vector3((float)Math.Sin(t*.7),0,(float)Math.Cos(t*.7));
                set.Select(cands,cands.Length,0,eye);arrows.Clear();
                for(int i=0;i<set.Count;i++)
                {
                    var m=set.Marks[i];var vp=NativeQuotaMarks.Viewport(eye,fwd,Vector3.UnitY,65,2150/900f,m.Anchor);
                    var mk=NativeQuotaMarks.Marker(vp.X,vp.Y,vp.Z,2150/900f,m.Distance,i%2==0,t,f%3==0,level);markers[i]=mk;sink+=mk.Alpha;
                    if(!mk.OnScreen)arrows.Add(NativeQuotaMarks.Arrow(vp.X,vp.Y,vp.Z,2150/900f,m.Distance,t,f%3==0,level,i));
                    sink+=NativeQuotaMarks.Metres(m.Distance).Length;
                }
                arrows.Spread(2150/900f);NativeQuotaMarks.Declutter(markers,set.Count,2150,900);sink+=NativeQuotaMarks.Tag(q.Remaining).Length+arrows.Count;
            }
            for(int f=0;f<600;f++)Frame(f);
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int f=600;f<1800;f++)Frame(f);
            long bytes=GC.GetAllocatedBytesForCurrentThread()-before;Check(bytes==0,"no allocation per frame ("+bytes+" bytes over 1200 frames)");Check(sink>0,"the frames ran");
        }

        // 10. Sources: the Runtime hooks exist; the feature is presentation only and no authority file reads it.
        private static void Sources(string root)
        {
            string A=Path.Combine(root,"Assets/ChipArena");string R(string f)=>File.ReadAllText(Path.Combine(A,f));
            string core=R("Core/NativeQuotaMarks.cs"),pres=R("Runtime/NativeQuotaMarksPresentation.cs"),audio=R("Runtime/NativeQuotaMarkAudio.cs"),live=R("Runtime/NativeExpeditionLiveUI.cs"),hud=R("Runtime/NativeExpeditionHud.cs"),na=R("Runtime/NativeAudio.cs");
            int labels=live.IndexOf("ExpeditionWorldLabels();",StringComparison.Ordinal),hook=live.IndexOf("QuotaMarksHud(v,width,height);",StringComparison.Ordinal),minimal=live.IndexOf("E.Minimal(v,width,1,false);",StringComparison.Ordinal);
            Check(labels>0&&hook>labels&&minimal>hook,"the in-scene hook: after the world labels, under the minimal HUD");
            Check(Regex.Matches(live,@"QuotaMarksHud\(").Count==1,"one in-scene hook");
            Check(hud.Contains("H.Bar(quota,b.progress,b.target,20,Tone(row.tone,a),C.Void(.75f*a));y+=20;\n            y=NativeQuotaMarksHud.Building(r,y,v,a);"),"the J screen hook: under the KILL QUOTA bar");
            Check(na.Contains("ClearHoverClips();ClearQuotaMark();"),"the ping's clip is released with the audio");
            Check(pres.Contains("private void QuotaMarksHud(ExpeditionView v,float width,float height)")&&pres.Contains("presentation.HordeDrawnRoot(m.Id,out var drawn)")&&pres.Contains("Q.Of(v)")&&pres.Contains("quotaMarks.Select(")&&pres.Contains("Q.Marker(")&&pres.Contains("Q.Arrow(")&&pres.Contains("quotaArrows.Spread(")&&pres.Contains("Q.Declutter(quotaMarkers,quotaMarks.Count,width,height);")&&pres.Contains("quotaCue.Step(cue)"),"the Runtime draws the Core rules");
            Check(pres.Contains("ExpeditionWorldShown")&&pres.Contains("Event.current.type!=EventType.Repaint"),"nothing in menus, intros or replays; repaint only");
            Check(pres.Contains("prefs.reducedEffects")&&pres.Contains("!presentation.LowGraphics"),"the reduced and low graphics tiers");
            Check(pres.Contains("C.Danger(a)")&&!Regex.IsMatch(pres,@"0x[0-9a-fA-F]{6}")&&!pres.Contains("C.Warn(")&&!pres.Contains("C.Cyan("),"the hunt colour is the HUD's enemy tone (not the hazards' amber or the allies' cyan); palette only");
            Check(pres.Contains("physics.Raycast(eye,")&&pres.Contains("NativeArena.WorldMask"),"plain sight is a ray from the camera through the world");
            Check(audio.Contains("BankPlay(NativeQuotaMarks.CueKey,")&&audio.Contains("NativeQuotaMarks.PingSamples()")&&audio.Contains("BankVoice("),"the cue: the bank's key, else the built-in ping on an existing voice");
            Check(!core.Contains("using UnityEngine")&&!core.Contains("NativeMinion")&&!core.Contains("NativeArena"),"the rules are engine-free (Core)");
            // Presentation only: the new files change nothing in the world, the run or the network.
            foreach(var (name,src) in new[]{("core",core),("presentation",pres),("audio",audio)})
            {
                foreach(var bad in new[]{"Random","DateTime","Emit(","SpawnPveAdd","RemoveMinion","MonsterDefeated","Progress(","StartBosses","Send(","Expedition.Session","NetworkFrame","AddForce","SetActive("})Check(!src.Contains(bad),name+": no "+bad);
                Check(!Regex.IsMatch(src,@"(Body|transform)\.(position|rotation)\s*=[^=]")&&!Regex.IsMatch(src,@"\bv\.\w+\s*=[^=]"),name+": writes no body or view state");
                if(name!="core")Check(!Regex.IsMatch(src,@"\bm\.(Hp|MaxHp|Yaw|Speed|LastAttack|StrikeAt|Pve|Body)\s*=[^=]")&&!Regex.IsMatch(src,@"\ba\.(Crawl|Hub|HubRing|SpawnAt|Stats|Target|BossSpawn)\s*=[^=]"),name+": writes no add state");
            }
            // The feature is read by its own files and the hooks only; no authority file (rules, AI, spawning, director, network) names it.
            var allowed=new HashSet<string>{"Core/NativeQuotaMarks.cs","Runtime/NativeQuotaMarksPresentation.cs","Runtime/NativeQuotaMarkAudio.cs","Runtime/NativeExpeditionLiveUI.cs","Runtime/NativeExpeditionHud.cs","Runtime/NativeAudio.cs"};
            var names=new Regex(@"\b(NativeQuotaMarks|QuotaMarkSet|QuotaArrowSet|QuotaMarkCue|QuotaCandidate|QuotaMarksHud|NativeQuotaMarksHud|HordeDrawnRoot|ClearQuotaMark|QuotaMark)\b");
            foreach(var file in Directory.GetFiles(A,"*.cs",SearchOption.AllDirectories))
            {
                string rel=Path.GetRelativePath(A,file).Replace('\\','/');if(!names.IsMatch(File.ReadAllText(file)))continue;
                Check(allowed.Contains(rel),"only presentation files read the quota marks ("+rel+")");
            }
            foreach(var f in new[]{"Runtime/NativeExpeditionM1Arena.cs","Runtime/NativePveHorde.cs","Runtime/NativeNetworkFrame.cs","Runtime/NativeExpeditionNetwork.cs","Runtime/NativeExpeditionSplitRuntime.cs","Runtime/NativeZoneBossAuthority.cs","Runtime/NativeExpeditionHubAuthority.cs","Core/NativeExpeditionDirector.cs","Core/NativeExpedition.cs","Core/NativeZoneBossRules.cs","Core/NativeZoneCombat.cs","Core/NativeExpeditionView.cs"})
                Check(File.Exists(Path.Combine(A,f))&&!names.IsMatch(R(f)),"authority untouched by the markers: "+f);
            foreach(var f in new[]{"Core/NativeQuotaMarks.cs","Runtime/NativeQuotaMarksPresentation.cs","Runtime/NativeQuotaMarkAudio.cs"})Check(File.Exists(Path.Combine(A,f+".meta")),"Unity meta for "+f);
        }
    }
}
#endif
