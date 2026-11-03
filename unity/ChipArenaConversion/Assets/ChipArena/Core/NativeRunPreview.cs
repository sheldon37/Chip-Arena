using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// A sample of a generated run for the Expeditions screen (owner 2026-11-03: "the map showing is inaccurate as expedition is
    /// procedurally generated"). A full run rolls its own city at the Deployment Station, so the screen cannot show its map: it shows
    /// this instead, a made-up plan of city blocks with a route from the hub through three areas to the final boss, rolled again and
    /// again and labelled a sample. Nothing here reaches the game: it reads no layout and its seeds are not run seeds. Engine-free;
    /// Tests/ExpeditionsMenuChecks holds its shape and Runtime/NativeExpeditionsMenu.cs draws it.
    /// </summary>
    public sealed class NativeRunPreview
    {
        public const int Columns=12,Rows=12,Nodes=5;
        public const byte Block=0,Landmark=1,Plaza=2;
        public readonly uint Seed;
        public readonly int Count;
        /// <summary>u, v, w, h per block (plan units 0..1).</summary>
        public readonly float[] Box;
        /// <summary>A block's height, 0..1.</summary>
        public readonly float[] Level;
        /// <summary>When a block arrives as the plan builds, 0..1: outward from the hub.</summary>
        public readonly float[] Arrive;
        public readonly byte[] Kind;
        /// <summary>u, v pairs: the hub, the three areas and the final boss.</summary>
        public readonly float[] Node;
        /// <summary>u, v pairs: the route's corners from the hub to the final boss, each leg axis-aligned along a street.</summary>
        public readonly float[] Path;
        /// <summary>The route's distance at each corner (0 at the hub, 1 at the final boss), and each node's.</summary>
        public readonly float[] PathAt,NodeAt;
        /// <summary>"3F7A·91C2".</summary>
        public readonly string Label;

        private NativeRunPreview(uint seed,List<float> box,List<float> level,List<float> arrive,List<byte> kind,float[] node,List<float> path,float[] pathAt,float[] nodeAt)
        {
            Seed=seed;Count=kind.Count;Box=box.ToArray();Level=level.ToArray();Arrive=arrive.ToArray();Kind=kind.ToArray();Node=node;Path=path.ToArray();PathAt=pathAt;NodeAt=nodeAt;
            string h=seed.ToString("X8");Label=h.Substring(0,4)+"·"+h.Substring(4);
        }
        // A small xorshift: the sample needs only to differ from one roll to the next, the same for the same seed.
        private struct Dice
        {
            private uint s;public Dice(uint seed){s=seed*2654435761u+0x9E3779B9u;if(s==0)s=1;Next();Next();}
            public uint Next(){s^=s<<13;s^=s>>17;s^=s<<5;return s;}
            public float Float()=>(Next()>>8)*(1f/16777216f);
            public int Range(int lo,int hi)=>lo+(int)(Float()*(hi-lo));
        }
        /// <summary>The seed the screen's nth roll shows.</summary>
        public static uint SeedOf(long roll){uint x=(uint)roll*2166136261u+0x51ED270Bu;x^=x>>15;x*=2246822519u;x^=x>>13;return x==0?1:x;}
        public static NativeRunPreview Roll(uint seed)
        {
            var dice=new Dice(seed);
            // The streets: uneven columns and rows.
            var xs=Lines(Columns,ref dice);var ys=Lines(Rows,ref dice);
            // The nodes stand at crossings, climbing from the hub at the foot to the final boss at the top, each swung well across.
            var col=new int[Nodes];var row=new int[Nodes];
            for(int i=0;i<Nodes;i++)
            {
                row[i]=i==0?Rows-1:i==Nodes-1?1:Math.Max(2,Math.Min(Rows-2,(int)Math.Round((Rows-1)*(1-i/(float)(Nodes-1))))+dice.Range(-1,1));
                int c;int tries=0;do{c=dice.Range(1,Columns);}while(i>0&&Math.Abs(c-col[i-1])<3&&++tries<12);col[i]=c;
            }
            // The route: along the row, then up the column, between each pair of nodes.
            var vert=new bool[Columns+1,Rows];var horiz=new bool[Columns,Rows+1];var path=new List<float>{xs[col[0]],ys[row[0]]};var turns=new List<int>{0};
            for(int i=1;i<Nodes;i++)
            {
                int a=col[i-1],b=col[i],y=row[i-1],t=row[i];for(int c=Math.Min(a,b);c<Math.Max(a,b);c++)horiz[c,y]=true;for(int r=t;r<y;r++)vert[b,r]=true;
                if(a!=b){path.Add(xs[b]);path.Add(ys[y]);}path.Add(xs[b]);path.Add(ys[t]);turns.Add(path.Count/2-1);
            }
            int corners=path.Count/2;var pathAt=new float[corners];float len=0;for(int i=1;i<corners;i++){len+=Math.Abs(path[i*2]-path[i*2-2])+Math.Abs(path[i*2+1]-path[i*2-1]);pathAt[i]=len;}for(int i=1;i<corners;i++)pathAt[i]/=Math.Max(1e-5f,len);
            var node=new float[Nodes*2];var nodeAt=new float[Nodes];for(int i=0;i<Nodes;i++){node[i*2]=xs[col[i]];node[i*2+1]=ys[row[i]];nodeAt[i]=pathAt[turns[i]];}
            // The blocks: a cell each, some merged across a street the route does not use, some left open; those by an area's node are its landmarks.
            var box=new List<float>();var level=new List<float>();var arrive=new List<float>();var kind=new List<byte>();
            float far=0;for(int r=0;r<Rows;r++)for(int c=0;c<Columns;c++){float u=(xs[c]+xs[c+1])*.5f-node[0],v=(ys[r]+ys[r+1])*.5f-node[1];far=Math.Max(far,(float)Math.Sqrt(u*u+v*v));}
            for(int r=0;r<Rows;r++)
            {
                for(int c=0;c<Columns;c++)
                {
                    int end=c;if(c+1<Columns&&!vert[c+1,r]&&dice.Float()<.24f)end=c+1;
                    float x0=xs[c],x1=xs[end+1],y0=ys[r],y1=ys[r+1],gap=.16f*Math.Min(xs[c+1]-xs[c],y1-y0),roll=dice.Float();
                    float cu=(x0+x1)*.5f,cv=(y0+y1)*.5f;bool near=false;for(int i=1;i<Nodes-1&&!near;i++){float du=cu-node[i*2],dv=cv-node[i*2+1];near=du*du+dv*dv<.012f;}
                    byte k=near&&roll<.8f?Landmark:roll<.09f?Plaza:Block;
                    box.Add(x0+gap);box.Add(y0+gap);box.Add(x1-x0-2*gap);box.Add(y1-y0-2*gap);level.Add(k==Plaza?.1f:.25f+.75f*dice.Float()*dice.Float());
                    {float du=cu-node[0],dv=cv-node[1];arrive.Add(Math.Min(1,(float)Math.Sqrt(du*du+dv*dv)/Math.Max(.01f,far)));}kind.Add(k);c=end;
                }
            }
            return new NativeRunPreview(seed,box,level,arrive,kind,node,path,pathAt,nodeAt);
        }
        private static float[] Lines(int n,ref Dice dice)
        {
            var w=new float[n];float sum=0;for(int i=0;i<n;i++){w[i]=.62f+.76f*dice.Float();sum+=w[i];}
            var at=new float[n+1];float run=0;for(int i=0;i<n;i++){run+=w[i]/sum;at[i+1]=run;}at[n]=1;return at;
        }

        // ---- The screen's cycle: the plan builds, holds and fades, and the next roll takes over ----------------------------
        /// <summary>One roll's seconds, and the shares of it the plan takes to build and to fade.</summary>
        public const float Period=6.4f,BuildShare=.42f,FadeShare=.08f;
        /// <summary>The roll showing at `time` seconds.</summary>
        public static long RollAt(double time)=>(long)Math.Floor(time/Period);
        /// <summary>How far the plan has built at `time` (0..1), and its opacity.</summary>
        public static void Cycle(double time,out float build,out float fade)
        {
            float k=(float)(time/Period-Math.Floor(time/Period));build=Math.Min(1,k/BuildShare);
            float o=(k-(1-FadeShare))/FadeShare;fade=o<=0?1:o>=1?0:1-o*o*(3-2*o);
        }
        /// <summary>The plan's position on the route at distance `at` (0..1) as u, v.</summary>
        public void Along(float at,out float u,out float v)
        {
            int n=PathAt.Length;at=at<0?0:at>1?1:at;
            for(int i=1;i<n;i++)if(at<=PathAt[i]){float t=(at-PathAt[i-1])/Math.Max(1e-5f,PathAt[i]-PathAt[i-1]);u=Path[i*2-2]+(Path[i*2]-Path[i*2-2])*t;v=Path[i*2-1]+(Path[i*2+1]-Path[i*2-1])*t;return;}
            u=Path[n*2-2];v=Path[n*2-1];
        }
    }
}
