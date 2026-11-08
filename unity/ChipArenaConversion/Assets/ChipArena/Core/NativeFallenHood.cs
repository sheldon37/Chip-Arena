using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// NOCTIEL's hood as cloth (the owner, 2026-11-07: "Assuming he has a hood on? Can we also integrate magica cloth into that as well; give it more of a
    /// fabric appearance"). In place of the rigid cowl (the gear bank's "cowl", phases 1 to 3): a loose fabric hood, its crown drawn up to a soft peak
    /// behind, open round the mask with a rolled hem framing it, creased where it gathers under the jaw, and a short skirt of it lying out over the
    /// collar. One double-sided grid in the skeleton's bind space (x right, y up, z forward, the body about 1.76 m tall: the game draws it scaled),
    /// skinned to the head, the skirt handed down to the neck and the chest so without the cloth simulation it still follows them. Runtime
    /// (NativeFallenCloth) makes it a skinned renderer named with the bridge's "Hat." prefix (its hood tuning: heavier, stiffer) and hands it to the
    /// licensed cloth simulation with its paint map: the crown and the face fixed on the head (red), the skirt from the neck down moving (green). Pure.
    /// </summary>
    public static class NativeFallenHood
    {
        /// <summary>The renderer's name (its paint map is registered by it; "Hat." is the bridge's hood tuning).</summary>
        public const string Name="Hat.NOCTIEL hood";
        /// <summary>Bones: 0 head, 1 neck_01, 2 spine_05.</summary>
        public static readonly string[] Bones={"head","neck_01","spine_05"};
        /// <summary>A ring of the hood from the peak down: its height, half sizes across (x) and front to back (z), centre's depth (z), and the face opening's
        /// half angle from the front (degrees; 0 closed over the brow and the crown).</summary>
        public struct Ring{public float Y,Rx,Rz,Cz,Open;public Ring(float y,float rx,float rz,float cz,float open){Y=y;Rx=rx;Rz=rz;Cz=cz;Open=open;}}
        /// <summary>From the peak to the skirt's hem. The face opening (rings 4 to 8) frames the mask (its front at z .11, 1.555 to 1.75 high), wider than it.</summary>
        public static readonly Ring[] Rings=
        {
            new Ring(1.872f,.004f,.004f,-.112f,0),new Ring(1.858f,.045f,.05f,-.088f,0),new Ring(1.832f,.08f,.09f,-.062f,0),new Ring(1.798f,.107f,.12f,-.044f,0),
            new Ring(1.756f,.12f,.134f,-.034f,26),new Ring(1.702f,.124f,.14f,-.03f,48),new Ring(1.64f,.122f,.138f,-.03f,52),new Ring(1.582f,.115f,.132f,-.034f,46),
            new Ring(1.536f,.106f,.124f,-.04f,34),new Ring(1.497f,.111f,.127f,-.05f,28),new Ring(1.458f,.133f,.143f,-.06f,25),new Ring(1.424f,.158f,.162f,-.07f,23),
        };
        /// <summary>The last ring held on the head: those after it (the skirt from the neck) move.</summary>
        public const int FixedRings=9;
        /// <summary>Rows between two rings, columns round.</summary>
        public const int Sub=2,Columns=26;
        /// <summary>Charcoal (dark like the cowl it replaces, but in the body shader's cloth band, so it has its velvet sheen), its creases darker (out of
        /// the band: no sheen), the trim round the face and the hem pale.</summary>
        public const uint Cloth=0x111114,Fold=0x0A0A0C,Trim=0x8E8E92;
        public static int Rows=>(Rings.Length-1)*Sub;
        /// <summary>The hood: the outer copy first, then the inner, flipped (NativeFallenRobe's grid).</summary>
        public static NativeFallenRobe.Grid Build()
        {
            int rows=Rows,cols=Columns;var pos=new List<Vector3>();var uv=new List<Vector2>();var col=new List<uint>();var bone=new List<int>();var weight=new List<float>();var outward=new List<Vector3>();var tris=new List<int>();
            for(int r=0;r<=rows;r++)
            {
                int k=Math.Min(Rings.Length-2,r/Sub);float f=(r-k*Sub)/(float)Sub;var a=Rings[k];var b=Rings[k+1];
                float y=Lerp(a.Y,b.Y,f),rx=Lerp(a.Rx,b.Rx,f),rz=Lerp(a.Rz,b.Rz,f),cz=Lerp(a.Cz,b.Cz,f),open=Lerp(a.Open,b.Open,f);
                float t=(float)r/rows,skirt=Smooth((t-(FixedRings-1f)/(Rings.Length-1))/(1-(FixedRings-1f)/(Rings.Length-1)));
                for(int c=0;c<=cols;c++)
                {
                    float u=(float)c/cols,deg=open+(360-2*open)*u,an=deg*(float)Math.PI/180;
                    // Folds: soft creases down the sides, deeper where it gathers under the jaw and lies out over the collar; none on the crown.
                    float crease=(.004f+.016f*skirt)*(float)Math.Sin(an*7+t*3.1f)+.005f*Smooth((t-.25f)/.3f)*(float)Math.Sin(an*11+1.7f);
                    float sx=rx*(1+crease/Math.Max(.02f,rx)),sz=rz*(1+crease/Math.Max(.02f,rz));
                    var p=new Vector3((float)Math.Sin(an)*sx,y,cz+(float)Math.Cos(an)*sz);
                    // The opening's hem rolls in a little.
                    bool rim=open>0&&(c==0||c==cols),hem=r==rows;if(rim){p.X*=.97f;p.Z-=.006f;}
                    pos.Add(p);uv.Add(new Vector2(u,1-t));outward.Add(Vector3.Normalize(new Vector3((float)Math.Sin(an),.25f*(1-t),(float)Math.Cos(an))));
                    col.Add(rim||hem?Trim:crease<-.006f?Fold:Cloth);
                    // Skinned: the head; the skirt handed down to the neck, then the chest.
                    int bi=0;float w=0;if(skirt>0){if(skirt<.5f){bi=1;w=skirt*2*.6f;}else{bi=2;w=.3f+(skirt-.5f)*2*.5f;}}
                    bone.Add(bi);weight.Add(w);
                }
            }
            int wdt=cols+1;
            for(int r=0;r<rows;r++)for(int c=0;c<cols;c++){int a=r*wdt+c,b=a+1,d=a+wdt,e=d+1;Tri(tris,pos,outward,a,b,e);Tri(tris,pos,outward,a,e,d);}
            int n=pos.Count;var g=new NativeFallenRobe.Grid{Side=n,Positions=new Vector3[n*2],Normals=new Vector3[n*2],Uv=new Vector2[n*2],Colors=new uint[n*2],Alpha=new byte[n*2],Bone1=new int[n*2],Weight1=new float[n*2]};
            var normals=Normals(pos,tris,outward);
            for(int i=0;i<n;i++)
            {
                g.Positions[i]=g.Positions[n+i]=pos[i];g.Normals[i]=normals[i];g.Normals[n+i]=-normals[i];g.Uv[i]=g.Uv[n+i]=uv[i];g.Colors[i]=col[i];g.Colors[n+i]=Fold;
                g.Alpha[i]=255;g.Alpha[n+i]=0;g.Bone1[i]=g.Bone1[n+i]=bone[i];g.Weight1[i]=g.Weight1[n+i]=weight[i];
            }
            var all=new int[tris.Count*2];for(int i=0;i<tris.Count;i+=3){all[i]=tris[i];all[i+1]=tris[i+1];all[i+2]=tris[i+2];all[tris.Count+i]=tris[i]+n;all[tris.Count+i+1]=tris[i+2]+n;all[tris.Count+i+2]=tris[i+1]+n;}
            g.Triangles=all;return g;
        }
        /// <summary>Whether a vertex at the grid's v (1 at the peak) is held on the head: down to the fixed rings, the skirt below them moves.</summary>
        public static bool Fixed(float v)=>v>=1-((FixedRings-1)*Sub+.5f)/Rows;
        /// <summary>The paint map (red fixed, green moving), width × height, rows from the bottom.</summary>
        public static uint[] Paint(int width,int height)
        {
            var px=new uint[width*height];
            for(int y=0;y<height;y++){bool red=Fixed((y+.5f)/height);for(int x=0;x<width;x++)px[y*width+x]=red?0xFF0000u:0x00FF00u;}
            return px;
        }
        private static float Lerp(float a,float b,float t)=>a+(b-a)*t;
        private static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        // A triangle wound so its front (Unity's: Cross(b-a,c-a)) faces out from the head.
        private static void Tri(List<int> tris,List<Vector3> pos,List<Vector3> outward,int a,int b,int c)
        {
            var n=Vector3.Cross(pos[b]-pos[a],pos[c]-pos[a]);var o=outward[a]+outward[b]+outward[c];
            if(n.LengthSquared()<1e-14f)return;
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
