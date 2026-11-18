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
        Eject(scene);
        Console.WriteLine("PASS Deployment performance: "+count+" checks");
    }
    // The eject's set under the floor (re-pinned 2026-11-18, W3): the tube, seat and belly models (NativeDeployEjectModels) are drawn
    // only after Deploy and only for occupied pads, counted against their own budgets, the city only in the release shot (built on a
    // worker once per run); every frame of the eject allocates nothing.
    private static readonly NativeDeployStage.Model[] EjectModels={NativeDeployStage.Model.PadRing,NativeDeployStage.Model.Iris,NativeDeployStage.Model.Tube,NativeDeployStage.Model.TubeDeep,
        NativeDeployStage.Model.Collar,NativeDeployStage.Model.Seat,NativeDeployStage.Model.Harness,NativeDeployStage.Model.Clamp,NativeDeployStage.Model.Belly,NativeDeployStage.Model.City};
    private static void Eject(string scene)
    {
        var budgets=new (string id,WeaponMesh mesh,int budget)[]{("pad ring",NativeDeployModels.PadRing(),1200),("tube",NativeDeployEjectModels.TubeOf(NativeDeployEject.TubeLength(0)),2000),
            ("deep tube",NativeDeployEjectModels.TubeOf(NativeDeployEject.TubeLength(1)),2600),("collar",NativeDeployEjectModels.CollarModel(),1000),("seat",NativeDeployEjectModels.SeatModel(),1200),
            ("harness",NativeDeployEjectModels.HarnessModel(),800),("clamp",NativeDeployEjectModels.ClampModel(),400),("belly",NativeDeployEjectModels.BellyModel(),8000)};
        foreach(var (id,mesh,budget) in budgets)
        {
            Check(mesh.Triangles>0&&mesh.Triangles<=budget,id+" budget ("+mesh.Triangles+")");
            foreach(var s in mesh.Sections)
            {
                Check(s.Positions.All(p=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z))&&s.Normals.All(n=>Math.Abs(n.Length()-1)<1e-3f)&&s.Indices.Count%3==0,id+": clean geometry");
                Check(s.Colors.All(c=>c.X<=1.0001f&&c.Y<=1.0001f&&c.Z<=1.0001f&&c.W==1),id+": opaque colours in range");
                if(s.Bone>=NativeDeployEjectModels.Lights)for(int i=0;i<s.Colors.Count;i++){var c=s.Colors[i];var e=s.Emission[i];if(Math.Abs(c.X-e.X)+Math.Abs(c.Y-e.Y)+Math.Abs(c.Z-e.Z)>1e-4f||e.X+e.Y+e.Z<=0){Check(false,id+": its lights are unlit");break;}}
            }
        }
        var pool=new[]{new ExpeditionInstanceTheme{id="exp-ward-zero",theme="quarantine"},new ExpeditionInstanceTheme{id="exp-transit",theme="rail"},new ExpeditionInstanceTheme{id="exp-foundry",theme="industrial"}};
        var a=new NativeExpeditionDeployment("eject","host",4242);a.RollInstances("host",pool);var rolled=a.Snapshot();a.Deploy("host",true);var dep=a.Snapshot();
        var timer=new Stopwatch();timer.Start();var city=NativeDeployEjectModels.CityModel(dep);timer.Stop();
        Check(city.Triangles>2000&&city.Triangles<=20000&&timer.Elapsed.TotalMilliseconds<250,"the rolled city builds in its budget ("+city.Triangles+" triangles, "+timer.Elapsed.TotalMilliseconds.ToString("F1")+" ms)");
        Check(scene.Contains("cityJob=Task.Run(()=>NativeDeployEjectModels.CityModel(copy))")&&scene.Contains("if(!uploaded&&cityJob!=null&&cityJob.IsCompleted)"),"the city builds on a worker, uploaded on a frame without another upload");
        foreach(var cut in new[]{false,true})for(int seats=1;seats<=4;seats++)
        {
            var st=new NativeDeployStage();var input=new DeployInput{State=rolled,Instances=pool,Mutators=NativeExpeditionMutators.Ids,Host=true,PartyReady=true,SinceInstanceRoll=9,SinceMutatorRoll=float.NaN,SinceDeploy=float.NaN,Time=1,Seats=seats,Ready=new bool[8],Mine=seats-1,Short=cut};
            st.Frame(input,-1);Check(!Items(st).Any(m=>EjectModels.Contains(m)),"nothing of the eject's set before Deploy");
            input.State=dep;float travel=NativeDeployEject.TravelOf(cut?NativeDeployEject.Cut.Short:NativeDeployEject.Played);int most=0,worst=0;
            for(float t=0;t<travel+.3f;t+=1/30f)
            {
                input.SinceDeploy=t;input.Time=1+t;st.Frame(input,1/30f);int n=0,tris=0;
                for(int i=0;i<st.Count;i++){var m=st.Items[i].Model;if(!EjectModels.Contains(m))continue;n++;tris+=st.Models[(int)m].Triangles;}
                most=Math.Max(most,n);worst=Math.Max(worst,tris);
                Check(Items(st).Count(m=>m==NativeDeployStage.Model.Tube||m==NativeDeployStage.Model.TubeDeep)==seats&&Items(st).Count(m=>m==NativeDeployStage.Model.Seat)==seats,"a tube and a seat for each occupied pad only");
                Check(Items(st).Contains(NativeDeployStage.Model.City)==(st.Shot==NativeDeployPresentation.Shot.Release),"the city only in the release shot");
                Check(st.Count<=NativeDeployStage.Capacity&&st.PodShown.Take(seats).All(x=>x)&&!st.PodShown.Skip(seats).Any(x=>x),"the occupied pads' pods and none other");
            }
            // the same frames again, measured: the stage's eject allocates nothing per frame
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(float t=0;t<travel+.3f;t+=1/30f){input.SinceDeploy=t;input.Time=1+t;st.Frame(input,1/30f);}
            long used=GC.GetAllocatedBytesForCurrentThread()-before;Check(used==0,"the eject's frames allocate nothing ("+used+" bytes)");
            Check(most<=NativeDeployStage.EjectItems&&worst<60000,"the eject's set within its items and triangles ("+most+" items, "+worst+" triangles, a party of "+seats+")");
        }
    }
    private static System.Collections.Generic.IEnumerable<NativeDeployStage.Model> Items(NativeDeployStage st){for(int i=0;i<st.Count;i++)yield return st.Items[i].Model;}
}
