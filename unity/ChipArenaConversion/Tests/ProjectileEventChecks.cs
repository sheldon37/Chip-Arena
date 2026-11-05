using System;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class ProjectileEventChecks
{
    public static void Run()
    {
        int n=0;void Check(bool ok,string why){n++;if(!ok)throw new Exception("Projectile event data: "+why);}
        var events=new NativeEventBuffer();
        for(int i=0;i<100;i++)events.Add(new ArenaEvent{Id=i,Kind=EventKind.BurnTick,Time=1});
        Check(events.Count==32,"ordinary effects retain the first-32 quota");
        var kinds=new[]{EventKind.Shot,EventKind.Hit,EventKind.Headshot,EventKind.Impact,EventKind.Ricochet,EventKind.Split};
        for(int round=1;round<=60;round++)foreach(var kind in kinds)events.Add(new ArenaEvent{Id=1000+round*10+(int)kind,Kind=kind,Projectile=round,Time=1});
        Check(events.Count==392&&kinds.All(k=>events.Count(e=>e.Kind==k)==60),"multiplied burst preserves all 360 correlated notifications after decorative quota exhaustion");
        for(int i=0;i<200;i++)events.Add(new ArenaEvent{Kind=EventKind.Shot,Projectile=1000+i,Time=1});
        Check(events.Count==512&&!events.Any(e=>e.Kind==EventKind.BurnTick),"capacity first evicts ordinary transient effects");
        var oldest=events[0];events.Add(new ArenaEvent{Kind=EventKind.Heal,Time=2});Check(events[0].Projectile==oldest.Projectile&&events.Count==512,"ordinary effects cannot evict a full protected buffer");
        events.Add(new ArenaEvent{Kind=EventKind.Death,Time=2});Check(events.Count==512&&events.Last().Kind==EventKind.Death,"hard limit retains new critical event by evicting oldest if no disposable events remain");
        var ends=new NativeShotEnds();ends.Record(1,10,new V3(1,0,1),2);ends.Record(2,10,new V3(1,0,1),3);
        var options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};var copy=JsonSerializer.Deserialize<NativeShotEnd[]>(JsonSerializer.Serialize(ends.Snapshot(10),options),options);
        Check(copy[0].Owner==2&&copy[1].Owner==3&&NativeShotEnds.Valid(copy,10),"coincident endpoints retain distinct shooters through field JSON");
        copy[0].Owner=-1;Check(!NativeShotEnds.Valid(copy,10),"negative owner rejected");copy[0].Owner=0;Check(NativeShotEnds.Valid(copy,10),"legacy absent owner accepted");
        var kit=new NativeBombKit();NativeBombPreview.Write(kit,System.Numerics.Vector3.Zero,0,0,4,2);
        Check(kit.Bombs.Length>0&&kit.Bombs.All(b=>b.SampleAt==4)&&kit.Valid(),"preview records sampled clock separately from spawn");
        var detached=kit.ViewCopy();kit.Bombs[0].SampleAt=5;Check(detached.Bombs[0].SampleAt==4,"snapshot copy owns sample timestamp");
        var wire=JsonSerializer.Deserialize<NativeBombKit>(JsonSerializer.Serialize(detached,options),options);Check(wire.Bombs[0].SampleAt==4&&wire.Valid(),"sample clock survives field JSON");wire.Bombs[0].SampleAt=float.NaN;Check(!wire.Valid(),"nonfinite sample rejected");
        Console.WriteLine("PASS projectile event data: "+n+" checks");
    }
}
