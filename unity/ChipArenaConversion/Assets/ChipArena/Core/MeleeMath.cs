using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // Three.js-compatible helpers for the captured-melee port (MeleeMotion). The source layers mix Euler writes,
    // quaternion slerps and basis solves; matching three's exact conventions keeps the port's poses on the source's.
    // Source frame throughout (+x is the rig's left, +z forward); the Unity boundary mirrors once in CapturedAnimation.
    public static class MeleeMath
    {
        public const float TAU=(float)(Math.PI*2),PI=(float)Math.PI;
        public static readonly Vector3 X=Vector3.UnitX,Y=Vector3.UnitY,Z=Vector3.UnitZ;
        public static float Clamp(float v,float a,float b)=>Math.Max(a,Math.Min(b,v));
        public static float Lerp(float a,float b,float t)=>a+(b-a)*t;
        // smoothMotion (motion-quality): the quintic ease every melee layer uses.
        public static float Ease(float x){x=Clamp(x,0,1);return x*x*x*(x*(x*6-15)+10);}
        // shared smooth (souls-combat): the cubic of the authoritative step travel.
        public static float Smooth(float x){x=Clamp(x,0,1);return x*x*(3-2*x);}
        public static float SmoothStep(float x,float min,float max){if(x<=min)return 0;if(x>=max)return 1;x=(x-min)/(max-min);return x*x*(3-2*x);}
        public static float Wrap(float a)=>a-TAU*(float)Math.Floor(a/TAU+.5f); // JavaScript Math.round
        public static float Frac(float x)=>x-(float)Math.Floor(x);
        public static float Hermite(float p0,float p1,float m0,float m1,float s){float s2=s*s,s3=s2*s;return (2*s3-3*s2+1)*p0+(s3-2*s2+s)*m0+(3*s2-2*s3)*p1+(s3-s2)*m1;}
        // pose-curves poseCurve: monotone cubic Hermite with zero end tangents.
        public static float PoseCurve(float[] times,float[] values,float time)
        {
            if(time<=times[0])return values[0];int last=times.Length-1;if(time>=times[last])return values[last];int i=0;while(times[i+1]<time)i++;
            float Slope(int n)=>(values[n+1]-values[n])/(times[n+1]-times[n]);
            float Tangent(int n){if(n==0||n==last)return 0;float a=Slope(n-1),b=Slope(n);return a*b<=0?0:2*a*b/(a+b);}
            float h=times[i+1]-times[i],u=(time-times[i])/h,u2=u*u,u3=u2*u;
            return (2*u3-3*u2+1)*values[i]+(u3-2*u2+u)*h*Tangent(i)+(-2*u3+3*u2)*values[i+1]+(u3-u2)*h*Tangent(i+1);
        }
        // FNV-1a phase of a player id, as the source's hashPhase (Math.imul wrap included).
        public static float HashPhase(string id)
        {
            if(string.IsNullOrEmpty(id))return 0;uint h=2166136261;foreach(char c in id){h^=c;h=unchecked(h*16777619);}
            return (float)(h%9973/9973.0*Math.PI*2);
        }
        public static Vector3 Unit(Vector3 v)=>v.LengthSquared()>0?Vector3.Normalize(v):v;
        public static Vector3 Rotate(Vector3 v,Quaternion q)=>Vector3.Transform(v,q);
        public static Quaternion Inverse(Quaternion q)=>Quaternion.Conjugate(Quaternion.Normalize(q));
        public static Quaternion AxisAngle(Vector3 axis,float angle){float h=angle*.5f,s=(float)Math.Sin(h);return new Quaternion(axis.X*s,axis.Y*s,axis.Z*s,(float)Math.Cos(h));}
        public static Quaternion Euler(float x,float y,float z)
        {
            float c1=(float)Math.Cos(x/2),c2=(float)Math.Cos(y/2),c3=(float)Math.Cos(z/2),s1=(float)Math.Sin(x/2),s2=(float)Math.Sin(y/2),s3=(float)Math.Sin(z/2);
            return new Quaternion(s1*c2*c3+c1*s2*s3,c1*s2*c3-s1*c2*s3,c1*c2*s3+s1*s2*c3,c1*c2*c3-s1*s2*s3);
        }
        public static Quaternion Euler(Vector3 e)=>Euler(e.X,e.Y,e.Z);
        public static Quaternion EulerYXZ(float x,float y,float z)
        {
            float c1=(float)Math.Cos(x/2),c2=(float)Math.Cos(y/2),c3=(float)Math.Cos(z/2),s1=(float)Math.Sin(x/2),s2=(float)Math.Sin(y/2),s3=(float)Math.Sin(z/2);
            return new Quaternion(s1*c2*c3+c1*s2*s3,c1*s2*c3-s1*c2*s3,c1*c2*s3-s1*s2*c3,c1*c2*c3+s1*s2*s3);
        }
        // Euler.setFromQuaternion (order XYZ), through the rotation matrix exactly as three does.
        public static Vector3 ToEuler(Quaternion q)
        {
            float x=q.X,y=q.Y,z=q.Z,w=q.W,x2=x+x,y2=y+y,z2=z+z,xx=x*x2,xy=x*y2,xz=x*z2,yy=y*y2,yz=y*z2,zz=z*z2,wx=w*x2,wy=w*y2,wz=w*z2;
            float m11=1-(yy+zz),m12=xy-wz,m13=xz+wy,m22=1-(xx+zz),m23=yz-wx,m32=yz+wx,m33=1-(xx+yy);
            float ey=(float)Math.Asin(Clamp(m13,-1,1));
            return Math.Abs(m13)<.9999999f?new Vector3((float)Math.Atan2(-m23,m33),ey,(float)Math.Atan2(-m12,m11)):new Vector3((float)Math.Atan2(m32,m22),ey,0);
        }
        // Quaternion.slerp as three implements it (shortest arc, normalised lerp near identity).
        public static Quaternion Slerp(Quaternion a,Quaternion b,float t)
        {
            if(t==0)return a;if(t==1)return b;
            float x=a.X,y=a.Y,z=a.Z,w=a.W,cos=w*b.W+x*b.X+y*b.Y+z*b.Z;
            if(cos<0){b=new Quaternion(-b.X,-b.Y,-b.Z,-b.W);cos=-cos;}
            if(cos>=1)return a;
            float sqrSin=1-cos*cos;
            if(sqrSin<=1.1920929E-07f){float s=1-t;return Quaternion.Normalize(new Quaternion(s*x+t*b.X,s*y+t*b.Y,s*z+t*b.Z,s*w+t*b.W));}
            float sin=(float)Math.Sqrt(sqrSin),half=(float)Math.Atan2(sin,cos),ra=(float)Math.Sin((1-t)*half)/sin,rb=(float)Math.Sin(t*half)/sin;
            return new Quaternion(x*ra+b.X*rb,y*ra+b.Y*rb,z*ra+b.Z*rb,w*ra+b.W*rb);
        }
        public static float AngleTo(Quaternion a,Quaternion b)=>2*(float)Math.Acos(Math.Abs(Clamp(Quaternion.Dot(a,b),-1,1)));
        // Quaternion.setFromUnitVectors.
        public static Quaternion FromTo(Vector3 from,Vector3 to)
        {
            float r=Vector3.Dot(from,to)+1;Quaternion q;
            if(r<1e-6f){r=0;q=Math.Abs(from.X)>Math.Abs(from.Z)?new Quaternion(-from.Y,from.X,0,r):new Quaternion(0,-from.Z,from.Y,r);}
            else{Vector3 c=Vector3.Cross(from,to);q=new Quaternion(c.X,c.Y,c.Z,r);}
            return Quaternion.Normalize(q);
        }
        // Quaternion.setFromRotationMatrix(Matrix4.makeBasis(x, y, z)).
        public static Quaternion Basis(Vector3 bx,Vector3 by,Vector3 bz)
        {
            float m11=bx.X,m12=by.X,m13=bz.X,m21=bx.Y,m22=by.Y,m23=bz.Y,m31=bx.Z,m32=by.Z,m33=bz.Z,trace=m11+m22+m33;
            if(trace>0){float s=.5f/(float)Math.Sqrt(trace+1);return new Quaternion((m32-m23)*s,(m13-m31)*s,(m21-m12)*s,.25f/s);}
            if(m11>m22&&m11>m33){float s=2*(float)Math.Sqrt(1+m11-m22-m33);return new Quaternion(.25f*s,(m12+m21)/s,(m13+m31)/s,(m32-m23)/s);}
            if(m22>m33){float s=2*(float)Math.Sqrt(1+m22-m11-m33);return new Quaternion((m12+m21)/s,.25f*s,(m23+m32)/s,(m13-m31)/s);}
            {float s=2*(float)Math.Sqrt(1+m33-m11-m22);return new Quaternion((m13+m31)/s,(m23+m32)/s,.25f*s,(m21-m12)/s);}
        }
        public static Vector3 ApplyAxisAngle(Vector3 v,Vector3 axis,float angle)=>Vector3.Transform(v,AxisAngle(axis,angle));
        public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*t;
    }
}
