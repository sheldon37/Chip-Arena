using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    public static class NativeConverterRoute
    {
        // The hall and casting floor are joined by the enlarged, boss-opened shutter.
        public static bool Contains(float x,float z)=>x>=-42&&x<=-28&&Math.Abs(z)<=7
            ||x>=-42&&x<=-12&&Math.Abs(z)<=.8f
            ||x>=-14&&x<=30&&z>=-9&&z<=10;
        public static Vector3 Goal(Vector3 at,Vector3 target)
        {
            bool east=target.X>-24;
            if(east&&at.X<-14)return Math.Abs(at.Z)>.3f&&at.X<-28?new Vector3(-34,at.Y,0):new Vector3(-13,at.Y,0);
            if(!east&&at.X>-28)return Math.Abs(at.Z)>.3f&&at.X>-14?new Vector3(-13,at.Y,0):new Vector3(-34,at.Y,0);
            return east?new Vector3(Math.Max(-13,Math.Min(30,target.X)),at.Y,Math.Max(-9,Math.Min(10,target.Z)))
                :new Vector3(Math.Max(-42,Math.Min(-28,target.X)),at.Y,Math.Max(-7,Math.Min(7,target.Z)));
        }
        // A bounded 2 m footprint graph: waypoint clamping alone cannot go around casting-floor obstacles.
        public sealed class Path
        {
            private const int Columns=37,Rows=10,Count=Columns*Rows;
            private readonly int[] parents=new int[Count],queue=new int[Count],path=new int[Count];
            private int length,index;private float y;
            public void Clear(){length=index=0;}
            private Vector3 Point(int n)=>new Vector3(-42+2*(n%Columns),y,-8+2*(n/Columns));
            public bool Plan(Vector3 at,Vector3 target,Func<Vector3,Vector3,bool> clear)
            {
                Clear();y=at.Y;int read=0,write=0,best=-1;float score=float.PositiveInfinity;
                for(int i=0;i<Count;i++)parents[i]=-1;
                for(int i=0;i<Count;i++){var p=Point(i);if(Contains(p.X,p.Z)&&Vector3.DistanceSquared(at,p)<=9&&clear(at,p)){parents[i]=i;queue[write++]=i;}}
                while(read<write)
                {
                    int n=queue[read++];var p=Point(n);float d=Vector2.Distance(new Vector2(p.X,p.Z),new Vector2(target.X,target.Z));
                    if(d<score-.01f){score=d;best=n;}
                    for(int side=0;side<4;side++)
                    {
                        int next=side==0?n-1:side==1?n+1:side==2?n-Columns:n+Columns;
                        if(next<0||next>=Count||(side<2&&next/Columns!=n/Columns)||parents[next]>=0)continue;
                        var q=Point(next);if(!Contains(q.X,q.Z)||!clear(p,q))continue;parents[next]=n;queue[write++]=next;
                    }
                }
                if(best<0)return false;
                do{path[length++]=best;int previous=parents[best];if(previous==best)break;best=previous;}while(length<Count);
                Array.Reverse(path,0,length);return true;
            }
            public bool Next(Vector3 at,out Vector3 goal)
            {
                while(index<length&&Vector3.DistanceSquared(at,Point(path[index]))<.0225f)index++;
                goal=index<length?Point(path[index]):at;return index<length;
            }
        }
    }
}
