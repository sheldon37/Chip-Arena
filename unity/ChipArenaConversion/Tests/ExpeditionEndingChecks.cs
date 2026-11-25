#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ChipArena.UnityConversion
{
    // The Expedition quota module and end sequence (Claude, 2026-10-20; Core/NativeExpeditionQuotaModel, Core/NativeExpeditionEnding,
    // Runtime/NativeExpeditionQuotaHud, NativeExpeditionEndingHud, NativeExpeditionEndingUI; docs/EXPEDITION-ENDING-UI.md):
    //  1. the quota rules on hand-built views and on the real split-area run for Ward Zero, Central Station and City Ironworks;
    //  2. the quota model's states: kill flashes and the count's pop, the complete beat only when watched (once, with its cue), the
    //     collapse, the LAST row in step with the markers, re-entry, a host transfer, a new loop, Reduced effects, the party pips;
    //  3. the module's layout at 1280x720 @1.5, 1920x1080, 3440x1440 and more: inside the top band, clear of the wallet, the J tag, the
    //     crosshair and the combat numbers;
    //  4. the ending's outcome on every fixture and through the real authority (victory, extraction, the run ended, a wipe, a solo
    //     failure, a dead pilot whose team stands, a reconnect), host and guest, a late joiner and a host change;
    //  5. the sequence and its count-ups (start at zero, monotonic, exact at the end, the stagger), skip, Reduced effects;
    //  6. the actions per outcome and client (host, guest, offline, a lobby return running, the host lost), their focus;
    //  7. degradation when optional data is missing (and after the guests' JSON decode);
    //  8. the summary's layout at the same resolutions, with and without the account XP column, 1 to 8 pilots;
    //  9. zero allocation per frame for both models;
    // 10. source scans: replicated fields only, presentation only, the hooks, the cursor path, the DEV preview, the metas.
    internal static class ExpeditionEndingChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Expedition ending: "+what);count++;}
        private static bool Near(float a,float b,float e=1e-3f)=>Math.Abs(a-b)<=e;
        private static readonly (int w,int h,float s,string name)[] Screens={(1280,720,1.5f,"1280x720 @1.5"),(1920,1080,1,"1920x1080"),(3440,1440,1,"3440x1440"),(2560,1080,1,"2560x1080"),(3840,2160,1,"3840x2160"),(1280,720,1,"1280x720"),(1600,900,1,"1600x900"),(1920,1080,1.5f,"1920x1080 @1.5"),(1280,720,2,"1280x720 @2")};

        public static void Run(string root)
        {
            count=0;var o=new JsonSerializerOptions{IncludeFields=true};
            var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
            var layout=NativeExpeditionLayout.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ExpeditionLayout.json")));
            QuotaRules();QuotaLive(content,layout);QuotaModel();QuotaLayout();
            Outcomes();Authority();Sequence();Actions();Missing(o);EndingLayout();NoAllocation();Sources(root);
            Console.WriteLine("PASS Expedition quota & ending ("+count+" checks: the quota stages from the replicated instance (running, closing at 10, the last 5 with the markers, met, boss, cleared) on all three live zones; kill flashes, the pop, one beat and one cue only when watched, the collapse; the module inside the top band clear of the wallet, J tag, crosshair and combat numbers at 720p @1.5, 1080p, 3440x1440 and 6 more; the ending's outcome on every fixture and the real authority (victory, extracted, run ended, wiped, failed, standing team, reconnect, guest, late joiner, host change); count-ups from 0 to the exact value, skip and Reduced effects; the pause menu's actions per client; graceful degradation; the summary and XP column laid out without overlap; 0 bytes per frame; replicated fields only)");
        }

        // ---- views ----------------------------------------------------------------------------------------------------------------------
        private static ExpeditionView View(int progress,int target=36,string area="exp-ward-zero",string[] bosses=null,bool cleared=false,ExpeditionPhase phase=ExpeditionPhase.Instance,int loop=0,string run="run-1")
        {
            var v=new ExpeditionView{run=run,loop=loop,area=area,world="exp-city",phase=phase,viewer="host",host="host",areaIdentity=new ExpeditionCohortTicket{run=run,area=area},
                instances=new[]{new ExpeditionViewInstance{id="exp-ward-zero",progress=progress,target=target,entered=true,current=area=="exp-ward-zero"&&phase==ExpeditionPhase.Instance,cleared=cleared,bosses=bosses??Array.Empty<string>()},
                    new ExpeditionViewInstance{id="exp-transit",target=36}},
                members=new[]{new ExpeditionViewMember{account="host",name="Host",alive=true,connected=true,area=area,host=true},new ExpeditionViewMember{account="guest",name="Guest",alive=false,connected=true,area=area},new ExpeditionViewMember{account="hub",name="Hub",alive=true,connected=true,area="exp-city"}}};
            v.mine=new ExpeditionViewPersonal{account="host",alive=true,connected=true};return v;
        }

        // 1. The quota rules.
        private static void QuotaRules()
        {
            Check(NativeExpeditionQuota.LastAt==NativeQuotaMarks.Threshold&&NativeExpeditionQuota.LastAt==5&&NativeExpeditionQuota.CloseAt==10,"LAST at the markers' threshold (5), closing in from 10");
            var S=new Func<ExpeditionView,ExpeditionQuotaStage>(v=>NativeExpeditionQuota.Of(v).Stage);
            Check(S(View(12))==ExpeditionQuotaStage.Running&&S(View(25))==ExpeditionQuotaStage.Running,"running above 10 left");
            for(int left=36;left>=0;left--)
            {
                var v=View(36-left);var st=S(v);var want=left==0?ExpeditionQuotaStage.Complete:left<=5?ExpeditionQuotaStage.Last:left<=10?ExpeditionQuotaStage.Closing:ExpeditionQuotaStage.Running;
                Check(st==want,left+" left: "+want+" ("+st+")");
                Check((st==ExpeditionQuotaStage.Last)==NativeQuotaMarks.Of(v).Active,left+" left: the LAST stage turns on exactly with the markers");
                float heat=NativeExpeditionQuota.Heat(left,36);Check(left>10||left==0?heat==0:left<=5?heat==1:heat>=.35f&&heat<1,left+" left: the emphasis ("+heat+")");
                if(left<36)Check(NativeExpeditionQuota.Heat(left,36)>=NativeExpeditionQuota.Heat(left+1,36)||left==0,"the emphasis never falls as kills land ("+left+")");
            }
            Check(S(View(36,bosses:new[]{"m1-ward-siren"}))==ExpeditionQuotaStage.Boss&&S(View(33,bosses:new[]{"m1-ward-siren"}))==ExpeditionQuotaStage.Boss,"the boss phase (bosses[])");
            var arrived=View(36);arrived.bossArrivals=new[]{new ExpeditionBossArrivalView{id="b",area="exp-ward-zero"}};Check(S(arrived)==ExpeditionQuotaStage.Boss,"a boss arrival in the area");
            var other=View(20);other.bossArrivals=new[]{new ExpeditionBossArrivalView{id="b",area="exp-transit"}};Check(S(other)==ExpeditionQuotaStage.Running,"another area's arrival changes nothing");
            Check(S(View(36,bosses:new[]{"x"},cleared:true))==ExpeditionQuotaStage.Cleared,"cleared");
            var loading=View(20);loading.loading=true;var frozen=View(20);frozen.migrating=true;Check(S(loading)==ExpeditionQuotaStage.None&&S(frozen)==ExpeditionQuotaStage.None,"loading and a host transfer: none (the objective tag says so)");
            Check(S(View(20,phase:ExpeditionPhase.Town,area:"exp-city"))==ExpeditionQuotaStage.None&&S(View(20,phase:ExpeditionPhase.FinalBoss,area:"exp-city"))==ExpeditionQuotaStage.None&&S(null)==ExpeditionQuotaStage.None&&S(new ExpeditionView())==ExpeditionQuotaStage.None,"the hub, the world boss, no view: none");
            var elsewhere=View(20);elsewhere.area="exp-transit";Check(S(elsewhere)==ExpeditionQuotaStage.None,"the viewer in another area: none");
            Check(S(View(0,target:0))==ExpeditionQuotaStage.None,"no target: none");
            var q=NativeExpeditionQuota.Of(View(20));Check(q.Party==2&&q.Alive==1&&q.Progress==20&&q.Target==36&&q.Remaining==16,"the party in this zone: the hub pilot is not counted; the down guest is a hollow pip");
            var split=View(20);split.areaIdentity=null;Check(NativeExpeditionQuota.Of(split).Party==3,"a run without split areas counts every pilot in the run");
            var over=View(40);Check(NativeExpeditionQuota.Of(over).Progress==36&&NativeExpeditionQuota.Of(over).Remaining==0,"progress past the target is held at it");
            Check(NativeExpeditionQuota.Cells(36,out _)==36&&NativeExpeditionQuota.Cells(48,out _)==48&&NativeExpeditionQuota.Cells(120,out float step)<=48&&step>1,"one cell per kill up to 48; a larger target groups kills");
            for(int p=0;p<=120;p++){int c=NativeExpeditionQuota.CellOf(p,120);Check(c>=0&&c<=NativeExpeditionQuota.Cells(120,out _)&&(p==0||c>=NativeExpeditionQuota.CellOf(p-1,120)),"grouped cells fill monotonically ("+p+")");}
            Check(NativeExpeditionQuota.CellOf(120,120)==NativeExpeditionQuota.Cells(120,out _)&&NativeExpeditionQuota.CellOf(36,36)==36&&NativeExpeditionQuota.CellOf(0,36)==0,"empty at 0, full at the target");
            foreach(ExpeditionQuotaStage st in Enum.GetValues(typeof(ExpeditionQuotaStage)))Check(Enum.IsDefined(typeof(ExpeditionTone),NativeExpeditionQuota.Tone(st)),"a tone for "+st);
            Check(NativeExpeditionQuota.Tone(ExpeditionQuotaStage.Last)==ExpeditionTone.Danger&&NativeExpeditionQuota.Tone(ExpeditionQuotaStage.Closing)==ExpeditionTone.Warn&&NativeExpeditionQuota.Tone(ExpeditionQuotaStage.Running)==ExpeditionTone.Cyan,"calm cyan, closing amber, the last few in the markers' hunt tone");
            Check(ReferenceEquals(NativeExpeditionQuota.Left(5),NativeExpeditionQuota.Left(5))&&NativeExpeditionQuota.Left(5)=="5 LEFT"&&NativeExpeditionQuota.Of(36)=="/ 36","the words, cached");
        }

        // 1b. The real split-area run: the same rule on all three live zones.
        private static void QuotaLive(ConversionContent content,ExpeditionLayout layout)
        {
            Check(layout.instances.Length==3,"three live instances");
            var run=NativeExpedition.Create("quota-ui",layout.world,"host","pistol",7,layout.instances.Select(id=>new ExpeditionInstance{id=id,target=36}).ToArray());
            Check(run.Join("guest","pistol"),"a guest joins");run.EnableAreas();
            var sessions=layout.areas.ToDictionary(a=>a.id,a=>new ExpeditionSession(run.ForArea(a.id),content));
            ExpeditionView V(string area,string who)=>NativeExpeditionView.Build(sessions[area].Run,who,sessions[area].Sources);
            foreach(var id in layout.instances)
            {
                Check(run.MoveMembers(layout.world,id,new[]{"host"}),id+": the host enters");var area=sessions[id].Run;int kill=0;var m=new ExpeditionQuotaModel();float t=10;bool cued=false;
                ExpeditionQuotaStage At(int left){while(area.Snapshot().instances.First(i=>i.id==id).progress<36-left){Check(area.Progress("q-"+id+"-"+(kill++),1),id+": a kill counts");t+=.5f;m.Step(V(id,"host"),t,false,1600);cued|=m.CueNow;}return NativeExpeditionQuota.Of(V(id,"host")).Stage;}
                m.Step(V(id,"host"),t,false,1600);Check(m.Shown&&m.Stage==ExpeditionQuotaStage.Running&&m.AreaText==NativeExpeditionJoinZones.Of(id).Name,id+": the module on entry, the zone's name");
                Check(At(11)==ExpeditionQuotaStage.Running&&At(10)==ExpeditionQuotaStage.Closing&&At(6)==ExpeditionQuotaStage.Closing&&At(5)==ExpeditionQuotaStage.Last&&At(1)==ExpeditionQuotaStage.Last,id+": running, closing at 10, last at 5");
                Check(!cued,id+": no beat before the quota is met");
                Check(At(0)==ExpeditionQuotaStage.Complete&&m.Beat&&cued,id+": met: the beat and its cue");
                Check(area.StartBosses(new[]{"m1-ward-siren"}),id+": the boss starts");t+=.1f;m.Step(V(id,"host"),t,false,1600);Check(m.Stage==ExpeditionQuotaStage.Boss&&m.Beat&&!m.CueNow,id+": the boss phase keeps the beat, no second cue");
                Check(NativeExpeditionQuota.Of(V(layout.world,"guest")).Stage==ExpeditionQuotaStage.None,id+": the guest in the hub sees no module");
                Check(run.MoveMembers(id,layout.world,new[]{"host"}),id+": back to the hub");m.Step(V(layout.world,"host"),t+.1f,false,1600);Check(!m.Shown,id+": left the zone: no module");
            }
        }

        // 2. The model's states.
        private static void QuotaModel()
        {
            var m=new ExpeditionQuotaModel();float t=100;
            m.Step(View(20),t,false,1600);Check(m.Shown&&m.Stage==ExpeditionQuotaStage.Running&&m.KillAge>=NativeExpeditionQuota.Never/2&&!m.Beat&&m.Tall==1&&m.Pop==0,"first sight: no flash, no beat, tall");
            Check(m.CountText=="20"&&m.TargetText=="/ 36"&&m.LeftText=="16 LEFT"&&m.EyebrowText=="KILL QUOTA"&&m.AreaText=="WARD ZERO"&&m.Cells==36&&Near(m.Fill,20/36f),"the words and the bar");
            m.Step(View(22),t+.05f,false,1600);Check(m.KillAge==0&&m.FlashFrom==20&&m.FlashTo==22&&Near(m.Pop,NativeExpeditionQuota.PopScale),"two kills in one update: their two cells flash, the count pops");
            m.Step(View(22),t+.05f+NativeExpeditionQuota.CountPop/2,false,1600);Check(m.Pop>0&&m.Pop<NativeExpeditionQuota.PopScale,"the pop eases out");
            m.Step(View(22),t+.05f+NativeExpeditionQuota.CountPop+.01f,false,1600);Check(m.Pop==0&&m.KillAge>NativeExpeditionQuota.CountPop,"the pop is over in "+NativeExpeditionQuota.CountPop+" s");
            float p0=-1;for(float k=0;k<=NativeExpeditionQuota.CountPop;k+=.01f){var q=new ExpeditionQuotaModel();q.Step(View(10),0,false,1600);q.Step(View(11),1,false,1600);q.Step(View(11),1+k,false,1600);Check(p0<0||q.Pop<=p0+1e-6f,"the pop never grows back ("+k+")");p0=q.Pop;}
            // Reduced effects: no pop, heights at once.
            var r=new ExpeditionQuotaModel();r.Step(View(20),0,true,1600);r.Step(View(21),1,true,1600);Check(r.Pop==0&&r.KillAge==0,"reduced: the kill is still known, no pop");
            r.Step(View(31),2,true,1600);Check(r.Stage==ExpeditionQuotaStage.Last&&r.LastIn==1,"reduced: the LAST row at once");
            // The LAST row eases in with the stage.
            var l=new ExpeditionQuotaModel();l.Step(View(30),0,false,1600);Check(l.LastIn==0&&l.Stage==ExpeditionQuotaStage.Closing,"6 left: no LAST row");
            l.Step(View(31),1/60f,false,1600);float last1=l.LastIn;Frames(l,View(31),1/60f,1/60f+NativeExpeditionQuota.LastReveal+.05f);Check(last1>0&&last1<.1f&&l.LastIn==1&&l.LastText==NativeQuotaMarks.Tag(5),"5 left: the LAST row eases in over "+NativeExpeditionQuota.LastReveal+" s with the markers' own words");
            // The beat: once, only when watched.
            var b=new ExpeditionQuotaModel();int cues=0;b.Step(View(35),0,false,1600);
            for(float k=0;k<=4;k+=1/60f){var v=k<.2f?View(35):k<1?View(36):View(36,bosses:new[]{"m1-ward-siren"});b.Step(v,1+k,false,1600);if(b.CueNow)cues++;
                if(k>=.2f&&k<.2f+NativeExpeditionQuota.BeatEnd-.02f)Check(b.Beat&&b.Tall==1&&b.Tone==ExpeditionTone.Good,"the beat holds the tall module in green ("+k+")");}
            Check(cues==1,"one cue per quota ("+cues+")");Check(!b.Beat&&b.Stage==ExpeditionQuotaStage.Boss&&b.Tall==0&&Near(b.H,NativeExpeditionQuota.CompactH),"after the beat: the compact boss strip");
            Check(b.CompactText=="WARD ZERO · BOSS · 0 / 1 DOWN"&&b.HintText==NativeExpeditionQuota.BossHint,"the boss strip's words and the beat's hint");
            var h=new ExpeditionQuotaModel();h.Step(View(35),0,false,1600);h.Step(View(36),1,false,1600);Frames(h,View(36),1,1+NativeExpeditionQuota.BeatHintAt-.02f);Check(h.HintIn==0,"no hint before "+NativeExpeditionQuota.BeatHintAt+" s");
            Frames(h,View(36),1+NativeExpeditionQuota.BeatHintAt,1+NativeExpeditionQuota.BeatHintAt+NativeExpeditionQuota.LastReveal+.05f);Check(h.HintIn==1&&h.Beat&&Near(h.H,NativeExpeditionQuota.TallH+NativeExpeditionQuota.HintRow),"the hint row by "+NativeExpeditionQuota.BeatHintAt+" s, under the body");
            var first=new ExpeditionQuotaModel();first.Step(View(36),0,false,1600);first.Step(View(36,bosses:new[]{"x"}),1,false,1600);Check(!first.Beat&&!first.CueNow&&first.Stage==ExpeditionQuotaStage.Boss&&first.Tall<1,"first sight of a met quota (a late joiner, a reconnect): no beat");
            var join=new ExpeditionQuotaModel();join.Step(View(36,bosses:new[]{"x"}),0,false,1600);Check(!join.Beat&&join.Tall==0,"first sight in the boss phase: the compact strip at once");
            // Re-entry and a host transfer: the quota is seen again as it is.
            var e=new ExpeditionQuotaModel();e.Step(View(33),0,false,1600);e.Step(View(33,phase:ExpeditionPhase.Town,area:"exp-city"),1,false,1600);Check(!e.Shown,"left with 3 to go: no module");
            e.Step(View(36),2,false,1600);Check(!e.Beat&&!e.CueNow,"re-entering after the quota was met elsewhere: no beat");
            var f=new ExpeditionQuotaModel();f.Step(View(33),0,false,1600);var fz=View(36);fz.migrating=true;f.Step(fz,1,false,1600);Check(!f.Shown,"frozen for a host transfer: hidden");f.Step(View(36),2,false,1600);Check(!f.Beat,"after the transfer: no beat for what happened while frozen");
            var g=new ExpeditionQuotaModel();g.Step(View(30),0,false,1600);g.Step(View(4,loop:1),1,false,1600);Check(g.KillAge>=NativeExpeditionQuota.Never/2&&g.LoopText=="LOOP 2"&&g.EyebrowText=="KILL QUOTA · LOOP 2","a new loop: a fresh quota, no flash, the loop named");
            var c=new ExpeditionQuotaModel();c.Step(View(35),0,false,1600);c.Step(View(36,bosses:new[]{"x"},cleared:true),1,false,1600);Check(c.Beat&&c.CueNow&&c.Stage==ExpeditionQuotaStage.Cleared&&c.HintText==NativeExpeditionQuota.ExitHint,"met and cleared in one update: the beat, then the exit hint");
            Frames(c,View(36,bosses:new[]{"x"},cleared:true),1,1+NativeExpeditionQuota.BeatEnd+NativeExpeditionQuota.Collapse+.1f);Check(c.CompactText=="WARD ZERO CLEARED · VOTE TO LEAVE"&&c.Tone==ExpeditionTone.Good&&c.Tall==0,"the cleared strip");
            var n=new ExpeditionQuotaModel();n.Step(View(36,bosses:new[]{"x"}),0,false,1600);n.Clear();Check(!n.Shown,"Clear hides it");n.Step(null,1,false,1600);Check(!n.Shown,"no view: hidden");
            // The beat's timeline constants hold together.
            Check(NativeExpeditionQuota.BeatFlash<NativeExpeditionQuota.BeatTitle&&NativeExpeditionQuota.BeatTitle<NativeExpeditionQuota.BeatHintAt&&NativeExpeditionQuota.BeatHintAt<NativeExpeditionQuota.BeatEnd&&NativeExpeditionQuota.BeatEnd<3,"flash, title, hint, then the end within 3 s");
        }

        private static void Frames(ExpeditionQuotaModel m,ExpeditionView v,float from,float to){for(float t=from;t<=to+1e-4f;t+=1/60f)m.Step(v,t,false,1600);}

        // 3. The module's layout.
        private static void QuotaLayout()
        {
            foreach(var (w,h,s,name) in Screens)
            {
                var l=NativeUiMotion.Measure(w,h,s);float W=l.Width,H=l.Height;var m=new ExpeditionQuotaModel();
                foreach(var v in new[]{View(12),View(31),View(36,bosses:new[]{"x"}),View(36,bosses:new[]{"x"},cleared:true)})
                {
                    m.Clear();m.Step(v,0,true,W);float x0=m.X,x1=m.X+m.W,y1=NativeExpeditionQuota.Top+NativeExpeditionQuota.MaxHeight;
                    Check(m.Shown&&x0>=0&&x1<=W&&m.Y>=0&&m.Y+m.H<=y1+.01f,name+": inside the canvas ("+m.Stage+")");
                    Check(x0>=24+220+8||W<1066,name+": clear of the wallet panel ("+x0+")");Check(x1<=W-24-150-8,name+": clear of the J tag ("+x1+" of "+W+")");
                    Check(y1<NativeExpeditionQuota.CombatTop(H)&&y1<H/2-60,name+": above the combat numbers' band and the crosshair ("+y1+" < "+NativeExpeditionQuota.CombatTop(H)+")");
                    Check(m.W>=NativeExpeditionQuota.MinWidth-.01f&&m.W<=NativeExpeditionQuota.Width+.01f,name+": its width");
                    Check(Math.Abs((m.X+m.W/2)-W/2)<.01f,name+": centred");
                }
                // The words fit the module (Liberation Mono's .6 em advance plus tracking, the measure the painter and the HUD agree on).
                float Mono(string t,float size,float tr)=>t.Length*size*(.6f+tr);
                float mw=NativeExpeditionQuota.WidthOn(W);string longest=new[]{NativeExpeditionJoinZones.WardZero,NativeExpeditionJoinZones.Station,NativeExpeditionJoinZones.Ironworks}.Select(z=>NativeExpeditionJoinZones.Of(z).Name).OrderByDescending(n=>n.Length).First();
                Check(14+Mono(longest,10,.2f)+8+8*11<=mw-12,name+": the longest zone name and eight pips always fit (the eyebrow and PARTY give way first)");
                if(mw>=NativeExpeditionQuota.Width)Check(14+Mono(longest,10,.2f)+12+Mono("KILL QUOTA",10,.16f)+8<=mw-12-4*11-Mono("PARTY",9,.16f)-8,name+": a full-width module shows the eyebrow and PARTY beside four pips");
                Check(Mono("000",26,0)+4+Mono("/ 000",13,0)+10+60+12+Mono("00 LEFT",11,.08f)<mw-26,name+": the count, a bar of at least 60 and the kills left fit");
                foreach(var z in new[]{NativeExpeditionJoinZones.WardZero,NativeExpeditionJoinZones.Station,NativeExpeditionJoinZones.Ironworks})
                {
                    var cm=new ExpeditionQuotaModel();var cv=View(36,area:z,bosses:new[]{"x"},cleared:true);cv.instances[0].id=z;cv.instances[0].current=true;cm.Step(cv,0,true,W);
                    Check(cm.Shown&&Mono(cm.CompactText,10,.08f)<=mw-28,name+": the compact strip's longest words fit at its smallest size ("+cm.CompactText+")");
                }
                Check(Mono(NativeQuotaMarks.Tag(5),11,.12f)+32<mw,name+": the LAST row fits");
            }
        }

        // 4. The outcome on every fixture.
        private static ExpeditionEndingModel Ended(NativeExpeditionEndingFixtures.End e,float age,bool reduced=false,ExpeditionEndContext ctx=default)
        {
            var m=new ExpeditionEndingModel();if(!NativeExpeditionEndingFixtures.FirstSight(e))m.Step(NativeExpeditionEndingFixtures.QuotaView(NativeExpeditionEndingFixtures.Quota.Running),0,reduced,ctx);
            var v=NativeExpeditionEndingFixtures.EndView(e);for(float t=1;t<=1+age+1e-4f;t+=1/30f)m.Step(v,t,reduced,ctx);return m;
        }
        private static void Outcomes()
        {
            var F=typeof(NativeExpeditionEndingFixtures.End);
            var want=new Dictionary<NativeExpeditionEndingFixtures.End,ExpeditionEndKind>{{NativeExpeditionEndingFixtures.End.Victory,ExpeditionEndKind.Victory},{NativeExpeditionEndingFixtures.End.VictoryDown,ExpeditionEndKind.Victory},{NativeExpeditionEndingFixtures.End.VictoryGuest,ExpeditionEndKind.Victory},
                {NativeExpeditionEndingFixtures.End.Extracted,ExpeditionEndKind.Extracted},{NativeExpeditionEndingFixtures.End.ExtractedSaved,ExpeditionEndKind.Extracted},{NativeExpeditionEndingFixtures.End.RunEnded,ExpeditionEndKind.Extracted},{NativeExpeditionEndingFixtures.End.Wiped,ExpeditionEndKind.Wiped},
                {NativeExpeditionEndingFixtures.End.Failed,ExpeditionEndKind.Failed},{NativeExpeditionEndingFixtures.End.LateJoiner,ExpeditionEndKind.Victory},{NativeExpeditionEndingFixtures.End.Minimal,ExpeditionEndKind.Wiped}};
            want[NativeExpeditionEndingFixtures.End.HostChange]=ExpeditionEndKind.Victory;
            foreach(NativeExpeditionEndingFixtures.End e in Enum.GetValues(F))
            {
                if(e==NativeExpeditionEndingFixtures.End.Off){Check(NativeExpeditionEndingFixtures.EndView(e)==null,"OFF: no view");continue;}
                var k=NativeExpeditionEnding.Classify(NativeExpeditionEndingFixtures.EndView(e),out bool down);Check(k==want[e],e+": "+want[e]+" ("+k+")");
                Check(NativeExpeditionEndingFixtures.EndNames.Length==Enum.GetValues(F).Length,"a name per ending fixture");
            }
            // A host change at victory: the ending stays (frozen, its choice waiting, said so), and is not replayed after the transfer.
            var hm=new ExpeditionEndingModel();hm.Step(NativeExpeditionEndingFixtures.QuotaView(NativeExpeditionEndingFixtures.Quota.Running),0,false,default);var hv=NativeExpeditionEndingFixtures.EndView(NativeExpeditionEndingFixtures.End.Victory);hm.Step(hv,1,false,default);
            var hc=NativeExpeditionEndingFixtures.EndView(NativeExpeditionEndingFixtures.End.HostChange);hm.Step(hc,2,false,default);
            Check(hm.Kind==ExpeditionEndKind.Victory&&hm.Frozen&&hm.Note==NativeExpeditionEnding.FrozenNote&&!hm.ActionEnabled[0]&&hm.ActionNote==NativeExpeditionEnding.FrozenNote&&hm.Age==1&&!hm.OpensScreen,"a host transfer during the victory: frozen, the choice waits, the sequence keeps its time");
            hc.migrating=false;hc.host="nova";hc.members[0].host=false;hc.members[1].host=true;hm.Step(hc,3,false,default);Check(hm.Kind==ExpeditionEndKind.Victory&&!hm.Frozen&&hm.ActionEnabled[0]&&hm.Age==2&&!hm.OpensScreen&&!hm.RowHost[0]&&hm.RowHost[1],"after the transfer: the same victory (no replay), the choice open again, HOST on the new host");
            var wf=NativeExpeditionEndingFixtures.EndView(NativeExpeditionEndingFixtures.End.Wiped);wf.migrating=true;Check(NativeExpeditionEnding.Classify(wf,out _)==ExpeditionEndKind.None,"a wipe is not decided while frozen");
            Check(NativeExpeditionEnding.Classify(NativeExpeditionEndingFixtures.EndView(NativeExpeditionEndingFixtures.End.VictoryDown),out bool d)==ExpeditionEndKind.Victory&&d,"down at victory: a victory, flagged down");
            var vd=Ended(NativeExpeditionEndingFixtures.End.VictoryDown,9);Check(vd.Note!=null&&vd.Note.Contains("DOWN WHEN IT FELL")&&vd.LootTone==ExpeditionTone.Danger&&vd.LootText.Contains("NOT ELIGIBLE"),"down at victory: the note and the world boss loot not eligible (the stored dice)");
            var vv=Ended(NativeExpeditionEndingFixtures.End.Victory,9);Check(vv.Title==NativeExpeditionEnding.TitleVictory&&vv.LootText.Contains("1 CHIP RESERVED FOR YOU")&&vv.ContinueText=="LOOP 2 · 1 / 3 READY"&&vv.WaitingText!=null,"victory: the title, your reserved world boss chip, the loop vote, the unopened reward");
            Check(vv.RowStatus[0]=="DECIDING"&&vv.RowStatus[1]=="VOTED TO EXTRACT"&&vv.RowStatus[2]=="READY · LOOP 2"&&vv.RowYou[0]&&vv.RowHost[0]&&vv.RowName[0]=="Sheldon","victory: you first; deciding, voted to extract, ready");
            var vg=Ended(NativeExpeditionEndingFixtures.End.VictoryGuest,9);Check(vg.RowYou[0]&&vg.RowName[0]=="Nova"&&!vg.RowHost[0]&&vg.RowHost[1]&&vg.WalletText=="$860"&&vg.ChipTotal==5,"a guest: their own row first, their own wallet and build only");
            var ex=Ended(NativeExpeditionEndingFixtures.End.Extracted,9);Check(ex.Title==NativeExpeditionEnding.TitleExtracted&&ex.Subtitle.Contains("WAITING FOR THE SAVE")&&ex.Tone==ExpeditionTone.Warn&&ex.Note=="2 PILOTS ARE STILL IN THE RUN","extracted, save not confirmed: says so, in amber");
            var es=Ended(NativeExpeditionEndingFixtures.End.ExtractedSaved,9);Check(es.Subtitle.Contains("EXTRACTION SAVED")&&es.Tone==ExpeditionTone.Cyan&&es.StatText[0]=="23:08","extracted and saved; the time frozen at the extraction (the completion's seconds, not the clock)");
            var re=Ended(NativeExpeditionEndingFixtures.End.RunEnded,9);Check(re.Kind==ExpeditionEndKind.Extracted&&re.Note=="THE RUN HAS ENDED · EVERY PILOT EXTRACTED","the last extraction: the run ended");
            var wi=Ended(NativeExpeditionEndingFixtures.End.Wiped,9);Check(wi.Title==NativeExpeditionEnding.TitleWiped&&wi.Subtitle=="THE WHOLE PARTY IS DOWN · CENTRAL STATION · BOSS FIGHT"&&wi.ZoneHere[1]&&wi.ZoneState[1].StartsWith("DOWN AT THE BOSS")&&wi.RowCount==4&&wi.RowStatus.Take(4).All(s=>s=="DOWN"),"wiped: where and what, the zone marked, every pilot down");
            var fa=Ended(NativeExpeditionEndingFixtures.End.Failed,9);Check(fa.Title==NativeExpeditionEnding.TitleFailed&&fa.Subtitle=="YOU WENT DOWN ALONE · WARD ZERO · KILL QUOTA 31 / 36"&&fa.PartyText=="SOLO"&&fa.ZoneState[0]=="DOWN HERE · 31 / 36","failed alone: where and how far");
            foreach(var x in new[]{vv,ex,wi,fa})Check(x.ZoneCount==4&&x.ZoneName[3]==NativeExpeditionPresentation.WorldBoss,"the route ends at the world boss ("+x.Kind+")");
            Check(vv.ZoneState[3]=="DEFEATED"&&wi.ZoneState[3].StartsWith("LOCKED"),"the world boss node: defeated, or locked");
            var late=Ended(NativeExpeditionEndingFixtures.End.LateJoiner,0);Check(late.Settled&&!late.OpensScreen&&late.Kind==ExpeditionEndKind.Victory,"a late joiner: settled at once, no auto-open");
        }

        // 4b. Through the real authority (the DEV preview's own run of the rules: NativeExpeditionPreview).
        private static void Authority()
        {
            var p=new NativeExpeditionPreview();p.Load(Array.FindIndex(NativeExpeditionPreview.Scenarios,s=>s.Id=="victory"));var m=new ExpeditionEndingModel();var g=new ExpeditionEndingModel();float t=0;
            void Step(){t+=1/30f;m.Step(p.View("sheldon"),t,false,new ExpeditionEndContext{Host=true});g.Step(p.View("nova"),t,false,default);}
            Step();Check(!m.Shown&&!g.Shown,"the world boss fight: no ending");
            p.Next();Step();Check(!m.Shown,"a pilot down while the others stand: no ending (the dead pilot's progress screen is J's board)");
            Check(NativeExpeditionEnding.Classify(p.View("kestrel"),out _)==ExpeditionEndKind.None,"the downed pilot herself: no ending while the team stands");
            p.Next();Step();Check(m.Shown&&m.Kind==ExpeditionEndKind.Victory&&m.OpensScreen&&!m.Settled&&g.OpensScreen,"the world boss falls: the victory, watched, opens the screen for host and guest");
            Check(m.StatText[0]=="0:00","the count-ups start at zero");
            Step();Check(!m.OpensScreen,"opens once");
            for(int i=0;i<200;i++)Step();Check(m.Settled&&m.StatText[1]=="3 / 3"&&m.RowCount==4,"settled: every zone, four pilots");
            Check(m.RowStatus.Take(4).Contains("DOWN"),"Kestrel's row: down");
            p.Next();Step();var astra=NativeExpeditionEnding.Classify(p.View("astra"),out _);Check(astra==ExpeditionEndKind.Extracted&&m.Kind==ExpeditionEndKind.Victory&&m.RowStatus.Take(4).Contains("EXTRACTED"),"Astra extracts: her ending is EXTRACTED, the others' victory shows her extracted");
            p.Next();Step();Check(m.ContinueText.Contains("2 / 3"),"Nova and Kestrel choose Continue: 2 / 3 ready ("+m.ContinueText+")");
            p.Next();Step();Check(!m.Shown,"loop 2 starts: the victory ends (the transfer screen takes over)");
            var x=new ExpeditionEndingModel();x.Step(p.View("astra"),t,false,default);Check(x.Shown&&x.Kind==ExpeditionEndKind.Extracted&&x.Settled,"the extracted pilot's ending survives the others' next loop (shown settled to a new screen)");
            // A wipe in the final fight: everyone down.
            var w=new NativeExpeditionPreview();w.Load(Array.FindIndex(NativeExpeditionPreview.Scenarios,s=>s.Id=="victory"));var wm=new ExpeditionEndingModel();wm.Step(w.View("sheldon"),0,false,default);
            foreach(var a in new[]{"sheldon","astra","nova","kestrel"})w.Run.Defeat(a);
            wm.Step(w.View("sheldon"),1,false,default);Check(wm.Kind==ExpeditionEndKind.Wiped&&wm.OpensScreen&&wm.Subtitle=="THE WHOLE PARTY IS DOWN · THE WORLD BOSS"&&wm.ZoneHere[3],"a wipe at the world boss: watched, where it ended");
            Check(w.View("sheldon").Member("sheldon").xpCompletion!=null&&!w.View("sheldon").Member("sheldon").xpCompletion.won,"the authority froze a lost completion for the account XP column");
            // A solo pilot down in a zone, and a pilot who reconnects standing after the others fell (the run goes on: no ending).
            var solo=NativeExpedition.Create("solo","city","me","pistol",3,new[]{new ExpeditionInstance{id="ward-zero",target=36}});Check(solo.Enter("ward-zero")&&solo.Progress("k1",12)&&solo.Defeat("me"),"a solo run: enter, 12 kills, down");
            var sm=new ExpeditionEndingModel();sm.Step(NativeExpeditionView.Build(solo,"me"),0,false,default);Check(sm.Kind==ExpeditionEndKind.Failed&&sm.Settled,"solo failure on first sight: settled");
            var two=NativeExpedition.Create("two","city","a","pistol",3,new[]{new ExpeditionInstance{id="ward-zero",target=36}});Check(two.Join("b","rifle")&&two.Enter("ward-zero")&&two.Disconnect("b")&&two.Defeat("a"),"a run of two: b drops out standing, a falls");
            var tm=new ExpeditionEndingModel();tm.Step(NativeExpeditionView.Build(two,"a"),0,false,default);Check(tm.Kind==ExpeditionEndKind.Wiped,"nobody standing and connected: the run stops, wiped");
            Check(two.Reconnect("b"),"b reconnects, standing");tm.Step(NativeExpeditionView.Build(two,"a"),1,false,default);Check(!tm.Shown,"someone stands again: the ending goes away");
        }

        // 5. The sequence and its count-ups.
        private static void Sequence()
        {
            Check(NativeExpeditionEnding.TitleAt<NativeExpeditionEnding.RiseAt&&NativeExpeditionEnding.RiseAt<NativeExpeditionEnding.PanelAt&&NativeExpeditionEnding.PanelAt<NativeExpeditionEnding.StatsAt&&NativeExpeditionEnding.StatsAt<NativeExpeditionEnding.RowsAt&&NativeExpeditionEnding.RowsAt<NativeExpeditionEnding.RewardsAt&&NativeExpeditionEnding.RewardsAt<NativeExpeditionEnding.XpAt&&NativeExpeditionEnding.XpAt<NativeExpeditionEnding.ActionsAt&&NativeExpeditionEnding.ActionsAt<NativeExpeditionEnding.SettleAt,"title, rise, panel, stats, party, rewards, XP, actions, settled: in that order");
            Check(NativeExpeditionEnding.StatsAt+3*NativeExpeditionEnding.StatStep+NativeExpeditionEnding.CountFor<=NativeExpeditionEnding.SettleAt&&NativeExpeditionEnding.RewardsAt+.1f+NativeExpeditionEnding.CountFor<=NativeExpeditionEnding.SettleAt,"every count-up ends before the sequence settles");
            foreach(long final in new long[]{0,1,3,36,108,1694,1240,999999})
            {
                long prev=-1;for(int k=0;k<=NativeExpeditionEnding.CountSteps;k++){long n=NativeExpeditionEnding.Value(final,k);Check(n>=prev&&n>=0&&n<=final,"count-up of "+final+" is monotonic within 0.."+final+" (step "+k+")");prev=n;}
                Check(NativeExpeditionEnding.Value(final,0)==0&&NativeExpeditionEnding.Value(final,NativeExpeditionEnding.CountSteps)==final&&NativeExpeditionEnding.Value(final,999)==final&&NativeExpeditionEnding.Value(final,-3)==0,"count-up of "+final+": 0 first, exactly "+final+" last");
            }
            Check(NativeExpeditionEnding.Step(NativeExpeditionEnding.StatsAt-.1f,NativeExpeditionEnding.StatsAt)==0&&NativeExpeditionEnding.Step(NativeExpeditionEnding.StatsAt+NativeExpeditionEnding.CountFor,NativeExpeditionEnding.StatsAt)==NativeExpeditionEnding.CountSteps,"a count runs over exactly CountFor ("+NativeExpeditionEnding.CountFor+" s)");
            Check(NativeExpeditionEnding.Step(NativeExpeditionEnding.StatsAt+NativeExpeditionEnding.CountFor*.25f,NativeExpeditionEnding.StatsAt)>NativeExpeditionEnding.CountSteps/2,"ease out: more than half by a quarter of the time");
            // The model's own count-ups, sampled every frame: 0 until each tile's start, monotonic, final after its count.
            var m=new ExpeditionEndingModel();m.Step(NativeExpeditionEndingFixtures.QuotaView(NativeExpeditionEndingFixtures.Quota.Running),0,false,default);var v=NativeExpeditionEndingFixtures.EndView(NativeExpeditionEndingFixtures.End.Wiped);
            var finals=new[]{"18:47","1 / 3","72","1"};var last=new string[4];var seq=new List<string>[4];for(int i=0;i<4;i++)seq[i]=new List<string>();
            for(float t=0;t<=7;t+=1/60f)
            {
                m.Step(v,1+t,false,default);
                for(int i=0;i<4;i++){float at=NativeExpeditionEnding.StatsAt+i*NativeExpeditionEnding.StatStep;if(t<at-1e-3f)Check(m.StatText[i]==(i==0?"0:00":i==1?"0 / 3":"0"),"stat "+i+" shows zero before its start ("+t+")");if(t>=at+NativeExpeditionEnding.CountFor+1e-3f)Check(m.StatText[i]==finals[i],"stat "+i+" final after its count ("+m.StatText[i]+")");if(!ReferenceEquals(m.StatText[i],last[i])){seq[i].Add(m.StatText[i]);last[i]=m.StatText[i];}}
                if(t<NativeExpeditionEnding.RiseAt)Check(m.Rise==0,"the title holds the centre until "+NativeExpeditionEnding.RiseAt+" s");
                if(t>=NativeExpeditionEnding.RiseAt+NativeExpeditionEnding.RiseFor)Check(m.Rise==1,"the title is in the header after the rise");
                if(t<NativeExpeditionEnding.ActionsAt)Check(m.Chosen==ExpeditionEndAction.None,"no action can be chosen before the actions show");
                if(Math.Abs(t-NativeExpeditionEnding.SettleAt)>.01f)Check(m.Playing==(t<NativeExpeditionEnding.SettleAt),"playing until "+NativeExpeditionEnding.SettleAt+" s ("+t+")");
            }
            var secs=seq[0].Select(s=>{var p=s.Split(':');return int.Parse(p[0])*60+int.Parse(p[1]);}).ToList();Check(secs.Count>10&&secs.Zip(secs.Skip(1),(a,b)=>b>a).All(x=>x)&&secs.Last()==18*60+47,"the run time counts up through "+secs.Count+" distinct values to 18:47");
            Check(seq[2].Select(int.Parse).Zip(seq[2].Skip(1).Select(int.Parse),(a,b)=>b>a).All(x=>x),"quota kills count up monotonically");
            // Skip, Reduced effects, and one sequence per ending.
            var s=new ExpeditionEndingModel();s.Step(NativeExpeditionEndingFixtures.QuotaView(NativeExpeditionEndingFixtures.Quota.Running),0,false,default);s.Step(v,1,false,default);Check(s.Playing,"playing");s.Skip();s.Step(v,1.1f,false,default);Check(s.Settled&&s.StatText[0]=="18:47"&&s.Rise==1&&s.ActionsIn==1&&s.Glitch<0&&s.Sweep<0&&s.Bars==0,"a skip jumps to the settled summary");
            var rd=new ExpeditionEndingModel();rd.Step(NativeExpeditionEndingFixtures.QuotaView(NativeExpeditionEndingFixtures.Quota.Running),0,true,default);rd.Step(v,1,true,default);Check(rd.Settled&&rd.OpensScreen&&rd.StatText[0]=="18:47"&&rd.Flash==0&&rd.Glitch<0,"reduced effects: the settled summary at once (still opening the screen once)");
            var same=new ExpeditionEndingModel();same.Step(NativeExpeditionEndingFixtures.QuotaView(NativeExpeditionEndingFixtures.Quota.Running),0,false,default);same.Step(v,1,false,default);float a0=same.Age;same.Step(v,2,false,default);var v2=NativeExpeditionEndingFixtures.EndView(NativeExpeditionEndingFixtures.End.Wiped);v2.host="nova";v2.members[0].host=false;v2.members[1].host=true;same.Step(v2,3,false,default);
            Check(same.Age>a0&&same.Age==2&&!same.OpensScreen,"a new view object, a host change: the same ending keeps playing (no replay)");
            Check(!same.RowHost[0]&&same.RowHost[1],"the HOST mark follows the authority's host (members[].host)");
            same.Step(NativeExpeditionEndingFixtures.EndView(NativeExpeditionEndingFixtures.End.Wiped),3.5f,false,new ExpeditionEndContext{Host=true});Check(same.Action[0]==ExpeditionEndAction.Lobby,"becoming the network host: the lobby return is offered");
        }

        // 6. The actions.
        private static void Actions()
        {
            ExpeditionEndingModel A(NativeExpeditionEndingFixtures.End e,ExpeditionEndContext c)=>Ended(e,9,false,c);
            ExpeditionEndAction[] Of(ExpeditionEndingModel m)=>m.Action.Take(m.ActionCount).ToArray();
            var host=new ExpeditionEndContext{Host=true};var guest=new ExpeditionEndContext();var offline=new ExpeditionEndContext{Offline=true};var returning=new ExpeditionEndContext{Host=true,Returning=true};var lost=new ExpeditionEndContext{HostLost=true};
            var v=A(NativeExpeditionEndingFixtures.End.Victory,host);Check(Of(v).SequenceEqual(new[]{ExpeditionEndAction.Choose,ExpeditionEndAction.Close})&&v.ActionPrimary[0]&&v.Action[v.Focus]==ExpeditionEndAction.Choose,"victory: extract or go again (the board's victory choices), close");
            var w=A(NativeExpeditionEndingFixtures.End.Wiped,host);Check(Of(w).SequenceEqual(new[]{ExpeditionEndAction.Lobby,ExpeditionEndAction.Menu,ExpeditionEndAction.Board})&&w.ActionEnabled[0]&&w.ActionNote==null,"wiped, host: return to lobby, main menu, run board");
            var wg=A(NativeExpeditionEndingFixtures.End.Wiped,guest);Check(Of(wg).SequenceEqual(new[]{ExpeditionEndAction.Menu,ExpeditionEndAction.Board})&&wg.ActionNote==NativeExpeditionEnding.GuestNote,"wiped, guest: main menu, the board, and who can return everyone");
            var wo=A(NativeExpeditionEndingFixtures.End.Failed,offline);Check(Of(wo)[0]==ExpeditionEndAction.Lobby&&wo.ActionEnabled[0],"offline: the lobby return (the pause menu's own condition)");
            var wr=A(NativeExpeditionEndingFixtures.End.Wiped,returning);Check(!wr.ActionEnabled[0]&&wr.Action[wr.Focus]==ExpeditionEndAction.Menu&&wr.ActionNote==NativeExpeditionEnding.ReturningNote,"a lobby return already counting down: disabled, the focus skips it");
            var wl=A(NativeExpeditionEndingFixtures.End.Wiped,lost);Check(Of(wl).SequenceEqual(new[]{ExpeditionEndAction.Menu,ExpeditionEndAction.Board})&&wl.ActionNote==NativeExpeditionEnding.LostNote,"the host gone: main menu (to reclaim the seat)");
            var ex=A(NativeExpeditionEndingFixtures.End.Extracted,guest);Check(Of(ex).SequenceEqual(new[]{ExpeditionEndAction.Menu,ExpeditionEndAction.Close,ExpeditionEndAction.Board}),"extracted while the others play: main menu, keep watching, the board");
            var en=A(NativeExpeditionEndingFixtures.End.RunEnded,host);Check(Of(en)[0]==ExpeditionEndAction.Lobby,"the run ended: the host returns everyone");
            var fr=NativeExpeditionEndingFixtures.EndView(NativeExpeditionEndingFixtures.End.HostChange);var fm=new ExpeditionEndingModel();fm.Step(fr,0,true,host);Check(Of(fm)[0]==ExpeditionEndAction.Choose&&!fm.ActionEnabled[0]&&fm.Action[fm.Focus]==ExpeditionEndAction.Close,"frozen victory: the choice waits, the focus on close");
            // Focus: wraps over the enabled actions only.
            var f=A(NativeExpeditionEndingFixtures.End.Wiped,host);Check(f.Focus==0,"focus on the primary");f.Move(1);Check(f.Focus==1,"right");f.Move(1);Check(f.Focus==2,"right");f.Move(1);Check(f.Focus==0,"wraps");f.Move(-1);Check(f.Focus==2,"left wraps");
            var fd=A(NativeExpeditionEndingFixtures.End.Wiped,returning);for(int i=0;i<6;i++){fd.Move(1);Check(fd.ActionEnabled[fd.Focus],"never on a disabled action");}
            Check(f.Chosen==f.Action[f.Focus],"Enter chooses the focused action once they show");
            foreach(ExpeditionEndAction a in Enum.GetValues(typeof(ExpeditionEndAction)))if(a!=ExpeditionEndAction.None)Check(NativeExpeditionEnding.Label(a,2).Length>0&&NativeExpeditionEnding.Label(a,2).Length<=20,"a short label for "+a);
        }

        // 7. Missing optional data.
        private static void Missing(JsonSerializerOptions o)
        {
            var m=Ended(NativeExpeditionEndingFixtures.End.Minimal,9);
            Check(m.Kind==ExpeditionEndKind.Wiped&&m.StatText[0]=="1:15","no account XP completion: the clock frozen when the ending began");
            Check(m.RowName[0]=="sheldon"&&m.RowWeapon[0]==null&&m.RowName[1]=="pilot-2"&&m.RowStatus[1]=="OFFLINE","no names or weapons: the account id, no icon; an offline pilot");
            Check(m.ChipsText=="NO CHIPS"&&m.ChipShown==0&&m.WalletText=="$0"&&m.LootText==null&&m.WaitingText==null,"no chips, an empty wallet, no dice, no rewards: said plainly, nothing invented");
            Check(m.ZoneCount==2&&m.ZoneName[1]==NativeExpeditionPresentation.WorldBoss,"one zone: the route still ends at the world boss");
            // The guest's decode: absent strings as "", absent arrays as null, then the contract's Normalize.
            foreach(NativeExpeditionEndingFixtures.End e in Enum.GetValues(typeof(NativeExpeditionEndingFixtures.End)))
            {
                var v=NativeExpeditionEndingFixtures.EndView(e);if(v==null)continue;
                var json=JsonSerializer.Serialize(v,o);var back=NativeExpeditionView.Normalize(JsonSerializer.Deserialize<ExpeditionView>(json,o));
                Check(NativeExpeditionEnding.Classify(back,out _)==NativeExpeditionEnding.Classify(v,out _),e+": the same outcome after the round trip");
                var a=new ExpeditionEndingModel();var b=new ExpeditionEndingModel();a.Step(v,0,true,default);b.Step(back,0,true,default);
                Check(a.Title==b.Title&&a.Subtitle==b.Subtitle&&a.RowCount==b.RowCount&&a.StatText.SequenceEqual(b.StatText)&&a.WalletText==b.WalletText&&a.ChipsText==b.ChipsText,e+": the same summary after the round trip");
            }
            var bare=new ExpeditionView{run="r",viewer="me",mine=new ExpeditionViewPersonal{account="me"},members=new[]{new ExpeditionViewMember{account="me",connected=true}}};
            var bm=new ExpeditionEndingModel();bm.Step(bare,0,false,default);Check(bm.Kind==ExpeditionEndKind.Failed&&bm.StatCount==4&&bm.ZoneCount==0&&bm.Title!=null,"a bare view (no instances, no clock): a failure with zeros, no route, no crash");
            var none=new ExpeditionEndingModel();none.Step(new ExpeditionView(),0,false,default);none.Step(null,1,false,default);Check(!none.Shown,"an empty view or none: nothing");
            var spectator=NativeExpeditionEndingFixtures.EndView(NativeExpeditionEndingFixtures.End.Wiped);spectator.mine=new ExpeditionViewPersonal();Check(NativeExpeditionEnding.Classify(spectator,out _)==ExpeditionEndKind.None,"a viewer who is not a member: nothing");
            var q=new ExpeditionQuotaModel();var qv=View(20);qv.members=Array.Empty<ExpeditionViewMember>();qv.bossArrivals=null;q.Step(qv,0,false,1600);Check(q.Shown&&q.Party==0,"the quota without members or arrivals: the module, no pips");
        }

        // 8. The summary's layout.
        private static void EndingLayout()
        {
            foreach(var (w,h,s,name) in Screens)
            {
                var u=NativeUiMotion.Measure(w,h,s);float W=u.Width,H=u.Height;
                foreach(bool xp in new[]{false,true})for(int rows=1;rows<=8;rows++)foreach(bool extra in new[]{false,true})
                {
                    var l=ExpeditionEndingLayout.Of(W,H,xp,rows,4,extra);string at=name+" xp="+xp+" rows="+rows+" vote="+extra;
                    Check(l.X>=ExpeditionEndingLayout.Margin-.01f&&l.X+l.W<=W-ExpeditionEndingLayout.Margin+.01f&&l.Y>=ExpeditionEndingLayout.Margin-.01f&&l.Y+l.H<=H-ExpeditionEndingLayout.Margin+.01f,at+": the panel inside the canvas");
                    float bottom=l.ActionsY+l.ActionsH+l.Pad;Check(bottom<=l.Y+l.H+.5f||l.H>=H-2*ExpeditionEndingLayout.Margin-.5f&&bottom<=H-4,at+": the actions inside the panel ("+bottom+" / "+(l.Y+l.H)+")");
                    Check(l.HeaderY<l.HeaderTitleY&&l.HeaderTitleY+l.TitleSize<=l.SubY+.01f&&l.SubY<l.StatsY&&l.StatsY+l.StatsH<=(l.ZonesShown?l.ZonesY:l.PartyY)+.01f&&(!l.ZonesShown||l.ZonesY+l.ZonesH<=l.PartyY+.01f)&&l.PartyY+l.PartyH<=l.RewardsY+.01f&&l.RewardsY+l.RewardsH<=l.ActionsY+.01f,at+": the blocks in order, none overlapping");
                    for(int i=0;i<rows;i++){l.Row(i,out float rx,out float ry,out float rw);Check(rx>=l.X+l.Pad-.01f&&rx+rw<=l.X+l.W-l.Pad+.01f&&ry>=l.PartyY&&ry+l.RowH<=l.PartyY+l.PartyH-(extra?20:0)+4.01f,at+": party row "+i+" in its block");}
                    for(int i=0;i<3;i++){l.Button(i,3,out float bx,out float bw);Check(bx>=l.X+l.Pad-.01f&&bx+bw<=l.X+l.W-l.Pad+.01f&&bw>=120,at+": button "+i+" fits ("+bw+")");}
                    for(int i=0;i<4;i++){l.Tile(i,4,out float tx,out float tw);Check(tw>=110&&tx+tw<=l.X+l.W-l.Pad+.01f,at+": stat tile "+i+" ("+tw+")");}
                    if(xp&&l.Xp)
                    {
                        var d=NativeAccountXpLayout.Dock(W,H,l.X,l.W,l.Y,ExpeditionEndingLayout.HostMin,4,AccountXpStatus.Confirmed,false);
                        Check(d.Docked&&d.Panel.X>=l.X+l.W+NativeAccountXpLayout.Gap-.01f&&d.Panel.X+d.Panel.W<=W-NativeAccountXpLayout.Margin+.01f&&d.Panel.Y+d.Panel.H<=H-NativeAccountXpLayout.Margin+.01f,at+": the account XP column beside the summary, inside the canvas");
                    }
                    if(xp&&!l.Xp)Check(!NativeAccountXpLayout.Fits(W,ExpeditionEndingLayout.HostMin),at+": no XP column only when it cannot dock (the mini card then)");
                    // The longest words fit their boxes (Liberation Mono .6 em + tracking; the bold sans title at its measured ~.72 em).
                    float inner=l.W-2*l.Pad;float Mono(string t,float size,float tr)=>t.Length*size*(.6f+tr);
                    Check(Mono("THE WHOLE PARTY IS DOWN · CENTRAL STATION · KILL QUOTA 36 / 36",13,.2f)<=inner+.5f||l.Compact,at+": the longest subtitle fits ("+Mono("THE WHOLE PARTY IS DOWN · CENTRAL STATION · KILL QUOTA 36 / 36",13,.2f)+" / "+inner+")");
                    Check(NativeExpeditionEnding.TitleVictory.Length*l.TitleSize*.8f<=inner,at+": the header title fits");
                    Check(NativeExpeditionEnding.TitleVictory.Length*l.BigTitle*.8f<=W-32,at+": the big title fits the canvas");
                    Check(l.TitleCenterY+l.BigTitle*1.2f+60<H&&l.TitleCenterY-l.BigTitle-40>H*.09f,at+": the title moment clear of the letterbox bars");
                }
            }
            var c=ExpeditionEndingLayout.Of(NativeUiMotion.Measure(1280,720,2).Width,NativeUiMotion.Measure(1280,720,2).Height,true,8,4,true);Check(c.Compact,"a short canvas (720p @2) turns compact");
            var big=ExpeditionEndingLayout.Of(1600,900,true,3,4,true);Check(!big.Compact&&big.Xp&&big.PartyColumns==2,"1080p: full size, the XP column docked, two party columns");
        }

        // 9. No allocation per frame.
        private static void NoAllocation()
        {
            var views=new[]{View(20),View(21),View(31),View(35),View(36),View(36,bosses:new[]{"m1-ward-siren"})};
            var q=new ExpeditionQuotaModel();float t=0;foreach(var v in views)for(int i=0;i<30;i++)q.Step(v,t+=1/60f,false,1600);
            var e=new ExpeditionEndingModel();var lead=NativeExpeditionEndingFixtures.QuotaView(NativeExpeditionEndingFixtures.Quota.Running);var end=NativeExpeditionEndingFixtures.EndView(NativeExpeditionEndingFixtures.End.Victory);
            var ctx=new ExpeditionEndContext{Host=true};e.Step(lead,0,false,ctx);for(float k=0;k<7;k+=1/60f)e.Step(end,1+k,false,ctx);
            int sink=0;GC.Collect();long before=GC.GetAllocatedBytesForCurrentThread();
            // The warmed quota over the same views (its words already built, kills and stage changes included), 1,200 frames.
            for(int f=0;f<1200;f++){var v=views[(f/200)%views.Length];q.Step(v,t+=1/60f,false,1600);sink+=q.CountText.Length+(q.Beat?1:0);}
            long bytes=GC.GetAllocatedBytesForCurrentThread()-before;Check(bytes==0,"the quota model allocates nothing per frame ("+bytes+" bytes over 1200 frames)");
            // The settled ending, 1,200 frames.
            GC.Collect();before=GC.GetAllocatedBytesForCurrentThread();
            for(int f=0;f<1200;f++){e.Step(end,10+f/60f,false,ctx);sink+=e.StatText[0].Length+e.WalletText.Length;}
            bytes=GC.GetAllocatedBytesForCurrentThread()-before;Check(bytes==0,"the settled ending allocates nothing per frame ("+bytes+" bytes over 1200 frames)");Check(sink>0,"the frames ran");
            // And through a whole sequence once its words exist (the count-ups walk precomputed steps).
            var s=new ExpeditionEndingModel();s.Step(lead,0,false,ctx);s.Step(end,1,false,ctx);GC.Collect();before=GC.GetAllocatedBytesForCurrentThread();
            for(float k=0;k<7;k+=1/60f){s.Step(end,1+k,false,ctx);sink+=s.StatText[3].Length;}
            bytes=GC.GetAllocatedBytesForCurrentThread()-before;Check(bytes==0,"no allocation through the whole cinematic ("+bytes+" bytes)");
        }

        // 10. Source scans.
        private static string Body(string text,string head)
        {
            int i=text.IndexOf(head,StringComparison.Ordinal);if(i<0)return null;int b=text.IndexOf('{',i);int depth=0;
            for(int j=b;j<text.Length;j++){if(text[j]=='{')depth++;else if(text[j]=='}'&&--depth==0)return text.Substring(i,j-i+1);}return null;
        }
        private static void Sources(string root)
        {
            string A=Path.Combine(root,"Assets/ChipArena");string R(string f)=>File.ReadAllText(Path.Combine(A,f));
            string qcore=R("Core/NativeExpeditionQuotaModel.cs"),ecore=R("Core/NativeExpeditionEnding.cs"),fix=R("Core/NativeExpeditionEndingFixtures.cs"),qhud=R("Runtime/NativeExpeditionQuotaHud.cs"),ehud=R("Runtime/NativeExpeditionEndingHud.cs"),eui=R("Runtime/NativeExpeditionEndingUI.cs");
            string live=R("Runtime/NativeExpeditionLiveUI.cs"),hud=R("Runtime/NativeExpeditionHud.cs"),marks=R("Runtime/NativeQuotaMarksPresentation.cs"),dev=R("Runtime/NativeDevPanel.cs"),game=R("Runtime/ConversionGame.cs");
            // Engine-free rules; presentation only everywhere.
            foreach(var (name,src) in new[]{("quota rules",qcore),("ending rules",ecore),("fixtures",fix)})Check(!src.Contains("using UnityEngine")&&!src.Contains("NativeArena")&&!src.Contains("NativeMinion"),name+": engine-free (Core)");
            foreach(var (name,src) in new[]{("quota rules",qcore),("ending rules",ecore),("quota HUD",qhud),("ending HUD",ehud),("ending UI",eui)})
            {
                foreach(var bad in new[]{"Random","DateTime","Send(","Expedition.Session","NetworkFrame","Progress(","StartBosses","DefeatBoss","FinishFinal","Defeat(","Extract(","VoteContinue","NextLoop","Credit(","Reprint(","SetActive(","Cursor.lockState","Cursor.visible","QueueXp","RetryXp"})
                    Check(!src.Contains(bad),name+": no "+bad+" (presentation only; the cursor stays the Expedition screen's)");
                Check(!Regex.IsMatch(src,@"\bNativeExpedition\b"),name+": no authority reference");
                Check(!Regex.IsMatch(src,@"\b(v|view)\.\w+(\.\w+)?\s*=[^=]")&&!Regex.IsMatch(src,@"\bm\.(alive|connected|extracted|host|area|xpCompletion)\s*=[^=]")&&!Regex.IsMatch(src,@"\bme\.\w+\s*=[^=]"),name+": writes no view field");
            }
            // Replicated fields only: every view, member, instance and personal field the rules read is a field of the replicated contract.
            var contract=new HashSet<string>(typeof(ExpeditionView).GetFields().Select(f=>f.Name).Concat(new[]{"CurrentBuilding","Member","ViewerIsHost"}));
            var memberF=new HashSet<string>(typeof(ExpeditionViewMember).GetFields().Select(f=>f.Name));var instF=new HashSet<string>(typeof(ExpeditionViewInstance).GetFields().Select(f=>f.Name));
            var mineF=new HashSet<string>(typeof(ExpeditionViewPersonal).GetFields().Select(f=>f.Name));
            foreach(var (name,src) in new[]{("quota rules",qcore),("ending rules",ecore)})
            {
                foreach(Match mt in Regex.Matches(src,@"\bv\.(\w+)"))Check(contract.Contains(mt.Groups[1].Value),name+": reads the replicated view's "+mt.Groups[1].Value);
                foreach(Match mt in Regex.Matches(src,@"\bv\.mine\.(\w+)|\bme\.(\w+)"))Check(mineF.Contains(mt.Groups[1].Success?mt.Groups[1].Value:mt.Groups[2].Value),name+": reads the viewer's own block's "+mt.Value);
                foreach(Match mt in Regex.Matches(src,@"\bm\.(alive|connected|extracted|host|area|account|name|weapon|extractVote|xpCompletion|seat|carriedBy)\b"))Check(memberF.Contains(mt.Groups[1].Value),name+": a member field "+mt.Groups[1].Value);
                foreach(Match mt in Regex.Matches(src,@"\b[bi]\.(progress|target|cleared|entered|bosses|defeated|id|current)\b"))Check(instF.Contains(mt.Groups[1].Value),name+": an instance field "+mt.Groups[1].Value);
            }
            Check(!Regex.IsMatch(ecore,@"\bv\.(revision|savedRevision|anchors|chests|hub|joins|closures)\b"),"the ending never keys on the view's revision (it rises every tick) or the world pieces");
            // The draws build no strings per frame; palette only; motion respects Reduced effects.
            foreach(var (name,src) in new[]{("quota HUD",qhud),("ending HUD",ehud)})
            {
                string code=Regex.Replace(src,@"//[^\n]*","");int cut=code.IndexOf("public sealed partial class ConversionGame",StringComparison.Ordinal);if(cut>0)code=code.Substring(0,cut);
                Check(!Regex.IsMatch(code,"\\+\\s*\"|\"\\s*\\+|\\$\"|ToString\\(|string\\.Format|new List|Substring\\(|\\.Select\\(|\\.Where\\("),name+": builds no strings or queries per frame");
                Check(!Regex.IsMatch(src,@"0x[0-9a-fA-F]{6}")&&src.Contains("NativeCyberTheme"),name+": the palette only (no restated colour)");
                foreach(var statement in src.Split(';'))if(statement.Contains("Text("))foreach(var tone in new[]{"Violet","Dim","Edge","CyanDeep"})Check(!statement.Contains("C."+tone+"("),name+": no "+tone+" text");
                Check(src.Contains("reduced"),name+": Reduced effects");
                Check(!Regex.IsMatch(code,@"U\.Push\([^;]*,\s*(45|90|-45)\s*\)"),name+": no rotated transform inside a clip band (markup instead)");
            }
            Check(qhud.Contains("if(beat<.9f&&!reduced)")&&qhud.Contains("if(beat<Q.BeatFlash&&!reduced)")&&qhud.Contains("!reduced&&m.KillAge<Q.Sweep")&&qhud.Contains("reduced||m.KillAge>=Q.KillFlash"),"the quota's streak, flash, sweep and cell flash are off with Reduced effects");
            Check(ehud.Contains("if(!reduced&&m.Flash>0)")&&ehud.Contains("if(!reduced&&m.Glitch>=0)")&&ehud.Contains("if(!reduced&&m.Sweep>=0)")&&ehud.Contains("blur:reduced?0:m.TitleBlur"),"the ending's flash, glitch, sweep and blur are off with Reduced effects");
            Check(qhud.Contains("NativeQuotaMarksHud.Kite(")&&qcore.Contains("NativeQuotaMarks.Tag(q.Remaining)")&&qcore.Contains("LastAt=>NativeQuotaMarks.Threshold"),"the LAST row is the markers' own words, kite and threshold");
            // The hooks.
            var sync=Body(live,"private void ExpeditionLiveSync()");Check(sync.Contains("ExpeditionQuotaSync(null);ExpeditionEndingSync(null);return;")&&sync.TrimEnd().EndsWith("ExpeditionQuotaSync(v);ExpeditionEndingSync(v);\n        }")&&sync.IndexOf("AccountXpExpeditionSync();",StringComparison.Ordinal)<sync.IndexOf("ExpeditionEndingSync(v);",StringComparison.Ordinal),"the models follow the view in ExpeditionLiveSync, after the account XP sync (so the ending can keep the card closed)");
            var keys=Body(live,"private bool ExpeditionLiveKeys()");Check(keys.IndexOf("ExpeditionLiveSync();",StringComparison.Ordinal)<keys.IndexOf("ExpeditionEndingKeys();",StringComparison.Ordinal)&&keys.IndexOf("ExpeditionEndingKeys();",StringComparison.Ordinal)<keys.IndexOf("KeyCode.J",StringComparison.Ordinal)&&keys.IndexOf("GUIUtility.keyboardControl!=0",StringComparison.Ordinal)<keys.IndexOf("ExpeditionEndingKeys();",StringComparison.Ordinal),"the ending's keys after the sync, before J, not while a text field has focus");
            var gui=Body(live,"private bool ExpeditionLiveGui(");Check(gui.IndexOf("if(v==null||!expeditionScreenOpen)return false;",StringComparison.Ordinal)<gui.IndexOf("if(ExpeditionEndingGui(v,width,height))return true;",StringComparison.Ordinal)&&gui.IndexOf("if(ExpeditionEndingGui(v,width,height))return true;",StringComparison.Ordinal)<gui.IndexOf("NativeExpeditionScreen.Panels(",StringComparison.Ordinal)&&gui.Contains("ExpeditionEndingButton(new Rect(foot.xMax-402,foot.y+1,126,30),modal);"),"the ending draws on the open Expedition screen in place of the board; the board's RUN SUMMARY");
            var scene=Body(live,"private void ExpeditionLiveHud(");Check(scene.Contains("bool ending=ExpeditionEndingTag(width);E.SceneQuota=ending||expeditionQuota.Shown;\n            E.Minimal(v,width,1,false);\n            E.SceneQuota=false;if(!ending)ExpeditionQuotaHud(width,height);")&&scene.Contains("float top=ending?58:ExpeditionTopStack;"),"the top centre: the module or the ending's strip in place of the objective tag; the banner and plates below it");
            Check(hud.Contains("public static bool SceneQuota;")&&Body(hud,"public static void Minimal(").Contains("if(!SceneQuota)"),"the minimal HUD's objective tag steps aside for them");
            Check(marks.Contains("if(!expeditionQuota.Shown)NativeQuotaMarksHud.Tag("),"the markers' LAST tag steps aside while the module shows it");
            // Cursor ownership: the ending lives on the Expedition screen, so #290's path owns the pointer and pauses spectating.
            Check(game.Contains("bool look=!BoardOpen&&!ExpeditionScreenOpen&&DevLook(")&&R("Runtime/NativeReplay.cs").Contains("ExpeditionScreenOpen)return;")&&eui.Contains("expeditionScreenOpen=true;")&&!Regex.IsMatch(eui+ehud,@"\bCursor\."),"cursor: the open Expedition screen's (no cursor writes of its own)");
            Check(Body(eui,"private void ExpeditionEndingSync(").Contains("if(showing&&NativeAccountXpLayout.Fits(U.W,ExpeditionEndingLayout.HostMin))accountXpCardOpen=false;")&&eui.Contains("if(xp&&l.Xp&&m.XpIn>0&&m.PanelIn>0)AccountXpDocked(AccountXpSurface.Expedition,l.X,l.W,l.Y,ExpeditionEndingLayout.HostMin,m.XpIn*m.PanelIn);")&&eui.Contains("else if(card)AccountXpExpeditionCard(W,Hh);")&&!Regex.IsMatch(eui+ehud+qhud,@"\bNativeAccountXp(Panel|Hud|Badge|Seen)\b"),"the account XP column is the account XP panel's own dock (no card over the ending); too narrow to dock, the card itself, modal, after the summary");
            // The actions are the pause menu's own.
            var act=Body(eui,"private void ExpeditionEndingAct(");var pause=R("Runtime/NativeHomeMenu.cs");
            Check(act.Contains("if(!(Hosting||!Online)||LobbyReturnSeconds>0)return;if(!Online)afterMatch=OfflineLobby();RequestLobbyReturn();")&&pause.Contains("(Hosting||!Online)&&!returning,()=>{if(!Online)afterMatch=OfflineLobby();RequestLobbyReturn();"),"Return to Lobby: the pause menu's call and condition");
            Check(act.Contains("RequestStage(StageLoadKind.Return,()=>{if(Online)LeaveRoom();else ReturnToLobby();});")&&pause.Contains("RequestStage(StageLoadKind.Return,()=>{if(Online)LeaveRoom();else ReturnToLobby();});"),"Return to Main Menu: the pause menu's call");
            Check(eui.Contains("Host=Hosting,Offline=!Online,Returning=LobbyReturnSeconds>0,HostLost=arena!=null&&arena.Replica&&server==null"),"the client flags are the pause menu's");
            Check(Regex.Matches(eui,@"nativeAudio\?\.Cue\(").Count==2&&eui.Contains("nativeAudio?.Cue(lost?\"ui.round-lost\":\"ui.round-won\",lost?\"lose\":\"win\",null,.55f,mine:true);")&&qhud.Contains("nativeAudio?.Cue(\"pve.clear\",\"go\",null,.6f,mine:true);")&&NativeSoundKeys.All.Any(k=>k.Key=="pve.clear")&&NativeSoundKeys.All.Any(k=>k.Key=="ui.round-won")&&NativeSoundKeys.All.Any(k=>k.Key=="ui.round-lost"),"the stings use existing, registered cue keys (the PvE clear for the quota, the match end's for the ending)");
            // The DEV preview: a view tool for developers.
            var a=NativeDevActions.Find("preview-expedition-ui");Check(a!=null&&a.Kind==NativeDevKind.Presentation&&a.Tab==NativeDevTab.Previews,"DEV panel · Previews: a view tool");
            Check(dev.Contains("case \"preview-expedition-ui\":ExpeditionUiPreviewToggle();break;")&&dev.Contains("ExpeditionUiPreviewHud(W,Hh);")&&Body(dev,"private float DevPreviewsTab(").Contains("y=ExpeditionUiPreviewPanel(x,y,w);")&&eui.Contains("private bool ExpeditionUiPreviewShown=>expeditionUiPreview&&account!=null&&account.Developer;"),"the preview is wired, for developers only");
            Check(NativeExpeditionEndingFixtures.QuotaNames.Length==Enum.GetValues(typeof(NativeExpeditionEndingFixtures.Quota)).Length&&fix.Contains("MOCK"),"a name per quota fixture; labelled MOCK");
            foreach(NativeExpeditionEndingFixtures.Quota qf in Enum.GetValues(typeof(NativeExpeditionEndingFixtures.Quota))){var v=NativeExpeditionEndingFixtures.QuotaView(qf);Check(qf==NativeExpeditionEndingFixtures.Quota.Off?v==null:NativeExpeditionQuota.Of(v).Stage!=ExpeditionQuotaStage.None,"quota fixture "+qf+" shows the module");}
            // Metas and their GUIDs.
            string Guid(string f)=>Regex.Match(File.ReadAllText(f),@"guid:\s*([0-9a-f]{32})").Groups[1].Value;
            var mine=new[]{"Core/NativeExpeditionQuotaModel.cs","Core/NativeExpeditionEnding.cs","Core/NativeExpeditionEndingFixtures.cs","Runtime/NativeExpeditionQuotaHud.cs","Runtime/NativeExpeditionEndingHud.cs","Runtime/NativeExpeditionEndingUI.cs"}.Select(f=>Path.Combine(A,f+".meta")).ToArray();
            foreach(var f in mine)Check(File.Exists(f)&&Guid(f).Length==32,"Unity meta for "+Path.GetFileName(f));
            var own=new HashSet<string>(mine.Select(Guid));Check(own.Count==mine.Length,"the new metas' GUIDs are distinct");
            foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories))if(!mine.Contains(f))Check(!own.Contains(Guid(f)),"no other asset shares a new GUID ("+Path.GetFileName(f)+")");
            // No authority, network or account file knows the new presentation.
            var names=new Regex(@"\b(ExpeditionQuotaModel|NativeExpeditionQuota|ExpeditionEndingModel|NativeExpeditionEnding|ExpeditionEndingLayout|NativeExpeditionQuotaHud|NativeExpeditionEndingHud|NativeExpeditionEndingFixtures|ExpeditionEndKind)\b");
            var allowed=new HashSet<string>{"Core/NativeExpeditionQuotaModel.cs","Core/NativeExpeditionEnding.cs","Core/NativeExpeditionEndingFixtures.cs","Runtime/NativeExpeditionQuotaHud.cs","Runtime/NativeExpeditionEndingHud.cs","Runtime/NativeExpeditionEndingUI.cs","Runtime/NativeExpeditionLiveUI.cs","Runtime/NativeExpeditionHud.cs"};
            foreach(var file in Directory.GetFiles(A,"*.cs",SearchOption.AllDirectories)){string rel=Path.GetRelativePath(A,file).Replace('\\','/');if(names.IsMatch(File.ReadAllText(file)))Check(allowed.Contains(rel),"only presentation files name the quota module and the ending ("+rel+")");}
        }
    }
}
#endif
