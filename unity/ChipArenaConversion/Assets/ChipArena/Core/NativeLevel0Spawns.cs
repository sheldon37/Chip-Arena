using System;
using System.Collections.Generic;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // Distance through doors/corridors, never through the maze's partitions.
    public static class NativeLevel0Spawns
    {
        private static readonly (int x,int z)[] Steps={(1,0),(-1,0),(0,1),(0,-1)};
        public static int Cell(BackroomsLayout l,V3 p)
        {int x=(int)Math.Floor(p.x/BackroomsLayout.Cell),z=(int)Math.Floor(p.z/BackroomsLayout.Cell);return l.Inside(x,z)?l.Index(x,z):-1;}
        public static int[] Routes(BackroomsLayout l,IEnumerable<V3> players)
        {
            if(l==null)return null;var distances=Enumerable.Repeat(-1,l.Floor.Length).ToArray();var queue=new int[distances.Length];int head=0,tail=0;
            foreach(var p in players){int c=Cell(l,p);if(NativeBackroomsGenerator.Walkable(l,c)&&distances[c]<0){distances[c]=0;queue[tail++]=c;}}
            if(tail==0)return null;
            while(head<tail){int c=queue[head++];foreach(var step in Steps)if(NativeBackroomsGenerator.Link(l,c,step.x,step.z)){int n=c+step.x+step.z*l.Width;if(distances[n]<0){distances[n]=distances[c]+1;queue[tail++]=n;}}}return distances;
        }
        public static float Distance(BackroomsLayout l,int[] routes,V3 point)
        {int c=Cell(l,point);return routes==null||c<0||routes[c]<0?float.PositiveInfinity:routes[c]*BackroomsLayout.Cell;}
        public static bool Retire(bool dormant,bool damaged,bool observed,float age,float routeDistance)
        =>dormant&&!damaged&&!observed&&age>=20&&routeDistance>48;
    }
}
