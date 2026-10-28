using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// A frame's volumetric beams (docs/LIGHTING-ADVANCED.md "Volumetric beams"; Runtime NativeVolumetricBeams draws them,
    /// Resources/NativeVolumetricBeam.shader shades them). The maps' FX builders (Brickfall's sun shafts, the Factory's skylight
    /// sheets, Central Station's moonlight and sodium cones, the town's lamp shafts, Ward Zero's and the Ironworks' shafts) add
    /// their shafts here in place of their billboard quads while the runtime hands them a list (the medium and high tiers with
    /// the look's `beams` on); without one (the low tier, CI, every other look) they draw the billboards exactly as before.
    /// <para>A beam is a soft cone (or an elliptic cylinder) from its source `A` to its end `B`: radius `R0` at the source and
    /// `R1` at the end across `Wide` (unit, perpendicular to the axis), `Aspect` times that across the other axis; its light
    /// (linear, HDR: the bloom takes it past 1) at the source, fading to nothing at the end; `Dust` how much the dust breaks
    /// it; `Seed` its own dust offset. The billboard's look carries over: its colour times its opacity times
    /// <see cref="FromBillboard"/> is the light at the beam's centre line.</para>
    /// Fixed capacity; Add refuses past it. Allocation free.
    /// </summary>
    public sealed class NativeBeamList
    {
        public const int Capacity=128;
        /// <summary>A billboard shaft's opacity to a beam's light: the beam's soft edge and fall-off keep about this share of a flat sheet's.</summary>
        public const float FromBillboard=2.4f;
        public int Count;
        public readonly float[] Ax=new float[Capacity],Ay=new float[Capacity],Az=new float[Capacity],Bx=new float[Capacity],By=new float[Capacity],Bz=new float[Capacity];
        public readonly float[] R0=new float[Capacity],R1=new float[Capacity],Aspect=new float[Capacity],Wx=new float[Capacity],Wy=new float[Capacity],Wz=new float[Capacity];
        public readonly float[] R=new float[Capacity],G=new float[Capacity],B=new float[Capacity],Dust=new float[Capacity],Seed=new float[Capacity];
        /// <summary>The look's gain on every beam (NativeAdvancedLooks' beams.gain; the runtime sets it before the frame's builds).</summary>
        public float Gain=1;
        public void Clear()=>Count=0;
        /// <summary>A beam from (ax, ay, az) to (bx, by, bz), radii r0 and r1, light r, g, b (linear), its dust share and seed;
        /// `wide` the cross axis the radii run along (any vector not along the axis; zero picks one), `aspect` the other's share.</summary>
        public bool Add(double ax,double ay,double az,double bx,double by,double bz,double r0,double r1,double r,double g,double b,double dust=.6,double seed=0,double wx=0,double wy=0,double wz=0,double aspect=1)
        {
            double dx=bx-ax,dy=by-ay,dz=bz-az,len=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(Count>=Capacity||len<.05||!(r+g+b>0))return false;dx/=len;dy/=len;dz/=len;
            // The cross axis: `wide` with its part along the axis taken off, else the axis crossed with up (or x for a vertical axis).
            double d=wx*dx+wy*dy+wz*dz;double cx=wx-d*dx,cy=wy-d*dy,cz=wz-d*dz,cl=Math.Sqrt(cx*cx+cy*cy+cz*cz);
            if(cl<1e-4){if(Math.Abs(dy)<.95){cx=dz;cy=0;cz=-dx;}else{cx=1;cy=0;cz=0;double e=cx*dx;cx-=e*dx;cy-=e*dy;cz-=e*dz;}cl=Math.Sqrt(cx*cx+cy*cy+cz*cz);}
            int i=Count++;Ax[i]=(float)ax;Ay[i]=(float)ay;Az[i]=(float)az;Bx[i]=(float)bx;By[i]=(float)by;Bz[i]=(float)bz;R0[i]=(float)Math.Max(.02,r0);R1[i]=(float)Math.Max(.02,r1);Aspect[i]=(float)Math.Max(.05,aspect);
            Wx[i]=(float)(cx/cl);Wy[i]=(float)(cy/cl);Wz[i]=(float)(cz/cl);R[i]=(float)(r*Gain);G[i]=(float)(g*Gain);B[i]=(float)(b*Gain);Dust[i]=(float)Math.Max(0,Math.Min(1,dust));Seed[i]=(float)seed;return true;
        }
        /// <summary>A billboard shaft's beam: the sheet from (ax, ay, az) to (bx, by, bz), `w0` and `w1` wide, colour times opacity.</summary>
        public bool Shaft(double ax,double ay,double az,double bx,double by,double bz,double w0,double w1,double r,double g,double b,double alpha,double dust=.6,double seed=0)
        {double k=alpha*FromBillboard;return Add(ax,ay,az,bx,by,bz,w0*.5,w1*.5,r*k,g*k,b*k,dust,seed);}

        /// <summary>The share of a beam a camera at (ex, ey, ez) sees: 1 outside, fading out from <see cref="InsideFade"/> times
        /// the beam's radius at the camera's place along it down to the radius itself (inside: 0), and 1 beyond either end.</summary>
        public float Outside(int i,double ex,double ey,double ez)
        {
            double dx=Bx[i]-Ax[i],dy=By[i]-Ay[i],dz=Bz[i]-Az[i],len=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(len<1e-4)return 1;dx/=len;dy/=len;dz/=len;
            double px=ex-Ax[i],py=ey-Ay[i],pz=ez-Az[i],t=px*dx+py*dy+pz*dz;if(t<0||t>len)return 1;
            double qx=px-dx*t,qy=py-dy*t,qz=pz-dz*t,along=t/len,r=R0[i]+(R1[i]-R0[i])*along;
            // The ellipse's radius toward the camera: the cross axis's share at the radius, the other at its aspect.
            double q=Math.Sqrt(qx*qx+qy*qy+qz*qz);if(q<1e-5)return 0;double cw=(qx*Wx[i]+qy*Wy[i]+qz*Wz[i])/q,a=Aspect[i];double rr=r*a/Math.Sqrt(a*a*cw*cw+(1-cw*cw));
            double f=(q/rr-1)/(InsideFade-1);return (float)(f<0?0:f>1?1:f*f*(3-2*f));
        }
        public const double InsideFade=1.6;

        /// <summary>The unit beam the runtime instances: an open cone side along +y from y 0 (radius 1) to y 1 (radius 1; the
        /// vertex stage narrows or widens it per beam), `segments` round and `rings` along; positions and normals (3 each),
        /// triangles wound outward.</summary>
        public static void UnitMesh(int segments,int rings,out float[] positions,out float[] normals,out int[] triangles)
        {
            int n=(segments+1)*(rings+1);positions=new float[n*3];normals=new float[n*3];triangles=new int[segments*rings*6];int t=0;
            for(int r=0;r<=rings;r++)for(int s=0;s<=segments;s++)
            {
                int v=r*(segments+1)+s;double a=2*Math.PI*s/segments;float c=(float)Math.Cos(a),sn=(float)Math.Sin(a);
                positions[v*3]=c;positions[v*3+1]=(float)r/rings;positions[v*3+2]=sn;normals[v*3]=c;normals[v*3+1]=0;normals[v*3+2]=sn;
            }
            for(int r=0;r<rings;r++)for(int s=0;s<segments;s++)
            {
                int a=r*(segments+1)+s,b=a+1,c=a+segments+1,d=c+1;
                triangles[t++]=a;triangles[t++]=c;triangles[t++]=b;triangles[t++]=b;triangles[t++]=c;triangles[t++]=d;
            }
        }
    }
}
