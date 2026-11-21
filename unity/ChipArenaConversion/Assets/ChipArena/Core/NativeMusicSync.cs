using System;
namespace ChipArena.UnityConversion
{
    // Music seeks and the intro renderer must read the same PCM clock. AudioSource.time on a streamed/compressed clip
    // can report packet boundaries seconds apart; comparing it to the match clock repeatedly rewinds the audible song.
    public sealed class NativeMusicSync
    {
        public const double Drift=.12,SeekInterval=.25;
        private double soughtAt=double.NegativeInfinity,lastDsp=double.NaN,lastTarget,settleUntil;
        public void Reset(){soughtAt=double.NegativeInfinity;lastDsp=double.NaN;settleUntil=0;}
        public static double Position(int sample,int frequency)=>sample>=0&&frequency>0?sample/(double)frequency:-1;
        public static int Sample(double seconds,int frequency,int samples)
        {
            if(!(seconds>0)||frequency<=0||samples<=0)return 0;
            return (int)Math.Min(samples-1,Math.Floor(seconds*frequency));
        }
        // Give the audio thread time to consume a seek before another correction. A late load starts at the current
        // authoritative position; packet-sized readbacks must never provoke Play/seek on every rendered frame.
        // A capped simulation clock loses time during rendering stalls while the DSP clock keeps playing.
        // Do not turn that interval into repeated audible rewinds. Resume correction after .75 s of stable timing.
        // Authored edits / loop wraps are discontinuities, and must still seek immediately.
        public bool SeekStable(double playback,double target,double dspTime)
        {
            if(!double.IsNaN(lastDsp))
            {
                double elapsed=dspTime-lastDsp,advance=target-lastTarget;
                bool edit=advance<-.5||advance-elapsed>1;
                if(edit){lastDsp=dspTime;lastTarget=target;settleUntil=0;return Seek(playback,target,dspTime);}
                if(elapsed<0||elapsed>.15||Math.Abs(advance-elapsed)>.1)settleUntil=dspTime+.75;
            }
            lastDsp=dspTime;lastTarget=target;
            return dspTime>=settleUntil&&Seek(playback,target,dspTime);
        }
        public bool Seek(double playback,double target,double dspTime)
        {
            if(!(target>=0)||double.IsInfinity(target)||Math.Abs(playback-target)<=Drift||dspTime-soughtAt<SeekInterval)return false;
            soughtAt=dspTime;return true;
        }
    }
}
