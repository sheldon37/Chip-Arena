using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class FirstUseStallChecks
{
    public static void Run(string root)
    {
        int n=0;void Check(bool value,string why){if(!value)throw new Exception("First-use stalls: "+why);n++;}
        var work=new NativeLatestWork<int>();using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim())
        {
            try
            {
                int skipped=0;work.Request(()=>{entered.Set();release.Wait();return 1;});Check(entered.Wait(5000),"worker started");
                Check(!work.Poll(out _,out _),"owner never waits for unfinished work");
                work.Request(()=>{Interlocked.Increment(ref skipped);return 2;});work.Request(()=>3);
                Check(skipped==0&&!work.Poll(out _,out _),"rerolls queue without concurrent generation");release.Set();
                int result=0;Exception error=null;Check(SpinWait.SpinUntil(()=>work.Poll(out result,out error),10000)&&result==3&&error==null&&skipped==0,"stale result discarded and only latest reroll runs");
            }
            finally{release.Set();}
        }
        using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim())
        {
            try
            {
                work.Request(()=>{entered.Set();release.Wait();throw new InvalidOperationException("old station");});Check(entered.Wait(5000),"reset fixture started");
                work.Reset();work.Request(()=>4);release.Set();int result=0;Exception error=null;
                Check(SpinWait.SpinUntil(()=>work.Poll(out result,out error),10000)&&result==4&&error==null,"leaving discards old result and observes old fault");
            }
            finally{release.Set();}
        }
        work.Request(()=>throw new FormatException("bad ticket"));Exception failure=null;
        Check(SpinWait.SpinUntil(()=>work.Poll(out _,out failure),10000)&&failure is FormatException,"current failure reaches owner without blocking");
        var sizes=Enumerable.Range(1,1024).Select(x=>NativeIconRaster.CachePixels(x)).Distinct().ToArray();
        Check(sizes.SequenceEqual(new[]{32,64,128,256,512}),"at most five textures per glyph across resizing");
        Check(Enumerable.Range(32,481).All(x=>NativeIconRaster.CachePixels(x)>=x),"no lower final resolution for requested pixels");
        var timer=Stopwatch.StartNew();foreach(var id in NativeBombChips.Ids)NativeIconRaster.Raster(NativeIconRaster.Parse(NativeBombChips.Glyph(id),1,NativeIconRaster.Mode.Markup),256,NativeBombChips.GlyphBox);
        Console.WriteLine("Bomb glyph cold raster benchmark: ten 256px glyphs "+timer.Elapsed.TotalMilliseconds.ToString("F1")+" ms; now worker work, not OnGUI (managed CPU only)");
        string mapsJson=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),authored=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ExpeditionLayout.json"));
        var maps=JsonSerializer.Deserialize<UnityMapContent>(mapsJson,new JsonSerializerOptions{IncludeFields=true});
        var content=new ConversionContent{maps=maps.maps,contentRevision=NativeMapRules.Revision(mapsJson)};
        var layout=NativeExpeditionLayout.Parse(authored);layout.compatibility=NativeExpeditionLayout.Compatibility(authored,content);
        var room=new ExpeditionDeploymentRoom("perf","host",123,"black-siren",layout,content);
        timer.Restart();Check(room.Execute("host",0,ExpeditionDeploymentIntent.RollInstances),"roll accepted");double rollMs=timer.Elapsed.TotalMilliseconds;
        var ticket=room.Ticket;timer.Restart();var expected=ExpeditionRunBundle.Build(ticket,content,authored);double buildMs=timer.Elapsed.TotalMilliseconds;
        var builder=new NativeLatestWork<ExpeditionRunBundle>();builder.Request(()=>ExpeditionRunBundle.Build(ticket.Copy(),content,authored));ExpeditionRunBundle actual=null;Exception buildError=null;
        Check(SpinWait.SpinUntil(()=>builder.Poll(out actual,out buildError),30000)&&buildError==null&&actual.Identity==expected.Identity,"worker generation keeps exact authenticated identity");
        Check(content.maps.Length==maps.maps.Length&&!content.maps.Any(m=>m.id==NativeExpeditionGeneratedWorld.MapId),"worker leaves source content unchanged");
        Console.WriteLine("Destination benchmark: roll "+rollMs.ToString("F1")+" ms, bundle build "+buildMs.ToString("F1")+" ms; bundle generation now leaves click and packet handlers");
        var icons=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeIcons.cs"));
        Check(icons.Contains("if(NativeBombChips.Index(bombId)>=0){DrawBomb(r,bombId,c,maxPx);return;}")&&icons.Contains("bombJob.IsCompleted"),"Bomb draws bypass synchronous rasterization");
        var runtime=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeExpeditionDeploymentRoom.cs"));
        Check(runtime.Contains("deploymentWork.Request(()=>ExpeditionRunBundle.Build(copy,original,authored))")&&runtime.Contains("deploymentWork.Reset()"),"generation queued and invalidated on leave");
        Console.WriteLine("PASS first-use stalls: "+n+" checks");
    }
}
