using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // Small, bounded footprint graph. Every connection is swept against the live area scene,
    // including fallen scenery. A blocked waypoint is never retained indefinitely.
    public sealed class NativeStationRoute
    {
        private const int Columns=25,Rows=7,Count=Columns*Rows;
        private readonly int[] parent=new int[Count],queue=new int[Count],path=new int[Count];
        private int length,index;
        private float y;
        public void Clear(){length=index=0;}
        public static bool Contains(Vector3 p,int phase)=>Math.Abs(p.X)<=24&&p.Z>=(phase>=2?-6:0)&&p.Z<=6;
        private Vector3 Point(int i)=>new Vector3(-24+2*(i%Columns),y,-6+2*(i/Columns));
        public bool Plan(Vector3 at,Vector3 target,int phase,Func<Vector3,Vector3,bool> clear)
        {
            Clear();y=at.Y;int read=0,write=0,best=-1;float score=float.PositiveInfinity;
            for(int i=0;i<Count;i++)parent[i]=-1;
            // Several visible seed nodes avoid snapping the actor through an obstacle.
            for(int i=0;i<Count;i++)if(Contains(Point(i),phase)&&Vector3.DistanceSquared(at,Point(i))<=9&&clear(at,Point(i)))
            {parent[i]=i;queue[write++]=i;}
            while(read<write)
            {
                int n=queue[read++];Vector3 pos=Point(n);
                float d=Math.Abs(Vector2.Distance(new Vector2(pos.X,pos.Z),new Vector2(target.X,target.Z))-7.5f);
                // Prefer the shortest visited path when two destinations have equal reach.
                if(d<score-.01f){score=d;best=n;}
                for(int side=0;side<4;side++)
                {
                    int next=side==0?n-1:side==1?n+1:side==2?n-Columns:n+Columns;
                    if(next<0||next>=Count||(side<2&&next/Columns!=n/Columns)||parent[next]>=0||!Contains(Point(next),phase))continue;
                    if(!clear(pos,Point(next)))continue;
                    parent[next]=n;queue[write++]=next;
                }
            }
            if(best<0)return false;
            do{path[length++]=best;int prev=parent[best];if(prev==best)break;best=prev;}while(length<Count);
            Array.Reverse(path,0,length);return true;
        }
        public bool Next(Vector3 at,out Vector3 goal)
        {
            while(index<length&&Vector3.DistanceSquared(at,Point(path[index]))<.0225f)index++;
            goal=index<length?Point(path[index]):at;return index<length;
        }
    }
}
