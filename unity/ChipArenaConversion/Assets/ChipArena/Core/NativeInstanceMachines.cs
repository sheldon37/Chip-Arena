using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The chip, recruit and mending machines in the generated instances (docs/EXPEDITION-INSTANCE-MACHINES.md): the Backrooms and the Poolrooms were the only instances with none, so a run there had nowhere to spend. Every peer rebuilds the spots
    // from the seed with the rest of the level. The authority needs nothing new: a machine on the area's list is served as the authored ones are (NativeExpeditionLayout.BindContent, TryExpeditionInteract). Each stands with its back to a real wall,
    // facing an open cell, along the way from the door to the exit: chip first, then recruit, mending, and a second chip before the end.
    public struct InstanceMachineSpot
    {
        public int Cell,Dx,Dz;public string Kind;
        public InstanceMachineSpot(int cell,int dx,int dz,string kind){Cell=cell;Dx=dx;Dz=dz;Kind=kind;}
    }
    public static class NativeInstanceMachines
    {
        /// <summary>What stands where along the route (a share of the way from the door to the far end).</summary>
        public static readonly (string kind,float share)[] Route={("chip",.26f),("recruit",.48f),("med",.68f),("chip",.88f)};
        /// <summary>The machine's depth behind its spot and the air left behind its collider (the block: NativeExpeditionLayout.BindContent, .7 m behind the spot).</summary>
        public const float Behind=.7f,Air=.02f;
        /// <summary>How far apart the machines keep, and how far from the door and the exit.</summary>
        public const float Apart=14,FromDoor=10,FromExit=7;
        /// <summary>The spot of a machine with its back to the wall on side (dx, dz) of a cell whose centre is `centre`: `cell` the cell's size, `thickness` the wall's.</summary>
        public static V3 SpotOf(V3 centre,int dx,int dz,float cell,float thickness)
        {
            float back=cell/2-thickness/2-Behind-Air;return new V3(centre.x+dx*back,centre.y,centre.z+dz*back);
        }
        /// <summary>The area's machine list from the chosen spots: the model's front toward the open cell, as the authored ones.</summary>
        public static PveMachine[] Machines(string area,InstanceMachineSpot[] spots,Func<int,V3> centre,float cell,float thickness)
        {
            var list=new PveMachine[spots.Length];
            for(int i=0;i<spots.Length;i++)
            {
                var s=spots[i];var p=SpotOf(centre(s.Cell),s.Dx,s.Dz,cell,thickness);float fx=-s.Dx,fz=-s.Dz;
                PvePoint Ahead(float by)=>new PvePoint{x=p.x+fx*by,y=p.y,z=p.z+fz*by};
                list[i]=new PveMachine{m=i,id=area+":machine:"+i,kind=s.Kind,x=p.x,y=p.y,z=p.z,yaw=(float)Math.Atan2(fx,fz),front=Ahead(.7f),dispense=Ahead(1.3f),chipDrop=Ahead(2.2f)};
            }
            return list;
        }
        /// <summary>The cells holding a machine, for the spawn anchors and the navigation to keep out of.</summary>
        public static HashSet<int> Cells(InstanceMachineSpot[] spots){var s=new HashSet<int>();if(spots!=null)foreach(var m in spots)s.Add(m.Cell);return s;}
        private static readonly (int x,int z)[] Sides={(1,0),(0,1),(-1,0),(0,-1)};
        // The route's picks: for each share, the valid (cell, wall side) nearest that far along, the lit and the sheltered ones first. `valid` says whether a machine can stand there; `score` adds to the distance from the target.
        private static InstanceMachineSpot[] Pick(int cells,int[] distance,Func<int,V3> centre,Func<int,int,int,bool> valid,Func<int,int,int,float> score)
        {
            int far=distance.Max();var chosen=new List<InstanceMachineSpot>();
            foreach(var (kind,share) in Route){var one=PickOne(cells,distance,centre,valid,score,chosen,far*share,kind);if(one.HasValue)chosen.Add(one.Value);}
            return chosen.ToArray();
        }
        private static float Distance(V3 a,V3 b){float dx=a.x-b.x,dz=a.z-b.z;return (float)Math.Sqrt(dx*dx+dz*dz);}
        /// <summary>Level 0's picks. A 3.2 m cell leaves its centre clear of a machine against its wall (the block reaches .9 m in, the centre is 1.51 m from the wall's face), so the navigation stays as it is.</summary>
        public static InstanceMachineSpot[] Backrooms(BackroomsLayout l,int[] players,int[] enemies,int entry,int exit)
        {
            var distance=NativeBackroomsGenerator.Distances(l,entry);var no=new HashSet<int>(players);no.UnionWith(enemies);no.Add(exit);var door=NativeBackroomsGenerator.Position(l,entry);var end=NativeBackroomsGenerator.Position(l,exit);
            bool Valid(int c,int dx,int dz)
            {
                int x=c%l.Width,z=c/l.Width;if(no.Contains(c)||!NativeBackroomsGenerator.Walkable(l,c)||l.Between(x,z,dx,dz)!=BackroomsLayout.Wall)return false;
                // A real wall behind it, an open cell in front of it, room either side.
                if(!NativeBackroomsGenerator.Link(l,c,-dx,-dz))return false;var at=NativeBackroomsGenerator.Position(l,c);return Distance(at,door)>=FromDoor&&Distance(at,end)>=FromExit;
            }
            float Score(int c,int dx,int dz)
            {
                bool lit=l.Light[c]&&l.Circuits[l.Circuit[c]]!=BackroomsLayout.Dead;int links=Sides.Count(d=>NativeBackroomsGenerator.Link(l,c,d.x,d.z));
                // A lit cell reads from down the corridor; a dead end or a room's edge is out of the way of a running fight.
                return (lit?0:2.5f)+(links==2?1.5f:0);
            }
            return Pick(l.Width*l.Depth,distance,c=>NativeBackroomsGenerator.Position(l,c),Valid,Score);
        }
        /// <summary>The Poolrooms' picks. A 2 m cell's centre is inside a machine against its wall, so a machine takes its cell off the navigation (the generator's Map): only a deck corner with deck in front, and only if the level's own
        /// navigation (`nav`, built without the machines) still reaches everything it reached without that cell and the ones already taken.</summary>
        public static InstanceMachineSpot[] Poolrooms(PoolroomsLayout l,int[] players,int[] enemies,int entry,int exit,MapNav nav)
        {
            var distance=NativePoolroomsGenerator.Distances(l,entry);var no=new HashSet<int>(players);no.UnionWith(enemies);no.Add(exit);var door=NativePoolroomsGenerator.Position(l,entry);var end=NativePoolroomsGenerator.Position(l,exit);
            bool Deck(int x,int z)=>l.Inside(x,z)&&l.Floor[l.Index(x,z)]==PoolroomsLayout.Deck;
            var graph=new NavGraph(nav);string start=NavGraph.Id(entry);var taken=new HashSet<int>();var whole=new Dictionary<int,bool>();int count=0;
            bool Valid(int c,int dx,int dz)
            {
                int x=c%l.Width,z=c/l.Width;if(no.Contains(c)||l.Floor[c]!=PoolroomsLayout.Deck||l.Between(x,z,dx,dz)!=PoolroomsLayout.Wall)return false;
                if(!l.Passable(x,z,-dx,-dz)||!Deck(x-dx,z-dz))return false;
                // A corner: a wall on a side too, so that nobody's way along the wall runs through it.
                bool corner=l.Between(x,z,dz,dx)==PoolroomsLayout.Wall||l.Between(x,z,-dz,-dx)==PoolroomsLayout.Wall;if(!corner)return false;
                var at=NativePoolroomsGenerator.Position(l,c);if(Distance(at,door)<FromDoor||Distance(at,end)<FromExit)return false;
                // The picks so far decide what going without this cell costs: asked again once one is taken.
                if(whole.Count>0&&count!=taken.Count){whole.Clear();}count=taken.Count;
                if(!whole.TryGetValue(c,out bool keeps))whole[c]=keeps=graph.KeepsAll(start,taken,c);return keeps;
            }
            float Score(int c,int dx,int dz)=>l.Light[c]?0:2.5f;
            // Pick chooses one at a time: each chosen cell comes off before the next is asked about.
            var chosen=new List<InstanceMachineSpot>();int far=distance.Max();
            foreach(var (kind,share) in Route)
            {
                var one=PickOne(l.Floor.Length,distance,c=>NativePoolroomsGenerator.Position(l,c),Valid,Score,chosen,far*share,kind);if(one.HasValue){chosen.Add(one.Value);taken.Add(one.Value.Cell);}
            }
            return chosen.ToArray();
        }
        private static InstanceMachineSpot? PickOne(int cells,int[] distance,Func<int,V3> centre,Func<int,int,int,bool> valid,Func<int,int,int,float> score,List<InstanceMachineSpot> chosen,float target,string kind)
        {
            float best=float.MaxValue;InstanceMachineSpot? pick=null;
            for(int c=0;c<cells;c++)
            {
                if(distance[c]<0)continue;var at=centre(c);bool close=false;foreach(var m in chosen)if(Distance(centre(m.Cell),at)<Apart){close=true;break;}if(close)continue;
                foreach(var d in Sides){if(!valid(c,d.x,d.z))continue;float s=Math.Abs(distance[c]-target)+score(c,d.x,d.z);if(s<best){best=s;pick=new InstanceMachineSpot(c,d.x,d.z,kind);}}
            }
            return pick;
        }
        // The level's navigation by cell (node ids "pool:cell:tread").
        private sealed class NavGraph
        {
            private readonly Dictionary<string,List<string>> links=new Dictionary<string,List<string>>();
            public static string Id(int cell)=>"pool:"+cell.ToString(System.Globalization.CultureInfo.InvariantCulture)+":0";
            private static int CellOf(string id){int a=id.IndexOf(':')+1,b=id.IndexOf(':',a);return int.Parse(id.Substring(a,b-a),System.Globalization.CultureInfo.InvariantCulture);}
            public NavGraph(MapNav nav){foreach(var n in nav.nodes)links[n.id]=new List<string>();foreach(var k in nav.links){links[k.a].Add(k.b);links[k.b].Add(k.a);}}
            private int Reached(string start,HashSet<int> skip,int also,out HashSet<string> seen)
            {
                seen=new HashSet<string>{start};var q=new Queue<string>();q.Enqueue(start);
                while(q.Count>0)foreach(var o in links[q.Dequeue()]){int c=CellOf(o);if(c==also||skip.Contains(c))continue;if(seen.Add(o))q.Enqueue(o);}
                return seen.Count;
            }
            /// <summary>Whether, with `taken` off the navigation, also taking `cell` leaves everything reached but that cell's own nodes.</summary>
            public bool KeepsAll(string start,HashSet<int> taken,int cell)
            {
                Reached(start,taken,-1,out var before);if(!before.Contains(Id(cell)))return false;int own=0;foreach(var id in before)if(CellOf(id)==cell)own++;
                return Reached(start,taken,cell,out _)==before.Count-own;
            }
        }
    }
}
