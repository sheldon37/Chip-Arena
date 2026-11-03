using System;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    internal static class BackroomsEntityChecks
    {
        public static void Run()
        {
            int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception("Backrooms entities: "+why);}
            foreach(int seed in Enumerable.Range(0,32))
            {
                var b=NativeBackroomsGenerator.Build(seed,true);var pool=NativePoolroomsGenerator.Build(seed,true);
                foreach(var area in new[]{NativeBackroomsExpedition.Area(b),NativePoolroomsExpedition.Area(pool)})
                {
                    Check(area.spawns.Length<=256&&area.spawns.Select(a=>a.entry.e).Distinct().Count()==area.spawns.Length,"bounded unique anchors");
                    foreach(var kind in NativeBackroomsKinds.For(area.map))Check(area.spawns.Any(a=>a.allow.Contains(kind.Id)),"seed "+seed+" anchor "+kind.Id);
                    foreach(var a in area.spawns)
                    {var pos=a.entry.@exit.Position();Check(NativeMapRules.Finite(pos),"finite anchor");var map=NativeBackroomsKinds.Level0(area.map)?b.Map:pool.Map;
                        float height=Math.Max(1.8f,a.allow.Select(id=>NativeHordeZones.Of(NativeHordeKinds.Resolve(id))?.Height??1.8f).Max());
                        foreach(var box in map.blocks)if(box.pos.y+box.size.y/2>pos.y+.02f&&box.pos.y-box.size.y/2<pos.y+height-.02f)
                        {float dx=Math.Max(0,Math.Abs(pos.x-box.pos.x)-box.size.x/2),dz=Math.Max(0,Math.Abs(pos.z-box.pos.z)-box.size.z/2);Check(dx*dx+dz*dz>=.45f*.45f,"seed "+seed+" capsule clearance "+a.entry.id+" / "+box.id);}
                        if(a.allow.Contains("smiler"))Check(NativeBackroomsKinds.Dark(b.Layout,pos),"dark Smiler anchor");if(a.allow.Contains("drowned"))Check(NativePoolroomsWater.Depth(pool.Layout,pos)>.8f,"deep Drowned anchor");}
                    for(int wave=1;wave<=4;wave++)foreach(var row in NativeZoneCombat.Encounter(area.map,wave,false))Check(NativeHordeKinds.Known(row.name)&&NativeZoneCombat.Cap(row.name,wave,false)>0,"valid mix");
                }
            }
            Check(NativeBackroomsKinds.Wave(NativePoolroomsGenerator.InstanceMap,39,100)==2&&NativeBackroomsKinds.Wave(NativePoolroomsGenerator.InstanceMap,40,100)==3,"40 percent Lifeguard intro");
            Check(NativeBackroomsKinds.Wave(NativeBackroomsGenerator.InstanceMap,49,100)==2&&NativeBackroomsKinds.Wave(NativeBackroomsGenerator.InstanceMap,50,100)==3,"half quota Smiler intro");
            Check(NativeBackroomsKinds.SpawnKind(NativeBackroomsGenerator.InstanceMap,NativeExpeditionMutators.Kind(new[]{"hulkamania"},"faceling"))=="brute","Level 0 Hulkamania cannot stall quota");
            var l=new BackroomsLayout("test",1,4,4);l.Circuits[0]=BackroomsLayout.Dead;l.Circuit[5]=1;l.Circuits[1]=BackroomsLayout.On;
            Check(NativeBackroomsKinds.Dark(l,new V3(1,0,1)),"dark start");Check(!NativeBackroomsKinds.DarkPath(l,new V3(1,0,4.8f),new V3(10,0,4.8f)),"cannot cross lit cell between dark endpoints");
            Check(!NativeBackroomsKinds.Dark(l,new V3(-1,0,1)),"outside not dark");
            foreach(var kind in NativeBackroomsKinds.Kinds)Check(NativeHordeKinds.Known(kind.Id)&&kind.Proposed.hp>0,"network kind known");
            foreach(var attack in new[]{"wake","howl","pounce","blink","lunge","hook","sweep"})
            {var f=new NativeZoneEnemyFrame{attack=attack,dormant=true,wakeAt=2,blinkAt=3,blinkX=4,blinkY=1,blinkZ=5,dreadUntil=7,dreadTarget=1,grabAt=2,releaseAt=3,grabTarget=1};Check(f.Valid(),"special valid");var json=JsonSerializer.Serialize(f,new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true});var copy=JsonSerializer.Deserialize<NativeZoneEnemyFrame>(json,new JsonSerializerOptions{IncludeFields=true});Check(copy.Valid()&&copy.dormant&&copy.blinkZ==5&&copy.grabTarget==1&&copy.dreadTarget==1&&copy.attack==attack,"wire roundtrip");copy.blinkAt=float.NaN;Check(!copy.Valid(),"nonfinite rejected");}
            Console.WriteLine("PASS Backrooms entities: "+count+" checks");
        }
    }
}
