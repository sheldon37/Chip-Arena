using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The parry's hexagon barrier (owner 2026-11-09: "pressing F; Lets add more of an effect rather that just turning character a color.
    /// Make it high quality, animated, and visible. Maybe like hexagon barriers?"): a dome of separate hexagonal cells round the pilot (a
    /// geodesic sphere's dual: twelve pentagons, the rest hexagons), each cell its own plate with a gap round it. Runtime
    /// NativeParryBarrierPresentation.cs draws it while the parry's window is open, in Resources/NativeParryBarrier.shader, which animates
    /// every cell from what each vertex carries here: the cells snap in from the front round to the back, breathe, ripple out from a parried
    /// blow and burst outward as the window closes. Engine-free so the managed checks build it. Presentation only.
    /// </summary>
    public static class NativeParryBarrier
    {
        /// <summary>The dome round the pilot: its middle's height (m, at the pilot's scale), its radius and its height's stretch; the cells'
        /// gap (each plate's corners drawn in to this share of the way out).</summary>
        public const float Middle=.95f,Radius=1.02f,Tall=1.08f,Inset=.9f;
        /// <summary>The geodesic frequency: Full on the normal tiers (362 cells), Low on the low one (162).</summary>
        public const int Full=6,Low=4;
        /// <summary>One cell: its middle (unit) and its corners round it (unit, in order), and its seed (0..1).</summary>
        public struct Cell{public Vector3 Middle;public Vector3[] Corners;public float Seed;}
        /// <summary>The mesh: positions on the unit sphere, normals, uv (x 0 at a cell's middle to 1 at its rim, y its seed), uv2 its cell's
        /// middle, and the triangles (a fan per cell).</summary>
        public sealed class Mesh{public readonly List<Vector3> P=new List<Vector3>(),N=new List<Vector3>(),Middle=new List<Vector3>();public readonly List<Vector2> Uv=new List<Vector2>();public readonly List<int> T=new List<int>();public int Cells;}

        private static readonly Vector3[] Ico;private static readonly int[] Faces={0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1};
        static NativeParryBarrier()
        {
            float t=(1+(float)Math.Sqrt(5))/2;
            Ico=new[]{new Vector3(-1,t,0),new Vector3(1,t,0),new Vector3(-1,-t,0),new Vector3(1,-t,0),new Vector3(0,-1,t),new Vector3(0,1,t),new Vector3(0,-1,-t),new Vector3(0,1,-t),new Vector3(t,0,-1),new Vector3(t,0,1),new Vector3(-t,0,-1),new Vector3(-t,0,1)};
            for(int i=0;i<Ico.Length;i++)Ico[i]=Vector3.Normalize(Ico[i]);
        }
        private static float Hash(int k){uint h=(uint)k*0x9E3779B1u;h^=h>>15;h*=0x2C1B3C6Du;h^=h>>12;return (h&0xffffff)/16777216f;}

        /// <summary>The cells of the frequency-`f` geodesic sphere's dual: one per vertex of the subdivided icosahedron (10 f² + 2), its
        /// corners the middles of the triangles round it, ordered about it.</summary>
        public static Cell[] Cells(int f)
        {
            f=Math.Max(1,f);var verts=new List<Vector3>();var index=new Dictionary<(int,int,int),int>();var tris=new List<int>();
            int Vert(Vector3 p){p=Vector3.Normalize(p);var key=((int)Math.Round(p.X*20000),(int)Math.Round(p.Y*20000),(int)Math.Round(p.Z*20000));if(index.TryGetValue(key,out int i))return i;index[key]=verts.Count;verts.Add(p);return verts.Count-1;}
            for(int face=0;face<20;face++)
            {
                Vector3 a=Ico[Faces[face*3]],b=Ico[Faces[face*3+1]],c=Ico[Faces[face*3+2]];var row=new int[f+1][];
                for(int i=0;i<=f;i++){row[i]=new int[f-i+1];for(int j=0;j<=f-i;j++)row[i][j]=Vert(a+(b-a)*(i/(float)f)+(c-a)*(j/(float)f));}
                for(int i=0;i<f;i++)for(int j=0;j<f-i;j++)
                {
                    tris.Add(row[i][j]);tris.Add(row[i+1][j]);tris.Add(row[i][j+1]);
                    if(j<f-i-1){tris.Add(row[i+1][j]);tris.Add(row[i+1][j+1]);tris.Add(row[i][j+1]);}
                }
            }
            var round=new List<Vector3>[verts.Count];for(int i=0;i<round.Length;i++)round[i]=new List<Vector3>(6);
            for(int t=0;t<tris.Count;t+=3){var m=Vector3.Normalize(verts[tris[t]]+verts[tris[t+1]]+verts[tris[t+2]]);for(int k=0;k<3;k++)round[tris[t+k]].Add(m);}
            var cells=new Cell[verts.Count];
            for(int i=0;i<verts.Count;i++)
            {
                var n=verts[i];var u=Vector3.Normalize(Vector3.Cross(Math.Abs(n.Y)<.9f?Vector3.UnitY:Vector3.UnitX,n));var w=Vector3.Cross(n,u);
                var list=round[i];list.Sort((p,q)=>Math.Atan2(Vector3.Dot(p,w),Vector3.Dot(p,u)).CompareTo(Math.Atan2(Vector3.Dot(q,w),Vector3.Dot(q,u))));
                cells[i]=new Cell{Middle=n,Corners=list.ToArray(),Seed=Hash(i+1)};
            }
            return cells;
        }
        /// <summary>The barrier's mesh at frequency `f`: each cell a plate (a fan from its middle to its corners drawn in to Inset), on the
        /// unit sphere.</summary>
        public static Mesh Build(int f)
        {
            var m=new Mesh();var cells=Cells(f);m.Cells=cells.Length;
            foreach(var c in cells)
            {
                int start=m.P.Count;m.P.Add(c.Middle);m.N.Add(c.Middle);m.Uv.Add(new Vector2(0,c.Seed));m.Middle.Add(c.Middle);
                foreach(var corner in c.Corners){var p=Vector3.Normalize(c.Middle+(corner-c.Middle)*Inset);m.P.Add(p);m.N.Add(p);m.Uv.Add(new Vector2(1,c.Seed));m.Middle.Add(c.Middle);}
                int k=c.Corners.Length;for(int i=0;i<k;i++){m.T.Add(start);m.T.Add(start+1+i);m.T.Add(start+1+(i+1)%k);}
            }
            return m;
        }
        /// <summary>A cell's moment (the shader's own sums, kept here for the checks): when it snaps in after the window opens (s; the cells
        /// facing `facing` first, round to the back), how far in it is at `age` (0..1, overshooting), and how far gone it is at `closed` s after
        /// the window shut (0..1; negative closed: open).</summary>
        public const float Sweep=.13f,Scatter=.035f,Snap=.1f,Burst=.2f,BurstScatter=.06f;
        public static float Delay(Vector3 middle,Vector3 facing,float seed)=>(1-Vector3.Dot(middle,facing))*.5f*Sweep+seed*Scatter;
        public static float In(float age,float delay){float x=(age-delay)/Snap;if(x<=0)return 0;if(x>=1)return 1;float y=x-1;return 1+2.70158f*y*y*y+1.70158f*y*y;}
        public static float Gone(float closed,float seed){if(closed<0)return 0;float x=(closed-seed*BurstScatter)/Burst;return x<0?0:x>1?1:x;}
        /// <summary>How long the barrier shows after the window shuts (its cells' burst).</summary>
        public const float After=Burst+BurstScatter;
    }
}
