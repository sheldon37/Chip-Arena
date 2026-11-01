using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ChipArena.UnityConversion
{
    // The model's clips as the bake stores them (Core NativeVisualMotion; the local tool samples each pack clip on the body, in
    // place and with its foot IK, and writes Resources/ChipArenaVisualMotion.bytes): per clip its length, its Hit event, its
    // loop and, at a fixed rate, each frame's humanoid pose (the body's position and rotation and every muscle). A few hundred
    // kilobytes per weapon set where the pack's own files would be tens of megabytes. Sampled here between frames and mixed by
    // weight; the presentation sets the mixed pose on the model's skeleton. Little-endian:
    // "CAMO", version 1, muscle count, clip count; per clip: name (length, UTF-8), length, hit, rate, its travel on the body
    // (x, z m/s), loop byte, frame count, then frames of (body position xyz, body rotation xyzw, muscles).
    public static class NativeVisualMotionClips
    {
        public const int Version=1, Head=7;
        public sealed class Clip
        {
            public string Name; public float Length, Hit, Rate, SpeedX, SpeedZ; public bool Loop; public int Frames, Stride; public float[] Data;
        }
        public sealed class Library : NativeVisualMotion.IClips
        {
            public int Muscles; public readonly Dictionary<string,Clip> Clips=new Dictionary<string,Clip>();
            public bool Has(string clip)=>clip!=null&&Clips.ContainsKey(clip);
            public float Length(string clip)=>Clips.TryGetValue(clip,out var c)?c.Length:1;
            public float Hit(string clip)=>Clips.TryGetValue(clip,out var c)?c.Hit:.5f;
        }

        public static Library Read(byte[] bytes)
        {
            using(var r=new BinaryReader(new MemoryStream(bytes),Encoding.UTF8))
            {
                if(r.ReadByte()!='C'||r.ReadByte()!='A'||r.ReadByte()!='M'||r.ReadByte()!='O')throw new InvalidDataException("not a motion bank");
                if(r.ReadInt32()!=Version)throw new InvalidDataException("motion bank version");
                var lib=new Library{Muscles=r.ReadInt32()};int count=r.ReadInt32();
                if(lib.Muscles<1||lib.Muscles>512||count<0||count>10000)throw new InvalidDataException("motion bank header");
                for(int k=0;k<count;k++)
                {
                    var c=new Clip{Name=Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32())),Length=r.ReadSingle(),Hit=r.ReadSingle(),Rate=r.ReadSingle(),SpeedX=r.ReadSingle(),SpeedZ=r.ReadSingle(),Loop=r.ReadByte()!=0,Frames=r.ReadInt32(),Stride=Head+lib.Muscles};
                    if(c.Frames<1||c.Frames>100000||!(c.Rate>0))throw new InvalidDataException("motion bank clip "+c.Name);
                    c.Data=new float[c.Frames*c.Stride];for(int i=0;i<c.Data.Length;i++)c.Data[i]=r.ReadSingle();
                    lib.Clips[c.Name]=c;
                }
                return lib;
            }
        }
        public static byte[] Write(int muscles,IEnumerable<Clip> clips)
        {
            var list=new List<Clip>(clips);
            using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Encoding.UTF8))
            {
                w.Write((byte)'C');w.Write((byte)'A');w.Write((byte)'M');w.Write((byte)'O');w.Write(Version);w.Write(muscles);w.Write(list.Count);
                foreach(var c in list)
                {
                    var name=Encoding.UTF8.GetBytes(c.Name);w.Write(name.Length);w.Write(name);w.Write(c.Length);w.Write(c.Hit);w.Write(c.Rate);w.Write(c.SpeedX);w.Write(c.SpeedZ);w.Write((byte)(c.Loop?1:0));w.Write(c.Frames);
                    foreach(var f in c.Data)w.Write(f);
                }
                w.Flush();return m.ToArray();
            }
        }

        /// <summary>The body's turn in clip `c` at `time` (its nearest frame; radians, clockwise seen from above, 0 its first
        /// frame's forward when that faces ahead).</summary>
        public static float Yaw(Clip c,float time)
        {
            int last=c.Frames-1,f=(int)Math.Round(time*c.Rate);f=f<0?0:f>last?last:f;int o=f*c.Stride;
            float x=c.Data[o+3],y=c.Data[o+4],z=c.Data[o+5],w=c.Data[o+6];
            return (float)Math.Atan2(2*(x*z+w*y),1-2*(x*x+y*y));
        }
        /// <summary>Adds `weight` times clip `c`'s pose at `time` (looped or held at its ends) into `sum` (Head + muscles
        /// floats; the rotation kept on the hemisphere of what `sum` holds). Allocation free.</summary>
        public static void Add(Clip c,float time,float weight,float[] sum)
        {
            if(weight<=0)return;
            float f=time*c.Rate;int last=c.Frames-1;bool loop=c.Loop&&last>0;
            if(loop){f%=last;if(f<0)f+=last;}else f=f<0?0:f>last?last:f;
            int a=(int)f;if(a>=last)a=last;int b=a<last?a+1:a;float t=f-a;
            // Between its frames the clip follows a smooth curve through the frames either side (a monotone cubic: no
            // overshoot past a frame; a straight line between two frames bent the motion at every frame, 30 times a second).
            int p=a>0?a-1:loop?last-1:a,n=b<last?b+1:loop?(b+1)%last:b;
            int ip=p*c.Stride,ia=a*c.Stride,ib=b*c.Stride,iq=n*c.Stride;
            // The rotation: on the running sum's side, its neighbours on its own.
            float dot=0;for(int i=3;i<7;i++)dot+=sum[i]*c.Data[ia+i];float sign=dot<0?-1:1;
            float sp=Side(c.Data,ia,ip),sb=Side(c.Data,ia,ib),sq=Side(c.Data,ia,iq);
            float t2=t*t,t3=t2*t,h00=2*t3-3*t2+1,h10=t3-2*t2+t,h01=-2*t3+3*t2,h11=t3-t2;
            for(int i=0;i<c.Stride;i++)
            {
                float v0=c.Data[ip+i],v1=c.Data[ia+i],v2=c.Data[ib+i],v3=c.Data[iq+i];
                if(i>=3&&i<7){v0*=sp*sign;v1*=sign;v2*=sb*sign;v3*=sq*sign;}
                float d0=v1-v0,d1=v2-v1,d2=v3-v2;
                float m1=p==a?d1:d0*d1<=0?0:(d0+d1)*.5f,m2=n==b?d1:d1*d2<=0?0:(d1+d2)*.5f;
                float lim=3*Math.Abs(d1);if(Math.Abs(m1)>lim)m1=Math.Sign(m1)*lim;if(Math.Abs(m2)>lim)m2=Math.Sign(m2)*lim;
                sum[i]+=weight*(h00*v1+h10*m1+h01*v2+h11*m2);
            }
        }
        // +1 when frame `j`'s rotation lies on frame `i`'s hemisphere, else -1.
        private static float Side(float[] d,int i,int j){float q=0;for(int k=3;k<7;k++)q+=d[i+k]*d[j+k];return q<0?-1:1;}
        /// <summary>A looping stride's phase (0..1): where its left knee is most bent against the right, from the first harmonic
        /// of the two knees' difference over the loop (`left`, `right`: the Lower Leg Stretch muscles). The pack's runs and walks
        /// do not agree on it (their left feet land up to 0.4 of a stride apart), so mixed at one phase their legs scissored and
        /// slid; each offset onto one phase, they step together.</summary>
        public static float StridePhase(Clip c,int left,int right)
        {
            int n=c.Loop&&c.Frames>2?c.Frames-1:c.Frames;if(n<2||left<0||right<0||Head+Math.Max(left,right)>=c.Stride)return 0;
            double re=0,im=0;
            for(int f=0;f<n;f++){int o=f*c.Stride+Head;double d=c.Data[o+right]-c.Data[o+left],ang=2*Math.PI*f/n;re+=d*Math.Cos(ang);im+=d*Math.Sin(ang);}
            if(re*re+im*im<1e-12)return 0;
            double ph=Math.Atan2(im,re)/(2*Math.PI);return (float)(ph-Math.Floor(ph));
        }
        /// <summary>The mixed pose from the sum of weights `total`: everything averaged, the rotation normalised.</summary>
        public static void Finish(float[] sum,float total)
        {
            if(!(total>1e-6f))return;
            for(int i=0;i<sum.Length;i++)sum[i]/=total;
            float n=(float)Math.Sqrt(sum[3]*sum[3]+sum[4]*sum[4]+sum[5]*sum[5]+sum[6]*sum[6]);
            if(n>1e-6f)for(int i=3;i<7;i++)sum[i]/=n;else{sum[3]=sum[4]=sum[5]=0;sum[6]=1;}
        }
    }
}
