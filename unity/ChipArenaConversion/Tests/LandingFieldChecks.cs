using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChipArena.UnityConversion
{
    internal static class LandingFieldChecks
    {
        private const int Seeds=2000;

        private static void Check(bool ok,uint seed,string message)
        {
            if(!ok)throw new Exception("Landing field "+seed+": "+message);
        }

        public static void Fields()
        {
            bool Clear(float x,float z,float yaw)=>NativeExpeditionHubPlan.Clear(new ExpeditionPoint{x=x,z=z,yaw=yaw});
            Check(!Clear(13.5f,13.5f,225),0,"reject a hull on the chip machine");
            Check(!Clear(-13.5f,13.5f,135),0,"reject a hull on the revive machine");
            Check(Clear(13.5f,8,0)&&!Clear(13.5f,8,180),0,"skid behind a clear centre must miss the camp machines");
            Check(!Clear(13.5f,8.5f,0),0,"reject a ramp foot too close to the chip service");
            var figures=new HashSet<(float,float,float,float,float,float,float,float)>();
            for(uint i=0;i<Seeds;i++)
            {
                uint seed=unchecked(i*2654435761u);
                var spots=NativeExpeditionHubPlan.LandingField(seed);
                var replay=NativeExpeditionHubPlan.LandingField(seed);
                Check(spots.Length==8,seed,"eight slots");
                var quadrants=new HashSet<(bool,bool)>();
                for(int seat=0;seat<spots.Length;seat++)
                {
                    var p=spots[seat];
                    Check(p.x==replay[seat].x&&p.z==replay[seat].z&&p.yaw==replay[seat].yaw,seed,"reconstruction");
                    Check(NativeExpeditionHubPlan.Clear(p),seed,"camp and skid apron clearance at "+seat);
                    Check(Math.Min(Math.Abs(p.x),Math.Abs(p.z))>=4.2f,seed,"gate axis clearance");
                    double r=Math.Sqrt(p.x*p.x+p.z*p.z);
                    double yaw=p.yaw*Math.PI/180;
                    Check((-p.x*Math.Sin(yaw)-p.z*Math.Cos(yaw))/r>=Math.Cos(Math.PI/6)-.000001,seed,"towards the fountain");
                    if(seat<4)
                    {
                        Check(r>=11&&r<=15,seed,"active seat radius");
                        quadrants.Add((p.x>0,p.z>0));
                    }
                    for(int j=0;j<seat;j++)
                        Check((p.Position-spots[j].Position).Length>=12,seed,"separation "+seat+"/"+j);
                }
                Check(quadrants.Count==4,seed,"non-collinear active pods");
                Check(figures.Add((spots[0].x,spots[0].z,spots[1].x,spots[1].z,spots[2].x,spots[2].z,spots[3].x,spots[3].z)),seed,"repeated landing figure");
            }
            Console.WriteLine("PASS 2,000 deterministic landing fields, eight seats each");
        }

        public static void WorldSweep(string root,params uint[] seeds)
        {
            if(seeds.Length==0)
            {
                Fields();
                seeds=Enumerable.Range(0,Seeds).Select(i=>unchecked((uint)i*2654435761u)).ToArray();
            }
            string Read(string name)=>File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources",name));
            string mapsJson=Read("UnityMaps.json"),layoutJson=Read("ExpeditionLayout.json");
            var maps=JsonSerializer.Deserialize<UnityMapContent>(mapsJson,new JsonSerializerOptions{IncludeFields=true});
            var content=new ConversionContent{maps=maps.maps,contentRevision=NativeMapRules.Revision(mapsJson)};
            var layout=NativeExpeditionLayout.Parse(layoutJson);
            layout.compatibility=NativeExpeditionLayout.Compatibility(layoutJson,content);
            int completed=0;
            var clock=System.Diagnostics.Stopwatch.StartNew();
            Parallel.ForEach(seeds,new ParallelOptions{MaxDegreeOfParallelism=Math.Min(6,Environment.ProcessorCount)},seed=>
            {
                try
                {
                    var room=new ExpeditionDeploymentRoom("landing-sweep","pilot",seed,"black-siren",layout,content);
                    Check(room.Execute("pilot",0,ExpeditionDeploymentIntent.RollInstances),seed,"roll");
                    // The live graph floods camp-connected surfaces. Check independently that each gameplay point attaches.
                    var world=NativeExpeditionGeneratedWorld.Build(room);
                    var map=world.Map;
                    var cabin=NativeCradleCard.CityNightMap(world,200,152);
                    int CabinPixel(V3 p)
                    {
                        int x=(int)Math.Round((p.x-map.bounds.minX)/(map.bounds.maxX-map.bounds.minX)*199);
                        int y=(int)Math.Round((map.bounds.maxZ-p.z)/(map.bounds.maxZ-map.bounds.minZ)*151);
                        return cabin[y*200+x]&0xffffff;
                    }
                    foreach(var door in world.Area.anchors.Where(a=>a.kind=="door"))
                    {
                        var district=world.Blueprint.districts.Single(d=>d.instance==door.building);
                        Check(CabinPixel(new V3(door.x,door.y,door.z))==NativeDeployPresentation.DestinationOf(district.instance,district.theme).Color,seed,"cabin entrance "+door.building);
                    }
                    Check(CabinPixel(map.nav.nodes[map.nav.nodes.Length/2].pos)!=0x091323,seed,"cabin street");
                    bool Supported(string id,V3 p,float r,float h)=>ExpeditionLayoutChecks.Supported(map,p,r,h);
                    var nodes=map.nav.nodes.ToLookup(n=>((int)n.pos.x,(int)n.pos.z));
                    bool Attached(V3 p,float r,float h)
                    {
                        int x=(int)Math.Round(p.x/2)*2,z=(int)Math.Round(p.z/2)*2;
                        for(int dx=-2;dx<=2;dx+=2)
                        for(int dz=-2;dz<=2;dz+=2)
                        {
                            foreach(var n in nodes[(x+dx,z+dz)])
                            {
                            var delta=n.pos-new V3(0,.9f,0)-p;
                            if(delta.Length>3)continue;
                            int steps=Math.Max(1,(int)Math.Ceiling(delta.Length/.25));
                            bool clear=true;
                            for(int k=0;k<=steps&&clear;k++)clear=Supported(map.id,p+delta*(k/(float)steps),r,h);
                            if(clear)return true;
                            }
                        }
                        return false;
                    }
                    foreach(var gate in world.Hub.Gates)
                        Check(Attached(gate.Outside.Position,NativeHordeKinds.HulkRadius,NativeHordeKinds.HulkHeight),seed,"camp route to "+gate.Id);
                    foreach(var row in NativeCamp.Table)
                    {
                        var block=map.blocks.Single(b=>b.id==row.Id);
                        Check(block.pos.x==row.X&&block.pos.z==row.Z&&block.pos.y==row.Y0+row.Sy/2&&
                            block.size.x==row.Sx&&block.size.y==row.Sy&&block.size.z==row.Sz,seed,"generated camp solid "+row.Id);
                    }
                    var machines=world.Area.machines;
                    Check(machines.Length==2&&machines[0].m==0&&machines[0].kind=="chip"&&machines[1].m==1&&machines[1].kind=="med",seed,"camp machine roster");
                    foreach(var m in machines)
                    {
                        Check(m.id=="exp-city:hub-machine:"+m.kind,seed,"hub service id");
                        foreach(var (point,distance) in new[]{(m.front,.7f),(m.dispense,1.3f),(m.chipDrop,2.2f)})
                        {
                            Check((point.Position()-NativeMachineRules.World(m,0,0,distance)).Length<.001f,seed,"hub service point "+m.id);
                            Check(Attached(point.Position(),.45f,1.8f),seed,"walkable hub service "+m.id);
                        }
                    }
                    var caches=world.Area.anchors.Where(a=>a.kind=="chest").ToArray();
                    var approaches=caches.Select(a=>NativeExpeditionGeneratedWorld.CacheApproach(a).Position).ToHashSet();
                    var routed=new HashSet<V3>();
                    bool Reach(string id,V3 from,V3 to,float r,float h)
                    {
                        if(r==NativeHordeKinds.HulkRadius&&h==NativeHordeKinds.HulkHeight&&approaches.Contains(to))
                            routed.Add(to);
                        return Attached(from,r,h)&&Attached(to,r,h);
                    }
                    Check(caches.Length==5,seed,"five Meridian caches");
                    foreach(var cache in caches)
                        Check(!Supported(map.id,new V3(cache.x,cache.y,cache.z),.45f,1.8f),seed,"solid locker obstructs its centre");
                    NativeExpeditionLayout.ValidateGeometry(new ExpeditionLayout{revision=NativeExpeditionGeneratedWorld.Version,areas=new[]{world.Area}},
                        new ConversionContent{maps=new[]{map}},null,Supported,Reach);
                    Check(routed.SetEquals(approaches),seed,"Hulk route checked for each cache approach");
                    var expected=NativeExpeditionHubPlan.LandingField(world.Blueprint.worldSeed);
                    for(int j=0;j<8;j++)
                    {
                        var slot=world.Area.slots[j];
                        Check((slot.Position-NativeExpeditionHubPlan.RampFoot(expected[j]).Position).Length==0&&slot.yaw==expected[j].yaw,seed,"expedition spawn "+j);
                        var old=new V3(j%4*2-3,1.1f,j/4*3+9);
                        var ffa=map.spawns.ffa[j];var team=map.spawns.teams[j/4].points[j%4];
                        Check((ffa.pos-old).Length==0&&ffa.yaw==0&&(team.pos-old).Length==0&&team.yaw==0,seed,"PvP row "+j);
                    }
                }
                catch(Exception e){throw new Exception("World sweep run seed "+seed,e);}
                int done=System.Threading.Interlocked.Increment(ref completed);
                if(done==1||done%100==0)Console.WriteLine("Generated world sweep "+done+"/"+seeds.Length+" at "+clock.Elapsed);
            });
            Console.WriteLine("PASS "+seeds.Length+" generated maps through Validate and ValidateGeometry, Hulk nav and separate PvP spawn rows");
        }
    }
}
