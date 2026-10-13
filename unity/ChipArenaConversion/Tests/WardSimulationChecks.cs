using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    internal static class WardSimulationChecks
    {
        public static void Run(string root)
        {
            int count=0;
            void Check(bool ok,string why){count++;if(!ok)throw new Exception("Ward simulation: "+why);}
            var options=new JsonSerializerOptions{IncludeFields=true};
            T Read<T>(string name)=>JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources",name)),options);
            var content=Read<ConversionContent>("GameContent.json");var old=content.maps.Single(m=>m.id=="siren");
            var parity=JsonSerializer.Deserialize<ParityFixtures>(File.ReadAllText(Path.Combine(root,"Tests/source-parity.json")),options);
            // Model JsonUtility's omitted inline objects, then execute the Editor's corrected order.
            foreach(var legacy in content.maps){legacy.bounds=new MapBounds();legacy.spawns=new MapSpawns();legacy.nav=new MapNav();}
            Check(NativeParityChecks.Run(content,parity).Count>0,"pinned source parity passes before map replacement, with JsonUtility defaults");
            string editor=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Editor/NativeBuildChecks.cs"));
            int parityAt=editor.IndexOf("NativeParityChecks.Run(data,fixtures)",StringComparison.Ordinal),mergeAt=editor.IndexOf("NativeUnityMapLoader.Load(data)",StringComparison.Ordinal),legacyAt=editor.IndexOf("CheckLegacyMapDefaults(data)",StringComparison.Ordinal);
            Check(parityAt>=0&&mergeAt>parityAt&&legacyAt>mergeAt,"Editor checks raw source parity before map loading, then validates normalized legacy maps");
            var maps=Read<UnityMapContent>("UnityMaps.json");NativeMapRules.Merge(content,maps,File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")));
            Check(content.maps.Count(m=>!m.unity)==14,"Ward replacement removes exactly one legacy map");
            bool rejectsMerged=false;try{NativeParityChecks.Run(content,parity);}catch(Exception e){rejectsMerged=e.Message=="Content inventory differs from pinned source";}
            Check(rejectsMerged,"regression reproduces original failure when parity sees merged content; inventory gate remains strict");
            var map=content.maps.Single(m=>m.id=="siren");var pve=Read<NativePveContent>("PveContent.json");NativeWardEntries.Validate(pve,map);
            // Machine collision belongs to PveContent, not the authored map's replicated props.
            Check(pve.machineBlocks.Length==8&&pve.machineBlocks.All(b=>!map.blocks.Any(m=>m.id==b.id)),"reproduce out-of-map PvE machine definitions");
            string arenaSource=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeArena.cs"));
            string pveSource=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativePve.cs"));
            string frameSource=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeNetworkFrame.cs"));
            Check(arenaSource.Contains("public bool Replicated=>!LocalFixture&&Definition.role!=\"carried\"")&&pveSource.Contains("prop.LocalFixture=true;prop.Indestructible=true"),"only locally authored machines opt out of prop replication");
            Check(frameSource.Contains("Props.Where(p=>p.Replicated).Select(FrameProp)")&&frameSource.Contains("if(!p.Replicated)continue;var pf="),"host serialization and guest removal share the fixture exclusion");
            Check(map.name=="Ward Zero"&&map.look=="quarantine"&&map.music==old.music&&map.rotation==old.rotation,"identity preserved");
            Check(map.blocks.Length==616&&map.ramps.Length==8&&map.bounds.ceiling==13&&map.bounds.killY==-6,"complete import");
            Check(pve.entries.Length==53&&pve.machines.Length==8&&pve.pads.Length==8&&pve.hideSpots.Length==12,"PvE import");
            Check(pve.pads.Count(p=>p.y==6)==4&&pve.hideSpots.Count(p=>p.y==6)==6,"two floors");
            Check(new PvePoint{x=1,y=6,z=2}.Position(.9f).y==6.9f,"point height adds floor");
            foreach(var h in map.hazards)
            {
                if(h.kind=="burner"||h.kind=="gas")
                {
                    for(int round=1;round<=2;round++)
                    {
                        double start=10+h.first+(round%2==0?h.offset:0)+h.warn;
                        Check(NativeStageHazards.DamageTicks(h,round,10,start-.01,start)==1,"first tick");
                        Check(NativeStageHazards.DamageTicks(h,round,10,start-.01,start+h.live)==(int)(h.live/h.tick),"whole window");
                        Check(NativeStageHazards.DamageTicks(h,round,10,start+h.live,start+h.live+.1)==0,"clear harmless");
                        Check(NativeWardZeroHazards.At(h,NativePhase.Intro,round,10,start).Phase==NativeHazardPhase.Idle,"intro harmless");
                    }
                    Check(NativeStageHazards.DeathKind(h.kind),"death kind");
                    float period=h.period;h.period=1;bool rejected=false;try{NativeStageHazards.Validate(map);}catch(InvalidOperationException){rejected=true;}h.period=period;Check(rejected,"invalid timing rejected");
                }
                if(h.kind=="burner"){Check(!NativeStageHazards.BurstTouch(h,h.center),"lane pocket harmless");Check(NativeStageHazards.BurstTouch(h,h.zones[0].center),"row damages");}
            }
            var nodes=map.nav.nodes.ToDictionary(n=>n.id);
            foreach(var ramp in map.ramps)
            {
                var d=(ramp.high-ramp.low);var dir=new V3(d.x,0,d.z).Normalized;
                bool Near(MapNavNode n){var v=n.pos-ramp.low;float along=v.x*dir.x+v.z*dir.z,cross=v.x*dir.z-v.z*dir.x;return along>=-2&&along<=Math.Sqrt(d.x*d.x+d.z*d.z)+2&&Math.Abs(cross)<=ramp.width/2;}
                var eligible=nodes.Values.Where(Near).ToDictionary(n=>n.id);var edges=eligible.Keys.ToDictionary(k=>k,k=>new List<string>());
                foreach(var l in map.nav.links)if(l.kind!="drop"&&eligible.ContainsKey(l.a)&&eligible.ContainsKey(l.b)){edges[l.a].Add(l.b);edges[l.b].Add(l.a);}
                var start=eligible.Values.OrderBy(n=>(n.pos-(ramp.low+new V3(0,.9f,0))).Length).First();
                var end=eligible.Values.OrderBy(n=>(n.pos-(ramp.high+new V3(0,.9f,0))).Length).First();
                var seen=new HashSet<string>();var queue=new Queue<string>();queue.Enqueue(start.id);
                while(queue.Count>0){var at=queue.Dequeue();if(!seen.Add(at))continue;foreach(var next in edges[at])queue.Enqueue(next);}
                Check(seen.Contains(end.id),"isolated route "+ramp.id);
            }
            Check(map.nav.links.Count(l=>l.kind=="drop")==12&&map.nav.links.Where(l=>l.kind=="drop").All(l=>nodes[l.a].pos.y>nodes[l.b].pos.y),"one-way drop data");
            foreach(var e in pve.entries)
            {
                var timeline=NativeWardEntries.Timeline(e,true,e.hulk?"hulk":"brute");
                Check(timeline.duration>0&&timeline.walk<timeline.duration,"entry timeline "+e.id);
                if(e.kind=="breach")Check(!NativeWardEntries.Allows(e,"runner")&&NativeWardEntries.Allows(e,"hulk"),"breach hulk");
                if(e.kind=="drop")Check(NativeWardEntries.Allows(e,"hulk")==e.hulk,"hulk drop eligibility");
            }
            var b=new NativeBreachRecord{entry=45,breakAt=12,restoreAt=24};var clone=JsonSerializer.Deserialize<NativeBreachRecord>(JsonSerializer.Serialize(b,options),options);
            Check(clone.Valid()&&clone.restoreAt==24&&NativeBreach.SolidAt(clone.restoreAt)==26.6f,"breach roundtrip");
            clone.restoreAt=float.NaN;Check(!clone.Valid(),"breach bounds");
            Console.WriteLine("PASS Ward simulation: "+count+" checks; map replacement, hazards, eight isolated floor routes, entries and breach records");
        }
    }
}
