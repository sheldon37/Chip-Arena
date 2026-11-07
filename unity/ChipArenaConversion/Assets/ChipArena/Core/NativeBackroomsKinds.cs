using System;
using System.Collections.Generic;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // Append-only network IDs. Authority tuning; presentation consumes the same stats and finite Zone frame.
    public static class NativeBackroomsKinds
    {
        public static bool Level0(string map)=>map==NativeBackroomsGenerator.InstanceMap;
        public static bool Pool(string map)=>map==NativePoolroomsGenerator.InstanceMap;
        public static bool Active(string map)=>Level0(map)||Pool(map);
        public static string SpawnKind(string map,string kind)=>Level0(map)&&kind=="hulk"?"brute":NativePoolroomsGenerator.SpawnKind(map,kind);
        public static bool Own(string id)=>id=="faceling"||id=="hound"||id=="smiler"||id=="drowned"||id=="tiler"||id=="lifeguard";
        public static readonly HordeZoneKind[] Kinds={
            Kind("faceling","Faceling",false,0,60,4,1.4f,4.4f,1.8f,.32f,.9f,1.7f,.4f),
            Kind("hound","Hound",false,1,30,3,6,6,1.8f,.3f,1.1f,.85f,.35f),
            Kind("smiler","Smiler",false,2,600,30,2,2,2.6f,.6f,2.5f,2.2f,.4f),
            Kind("drowned","Drowned",true,0,72,6,1,1,2.2f,.4f,1.4f,1.7f,.42f),
            Kind("tiler","Tiler",true,1,23,3,5.5f,5.5f,2.5f,.4f,1.2f,.65f,.3f),
            Kind("lifeguard","Lifeguard",true,2,720,15,1.4f,2.2f,2.6f,.5f,2,2.2f,.42f)};
        private static HordeZoneKind Kind(string id,string name,bool pool,int body,float hp,float damage,float walk,float run,float reach,float hit,float cycle,float height,float radius)
            =>new HordeZoneKind{Shipped=true,Id=id,Name=name,Zone=pool?NativePoolroomsGenerator.InstanceMap:NativeBackroomsGenerator.InstanceMap,Body=body,Height=height,Radius=radius,Scale=(id=="lifeguard"?2.6f:id=="hound"?1.7f:height)/1.7f,Mass=body==2?2:1,Attack=NativeHordeKinds.Attack.Swipe,
                Proposed=new PveAddStats{id=id,attack="swipe",hp=hp,damage=damage,walk=walk,run=run,reach=reach,startRange=reach-.25f,hit=hit,cycle=cycle,lunge=.3f,impulse=60,reward=body==2?100:20,limbFrac=.45f,headFrac=.5f,stagger=.4f}};
        public static HordeZoneKind[] For(string map)=>Kinds.Where(k=>k.Zone==map).ToArray();
        public static int Wave(string map,int progress,int target)=>Pool(map)?progress<target*.15?1:progress<target*.4?2:progress<target*.85?3:4:NativeZoneCombat.Wave(progress,target);
        public static string Intro(string map,int wave)=>Level0(map)?new[]{"faceling","hound","smiler","hound"}[wave-1]:new[]{"drowned","tiler","lifeguard","tiler"}[wave-1];
        public static int Pack(string kind)=>kind=="hound"||kind=="cinderling"?3:kind=="tiler"?3:1;
        public static int Cap(string kind)=>kind=="smiler"||kind=="lifeguard"?1:kind=="hound"?6:kind=="tiler"?8:12;
        public static NumberValue[] Mix(string map,int wave)
        {
            string[] ids=Level0(map)?wave<2?new[]{"faceling","runner"}:wave==2?new[]{"faceling","hound","runner"}:wave==3?new[]{"faceling","smiler","brute"}:new[]{"faceling","hound","smiler","runner","brute"}:
                wave<2?new[]{"drowned","runner"}:wave==2?new[]{"drowned","tiler","spitter"}:new[]{"drowned","tiler","lifeguard","runner","spitter"};
            return ids.Select((id,i)=>new NumberValue{name=id,value=i==0?6:id=="smiler"||id=="lifeguard"?.3:1}).ToArray();
        }
        public static bool Dark(BackroomsLayout l,V3 p)
        {if(l==null)return false;int x=(int)Math.Floor(p.x/BackroomsLayout.Cell),z=(int)Math.Floor(p.z/BackroomsLayout.Cell);if(!l.Inside(x,z))return false;int c=l.Index(x,z);return NativeBackroomsGenerator.Walkable(l,c)&&(l.Circuits[l.Circuit[c]]==BackroomsLayout.Dead||l.Circuits[l.Circuit[c]]==BackroomsLayout.Flicker);}
        public static bool DarkPath(BackroomsLayout l,V3 from,V3 to)
        {int n=Math.Max(1,(int)Math.Ceiling((to-from).Length/.2f));for(int i=0;i<=n;i++)if(!Dark(l,from+(to-from)*(i/(float)n)))return false;return true;}
        private static ExpeditionSpawnLayout Spawn(int i,string label,V3 p,float yaw,params string[] kinds)=>new ExpeditionSpawnLayout{allow=kinds,entry=new PveEntry{e=i,id=label+"-"+i,kind="door",yaw=yaw,from=new PvePoint{x=p.x,y=p.y,z=p.z},@exit=new PvePoint{x=p.x,y=p.y,z=p.z},clear=1,allowed=kinds}};
        public static ExpeditionSpawnLayout[] Anchors(BackroomsGenerated g)
        {
            var l=g.Layout;var list=new List<ExpeditionSpawnLayout>();var machines=NativeInstanceMachines.Cells(g.Machines);
            foreach(int c in g.Enemies)list.Add(Spawn(list.Count,"br-roam",NativeBackroomsGenerator.Position(l,c),0,"runner","brute","hound"));
            var walls=new List<ExpeditionSpawnLayout>();var darks=new List<ExpeditionSpawnLayout>();for(int c=0;c<l.Floor.Length;c++)
            {
                if(!NativeBackroomsGenerator.Walkable(l,c)||machines.Contains(c))continue;var p=NativeBackroomsGenerator.Position(l,c);if(g.Players.Any(a=>(NativeBackroomsGenerator.Position(l,a)-p).Length<12)||(p-g.Exit).Length<4)continue;
                if(Dark(l,p))darks.Add(Spawn(0,"br-dark",p,0,"smiler"));
                int x=c%l.Width,z=c/l.Width;foreach(var d in new[]{(1,0),(-1,0),(0,1),(0,-1)})if(l.Between(x,z,d.Item1,d.Item2)==BackroomsLayout.Wall)
                {p+=new V3(d.Item1,0,d.Item2)*(BackroomsLayout.Cell/2-.65f);walls.Add(Spawn(0,"br-wall",p,(float)Math.Atan2(d.Item1,d.Item2),"faceling"));break;}
            }
            // Sample the complete maze, not its first rows; reserve room in the 256-entry wire table.
            foreach(var set in new[]{walls,darks})
            {
                int take=Math.Min(set.Count,ReferenceEquals(set,walls)?160:64);
                for(int i=0;i<take;i++){var entry=set[i*set.Count/take];entry.entry.e=list.Count;entry.entry.id+=":"+list.Count;list.Add(entry);}
            }
            return list.ToArray();
        }
        public static ExpeditionSpawnLayout[] Anchors(PoolroomsGenerated g)
        {
            var l=g.Layout;var list=new List<ExpeditionSpawnLayout>();var machines=NativeInstanceMachines.Cells(g.Machines);
            foreach(int c in g.Enemies)list.Add(Spawn(list.Count,"pool-roam",NativePoolroomsGenerator.Position(l,c),0,"runner","spitter","brute"));
            // Interleave anchor types so the bounded list cannot fill with one terrain class.
            var deep=new List<ExpeditionSpawnLayout>();var tiles=new List<ExpeditionSpawnLayout>();var guards=new List<ExpeditionSpawnLayout>();
            for(int c=0;c<l.Floor.Length;c++)
            {
                var p=NativePoolroomsGenerator.Position(l,c);if(machines.Contains(c)||g.Players.Any(a=>(NativePoolroomsGenerator.Position(l,a)-p).Length<12)||(p-g.Exit).Length<4)continue;
                int x=c%l.Width,z=c/l.Width;
                if(l.Floor[c]==PoolroomsLayout.Deep)deep.Add(Spawn(0,"pool-deep",p,0,"drowned"));
                if(l.Floor[c]!=PoolroomsLayout.Deck)continue;
                foreach(var d in new[]{(1,0),(-1,0),(0,1),(0,-1)})if(l.Between(x,z,d.Item1,d.Item2)==PoolroomsLayout.Wall)
                {tiles.Add(Spawn(0,"pool-tile",p+new V3(d.Item1,0,d.Item2)*.3f,(float)Math.Atan2(-d.Item1,-d.Item2),"tiler"));break;}
                if(new[]{(1,0),(-1,0),(0,1),(0,-1)}.Any(d=>l.Passable(x,z,d.Item1,d.Item2)&&l.Floor[l.Index(x+d.Item1,z+d.Item2)]==PoolroomsLayout.Deep))guards.Add(Spawn(0,"pool-edge",p,0,"lifeguard"));
            }
            foreach(var set in new[]{deep,tiles,guards})foreach(var a in set.Take(40)){a.entry.e=list.Count;a.entry.id+=":"+list.Count;list.Add(a);}
            return list.ToArray();
        }
    }
}
