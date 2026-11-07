using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using ChipArena.UnityConversion;
internal static class DeployPerformanceChecks
{
    private static int count;
    private static void Check(bool ok,string message){count++;if(!ok)throw new Exception("Deployment performance: "+message);}
    public static void Run(string root)
    {
        count=0;var pool=new[]{new ExpeditionInstanceTheme{id="exp-ward-zero",theme="quarantine"},new ExpeditionInstanceTheme{id="exp-transit",theme="rail"},new ExpeditionInstanceTheme{id="exp-foundry",theme="industrial"}};
        var authority=new NativeExpeditionDeployment("perf","host",42);authority.RollInstances("host",pool);authority.RollMutators("host",NativeExpeditionMutators.Ids);
        var input=new DeployInput{State=authority.Snapshot(),Instances=pool,Mutators=NativeExpeditionMutators.Ids,Host=true,PartyReady=true,SinceInstanceRoll=9,SinceMutatorRoll=9,SinceDeploy=float.NaN,Time=9};
        var stage=new NativeDeployStage();stage.Frame(input,-1);var original=stage.Strip(1,0);int version=stage.StripVersion;
        // Preallocate equivalent copies so this measures renderer work, not the defensive API's intentional clone.
        var copies=Enumerable.Range(0,120).Select(_=>NativeExpeditionMutators.Ids).ToArray();var timer=new Stopwatch();timer.Start();long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<copies.Length;i++){input.Mutators=copies[i];input.Time+=1/60f;stage.Frame(input,1/60f);}
        long allocated=GC.GetAllocatedBytesForCurrentThread()-before;timer.Stop();int rebuilds=stage.StripVersion-version;
        Console.WriteLine("Deployment catalog benchmark: 120 equivalent-catalog frames, "+rebuilds+" strip rebuilds, "+allocated+" stage bytes, "+timer.Elapsed.TotalMilliseconds.ToString("F2")+" ms (excludes Unity/art workers/GPU)");
        Check(rebuilds==0,"equivalent catalogs must not schedule new strip/artwork jobs");Check(ReferenceEquals(original,stage.Strip(1,0)),"existing strip storage retained");Check(allocated==0,"equivalent catalog comparisons allocate no frame memory");
        input.Mutators=new[]{"jackpot"};stage.Frame(input,1/60f);Check(stage.StripVersion==version+1,"actual catalog change rebuilds");
        version=stage.StripVersion;input.Mutators=new[]{"jackpot"};stage.Frame(input,1/60f);Check(stage.StripVersion==version,"same changed catalog stays cached");
        input.State.mutators=new[]{"future-mutator"};stage.Frame(input,1/60f);Check(stage.StripVersion==version+1&&stage.Strip(1,0).Contains("future-mutator"),"result absent from catalog still invalidates strips");
        string live=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeDeploymentLive.cs"));
        Check(live.Contains("static readonly string[] LiveDeployMutators=NativeExpeditionMutators.Ids;")&&live.Contains("Mutators=LiveDeployMutators}"),"live frame reuses one catalog array");
        var full=pool.Concat(new[]{new ExpeditionInstanceTheme{id=NativeBackroomsGenerator.AreaId,theme="backrooms"},new ExpeditionInstanceTheme{id=NativePoolroomsGenerator.AreaId,theme="poolrooms"}}).ToArray();
        var rolls=new NativeExpeditionDeployment("all-destinations","host",42);var reels=new NativeDeployStage();
        var complete=new DeployInput{State=rolls.Snapshot(),Instances=full,Mutators=NativeExpeditionMutators.Ids,SinceInstanceRoll=float.NaN,SinceMutatorRoll=float.NaN,SinceDeploy=float.NaN};
        reels.Frame(complete,-1);int initial=reels.StripVersion;var strips=Enumerable.Range(0,6).Select(i=>reels.Strip(i/3,i%3)).ToArray();
        for(int roll=0;roll<100;roll++){rolls.RollInstances("host",full);complete.State=rolls.Snapshot();complete.SinceInstanceRoll=0;reels.Frame(complete,1/60f);Check(reels.StripVersion==initial&&Enumerable.Range(0,6).All(i=>ReferenceEquals(strips[i],reels.Strip(i/3,i%3))),"all five destinations roll without rebuilding reel textures");}
        Check(live.Contains("Instances=LiveDeployInstances")&&live.Contains("id=NativeBackroomsGenerator.AreaId")&&live.Contains("id=NativePoolroomsGenerator.AreaId"),"live catalog includes both generated destinations before the first roll");
        string scene=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeDeployScene.cs"));
        Check(scene.Contains("if(stripKeys[slot]==key)continue;")&&!scene.Contains("tileJobs.Clear()"),"unchanged strips and rasterized cards survive rerolls");
        Check(scene.Contains("uploaded=true;break;")&&scene.Contains("if(!uploaded&&stampJob")&&scene.Contains("if(!uploaded&&mapJob"),"only one texture upload per frame");
        Check(scene.Contains("tileTail.ContinueWith"),"cold raster jobs use one CPU worker at a time");
        Console.WriteLine("PASS Deployment performance: "+count+" checks");
    }
}
