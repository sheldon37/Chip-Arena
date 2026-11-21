using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// NOCTIEL's robe as cloth (the owner, 2026-11-07: "Its hard to see his legs ... integrate magica cloth into their clothes to simulate cloth
    /// movement"). In place of the seven rigid strips that hung to its ankles all round (the front two right over the legs), a skirt hangs from
    /// under the faulds: a narrow panel hanging between the legs to above the knee (the cloth the eye meets from in front), open either side of it
    /// so both legs show whole, a side panel each side ending at the knee, a long panel behind to mid-calf, slits between them. Pleated, its hem a little ragged; phase 2's is shorter and burnt. One double-sided grid per phase in the skeleton's bind
    /// space (x right, y up, z forward, the body about 1.76 m tall: the game draws it scaled), skinned to the pelvis with the side panels' hems
    /// leaning on their thighs, so without the cloth simulation it still follows the legs. Runtime (NativeFallenCloth) makes it a skinned renderer
    /// and hands it to the licensed cloth simulation through NativeVisualCloth with its paint map: the top <see cref="FixedRows"/> rows fixed (red),
    /// the rest moving (green), by the grid's v (1 at the waist). Pure.
    /// </summary>
    public static class NativeFallenRobe
    {
        /// <summary>The renderers' names, "NOCTIEL robe 1" and "NOCTIEL robe 2" (phase 1's, phase 2's): the paint maps are registered by them.</summary>
        public const string Name="NOCTIEL robe";
        public struct Panel{public float From,To,Length;public int Columns,Pleats;public int Thigh;}
        /// <summary>Round the waist in degrees from the front toward its right (x = sin, z = cos); Thigh: which leg its hem leans on (0 none, 1 left, 2 right).
        /// Since 2026-11-08 the front panel is the tabard's (NativeFallenTabard): nothing of the skirt hangs within <see cref="Open"/> of the front.</summary>
        public static readonly Panel[] Long={new Panel{From=58,To=124,Length=.36f,Columns=10,Pleats=2,Thigh=2},new Panel{From=132,To=228,Length=.66f,Columns=14,Pleats=3},new Panel{From=236,To=302,Length=.36f,Columns=10,Pleats=2,Thigh=1}};
        public static readonly Panel[] Burnt={new Panel{From=58,To=124,Length=.29f,Columns=10,Pleats=2,Thigh=2},new Panel{From=132,To=228,Length=.5f,Columns=14,Pleats=3},new Panel{From=236,To=302,Length=.29f,Columns=10,Pleats=2,Thigh=1}};
        /// <summary>The front panel's half width (degrees): nothing else hangs within <see cref="Open"/> of the front, where the legs are.</summary>
        public const float Front=15,Open=55;
        /// <summary>The waist ring it hangs from (just under the faulds' last ring at y .9), its half sizes, and how far the hem flares out from them.</summary>
        public const float Top=.905f,CentreZ=.03f,WaistX=.168f,WaistZ=.142f,FlareX=.1f,FlareZ=.09f,Pleat=.011f;
        public const int Rows=14,FixedRows=2;
        /// <summary>Bones: 0 pelvis, 1 thigh_l, 2 thigh_r.</summary>
        public static readonly string[] Bones={"pelvis","thigh_l","thigh_r"};
        public const uint Cloth=0x17171B,Trim=0x8E8E92,Ash=0x55555A;
        public sealed class Grid
        {
            public Vector3[] Positions,Normals;public Vector2[] Uv;public uint[] Colors;public byte[] Alpha;public int[] Triangles;public int[] Bone1;public float[] Weight1;
            /// <summary>Each vertex's first bone (null: bone 0 for all), and the outer copy's hem vertices (where the cloth sheds its motes).</summary>
            public int[] Bone0,Hem;
            /// <summary>Vertices in one side's copy (the inner copy follows, the same order).</summary>
            public int Side;
        }
        public static Panel[] Of(int phase)=>phase==2?Burnt:Long;
        /// <summary>Phase 1's skirt (phase 2's burnt shorter): the outer copy first, then the inner, flipped.</summary>
        public static Grid Build(int phase)
        {
            var panels=Of(phase);bool burnt=phase==2;
            var pos=new List<Vector3>();var uv=new List<Vector2>();var col=new List<uint>();var bone=new List<int>();var weight=new List<float>();var tris=new List<int>();var outward=new List<Vector3>();var hem=new List<int>();
            for(int p=0;p<panels.Length;p++)
            {
                var q=panels[p];int cols=q.Columns,first=pos.Count;
                for(int r=0;r<=Rows;r++)
                {
                    float t=(float)r/Rows;
                    for(int c=0;c<=cols;c++)
                    {
                        float u=(float)c/cols,a=(q.From+(q.To-q.From)*u)*(float)Math.PI/180;
                        // The hem: a little ragged (burnt, torn shorter in places); the length eases in so the waist stays a clean ring.
                        float rag=Hash(p*131+c*17+phase*7)*2-1,length=q.Length*(1+(burnt?.16f:.05f)*rag*(c==0||c==cols?.3f:1));
                        float pleat=Pleat*(.35f+.65f*t)*(float)Math.Sin(u*Math.PI*2*q.Pleats),rx=WaistX+FlareX*(float)Math.Pow(t,1.15)+pleat,rz=WaistZ+FlareZ*t+pleat;
                        float y=Top-length*t;var at=new Vector3((float)Math.Sin(a)*rx,y,CentreZ+(float)Math.Cos(a)*rz);
                        pos.Add(at);uv.Add(new Vector2(u,1-t));outward.Add(Vector3.Normalize(new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a))));
                        bool edge=c==0||c==cols||r==Rows;col.Add(edge?(burnt&&r>=Rows-1?Ash:Trim):Cloth);if(r==Rows)hem.Add(pos.Count-1);
                        bone.Add(q.Thigh);weight.Add(q.Thigh==0?0:.5f*t*t);
                    }
                }
                int w=cols+1;
                for(int r=0;r<Rows;r++)for(int c=0;c<cols;c++)
                {
                    int a=first+r*w+c,b=a+1,d=a+w,e=d+1;
                    Tri(tris,pos,outward,a,b,e);Tri(tris,pos,outward,a,e,d);
                }
            }
            int n=pos.Count;var g=new Grid{Hem=hem.ToArray(),Side=n,Positions=new Vector3[n*2],Normals=new Vector3[n*2],Uv=new Vector2[n*2],Colors=new uint[n*2],Alpha=new byte[n*2],Bone1=new int[n*2],Weight1=new float[n*2]};
            var normals=Normals(pos,tris,outward);
            for(int i=0;i<n;i++)
            {
                g.Positions[i]=g.Positions[n+i]=pos[i];g.Normals[i]=normals[i];g.Normals[n+i]=-normals[i];g.Uv[i]=g.Uv[n+i]=uv[i];g.Colors[i]=g.Colors[n+i]=col[i];
                g.Alpha[i]=255;g.Alpha[n+i]=0;g.Bone1[i]=g.Bone1[n+i]=bone[i];g.Weight1[i]=g.Weight1[n+i]=weight[i];
            }
            var all=new int[tris.Count*2];for(int i=0;i<tris.Count;i+=3){all[i]=tris[i];all[i+1]=tris[i+1];all[i+2]=tris[i+2];all[tris.Count+i]=tris[i]+n;all[tris.Count+i+1]=tris[i+2]+n;all[tris.Count+i+2]=tris[i+1]+n;}
            g.Triangles=all;return g;
        }
        /// <summary>The paint map (red fixed, green moving), width × height, rows from the bottom: red where the grid's v reaches the fixed rows.</summary>
        public static bool Fixed(float v)=>v>=1-(FixedRows-.5f)/Rows;
        public static uint[] Paint(int width,int height)
        {
            var px=new uint[width*height];
            for(int y=0;y<height;y++){bool red=Fixed((y+.5f)/height);for(int x=0;x<width;x++)px[y*width+x]=red?0xFF0000u:0x00FF00u;}
            return px;
        }
        // A triangle wound so its front (Unity's: Cross(b-a,c-a)) faces out from the waist.
        private static void Tri(List<int> tris,List<Vector3> pos,List<Vector3> outward,int a,int b,int c)
        {
            var n=Vector3.Cross(pos[b]-pos[a],pos[c]-pos[a]);var o=outward[a]+outward[b]+outward[c];
            if(Vector3.Dot(n,o)>=0){tris.Add(a);tris.Add(b);tris.Add(c);}else{tris.Add(a);tris.Add(c);tris.Add(b);}
        }
        private static Vector3[] Normals(List<Vector3> pos,List<int> tris,List<Vector3> outward)
        {
            var n=new Vector3[pos.Count];
            for(int i=0;i<tris.Count;i+=3){int a=tris[i],b=tris[i+1],c=tris[i+2];var f=Vector3.Cross(pos[b]-pos[a],pos[c]-pos[a]);n[a]+=f;n[b]+=f;n[c]+=f;}
            for(int i=0;i<n.Length;i++)n[i]=n[i].LengthSquared()>1e-12f?Vector3.Normalize(n[i]):outward[i];
            return n;
        }
        private static float Hash(int k){uint x=(uint)k*2654435761u;x^=x>>15;x*=2246822519u;x^=x>>13;return (x&0xFFFF)/65535f;}
    }
}
