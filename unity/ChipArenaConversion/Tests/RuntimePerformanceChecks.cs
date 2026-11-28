using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class RuntimePerformanceChecks
{
    public static void Run(string root)
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception("Runtime performance: "+why);}
        var data=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new JsonSerializerOptions{IncludeFields=true});
        foreach(var w in data.weapons){Check(ReferenceEquals(data.Weapon(w.id),Array.Find(data.weapons,x=>x.id==w.id)),"weapon lookup");Check(data.Weight(w.id)==((data.weights==null?null:Array.Find(data.weights,x=>x?.name==w.id))?.value??4),"weight lookup");}
        foreach(var a in data.abilities)Check(ReferenceEquals(data.Ability(a.spec,a.slot),Array.Find(data.abilities,x=>x.spec==a.spec&&x.slot==a.slot)),"first ability wins");
        foreach(var c in data.chips)Check(ReferenceEquals(data.Chip(c.id),Array.Find(data.chips,x=>x.id==c.id)),"chip lookup");
        foreach(var m in data.materials)Check(ReferenceEquals(data.Material(m.id),Array.Find(data.materials,x=>x.id==m.id)),"material lookup");
        Check(data.Ability("absent","q")==null&&data.Weight("absent")==4,"missing optional rows");
        bool threw=false;try{data.Weapon("absent");}catch(ArgumentException){threw=true;}Check(threw,"missing required row throws");
        var actor=new Actor{Id=1,Technique=new NativeTechnique{Kind="riposte"}};var source=new NativeTechniqueSnapshotSource(new Snapshot{Actors=new[]{null,actor}});
        Check(source.Of(1)==actor.Technique&&source.Of(2)==null,"snapshot technique selection");
        string spec=data.weapons[0].id;var watch=new Stopwatch();object sink=null;
        static ContentWeapon LegacyWeapon(ConversionContent d,string id)=>Array.Find(d.weapons,x=>x.id==id);
        static ContentAbility LegacyAbility(ConversionContent d,string id,string slot)=>Array.Find(d.abilities,x=>x.spec==id&&x.slot==slot);
        Action old=()=>{sink=LegacyWeapon(data,spec);sink=LegacyAbility(data,spec,"q");};
        Action current=()=>{sink=data.Weapon(spec);sink=data.Ability(spec,"q");sink=source.Of(1);};
        (long bytes,double ms) Measure(Action a){for(int i=0;i<100;i++)a();long b=GC.GetAllocatedBytesForCurrentThread();watch.Restart();for(int i=0;i<100000;i++)a();watch.Stop();return (GC.GetAllocatedBytesForCurrentThread()-b,watch.Elapsed.TotalMilliseconds);}
        var before=Measure(old);var after=Measure(current);Check(after.bytes==0,"hot lookup allocation budget");GC.KeepAlive(sink);
        var maps=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),new JsonSerializerOptions{IncludeFields=true});int links=0;
        bool Legacy(ContentMap m,MapNavLink l,NativeStructureState state){foreach(var d in m.segments??Array.Empty<StructureDefinition>()){int s=state.Get(d.id).state;if(s!=0&&(d.nav?.remove??Array.Empty<string>()).Contains(l.id))return false;if((d.nav?.add??Array.Empty<string>()).Contains(l.id)&&s!=3)return false;}return true;}
        foreach(var map in maps.maps)
        {
            if(map.nav==null)continue;var index=new NativeNavDependencies(map.segments);var state=new NativeStructureState(map);
            // Includes all-intact, all-fallen and mixed immediate changes. No stale state cache is permitted.
            for(int pattern=0;pattern<8;pattern++)
            {int n=0;foreach(var d in map.segments??Array.Empty<StructureDefinition>())state.Get(d.id).state=pattern<4?pattern:(n+++pattern)%4;
             foreach(var link in map.nav.links){Check(index.Enabled(link.id,state)==Legacy(map,link,state),"navigation dependency parity "+map.id+"/"+link.id);links++;}}
            if(map.nav.links.Length>0){var link=map.nav.links[0];long b=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)index.Enabled(link.id,state);Check(GC.GetAllocatedBytesForCurrentThread()==b,"navigation dependency no allocations");}
        }
        var window=new NativeHitchWindow();window.Percentiles(out double p95,out double p99);Check(p95==0&&p99==0,"empty window");
        for(int i=1;i<=240;i++)window.Observe(i,i);window.Percentiles(out p95,out p99);Check(p95==228&&p99==238,"nearest rank percentiles");
        for(int i=0;i<240;i++)window.Observe(300+i,8);window.Percentiles(out p95,out p99);Check(p95==8&&p99==8,"rolling percentiles recover");
        long allocated=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<100;i++)window.Percentiles(out p95,out p99);Check(GC.GetAllocatedBytesForCurrentThread()==allocated,"reporting scratch reused");
        Console.WriteLine($"PASS runtime performance: {checks} checks; {links} nav-state comparisons; 100k lookup batches old {before.bytes} B / {before.ms:F2} ms, new {after.bytes} B / {after.ms:F2} ms (managed microbenchmark, not game FPS)");
    }
}
