using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The Sheldon hood's skinned mesh (NativeSheldonHood.Build), in the model's head bone frame at rest (x the model's
    /// right, y up, z ahead; metres). Outer: the cloth's triangles and the rim's piping; Inner: the cloth again wound the other
    /// way (its dark lining, drawn with its own material). Colors: the visual body shader's layer pick (rgb) and outline share
    /// (alpha). Each vertex has four entries in Bones (indices into NativeSheldonHood.Bones) and Weights (summing to 1).</summary>
    public sealed class SheldonHoodMesh
    {
        public Vector3[] Positions,Normals;public Vector4[] Colors;public Vector2[] Uv;public int[] Outer,Inner,Bones;public float[] Weights;
    }

    /// <summary>
    /// The Sheldon hood on the model (owner 2026-10-27: "remake Sheldon clothes into functional clothing items (Hood/face,
    /// ...)", "I want it to utilize magica cloth for any kind of fabric movement. I want a very clean design that fits the model
    /// perfectly."; "Id like to utilize the same face on the sheldon face"). The Sheldon Head worn on the model is a cloth hood
    /// fitted round the model's head and hair (the hair laid flat by its hood shape), with the Sheldon Head's own face in its
    /// opening: the void (Void: a plate shaped to the opening, with the face's soft glow) and the live crescent eyes, jagged
    /// grin and broken ring (NativeSheldonHead), on the head bone as the hats are fitted (NativeModelHats.SheldonScale).
    /// The hood:
    ///  - each horizontal section a superellipse round the head's middle: its half width, back and front from smooth profiles
    ///    (HalfWidth, Back, Front), a dome over the crown with a soft point up the back seam (Peak); measured against the baked
    ///    head with every hair style laid flat (at least 2 cm clear) and every top's collar and shoulders;
    ///  - the opening an arch from the chin to over the brow (OpeningHalfWidth), its rim RimSide to RimBrim ahead (a brim over
    ///    the brow, the walls standing out past the face), in front of the void, piped in the glow (a rolled tube);
    ///  - below the chin a cowl over the neck and the tops of the shoulders, its hem down to the chest and back (Hem), piped in
    ///    the glow (a band along it);
    ///  - lined dark inside.
    /// Skinned to the head (everything down to the chin, the rim and the face's frame with it), easing through the neck to the
    /// upper spine and the collarbones over the cowl. As cloth (the local install's Magica bridge, NativeVisualCloth): the
    /// PaintMap holds the head part fixed and lets the cowl move. Its colours (Colour map: the cloth, the piping) are the Sheldon
    /// palette (NativeSheldonWear). Deterministic; built once. Presentation only.
    /// </summary>
    public static class NativeSheldonHood
    {
        /// <summary>The skeleton's bones it is skinned to (the visual body bake's names).</summary>
        public static readonly string[] Bones={"head","neck_01","spine_05","clavicle_l","clavicle_r"};
        public const int HeadBone=0,NeckBone=1,SpineBone=2,ClavicleLeft=3,ClavicleRight=4;
        /// <summary>Its renderer's name on a body (a hat: the clothing renderers' "Hat." slot).</summary>
        public const string Renderer="Hat.Sheldon_Hood";

        // ---- the opening ------------------------------------------------------------------------------------------------
        /// <summary>The arch: from the chin (OpenBottom) to over the brow (OpenTop), widest (OpenHalf each side) at OpenWidest;
        /// rounder over the top (OpenTopPower) than under the chin (OpenBottomPower).</summary>
        public const float OpenBottom=-.052f,OpenTop=.198f,OpenHalf=.102f,OpenWidest=.080f,OpenTopPower=2.4f,OpenBottomPower=3.2f;
        /// <summary>How far ahead the rim stands: at its sides and bottom, and at its top (the brim over the brow).</summary>
        public const float RimSide=.182f,RimBrim=.205f;
        public static float OpeningHalfWidth(float y)
        {
            if(y<=OpenBottom||y>=OpenTop)return 0;
            if(y>=OpenWidest){float t=(y-OpenWidest)/(OpenTop-OpenWidest);return OpenHalf*MathF.Pow(MathF.Max(0,1-MathF.Pow(t,OpenTopPower)),1/OpenTopPower);}
            float u=(OpenWidest-y)/(OpenWidest-OpenBottom);return OpenHalf*MathF.Pow(MathF.Max(0,1-MathF.Pow(u,OpenBottomPower)),1/OpenBottomPower);
        }
        public static float RimZ(float y)=>RimSide+(RimBrim-RimSide)*Smooth((y-.05f)/(OpenTop-.05f));
        /// <summary>The arch at parameter p in [-1, 1] (-1 the bottom, 0 the widest, 1 the top) on its right side: spaced evenly
        /// round the curve, so the rows that carry the rim follow it closely at its rounded ends.</summary>
        public static float OpeningY(float p)
        {
            float a=Math.Abs(p)*MathF.PI/2,s=MathF.Sin(a);
            return p>=0?OpenWidest+(OpenTop-OpenWidest)*MathF.Pow(s,2/OpenTopPower):OpenWidest-(OpenWidest-OpenBottom)*MathF.Pow(s,2/OpenBottomPower);
        }

        // ---- the shell ---------------------------------------------------------------------------------------------------
        /// <summary>The sections' middle (z), the hood's top, where the dome over the crown begins, the dome's half width and back
        /// there, and the sections' front and back exponents (a squarer front: the walls stand out to the rim).</summary>
        public const float CentreZ=-.005f,Top=.262f,DomeBase=.100f,DomeHalf=.155f,DomeBack=.175f,FrontPower=3.4f,BackPower=2.3f;
        /// <summary>Below the dome: the half width (the jaw's pinch, then out over the shoulders), the back, and the front under
        /// the chin (from the rim's bottom down to the chest), by height.</summary>
        private static readonly float[] HalfY={-.215f,-.15f,-.11f,-.075f,-.035f,.04f,DomeBase},HalfV={.215f,.212f,.188f,.152f,.137f,.152f,DomeHalf};
        private static readonly float[] BackY={-.215f,-.10f,-.04f,.03f,DomeBase},BackV={.152f,.158f,.158f,.166f,DomeBack};
        private static readonly float[] ChinY={-.215f,-.15f,-.12f,-.09f,OpenBottom},ChinV={.160f,.150f,.145f,.160f,RimSide-CentreZ};
        /// <summary>The cowl's hem by direction a (0 ahead, pi/2 to the right): down to the chest and the back, up over the shoulders.</summary>
        public const float HemFront=-.215f,HemRise=.090f;
        public static float Hem(float a){float s=MathF.Sin(a);return HemFront+HemRise*s*s;}
        /// <summary>The soft point up the back seam: how far it stands out, its height and spread.</summary>
        public const float PeakOut=.026f,PeakY=.205f,PeakSpread=.045f;

        private static float Smooth(float x){x=x<0?0:x>1?1:x;return x*x*(3-2*x);}
        // A Catmull-Rom curve through (ys, vs) (ys ascending), flat past the ends.
        private static float Spline(float y,float[] ys,float[] vs)
        {
            int n=ys.Length;if(y<=ys[0])return vs[0];if(y>=ys[n-1])return vs[n-1];
            int i=0;while(i<n-2&&y>ys[i+1])i++;
            float h=ys[i+1]-ys[i],t=(y-ys[i])/h,t2=t*t,t3=t2*t;
            float Slope(int k)=>k==0||k==n-1?0:(vs[k+1]-vs[k-1])/(ys[k+1]-ys[k-1]);
            float m0=Slope(i)*h,m1=Slope(i+1)*h;
            return (2*t3-3*t2+1)*vs[i]+(t3-2*t2+t)*m0+(-2*t3+3*t2)*vs[i+1]+(t3-t2)*m1;
        }
        private static float Dome(float y)=>y>DomeBase?MathF.Sqrt(MathF.Max(0,1-MathF.Pow((y-DomeBase)/(Top-DomeBase),2))):1;
        public static float HalfWidth(float y)=>y>DomeBase?DomeHalf*MathF.Pow(Dome(y),.9f):Spline(y,HalfY,HalfV);
        public static float Back(float y)=>y>DomeBase?DomeBack*MathF.Pow(Dome(y),.75f):Spline(y,BackY,BackV);
        /// <summary>The front: through the rim beside the opening; over the brow the brim rounding up to the top; under the chin
        /// down to the chest.</summary>
        public static float Front(float y)
        {
            float w=OpeningHalfWidth(y);
            if(w>0){float q=1-MathF.Pow(w/HalfWidth(y),FrontPower);return (RimZ(y)-CentreZ)/MathF.Pow(MathF.Max(q,1e-3f),1/FrontPower);}
            if(y>=OpenTop){float t=(y-OpenTop)/(Top-OpenTop);return (RimBrim-CentreZ)*MathF.Pow(MathF.Max(0,1-t*t),.4f);}
            return Spline(y,ChinY,ChinV);
        }
        private static float Peak(float y,float back)=>back<=0?0:PeakOut*MathF.Exp(-MathF.Pow((y-PeakY)/PeakSpread,2))*MathF.Pow(back,6);
        /// <summary>The surface at height y in direction a (0 ahead, pi/2 toward +x).</summary>
        public static Vector3 Section(float y,float a)
        {
            float sa=MathF.Sin(a),ca=MathF.Cos(a),x=HalfWidth(y),f=Front(y),b=Back(y)+Peak(y,-ca);
            if(x<1e-5f||f<1e-5f||b<1e-5f)return new Vector3(0,y,CentreZ);
            float p=ca>=0?FrontPower:BackPower,z=ca>=0?f:b;
            float r=MathF.Pow(MathF.Pow(Math.Abs(sa)/x,p)+MathF.Pow(Math.Abs(ca)/z,p),-1/p);
            return new Vector3(r*sa,y,CentreZ+r*ca);
        }
        /// <summary>The angle a section reaches the opening's side at (0 where the section is closed).</summary>
        public static float RimAngle(float y)
        {
            float w=OpeningHalfWidth(y);if(w<=0)return 0;
            float lo=0,hi=MathF.PI/2;for(int i=0;i<48;i++){float mid=(lo+hi)/2;if(Section(y,mid).X<w)lo=mid;else hi=mid;}
            return (lo+hi)/2;
        }

        // ---- the mesh ----------------------------------------------------------------------------------------------------
        /// <summary>Rows: over the brow to the top, along the opening, down the cowl (with a last narrow one for the hem's piping);
        /// columns round each row (from the opening's right side round the back to its left where it is open).</summary>
        public const int DomeRows=9,OpenRows=22,CowlRows=9,Columns=48,TubeSides=8;
        /// <summary>How much higher than the chin the back of the hood starts easing off the head (m).</summary>
        public const float NapeRise=.06f;
        /// <summary>The rim's piping tube (radius) and the hem's piping band (height, m).</summary>
        public const float TubeRadius=.0042f,HemBand=.006f;
        /// <summary>UV layout (UV0; the colour map and the cloth's paint map share it, read by texel): the cloth's rows run down v
        /// from 0 to SurfaceV, the hem's band row on a texel's edge (HemBandV), the rim's tube in v past TubeV.</summary>
        public const float SurfaceV=.85f,HemBandV=53f/64,TubeV=.92f;
        /// <summary>The rows: over the brow, the opening (both closed ends too), the cowl and its hem's band.</summary>
        public const int RowCount=DomeRows+OpenRows+1+CowlRows+1;
        /// <summary>The cloth holds the rows to the opening's bottom fixed (the head part) and lets the cowl's move: the boundary,
        /// halfway between those rows' v.</summary>
        public static float FixedV=>SurfaceV*(DomeRows+OpenRows+.5f)/(RowCount-1);

        private struct Row{public float A0;public bool Cowl;public float Y,S;}
        private static Row[] Rows()
        {
            var rows=new List<Row>();
            for(int i=0;i<DomeRows;i++)rows.Add(new Row{Y=OpenTop+(Top-OpenTop)*MathF.Cos(MathF.PI/2*i/DomeRows)});
            for(int i=0;i<=OpenRows;i++){float y=OpeningY(1-2f*i/OpenRows);rows.Add(new Row{Y=y,A0=i==0||i==OpenRows?0:RimAngle(y)});}
            for(int i=1;i<=CowlRows;i++)rows.Add(new Row{Cowl=true,S=i/(float)CowlRows});
            // The hem's narrow band row (S: a share of the way, set per column below), then the hem.
            var last=rows[rows.Count-1];rows[rows.Count-1]=new Row{Cowl=true,S=-1};rows.Add(last);
            return rows.ToArray();
        }
        private static float RowY(in Row r,float a)
        {
            if(!r.Cowl)return r.Y;float hem=Hem(a);
            float s=r.S>=0?r.S:1-HemBand/Math.Max(.01f,OpenBottom-hem);
            return OpenBottom+(hem-OpenBottom)*s;
        }

        /// <summary>The hood (see the class comment). Deterministic: the same arrays on every call.</summary>
        public static SheldonHoodMesh Build()
        {
            var rows=Rows();int R=rows.Length,K=Columns+1;if(R!=RowCount)throw new InvalidOperationException("Sheldon hood rows");
            var pos=new List<Vector3>();var uv=new List<Vector2>();var col=new List<Vector4>();
            for(int j=0;j<R;j++)
            {
                float a0=rows[j].A0,v=SurfaceV*j/(R-1);if(j==R-2)v=HemBandV;
                for(int k=0;k<K;k++)
                {
                    float a=a0+(2*MathF.PI-2*a0)*k/Columns;
                    pos.Add(j==0?new Vector3(0,Top,CentreZ):Section(RowY(rows[j],a),a));uv.Add(new Vector2((k+.5f)/K,v));col.Add(new Vector4(1,0,0,1));
                }
            }
            var outer=new List<int>();var inner=new List<int>();
            for(int j=0;j<R-1;j++)for(int k=0;k<Columns;k++)
            {
                int a=j*K+k,b=a+1,c=a+K+1,d=a+K;
                // Outward: (a, c, b) and (a, d, c) (the rows run down, the columns toward +x from ahead); the pole's row degenerates.
                if(j>0){outer.Add(a);outer.Add(c);outer.Add(b);inner.Add(a);inner.Add(b);inner.Add(c);}
                outer.Add(a);outer.Add(d);outer.Add(c);inner.Add(a);inner.Add(c);inner.Add(d);
            }
            var nrm=Normals(pos,outer);
            // A closed row's two ends are one point (the seam ahead): one normal; the pole points up.
            for(int j=0;j<R;j++){if(rows[j].A0>0)continue;var n=Vector3.Normalize(nrm[j*K]+nrm[j*K+Columns]);nrm[j*K]=nrm[j*K+Columns]=n;}
            for(int k=0;k<K;k++)nrm[k]=Vector3.UnitY;
            var normals=new List<Vector3>(nrm);
            // The rim's piping: a tube round the opening (the right side up, over the top, the left side down, under the chin).
            var rim=new List<Vector3>();var rimN=new List<Vector3>();
            int firstOpen=DomeRows,lastOpen=DomeRows+OpenRows;
            for(int j=lastOpen;j>=firstOpen;j--){rim.Add(pos[j*K]);rimN.Add(nrm[j*K]);}
            for(int j=firstOpen+1;j<lastOpen;j++){rim.Add(pos[j*K+Columns]);rimN.Add(nrm[j*K+Columns]);}
            int tube=pos.Count,m=rim.Count;
            for(int i=0;i<m;i++)
            {
                var along=Vector3.Normalize(rim[(i+1)%m]-rim[(i+m-1)%m]);var side=Vector3.Normalize(Vector3.Cross(along,rimN[i]));var up=Vector3.Cross(side,along);
                var centre=rim[i]+up*(TubeRadius*.35f);
                for(int s=0;s<TubeSides;s++)
                {
                    float t=2*MathF.PI*s/TubeSides;var n=up*MathF.Cos(t)+side*MathF.Sin(t);
                    pos.Add(centre+n*TubeRadius);normals.Add(n);uv.Add(new Vector2(.25f,(TubeV+1)/2));col.Add(new Vector4(1,0,0,0));
                }
            }
            for(int i=0;i<m;i++)for(int s=0;s<TubeSides;s++)
            {
                int a=tube+i*TubeSides+s,b=tube+i*TubeSides+(s+1)%TubeSides,c=tube+((i+1)%m)*TubeSides+(s+1)%TubeSides,d=tube+((i+1)%m)*TubeSides+s;
                // Outward from the tube's centre line.
                var mid=(pos[a]+pos[c])/2;var ctr=(rim[i]+rim[(i+1)%m])/2;var face=Vector3.Cross(pos[d]-pos[a],pos[b]-pos[a]);
                if(Vector3.Dot(face,mid-ctr)>=0){outer.Add(a);outer.Add(d);outer.Add(b);outer.Add(b);outer.Add(d);outer.Add(c);}
                else{outer.Add(a);outer.Add(b);outer.Add(d);outer.Add(b);outer.Add(c);outer.Add(d);}
            }
            var mesh=new SheldonHoodMesh{Positions=pos.ToArray(),Normals=normals.ToArray(),Uv=uv.ToArray(),Colors=col.ToArray(),Outer=outer.ToArray(),Inner=inner.ToArray()};
            int nv=mesh.Positions.Length;mesh.Bones=new int[nv*4];mesh.Weights=new float[nv*4];
            for(int i=0;i<nv;i++)Weigh(mesh.Positions[i],i<tube,mesh.Bones,mesh.Weights,i*4);
            return mesh;
        }
        private static Vector3[] Normals(List<Vector3> pos,List<int> tris)
        {
            var n=new Vector3[pos.Count];
            for(int i=0;i+2<tris.Count;i+=3){var a=pos[tris[i]];var f=Vector3.Cross(pos[tris[i+1]]-a,pos[tris[i+2]]-a);n[tris[i]]+=f;n[tris[i+1]]+=f;n[tris[i+2]]+=f;}
            for(int i=0;i<n.Length;i++){float l=n[i].Length();n[i]=l>1e-12f?n[i]/l:Vector3.UnitY;}
            return n;
        }
        /// <summary>A point's bones and weights: the head down to the chin (and the rim's piping all of it), the back of the hood
        /// easing onto the neck from higher up (the nape follows the neck, not the turning head, over a hoodie's hood); below,
        /// through the neck to the upper spine, the cowl's sides partly on their collarbone.</summary>
        public static void Weigh(Vector3 p,bool cloth,int[] bones,float[] weights,int at)
        {
            float from=OpenBottom+NapeRise*Smooth(-p.Z/.1f),head=cloth?1-Smooth((from-p.Y)/.06f):1,rest=1-head,neck=rest*(1-Smooth((-.075f-p.Y)/.05f)),spine=rest-neck;
            float side=.65f*Smooth((Math.Abs(p.X)-.07f)/.09f),clav=spine*side;spine-=clav;
            bones[at]=HeadBone;bones[at+1]=NeckBone;bones[at+2]=SpineBone;bones[at+3]=p.X<0?ClavicleLeft:ClavicleRight;
            weights[at]=head;weights[at+1]=neck;weights[at+2]=spine;weights[at+3]=clav;
        }

        // ---- the maps ----------------------------------------------------------------------------------------------------
        /// <summary>The colour map and the cloth's paint map (Size x Size, RGBA32 rows from v 0): the visual body shader's colour
        /// weights (red: the cloth, its first colour; blue: the piping, the accent the glow takes) and Magica's (red fixed,
        /// green moving).</summary>
        public const int MapSize=64;
        public static void Maps(byte[] colour,byte[] paint)
        {
            for(int y=0;y<MapSize;y++)for(int x=0;x<MapSize;x++)
            {
                float v=(float)y/MapSize;int i=(y*MapSize+x)*4;bool pipe=v>=HemBandV-1e-4f&&v<SurfaceV+.02f||v>=TubeV;bool still=v<FixedV||v>=TubeV;
                colour[i]=(byte)(pipe?0:255);colour[i+1]=0;colour[i+2]=(byte)(pipe?255:0);colour[i+3]=255;
                paint[i]=(byte)(still?255:0);paint[i+1]=(byte)(still?0:255);paint[i+2]=0;paint[i+3]=255;
            }
        }

        // ---- the face's void ----------------------------------------------------------------------------------------------
        /// <summary>The void's plate in the Sheldon Head's own frame (NativeSheldonHead: source frame, +x the rig's left), so it
        /// rides the face's fit with the eyes and grin: flat at VoidZ (model) just over the nose, its outline the opening's
        /// grown by VoidMargin (inside the hood's walls, behind the rim), its lower half reaching VoidChin further down under the
        /// chin (a look down through the opening meets it, not the gap between the cowl and the chest), with the face's soft glow.</summary>
        public const float VoidZ=.160f,VoidMargin=.010f,VoidChin=.016f;
        public static SheldonSection Void()
        {
            var s=new SheldonSection{Part="sheldon-hood-void",Bone=CapturedRig.Head};
            var sc=NativeModelHats.SheldonScale;var off=NativeModelHats.SheldonOffset;
            Vector2 ToVoxel(float x,float y)=>new Vector2(x/sc.X,(y-off.Y)/sc.Y);
            float z=(VoidZ-off.Z)/sc.Z;
            // The outline (model frame): up the right side, over the top, down the left, under the chin.
            var outline=new List<Vector2>();const int side=24;
            for(int i=0;i<=side;i++){float p=-1+2f*i/side,y=OpeningY(p);outline.Add(new Vector2(OpeningHalfWidth(y)+VoidMargin,y-(p<0?VoidChin*-p:0)));}
            outline[0]=new Vector2(0,OpenBottom-VoidMargin-VoidChin);outline[side]=new Vector2(0,OpenTop+VoidMargin);
            for(int i=side-1;i>0;i--)outline.Add(new Vector2(-outline[i].X,outline[i].Y));
            var features=NativeSheldonHead.FaceFeatures();var colour=NativeSheldonHead.VoidColor;
            var centre=new Vector2(0,OpenWidest);const int rings=6;int n=outline.Count;
            int hub=s.Add(new Vector3(ToVoxel(centre.X,centre.Y),z),Vector3.UnitZ,colour,NativeSheldonHead.VoidBloom(features,ToVoxel(centre.X,centre.Y)));
            for(int r=1;r<=rings;r++)for(int i=0;i<n;i++)
            {
                var p=centre+(outline[i]-centre)*(r/(float)rings);var q=ToVoxel(p.X,p.Y);
                s.Add(new Vector3(q,z),Vector3.UnitZ,colour,NativeSheldonHead.VoidBloom(features,q));
            }
            // Counter-clockwise seen from +z (the face's side), as the Sheldon Head's sections are.
            for(int i=0;i<n;i++){int a=hub+1+i,b=hub+1+(i+1)%n;Ccw(s,hub,a,b);}
            for(int r=1;r<rings;r++)for(int i=0;i<n;i++)
            {
                int a=hub+1+(r-1)*n+i,b=hub+1+(r-1)*n+(i+1)%n,c=hub+1+r*n+(i+1)%n,d=hub+1+r*n+i;Ccw(s,a,d,c);Ccw(s,a,c,b);
            }
            return s;
        }
        private static void Ccw(SheldonSection s,int a,int b,int c)
        {
            var p=s.Positions;if(Vector3.Cross(p[b]-p[a],p[c]-p[a]).Z>=0)s.Tri(a,b,c);else s.Tri(a,c,b);
        }
    }
}
