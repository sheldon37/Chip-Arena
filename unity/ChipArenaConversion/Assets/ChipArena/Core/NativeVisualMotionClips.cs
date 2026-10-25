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
            float f=time*c.Rate;int last=c.Frames-1;
            if(c.Loop&&last>0){f%=last;if(f<0)f+=last;}else f=f<0?0:f>last?last:f;
            int a=(int)f;if(a>=last)a=last;int b=a<last?a+1:a;float t=f-a;
            int ia=a*c.Stride,ib=b*c.Stride;
            // The rotation: on the running sum's side.
            float dot=0;for(int i=3;i<7;i++)dot+=sum[i]*c.Data[ia+i];float sign=dot<0?-1:1;
            for(int i=0;i<c.Stride;i++)
            {
                float va=c.Data[ia+i],vb=c.Data[ib+i];
                if(i>=3&&i<7){float qd=0;for(int j=3;j<7;j++)qd+=c.Data[ia+j]*c.Data[ib+j];if(qd<0)vb=-vb;va*=sign;vb*=sign;}
                sum[i]+=weight*(va+(vb-va)*t);
            }
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
