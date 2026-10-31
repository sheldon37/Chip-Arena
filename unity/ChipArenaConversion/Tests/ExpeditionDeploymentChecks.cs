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
        private static string Json(object value)=>JsonSerializer.Serialize(value,new JsonSerializerOptions{IncludeFields=true});
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
        public static void Run()
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
            Console.WriteLine("PASS Expedition deployment: "+count+" checks");
        }
    }
}
