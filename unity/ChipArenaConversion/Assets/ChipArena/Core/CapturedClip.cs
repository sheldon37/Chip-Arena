using System;

namespace ChipArena.UnityConversion
{
    // Port of mocap-clip.ts: channel-major delta/zigzag data, frame-major decoded samples.
    public sealed class CapturedClip
    {
        public const int Channels = 64;
        public readonly string Name, StateName, Source, Data;
        public readonly int Fps, Frames;
        public readonly bool Loop;
        public readonly float[] Marks, Pace;
        private float[] decoded, attackAt, attackMarks;
        private float lastWindup=-1,lastActive=-1,lastRecovery=-1;
        public float Duration => (Frames - 1f) / Fps;
        public CapturedClip(string name, string source, int fps, int frames, bool loop, float[] marks, float[] pace, string data)
        { Name=name; StateName=name.Substring(name.IndexOf('/')+1); Source=source; Fps=fps; Frames=frames; Loop=loop; Marks=marks; Pace=pace; Data=data; }
        public float[] Decode()
        {
            if(decoded!=null) return decoded;
            if(Frames<2||Frames>10000||Fps<1||Fps>240) throw new FormatException("Invalid clip dimensions: "+Name);
            const string alphabet="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
            var values=new float[Frames*Channels]; int index=0;
            for(int c=0;c<Channels;c++)
            {
                int previous=0;
                for(int f=0;f<Frames;f++)
                {
                    int z=0,shift=0,g;
                    do
                    {
                        if(index>=Data.Length||shift>25) throw new FormatException("Truncated clip: "+Name);
                        g=alphabet.IndexOf(Data[index++]); if(g<0) throw new FormatException("Corrupt clip: "+Name);
                        z|=(g&31)<<shift; shift+=5;
                    } while((g&32)!=0);
                    previous=checked(previous+((z&1)!=0?-(z+1)/2:z/2));
                    values[f*Channels+c]=previous/256f;
                }
            }
            if(index!=Data.Length) throw new FormatException("Clip length mismatch: "+Name);
            decoded=values; return values;
        }
        public void Sample(float time,float[] output)
        {
            float[] d=Decode(); float t=Loop?Wrap(time,Duration):Clamp(time,0,Duration);
            float x=t*Fps; int i=Math.Min(Frames-2,(int)x); float u=x-i,u2=u*u,u3=u2*u;
            float w0=-.5f*u3+u2-.5f*u,w1=1.5f*u3-2.5f*u2+1,w2=-1.5f*u3+2*u2+.5f*u,w3=.5f*u3-.5f*u2;
            int a=At(i-1)*Channels,b=At(i)*Channels,c=At(i+1)*Channels,e=At(i+2)*Channels;
            for(int k=0;k<Channels;k++) output[k]=w0*d[a+k]+w1*d[b+k]+w2*d[c+k]+w3*d[e+k];
        }
        private int At(int i) { return Loop?((i%(Frames-1))+(Frames-1))%(Frames-1):Math.Min(Frames-1,Math.Max(0,i)); }
        // Every channel's mean over one authored cycle (a loop's last frame repeats its first): the posture a locomotion clip
        // oscillates about. The cubic interpolation of a periodic key sequence integrates to the mean of its keys.
        private float[] cycleMean;
        public float[] CycleMean()
        {
            if(cycleMean!=null) return cycleMean;
            float[] d=Decode(); int n=Loop?Frames-1:Frames; var m=new float[Channels];
            for(int f=0;f<n;f++) for(int c=0;c<Channels;c++) m[c]+=d[f*Channels+c];
            for(int c=0;c<Channels;c++) m[c]/=n;
            return cycleMean=m;
        }
        public float AttackTime(float age,float windup,float active,float recovery)
        {
            if(attackAt==null||lastWindup!=windup||lastActive!=active||lastRecovery!=recovery)
            {
                int n=Pace.Length+4; attackAt=new float[n]; attackMarks=new float[n];
                attackMarks[0]=Marks[0];
                for(int i=0;i<Pace.Length;i++) { attackAt[i+1]=windup*(i+1)/(Pace.Length+1); attackMarks[i+1]=Pace[i]; }
                attackAt[n-3]=windup; attackAt[n-2]=windup+active; attackAt[n-1]=windup+active+recovery;
                for(int i=0;i<3;i++) attackMarks[n-3+i]=Marks[i+1];
                lastWindup=windup; lastActive=active; lastRecovery=recovery;
            }
            return Retime(attackAt,attackMarks,age);
        }
        // Monotone C1 timing preserves source contact marks at authoritative hit-window boundaries.
        public static float Retime(float[] at,float[] marks,float t)
        {
            int n=at.Length-1; if(t<=at[0]) return marks[0]; if(t>=at[n]) return marks[n];
            int i=0; while(at[i+1]<t) i++;
            // A zero-length knot span (a cut with no windup: the Siren's, whose weapon windup is 0) has no slope; dividing by it
            // made the Hermite tangents and the sampled clip time NaN, which SampleAnimation wrote into every joint.
            float Slope(int k) => at[k+1]!=at[k]?(marks[k+1]-marks[k])/(at[k+1]-at[k]):0;
            float Tangent(int k) { if(k==0) return Slope(0); if(k==n) return Slope(n-1); float a=Slope(k-1),b=Slope(k); return a*b<=0?0:2*a*b/(a+b); }
            float h=at[i+1]-at[i],u=(t-at[i])/h,u2=u*u,u3=u2*u;
            return (2*u3-3*u2+1)*marks[i]+(u3-2*u2+u)*h*Tangent(i)+(-2*u3+3*u2)*marks[i+1]+(u3-u2)*h*Tangent(i+1);
        }
        public static float Wrap(float t,float length) => ((t%length)+length)%length;
        public static float Clamp(float t,float min,float max) => Math.Min(max,Math.Max(min,t));
        public static float Ease(float t) { t=Clamp(t,0,1); return t*t*(3-2*t); }
    }
}
