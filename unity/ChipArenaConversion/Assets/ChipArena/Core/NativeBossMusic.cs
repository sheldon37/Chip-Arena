using System;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeMusicLoop {public double start,end;public int fromBar,toBar;}
    [Serializable] public sealed class NativeMusicSection {public string id;public int fromBar,toBar;}
    [Serializable] public sealed class NativeBossTrack
    {
        public string id,file;public double duration,introEnd;public double[] beats;public int firstDownbeat;public NativeMusicLoop loop;public NativeMusicSection[] sections;
        public double Looped(double time){if(time<loop.end)return time;double span=loop.end-loop.start;return loop.start+((time-loop.start)%span+span)%span;}
        public int Beat(double time){int lo=0,hi=beats.Length-1,found=-1;while(lo<=hi){int mid=(lo+hi)/2;if(beats[mid]<=time){found=mid;lo=mid+1;}else hi=mid-1;}return found;}
        public (int bar,int beat,double phase) Position(double time)
        {int i=Beat(time);if(i<firstDownbeat)return(0,Math.Max(1,i+5-firstDownbeat),0);int rel=i-firstDownbeat;double next=i+1<beats.Length?beats[i+1]:beats[i]+beats[i]-beats[i-1];return(rel/4+1,rel%4+1,Math.Max(0,Math.Min(1,(time-beats[i])/(next-beats[i]))));}
        public string Section(double time){int bar=Position(time).bar;foreach(var s in sections)if(bar>=s.fromBar&&bar<=s.toBar)return s.id;return bar<1?"build":"outro";}
        public (bool active,float warning) Vent(double raw,int group)
        {
            double t=Looped(raw);var pos=Position(t);if(pos.bar<1)return(false,0);bool live(string s)=>s=="lift"||s=="climax";
            int nextIndex=firstDownbeat+pos.bar*4;double next=pos.bar==loop.toBar?loop.start:nextIndex<beats.Length?beats[nextIndex]:double.PositiveInfinity;
            string section=Section(t),nextSection=double.IsInfinity(next)?section:Section(next);
            return(live(section)&&pos.beat==1&&pos.bar%2==group,live(nextSection)&&pos.beat>=3&&(pos.bar+1)%2==group?(float)((pos.beat-3+pos.phase)/2):0);
        }
    }
}
