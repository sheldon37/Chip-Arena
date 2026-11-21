using System;
using System.IO;

namespace ChipArena.UnityConversion
{
    /// <summary>Interleaved normalized bands at a fixed sample rate, sampled at the shared song clock.
    /// Music volume and device output have no effect. Gameplay never consumes these values.</summary>
    public sealed class NativeMusicEnvelope
    {
        public readonly struct Bands
        {
            public readonly float Bass,Body,Air,Attack;
            public Bands(float bass,float body,float air,float attack)
            {Bass=bass;Body=body;Air=air;Attack=attack;}
        }
        private readonly byte[] data;
        private readonly int count,hz;
        public readonly float Duration;
        public NativeMusicEnvelope(byte[] bytes)
        {
            using(var reader=new BinaryReader(new MemoryStream(bytes,false)))
            {
                if(reader.ReadUInt32()!=0x3143534d)throw new InvalidDataException("Unknown music envelope");
                count=reader.ReadInt32();hz=reader.ReadInt32();Duration=reader.ReadSingle();
                if(count<1||hz<1||hz>200||!MathUtil.Finite(Duration)||Duration<=0||Math.Abs(count-Duration*hz)>2||bytes.Length!=16+4L*count)
                    throw new InvalidDataException("Invalid music envelope dimensions");
            }
            data=bytes;
        }
        public Bands At(double clock)
        {
            double x=(Math.Max(0,clock)%Duration)*hz;
            int i=Math.Min((int)x,count-1),a=16+4*i,b=i+1<count?a+4:16;float f=(float)(x-Math.Floor(x));
            float Band(int k)=>(data[a+k]+(data[b+k]-data[a+k])*f)/255f;
            return new Bands(Band(0),Band(1),Band(2),Band(3));
        }
    }
}
