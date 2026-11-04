using System;
using System.Linq;
using ChipArena.UnityConversion;
internal static class Level0SpawnChecks
{
    public static void Run()
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception("Level 0 spawns: "+why);}
        for(int seed=0;seed<32;seed++)
        {
            var g=NativeBackroomsGenerator.Build(seed,true);var l=g.Layout;var anchors=NativeBackroomsKinds.Anchors(g);
            Check(anchors.Length<=256&&anchors.Select(x=>x.entry.e).Distinct().Count()==anchors.Length,"wire table bounded/unique");
            var walls=anchors.Where(a=>a.allow.Contains("faceling")).ToArray();
            for(int z=0;z<2;z++)for(int x=0;x<2;x++)Check(walls.Count(a=>(a.entry.@exit.x>=l.Width*BackroomsLayout.Cell/2?1:0)==x&&(a.entry.@exit.z>=l.Depth*BackroomsLayout.Cell/2?1:0)==z)>=12,"facelings distributed in every quadrant, seed "+seed);
            var routes=NativeLevel0Spawns.Routes(l,new[]{g.Entry});var expected=NativeBackroomsGenerator.Distances(l,l.Index(l.EntryX,l.EntryZ));
            Check(routes.SequenceEqual(expected),"route weights follow actual nav doors");
            var both=NativeLevel0Spawns.Routes(l,new[]{g.Entry,g.Exit});var exit=NativeBackroomsGenerator.Distances(l,l.Index(l.ExitX,l.ExitZ));
            for(int c=0;c<routes.Length;c++)Check(both[c]==(routes[c]<0?exit[c]:exit[c]<0?routes[c]:Math.Min(routes[c],exit[c])),"split-party nearest path");
            Check(NativeLevel0Spawns.Distance(l,routes,new V3(-1,0,0))==float.PositiveInfinity,"outside excluded");
        }
        var maze=new BackroomsLayout("test",0,4,4);maze.Set(1,1,1,0,BackroomsLayout.Wall);
        var path=NativeLevel0Spawns.Routes(maze,new[]{NativeBackroomsGenerator.Position(maze,5)});
        Check(path[6]==3,"adjacent cell behind wall uses detour instead of Euclidean distance");
        Check(NativeLevel0Spawns.Retire(true,false,false,30,60),"unengaged far sleeper frees a slot");
        Check(!NativeLevel0Spawns.Retire(false,false,false,30,60),"awake enemy retained");
        Check(!NativeLevel0Spawns.Retire(true,true,false,30,60),"damaged enemy retained");
        Check(!NativeLevel0Spawns.Retire(true,false,true,30,60),"visible enemy retained");
        Check(!NativeLevel0Spawns.Retire(true,false,false,19,60),"new spawn retained");
        Check(!NativeLevel0Spawns.Retire(true,false,false,30,40),"near sleeper retained");
        Console.WriteLine("PASS Level 0 encounter placement: "+count+" checks across 32 seeds");
    }
}
