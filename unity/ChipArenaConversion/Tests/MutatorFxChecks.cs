using System;
using System.IO;
using System.Linq;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Expedition mutators' looks (Claude, 2026-11-02; owner: "Yes, start them", Astra's handoff in docs/EXPEDITION-DEPLOYMENT.md):
    // STORM's warning and strike, the blasts, BLOATED's gas, ROULETTE's glints (Core NativeMutatorFx), DARKNESS, WHITEOUT, BIG
    // HEADS and the station's payout (Core NativeMutatorLook), and their wiring in the runtime.
    internal static class MutatorFxChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Mutator looks: "+what);count++;}
        public static void Run(string root)
        {
            count=0;
            // ---- events and blasts ----
            Check(NativeMutatorFx.Event("mutator-storm")&&NativeMutatorFx.Event("mutator-roulette")&&!NativeMutatorFx.Event("minion-spit")&&!NativeMutatorFx.Event(null),"mutator events are told apart");
            Check(NativeMutatorFx.Blast(NativeMutatorFx.Storm,out var style,out var tint,out double share)&&style==NativeBlastStyle.Shock&&share==1&&NativeBlastSim.Lightning(style,tint)&&!NativeBlastSim.BlueCyan(tint.Value.Hot)&&!NativeBlastSim.BlueCyan(tint.Value.Cool),"the storm strikes as a Shock blast with its sky bolt, never blue or cyan (owner 2026-10-10)");
            Check(NativeMutatorFx.Blast(NativeMutatorFx.Explosive,out style,out _,out share)&&style==NativeBlastStyle.Fire&&share<.5,"HIGH EXPLOSIVE's blasts are small fires at a share of the quality (one a shot)");
            Check(NativeMutatorFx.Blast(NativeMutatorFx.Martyr,out style,out tint,out _)&&style==NativeBlastStyle.Nova&&tint.HasValue,"MARTYR is a tinted nova");
            Check(!NativeMutatorFx.Blast(NativeMutatorFx.Bloated,out _,out _,out _)&&!NativeMutatorFx.Blast(NativeMutatorFx.StormWarning,out _,out _,out _)&&!NativeMutatorFx.Blast(NativeMutatorFx.Roulette,out _,out _,out _),"the warning, the gas and the glints are no blast");
            // ---- the storm's warning ----
            var fx=new NativeMutatorFx();var eye=new Vector3(0,3,-8);var right=new Vector3(1,0,0);var up=new Vector3(0,1,0);
            fx.Add(NativeMutatorFx.StormWarning,new Vector3(2,0,4),10,2,3,7);
            fx.Build(10.5,eye,right,up,false,0);int low=fx.Quads.Count;uint h0=fx.TelegraphHash;
            Check(fx.Telegraphs==1&&low>0,"a warning draws at the lowest effects quality");
            fx.Build(10.5,eye,right,up,false,1);Check(fx.Quads.Count==low&&fx.TelegraphHash==h0,"a warning is never thinned by the effects quality (rule zero: readability first)");
            Check(NativeMutatorFx.Progress(10,12,10)==0&&Math.Abs(NativeMutatorFx.Progress(10,12,11)-.5)<1e-9&&NativeMutatorFx.Progress(10,12,13)==1,"its fill grows from its event to its strike");
            int Crossings(double p){int n=0;double last=NativeMutatorFx.Pulse(0,p);for(double t=.01;t<1;t+=.01){double v=NativeMutatorFx.Pulse(t,p);if(last<.5&&v>=.5)n++;last=v;}return n;}
            Check(Crossings(1)>=2*Crossings(0),"its pulse quickens toward the strike");
            for(double t=10;t<12.29;t+=.05){fx.Build(t,eye,right,up,true,1);Check(fx.Telegraphs==1,"the warning shows over its whole two seconds ("+t.ToString("0.00")+")");}
            fx.Build(12.4,eye,right,up,false,1);Check(fx.Telegraphs==0&&fx.Warnings==0,"a warning no strike took goes soon after its strike time");
            // ---- the strike ----
            fx.Clear();fx.Add(NativeMutatorFx.StormWarning,new Vector3(2,0,4),10,2,3,7);fx.Add(NativeMutatorFx.Storm,new Vector3(2,0,4),12,3,3,8);
            Check(fx.Warnings==0&&fx.Strikes==1,"the strike takes its warning");
            fx.Build(12.02,eye,right,up,false,1);Check(fx.Bolts==1&&fx.Scorches==1,"the strike draws its bolt and scorch");
            fx.Build(12.6,eye,right,up,false,1);Check(fx.Bolts==0&&fx.Scorches==1,"the bolt is gone in half a second, the scorch stays");
            fx.Build(16.1,eye,right,up,false,1);Check(fx.Scorches==0,"the scorch fades out in four seconds");
            var floor=new Vector3(2,.3f,4);Check(NativeMutatorFx.BoltPoint(floor,8,NativeMutatorFx.BoltSegments)==floor&&Math.Abs(NativeMutatorFx.BoltPoint(floor,8,0).Y-(floor.Y+NativeMutatorFx.BoltHeight))<1e-3,"the bolt runs from high in the sky to the floor");
            bool dip=NativeMutatorFx.Stroke(.09,false)<.5&&NativeMutatorFx.Stroke(.15,false)>.8;double prev=2;bool steady=true;for(double t=0;t<=NativeMutatorFx.StrikeSeconds;t+=.01){double s=NativeMutatorFx.Stroke(t,true);steady&=s<=prev+1e-9;prev=s;}
            Check(dip&&steady,"the bolt re-strikes once; with reduced effects it fades once without flicker");
            // ---- the others ----
            fx.Clear();fx.Add(NativeMutatorFx.Bloated,new Vector3(0,0,3),20,3,3,11);fx.Add(NativeMutatorFx.Roulette,new Vector3(1,1,3),20,1,0,12);fx.Add("mutator-unknown",Vector3.Zero,20,0,0,13);
            fx.Build(20.3,eye,right,up,false,1);Check(fx.BurstsDrawn==1&&fx.Spins==1&&fx.Quads.Count>0,"BLOATED's gas and ROULETTE's glints draw; an unknown event draws nothing");
            fx.Build(21.5,eye,right,up,false,1);Check(fx.BurstsDrawn==0&&fx.Spins==0,"and they are gone in a second and a half");
            fx.Clear();for(int i=0;i<NativeMutatorFx.MaxWarnings+8;i++)fx.Add(NativeMutatorFx.StormWarning,new Vector3(i*4,0,0),30+i*.01,2,3,100+i);
            Check(fx.Warnings==NativeMutatorFx.MaxWarnings,"warnings are bounded (the oldest goes)");
            Check(NativeMutatorFx.Life(NativeMutatorFx.StormWarning,2)>2&&NativeMutatorFx.Life(NativeMutatorFx.Storm,3)>=NativeMutatorFx.ScorchSeconds-1e-9&&NativeMutatorFx.Life("mutator-explosive",1.5)==0,"a late event is recorded only while its look still plays");
            // ---- DARKNESS, WHITEOUT, BIG HEADS, payout ----
            string[] run={"darkness","big-heads","storm"};
            Check(NativeMutatorLook.Dark(run)&&!NativeMutatorLook.White(run)&&NativeMutatorLook.BigHeads(run)&&!NativeMutatorLook.Dark(null),"the look mutators are read from the run's ids");
            Check(NativeMutatorLook.DarkAmbient<=.41f&&NativeMutatorLook.HeadScale==2.2f&&NativeMutatorLook.WhiteEnd==25,"the catalog's numbers: ambient -60%, heads x2.2, fog to 25 m");
            Check(NativeMutatorLook.Within(24*24,true)&&!NativeMutatorLook.Within(26*26,true)&&NativeMutatorLook.Within(80*80,false)&&NativeMutatorLook.Sight(true,55)==25&&NativeMutatorLook.Sight(false,55)==55,"WHITEOUT hides the world's markers past 25 m, and only then");
            var ids=new[]{"iron-hide","storm","jackpot"};string pay=NativeMutatorLook.PayoutLabel(ids);
            Check(pay=="RUN PAYOUT ×1.35"&&ReferenceEquals(pay,NativeMutatorLook.PayoutLabel(ids))&&NativeMutatorLook.PayoutLabel(new[]{"jackpot"})=="RUN PAYOUT ×1.00","the station's payout line: a Bane +25%, a Chaos +10%, a Boon nothing; one string per run");
            // ---- the runtime's wiring ----
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/"+f));
            var p=R("Runtime/NativeMutatorPresentation.cs");
            Check(R("Runtime/ArenaPresentation.cs").Contains("PodFx(arena,camera);MutatorFx(arena,camera,now);"),"the effects pass draws the mutators' looks");
            Check(p.Contains("var ids=GeneratedRun?.Mutators;")&&!p.Contains("ActiveMutators"),"the looks read the run's ticket (host and guests alike), not the host's bound list");
            Check(R("Runtime/NativeBlastPresentation.cs").Contains("NativeMutatorFx.Blast(e.Detail,out var mStyle,out var mTint,out double mShare)"),"the blasts go through the shared pools, with no generic spark burst");
            var lit=R("Runtime/NativeLighting.cs");
            Check(lit.Contains("SlotColors(arenaColor)")&&lit.Contains("SlotColors(uploadColor)")&&lit.Contains("applyCount++;")&&lit.Contains("zoneHouse=scale;House();"),"DARKNESS puts out the light slots and dims the house light, again after every new map");
            Check(R("Core/NativeExpeditionCityFx.cs").Contains("if(!LampsOut){HaloFx(scene,time);BulbFx(scene,time);}")&&R("Runtime/NativeExpeditionCityPresentation.cs").Contains("if(MutatorDark)color.w*=NativeMutatorLook.DarkGlow;"),"DARKNESS puts out the town's lamps and dims its lit windows and signs");
            Check(R("Runtime/NativeCombatHud.cs").Contains("NativeMutatorLook.Sight(NativeLighting.MutatorWhite,range)")&&R("Runtime/NativeQuotaMarksPresentation.cs").Contains("NativeMutatorLook.Within(mk.Distance*mk.Distance,NativeLighting.MutatorWhite)")&&R("Runtime/NativeExpeditionWorldPresentation.cs").Split(new[]{"NativeMutatorLook.Within("},StringSplitOptions.None).Length==3,"WHITEOUT reaches the names, bars, quota marks and world pieces");
            Check(R("Runtime/NativeVisualBodyPresentation.cs").Contains("VisualBigHead(body);")&&R("Runtime/NativeZombieModelPresentation.cs").Contains("ZombieCollapse(z,lost,hidden);ZombieBigHead(z);")&&R("Runtime/NativeHordeKindPresentation.cs").Contains("HordeBigHead(rig);")&&R("Runtime/NativeSourcePresentation.cs").Contains("Matrix4x4 Bone(int b)=>BigHeadBone(rig,b);"),"BIG HEADS reaches modelled and voxel pilots and zombies");
            Check(p.Contains("(z.Hidden&(1<<CapturedRig.Head))!=0)return;"),"a zombie's lost head stays gone under BIG HEADS");
            Check(R("Runtime/NativeExpeditionLiveUI.cs").Contains("ExpeditionMutatorChips(presentation.GeneratedRun?.Mutators,hint.x-8);")&&R("Runtime/NativeDeployConsoleUI.cs").Contains("NativeMutatorLook.PayoutLabel(s.mutators)"),"the HUD shows the run's mutators and the station its payout");
            Console.WriteLine("PASS mutator looks ("+count+" checks: the storm's warning never thinned and taken by its strike, the bolt sky to floor, blasts, gas, glints, DARKNESS, WHITEOUT, BIG HEADS, payout, wiring)");
        }
    }
}
