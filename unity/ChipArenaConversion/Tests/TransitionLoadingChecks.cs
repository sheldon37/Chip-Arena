#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // Menu screen transitions and stage loading (Core NativeScreenTransition, NativeStageLoading, StagePreloadPlan,
    // NativeLoadingLayout, NativeStageSchematic, NativeStageLabels; Runtime NativeMenuTransitionUI, NativeStageLoadingUI,
    // NativeScreenTransitionHud, NativeStageLoadingHud, NativeStagePrewarm): the transition covers the screen before the swap,
    // swaps hidden and reveals; input is held no longer than the cover; the reduced variant does not move; the layout fits
    // every resolution and UI scale in the safe band; the loading state machine shows on stage entry, waits for the preload
    // and the game, fades, and never sticks; the preload covers every lazily built stage kit; the per-frame paths allocate
    // nothing; the runtime is wired where the menus navigate and the stages start.
    internal static class TransitionLoadingChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Transitions/loading: "+what);count++;}
        public static void Run(string root)
        {
            count=0;var R=new Func<string,string>(f=>File.ReadAllText(Path.Combine(root,f)));
            Timeline();Reduced();Retarget();
            Layout();
            Loading();Plan();
            var maps=Maps(root);Schematics(maps);Labels(maps);
            Allocation(maps);
            Wiring(R,root);
            Console.WriteLine("PASS transitions/loading: "+count+" checks (menu shutters cover before the swap, input held only while covered, reduced fade, layouts 720p-4K/ultrawide/UI scale, loading state machine never sticks, preload covers every stage kit, allocation-free, runtime wiring)");
        }

        // ---- the menu transition ------------------------------------------------------------------------------------------
        private static void Timeline()
        {
            var t=new NativeScreenTransition();float t0=10;
            Check(!t.Active(t0)&&!t.BlocksInput(t0)&&t.Coverage(t0)==0,"idle before it begins");
            Check(t.Begin(t0,false),"a fresh transition begins");
            Check(t.TotalTime>=.4f&&t.TotalTime<=.6f,"the full transition runs .4-.6 s ("+t.TotalTime+")");
            Check(t.CoverTime+t.HoldTime<=.3f,"input is held .3 s at most");
            Check(t.Phase(t0+.01f)==ScreenTransitionPhase.Cover&&!t.SwapDue(t0+.01f),"covering first, no swap yet");
            // Before the swap the screen is not yet covered and the new title is not shown.
            for(float s=t0;s<t.SwapAt-NativeScreenTransition.TitleLead;s+=.005f)Check(t.TitleAlpha(s)==0,"the destination title never shows on the old screen ("+(s-t0)+")");
            float prev=-1;for(float s=t0;s<t.SwapAt;s+=.004f){float c=t.Coverage(s);Check(c>=prev-1e-4f,"the cover only closes ("+(s-t0)+")");prev=c;}
            // At the swap and through the hold every band covers the whole width, at every chevron overshoot.
            foreach(float k in new[]{0f,.02f,.05f})for(float s=t.SwapAt;s<t.RevealAt;s+=.005f)for(int i=0;i<NativeScreenTransition.Bands;i++)
            {t.Band(i,s,k,out float l,out float r);Check(l<=1e-4f&&r>=1-1e-4f,"band "+i+" covers the width at the swap/hold (k "+k+", t "+(s-t0)+": "+l+".."+r+")");}
            Check(t.SwapDue(t.SwapAt)&&t.Coverage(t.SwapAt)>=1-1e-4f,"the swap is due exactly when covered");
            t.MarkSwapped();Check(!t.SwapDue(t.SwapAt+.01f),"the swap runs once");
            for(float s=t0;s<t.RevealAt;s+=.005f)Check(t.BlocksInput(s),"input held while covering and covered ("+(s-t0)+")");
            for(float s=t.RevealAt;s<t.EndAt+.2f;s+=.005f)Check(!t.BlocksInput(s),"input free through the reveal ("+(s-t0)+")");
            prev=2;for(float s=t.RevealAt;s<t.EndAt;s+=.004f){float c=t.Coverage(s);Check(c<=prev+1e-4f,"the reveal only opens");prev=c;}
            Check(t.Coverage(t.EndAt-.001f)<.05f&&!t.Active(t.EndAt),"the reveal ends open and idle");
            // The lead edges: in from the right leading left, out to the left leading right; the title rides the middle band.
            Check(t.LeadsLeft(t0+.05f)&&!t.LeadsLeft(t.RevealAt+.05f),"forward: lead left in, right out");
            t.Band(0,t0+.06f,0,out float l0,out _);t.Band(NativeScreenTransition.Bands-1,t0+.06f,0,out float l6,out _);Check(l0<l6,"top band first: a diagonal front");
            Check(Math.Abs(t.Shift(3,t.SwapAt,.03f))<1e-4f&&t.Shift(3,t0+.02f,.03f)>.2f,"the middle band's content slides in with it");
            Check(t.Glitch(t.SwapAt)>.9f&&t.Glitch(t0)==0&&t.Glitch(t.EndAt-.01f)==0,"the glitch bursts at the swap only");
            Check(t.Scan(t.SwapAt+.01f)>=0&&t.Scan(t0+.05f)<0,"the scan line runs in the hold");
            // Back: mirrored spans.
            var b=new NativeScreenTransition();b.Begin(t0,false,true);var f=new NativeScreenTransition();f.Begin(t0,false,false);
            for(float s=t0;s<f.EndAt;s+=.02f)for(int i=0;i<7;i++){f.Band(i,s,.03f,out float fl,out float fr);b.Band(i,s,.03f,out float bl,out float br);Check(Math.Abs(bl-(1-fr))<1e-4f&&Math.Abs(br-(1-fl))<1e-4f,"back runs mirrored");}
            // The band rows tile the canvas with a seam overlap; the chevron stays inside the chrome's 96 px corner bake.
            foreach(float h in new[]{600f,900f})foreach(float S in new[]{.6f,1.2f,1.6f,2.4f})
            {
                float y=0;for(int i=0;i<7;i++){NativeScreenTransition.BandRow(i,h,1/S,out float top,out float bh);Check(top<=y+1e-3f&&top+bh>=Math.Min(h,(i+1)*h/7)-1e-3f,"rows tile the canvas");y=top+bh;}
                Check(y>=h-1e-3f,"rows reach the foot");float cut=NativeScreenTransition.Chamfer(h/7,S);Check(cut*S<=96.01f&&cut>0&&cut<=h/7*.41f,"chevron within 96 px");
            }
        }
        private static void Reduced()
        {
            var t=new NativeScreenTransition();float t0=3;t.Begin(t0,true);
            Check(t.TotalTime<=.35f&&t.CoverTime+t.HoldTime<=.16f,"reduced: shorter and holds input less");
            float prev=-1;for(float s=t0;s<t.EndAt;s+=.003f)
            {
                Check(t.Glitch(s)==0&&t.Scan(s)<0&&t.Shift(3,s,.03f)==0,"reduced: no glitch, scan or motion");
                for(int i=0;i<7;i++){t.Band(i,s,.03f,out float l,out float r);Check(l==0&&r==1,"reduced: no bands sliding");}
                float a=t.Fade(s);if(s<t.SwapAt)Check(a>=prev-1e-4f,"reduced: fades up");if(s>=t.SwapAt&&s<t.RevealAt)Check(a==1,"reduced: covered for the swap");prev=a;
            }
            Check(t.TitleAlpha(t0+.01f)==0,"reduced: no title before the swap");
        }
        private static void Retarget()
        {
            var t=new NativeScreenTransition();t.Begin(0,false);float at=t.StartedAt;
            Check(!t.Begin(.05f,false)&&t.StartedAt==at,"a second navigation while covering only retargets");
            t.MarkSwapped();float hold=t.SwapAt+.02f;Check(t.Begin(hold,false)&&t.SwapDue(hold)&&t.Coverage(hold)>=1-1e-4f,"navigating in the hold stays covered and swaps again");
            var r=new NativeScreenTransition();r.Begin(0,false);r.MarkSwapped();float mid=r.RevealAt+.12f,before=r.Coverage(mid);bool back=r.Back;
            Check(r.Begin(mid,false)&&Math.Abs(r.Coverage(mid)-before)<.3f&&r.Back!=back,"navigating in the reveal closes again from the side the bands are on");
            r.Cancel();Check(!r.Active(mid),"cancel stops it");
        }

        // ---- the loading screen's layout ----------------------------------------------------------------------------------
        private static void Layout()
        {
            var cases=new (int w,int h,float scale,bool scan)[]{(1920,1080,1,true),(2560,1440,1,true),(3440,1440,1,true),(3840,2160,1,true),(2560,1080,1,true),(1280,720,1,true),(1920,1080,1.5f,true),(1280,720,1.6f,true),(1280,720,2f,false),(1600,1200,1,true),(1024,768,1,true),(1920,1080,2.4f,false)};
            foreach(var c in cases)
            {
                var ui=NativeUiMotion.Measure(c.w,c.h,c.scale);var l=NativeLoadingLayout.Measure(ui.Width,ui.Height,ui.SafeX,ui.SafeWidth);string at=c.w+"x"+c.h+" @"+c.scale;
                Check(l.Scan==c.scan,at+": the stage scan "+(c.scan?"shows":"drops")+" (safe "+ui.SafeWidth+")");
                foreach(var box in new[]{l.Text,l.Bar,l.Label,l.Tip,l.Spinner}.Concat(l.Scan?new[]{l.Panel}:new LoadingBox[0]))
                {Check(box.Inside(ui.Width,ui.Height),at+": a box inside the canvas");Check(box.X>=ui.SafeX-.01f&&box.Right<=ui.SafeX+ui.SafeWidth+.01f,at+": inside the safe band");}
                if(l.Scan){Check(!l.Text.Overlaps(l.Panel),at+": the title block clears the scan");Check(l.Panel.Bottom<=l.Label.Y,at+": the scan above the progress row");Check(l.Panel.W>=300&&l.Panel.H>=180,at+": a readable scan ("+l.Panel.W+"x"+l.Panel.H+")");}
                Check(l.Label.Bottom<=l.Bar.Y&&l.Bar.Bottom<=l.Tip.Y&&l.Tip.Bottom<=ui.Height-28,at+": label, bar and tip stacked at the foot, clear of the frame rule");
                Check(l.TitleSize>=34&&l.TitleSize<=76&&l.Eyebrow>=l.Text.Y&&l.Party+20<=l.Label.Y,at+": the title block fits above the foot");
                Check(l.Text.W>=200,at+": a title column ("+l.Text.W+")");
            }
        }

        // ---- the loading state machine --------------------------------------------------------------------------------------
        private static StageLoadSignals Sig(bool stage=true,bool ready=false,bool waiting=false,bool live=false,bool failed=false,float budget=NativeStageLoading.ReadyTimeout)
            =>new StageLoadSignals{Stage=stage,Ready=ready,Waiting=waiting,Live=live,Failed=failed,Budget=budget};
        private static float Run(NativeStageLoading m,Func<float,StageLoadSignals> at,float work,float limit,float dt=1/60f)
        {
            float t=0;float shown=0;while(m.Visible&&t<limit){m.Step(dt,at(t),work);Check(m.Shown>=shown-1e-5f,"the progress shown never goes back");shown=m.Shown;t+=dt;}return t;
        }
        private static void Loading()
        {
            // A requested start: cover, the action once covered, hold for the preload, then the minimum, then reveal.
            var m=new NativeStageLoading();m.Request(StageLoadKind.Match,false);
            Check(m.Phase==StageLoadPhase.Cover&&m.BlocksInput&&!m.TakeAction(),"a request covers first; no action before it is covered");
            float t=0;while(m.Phase==StageLoadPhase.Cover){m.Step(1/60f,Sig(stage:false),0);t+=1/60f;}
            Check(Math.Abs(t-NativeStageLoading.CoverSeconds)<.03f&&m.Covered,"covered after the cover time");
            Check(m.TakeAction()&&!m.TakeAction(),"the action runs once, under the cover");
            for(int i=0;i<120;i++)m.Step(1/60f,Sig(ready:false,budget:6),.4f);Check(m.Covered,"holds while the preload runs");
            m.Step(1/60f,Sig(ready:true,budget:6),1);Check(m.Phase==StageLoadPhase.Reveal&&m.Reason==NativeStageLoading.Ready&&!m.BlocksInput,"reveals once ready (input free)");
            Check(Math.Abs(m.RevealTime-NativeStageLoading.RevealSeconds)<1e-4f,"a normal reveal");
            Run(m,_=>Sig(ready:true),1,2);Check(!m.Visible&&m.Shown>=1-1e-4f,"ends idle with the bar full");
            // Minimum hold: ready at once still holds MinHold (no flash).
            m=new NativeStageLoading();m.Cover(StageLoadKind.Online,false);Check(m.Phase==StageLoadPhase.Hold&&!m.Requested,"a stage that appeared covers at once");
            float held=0;while(m.Covered&&held<2){m.Step(1/60f,Sig(ready:true),1);held+=1/60f;}Check(held>=NativeStageLoading.MinHold-.02f&&held<NativeStageLoading.MinHold+.05f,"holds the minimum, then opens ("+held+")");
            // Live play: open at once, fast.
            m=new NativeStageLoading();m.Cover(StageLoadKind.Area,false);m.Step(1/60f,Sig(live:true),.1f);Check(m.Phase==StageLoadPhase.Reveal&&m.FastReveal&&m.Reason==NativeStageLoading.Live&&m.RevealTime<=.21f,"live play opens at once, fast");
            // A refused start: open at once.
            m=new NativeStageLoading();m.Request(StageLoadKind.Match,false);Run(m,_=>Sig(stage:false),0,NativeStageLoading.CoverSeconds+.01f);m.TakeAction();m.Step(1/60f,Sig(stage:false,failed:true),0);
            Check(m.Phase==StageLoadPhase.Reveal&&m.Reason==NativeStageLoading.Failed,"a refused start opens at once");
            // A game hold: past the ready timeout, up to the wait timeout, then open (never stuck).
            m=new NativeStageLoading();m.Cover(StageLoadKind.Area,false);float w=0;while(m.Covered&&w<40){m.Step(.05f,Sig(ready:true,waiting:true),1);w+=.05f;}
            Check(w>=NativeStageLoading.WaitTimeout-.06f&&w<=NativeStageLoading.WaitTimeout+.1f&&m.Reason==NativeStageLoading.Waited,"a game hold holds up to its timeout, then opens ("+w+")");
            m=new NativeStageLoading();m.Cover(StageLoadKind.Area,false);w=0;bool waiting=true;while(m.Covered&&w<5){if(w>1.5f)waiting=false;m.Step(.05f,Sig(ready:true,waiting:waiting),1);w+=.05f;}
            Check(w>1.5f&&w<1.7f&&m.Reason==NativeStageLoading.Ready,"opens as soon as the game releases its hold");
            // A slow preload: never past the clock's budget (countdown 1 s, intro 2 s, no clock 6 s); a missing stage 4 s.
            foreach(var (phase,limit) in new[]{((NativePhase?)NativePhase.Countdown,1f),(NativePhase.Intro,2f),(NativePhase.Draft,2f),(null,NativeStageLoading.ReadyTimeout)})
            {
                m=new NativeStageLoading();m.Cover(StageLoadKind.Match,false);float budget=NativeStageLoading.Budget(phase);w=0;while(m.Covered&&w<20){m.Step(.05f,Sig(ready:false,budget:budget),.5f);w+=.05f;}
                Check(Math.Abs(w-Math.Max(NativeStageLoading.MinHold,limit))<.11f&&m.Reason==NativeStageLoading.Timeout,"a slow preload opens at the "+(phase?.ToString()??"no-clock")+" budget ("+w+")");
            }
            Check(NativeStageLoading.Budget(NativePhase.Fight)==0,"no budget in the fight");
            m=new NativeStageLoading();m.Cover(StageLoadKind.Return,false);w=0;while(m.Covered&&w<20){m.Step(.05f,Sig(stage:false,ready:true),1);w+=.05f;}
            Check(Math.Abs(w-NativeStageLoading.StageTimeout)<.11f&&m.Reason==NativeStageLoading.Missing,"a stage that never comes opens at its timeout");
            // A new stage while opening: covered again at once, a new load.
            m=new NativeStageLoading();m.Cover(StageLoadKind.Match,false);int serial=m.Serial;m.Step(.25f,Sig(ready:true),1);m.Step(.25f,Sig(ready:true),1);Check(m.Phase==StageLoadPhase.Reveal,"opening");
            m.Step(.1f,Sig(ready:true),1);m.Cover(StageLoadKind.Change,false);Check(m.Covered&&m.Serial==serial+1&&m.Kind==StageLoadKind.Change,"a new stage while opening covers again as a new load");
            // A request while covered merges into the same screen.
            m.Request(StageLoadKind.Return,false);Check(m.Serial==serial+1&&m.ActionPending&&m.Kind==StageLoadKind.Return,"a request while covered merges, its action pending");
            m.Step(.1f,Sig(ready:true),1);Check(m.Covered,"never opens with an action pending");Check(m.TakeAction(),"the merged action runs");
            // Reduced: shorter cover and reveal.
            m=new NativeStageLoading();m.Request(StageLoadKind.Boss,true);Check(m.CoverTime<NativeStageLoading.CoverSeconds&&m.Reduced,"reduced: a shorter cover");
            // Coverage: 0 idle, eased in, 1 covered, eased out.
            m=new NativeStageLoading();Check(m.Coverage==0,"idle uncovered");m.Request(StageLoadKind.Match,false);m.Step(.1f,Sig(),0);Check(m.Coverage>0&&m.Coverage<1,"part covered while closing");
        }
        private static void Plan()
        {
            var p=new StagePreloadPlan();p.Reset(true);Check(StagePreloadPlan.Labels.Length==6&&StagePreloadPlan.Weights.Length==6&&Math.Abs(StagePreloadPlan.Weights.Sum()-1)<1e-4f,"six steps, weights sum to 1");
            var order=new List<StagePreloadStep>();float prev=-1;
            while(!p.Done){order.Add(p.Step);Check(p.Work>=prev-1e-5f,"work only grows");prev=p.Work;p.Set(.5f);Check(p.Work>=prev,"partial adds");prev=p.Work;p.Next();}
            Check(order.SequenceEqual(new[]{StagePreloadStep.Stage,StagePreloadStep.Kit,StagePreloadStep.Frames,StagePreloadStep.Shaders,StagePreloadStep.Audio}),"a stage preload runs every step in order");
            Check(p.Work==1&&p.Label=="STAGE READY","done is full");
            p.Reset(false);order.Clear();while(!p.Done){order.Add(p.Step);p.Next();}
            Check(order.SequenceEqual(new[]{StagePreloadStep.Stage,StagePreloadStep.Frames}),"a return skips the kit, shaders and audio");
            p.Reset(false);p.Next();p.Set(.5f);Check(Math.Abs(p.Work-(StagePreloadPlan.Weights[0]+.5f*StagePreloadPlan.Weights[2])/(StagePreloadPlan.Weights[0]+StagePreloadPlan.Weights[2]+StagePreloadPlan.Weights[5]))<1e-4f,"skipped shares go to the kept steps");
            p.Reset(true);p.Next();p.Skip(StagePreloadStep.Kit);Check(p.Step==StagePreloadStep.Frames,"skipping the current step moves on");
            p.Reset(true);p.Skip(StagePreloadStep.Shaders);p.Next();p.Next();p.Next();Check(p.Step==StagePreloadStep.Audio,"a skipped step is passed over");
        }

        // ---- the stage scan ---------------------------------------------------------------------------------------------------
        private static List<ContentMap> Maps(string root)
        {
            var o=new JsonSerializerOptions{IncludeFields=true};var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
            var unity=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),o);
            var list=content.maps.Where(m=>!unity.maps.Any(u=>u.id==m.id)).ToList();list.AddRange(unity.maps);return list;
        }
        private static void Schematics(List<ContentMap> maps)
        {
            Check(maps.Count>=20&&maps.Any(m=>m.id=="expedition-ward-zero")&&maps.Any(m=>m.id=="cybercity"),"every map ("+maps.Count+")");
            foreach(var m in maps)
            {
                var s=NativeStageSchematic.Build(m);Check(s!=null&&s.Count>0&&s.Count<=NativeStageSchematic.Max,m.id+" scans ("+(s?.Count??0)+")");
                Check(s.Total==s.Count||s.Total>NativeStageSchematic.Max,m.id+": keeps every block under the cap");
                Check(s.Label==s.Total+" BLOCKS",m.id+": its label");
                Check(s.Aspect>.3f&&s.Aspect<3,m.id+": a sane plan aspect ("+s.Aspect+")");
                for(int i=0;i<s.Count;i++)
                {
                    float u=s.Box[i*4],v=s.Box[i*4+1],w=s.Box[i*4+2],h=s.Box[i*4+3];
                    if(!(u>=-1e-4f&&v>=-1e-4f&&w>0&&h>0&&u+w<=1+1e-4f&&v+h<=1+1e-4f&&s.Level[i]>=0&&s.Level[i]<=1))Check(false,m.id+": block "+i+" in the plan");
                }
                count++;
                bool plates=true;for(int i=0;i<s.Count;i++){if(s.Kind[i]!=NativeStageSchematic.Plate)plates=false;else if(!plates)Check(false,m.id+": plates draw first");}
                Check(s.Spawns.All(x=>x>=0&&x<=1)&&s.Spawns.Length%2==0&&s.Hazards.Length%2==0,m.id+": spawns and hazards in the plan");
                var again=NativeStageSchematic.Build(m);Check(again.Box.SequenceEqual(s.Box)&&again.Level.SequenceEqual(s.Level)&&again.Kind.SequenceEqual(s.Kind),m.id+": builds the same twice");
                Check(ReferenceEquals(NativeStageSchematic.For(m),NativeStageSchematic.For(m)),m.id+": cached per map");
            }
            var ward=maps.First(m=>m.id=="siren"&&m.blocks.Length>500);var ws=NativeStageSchematic.For(ward);Check(ws.Total==ward.blocks.Length&&ws.Spawns.Length>0,"Ward Zero scans all "+ward.blocks.Length+" blocks with its spawns");
            Check(ws.Kind.Any(k=>k==NativeStageSchematic.Cover)&&ws.Kind.Any(k=>k==NativeStageSchematic.Plate),"Ward Zero: cover and slab kinds");
            Check(NativeStageSchematic.Build(new ContentMap{id="empty",blocks=new ContentBlock[0]})==null&&NativeStageSchematic.For(null)==null,"no blocks, no scan");
        }
        private static void Labels(List<ContentMap> maps)
        {
            foreach(StageLoadKind k in Enum.GetValues(typeof(StageLoadKind))){Check(!string.IsNullOrEmpty(NativeStageLabels.Eyebrow(k))&&!string.IsNullOrEmpty(NativeStageLabels.Mode(k)),k+": eyebrow and mode");
                for(int s=0;s<12;s++){var tip=NativeStageLabels.Tip(k,s);Check(k==StageLoadKind.Return?tip==null:tip!=null&&tip.Length<=80,k+": its tips (and none on a return)");Check(tip==NativeStageLabels.Tip(k,s),"a load's tip is fixed");}}
            Check(NativeStageLabels.Tip(StageLoadKind.Boss,1)!=NativeStageLabels.Tip(StageLoadKind.Boss,2),"tips change between loads");
            var all=NativeStageLabels.GeneralTips.Concat(NativeStageLabels.BossTips).Concat(NativeStageLabels.ExpeditionTips).Concat(NativeStageLabels.SandboxTips).ToArray();
            Check(all.All(t=>t.Length<=80&&!t.Contains("  ")),"tips fit the line ("+all.Max(t=>t.Length)+")");
            Check(NativeStageLabels.StageName(maps.First(m=>m.id=="expedition-ward-zero"))=="WARD ZERO"&&NativeStageLabels.StageName(maps.First(m=>m.id=="factory"))=="FACTORY","stage names: capitals, (Expedition) dropped");
            Check(ReferenceEquals(NativeStageLabels.StageName(maps[0]),NativeStageLabels.StageName(maps[0])),"names cached");
            Check(NativeStageLabels.Percent(0)=="0%"&&NativeStageLabels.Percent(1)=="100%"&&NativeStageLabels.Percent(.505f)=="50%"&&NativeStageLabels.Percent(float.NaN)=="0%"&&NativeStageLabels.Percent(7)=="100%","percent strings");
            Check(NativeStageLabels.Count(3,4)=="3 / 4"&&ReferenceEquals(NativeStageLabels.Count(3,4),NativeStageLabels.Count(3,4)),"counts cached");
            var v=new StageLoadView();v.Begin(StageLoadKind.Boss,3);Check(v.Title==null&&v.Eyebrow=="ENTERING STAGE"&&v.Mode=="PVE · BOSS FIGHT"&&v.Tip!=null,"the view before the stage");
            var ward=maps.First(m=>m.id=="siren"&&m.blocks.Length>500);v.Stage(ward,5);Check(v.Title=="WARD ZERO"&&v.TitleAt==5&&v.Scan!=null&&v.Count=="616 BLOCKS"&&v.Tag!=null&&v.Accent==(ward.accent&0xffffff),"the view knows the stage");
            v.Stage(ward,9);Check(v.TitleAt==5,"the same stage keeps its title's moment");
            v.Stage(ward,9,"WARD ZERO");Check(v.TitleAt==5,"an area name");v.Returning(true,10);Check(v.Title==NativeStageLabels.ReturnLobby&&v.Tip==null&&v.Scan==null,"a return to the lobby");
        }

        // ---- per frame, nothing allocates -------------------------------------------------------------------------------------
        private static void Allocation(List<ContentMap> maps)
        {
            var t=new NativeScreenTransition();var m=new NativeStageLoading();var p=new StagePreloadPlan();var ward=maps.First(x=>x.id=="siren"&&x.blocks.Length>500);
            var sch=NativeStageSchematic.For(ward);var v=new StageLoadView();v.Begin(StageLoadKind.Boss,1);v.Stage(ward,0);
            t.Begin(0,false);m.Request(StageLoadKind.Match,false);p.Reset(true);var sig=Sig(ready:false,budget:2);float sink=0;
            for(int warm=0;warm<2;warm++)
            {
                long before=GC.GetAllocatedBytesForCurrentThread();
                for(int f=0;f<600;f++)
                {
                    float now=f/600f*.6f;sink+=t.Coverage(now)+t.Fade(now)+t.TitleAlpha(now)+t.Glitch(now)+t.Scan(now)+t.Shift(3,now,.03f);t.SwapDue(now);t.BlocksInput(now);
                    for(int i=0;i<7;i++){t.Band(i,now,.03f,out float l,out float r);sink+=l+r;NativeScreenTransition.Span(i,now,.05f,f%2==0,.03f,false,out l,out r);sink+=NativeScreenTransition.ShiftOf(i,now,.05f,f%2==1,.03f,false);}
                    m.Step(1/60f,sig,p.Work);sink+=m.Coverage+m.Shown;p.Set(f/600f);
                    var l0=NativeLoadingLayout.Measure(2150,900,275,1600);sink+=l0.TitleSize;
                    sink+=NativeStageLabels.Percent(f/600f).Length+NativeStageLabels.Count(f%9,8).Length;sink+=NativeStageLoading.Budget(NativePhase.Countdown);
                    for(int i=0;i<sch.Count;i++)sink+=sch.Box[i*4]+sch.Level[i];sink+=v.Title.Length;
                }
                long used=GC.GetAllocatedBytesForCurrentThread()-before;if(warm==1)Check(used==0,"the per-frame timeline, state machine, layout, labels and scan reads allocate nothing ("+used+" bytes)");
            }
            Check(!float.IsNaN(sink),"sink");
        }

        // ---- runtime wiring ---------------------------------------------------------------------------------------------------
        private static string Body(string text,string signature)
        {
            int i=text.IndexOf(signature,StringComparison.Ordinal);if(i<0)return null;int b=text.IndexOf('{',i);if(b<0)return null;int depth=0;
            for(int j=b;j<text.Length;j++){if(text[j]=='{')depth++;else if(text[j]=='}'&&--depth==0)return text.Substring(i,j-i+1);}return null;
        }
        private static void Wiring(Func<string,string> R,string root)
        {
            var home=R("Assets/ChipArena/Runtime/NativeHomeMenu.cs");var go=Body(home,"private void Go(MenuScreen screen,bool sandbox)");
            Check(go!=null&&go.Contains("ShowScreen(screen,sandbox)")&&!go.Contains("menuScreen=screen"),"Go shows the screen through the transition");
            Check(go.Contains("var from=MenuTarget")&&go.Contains("ApplySettings();Save();")&&go.Contains("CloseNetwork();")&&go.Contains("LockerEnter();"),"Go keeps its side effects, now, from where the menu is going");
            var hm=Body(home,"private void HomeMenu()");Check(hm.Contains("MenuTransitionFrame(now,seen)")&&hm.Contains("MenuTransitionGui(now)")&&hm.Contains("homeSeen=now")&&hm.IndexOf("MenuTransitionFrame",StringComparison.Ordinal)<hm.IndexOf("switch(menuScreen)",StringComparison.Ordinal),"the home menu swaps before it draws and draws the shutters last");
            Check(hm.Contains("var target=MenuTarget;")&&hm.Contains("target!=MenuScreen.Lobby")&&hm.Contains("target==MenuScreen.Lobby"),"room routing reads where the menu is going");
            Check(home.Contains("Go(MenuScreen.Play,true)")&&!home.Contains("Go(MenuScreen.Play);playSandbox=true;"),"the sandbox link opens Play as the sandbox through the transition");
            Check(Body(home,"private void MenuBack()").Contains("MenuTarget"),"Escape steps back from where the menu is going");
            var mt=R("Assets/ChipArena/Runtime/NativeMenuTransitionUI.cs");
            var show=Body(mt,"private void ShowScreen(MenuScreen screen,bool sandbox)");Check(show.Contains("arena==null")&&show.Contains("!stageLoad.Visible")&&show.Contains("previewArena==null")&&show.Contains("menuTransition.Begin(now,prefs.reducedEffects,screen==MenuScreen.Home)"),"transitions only between menu screens, back toward home, reduced effects honoured");
            var frame=Body(mt,"private void MenuTransitionFrame(float now,bool seen)");Check(frame.Contains("EventType.Layout")&&frame.Contains("SwapDue(now)")&&frame.Contains("BlockGuiInput()"),"the swap runs on a layout event; input held while covered");
            var apply=Body(mt,"private void ApplyScreen(MenuScreen screen,bool sandbox)");Check(apply.Contains("menuScreen=screen;menuScreenAt=Time.unscaledTime;screenScroll=Vector2.zero;screenContent=400;GUIUtility.keyboardControl=0;")&&apply.Contains("playSandbox=sandbox"),"the swap is Go's old screen change");
            Check(Regex.Matches(mt,"MenuTitles=\\{([^}]*)\\}").Count==1&&Regex.Match(mt,"MenuTitles=\\{([^}]*)\\}").Groups[1].Value.Split(',').Length==Regex.Match(home,"enum MenuScreen\\{([^}]*)\\}").Groups[1].Value.Split(',').Length,"a title for every menu screen");
            // Stage starts and returns go through the loading screen.
            // The boss fight and DEV Expedition M1 start from the Expeditions screen (NativeExpeditionsMenu.cs, owner 2026-10-15).
            var expeditions=R("Assets/ChipArena/Runtime/NativeExpeditionsMenu.cs");
            Check(home.Contains("RequestStage(sandboxWanted?StageLoadKind.Sandbox:StageLoadKind.Match,StartMatch)")&&expeditions.Contains("RequestStage(StageLoadKind.Boss,StartMatch)")&&expeditions.Contains("RequestStage(StageLoadKind.Expedition,StartExpeditionM1)")&&home.Contains("private void BossScreen(float age)=>ExpeditionsScreen(age);"),"Play, Wave Defense (the boss fight) and DEV Expedition M1 start under the loading screen");
            Check(home.Contains("RequestStage(StageKindOf(arena),StartMatch)")&&home.Contains("RequestStage(StageLoadKind.Return,"),"restart and return to the main menu under the loading screen");
            Check(R("Assets/ChipArena/Runtime/NativeLobbyUI.cs").Contains("RequestStage(prefs.pve?StageLoadKind.Boss:StageLoadKind.Online,StartMatch)"),"the room's Start under the loading screen");
            var cg=R("Assets/ChipArena/Runtime/ConversionGame.cs");
            var ui=R("Assets/ChipArena/Runtime/NativeStageLoadingUI.cs");
            Check(Regex.IsMatch(cg,@"StageInputGate\(\);GuiFrame\(\);.*ExpeditionTransferGui\(\);StageLoadingGui\(\);"),"OnGUI: input gate before every screen, the loading screen over all of them");
            Check(cg.Contains("try{try{LateFrame();DamageScreen();GlitchScreen();}finally{FinishHitchFrame(HitchMs(at));}}finally{StageLoadingStep();}")&&ui.Contains("catch(Exception e){Debug.LogException(e);stageLoad.Cancel();")&&cg.Contains("bool escape=Input.GetKeyDown(KeyCode.Escape)&&!StageInputHeld;")&&!Regex.IsMatch(cg,@"if\(Input\.GetKeyDown\(KeyCode\.Escape\)"),"LateUpdate steps the loading screen; Escape waits while it is closed");
            Check(cg.Contains("StageLoadingRelease();"),"released with the game");
            Check(Body(ui,"private void StageObserve()").Contains("!ReferenceEquals(a,stageSeen)")&&Body(ui,"private void StageObserve()").Contains("map,stageSeenMap")&&Body(ui,"private void StageObserve()").Contains("areaBarrier"),"every stage change is observed: a new arena, a map swap, an area barrier");
            var frm=Body(ui,"private void StageLoadingFrame()");Check(frm.IndexOf("TakeAction()",StringComparison.Ordinal)<frm.IndexOf("StageObserve()",StringComparison.Ordinal)&&frm.Contains("stageLoad.Covered&&!stageLoad.ActionPending)StagePreload()"),"the action runs under the cover; the preload only behind the closed screen");
            var sig=Body(ui,"private StageLoadSignals StageSignals()");Check(sig.Contains("StageGate.Waiting")&&sig.Contains("areaBarrier?.active")&&sig.Contains("NativePhase.Fight")&&sig.Contains("NativeStageLoading.Budget(")&&sig.Contains("ReferenceEquals(arena,stageFrom)"),"signals: the gate, the barrier, live play, the clock's budget, a refused start");
            Check(ui.Contains("internal StageReadiness StagePresentation")&&ui.Contains("internal bool StagePresentationReady")&&ui.Contains("internal readonly StageLoadGate StageGate"),"the readiness and gate API for the network side");
            Check(R("Assets/ChipArena/Runtime/NativeExpeditionStatesPresentation.cs").Contains("||StageLoadingShown)return;"),"the Expedition transfer screen stands behind the loading screen");
            // The preload covers every lazily built stage kit SourceWorld draws.
            var world=Body(R("Assets/ChipArena/Runtime/NativeSourcePresentation.cs"),"private void SourceWorld(NativeArena arena)");var pre=R("Assets/ChipArena/Runtime/NativeStagePrewarm.cs");var warm=Body(pre,"public int PrewarmStage(NativeArena arena)");
            var needs=new Dictionary<string,string[]>{["FactoryScenery"]=new[]{"factoryStage.Prepare(arena)"},["BrickfallScenery"]=new[]{"EnsureBrickfall()"},["CyberCityScenery"]=new[]{"EnsureCyberCity()","MetroRebind(arena)"},
                ["ExpeditionCityScenery"]=new[]{"EnsureExpeditionCity()","ExpeditionCityBuild(map)"},["WardZeroScenery"]=new[]{"EnsureWardZero()","WardZeroBuild(map)"},["IronworksScenery"]=new[]{"EnsureIronworks()","IronworksBuild(map)"},["StationScenery"]=new[]{"EnsureStation()","StationBuild(map)"},["StationBossScenery"]=new[]{"EnsureStationBoss()"},["HazardKitWorld"]=new[]{"EnsureHazardKit()"}};
            foreach(Match call in Regex.Matches(world,@"\b([A-Z][A-Za-z]+(Scenery|KitWorld))\(arena\)"))
            {string n=call.Groups[1].Value;Check(needs.ContainsKey(n),"SourceWorld's "+n+" has a prewarm entry");foreach(var tok in needs[n])Check(warm.Contains(tok),n+" prewarms through "+tok);}
            var fac=Body(R("Assets/ChipArena/Runtime/NativeFactoryPresentation.cs"),"public void Render(NativeArena arena,float now,Camera camera,float quality,bool low,bool instancing,NativeDustCoat coat)");var prep=Body(pre,"public bool Prepare(NativeArena arena)");
            Check(fac.Contains("Pack(NativeFactoryKit.Build(map.blocks,hz,looks))")&&prep.Contains("Pack(NativeFactoryKit.Build(map.blocks,hz,looks))")&&prep.Contains("!ReferenceEquals(map,kitMap)"),"the Factory prepares its kit as its first frame does, once");
            var sw=Body(pre,"public void Step(Camera reference,int fog,int budget)");Check(sw.Contains("Resources.LoadAll<Shader>(\"\")")&&sw.Contains("Graphics.DrawMesh(")&&sw.Contains("Graphics.DrawMeshInstanced(")&&sw.Contains("camera.Render()")&&sw.Contains("isSupported"),"the shader warm-up draws every Resources shader plain and instanced into its hidden camera");
            Check(pre.Contains("cullingMask=1<<Layer")&&pre.Contains("targetTexture=target")&&pre.Contains("Layer=31"),"the warm-up never reaches the screen");
            var shaders=Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Resources"),"*.shader");Check(shaders.Length>=40,"Resources shaders to warm ("+shaders.Length+")");
            var instancing=shaders.Where(f=>File.ReadAllText(f).Contains("multi_compile_instancing")).Select(f=>Regex.Match(File.ReadAllText(f),"Shader\\s*\"([^\"]+)\"").Groups[1].Value).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            Check(instancing.SequenceEqual(StagePreloadPlan.InstancedShaders.OrderBy(x=>x,StringComparer.Ordinal)),"the warm-up's instanced shaders are exactly the instancing ones ("+string.Join(",",instancing)+")");
            Check(sw.Contains("StagePreloadPlan.Instanced(s.name)")&&sw.Contains("enableInstancing=false")&&sw.Contains("enableInstancing=true"),"plain for every shader, instanced only where the variant exists");
            var audio=R("Assets/ChipArena/Runtime/NativeEffectsAudio.cs");Check(audio.Contains("public int PreloadPending")&&audio.Contains("public int Preload(int count)")&&ui.Contains("nativeAudio.Preload(24)"),"the effects bank drains behind the screen");
            // Draw paths: constants only (no concatenation, formatting, arrays or lists per frame).
            foreach(var f in new[]{"NativeScreenTransitionHud.cs","NativeStageLoadingHud.cs","NativeMenuTransitionUI.cs"})
            {
                var text=R("Assets/ChipArena/Runtime/"+f);var code=string.Join("\n",text.Split('\n').Where(l=>!l.TrimStart().StartsWith("//")&&!l.TrimStart().StartsWith("///")));
                Check(!Regex.IsMatch(code,"\\+\\s*\"|\"\\s*\\+|\\$\"|ToString\\(|string\\.Format|new List|Substring\\(|new\\[\\]"),f+" builds no strings or arrays per frame");
            }
            Check(!Regex.IsMatch(Body(ui,"private void StageLoadingGui()"),"\\+\\s*\"|\"\\s*\\+|\\$\"|ToString\\("),"the loading screen's GUI pass builds no strings");
            // Metas: every new file has one, with a unique guid.
            var metas=Directory.GetFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories);var guids=new Dictionary<string,string>();
            foreach(var m in metas){var g=Regex.Match(File.ReadAllText(m),"guid: ([0-9a-f]{32})");if(!g.Success)continue;Check(!guids.ContainsKey(g.Value)||guids[g.Value]==m,"unique guid "+m);guids[g.Value]=m;}
            foreach(var f in new[]{"Core/NativeScreenTransition.cs","Core/NativeStageLoading.cs","Core/NativeStageSchematic.cs","Runtime/NativeScreenTransitionHud.cs","Runtime/NativeStageLoadingHud.cs","Runtime/NativeStageLoadingUI.cs","Runtime/NativeMenuTransitionUI.cs","Runtime/NativeStagePrewarm.cs"})
                Check(File.Exists(Path.Combine(root,"Assets/ChipArena/"+f+".meta")),f+" has its meta");
        }
    }
}
#endif
