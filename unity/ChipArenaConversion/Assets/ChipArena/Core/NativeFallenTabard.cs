using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// NOCTIEL's armour and robe together (the owner, 2026-11-08: "it still looks overall simple, maybe we can give them clothes attached to the hood? like
    /// some kind of robe ... Maybe like a armor / robe combo actually"). Over the plate, phases 1 and 2: a long tabard falling from under the hood's skirt
    /// at the collar, lying on the breastplate (narrower than it, so the plate shows either side), cinched at the belt over its clasp, then hanging free
    /// before the legs to the shin, its hem cut to a shallow point, a pale stripe down its middle and pale borders; phase 2's burnt shorter and ragged.
    /// It takes the place of the skirt's front panel. And a sash knotted at the left hip, its two tails to the knee. One double-sided grid per phase in
    /// the skeleton's bind space (x right, y up, z forward, the body about 1.76 m tall), skinned to the chest, the waist and the hips by height, the hem
    /// handed toward the thighs; Runtime (NativeFallenCloth) hands it to the licensed cloth simulation with its paint map: the tabard down to the belt and
    /// the sash's knot fixed (red), the rest moving (green). Pure.
    /// </summary>
    public static class NativeFallenTabard
    {
        public const string Name="NOCTIEL tabard";
        /// <summary>Bones: 0 spine_05 (the chest), 1 spine_03, 2 pelvis, 3 thigh_l, 4 thigh_r.</summary>
        public static readonly string[] Bones={"spine_05","spine_03","pelvis","thigh_l","thigh_r"};
        /// <summary>A row of the tabard down its middle: its height, half width, how far forward its middle lies (z), how much it wraps back toward its edges
        /// (z less by Wrap times x squared). Down to the belt it lies on the breastplate (its front at z .13 to .16 from 1.42 to 1.2), the belt's clasp (.15).</summary>
        public struct Row{public float Y,Half,Z,Wrap;public Row(float y,float half,float z,float wrap){Y=y;Half=half;Z=z;Wrap=wrap;}}
        public static readonly Row[] Rows=
        {
            new Row(1.47f,.055f,.075f,1f),new Row(1.42f,.083f,.15f,2.4f),new Row(1.37f,.094f,.178f,2.6f),new Row(1.31f,.1f,.184f,2.6f),new Row(1.25f,.097f,.175f,2.4f),
            new Row(1.2f,.09f,.165f,2.2f),new Row(1.11f,.085f,.157f,1.8f),new Row(Belt,.085f,.172f,1.6f),new Row(.95f,.098f,.182f,1.4f),new Row(.8f,.11f,.18f,1.1f),
            new Row(.62f,.12f,.174f,.9f),new Row(.45f,.128f,.17f,.7f),new Row(.34f,.132f,.167f,.6f),
        };
        /// <summary>The belt's height: down to it the tabard is held (fixed), below it hangs.</summary>
        public const float Belt=1.03f;
        /// <summary>Phase 2's hem (burnt shorter), the point the hem is cut to (m higher at the edges), columns across, rows between two of <see cref="Rows"/>.</summary>
        public const float BurntHem=.52f,Point=.05f;public const int Columns=8,Sub=2;
        /// <summary>The sash: its knot on the left hip (x, y, z), its two tails' lengths, width, rows.</summary>
        public static readonly Vector3 Knot=new Vector3(-.118f,1.02f,.108f);public const float TailA=.4f,TailB=.33f,TailWidth=.045f;public const int TailRows=8;
        public const uint Cloth=0x111114,Trim=0x8E8E92,Sash=0x55555C,SashEnd=0xB4B4BA,Ash=0x55555A;
        /// <summary>Where the tabard's grid ends across uv (the sash's tails are laid beyond it, so one paint map serves both).</summary>
        public const float TabardU=.8f;
        public static NativeFallenRobe.Grid Build(int phase)
        {
            bool burnt=phase==2;var pos=new List<Vector3>();var uv=new List<Vector2>();var col=new List<uint>();var b0=new List<int>();var b1=new List<int>();var wt=new List<float>();var outward=new List<Vector3>();var tris=new List<int>();var hem=new List<int>();
            // ---- the tabard ----
            float hemY=burnt?BurntHem:Rows[Rows.Length-1].Y;int rows=(Rows.Length-1)*Sub;
            for(int r=0;r<=rows;r++)
            {
                int k=Math.Min(Rows.Length-2,r/Sub);float f=(r-k*Sub)/(float)Sub;var a=Rows[k];var b=Rows[k+1];
                float y=Lerp(a.Y,b.Y,f),half=Lerp(a.Half,b.Half,f),z=Lerp(a.Z,b.Z,f),wrap=Lerp(a.Wrap,b.Wrap,f);
                // Burnt shorter: below the belt the rows close up toward the higher hem.
                float fullHem=Rows[Rows.Length-1].Y;if(burnt&&y<Belt)y=Belt-(Belt-y)*(Belt-hemY)/(Belt-fullHem);
                float t=(float)r/rows,below=Smooth((Belt-y)/.25f);
                for(int c=0;c<=Columns;c++)
                {
                    float u=(float)c/Columns,x=(u*2-1)*half;bool last=r==rows;
                    float yy=y;if(last){yy+=Point*Math.Abs(u*2-1);if(burnt)yy+=.05f*(Hash(c*13+7)*2-1)+.03f;}
                    // Folds below the belt, deepening toward the hem.
                    float fold=.009f*below*(float)Math.Sin(u*Math.PI*3+.4f)*(.4f+.6f*t);
                    var p=new Vector3(x,yy,z-wrap*x*x+fold);pos.Add(p);uv.Add(new Vector2(u*TabardU,1-t));outward.Add(Vector3.Normalize(new Vector3(x*4,0,1)));
                    bool edge=c==0||c==Columns||last||r==0,stripe=c==Columns/2;col.Add(burnt&&r>=rows-2?Ash:edge||stripe?Trim:Cloth);
                    // Skinned by height: the chest, the waist, the hips; the hem leaning on its thigh.
                    int i0,i1;float w;
                    if(yy>=1.33f){i0=0;i1=0;w=0;}else if(yy>=1.18f){i0=0;i1=1;w=Smooth((1.33f-yy)/.15f);}else if(yy>=1.05f){i0=1;i1=2;w=Smooth((1.18f-yy)/.13f);}
                    else{i0=2;i1=u<.5f?3:4;float s=Math.Abs(u*2-1);w=.35f*below*below*(.3f+.7f*s);}
                    b0.Add(i0);b1.Add(i1);wt.Add(w);if(last)hem.Add(pos.Count-1);
                }
            }
            int width=Columns+1;
            for(int r=0;r<rows;r++)for(int c=0;c<Columns;c++){int a=r*width+c,b=a+1,d=a+width,e=d+1;Tri(tris,pos,outward,a,b,e);Tri(tris,pos,outward,a,e,d);}
            // ---- the sash: two tails from a knot on the left hip ----
            for(int s=0;s<2;s++)
            {
                int first=pos.Count;float len=(s==0?TailA:TailB)*(burnt?.75f:1),splay=s==0?-.035f:.02f;
                for(int r=0;r<=TailRows;r++)
                {
                    float t=(float)r/TailRows;var c0=Knot+new Vector3(splay*t-.012f*t*t,-len*t,.012f*(float)Math.Sin(t*3.1f+s));
                    for(int c=0;c<=1;c++)
                    {
                        float side=(c-.5f)*TailWidth*(1-.25f*t);var p=c0+new Vector3(side*.8f,0,side*.6f);
                        pos.Add(p);uv.Add(new Vector2(TabardU+.02f+(s*2+c)*(.18f/3),1-t));outward.Add(Vector3.Normalize(new Vector3(-.6f,0,.8f)));
                        col.Add(r>=TailRows-1?SashEnd:Sash);b0.Add(2);b1.Add(3);wt.Add(.45f*t*t);if(r==TailRows)hem.Add(pos.Count-1);
                    }
                }
                for(int r=0;r<TailRows;r++){int a=first+r*2,b=a+1,d=a+2,e=d+1;Tri(tris,pos,outward,a,b,e);Tri(tris,pos,outward,a,e,d);}
            }
            // The knot itself: a small closed puff over where the tails leave (a fan of triangles round its centre).
            {
                int centre=pos.Count;var kc=Knot+new Vector3(0,.012f,.016f);pos.Add(kc);uv.Add(new Vector2(.99f,1));outward.Add(Vector3.Normalize(new Vector3(-.6f,0,.8f)));col.Add(Sash);b0.Add(2);b1.Add(2);wt.Add(0);
                int ring=pos.Count;const int n=8;
                for(int i=0;i<n;i++){float an=i*(float)Math.PI*2/n;var p=Knot+new Vector3((float)Math.Cos(an)*.034f*.8f,(float)Math.Sin(an)*.026f,(float)Math.Cos(an)*.034f*.6f);pos.Add(p);uv.Add(new Vector2(.99f,1));outward.Add(Vector3.Normalize(p-Knot+new Vector3(-.3f,0,.6f)));col.Add(Sash);b0.Add(2);b1.Add(2);wt.Add(0);}
                for(int i=0;i<n;i++)Tri(tris,pos,outward,centre,ring+i,ring+(i+1)%n);
            }
            int nv=pos.Count;var g=new NativeFallenRobe.Grid{Side=nv,Positions=new Vector3[nv*2],Normals=new Vector3[nv*2],Uv=new Vector2[nv*2],Colors=new uint[nv*2],Alpha=new byte[nv*2],Bone0=new int[nv*2],Bone1=new int[nv*2],Weight1=new float[nv*2],Hem=hem.ToArray()};
            var normals=Normals(pos,tris,outward);
            for(int i=0;i<nv;i++)
            {
                g.Positions[i]=g.Positions[nv+i]=pos[i];g.Normals[i]=normals[i];g.Normals[nv+i]=-normals[i];g.Uv[i]=g.Uv[nv+i]=uv[i];g.Colors[i]=col[i];g.Colors[nv+i]=col[i]==Cloth?0x0A0A0Cu:col[i];
                g.Alpha[i]=255;g.Alpha[nv+i]=0;g.Bone0[i]=g.Bone0[nv+i]=b0[i];g.Bone1[i]=g.Bone1[nv+i]=b1[i];g.Weight1[i]=g.Weight1[nv+i]=wt[i];
            }
            var all=new int[tris.Count*2];for(int i=0;i<tris.Count;i+=3){all[i]=tris[i];all[i+1]=tris[i+1];all[i+2]=tris[i+2];all[tris.Count+i]=tris[i]+nv;all[tris.Count+i+1]=tris[i+2]+nv;all[tris.Count+i+2]=tris[i+1]+nv;}
            g.Triangles=all;return g;
        }
        /// <summary>Whether a vertex at (u, v) is held: the tabard down to its belt row, the sash's knot and the first row of its tails.</summary>
        public static bool Fixed(float u,float v)
        {
            if(u>=TabardU+.01f)return v>=1-1.5f/TailRows;
            int beltRow=Array.FindIndex(Rows,r=>r.Y==Belt)*Sub,rows=(Rows.Length-1)*Sub;return v>=1-(beltRow+.5f)/rows;
        }
        public static uint[] Paint(int width,int height)
        {
            var px=new uint[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)px[y*width+x]=Fixed((x+.5f)/width,(y+.5f)/height)?0xFF0000u:0x00FF00u;
            return px;
        }
        private static float Lerp(float a,float b,float t)=>a+(b-a)*t;
        private static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        private static float Hash(int k){uint x=(uint)k*2654435761u;x^=x>>15;x*=2246822519u;x^=x>>13;return (x&0xFFFF)/65535f;}
        private static void Tri(List<int> tris,List<Vector3> pos,List<Vector3> outward,int a,int b,int c)
        {
            var n=Vector3.Cross(pos[b]-pos[a],pos[c]-pos[a]);if(n.LengthSquared()<1e-14f)return;var o=outward[a]+outward[b]+outward[c];
            if(Vector3.Dot(n,o)>=0){tris.Add(a);tris.Add(b);tris.Add(c);}else{tris.Add(a);tris.Add(c);tris.Add(b);}
        }
        private static Vector3[] Normals(List<Vector3> pos,List<int> tris,List<Vector3> outward)
        {
            var n=new Vector3[pos.Count];
            for(int i=0;i<tris.Count;i+=3){int a=tris[i],b=tris[i+1],c=tris[i+2];var f=Vector3.Cross(pos[b]-pos[a],pos[c]-pos[a]);n[a]+=f;n[b]+=f;n[c]+=f;}
            for(int i=0;i<n.Length;i++)n[i]=n[i].LengthSquared()>1e-12f?Vector3.Normalize(n[i]):outward[i];
            return n;
        }
    }
}
