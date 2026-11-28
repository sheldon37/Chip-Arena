using System;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
namespace ChipArena.UnityConversion
{
    // Verification only: fixed NIST P-256 / SHA-256 / IEEE P1363 (r || s).
    // Uses managed BigInteger rather than Mono's unimplemented ECDsa.Create(ECParameters).
    // Jacobian coordinates avoid an inverse per point operation. All operands are public;
    // this is deliberately NOT a constant-time signing or key-generation implementation.
    public static class NativeP256
    {
        private static BigInteger Hex(string hex)=>BigInteger.Parse("0"+hex,NumberStyles.HexNumber,CultureInfo.InvariantCulture);
        private static readonly BigInteger P=Hex("FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF");
        private static readonly BigInteger N=Hex("FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551");
        private static readonly BigInteger B=Hex("5AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B");
        private static readonly Point G=new Point(Hex("6B17D1F2E12C4247F8BCE6E563A440F277037D812DEB33A0F4A13945D898C296"),Hex("4FE342E2FE1A7F9B8EE7EB4A7C0F9E162BCE33576B315ECECBB6406837BF51F5"),1);
        private struct Point
        {
            public BigInteger X,Y,Z;
            public Point(BigInteger x,BigInteger y,BigInteger z){X=x;Y=y;Z=z;}
        }
        private static BigInteger Mod(BigInteger value){value%=P;return value.Sign<0?value+P:value;}
        private static BigInteger Unsigned(byte[] bytes,int offset,int length)
        {var little=new byte[length+1];for(int i=0;i<length;i++)little[i]=bytes[offset+length-1-i];return new BigInteger(little);}
        private static Point Double(Point q)
        {
            if(q.Z.IsZero||q.Y.IsZero)return default;
            var yy=Mod(q.Y*q.Y);var zz=Mod(q.Z*q.Z);var slope=Mod(3*(q.X*q.X-zz*zz));var v=Mod(4*q.X*yy);
            var x=Mod(slope*slope-2*v);return new Point(x,Mod(slope*(v-x)-8*yy*yy),Mod(2*q.Y*q.Z));
        }
        private static Point Add(Point a,Point b)
        {
            if(a.Z.IsZero)return b;if(b.Z.IsZero)return a;
            var z1=Mod(a.Z*a.Z);var z2=Mod(b.Z*b.Z);var u1=Mod(a.X*z2);var u2=Mod(b.X*z1);
            var s1=Mod(a.Y*b.Z*z2);var s2=Mod(b.Y*a.Z*z1);var h=Mod(u2-u1);var r=Mod(s2-s1);
            if(h.IsZero)return r.IsZero?Double(a):default;
            var hh=Mod(h*h);var hhh=Mod(h*hh);var v=Mod(u1*hh);var x=Mod(r*r-hhh-2*v);
            return new Point(x,Mod(r*(v-x)-s1*hhh),Mod(h*a.Z*b.Z));
        }
        private static Point Multiply(Point point,BigInteger scalar)
        {Point result=default;while(scalar>0){if(!scalar.IsEven)result=Add(result,point);point=Double(point);scalar>>=1;}return result;}
        public static bool Verify(byte[] data,byte[] signature,byte[] x,byte[] y)
        {
            if(data==null||signature==null||signature.Length!=64||x==null||x.Length!=32||y==null||y.Length!=32)return false;
            var qx=Unsigned(x,0,32);var qy=Unsigned(y,0,32);
            if(qx>=P||qy>=P||Mod(qy*qy-qx*qx*qx+3*qx-B)!=0)return false;
            var r=Unsigned(signature,0,32);var s=Unsigned(signature,32,32);if(r<=0||r>=N||s<=0||s>=N)return false;
            byte[] digest;using(var hash=SHA256.Create())digest=hash.ComputeHash(data);
            var inverse=BigInteger.ModPow(s,N-2,N);var result=Add(Multiply(G,Unsigned(digest,0,32)*inverse%N),Multiply(new Point(qx,qy,1),r*inverse%N));
            if(result.Z.IsZero)return false;var z=BigInteger.ModPow(result.Z,P-2,P);return Mod(result.X*z*z)%N==r;
        }
    }
}
