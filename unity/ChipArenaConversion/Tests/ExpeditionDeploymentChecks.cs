using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ChipArena.UnityConversion
{
    internal static class ExpeditionDeploymentChecks
    {
        private static int count;
        private static void Check(bool condition,string why)
        {
            if(!condition)throw new Exception("Expedition deployment: "+why);
            count++;
        }
        private static readonly ExpeditionInstanceTheme[] Pool=
        {
            new ExpeditionInstanceTheme{id="exp-ward-zero",theme="quarantine"},
            new ExpeditionInstanceTheme{id="exp-foundry",theme="industrial"},
            new ExpeditionInstanceTheme{id="exp-transit",theme="rail"},
            new ExpeditionInstanceTheme{id="exp-catacombs",theme="catacombs"},
        };
        private static string Json(object value)=>JsonSerializer.Serialize(value,new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true});
        private static bool Reachable(ExpeditionDeploymentState state,int gx,int gz)
        {
            var roads=new HashSet<string>(state.cells.Where(x=>x.road).Select(x=>x.x+":"+x.z));
            var seen=new HashSet<string>{"0:0"};
            var q=new Queue<(int x,int z)>();q.Enqueue((0,0));
            while(q.Count>0)
            {
                var node=q.Dequeue();
                if(node.x==gx&&node.z==gz)return true;
                foreach(var d in new[]{(1,0),(-1,0),(0,1),(0,-1)})
                {
                    int x=node.x+d.Item1,z=node.z+d.Item2;string id=x+":"+z;
                    if(roads.Contains(id)&&seen.Add(id))q.Enqueue((x,z));
                }
            }
            return false;
        }
        public static void Run(string root)
        {
            count=0;
            var a=new NativeExpeditionDeployment("station","host",12345);
            Check(!a.Ready&&!a.Deployed,"fresh station must roll first");
            Check(!a.RollInstances("guest",Pool),"guest cannot roll instances");
            Check(!a.RollMutators("host",new[]{"darkness","storm","surge"}),"mutators require an instance roll");
            Check(!a.RollInstances("host",new[]{Pool[0],Pool[0],Pool[1]}),"duplicate instance ids rejected");
            Check(a.RollInstances("host",Pool),"host rolls instances");
            var initial=a.Snapshot();
            Check(initial.instances.Length==3&&initial.instances.Distinct().Count()==3,"three unique instances");
            Check(initial.districts.Select(d=>d.side).SequenceEqual(new[]{"east","west","south"}),"assigned entrances");
            Check(initial.cells.Length==NativeExpeditionDeployment.GridWidth*NativeExpeditionDeployment.GridHeight,"grid size");
            Check(initial.cells.All(c=>c.influences.Length==4&&c.influences.All(v=>v>=0&&v<=100)&&c.influences.Sum()==100),"normalized themes");
            var center=initial.cells.Single(c=>c.x==0&&c.z==0);
            Check(center.sanctuary&&center.road&&center.influences[0]==100,"neutral central sanctuary");
            for(int i=0;i<3;i++)
            {
                var district=initial.districts[i];
                var portal=initial.cells.Single(c=>c.x==district.x&&c.z==district.z);
                Check(portal.road&&portal.influences[i+1]>60&&Reachable(initial,district.x,district.z),"themed reachable entrance "+i);
            }
            var b=new NativeExpeditionDeployment("station","host",12345);
            Check(b.RollInstances("host",Pool)&&Json(initial)==Json(b.Snapshot()),"same seed identical regardless of client");
            Check(!a.RollMutators("guest",new[]{"surge"}),"guest cannot roll mutators");
            Check(a.RollMutators("host",new[]{"surge","storm","darkness","brittle"}),"optional mutator roll");
            var mutable=a.Snapshot();
            Check(mutable.mutators.Length==3&&mutable.mutators.Distinct().Count()==3,"three unique mutators");
            Check(mutable.worldSeed==initial.worldSeed&&Json(mutable.cells)==Json(initial.cells),"mutator roll cannot change world");
            Check(a.SkipMutators("host")&&a.Snapshot().mutators.Length==0,"skip optional mutators");
            Check(a.RollMutators("host",new[]{"surge","storm","darkness","brittle"}),"reroll before deploy");
            Check(!a.Deploy("guest",true)&&!a.Deploy("host",false),"host and trusted ready signal required");
            Check(a.Deploy("host",true)&&a.Deployed&&!a.Ready,"deploy freezes state");
            string frozen=Json(a.Snapshot());
            Check(!a.RollInstances("host",Pool)&&!a.RollMutators("host",new[]{"surge"})&&!a.SkipMutators("host")&&!a.Deploy("host",true),"post-deployment machines locked");
            Check(Json(a.Snapshot())==frozen,"rejected commands leave snapshot intact");
            var detached=a.Snapshot();detached.instances[0]="changed";detached.districts[0].theme="changed";detached.cells[0].influences[0]=-10;
            Check(Json(a.Snapshot())==frozen,"nested snapshot deep copy");
            var different=new NativeExpeditionDeployment("station","host",54321);
            Check(different.RollInstances("host",Pool)&&different.Snapshot().worldSeed!=initial.worldSeed,"new seed changes the world seed");
            RoomChecks(root);
            Console.WriteLine("PASS Expedition deployment: "+count+" checks");
        }
        private static void RoomChecks(string root)
        {
            var options=new JsonSerializerOptions{IncludeFields=true};
            string Read(string name)=>System.IO.File.ReadAllText(System.IO.Path.Combine(root,"Assets/Conversion/Resources",name));
            string mapsJson=Read("UnityMaps.json"),layoutJson=Read("ExpeditionLayout.json");
            var maps=JsonSerializer.Deserialize<UnityMapContent>(mapsJson,options);
            var content=new ConversionContent{maps=maps.maps,contentRevision=NativeMapRules.Revision(mapsJson)};
            var layout=NativeExpeditionLayout.Parse(layoutJson);layout.compatibility=NativeExpeditionLayout.Compatibility(layoutJson,content);
            var room=new ExpeditionDeploymentRoom("room","host",123,"black-siren",layout,content);
            Check(!room.Execute("guest",0,ExpeditionDeploymentIntent.RollInstances),"room rejects guest");
            Check(room.Execute("host",0,ExpeditionDeploymentIntent.RollInstances),"room rolls validated live pool");
            Check(!room.Execute("host",0,ExpeditionDeploymentIntent.RollInstances),"duplicate roll rejected by revision");
            var generated=NativeExpeditionGeneratedWorld.Build(room);
            Check(generated.Map.blocks.Length>100&&generated.Map.nav.nodes.Length>1000,"roll creates real collision and navigation");
            Check(!NativeMapRules.ArenaSelectable(generated.Map)&&!generated.Map.rotation,"generated map never enters PvP");
            Check(generated.Area.doors.Length==3&&generated.Hub.Gates.Length==4&&generated.Area.gates.Single().opens=="final","three instances and separate final court");
            Check(NativeExpeditionGeneratedWorld.Build(room).Compatibility==generated.Compatibility,"deterministic generated geometry receipt");
            Check(generated.Hub.Routes.Length==3&&generated.Hub.Routes.All(r=>r.Path>=r.Straight),"street routes and measured distances");
            var composed=NativeExpeditionGeneratedWorld.ComposeLayout(generated,layoutJson);
            Check(composed.Area(composed.world).map==generated.Map.id&&composed.instances.SequenceEqual(room.Snapshot().instances),"generated manifest selects the rolled roster");
            foreach(string id in composed.instances)Check(Json(composed.Area(id))==Json(layout.Area(id)),"authored interior remains unchanged");
            var migratedWorld=NativeExpeditionGeneratedWorld.Build(ExpeditionDeploymentRoom.Restore(room.Ticket,"next",layout,content));
            Check(migratedWorld.Compatibility==generated.Compatibility,"host migration reconstructs identical map");
            var culture=System.Globalization.CultureInfo.CurrentCulture;
            try{var changed=(System.Globalization.CultureInfo)culture.Clone();changed.NumberFormat.NegativeSign="minus";changed.NumberFormat.NumberDecimalSeparator=",";System.Globalization.CultureInfo.CurrentCulture=changed;
                Check(NativeExpeditionGeneratedWorld.Build(room).Compatibility==generated.Compatibility,"culture-independent generation");}
            finally{System.Globalization.CultureInfo.CurrentCulture=culture;}

            foreach(uint seed in new uint[]{99,1234,98765,uint.MaxValue}.Concat(Enumerable.Range(0,64).Select(i=>(uint)i)))
            {
                var seedRoom=new ExpeditionDeploymentRoom("seed-room","host",seed,"black-siren",layout,content);
                Check(seedRoom.Execute("host",0,ExpeditionDeploymentIntent.RollInstances),"seed roll");
                var world=NativeExpeditionGeneratedWorld.Build(seedRoom);
                foreach(var door in world.Area.doors)
                {
                    foreach(var point in door.returns.Concat(new[]{door.approach}))Check(ExpeditionLayoutChecks.Supported(world.Map,point.Position,NativeHordeKinds.HulkRadius,NativeHordeKinds.HulkHeight),"independent entrance collision probe");
                    for(int i=0;i<8;i++){double angle=i*Math.PI/4;Check(ExpeditionLayoutChecks.Supported(world.Map,new V3(door.join.x+3*(float)Math.Cos(angle),0,door.join.z+3*(float)Math.Sin(angle)),.45f,1.8f),"join circle collision probe");}
                }
                Check(world.Map.blocks.Length<500&&world.Map.nav.nodes.Length<12000,"bounded generated map");
            }


            Check(room.Snapshot().instances.OrderBy(x=>x).SequenceEqual(layout.instances.OrderBy(x=>x)),"only authored roster selected");
            Check(!room.Execute("host",1,ExpeditionDeploymentIntent.RollMutators),"unapproved mutators unavailable");
            Check(room.Execute("host",1,ExpeditionDeploymentIntent.SkipMutators),"no-mutator start valid");
            var wire=JsonSerializer.Deserialize<ExpeditionDeploymentTicket>(Json(room.Ticket),options);
            var receiver=new ExpeditionDeploymentReceiver();
            Check(receiver.Accept(wire,layout,content),"guest restores compact ticket");
            Check(Json(receiver.Snapshot)==Json(room.Snapshot()),"guest rebuild exactly matches authority");
            var migrated=ExpeditionDeploymentRoom.Restore(wire,"new-host",layout,content);
            Check(!migrated.Execute("host",1,ExpeditionDeploymentIntent.RollInstances)&&
                migrated.Execute("new-host",1,ExpeditionDeploymentIntent.RollInstances),"only reassigned authority can continue");
            Check(room.Execute("host",1,ExpeditionDeploymentIntent.RollInstances)&&Json(room.Snapshot())==Json(migrated.Snapshot()),"migration preserves draw stream");
            Check(receiver.Accept(room.Ticket,layout,content)&&!receiver.Accept(wire,layout,content),"stale snapshot cannot rewind");
            Check(!room.Execute("host",2,ExpeditionDeploymentIntent.Deploy,true),"blueprint alone cannot deploy");
            string digest=new string('a',64);
            Check(!room.Execute("host",2,ExpeditionDeploymentIntent.Deploy,false,digest),"readiness enforced");
            Check(room.Execute("host",2,ExpeditionDeploymentIntent.Deploy,true,digest),"validated generation locks before run");
            Check(receiver.Accept(room.Ticket,layout,content),"guest receives locked run");
            var frozen=Json(room.Snapshot());
            var resumed=ExpeditionDeploymentRoom.Restore(room.Ticket,"next-host",layout,content);
            Check(Json(resumed.Snapshot())==frozen&&!resumed.Execute("next-host",3,ExpeditionDeploymentIntent.RollInstances),"deployed handoff permanently locked");
            Check(!receiver.Accept(wire,layout,content),"reconnect cannot unlock deployment");
            var bad=room.Ticket;bad.compatibility=new string('b',64);
            Check(!new ExpeditionDeploymentReceiver().Accept(bad,layout,content),"content mismatch rejected");
            bad=room.Ticket;bad.instanceRolls=int.MaxValue;bad.revision=int.MaxValue;
            Check(!new ExpeditionDeploymentReceiver().Accept(bad,layout,content),"overflow ticket rejected without replaying rolls");
            bad=room.Ticket;bad.generatedCompatibility=null;
            Check(!new ExpeditionDeploymentReceiver().Accept(bad,layout,content),"deployed snapshot requires generated compatibility");
            var copy=receiver.Snapshot;copy.cells[0].influences[0]=-5;
            Check(Json(receiver.Snapshot)==frozen,"receiver snapshots detached");
            var late=new ExpeditionDeploymentReceiver();
            Check(late.Accept(room.Ticket,layout,content)&&Json(late.Snapshot)==frozen,"late join same seed and locked selection");
            var broken=layout.Area(layout.instances[0]);string old=broken.map;broken.map="missing";
            bool rejected=false;try{new ExpeditionDeploymentRoom("bad","host",1,"black-siren",layout,content);}catch(ArgumentException){rejected=true;}
            broken.map=old;Check(rejected,"missing authored instance fails catalog");
        }
    }
}
